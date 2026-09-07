using PortOps.Domain;

namespace PortOps.Tests;

public sealed class ReadinessTests
{
    private readonly TerminalSnapshot snapshot = DemoTerminal.Create();
    private readonly ReadinessPolicy policy = new();

    [Theory]
    [InlineData("DEMO-001", ReadinessState.Ready, ReadinessState.Ready)]
    [InlineData("DEMO-002", ReadinessState.Blocked, ReadinessState.Blocked)]
    [InlineData("DEMO-003", ReadinessState.Blocked, ReadinessState.Blocked)]
    [InlineData("DEMO-004", ReadinessState.Blocked, ReadinessState.Ready)]
    [InlineData("DEMO-005", ReadinessState.Ready, ReadinessState.Conflicting)]
    [InlineData("DEMO-006", ReadinessState.Unknown, ReadinessState.Unknown)]
    public void Fixture_readiness_matches_operational_expectations(string id, ReadinessState pickup, ReadinessState loading)
    {
        var vehicle = snapshot.Vehicles.Single(x => x.Id == id);
        Assert.Equal(pickup, policy.Assess(vehicle, ReadinessKind.Pickup, snapshot.ScenarioTime).State);
        Assert.Equal(loading, policy.Assess(vehicle, ReadinessKind.Loading, snapshot.ScenarioTime).State);
    }

    [Fact]
    public void Discharge_alone_never_establishes_pickup_release()
    {
        var vehicle = snapshot.Vehicles.Single(x => x.Id == "DEMO-004") with { ActiveHolds = [], Observations = [] };
        Assert.NotEmpty(vehicle.Events);
        Assert.Equal(ReadinessState.Unknown, policy.Assess(vehicle, ReadinessKind.Pickup, snapshot.ScenarioTime).State);
    }

    [Fact]
    public void An_estimated_completion_in_the_past_does_not_close_a_hold()
    {
        var vehicle = snapshot.Vehicles.Single(x => x.Id == "DEMO-002");
        var result = policy.Assess(vehicle, ReadinessKind.Pickup, snapshot.ScenarioTime.AddHours(3));
        Assert.Equal(ReadinessState.Blocked, result.State);
        Assert.Contains(result.Evidence, e => e.Id == "e-h002");
    }

    [Fact]
    public void Active_hold_overrides_ready_flag_and_exposes_disagreement()
    {
        var vehicle = snapshot.Vehicles.Single(x => x.Id == "DEMO-002");
        vehicle = vehicle with { Observations = vehicle.Observations.Select(x => x with { Ready = true }).ToArray() };
        var result = policy.Assess(vehicle, ReadinessKind.Pickup, snapshot.ScenarioTime);
        Assert.Equal(ReadinessState.Blocked, result.State);
        Assert.Contains(result.Warnings, w => w.Contains("conflicts"));
    }

    [Fact]
    public void Conflicting_current_sources_are_both_cited()
    {
        var result = policy.Assess(snapshot.Vehicles.Single(x => x.Id == "DEMO-005"), ReadinessKind.Loading, snapshot.ScenarioTime);
        Assert.Equal(ReadinessState.Conflicting, result.State);
        Assert.Equal(new[] { "s-005l", "s-005plan" }, result.Evidence.Select(x => x.Id));
    }

    [Fact]
    public void New_record_replaces_old_record_from_same_source()
    {
        var vehicle = snapshot.Vehicles[0] with { Observations = [
            new(ReadinessKind.Pickup, false, new("old", "tos", snapshot.ScenarioTime.AddHours(-2), "Not ready")),
            new(ReadinessKind.Pickup, true, new("new", "tos", snapshot.ScenarioTime.AddHours(-1), "Released"))
        ] };
        var result = policy.Assess(vehicle, ReadinessKind.Pickup, snapshot.ScenarioTime);
        Assert.Equal(ReadinessState.Ready, result.State);
        Assert.Equal("new", Assert.Single(result.Evidence).Id);
    }

    [Theory]
    [InlineData(-6, ReadinessState.Ready)]
    [InlineData(-7, ReadinessState.Unknown)]
    [InlineData(1, ReadinessState.Unknown)]
    public void Freshness_boundary_and_future_records_are_handled(int offsetHours, ReadinessState expected)
    {
        var vehicle = snapshot.Vehicles[0] with { Observations = [
            new(ReadinessKind.Pickup, true, new("status", "tos", snapshot.ScenarioTime.AddHours(offsetHours), "Ready"))
        ] };
        var result = policy.Assess(vehicle, ReadinessKind.Pickup, snapshot.ScenarioTime);
        Assert.Equal(expected, result.State);
        if (expected == ReadinessState.Unknown) Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void Same_source_conflicts_at_identical_time_are_not_arbitrarily_resolved()
    {
        var vehicle = snapshot.Vehicles[0] with { Observations = [
            new(ReadinessKind.Pickup, true, new("a", "tos", snapshot.ScenarioTime, "Ready")),
            new(ReadinessKind.Pickup, false, new("b", "tos", snapshot.ScenarioTime, "Not ready"))
        ] };
        Assert.Equal(ReadinessState.Conflicting, policy.Assess(vehicle, ReadinessKind.Pickup, snapshot.ScenarioTime).State);
    }
}
