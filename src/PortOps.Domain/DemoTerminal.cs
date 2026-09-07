namespace PortOps.Domain;

/// <summary>Version 1 synthetic fixtures. Identifiers are deliberately not real VINs.</summary>
public static class DemoTerminal
{
    public static TerminalSnapshot Create()
    {
        var now = new DateTimeOffset(2026, 9, 7, 8, 0, 0, TimeSpan.Zero);
        Evidence Record(string id, string detail, int ageHours = 1, string source = "synthetic-tos") =>
            new(id, source, now.AddHours(-ageHours), detail);
        StatusObservation Status(string id, ReadinessKind kind, bool ready, int ageHours = 1,
            string source = "synthetic-tos") => new(kind, ready,
                Record(id, $"{kind} readiness recorded as {ready}.", ageHours, source));
        Vehicle Car(string id, string booking, IReadOnlyList<StatusObservation> observations,
            IReadOnlyList<Hold>? holds = null, IReadOnlyList<OperationalEvent>? events = null,
            string customer = "northstar") => new(id, customer, booking, observations, holds ?? [], events ?? []);

        var vehicles = new[]
        {
            Car("DEMO-001", "BOOK-101", [Status("s-001p", ReadinessKind.Pickup, true), Status("s-001l", ReadinessKind.Loading, true)]),
            Car("DEMO-002", "BOOK-101", [Status("s-002p", ReadinessKind.Pickup, false), Status("s-002l", ReadinessKind.Loading, false)],
                [new("h-002", "Pre-delivery inspection incomplete", "vehicle-processing", true, true,
                    now.AddHours(2), Record("e-h002", "Inspection hold remains open; completion is an estimate."))]),
            Car("DEMO-003", "BOOK-102", [Status("s-003p", ReadinessKind.Pickup, false), Status("s-003l", ReadinessKind.Loading, false)],
                [new("h-003", "Damage assessment pending", "damage-team", true, true, null,
                    Record("e-h003", "Damage assessment unresolved; no completion estimate."))]),
            Car("DEMO-004", "BOOK-103", [Status("s-004p", ReadinessKind.Pickup, false), Status("s-004l", ReadinessKind.Loading, true)],
                [new("h-004", "Pickup release check pending", "release-desk", true, false, null,
                    Record("e-h004", "Pickup-only release check is still open."))],
                [new("Discharged", now.AddHours(-2), Record("e-004discharge", "Vehicle discharge completed."))]),
            Car("DEMO-005", "BOOK-101", [Status("s-005p", ReadinessKind.Pickup, true),
                Status("s-005l", ReadinessKind.Loading, true),
                Status("s-005plan", ReadinessKind.Loading, false, 2, "synthetic-planning")]),
            Car("DEMO-006", "BOOK-104", [Status("s-006p", ReadinessKind.Pickup, true, 12), Status("s-006l", ReadinessKind.Loading, true, 12)]),
            Car("DEMO-007", "BOOK-201", [Status("s-007p", ReadinessKind.Pickup, true), Status("s-007l", ReadinessKind.Loading, true)], customer: "harborline")
        };
        var bookings = new[]
        {
            new Booking("BOOK-101", "northstar", "CALL-01", now.AddHours(4), Record("e-b101", "Synthetic loading cut-off at 12:00 UTC.")),
            new Booking("BOOK-102", "northstar", "CALL-01", now.AddHours(-1), Record("e-b102", "Synthetic loading cut-off at 07:00 UTC.")),
            new Booking("BOOK-103", "northstar", "CALL-02", null, Record("e-b103", "Import booking, no loading deadline configured.")),
            new Booking("BOOK-104", "northstar", "CALL-03", now.AddHours(48), Record("e-b104", "Synthetic loading cut-off in 48 hours.")),
            new Booking("BOOK-201", "harborline", "CALL-04", now.AddHours(8), Record("e-b201", "Harborline booking loading cut-off in 8 hours."))
        };
        var calls = new[]
        {
            new VesselCall("CALL-01", "Demo Horizon", "Fictional Zeebrugge Terminal", now.AddHours(-2), now.AddHours(1), null, null,
                Record("e-call01", "Arrival estimate revised from 06:00 to 09:00 UTC; not actual arrival.")),
            new VesselCall("CALL-02", "Demo Aurora", "Fictional Zeebrugge Terminal", now.AddHours(-8), now.AddHours(-8), now.AddHours(-8), now.AddHours(-2),
                Record("e-call02", "Vessel discharge completed at 06:00 UTC; individual pickup release is separate.")),
            new VesselCall("CALL-03", "Demo Atlas", "Fictional Antwerp Terminal", now.AddHours(40), now.AddHours(40), null, null,
                Record("e-call03", "Arrival estimate only.")),
            new VesselCall("CALL-04", "Demo Meridian", "Fictional Antwerp Terminal", now.AddHours(2), now.AddHours(2), null, null,
                Record("e-call04", "Arrival estimate only."))
        };
        return new(now, vehicles, bookings, calls);
    }
}
