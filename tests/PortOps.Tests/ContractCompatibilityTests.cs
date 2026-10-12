using System.Text.Json;
using PortOps.Domain;
namespace PortOps.Tests;
public sealed class ContractCompatibilityTests
{
    [Fact]
    public void Frozen_v01_payload_keeps_its_meaning_in_the_current_contract()
    {
        var fixture=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","contracts-v0.1.json"));
        var vehicle=JsonSerializer.Deserialize<Vehicle>(fixture,new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal("legacy-customer",vehicle.CustomerId);
        Assert.Equal(ReadinessKind.Pickup,vehicle.Observations.Single().Kind);
        Assert.False(vehicle.Observations.Single().Ready);
        Assert.True(vehicle.ActiveHolds.Single().BlocksPickup);
        Assert.Null(vehicle.ActiveHolds.Single().EstimatedCompletion);
        Assert.Equal(TimeSpan.FromHours(2),vehicle.Observations.Single().Evidence.ObservedAt.Offset);
    }
    [Fact]
    public void Baseline_enum_numbers_and_names_remain_stable()
    {
        Assert.Equal("Pickup",Enum.GetName(typeof(ReadinessKind),0));
        Assert.Equal("Loading",Enum.GetName(typeof(ReadinessKind),1));
        Assert.Equal("Ready",Enum.GetName(typeof(ReadinessState),0));
        Assert.Equal("Blocked",Enum.GetName(typeof(ReadinessState),1));
        Assert.Equal("Unknown",Enum.GetName(typeof(ReadinessState),2));
        Assert.Equal("Conflicting",Enum.GetName(typeof(ReadinessState),3));
        Assert.Equal("Critical",Enum.GetName(typeof(AttentionPriority),0));
        Assert.Equal("High",Enum.GetName(typeof(AttentionPriority),1));
        Assert.Equal("Review",Enum.GetName(typeof(AttentionPriority),2));
    }
    [Fact]
    public void New_envelope_preserves_source_observation_and_event_occurrence_separately()
    {
        var occurrence=DateTimeOffset.Parse("2026-10-12T08:20:00Z");
        var observation=occurrence.AddMinutes(2);
        var envelope=new ShipmentEventEnvelope("shipment-1","synthetic-customer","event-1",
            new("gate_in",occurrence,new("evidence-1","synthetic",observation,"Gate recorded")));
        var restored=JsonSerializer.Deserialize<ShipmentEventEnvelope>(JsonSerializer.Serialize(envelope))!;
        Assert.Equal(envelope,restored);Assert.Equal(1,restored.SchemaVersion);
        Assert.NotEqual(restored.Event.OccurredAt,restored.Event.Evidence.ObservedAt);
    }
}
