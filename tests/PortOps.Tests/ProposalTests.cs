using PortOps.Agent;
using PortOps.Domain;

namespace PortOps.Tests;

public sealed class ProposalTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "portops-proposal-tests", Guid.NewGuid().ToString("N"));
    private readonly MutableClock actionClock = new(new(2026, 9, 8, 10, 0, 0, TimeSpan.Zero));
    private readonly TerminalSnapshot snapshot = DemoTerminal.Create();
    private readonly ProposalActor writer = new("operator-1", "northstar");
    private readonly ProposalActor reviewer = new("reviewer-1", "northstar", true);
    private ProposalStore store;
    private readonly string path;
    private ProposalService Service(TerminalSnapshot? data = null) => new(Operations(data), new(), store, actionClock);
    private OperationsService Operations(TerminalSnapshot? data = null) => new(data ?? snapshot, new ScenarioClock(snapshot.ScenarioTime), new());

    public ProposalTests()
    {
        path = Path.Combine(directory, "proposals.json");
        store = new(path);
    }

    [Fact]
    public void Draft_uses_operational_sources_and_wall_clock_expiry_without_execution()
    {
        var draft = Service().Create(writer, "DEMO-003");
        Assert.Equal("draft", draft.Status);
        Assert.Null(draft.Delivery);
        Assert.Single(draft.Audit);
        Assert.Contains("afronding onbekend", draft.Body);
        Assert.Contains("2026-09-07 10:00", draft.Body);
        Assert.Contains(draft.Evidence, e => e.Id == "e-h003");
        Assert.Contains(draft.Evidence, e => e.Id == "proc-hold-v1");
        Assert.Equal(actionClock.Now.AddMinutes(30), draft.ExpiresAt);
        Assert.EndsWith("@example.invalid", draft.Destination);
    }

    [Fact]
    public void Ordinary_identity_cannot_approve_even_with_exact_payload()
    {
        var p = Service().Create(writer, "DEMO-003");
        Assert.Equal("forbidden", Assert.Throws<ProposalFailure>(() => Service().Decide(writer, p.Id, p.Version, p.PayloadHash, true)).Code);
        Assert.Equal("draft", Assert.Single(Service().List("northstar")).Status);
    }

    [Fact]
    public void Cross_customer_records_cannot_be_created_refreshed_or_approved()
    {
        Assert.Equal("not_found", Assert.Throws<ProposalFailure>(() => Service().Create(writer, "DEMO-007")).Code);
        var p = Service().Create(writer, "DEMO-003");
        var other = new ProposalActor("other", "harborline", true);
        Assert.Empty(Service().List("harborline"));
        Assert.Equal("not_found", Assert.Throws<ProposalFailure>(() => Service().Decide(other, p.Id, 1, p.PayloadHash, true)).Code);
        Assert.Equal("not_found", Assert.Throws<ProposalFailure>(() => Service().Refresh(other, p.Id, 1)).Code);
    }

    [Fact]
    public void Concurrent_approval_and_restart_preserve_exactly_one_simulated_delivery()
    {
        var p = Service().Create(writer, "DEMO-003");
        var results = Enumerable.Range(0, 12).AsParallel().Select(_ => Service().Decide(reviewer, p.Id, 1, p.PayloadHash, true)).ToArray();
        Assert.Single(results.Select(p => p.Delivery!.Id).Distinct());
        var saved = results[0];
        Assert.Equal(new[] { "created", "approved", "simulated_delivery" }, saved.Audit.Select(a => a.Event));
        Assert.Equal("reviewer-1", saved.Audit[1].ActorId);
        store.Dispose(); store = new(path);
        actionClock.Now = actionClock.Now.AddHours(2);
        var retried = Service().Decide(reviewer, p.Id, 1, p.PayloadHash, true);
        Assert.Equal(saved.Delivery, retried.Delivery);
        Assert.Equal(saved.Body, retried.Delivery!.Body);
        Assert.Equal(3, retried.Audit.Count);
    }

    [Fact]
    public void Expired_draft_requires_new_version_and_renewed_review()
    {
        var p = Service().Create(writer, "DEMO-003");
        actionClock.Now = p.ExpiresAt;
        Assert.Equal("expired", Assert.Single(Service().List("northstar")).Status);
        Assert.Equal("expired", Assert.Throws<ProposalFailure>(() => Service().Decide(reviewer, p.Id, 1, p.PayloadHash, true)).Code);
        var fresh = Service().Refresh(writer, p.Id, 1);
        Assert.Equal(2, fresh.Version);
        Assert.Equal("conflict", Assert.Throws<ProposalFailure>(() => Service().Decide(reviewer, p.Id, 1, p.PayloadHash, true)).Code);
        Assert.Equal("executed", Service().Decide(reviewer, p.Id, 2, fresh.PayloadHash, true).Status);
    }

    [Fact]
    public void Changed_operational_data_invalidates_review_until_refreshed()
    {
        var p = Service().Create(writer, "DEMO-003");
        var changed = snapshot with { Vehicles = snapshot.Vehicles.Select(v => v.Id == "DEMO-003"
            ? v with { ActiveHolds = [] } : v).ToArray() };
        Assert.Equal("stale", Assert.Throws<ProposalFailure>(() => Service(changed).Decide(reviewer, p.Id, 1, p.PayloadHash, true)).Code);
        var fresh = Service(changed).Refresh(writer, p.Id, 1);
        Assert.NotEqual(p.PayloadHash, fresh.PayloadHash);
        Assert.Equal("conflict", Assert.Throws<ProposalFailure>(() => Service(changed).Decide(reviewer, p.Id, 2, p.PayloadHash, true)).Code);
        Assert.Equal("executed", Service(changed).Decide(reviewer, p.Id, 2, fresh.PayloadHash, true).Status);
    }

    [Fact]
    public void Payload_tampering_is_detected_and_rejection_never_executes()
    {
        var p = Service().Create(writer, "DEMO-003");
        store.Write(p.Id, old => old! with { Body = "Changed without renewed review" });
        Assert.Equal("stale", Assert.Throws<ProposalFailure>(() => Service().Decide(reviewer, p.Id, 1, p.PayloadHash, true)).Code);
        var rejected = Service().Decide(reviewer, p.Id, 1, p.PayloadHash, false);
        Assert.Equal("rejected", rejected.Status);
        Assert.Null(rejected.Delivery);
        Assert.Equal("conflict", Assert.Throws<ProposalFailure>(() => Service().Decide(reviewer, p.Id, 1, p.PayloadHash, true)).Code);
    }

    [Fact]
    public void Failed_persistence_does_not_publish_an_in_memory_draft()
    {
        Directory.CreateDirectory(path); // Deliberately prevent replacing the snapshot file.
        Assert.ThrowsAny<IOException>(() => Service().Create(writer, "DEMO-003"));
        Assert.Empty(Service().List("northstar"));
    }

    [Fact]
    public void Second_store_owner_is_rejected()
    {
        Assert.ThrowsAny<IOException>(() => new ProposalStore(path));
    }

    [Fact]
    public async Task Agent_can_retrieve_a_procedure_and_propose_but_never_approve()
    {
        var ops = Operations();
        var model = new ScriptedModel(
            ScriptedModel.Call(name: "search_procedures", arguments: "{\"query\":\"damage\"}"),
            ScriptedModel.Call("call2", "propose_notification"),
            ScriptedModel.Answer("proc-hold-v1", "Controleer de geregistreerde blokkade en vraag de verantwoordelijke om opvolging."));
        var result = await new AgentRunner(model, new(ops, new(), Service()), ops, ScriptedModel.Options())
            .RunAsync("northstar", new("Maak een voorstel voor DEMO-003"), default);
        Assert.Equal("draft", Assert.Single(result.Proposals!).Status);
        Assert.Null(Assert.Single(Service().List("northstar")).Delivery);
        Assert.DoesNotContain(OperationalTools.Definitions.EnumerateArray(), t => t.GetProperty("name").GetString()!.Contains("approve"));
    }

    public void Dispose() { store.Dispose(); Directory.Delete(directory, true); }
    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
