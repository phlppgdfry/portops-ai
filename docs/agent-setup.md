# Agent setup and validation

The local API serves the Dutch operations desk at http://localhost:5088. Demo login and model access are separate: enter only the demo token in the browser. Provider keys stay on the server.

## Configure Azure OpenAI

Use the existing resource endpoint and deployed model name from your Azure account. No Azure resource is created by this application. Set these environment variables before starting the API:

```sh
export Agent__Provider=AzureOpenAI
export Agent__Endpoint='https://YOUR-RESOURCE.openai.azure.com'
export Agent__Model='YOUR-DEPLOYMENT-NAME'
# Set Agent__ApiKey securely in your local environment or .NET user secrets.
dotnet run --project src/PortOps.Api --launch-profile demo
```

The API project has UserSecretsId `portops-ai-local`; .NET user secrets are loaded in Development. Use `Agent:Provider`, `Agent:Endpoint`, `Agent:Model`, and `Agent:ApiKey` as secret names. Do not commit real keys. For an OpenAI account, use provider `OpenAI`, an accessible model ID and its API key; the adapter uses the fixed OpenAI Responses endpoint.

The adapter uses the [official Responses function-calling flow](https://developers.openai.com/api/docs/guides/function-calling), strict tool schemas and structured final output. Provider/model compatibility and actual account access still require a live test. Configured status means settings exist, not that connectivity was verified.

## API contract

`POST /api/agent/investigate` requires the same demo bearer token as operational reads:

```json
{"message":"Waarom is DEMO-003 geblokkeerd?","history":[]}
```

History contains at most six objects with `question` and `answer` strings, each at most 4,000 characters. Current messages are limited to 4,000 characters; the UI uses 2,000. Findings return text and retrieved evidence IDs, with corresponding source details, unknowns, tool timings, trace ID and token usage when available.

Default limits: 60 seconds, six model turns, eight tool calls, 2,500 output tokens per model call, one concurrent investigation per customer and ten requests per minute per customer. Request bodies are limited to 64 KiB. Cancellation stops the request; partial answers are not accepted.

Errors: 400 invalid input, 401 invalid demo token, 429 busy/rate limited, 503 missing model configuration, 504 timeout and 502 provider/protocol/source-validation failure. The agent can create a draft through `propose_notification`; it has no approval or delivery tool. Human-only authenticated endpoints perform review and simulated execution.

## Validation and remaining work

Run `dotnet test PortOps.slnx`. Domain, API, tool, runner and HTTP adapter tests use deterministic fixtures and scripted/provider-stub responses. They cover customer isolation, rejected tool arguments, citation membership, bounded execution, cancellation and provider failures. They do not establish actual model accuracy or injection resistance.

After configuring a real model, verify attention overview, DEMO-003 damage hold, DEMO-004 discharge versus pickup release, DEMO-005 conflicting observations and inaccessible DEMO-007. Review every finding against its cited records. Record provider, deployment/model version, run date, prompts, outcomes and failures; do not publish a quality score before evaluation.

Citation validation checks that references were retrieved, not that every sentence follows from those references. Versioned lexical procedure retrieval, local durable approvals and an evaluation runner are implemented. Local monitoring and diagnostic JSON export are implemented. External OTLP export, production database/identity and live-model validation remain outstanding.

## Live evaluation runner

With a configured local model, set `PORTOPS_TOKEN` to an operator demo token and run `python3 scripts/evaluate-agent.py --base-url http://localhost:5089`. The ten-case dataset is `evals/scenarios.json`; output is saved under ignored `artifacts/evals/`. One case intentionally creates a draft, but the runner never approves anything. Automated checks cover tool use, expected citations, restricted content and the execution boundary. Every result still requires the dataset’s manual factual review. An unconfigured model produces a blocked report and exit code 2, never a fabricated score.
