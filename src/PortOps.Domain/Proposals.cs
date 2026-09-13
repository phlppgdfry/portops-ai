using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PortOps.Domain;

public sealed record ProposalActor(string Id, string CustomerId, bool CanApprove = false);
public sealed record ProposalAudit(string Event, string ActorId, DateTimeOffset At, int Version);
public sealed record SimulatedDelivery(string Id, DateTimeOffset At, string Destination, string Subject, string Body);
public sealed record ActionProposal(string Id, string CustomerId, string VehicleId, int Version,
    string Status, string Destination, string Subject, string Body, IReadOnlyList<Evidence> Evidence,
    string StateHash, string PayloadHash, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt,
    IReadOnlyList<ProposalAudit> Audit, SimulatedDelivery? Delivery = null);
public sealed class ProposalFailure(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>Single-process local demo store. Atomic snapshots; no external delivery adapter is invoked.</summary>
public sealed class ProposalStore : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }, WriteIndented = true
    };
    private readonly object gate = new();
    private readonly string path;
    private readonly FileStream ownership;
    private Dictionary<string, ActionProposal> records;

    public ProposalStore(string path)
    {
        this.path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(this.path)!);
        // Fail closed when another process owns the store rather than risk overwriting its approvals.
        ownership = new FileStream(this.path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            records = File.Exists(this.path)
                ? JsonSerializer.Deserialize<Dictionary<string, ActionProposal>>(File.ReadAllText(this.path), Json)
                    ?? throw new InvalidDataException("Proposal store is empty or invalid.")
                : [];
        }
        catch { ownership.Dispose(); throw; }
    }

    public IReadOnlyList<ActionProposal> List(string customerId)
    {
        lock (gate) return records.Values.Where(p => p.CustomerId == customerId)
            .OrderByDescending(p => p.CreatedAt).ToArray();
    }

    public ActionProposal Write(string? id, Func<ActionProposal?, ActionProposal> change)
    {
        using var activity = DomainDiagnostics.Activities.StartActivity("proposal.persist");
        try { return WriteCore(id, change); }
        catch (ProposalFailure failure) { activity?.SetStatus(ActivityStatusCode.Error); activity?.SetTag("error.type", failure.Code); throw; }
        catch { activity?.SetStatus(ActivityStatusCode.Error); activity?.SetTag("error.type", "unexpected"); throw; }
    }

    private ActionProposal WriteCore(string? id, Func<ActionProposal?, ActionProposal> change)
    {
        lock (gate)
        {
            var next = change(id is not null ? records.GetValueOrDefault(id) : null);
            var updated = new Dictionary<string, ActionProposal>(records) { [next.Id] = next };
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    JsonSerializer.Serialize(file, updated, Json);
                    file.Flush(flushToDisk: true);
                }
                File.Move(temporary, path, overwrite: true);
                records = updated;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return next;
        }
    }

    public void Dispose() => ownership.Dispose();
}

public sealed class ProposalService(OperationsService operations, ProcedureCatalog procedures,
    ProposalStore store, TimeProvider actionClock)
{
    public IReadOnlyList<ActionProposal> List(string customerId) => store.List(customerId)
        .Select(p => p.Status == "draft" && p.ExpiresAt <= actionClock.GetUtcNow() ? p with { Status = "expired" } : p).ToArray();

    public ActionProposal Create(ProposalActor actor, string vehicleId) => Observe("proposal.create", () => store.Write(null, _ =>
    {
        if (store.List(actor.CustomerId).Count >= 500)
            throw new ProposalFailure("capacity", "The local demo proposal limit has been reached.");
        return Build(actor, vehicleId);
    }));

    public ActionProposal Refresh(ProposalActor actor, string id, int version) => Observe("proposal.refresh", () => store.Write(id, current =>
    {
        var previous = Scoped(actor, current, version);
        if (previous.Status != "draft") throw new ProposalFailure("conflict", "Only drafts can be refreshed.");
        var next = Build(actor, previous.VehicleId);
        return next with { Id = previous.Id, Version = previous.Version + 1, CreatedAt = previous.CreatedAt,
            Audit = [.. previous.Audit, new("refreshed", actor.Id, actionClock.GetUtcNow(), previous.Version + 1)] };
    }));

    public ActionProposal Decide(ProposalActor actor, string id, int version, string payloadHash, bool approve) => Observe(approve ? "proposal.approve" : "proposal.reject", () => store.Write(id, current =>
    {
        if (!actor.CanApprove) throw new ProposalFailure("forbidden", "A reviewer identity is required.");
        var p = Scoped(actor, current, version);
        if (p.PayloadHash != payloadHash) throw new ProposalFailure("conflict", "The reviewed payload has changed. Reload and review again.");
        // Retrying an already-completed decision returns the same delivery, even after expiry.
        if ((approve && p.Status == "executed") || (!approve && p.Status == "rejected")) return p;
        if (p.Status != "draft") throw new ProposalFailure("conflict", "This proposal is already closed.");
        if (p.ExpiresAt <= actionClock.GetUtcNow()) throw new ProposalFailure("expired", "The proposal expired. Refresh and review again.");
        var now = actionClock.GetUtcNow();
        if (!approve) return p with { Status = "rejected", Audit = [.. p.Audit, new("rejected", actor.Id, now, version)] };
        var vehicle = Vehicle(actor.CustomerId, p.VehicleId);
        if (StateHash(actor.CustomerId, vehicle) != p.StateHash || HashPayload(p.Destination, p.Subject, p.Body) != p.PayloadHash)
            throw new ProposalFailure("stale", "Operational data or the payload changed. Refresh and review again.");
        // Approval and simulated delivery are persisted in ONE snapshot. A crash/retry cannot send an email.
        return p with
        {
            Status = "executed",
            Delivery = new("sim-" + Guid.NewGuid().ToString("N"), now, p.Destination, p.Subject, p.Body),
            Audit = [.. p.Audit, new("approved", actor.Id, now, version), new("simulated_delivery", actor.Id, now, version)]
        };
    }));

    private static ActionProposal Observe(string name, Func<ActionProposal> action)
    {
        using var activity = DomainDiagnostics.Activities.StartActivity(name);
        try { var result = action(); activity?.SetTag("proposal.outcome", result.Status); return result; }
        catch (ProposalFailure failure) { activity?.SetStatus(ActivityStatusCode.Error); activity?.SetTag("error.type", failure.Code); throw; }
        catch { activity?.SetStatus(ActivityStatusCode.Error); activity?.SetTag("error.type", "unexpected"); throw; }
    }

    private ActionProposal Build(ProposalActor actor, string vehicleId)
    {
        var vehicle = Vehicle(actor.CustomerId, vehicleId);
        var docs = procedures.ForVehicle(actor.CustomerId, vehicle);
        var sources = vehicle.Pickup.Evidence.Concat(vehicle.Loading.Evidence)
            .Concat(docs.Select(d => d.Evidence)).DistinctBy(e => e.Id).ToArray();
        var destination = actor.CustomerId + "-operations@example.invalid";
        var subject = $"Concept statusopvolging {vehicleId} — fictieve demo";
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels");
        var scenarioTime = TimeZoneInfo.ConvertTime(operations.Now, zone).ToString("yyyy-MM-dd HH:mm");
        var holds = vehicle.Vehicle.ActiveHolds.Count == 0 ? "Geen actieve blokkades geregistreerd." :
            string.Join("\n", vehicle.Vehicle.ActiveHolds.Select(h => $"- {Dutch(h.Reason)}; verantwoordelijke: {Dutch(h.Owner)}; " +
                (h.EstimatedCompletion is { } estimate ? $"geschatte afronding {TimeZoneInfo.ConvertTime(estimate, zone):yyyy-MM-dd HH:mm} (Brussel), geen vrijgave." : "afronding onbekend.")));
        var warnings = vehicle.Pickup.Warnings.Concat(vehicle.Loading.Warnings).Distinct().ToArray();
        var body = $"Fictieve terminalstatus op {scenarioTime} (Brussel, scenariotijd).\n\n" +
            $"Voertuig: {vehicleId}\nAfhaling: {Label(vehicle.Pickup.State)}\nLaden: {Label(vehicle.Loading.State)}\n\n" + holds +
            (warnings.Length > 0 ? "\n\nBronwaarschuwingen:\n" + string.Join("\n", warnings.Select(Dutch)) : "") +
            "\n\nGraag de actuele status en eventuele openstaande beperkingen controleren. Dit concept bevestigt geen afhaalafspraak of vrijgavetijd." +
            "\n\nFictieve werkinstructies:\n" + string.Join("\n", docs.Select(d => $"{d.Title} (v{d.Version}): {d.Text}")) +
            "\n\nBronnen: " + string.Join(", ", sources.Select(e => e.Id)) + "\n\nDemo: bij goedkeuring wordt uitsluitend een gesimuleerde verzending vastgelegd.";
        var now = actionClock.GetUtcNow();
        return new(Guid.NewGuid().ToString("N"), actor.CustomerId, vehicleId, 1, "draft", destination, subject, body,
            sources, StateHash(actor.CustomerId, vehicle), HashPayload(destination, subject, body), now, now.AddMinutes(30),
            [new("created", actor.Id, now, 1)]);
    }

    private VehicleInvestigation Vehicle(string customerId, string vehicleId) => operations.GetVehicle(customerId, vehicleId)
        ?? throw new ProposalFailure("not_found", "No accessible record found.");
    private string StateHash(string customerId, VehicleInvestigation vehicle) => Hash(JsonSerializer.Serialize(new
    {
        vehicle, booking = operations.GetBookings(customerId).Single(b => b.Id == vehicle.Vehicle.BookingId),
        procedures = procedures.ForVehicle(customerId, vehicle)
    }));
    private static ActionProposal Scoped(ProposalActor actor, ActionProposal? p, int version)
    {
        if (p is null || p.CustomerId != actor.CustomerId) throw new ProposalFailure("not_found", "No accessible record found.");
        if (p.Version != version) throw new ProposalFailure("conflict", "The proposal version changed. Reload and review again.");
        return p;
    }
    private static string Dutch(string value) => value switch
    {
        "Damage assessment pending" => "Schadebeoordeling nog open",
        "Pre-delivery inspection incomplete" => "Inspectie vóór aflevering nog niet afgerond",
        "Pickup release check pending" => "Controle voor afhaalvrijgave nog open",
        "damage-team" => "Schadeteam", "vehicle-processing" => "Voertuigbewerking", "release-desk" => "Vrijgavebalie",
        "Stale status observations were excluded from readiness decisions." => "Verouderde statusgegevens tellen niet mee bij de beoordeling van de gereedheid.",
        "Future-dated status observations were excluded from readiness decisions." => "Statusgegevens met een toekomstige waarnemingstijd tellen niet mee bij de beoordeling.",
        "A ready observation conflicts with an unresolved hold; the hold prevents release." => "Een bron meldt gereed, maar er staat nog een blokkade open. Die blokkade verhindert de vrijgave.",
        _ => value
    };
    private static string Label(ReadinessState state) => state switch
    {
        ReadinessState.Ready => "Gereed", ReadinessState.Blocked => "Geblokkeerd",
        ReadinessState.Conflicting => "Tegenstrijdige bronnen", _ => "Onbekend"
    };
    private static string HashPayload(string destination, string subject, string body) => Hash(JsonSerializer.Serialize(new { destination, subject, body }));
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
