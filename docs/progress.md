# Progress and resume point

Updated: 2026-09-13. Product intent: [original vision](product-vision.md).

## Completed

- .NET operational API, synthetic RoRo records and deterministic readiness rules.
- Dutch operations desk and bounded model loop with six tools.
- Customer-scoped versioned fictional procedure retrieval (lexical matching).
- Deterministic notification drafts, separate reviewer identity and exact-version human approval/rejection.
- Local durable proposals, audit history, expiry and idempotent simulated delivery. No real messages sent.
- Monitoring collects customer-scoped traces, timings, errors and reported model usage; a reviewer can inspect and export a bounded local snapshot.
- Customer-scoped planning with explicit inventory and inbound volumes, 1/7/14-day UI horizons, source warnings and a sixth agent tool. See [planning rules](planning.md).
- 127 automated tests pass in Release configuration. Monitoring tests cover correlation, privacy, retention and customer/role isolation. Software tests cover customer/role isolation, changed/expired proposals, concurrent approvals, restart persistence and failure handling.
- Ten-case live-model evaluation runner with saved reports and explicit manual review requirements.

## Current validation boundary

Provider endpoint, deployment/model name and API key are still unavailable. No real-model quality or injection-resistance result has been measured. Automated tests use synthetic data and scripted models. Chat remains unavailable until configuration; the direct procedure, draft and review workflow is complete and usable locally.

## Resume

1. Read this file, [demo walkthrough](demo-walkthrough.md) and [agent setup](agent-setup.md).
2. Check `git status` and GitHub Actions.
3. Start the local application on **5089** (5088 is used by AccessFlow on this machine).
4. Operator and separate reviewer tokens are in .NET user secrets; local copies are in ignored `artifacts/demo-access.txt` and `artifacts/demo-reviewer-access.txt`.
5. Keep `src/PortOps.Api/App_Data/` to retain proposals and audit records. It is excluded from Git.
6. Commit and push completed milestones with updated progress notes.

## Next work — portfolio v1.0

1. Finish consistent Dutch copy and the complete demonstration flow.
2. When the owner chooses model access, configure it and run/manual-review the prepared evaluation dataset. Add adversarial retrieved-document cases and address observed failures.
3. Prepare the architecture walkthrough, actual evaluation report, demo video and v1.0 release.

Paid model access and Azure resources are intentionally deferred. Monitoring works locally without them; no live-model results are claimed.

## Later extensions

Authorized TOS integration, real delivery with an outbox, production identity/database storage, external OTLP monitoring, MCP, cloud hosting and a synthetic customs-document request are outside the current portfolio v1.0 finish line.
