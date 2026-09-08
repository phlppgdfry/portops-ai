using PortOps.Agent;
using PortOps.Domain;

namespace PortOps.Tests;

public sealed class ToolTests
{
    private static OperationalTools Tools()
    {
        var snapshot = DemoTerminal.Create();
        return new(new(snapshot, new ScenarioClock(snapshot.ScenarioTime), new ReadinessPolicy()));
    }

    [Theory]
    [InlineData("{\"id\":\"DEMO-003\",\"customerId\":\"harborline\"}")]
    [InlineData("{\"id\":\"DEMO-003\",\"id\":\"DEMO-007\"}")]
    [InlineData("{\"id\":3}")]
    [InlineData("{\"id\":\"../etc/passwd\"}")]
    [InlineData("{\"id\":null}")]
    [InlineData("[]")]
    [InlineData("not-json")]
    public void Invalid_or_scope_override_arguments_are_rejected(string arguments) =>
        Assert.Equal("invalid_arguments", Tools().Execute("northstar", new("x", "get_vehicle", arguments)).Status);

    [Fact]
    public void Unknown_completion_stays_null_in_tool_result()
    {
        var result = Tools().Execute("northstar", new("x", "get_vehicle", "{\"id\":\"DEMO-003\"}"));
        Assert.Equal(System.Text.Json.JsonValueKind.Null,
            result.Data.GetProperty("investigation").GetProperty("vehicle").GetProperty("activeHolds")[0].GetProperty("estimatedCompletion").ValueKind);
    }

    [Fact]
    public void Empty_overview_has_a_citable_rule_result()
    {
        var result = Tools().Execute("harborline", new("x", "get_attention", "{\"horizonHours\":24}"));
        Assert.Equal("ok", result.Status);
        Assert.Contains(result.Evidence, e => e.Id == "attention-overview-24" && e.Detail.StartsWith("0 vehicles"));
        Assert.DoesNotContain("northstar", result.Data.GetRawText());
    }

    [Fact]
    public void Vessel_call_is_scoped_and_vehicle_tool_includes_booking_reference()
    {
        Assert.Equal("not_found", Tools().Execute("northstar", new("x", "get_vessel_call", "{\"id\":\"CALL-04\"}")).Status);
        var result = Tools().Execute("northstar", new("x", "get_vehicle", "{\"id\":\"DEMO-003\"}"));
        Assert.Equal("CALL-01", result.Data.GetProperty("booking").GetProperty("vesselCallId").GetString());
    }
}
