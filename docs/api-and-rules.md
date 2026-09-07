# API and demo policy

## Endpoints

| Method / path | Result |
| --- | --- |
| GET /health | Public liveness and synthetic-demo label |
| GET /api/demo | Customer scope, fixed clock and demo policy metadata |
| GET /api/vehicles | Scoped vehicles, evidence, holds, events and both readiness assessments |
| GET /api/vehicles/{id} | One scoped vehicle investigation; 404 for absent or inaccessible records |
| GET /api/bookings | Scoped booking deadlines and vessel-call references |
| GET /api/vessel-calls/{id} | A call referenced by a scoped booking; no other customer's bookings included |
| GET /api/operations/attention?horizonHours=24 | Prioritized vehicle attention items, grouped by booking ID in the payload |

All `/api` routes require `Authorization: Bearer <configured-demo-token>`. Missing/invalid credentials return 401. Horizon must be an integer from 1 to 168; invalid input returns 400. Read APIs only: there is no release, notification or approval endpoint yet.

## Readiness policy v1

These are original fictional rules, not an implementation of any real terminal's release policy.

1. Assess pickup and loading independently.
2. An applicable unresolved hold blocks readiness, even after its estimated completion. A contradictory ready observation produces a warning.
3. Use the latest non-future observation per source. Preserve disagreements at identical timestamps.
4. Observations more than six hours old are stale. Future-dated observations are invalid for current decisions. Both conditions are visible as warnings.
5. Current sources that disagree produce `Conflicting`; no current sources produce `Unknown`.
6. Otherwise use the current explicit ready/not-ready observation. A stale alternative source cannot overrule current evidence, but remains cited and warned about.
7. Arrival and discharge events never imply readiness or a promised pickup time.

`Evidence` is the supporting record set, including stale records for transparency. `Warnings` and the typed assessment explain which records are ineligible. The complete original observation set remains available on the vehicle investigation endpoint.

## Attention policy v1

- `Critical`: loading deadline reached/passed while loading state is not Ready.
- `High`: loading deadline falls within the horizon, inclusive, while loading state is not Ready.
- `Review`: other unresolved readiness or observation-quality issues.
- Fully ready vehicles without warnings are omitted.

The horizon changes deadline urgency; it does not hide other operational exceptions. Overdue unresolved bookings remain visible. These are policy categories, not probabilities, forecasts or a claim that a particular vessel was missed.

## Limits

Data is in memory and immutable by API design. No persistence, external data adapter, user management, roles, model calls, document retrieval, action approval, telemetry exporter or live updates are present. The fixed clock is deliberately separate from wall time. The synthetic active-hold collection is a snapshot of unresolved holds at that scenario time, not a historical event replay engine.

## Validation

Run `dotnet test PortOps.slnx`. The initial 39 cases cover fixture outcomes, hold precedence, discharge/release separation, stale/future observations, source conflicts, deadline/horizon boundaries, time injection, customer isolation, authentication and HTTP contracts. These are software tests, not model evaluations or an agent accuracy score.
