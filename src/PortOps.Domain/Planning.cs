using System.Diagnostics;

namespace PortOps.Domain;

public sealed record InventoryPosition(string CustomerId, string VehicleId, Evidence Evidence);
public sealed record InboundPlan(string Id, string CustomerId, string VesselCallId, int? ExpectedVehicles,
    DateTimeOffset ArrivalInPlanning, Evidence Evidence);
public sealed record PlanningSnapshot(IReadOnlyList<InventoryPosition> Inventory, IReadOnlyList<InboundPlan> Inbound);
public sealed record PlanningStock(string VehicleId, string BookingId, ReadinessState Pickup, ReadinessState Loading,
    DateTimeOffset? LoadingDeadline, string DeadlineStatus, bool InventoryCurrent, IReadOnlyList<Hold> Holds,
    IReadOnlyList<string> Warnings, IReadOnlyList<Evidence> Evidence);
public sealed record PlanningArrival(string PlanId, string VesselCallId, string Vessel, DateTimeOffset EstimatedArrival,
    int? ExpectedVehicles, string VolumeStatus, DateTimeOffset? ArrivalInPlanning,
    IReadOnlyList<string> Warnings, IReadOnlyList<Evidence> Evidence);
public sealed record PlanningOverview(DateTimeOffset EvaluatedAt, DateTimeOffset WindowEnd, int HorizonDays,
    int InventoryCount, int ReadyForPickup, int VehiclesWithHolds, int OpenHolds, int VehiclesNeedingDataReview,
    int ExpectedVesselCalls, long KnownInboundVehicles, int InboundPlansWithUnknownVolume,
    IReadOnlyList<PlanningStock> Stock, IReadOnlyList<PlanningArrival> Arrivals,
    IReadOnlyList<string> Warnings, IReadOnlyList<Evidence> Evidence);

public static class DemoPlanning
{
    public static PlanningSnapshot Create(DateTimeOffset now)
    {
        Evidence Source(string id, string text) => new(id, "synthetic-planning-v1", now.AddHours(-1), text);
        // Explicit inventory membership: incoming planned volumes are never inserted into this snapshot.
        var stock = new[] { "DEMO-001", "DEMO-002", "DEMO-003", "DEMO-004", "DEMO-005", "DEMO-006" }
            .Select(id => new InventoryPosition("northstar", id, Source("stock-" + id, $"Fictieve voorraadopname: {id} staat op de terminal.")))
            .Append(new("harborline", "DEMO-007", Source("stock-DEMO-007", "Fictieve voorraadopname: DEMO-007 staat op de terminal."))).ToArray();
        return new(stock,
        [
            new("IN-101", "northstar", "CALL-01", 12, now.AddHours(-2),
                Source("e-in101", "Fictief aankomstplan IN-101: 12 nieuwe voertuigen verwacht. De oudere planning noemt 06:00 UTC; dit zijn geen voertuigen uit de huidige voorraad.")),
            new("IN-102", "northstar", "CALL-03", null, now.AddHours(40),
                Source("e-in102", "Fictief aankomstplan IN-102: voertuigvolume nog niet aangeleverd. Onbekend is niet nul.")),
            new("IN-201", "harborline", "CALL-04", 5, now.AddHours(2),
                Source("e-in201", "Fictief aankomstplan IN-201: 5 nieuwe Harborline-voertuigen verwacht."))
        ]);
    }
}

public sealed class PlanningService(OperationsService operations, PlanningSnapshot snapshot)
{
    public PlanningOverview GetOverview(string customerId, int horizonDays = 7)
    {
        if (horizonDays is < 1 or > 14) throw new ArgumentOutOfRangeException(nameof(horizonDays));
        using var activity = DomainDiagnostics.Activities.StartActivity("planning.overview");
        var now = operations.Now;
        var end = now.AddDays(horizonDays);
        var warnings = new List<string>();
        var stock = new List<PlanningStock>();
        var bookings = operations.GetBookings(customerId).ToDictionary(b => b.Id);
        foreach (var group in snapshot.Inventory.Where(p => p.CustomerId == customerId).GroupBy(p => p.VehicleId))
        {
            var vehicle = operations.GetVehicle(customerId, group.Key);
            if (vehicle is null || !bookings.TryGetValue(vehicle.Vehicle.BookingId, out var booking))
            {
                warnings.Add("Een voorraadverwijzing kon niet binnen deze klantomgeving worden gecontroleerd; deze telt niet mee.");
                continue;
            }
            var entries = group.Where(p => p.Evidence.ObservedAt <= now).OrderByDescending(p => p.Evidence.ObservedAt).ToArray();
            if (entries.Length == 0)
            {
                warnings.Add("Een voorraadverwijzing heeft uitsluitend een toekomstige bron; deze telt niet mee.");
                continue;
            }
            var notes = new List<string>();
            if (group.Count() > 1) notes.Add("Dubbele voorraadverwijzing: het voertuig telt eenmaal mee.");
            var inventoryCurrent = now - entries[0].Evidence.ObservedAt <= ReadinessPolicy.MaximumObservationAge;
            if (!inventoryCurrent)
                notes.Add("Voorraadopname is verouderd; huidige aanwezigheid opnieuw controleren.");
            if (vehicle.Pickup.State == ReadinessState.Conflicting || vehicle.Loading.State == ReadinessState.Conflicting)
                notes.Add("Statusbronnen spreken elkaar tegen; laat de verantwoordelijke operator dit controleren.");
            if (vehicle.Pickup.State == ReadinessState.Unknown || vehicle.Loading.State == ReadinessState.Unknown)
                notes.Add("Een actuele gereedheidsstatus ontbreekt.");
            if (vehicle.Pickup.Warnings.Count + vehicle.Loading.Warnings.Count > 0)
                notes.Add("Gereedheidsbeoordeling bevat bronwaarschuwingen; bekijk het voertuigonderzoek.");
            var deadlineStatus = booking.LoadingDeadline is not { } deadline ? "not_set"
                : deadline <= now ? "elapsed" : deadline <= end ? "within_window" : "outside_window";
            stock.Add(new(vehicle.Vehicle.Id, booking.Id, vehicle.Pickup.State, vehicle.Loading.State,
                booking.LoadingDeadline, deadlineStatus, inventoryCurrent, vehicle.Vehicle.ActiveHolds, notes,
                entries.Select(p => p.Evidence).Concat(vehicle.Pickup.Evidence).Concat(vehicle.Loading.Evidence)
                    .Append(booking.Evidence).DistinctBy(e => e.Id).ToArray()));
        }
        var arrivals = new List<PlanningArrival>();
        foreach (var group in snapshot.Inbound.Where(p => p.CustomerId == customerId).GroupBy(p => p.Id))
        {
            // Select the latest observable plan version. Conflicting latest versions are not summed or silently chosen.
            var observable = group.Where(p => p.Evidence.ObservedAt <= now).ToArray();
            if (observable.Length == 0) { warnings.Add("Een aankomstplan heeft uitsluitend een toekomstige bron en is uitgesloten."); continue; }
            var latestAt = observable.Max(p => p.Evidence.ObservedAt);
            var versions = observable.Where(p => p.Evidence.ObservedAt == latestAt).ToArray();
            var callIds = versions.Select(p => p.VesselCallId).Distinct().ToArray();
            if (callIds.Length != 1) { warnings.Add("Tegenstrijdige scheepsreferenties voor een aankomstplan; uitgesloten uit de volumetelling."); continue; }
            var call = operations.GetVesselCall(customerId, callIds[0]);
            if (call is null) { warnings.Add("Een scheepsreferentie kon niet binnen deze klantomgeving worden gecontroleerd; het aankomstplan is uitgesloten."); continue; }
            // Arrived calls do not establish that their planned cargo has entered inventory.
            if (call.ActualArrival is { } actual && actual <= now) continue;
            if (call.DischargeCompleted is { } discharged && discharged <= now) continue;
            if (call.EstimatedArrival > end) continue;
            var notes = new List<string>();
            if (call.EstimatedArrival < now) notes.Add("Aankomstverwachting verstreken zonder geregistreerde aankomst; opvolging nodig.");
            if (call.Evidence.ObservedAt > now || (call.ActualArrival is { } futureActual && futureActual > now)
                || (call.DischargeCompleted is { } futureDischarge && futureDischarge > now))
            { warnings.Add("Een aankomstplan bevat toekomstige scheepsobservaties; uitgesloten uit de volumetelling."); continue; }
            var stale = now - latestAt > ReadinessPolicy.MaximumObservationAge || now - call.Evidence.ObservedAt > ReadinessPolicy.MaximumObservationAge;
            if (stale) notes.Add("Planning of scheepsbron is verouderd; volume niet opgenomen in het bekende totaal.");
            var first = versions[0];
            var planningTimes = versions.Select(v => v.ArrivalInPlanning).Distinct().ToArray();
            if (planningTimes.Length > 1) notes.Add("Planningsbronnen noemen verschillende aankomsttijden; geen oorspronkelijke tijd gekozen.");
            if (versions.Any(v => v.ArrivalInPlanning != call.EstimatedArrival))
                notes.Add("Aankomsttijd in de planning wijkt af van de actuele scheepsverwachting. De scheepsverwachting bepaalt dit venster.");
            var volumes = versions.Select(v => v.ExpectedVehicles).Distinct().ToArray();
            var volumeStatus = stale ? "stale" : volumes.Length != 1 ? "conflicting"
                : volumes[0] is null ? "unknown" : volumes[0] < 0 ? "invalid" : "known";
            if (volumeStatus == "conflicting") notes.Add("Bronnen voor hetzelfde aankomstplan noemen verschillende aantallen; geen aantal gekozen.");
            if (volumeStatus == "invalid") notes.Add("Ongeldig negatief voertuigvolume; aantal niet meegeteld.");
            arrivals.Add(new(first.Id, call.Id, call.Vessel, call.EstimatedArrival,
                volumeStatus == "known" ? volumes[0] : null, volumeStatus, planningTimes.Length == 1 ? planningTimes[0] : null, notes,
                versions.Select(v => v.Evidence).Append(call.Evidence).DistinctBy(e => e.Id).ToArray()));
        }
        var orderedStock = stock.OrderBy(s => s.VehicleId).ToArray();
        var orderedArrivals = arrivals.OrderBy(a => a.EstimatedArrival).ThenBy(a => a.PlanId).ToArray();
        var ready = stock.Count(s => s.Pickup == ReadinessState.Ready && s.InventoryCurrent);
        var summary = new Evidence($"planning-summary-{horizonDays}", "portops-planning-rules-v1", now,
            $"Fictieve planning: {stock.Count} unieke voertuigen volgens voorraadopname; {ready} gereed voor afhaling met actuele voorraadopname; " +
            $"{arrivals.Sum(a => (long)(a.ExpectedVehicles ?? 0))} inkomende voertuigen uit plannen met bekend actueel volume; " +
            $"{arrivals.Count(a => a.ExpectedVehicles is null)} aankomstplannen zonder bruikbaar volume. Inkomende plannen zijn geen huidige voorraad of voorraadprognose.");
        return new(now, end, horizonDays, stock.Count, ready,
            stock.Count(s => s.Holds.Count > 0), stock.Sum(s => s.Holds.Count), stock.Count(s => s.Warnings.Count > 0),
            arrivals.Select(a => a.VesselCallId).Distinct().Count(), arrivals.Sum(a => (long)(a.ExpectedVehicles ?? 0)),
            arrivals.Count(a => a.ExpectedVehicles is null), orderedStock, orderedArrivals,
            warnings.Distinct().ToArray(), stock.SelectMany(s => s.Evidence).Concat(arrivals.SelectMany(a => a.Evidence))
                .Append(summary).DistinctBy(e => e.Id).ToArray());
    }
}
