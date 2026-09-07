# Implementation brief

## Scope

Build one operations agent for a fictional Belgian RoRo terminal. Primary user: dispatcher or operations coordinator; customer service uses the same evidence and tools. Begin with an operational attention overview, then vehicle investigation and a reviewable action proposal.

## Domain model

| Entity | Minimum information |
| --- | --- |
| Customer | ID and access scope |
| Vehicle | Synthetic identifier, customer, booking, terminal, separate pickup and loading readiness |
| Booking | Customer, vehicle references, direction, vessel-call reference, deadline |
| VesselCall | Port, vessel, planned/estimated/actual timestamps, source and observation time |
| OperationalEvent | Vehicle/booking, event type, occurrence time, ingestion time, source ID |
| ProcessingJob / Hold | Reason, state, responsible role, optional expected completion |
| Procedure | Synthetic text, version, applicability, access scope |
| ActionProposal | Customer, type, exact payload, evidence references, version, expiry, status |
| Approval / AuditEvent | Actor, authorized role, proposal version, timestamp and execution result |

Persist instants in UTC; show Europe/Brussels to users. Inject a clock so scenarios are repeatable. Distinguish historical fixture time from current system time.

## Tool boundaries

1. SearchOperations: scoped bookings/vehicles within a bounded time window.
2. GetVehicleTimeline: facts, holds, provenance and freshness.
3. GetVesselCall: distinct estimated and actual events.
4. SearchProcedures: scoped retrieval of versioned synthetic operational documents.
5. ProposeAction: persist a draft for human review; cannot execute it.

An application service computes readiness constraints, deadlines and counts in code. The model may call it through the operational tools; it cannot override its results. An independent authenticated endpoint approves the exact proposal version and executes through an idempotent simulated adapter. Approval must fail or require renewed review if the payload or relevant operational state has changed.

Tenant/customer identity comes from validated authentication, never from a model argument. Tool results and retrieved documents are untrusted content. Impose request timeouts, tool-call limits, output limits and cancellation.

## First demo fixtures

- Ready vehicle with current supporting events.
- Processing hold with a known completion estimate.
- Damage hold with no completion estimate.
- Deadline within 24 hours while a loading constraint remains open.
- Vessel ETA changed after an older planning snapshot.
- Discharge complete while pickup release remains blocked.
- Conflicting status observations from two synthetic sources.
- Vehicle owned by a different customer.
- Procedure containing a prompt-injection attempt.
- Approved notification retried after a simulated delivery failure.

Use clearly synthetic data, not real VINs or copied internal documents. Public web research supplies background; write original demo procedures and label their rules as fictional.

## Acceptance criteria

- The overview matches expected rule outputs at a fixed scenario time.
- Every operational assertion cites a returned record or document; unsupported conclusions are explicitly unknown.
- Arrival estimates are not presented as pickup commitments.
- Search and retrieval cannot disclose another customer's records, including through citations.
- Retrieved instructions cannot authorize actions or change tool permissions.
- Proposals remain drafts until an authorized human approves the exact version.
- Expired, modified or no-longer-valid proposals cannot execute on stale approval.
- Duplicate execution requests produce one simulated action.
- Provider failure yields an explicit unavailable result, with no fabricated answer or action.
- Traces expose model/tool timing, failures and usage when available without logging sensitive payloads by default.

## Evaluation plan

Separate deterministic domain/security tests from real-model evaluations. Model evaluations cover task completion, tool choice, evidence correctness, unknown handling and injection resistance. A recorded/mock model can test plumbing but cannot establish actual agent quality. Publish dataset version, provider/model configuration, run date, sample counts and failures alongside measured results. No invented performance or cost scores.

## Delivery sequence

1. Domain rules, synthetic fixtures, .NET API and deterministic acceptance tests.
2. Real model tool loop, provenance and procedure retrieval.
3. Authenticated review flow, durable approval state and simulated execution.
4. Operational UI, agent evaluation runner and tracing.
5. Reproducible local setup, CI, architecture explanation and demo recording.
6. MCP adapter and Azure deployment after local acceptance criteria pass.

Evaluate reuse of shipment-tracking-platform through a code review before copying components. Repository metadata alone does not validate its implementation. Keep the project runnable locally before introducing cloud dependencies.
