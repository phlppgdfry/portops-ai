# Progress and resume point

Updated: 2026-09-08. Product intent: [original vision](product-vision.md).

## Completed

- .NET operational API, synthetic RoRo records and deterministic readiness rules.
- Dutch operations desk and bounded model loop with five tools.
- Customer-scoped versioned fictional procedure retrieval (lexical matching).
- Deterministic notification drafts, separate reviewer identity and exact-version human approval/rejection.
- Local durable proposals, audit history, expiry and idempotent simulated delivery. No real messages sent.
- 101 automated tests pass locally in Release configuration. Software tests cover customer/role isolation, changed/expired proposals, concurrent approvals, restart persistence and failure handling.
- Nine-case live-model evaluation runner with saved reports and explicit manual review requirements.

## Current validation boundary

Provider endpoint, deployment/model name and API key are still unavailable. No real-model quality or injection-resistance result has been measured. Automated tests use synthetic data and scripted models. Chat remains unavailable until configuration; the direct procedure, draft and review workflow is complete and usable locally.

## Resume

1. Read this file, [demo walkthrough](demo-walkthrough.md) and [agent setup](agent-setup.md).
2. Check `git status` and GitHub Actions.
3. Start the local application on **5089** (5088 is used by AccessFlow on this machine).
4. Operator and separate reviewer tokens are in .NET user secrets; local copies are in ignored `artifacts/demo-access.txt` and `artifacts/demo-reviewer-access.txt`.
5. Keep `src/PortOps.Api/App_Data/` to retain proposals and audit records. It is excluded from Git.
6. Commit and push completed milestones with updated progress notes.

## Next work

1. Configure an accessible model and run/manual-review the prepared evaluation dataset.
2. Add adversarial retrieved-document evaluation fixtures and address observed model failures.
3. Export existing ActivitySource spans and demonstrate operational monitoring.
4. Add production identity and database persistence before cloud deployment; real delivery requires an outbox and adapter.
5. Prepare architecture walkthrough, actual evaluation report and demo video.
6. Integrate authorized TOS data when access exists; add MCP after the local workflow is validated.

Planning briefs and a synthetic customs-document request remain later product extensions, not delivered features.
