# Local monitoring

The reviewer workspace has a collapsible **Technische monitoring** panel. It collects actual completed API requests and their application steps using .NET `ActivitySource` and `ActivityListener`. It requires no cloud account or model subscription. The downloadable report is application JSON, not an OTLP export. OpenTelemetry/Grafana export is a possible later integration.

## Demonstrate it

1. Open http://localhost:5089 and sign in with the reviewer token.
2. Inspect a vehicle, open its procedures or create/refresh a proposal.
3. Expand **04 / Technische monitoring** and press **Vernieuwen**.
4. Expand a request to see parent/child span IDs, start offsets, durations and result codes.
5. Use **Download meetgegevens** for the same customer-scoped JSON snapshot.

Requests that create a proposal include `proposal.create`, `proposal.persist` and nested `procedure.search` steps. Failed decisions retain their error codes. With a configured model, agent requests add `agent.investigate`, `model.responses` and `tool.execute` steps, including reported token usage. With no model, a direct investigation request records HTTP 503 and `not_configured`; no model use is fabricated.

## Endpoints and correlation

All endpoints below require a reviewer role and use the authenticated customer claim. Operators cannot read diagnostics. Another customer's request ID returns 404 even if known. Caller-supplied traceparent/baggage never grants customer access.

| Endpoint | Output |
| --- | --- |
| GET /api/monitoring | Retained request metrics and traces |
| GET /api/monitoring/requests/{id} | One locally generated request ID |
| GET /api/monitoring/export | Downloadable JSON snapshot |

Captured responses include `X-PortOps-Trace-Id` and `X-PortOps-Request-Id`. The first is the distributed W3C trace ID; the second uniquely identifies this local request, even if callers reuse a distributed trace ID. Error response `traceId` uses the same W3C format. Use the local request ID for unambiguous lookup.

## What the numbers mean

- Keep up to **200 completed requests per customer** whose start time is within the last **60 minutes**; timestamps are actual UTC, displayed in Brussels time.
- Store at most **64 steps per request**. Truncated traces are marked; aggregate step counts may then be incomplete and token totals are withheld.
- Average and p95 are computed over retained requests, not lifetime traffic. P95 uses the nearest-rank definition. The UI displays the most recent 20 requests; the export contains the retained set.
- HTTP 4xx includes rejected/invalid requests; HTTP 5xx includes application and provider failures. Caller cancellation is recorded as diagnostic status 499 when observed. A successful HTTP request may contain a failed/repaired tool step.
- Model/token totals refer only to recorded `model.responses` spans. Unknown or incomplete usage stays null, not zero. There is no cost estimate or model accuracy metric.
- The store is bounded process memory, cleared on restart. It is separate from durable proposal/audit storage and is not a persistent monitoring backend.
- Monitoring reads and exports exclude themselves. Only authenticated requests matching an `/api/` endpoint are captured. Static assets, health checks, unknown routes, unauthenticated traffic and early body-size rejections are outside this dashboard. It is not a security/access-log replacement.
- The UI refreshes on opening the panel and manually; it does not claim to stream live updates.

## Data boundaries

Only route templates, methods, status codes, correlation IDs, timings and an explicit allowlist of diagnostic attributes are retained. The collector does not retain raw paths, query values, headers, baggage, actor identifiers, prompts, answers, procedure text, vehicle IDs, proposal bodies, credentials or exception messages. Tool names, outcomes and error codes must belong to fixed allowed sets. Numeric token counts and procedure result counts are supported.

The collector links steps to an internal request scope created after authentication. It never chooses customer scope from activity tags, baggage, query arguments or external trace IDs. A listener associated with another application/test host cannot collect that scope. These restrictions apply to this monitoring feature; they do not configure unrelated framework or third-party loggers.

## Validation

Automated tests verify role checks, cross-customer isolation with shared external trace IDs, header/response correlation, nested proposal steps, failure and timeout spans, bounded retention and tag filtering. Provider-success and provider-failure tests use an HTTP stub behind the real Responses adapter; they validate instrumentation, not a paid model or real-model quality.

Implementation uses the [.NET ActivityListener API](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.activitylistener?view=net-10.0). Existing instrumentation remains available for a future OpenTelemetry listener/exporter. Configure and audit its own filtering before sending diagnostics to an external service.
