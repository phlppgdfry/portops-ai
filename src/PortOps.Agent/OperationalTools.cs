using System.Text.Json;
using PortOps.Domain;

namespace PortOps.Agent;

public sealed class OperationalTools(OperationsService operations)
{
    public static readonly JsonElement Definitions = JsonDocument.Parse("""
    [
      {"type":"function","name":"get_attention","description":"Read the authenticated customer's attention overview, including booking references, readiness assessments and source evidence. Use for overview, risk and priority questions. Horizon changes urgency; all unresolved issues remain visible.","strict":true,"parameters":{"type":"object","properties":{"horizonHours":{"type":"integer","minimum":1,"maximum":168}},"required":["horizonHours"],"additionalProperties":false}},
      {"type":"function","name":"get_vehicle","description":"Investigate one vehicle: pickup and loading readiness, holds, events, booking and vessel-call reference. Use for RFP, release, completion and cause questions. Never infers a promised release time.","strict":true,"parameters":{"type":"object","properties":{"id":{"type":"string"}},"required":["id"],"additionalProperties":false}},
      {"type":"function","name":"get_vessel_call","description":"Read a vessel call accessible through the authenticated customer's bookings. Planned, estimated and actual arrival/discharge are distinct; none implies vehicle release.","strict":true,"parameters":{"type":"object","properties":{"id":{"type":"string"}},"required":["id"],"additionalProperties":false}}
    ]
    """).RootElement.Clone();

    public ToolResult Execute(string customerId, ToolCall call)
    {
        if (call.Arguments.Length > 2048) return Error("invalid_arguments");
        try
        {
            using var document = JsonDocument.Parse(call.Arguments, new JsonDocumentOptions { MaxDepth = 8 });
            var args = document.RootElement;
            if (args.ValueKind != JsonValueKind.Object) return Error("invalid_arguments");
            var properties = args.EnumerateObject().ToArray();
            var expected = call.Name == "get_attention" ? "horizonHours" : "id";
            // Reject duplicate keys and all extra arguments, including customer/role overrides.
            if (properties.Length != 1 || properties[0].Name != expected) return Error("invalid_arguments");
            if (call.Name == "get_attention")
            {
                if (!properties[0].Value.TryGetInt32(out var horizon) || horizon is < 1 or > 168)
                    return Error("invalid_arguments");
                var overview = operations.GetAttention(customerId, horizon);
                var bookings = operations.GetBookings(customerId);
                // Include an authoritative overview record so an empty overview can still be cited.
                var overviewEvidence = new Evidence($"attention-overview-{horizon}", "portops-rules-v1", operations.Now,
                    $"{overview.Items.Count} vehicles require attention out of {overview.VehiclesInScope} in scope; horizon {horizon} hours.");
                var evidence = overview.Items.SelectMany(x => x.Evidence).Append(overviewEvidence).DistinctBy(x => x.Id).ToArray();
                return new("ok", AgentJson.Element(new { overview, bookings, overviewEvidence }), evidence);
            }
            if (properties[0].Value.ValueKind != JsonValueKind.String) return Error("invalid_arguments");
            var id = properties[0].Value.GetString()!;
            if (id.Length is < 1 or > 64 || id.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
                return Error("invalid_arguments");
            switch (call.Name)
            {
                case "get_vehicle":
                    var vehicle = operations.GetVehicle(customerId, id);
                    if (vehicle is null) return Error("not_found");
                    var booking = operations.GetBookings(customerId).Single(x => x.Id == vehicle.Vehicle.BookingId);
                    var sources = vehicle.Pickup.Evidence.Concat(vehicle.Loading.Evidence)
                        .Concat(vehicle.Vehicle.Events.Select(x => x.Evidence)).Append(booking.Evidence)
                        .DistinctBy(x => x.Id).ToArray();
                    return new("ok", AgentJson.Element(new { investigation = vehicle, booking }), sources);
                case "get_vessel_call":
                    var vesselCall = operations.GetVesselCall(customerId, id);
                    return vesselCall is null ? Error("not_found") : new("ok", AgentJson.Element(vesselCall), [vesselCall.Evidence]);
                default: return Error("unknown_tool");
            }
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
        {
            return Error("invalid_arguments");
        }
    }

    private static ToolResult Error(string code) => new(code,
        AgentJson.Element(new { error = code, message = code == "not_found"
            ? "No accessible record found. Do not infer whether another customer owns it."
            : "Tool request rejected. Use only the declared schema and read tools." }), []);
}
