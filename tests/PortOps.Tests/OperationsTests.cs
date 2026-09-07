using PortOps.Domain;

namespace PortOps.Tests;

public sealed class OperationsTests
{
    private readonly TerminalSnapshot snapshot = DemoTerminal.Create();
    private OperationsService Service(DateTimeOffset? now = null) =>
        new(snapshot, new ScenarioClock(now ?? snapshot.ScenarioTime), new ReadinessPolicy());

    [Fact]
    public void Attention_overview_has_expected_order_and_excludes_ready_vehicle()
    {
        var result = Service().GetAttention("northstar");
        Assert.Equal(6, result.VehiclesInScope);
        Assert.Equal(new[] { "DEMO-003", "DEMO-002", "DEMO-005", "DEMO-006", "DEMO-004" }, result.Items.Select(x => x.VehicleId));
        Assert.Equal(AttentionPriority.Critical, result.Items[0].Priority);
        Assert.Equal(2, result.Items.Count(x => x.Priority == AttentionPriority.High));
        Assert.All(result.Items, item => Assert.NotEmpty(item.Evidence));
    }

    [Fact]
    public void Changing_horizon_changes_deadline_urgency_not_status_truth()
    {
        var result = Service().GetAttention("northstar", 1);
        Assert.Equal(AttentionPriority.Review, result.Items.Single(x => x.VehicleId == "DEMO-002").Priority);
        Assert.Equal(ReadinessState.Blocked, result.Items.Single(x => x.VehicleId == "DEMO-002").Loading.State);
    }

    [Fact]
    public void Exact_deadline_is_critical_when_loading_is_unresolved()
    {
        var result = Service(snapshot.ScenarioTime.AddHours(4)).GetAttention("northstar");
        Assert.Equal(AttentionPriority.Critical, result.Items.Single(x => x.VehicleId == "DEMO-002").Priority);
    }

    [Fact]
    public void Clock_is_injected_and_later_time_expires_old_status() =>
        Assert.Equal(ReadinessState.Unknown, Service(snapshot.ScenarioTime.AddHours(7)).GetVehicle("northstar", "DEMO-001")!.Pickup.State);

    [Fact]
    public void All_reads_enforce_customer_scope()
    {
        var service = Service();
        Assert.Null(service.GetVehicle("northstar", "DEMO-007"));
        Assert.Null(service.GetVesselCall("northstar", "CALL-04"));
        Assert.All(service.GetBookings("northstar"), b => Assert.Equal("northstar", b.CustomerId));
        Assert.All(service.GetVehicles("northstar"), v => Assert.Equal("northstar", v.Vehicle.CustomerId));
        Assert.Empty(service.GetAttention("unknown").Items);
        Assert.Empty(service.GetAttention("harborline").Items);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(169)]
    public void Unbounded_horizons_are_rejected(int horizon) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Service().GetAttention("northstar", horizon));
}
