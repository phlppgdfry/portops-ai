# Planning policy and demo counts

This is a fictional local planning brief, not a live TOS or a stock forecast. It works without a model. The authenticated API and `get_planning` agent tool use the same deterministic service.

## Reproduce the demonstration

At the fixed scenario time, 7 September 2026 at 10:00 Brussels, select 7 days:

| Northstar measure | Result |
| --- | --- |
| Unique inventory vehicles | 6 |
| Pickup-ready with current inventory evidence | 2 (DEMO-001 and DEMO-005) |
| Vehicles with active holds / total holds | 3 / 3 |
| Vehicles with source-quality warnings | 2 (DEMO-005 and DEMO-006) |
| Expected vessel calls | 2 |
| Known current inbound volume | 12 vehicles |
| Inbound plans without usable volume | 1 |

DEMO-005 is ready for pickup but has conflicting loading evidence. These assessments are independent. Readiness does not confirm a pickup appointment.

IN-101 explicitly describes 12 new incoming vehicles, separate from inventory. Its older planning time differs from the current vessel ETA, which determines inclusion in the horizon. IN-102 has no supplied volume; null does not mean zero. At 1 day only IN-101 remains, while all 6 stock vehicles remain visible. Harborline has a separate stock vehicle and a 5-vehicle inbound plan.

## Rules

- The endpoint accepts integer `horizonDays` from 1 through 14, default 7; the UI offers 1, 7 and 14. Invalid values return 400. Customer scope comes from authentication, never a query parameter.
- Inventory membership is explicit. Deduplicate vehicles and use the latest non-future inventory evidence. A stale inventory position stays visible as historical stock with a warning, but cannot contribute to the pickup-ready count. Future-only and inaccessible positions are excluded with generic warnings.
- Stock remains visible regardless of horizon. Deadlines are classified as elapsed, within the inclusive window, outside it or absent. Holds show their owner and optional estimated completion, never an implied release.
- Select the latest observable version of each customer-owned inbound plan. Conflicting latest vessel references are excluded; conflicting latest volumes are unknown. Conflicting original planning times remain unresolved and visible.
- Planned arrivals use the current accessible vessel ETA, including the upper boundary. Overdue ETAs without a registered arrival stay visible for follow-up. Already-arrived or discharged calls are excluded from expected arrivals; their volume is never automatically inserted into stock.
- Future vessel observations are excluded. Planning or vessel evidence older than six hours makes the incoming volume unusable for the known total. Negative values are invalid; explicit zero is valid. Unknown, stale, invalid and conflicting volumes are displayed as unknown and counted separately.
- Counts include source details and a derived `planning-summary-{days}` evidence record. No planned volume is inferred from a vessel name, ETA or the existing vehicle list. No expected closing stock is calculated without departure and inventory movement data.

## Validation and limits

The 15 added test cases cover baseline counts, horizon boundaries, already-arrived cargo, deduplication, conflicting versions, zero/negative/unknown volume, stale/future records, scoped joins, tool execution and authenticated API access. The complete Release suite has 127 passing tests.

The live-model dataset now includes a planning case; it has not been run against a real model. Monitoring records `planning.overview` and `get_planning` without payloads. All fixtures remain immutable synthetic data. Real source ownership, volumes, freshness thresholds and movement reconciliation require validation before any authorized TOS integration.
