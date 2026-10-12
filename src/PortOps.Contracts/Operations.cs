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

