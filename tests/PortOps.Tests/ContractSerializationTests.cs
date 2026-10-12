using System.Text.Json;
using PortOps.Domain;

namespace PortOps.Tests;

public sealed class ContractSerializationTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    [Fact]
    public void Vehicle_round_trip_preserves_evidence_holds_and_unknown_completion()
    {
        var at = DateTimeOffset.Parse("2026-10-12T08:00:00+02:00");
        var evidence = new Evidence("e-1", "synthetic", at, "Customs review pending");
        var vehicle = new Vehicle("v-1", "customer-1", "booking-1",
            [new(ReadinessKind.Pickup, false, evidence)],
            [new("hold-1", "Customs", "operator", true, false, null, evidence)],
            [new("gate_arrival", at, evidence)]);
        var serialized = JsonSerializer.Serialize(vehicle, Json);
        Assert.Contains("\"customerId\":\"customer-1\"", serialized);
        var restored = JsonSerializer.Deserialize<Vehicle>(serialized, Json)!;
        Assert.Equal(vehicle.Id, restored.Id);
        Assert.Equal(at, restored.Events.Single().OccurredAt);
        Assert.Equal(evidence, restored.ActiveHolds.Single().Evidence);
        Assert.Null(restored.ActiveHolds.Single().EstimatedCompletion);
        Assert.False(restored.Observations.Single().Ready);
        Assert.Equal(ReadinessKind.Pickup, restored.Observations.Single().Kind);
    }
    [Fact]
    public void Unknown_readiness_is_distinct_from_blocked_and_preserves_warnings()
    {
        var assessment = new ReadinessAssessment(ReadinessKind.Loading, ReadinessState.Unknown,
            "No current evidence", [], ["Stale observation"]);
        var restored = JsonSerializer.Deserialize<ReadinessAssessment>(JsonSerializer.Serialize(assessment, Json), Json)!;
        Assert.Equal(ReadinessState.Unknown, restored.State);
        Assert.Equal("Stale observation", restored.Warnings.Single());
        Assert.Empty(restored.Evidence);
    }
}
