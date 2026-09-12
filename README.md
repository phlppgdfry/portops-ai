# PortOps AI — Logistics Operations Agent

An independent portfolio project for investigating RoRo terminal operations, explaining vehicle and booking exceptions with evidence, and proposing actions for human approval.

**Status: .NET 10 operational API, tool-calling agent, versioned procedure retrieval, durable reviewable drafts and Dutch operations interface implemented. Automated tests use scripted models; a real-model smoke test is pending provider configuration. Human approval and simulated delivery work locally; real TOS integration, production identity, database storage and cloud deployment remain planned.**

## Run locally

Requires the .NET SDK specified in `global.json`. No database, model account or cloud subscription is needed for this milestone.

```bash
dotnet test PortOps.slnx
export DemoAuth__Keys__northstar="$(openssl rand -hex 24)"
export DemoAuth__ReviewerKeys__northstar="$(openssl rand -hex 24)"
dotnet run --project src/PortOps.Api --launch-profile demo
```

Open `http://localhost:5088` for the operations interface and sign in with the generated demo token. The API listens at the same address. Keep the generated token in your local shell; do not commit it. From a second terminal, use the same token in `PORTOPS_TOKEN`:

```bash
curl http://localhost:5088/health
curl -H "Authorization: Bearer $PORTOPS_TOKEN" http://localhost:5088/api/demo
curl -H "Authorization: Bearer $PORTOPS_TOKEN" http://localhost:5088/api/operations/attention
curl -H "Authorization: Bearer $PORTOPS_TOKEN" http://localhost:5088/api/vehicles/DEMO-004
```

Optional: configure a different `DemoAuth__Keys__harborline` token before starting to try the second customer. Demo tokens map to customer claims on the server; query parameters and headers cannot choose a different scope. This authentication mode refuses to start outside Development/Testing; use proper OIDC identity before external deployment.

All timestamps are evaluated against a **fixed scenario clock: 2026-09-07 08:00 UTC (10:00 Europe/Brussels)**. They are not live terminal statuses.

## Model connection

See [Agent setup and validation](docs/agent-setup.md) for Azure OpenAI configuration, limits and the remaining real-model checks. The interface, procedure lookup, draft preparation and human approval work without a model; chat stays disabled until configured.

## What works now

- Independent pickup and loading readiness, supported by source records and timestamps.
- Open holds, unknown completion times, stale observations and conflicting sources.
- An attention overview with deterministic urgency categories; no invented delay probabilities.
- Customer-scoped vehicle, booking and vessel-call endpoints.
- Authenticated HTTP integration tests alongside domain acceptance tests.
- Five scoped agent tools: attention, vehicle, vessel call, procedure search and notification draft.
- Bounded model loop and retrieved-source validation.
- Separate reviewer identity, exact-version approval and durable simulated delivery with audit records.
- Dutch operations desk with vehicle investigation, source details and cancellable chat.
- Reviewer-only local monitoring with per-request step details and downloadable diagnostic data.

Northstar's default overview contains five attention items:

| Vehicle | Priority | Explanation |
| --- | --- | --- |
| DEMO-003 | Critical | Loading deadline passed; damage assessment remains open |
| DEMO-002 | High | Inspection hold; loading deadline in four hours |
| DEMO-005 | High | Loading sources disagree; deadline in four hours |
| DEMO-006 | Review | Readiness observations are stale |
| DEMO-004 | Review | Discharged, but pickup release check remains open |

DEMO-001 is ready and is excluded. DEMO-007 belongs to another customer and is inaccessible to Northstar.

## Product

“Which vehicles and bookings need attention today, why, and what should we do next?”

PortOps combines operational records, vessel-call information and relevant procedures. Users can investigate readiness for pickup, departure constraints, processing holds and discrepancies, then prepare an operational brief or customer response.

Customer-service and planning workflows are applications of the same operations agent. They do not replace its broader purpose.

## Target demonstration

1. A dispatcher asks which bookings need attention within the next 24 hours.
2. The agent queries tenant-scoped vehicle, booking and vessel-call tools.
3. Deterministic rules identify documented holds and threatened deadlines.
4. The agent retrieves a relevant synthetic procedure and explains the findings with source IDs and timestamps.
5. The user investigates a vehicle that is not ready for pickup.
6. The agent drafts a notification; an authorized user approves its exact contents.
7. A simulated delivery adapter records the action once, with an audit trail.

## Planned engineering

- ASP.NET Core API and explicit domain rules (implemented).
- One tool-calling agent; model provider behind an interface (implemented; live-model evaluation pending).
- Synthetic in-memory TOS data first (implemented); no assumed access to commercial systems.
- Local atomic JSON proposal storage, lexical procedure retrieval and demo reviewer/customer authorization (implemented).
- PostgreSQL, richer retrieval and production identity (planned).
- Persisted approval workflow, idempotent simulated execution and audit records (implemented; no external delivery).
- Local reviewer monitoring: correlated request/model/tool traces, failures, durations, reported token usage and customer-scoped JSON export (implemented; external OTLP export remains optional).
- Nine-case live-model evaluation runner (implemented; real-model results pending).
- MCP and Azure deployment after the first complete workflow is verified.

See the progress document for the current milestone and its validation limits. Provider SDKs and deployment details must be verified against official documentation during implementation.

## Documentation

- [Local monitoring and diagnostic boundaries](docs/monitoring.md)
- [Five-minute demo and review flow](docs/demo-walkthrough.md)
- [Original product vision and workflow](docs/product-vision.md)
- [Current progress and resume point](docs/progress.md)

- [Domain research and evidence boundaries](docs/domain-research.md)
- [Implementation brief and acceptance criteria](docs/implementation-brief.md)
- [API and demo policy](docs/api-and-rules.md)
- [Architecture decisions](docs/architecture.md)

All operational examples will use fictional organizations, vehicles, documents and events. This project is not affiliated with, commissioned by, or integrated with ICO or any terminal operator.
