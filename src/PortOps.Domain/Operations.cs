namespace PortOps.Domain;

public enum ReadinessKind { Pickup, Loading }
public enum ReadinessState { Ready, Blocked, Unknown, Conflicting }
public enum AttentionPriority { Critical, High, Review }

public sealed record Evidence(string Id, string Source, DateTimeOffset ObservedAt, string Detail);
public sealed record StatusObservation(ReadinessKind Kind, bool Ready, Evidence Evidence);
public sealed record Hold(string Id, string Reason, string Owner, bool BlocksPickup,
    bool BlocksLoading, DateTimeOffset? EstimatedCompletion, Evidence Evidence);
public sealed record OperationalEvent(string Type, DateTimeOffset OccurredAt, Evidence Evidence);
public sealed record Vehicle(string Id, string CustomerId, string BookingId,
    IReadOnlyList<StatusObservation> Observations, IReadOnlyList<Hold> ActiveHolds,
    IReadOnlyList<OperationalEvent> Events);
public sealed record Booking(string Id, string CustomerId, string VesselCallId,
    DateTimeOffset? LoadingDeadline, Evidence Evidence);
public sealed record VesselCall(string Id, string Vessel, string Port,
    DateTimeOffset PlannedArrival, DateTimeOffset EstimatedArrival,
    DateTimeOffset? ActualArrival, DateTimeOffset? DischargeCompleted,
    Evidence Evidence);
public sealed record TerminalSnapshot(DateTimeOffset ScenarioTime,
    IReadOnlyList<Vehicle> Vehicles, IReadOnlyList<Booking> Bookings,
    IReadOnlyList<VesselCall> VesselCalls);

public sealed record ReadinessAssessment(ReadinessKind Kind, ReadinessState State,
    string Explanation, IReadOnlyList<Evidence> Evidence, IReadOnlyList<string> Warnings);
public sealed record VehicleInvestigation(Vehicle Vehicle, ReadinessAssessment Pickup,
    ReadinessAssessment Loading, DateTimeOffset EvaluatedAt);
public sealed record AttentionItem(string BookingId, string VehicleId,
    AttentionPriority Priority, string Reason, DateTimeOffset? LoadingDeadline,
    ReadinessAssessment Pickup, ReadinessAssessment Loading, IReadOnlyList<Evidence> Evidence);
public sealed record AttentionOverview(DateTimeOffset EvaluatedAt, DateTimeOffset WindowEnd,
    int VehiclesInScope, IReadOnlyList<AttentionItem> Items);

/// <summary>Fictional demo policy, not a terminal operator's release policy.</summary>
public sealed class ReadinessPolicy
{
    public static readonly TimeSpan MaximumObservationAge = TimeSpan.FromHours(6);

    public ReadinessAssessment Assess(Vehicle vehicle, ReadinessKind kind, DateTimeOffset now)
    {
        var observations = vehicle.Observations.Where(x => x.Kind == kind).ToArray();
        var holds = vehicle.ActiveHolds.Where(x => kind == ReadinessKind.Pickup
            ? x.BlocksPickup : x.BlocksLoading).ToArray();
        // Retain the newest record per source; never let a future record replace known history.
        var latest = observations.Where(x => x.Evidence.ObservedAt <= now)
            .GroupBy(x => x.Evidence.Source)
            .SelectMany(g => g.Where(x => x.Evidence.ObservedAt == g.Max(y => y.Evidence.ObservedAt)))
            .ToArray();
        var current = latest.Where(x => now - x.Evidence.ObservedAt <= MaximumObservationAge).ToArray();
        var warnings = new List<string>();
        if (latest.Any(x => now - x.Evidence.ObservedAt > MaximumObservationAge))
            warnings.Add("Stale status observations were excluded from readiness decisions.");
        if (observations.Any(x => x.Evidence.ObservedAt > now))
            warnings.Add("Future-dated status observations were excluded from readiness decisions.");
        var evidence = latest.Select(x => x.Evidence).Concat(holds.Select(x => x.Evidence))
            .DistinctBy(x => x.Id).ToArray();

        if (holds.Length > 0)
        {
            if (current.Any(x => x.Ready))
                warnings.Add("A ready observation conflicts with an unresolved hold; the hold prevents release.");
            return new(kind, ReadinessState.Blocked,
                "Unresolved hold(s): " + string.Join("; ", holds.Select(x => x.Reason)) +
                ". Completion estimates do not release holds.", evidence, warnings);
        }
        if (current.Length == 0)
            return new(kind, ReadinessState.Unknown,
                "No current explicit readiness observation. Arrival or discharge does not establish release.", evidence, warnings);
        if (current.Select(x => x.Ready).Distinct().Count() > 1)
            return new(kind, ReadinessState.Conflicting,
                "Current sources disagree. A responsible operator must reconcile the status.", evidence, warnings);
        return current[0].Ready
            ? new(kind, ReadinessState.Ready, "Current explicit readiness evidence and no applicable active hold.", evidence, warnings)
            : new(kind, ReadinessState.Blocked, "The current source reports not ready; no completion time is inferred.", evidence, warnings);
    }
}

/// <summary>All reads require a customer scope supplied by the authenticated application.</summary>
public sealed class OperationsService(TerminalSnapshot snapshot, TimeProvider clock, ReadinessPolicy policy)
{
    public DateTimeOffset Now => clock.GetUtcNow();
    public DateTimeOffset ScenarioTime => snapshot.ScenarioTime;

    public IReadOnlyList<VehicleInvestigation> GetVehicles(string customerId) => snapshot.Vehicles
        .Where(x => x.CustomerId == customerId).Select(Investigate).ToArray();

    public VehicleInvestigation? GetVehicle(string customerId, string vehicleId)
    {
        var vehicle = snapshot.Vehicles.SingleOrDefault(x => x.CustomerId == customerId && x.Id == vehicleId);
        return vehicle is null ? null : Investigate(vehicle);
    }

    public IReadOnlyList<Booking> GetBookings(string customerId) => snapshot.Bookings
        .Where(x => x.CustomerId == customerId).ToArray();

    public VesselCall? GetVesselCall(string customerId, string vesselCallId) =>
        snapshot.Bookings.Any(x => x.CustomerId == customerId && x.VesselCallId == vesselCallId)
            ? snapshot.VesselCalls.SingleOrDefault(x => x.Id == vesselCallId) : null;

    public AttentionOverview GetAttention(string customerId, int horizonHours = 24)
    {
        if (horizonHours is < 1 or > 168)
            throw new ArgumentOutOfRangeException(nameof(horizonHours), "Use a horizon from 1 to 168 hours.");
        var now = Now;
        var end = now.AddHours(horizonHours);
        var vehicles = snapshot.Vehicles.Where(x => x.CustomerId == customerId).ToArray();
        var bookings = GetBookings(customerId).ToDictionary(x => x.Id);
        var items = new List<AttentionItem>();
        foreach (var vehicle in vehicles)
        {
            var pickup = policy.Assess(vehicle, ReadinessKind.Pickup, now);
            var loading = policy.Assess(vehicle, ReadinessKind.Loading, now);
            var booking = bookings[vehicle.BookingId];
            var deadlineInWindow = booking.LoadingDeadline is { } deadline && deadline <= end;
            var loadingUnresolved = loading.State != ReadinessState.Ready;
            var pickupUnresolved = pickup.State != ReadinessState.Ready;
            var hasWarnings = pickup.Warnings.Count > 0 || loading.Warnings.Count > 0;
            if (!loadingUnresolved && !pickupUnresolved && !hasWarnings) continue;

            var priority = deadlineInWindow && loadingUnresolved
                ? booking.LoadingDeadline <= now ? AttentionPriority.Critical : AttentionPriority.High
                : AttentionPriority.Review;
            var reason = priority switch
            {
                AttentionPriority.Critical => "Loading deadline reached or passed; loading readiness is unresolved.",
                AttentionPriority.High => "Loading deadline falls within the selected window; loading readiness is unresolved.",
                _ => "Readiness or source quality requires review; no imminent loading deadline is asserted."
            };
            items.Add(new(booking.Id, vehicle.Id, priority, reason, booking.LoadingDeadline,
                pickup, loading, pickup.Evidence.Concat(loading.Evidence).Append(booking.Evidence)
                    .DistinctBy(x => x.Id).ToArray()));
        }
        return new(now, end, vehicles.Length, items.OrderBy(x => x.Priority)
            .ThenBy(x => x.LoadingDeadline ?? DateTimeOffset.MaxValue).ThenBy(x => x.VehicleId).ToArray());
    }

    private VehicleInvestigation Investigate(Vehicle vehicle)
    {
        var now = Now;
        return new(vehicle, policy.Assess(vehicle, ReadinessKind.Pickup, now),
            policy.Assess(vehicle, ReadinessKind.Loading, now), now);
    }
}

public sealed class ScenarioClock(DateTimeOffset instant) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => instant;
}
