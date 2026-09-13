# API and demo policy

## Endpoints

| Method / path | Result |
| --- | --- |
| GET /api/agent/status | Model configuration status and read-only limits |
| POST /api/agent/investigate | Bounded investigation with findings, evidence and execution metadata |
| GET /api/procedures?query=rfp | Scoped versioned fictional procedures, lexical matching |
| GET /api/vehicles/{id}/procedures | Relevant procedures for an accessible vehicle |
| GET /api/proposals | Customer-scoped drafts, decisions and simulation receipts |
| POST /api/proposals | Create a deterministic draft from `vehicleId` |
| POST /api/proposals/{id}/refresh | Regenerate a draft from current records using expected `version` |
| POST /api/proposals/{id}/approve | Reviewer-only exact `version` + `payloadHash` approval and simulated delivery |
| POST /api/proposals/{id}/reject | Reviewer-only exact-version rejection |
| GET /api/monitoring | Reviewer-only customer-scoped request metrics and traces |
| GET /api/monitoring/requests/{id} | One locally generated request ID; 404 if unavailable in scope |
| GET /api/monitoring/export | Reviewer-only diagnostic JSON download |
| GET /health | Public liveness and synthetic-demo label |
| GET /api/demo | Customer scope, fixed clock and demo policy metadata |
| GET /api/vehicles | Scoped vehicles, evidence, holds, events and both readiness assessments |
| GET /api/vehicles/{id} | One scoped vehicle investigation; 404 for absent or inaccessible records |
| GET /api/planning?horizonDays=7 | Scoped inventory, readiness, holds and explicit inbound plans; 1–14 days, default 7 |
| GET /api/bookings | Scoped booking deadlines and vessel-call references |
| GET /api/vessel-calls/{id} | A call referenced by a scoped booking; no other customer's bookings included |
| GET /api/operations/attention?horizonHours=24 | Prioritized vehicle attention items, grouped by booking ID in the payload |

All `/api` routes require `Authorization: Bearer <configured-demo-token>`. Missing/invalid credentials return 401. The attention horizon in hours must be an integer from 1 to 168; invalid input returns 400. Operational records remain read-only. Drafts and human decisions are writable; no real notification or terminal-release endpoint exists.

Planning uses the fixed scenario clock and keeps stock and expected inbound volumes separate. See [planning policy](planning.md) for null volumes, freshness, horizon and deduplication rules.

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

Terminal data is in memory and immutable by API design. Proposals, approval and simulated delivery are persisted in a local atomic JSON snapshot. A separate demo reviewer role is enforced. Production identity, an external TOS adapter, telemetry export and live terminal updates are not present. The fixed clock is deliberately separate from wall time. The synthetic active-hold collection is a snapshot of unresolved holds at that scenario time, not a historical event replay engine.

## Validation

Run `dotnet test PortOps.slnx`. The initial 39 cases cover fixture outcomes, hold precedence, discharge/release separation, stale/future observations, source conflicts, deadline/horizon boundaries, time injection, customer isolation, authentication and HTTP contracts. These are software tests, not model evaluations or an agent accuracy score.

## Agent milestone

See [Agent setup](agent-setup.md) for request format, configuration, limits and validation boundaries. Model adapter, scripted-model tests and a local interface are now implemented. No live-model quality score is claimed.

## Approval policy v1

`DemoAuth:Keys:{customer}` is an operator; `DemoAuth:ReviewerKeys:{customer}` is a separate reviewer token. All configured tokens must differ. Roles and customer claims are set only by the server. Extra draft input fields are rejected; recipients and text are generated from scoped records, never arbitrary destinations or model assertions.

Proposal expiry uses wall-clock UTC (30 minutes), separate from fixed operational scenario time. Refresh increments the version and regenerates the payload from current facts. Approval requires matching version, payload hash, unexpired status and an unchanged fingerprint of relevant operational records and procedures. A mismatch returns 409; another customer's proposal is 404 and an operator's approval attempt is 403.

The local store defaults to `src/PortOps.Api/App_Data/proposals.json` (ignored by Git, outside static files); configure `Proposals:Path` to override. One process owns the store. Each mutation flushes a new snapshot before atomic replacement and only then publishes in-memory state. Approval, audit and simulated receipt are written together. Retry returns the same receipt; real email is never sent. This is a bounded local demo store (500 proposals per customer), not a multi-instance database or real-delivery outbox. Keep the data directory to preserve the audit trail across restarts.

## Monitoring

Local application tracing and JSON export are implemented. See [monitoring](monitoring.md) for measured operations, retention, privacy boundaries and interpretation. External telemetry export remains optional.
