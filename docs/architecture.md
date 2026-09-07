# Architecture decisions — milestone 1

## Keep operational rules independent of a model

`PortOps.Domain` has no external package dependencies. `ReadinessPolicy` determines pickup and loading readiness; `OperationsService` scopes reads and computes attention. A future model will consume these services through tools rather than calculating its own deadlines or overriding holds.

## Make evidence a first-class return value

Every observation, hold, booking and vessel call has an evidence record containing ID, source, observation timestamp and detail. Assessments return supporting evidence and warnings. This becomes the input to future citation validation; returning evidence today does not yet guarantee a future LLM answer will use it correctly.

## Customer scope comes from identity

ASP.NET Core validates a configured demo bearer token and sets a customer claim. Endpoints supply that claim to every application read. The caller cannot select scope through a query argument. Vessel-call access requires a booking for that customer, and vehicle lookups return the same 404 for nonexistent and inaccessible IDs.

The local demo credential mechanism is deliberately small and has no default tokens. It is not production identity: OIDC, roles, credential lifecycle and deployment controls remain future work.

## Use repeatable scenarios

`DemoTerminal` defines seven synthetic vehicles, five bookings and four vessel calls for two customers. `ScenarioClock` freezes evaluation time, preventing an otherwise ready demonstration from turning stale as days pass. Unit tests inject alternative times to exercise expiration and deadline boundaries.

## Establish a small new foundation

This milestone implements the domain directly without copying shipment-tracking-platform code. No reuse claim is made without reviewing that repository. Real storage, external adapters and a model client can be added behind the application boundaries when required.

## Next boundary

Connect one real model to scoped read tools, preserve tool evidence IDs and add agent evaluation cases. Add procedure retrieval and reviewable proposals after that loop works. Do not represent a hardcoded responder or mock model as a functioning AI agent.
