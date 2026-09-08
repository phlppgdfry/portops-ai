# Progress and resume point

Updated: 2026-09-08.

## Completed

- Domain/API foundation previously published in commit `18f148a`.
- Read-only agent with three scoped operational tools and Azure OpenAI/OpenAI Responses adapter.
- Retrieved-source membership validation, bounded requests, cancellation and provider failure handling.
- Dutch local operations desk, source inspection and chat interface.
- 84 automated tests pass locally. These are software tests using synthetic fixtures and scripted model responses, not real-model evaluations.

## Next step

Configure the model provider locally as described in [agent-setup.md](agent-setup.md), then run and record the real-model checks there. No provider endpoint, deployment/model name or API key was available during this milestone. The operational interface is usable without a configured model.

After live validation: procedure retrieval and reviewable action proposals. Persistence, approval execution, tracing export, MCP and Azure deployment are still planned.

## Resume

1. Read this file and `docs/agent-setup.md`.
2. Check `git status` and the most recent GitHub Actions run.
3. Start `dotnet run --project src/PortOps.Api --launch-profile demo`.
4. Open http://localhost:5088 and use the configured Northstar demo token. This machine stores it in .NET user secrets; a local copy is in ignored `artifacts/demo-access.txt`.
5. Commit and push completed milestones and update this progress file. Keep credentials and generated build output outside Git.
