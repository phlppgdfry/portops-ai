# Architecture decisions

## Keep operational rules independent of a model

`PortOps.Domain` has no external package dependencies. `ReadinessPolicy` determines pickup and loading readiness; `OperationsService` scopes reads and computes attention. The agent consumes these services through tools rather than calculating its own deadlines or overriding holds.

## Make evidence a first-class return value

Every observation, hold, booking and vessel call has an evidence record containing ID, source, observation timestamp and detail. Assessments return supporting evidence and warnings. This becomes the input to future citation validation; returning evidence today does not yet guarantee a future LLM answer will use it correctly.

## Customer scope comes from identity

ASP.NET Core validates a configured demo bearer token and sets a customer claim. Endpoints supply that claim to every application read. The caller cannot select scope through a query argument. Vessel-call access requires a booking for that customer, and vehicle lookups return the same 404 for nonexistent and inaccessible IDs.

The local demo credential mechanism is deliberately small and has no default tokens. It is not production identity: A separate demo reviewer role is implemented; OIDC, production role management, credential lifecycle and deployment controls remain future work.

## Use repeatable scenarios

`DemoTerminal` defines seven synthetic vehicles, five bookings and four vessel calls for two customers. `ScenarioClock` freezes evaluation time, preventing an otherwise ready demonstration from turning stale as days pass. Unit tests inject alternative times to exercise expiration and deadline boundaries.

## Establish a small new foundation

This milestone implements the domain directly without copying shipment-tracking-platform code. No reuse claim is made without reviewing that repository. Real storage, external adapters and a model client can be added behind the application boundaries when required.

## Next boundary

Configure and evaluate a real model using the prepared dataset. Do not represent scripted model responses or deterministic draft templates as demonstrated live-model performance.

## Read-only agent boundary

`PortOps.Agent` wraps scoped domain reads through five strict tools. The Responses adapter replays all output items, including reasoning, with storage disabled. Client history remains untrusted user text. Findings must cite retrieved evidence IDs; membership validation does not prove factual entailment. One investigation per customer, bounded turns/tool calls, provider timeouts and response-size limits constrain execution. ActivitySource spans contain timing/status/usage metadata; no exporter is configured yet. See [setup and validation](agent-setup.md).

## Durable human review

The model can retrieve original versioned fictional procedures through a lexical search tool and create deterministic notification drafts. It cannot approve or execute. Separate reviewer claims authorize the decision endpoints. Proposal version, exact payload hash, current-record fingerprint and wall-clock expiry are checked before simulated delivery. A local single-owner JSON store atomically persists approval and receipt together; this prevents duplicate simulated actions across concurrent requests and restarts. Real delivery will need a transactional outbox and an idempotent external adapter when introduced. PostgreSQL remains a future production persistence choice.
