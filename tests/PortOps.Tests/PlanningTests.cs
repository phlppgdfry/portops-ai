using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PortOps.Agent;
using PortOps.Domain;

namespace PortOps.Tests;

public sealed class PlanningTests
{
    private readonly TerminalSnapshot terminal = DemoTerminal.Create();
    private PlanningSnapshot Data => DemoPlanning.Create(terminal.ScenarioTime);
    private PlanningService Service(PlanningSnapshot? data = null, TerminalSnapshot? state = null, DateTimeOffset? now = null) =>
        new(new(state ?? terminal, new ScenarioClock(now ?? terminal.ScenarioTime), new()), data ?? Data);

    [Fact]
    public void Default_week_separates_inventory_readiness_and_explicit_inbound_plans()
    {
        var result = Service().GetOverview("northstar");
        Assert.Equal(6, result.InventoryCount);
        Assert.Equal(2, result.ReadyForPickup); // DEMO-005 is pickup-ready despite a loading conflict.
        Assert.Equal(3, result.VehiclesWithHolds);
        Assert.Equal(3, result.OpenHolds);
        Assert.Equal(2, result.VehiclesNeedingDataReview);
        Assert.Equal(2, result.ExpectedVesselCalls);
        Assert.Equal(12, result.KnownInboundVehicles);
        Assert.Equal(1, result.InboundPlansWithUnknownVolume);
        Assert.Null(result.Arrivals.Single(a => a.PlanId == "IN-102").ExpectedVehicles);
        Assert.Equal("unknown", result.Arrivals.Single(a => a.PlanId == "IN-102").VolumeStatus);
        Assert.Contains(result.Arrivals.Single(a => a.PlanId == "IN-101").Warnings, w => w.Contains("wijkt af"));
        Assert.Equal("elapsed", result.Stock.Single(s => s.VehicleId == "DEMO-003").DeadlineStatus);
        Assert.Equal("not_set", result.Stock.Single(s => s.VehicleId == "DEMO-004").DeadlineStatus);
        Assert.All(result.Stock, s => Assert.NotEmpty(s.Evidence));
        Assert.All(result.Arrivals, a => Assert.NotEmpty(a.Evidence));
        Assert.Contains(result.Evidence, e => e.Id == "planning-summary-7");
    }

    [Fact]
    public void Horizon_uses_current_vessel_estimate_not_old_plan_and_preserves_stock()
    {
        var shortWindow = Service().GetOverview("northstar", 1);
        Assert.Equal(6, shortWindow.InventoryCount);
        Assert.Equal("IN-101", Assert.Single(shortWindow.Arrivals).PlanId);
        Assert.Equal(0, shortWindow.InboundPlansWithUnknownVolume);
        Assert.Equal("outside_window", shortWindow.Stock.Single(s => s.VehicleId == "DEMO-006").DeadlineStatus);
        var changed = terminal with { VesselCalls = terminal.VesselCalls.Select(c => c.Id == "CALL-01"
            ? c with { EstimatedArrival = terminal.ScenarioTime.AddDays(1) } : c).ToArray() };
        Assert.Single(Service(state: changed).GetOverview("northstar", 1).Arrivals); // Upper boundary inclusive.
        changed = changed with { VesselCalls = changed.VesselCalls.Select(c => c.Id == "CALL-01"
            ? c with { EstimatedArrival = c.EstimatedArrival.AddSeconds(1) } : c).ToArray() };
        Assert.Empty(Service(state: changed).GetOverview("northstar", 1).Arrivals);
    }

    [Fact]
    public void Arrived_ship_does_not_create_inventory_from_planned_volume()
    {
        var changed = terminal with { VesselCalls = terminal.VesselCalls.Select(c => c.Id == "CALL-01"
            ? c with { ActualArrival = terminal.ScenarioTime } : c).ToArray() };
        var result = Service(state: changed).GetOverview("northstar");
        Assert.Equal(6, result.InventoryCount);
        Assert.Equal(0, result.KnownInboundVehicles);
        Assert.DoesNotContain(result.Arrivals, a => a.VesselCallId == "CALL-01");
    }

    [Fact]
    public void Duplicate_stock_and_old_plan_versions_are_not_double_counted()
    {
        var data = Data;
        var plan = data.Inbound[0];
        data = data with { Inventory = [.. data.Inventory, data.Inventory[0]], Inbound = [.. data.Inbound,
            plan with { ExpectedVehicles = 1000, Evidence = plan.Evidence with { ObservedAt = plan.Evidence.ObservedAt.AddMinutes(-1) } }] };
        var result = Service(data).GetOverview("northstar");
        Assert.Equal(6, result.InventoryCount);
        Assert.Equal(12, result.KnownInboundVehicles);
        Assert.Contains(result.Stock[0].Warnings, w => w.Contains("Dubbele"));
    }

    [Fact]
    public void Conflicting_latest_volumes_and_times_are_not_silently_selected()
    {
        var data = Data; var plan = data.Inbound[0];
        data = data with { Inbound = [.. data.Inbound, plan with { ExpectedVehicles = 13,
            ArrivalInPlanning = plan.ArrivalInPlanning.AddHours(2), Evidence = plan.Evidence with { Id = "conflicting-plan-source" } }] };
        var result = Service(data).GetOverview("northstar");
        var arrival = result.Arrivals.Single(a => a.PlanId == plan.Id);
        Assert.Null(arrival.ExpectedVehicles);
        Assert.Null(arrival.ArrivalInPlanning);
        Assert.Equal("conflicting", arrival.VolumeStatus);
        Assert.Equal(0, result.KnownInboundVehicles);
        Assert.Equal(2, result.InboundPlansWithUnknownVolume);
        Assert.Contains(arrival.Evidence, e => e.Id == "conflicting-plan-source");
    }

    [Theory]
    [InlineData(0, "known", 1)]
    [InlineData(-1, "invalid", 2)]
    public void Zero_is_a_known_volume_and_negative_volume_is_invalid(int count, string status, int unknown)
    {
        var data = Data;
        data = data with { Inbound = data.Inbound.Select(p => p.Id == "IN-101" ? p with { ExpectedVehicles = count } : p).ToArray() };
        var result = Service(data).GetOverview("northstar");
        Assert.Equal(status, result.Arrivals.Single(a => a.PlanId == "IN-101").VolumeStatus);
        Assert.Equal(unknown, result.InboundPlansWithUnknownVolume);
        Assert.Equal(0, result.KnownInboundVehicles);
    }

    [Fact]
    public void Stale_data_is_visible_but_not_counted_as_current_readiness_or_known_volume()
    {
        var result = Service(now: terminal.ScenarioTime.AddHours(8)).GetOverview("northstar");
        Assert.Equal(6, result.InventoryCount);
        Assert.Equal(0, result.ReadyForPickup);
        Assert.All(result.Stock, s => Assert.False(s.InventoryCurrent));
        Assert.Equal(0, result.KnownInboundVehicles);
        Assert.All(result.Arrivals, a => Assert.Equal("stale", a.VolumeStatus));
        Assert.Contains(result.Arrivals.Single(a => a.PlanId == "IN-101").Warnings, w => w.Contains("verstreken"));
    }

    [Fact]
    public void Future_only_records_are_excluded_with_visible_quality_warning()
    {
        var data = Data;
        data = data with { Inventory = data.Inventory.Select(p => p.VehicleId == "DEMO-001"
            ? p with { Evidence = p.Evidence with { ObservedAt = terminal.ScenarioTime.AddMinutes(1) } } : p).ToArray(),
            Inbound = data.Inbound.Select(p => p.Id == "IN-101"
            ? p with { Evidence = p.Evidence with { ObservedAt = terminal.ScenarioTime.AddMinutes(1) } } : p).ToArray() };
        var result = Service(data).GetOverview("northstar");
        Assert.Equal(5, result.InventoryCount);
        Assert.Equal(1, result.ReadyForPickup);
        Assert.Equal(0, result.KnownInboundVehicles);
        Assert.Equal(2, result.Warnings.Count);
    }

    [Fact]
    public void Customer_scope_covers_stock_inbound_sources_and_bad_cross_customer_references()
    {
        var data = Data;
        data = data with { Inventory = [.. data.Inventory, new("northstar", "DEMO-007", new("private-source", "harborline-private", terminal.ScenarioTime, "secret-other-customer"))],
            Inbound = [.. data.Inbound, new("bad-ref", "northstar", "CALL-04", 999, terminal.ScenarioTime, new("secret-plan", "private", terminal.ScenarioTime, "secret-other-customer"))] };
        var result = Service(data).GetOverview("northstar");
        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("DEMO-007", json);
        Assert.DoesNotContain("CALL-04", json);
        Assert.DoesNotContain("secret", json);
        Assert.Equal(6, result.InventoryCount);
        Assert.Equal(12, result.KnownInboundVehicles);
        Assert.Equal(2, result.Warnings.Count);
        var other = Service().GetOverview("harborline");
        Assert.Equal(1, other.InventoryCount);
        Assert.Equal(5, other.KnownInboundVehicles);
        Assert.DoesNotContain("northstar", JsonSerializer.Serialize(other));
        var empty = Service().GetOverview("missing");
        Assert.Empty(empty.Stock); Assert.Empty(empty.Arrivals); Assert.Equal(0, empty.InventoryCount);
    }

    [Fact]
    public void Planning_tool_returns_scoped_calculations_and_rejects_scope_override()
    {
        var ops = new OperationsService(terminal, new ScenarioClock(terminal.ScenarioTime), new());
        var tools = new OperationalTools(ops, planning: Service());
        var result = tools.Execute("northstar", new("p1", "get_planning", "{\"horizonDays\":7}"));
        Assert.Equal("ok", result.Status);
        Assert.Equal(12, result.Data.GetProperty("knownInboundVehicles").GetInt64());
        Assert.Contains(result.Evidence, e => e.Id == "planning-summary-7");
        Assert.Equal("invalid_arguments", tools.Execute("northstar", new("p2", "get_planning", "{\"horizonDays\":7,\"customerId\":\"harborline\"}")).Status);
    }

    [Fact]
    public async Task Planning_endpoint_requires_auth_and_ignores_customer_override()
    {
        await using var factory = new DemoFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/planning")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", DemoFactory.Northstar);
        var result = (await client.GetFromJsonAsync<PlanningOverview>("/api/planning?horizonDays=7&customerId=harborline", AgentJson.Options))!;
        Assert.Equal(6, result.InventoryCount);
        Assert.Equal(12, result.KnownInboundVehicles);
        Assert.DoesNotContain(result.Stock, s => s.VehicleId == "DEMO-007");
        Assert.Equal(1, (await client.GetFromJsonAsync<PlanningOverview>("/api/planning?horizonDays=1", AgentJson.Options))!.ExpectedVesselCalls);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("15")]
    [InlineData("oops")]
    public async Task Invalid_planning_horizon_is_rejected(string days)
    {
        await using var factory = new DemoFactory();
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", DemoFactory.Northstar);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/planning?horizonDays=" + days)).StatusCode);
    }
}
