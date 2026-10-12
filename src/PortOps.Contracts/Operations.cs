namespace PortOps.Domain;

/// <summary>The operational question; pickup and vessel loading are assessed independently.</summary>
public enum ReadinessKind {
    /// <summary>Release for road pickup.</summary>
    Pickup = 0,
    /// <summary>Release for vessel loading.</summary>
    Loading = 1
}
/// <summary>Assessment state. Numeric values are part of the JSON compatibility contract.</summary>
public enum ReadinessState {
    /// <summary>Current evidence permits the assessed operation under the caller's policy.</summary>
    Ready = 0,
    /// <summary>An explicit negative observation or active hold prevents the operation.</summary>
    Blocked = 1,
    /// <summary>Current usable evidence is missing; this is not equivalent to ready.</summary>
    Unknown = 2,
    /// <summary>Current sources disagree and require reconciliation.</summary>
    Conflicting = 3
}
/// <summary>Operational review priority; it does not grant authority to release cargo.</summary>
public enum AttentionPriority {
    /// <summary>A deadline has elapsed while readiness remains unresolved.</summary>
    Critical = 0,
    /// <summary>A deadline is approaching while readiness remains unresolved.</summary>
    High = 1,
    /// <summary>Evidence or source quality needs review without an asserted imminent deadline.</summary>
    Review = 2
}

/// <summary>An attributable observation. The source and timestamp describe provenance, not verified truth.</summary>
/// <param name="Id">Stable source or domain identifier; must be unique within the documented integration scope.</param>
/// <param name="Source">Name of the observation-producing system or synthetic fixture. It is supplied by the integration, not proof of authenticity.</param>
/// <param name="ObservedAt">Instant the source observed the evidence, expressed with an explicit UTC offset. It can differ from occurrence time.</param>
/// <param name="Detail">Human-readable evidence context; do not place credentials or unnecessary personal data here.</param>
public sealed record Evidence(string Id, string Source, DateTimeOffset ObservedAt, string Detail);
/// <summary>Explicit readiness observation from one evidence source.</summary>
/// <param name="Kind">Whether the observation or assessment concerns pickup or loading.</param>
/// <param name="Ready">Explicit source readiness flag; downstream policy must still consider holds, freshness and conflicts.</param>
/// <param name="Evidence">Evidence supporting this record or assessment. Consumers must evaluate freshness and attribution.</param>
public sealed record StatusObservation(ReadinessKind Kind, bool Ready, Evidence Evidence);
/// <summary>An unresolved operational restriction; estimated completion does not release it.</summary>
/// <param name="Id">Stable source or domain identifier; must be unique within the documented integration scope.</param>
/// <param name="Reason">Human-readable reason for the restriction or review.</param>
/// <param name="Owner">Responsible operational party for resolving the hold, not an authorization principal.</param>
/// <param name="BlocksPickup">Whether this hold prevents road pickup.</param>
/// <param name="BlocksLoading">Whether this hold prevents vessel loading.</param>
/// <param name="EstimatedCompletion">Optional estimate for completion. Null means no estimate; an estimate does not release the hold.</param>
/// <param name="Evidence">Evidence supporting this record or assessment. Consumers must evaluate freshness and attribution.</param>
public sealed record Hold(string Id, string Reason, string Owner, bool BlocksPickup,
    bool BlocksLoading, DateTimeOffset? EstimatedCompletion, Evidence Evidence);
/// <summary>An occurrence with source evidence. Type names are defined by the integration.</summary>
/// <param name="Type">Integration-defined event name, such as gate_in or hold_opened; consumers must validate supported names.</param>
/// <param name="OccurredAt">Recorded event occurrence instant with UTC offset, distinct from evidence observation time.</param>
/// <param name="Evidence">Evidence supporting this record or assessment. Consumers must evaluate freshness and attribution.</param>
public sealed record OperationalEvent(string Type, DateTimeOffset OccurredAt, Evidence Evidence);
/// <summary>Customer-scoped vehicle records and their observed operational evidence.</summary>
/// <param name="Id">Stable source or domain identifier; must be unique within the documented integration scope.</param>
/// <param name="CustomerId">Server-established customer boundary. Never trust a client-selected value as authorization.</param>
/// <param name="BookingId">Reference to the booking associated with this vehicle or review item.</param>
/// <param name="Observations">Readiness observations. Read-only interface does not prevent mutation of an underlying caller-owned collection.</param>
/// <param name="ActiveHolds">Holds that remain unresolved in this snapshot.</param>
/// <param name="Events">Operational occurrence history represented by this snapshot.</param>
public sealed record Vehicle(string Id, string CustomerId, string BookingId,
    IReadOnlyList<StatusObservation> Observations, IReadOnlyList<Hold> ActiveHolds,
    IReadOnlyList<OperationalEvent> Events);
/// <summary>Customer-scoped booking with an optional vessel-loading deadline.</summary>
/// <param name="Id">Stable source or domain identifier; must be unique within the documented integration scope.</param>
/// <param name="CustomerId">Server-established customer boundary. Never trust a client-selected value as authorization.</param>
/// <param name="VesselCallId">Reference to the associated vessel call.</param>
/// <param name="LoadingDeadline">Optional explicit loading deadline. Null means not recorded; it does not imply unlimited time.</param>
/// <param name="Evidence">Evidence supporting this record or assessment. Consumers must evaluate freshness and attribution.</param>
public sealed record Booking(string Id, string CustomerId, string VesselCallId,
    DateTimeOffset? LoadingDeadline, Evidence Evidence);
/// <summary>Vessel-call times; expected arrival and recorded arrival are distinct facts.</summary>
/// <param name="Id">Stable source or domain identifier; must be unique within the documented integration scope.</param>
/// <param name="Vessel">Display name of the vessel; not a globally unique vessel identifier.</param>
/// <param name="Port">Integration-defined port name or code; the integration must state which convention it uses.</param>
/// <param name="PlannedArrival">Original scheduled arrival instant with UTC offset.</param>
/// <param name="EstimatedArrival">Current expected arrival instant; this does not establish actual arrival.</param>
/// <param name="ActualArrival">Optional recorded arrival instant; null means no actual arrival is recorded.</param>
/// <param name="DischargeCompleted">Optional recorded discharge-completion instant; it does not establish pickup readiness.</param>
/// <param name="Evidence">Evidence supporting this record or assessment. Consumers must evaluate freshness and attribution.</param>
public sealed record VesselCall(string Id, string Vessel, string Port,
    DateTimeOffset PlannedArrival, DateTimeOffset EstimatedArrival,
    DateTimeOffset? ActualArrival, DateTimeOffset? DischargeCompleted,
    Evidence Evidence);
/// <summary>A terminal scenario snapshot. ScenarioTime is the scenario clock, not a freshness guarantee.</summary>
/// <param name="ScenarioTime">Explicit scenario instant against which synthetic data was constructed.</param>
/// <param name="Vehicles">Vehicles represented in this snapshot; consumers must enforce customer scope.</param>
/// <param name="Bookings">Bookings represented in this snapshot.</param>
/// <param name="VesselCalls">Vessel calls represented in this snapshot.</param>
public sealed record TerminalSnapshot(DateTimeOffset ScenarioTime,
    IReadOnlyList<Vehicle> Vehicles, IReadOnlyList<Booking> Bookings,
    IReadOnlyList<VesselCall> VesselCalls);

/// <summary>Policy-produced read model explaining readiness, evidence and warnings.</summary>
/// <param name="Kind">Whether the observation or assessment concerns pickup or loading.</param>
/// <param name="State">Readiness assessment state; Unknown and Conflicting require distinct handling.</param>
/// <param name="Explanation">Human-readable explanation of the applied policy; not a machine authorization decision.</param>
/// <param name="Evidence">Evidence supporting this record or assessment. Consumers must evaluate freshness and attribution.</param>
/// <param name="Warnings">Source-quality or interpretation warnings retained for review.</param>
public sealed record ReadinessAssessment(ReadinessKind Kind, ReadinessState State,
    string Explanation, IReadOnlyList<Evidence> Evidence, IReadOnlyList<string> Warnings);
/// <summary>Independent pickup and loading assessments for one vehicle.</summary>
/// <param name="Vehicle">Vehicle and its underlying evidence.</param>
/// <param name="Pickup">Assessment for road pickup, independent of loading readiness.</param>
/// <param name="Loading">Assessment for vessel loading, independent of pickup readiness.</param>
/// <param name="EvaluatedAt">Explicit instant at which the result was evaluated.</param>
public sealed record VehicleInvestigation(Vehicle Vehicle, ReadinessAssessment Pickup,
    ReadinessAssessment Loading, DateTimeOffset EvaluatedAt);
/// <summary>An unresolved vehicle or booking requiring operational review.</summary>
/// <param name="BookingId">Reference to the booking associated with this vehicle or review item.</param>
/// <param name="VehicleId">Reference to the affected vehicle.</param>
/// <param name="Priority">Review priority under the producing application's documented policy.</param>
/// <param name="Reason">Human-readable reason for the restriction or review.</param>
/// <param name="LoadingDeadline">Optional explicit loading deadline. Null means not recorded; it does not imply unlimited time.</param>
/// <param name="Pickup">Assessment for road pickup, independent of loading readiness.</param>
/// <param name="Loading">Assessment for vessel loading, independent of pickup readiness.</param>
/// <param name="Evidence">Evidence supporting this record or assessment. Consumers must evaluate freshness and attribution.</param>
public sealed record AttentionItem(string BookingId, string VehicleId,
    AttentionPriority Priority, string Reason, DateTimeOffset? LoadingDeadline,
    ReadinessAssessment Pickup, ReadinessAssessment Loading, IReadOnlyList<Evidence> Evidence);
/// <summary>Customer-scoped attention results for an explicit evaluation window.</summary>
/// <param name="EvaluatedAt">Explicit instant at which the result was evaluated.</param>
/// <param name="WindowEnd">End instant of the selected review window.</param>
/// <param name="VehiclesInScope">Number of customer-visible vehicles considered by the producing application.</param>
/// <param name="Items">Review items returned by the producing application.</param>
public sealed record AttentionOverview(DateTimeOffset EvaluatedAt, DateTimeOffset WindowEnd,
    int VehiclesInScope, IReadOnlyList<AttentionItem> Items);


/// <summary>Versioned integration envelope connecting a shipment and customer to attributable event evidence.</summary>
/// <param name="ShipmentId">Shipment reference in the producing API; the envelope uses a string to avoid a GUID-only integration contract.</param>
/// <param name="CustomerId">Server-established customer boundary. Never trust a client-selected value as authorization.</param>
/// <param name="ReferenceId">Source event identifier. Repeated submission should preserve this identifier for idempotent ingestion.</param>
/// <param name="Event">Operational occurrence and its attributable evidence.</param>
/// <param name="CorrelationId">Optional grouping reference, such as a visit or hold ID. Distinct from the source event reference.</param>
/// <param name="SchemaVersion">Envelope wire-schema version. Version 1 is the initial supported shape, independent of NuGet release versions.</param>
public sealed record ShipmentEventEnvelope(string ShipmentId, string CustomerId,
    string ReferenceId, OperationalEvent Event, int SchemaVersion = 1, string? CorrelationId = null);
