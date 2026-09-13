# Five-minute local PortOps demonstration

All terminal data and procedures are fictional. The operational scenario stays at 7 September 2026, 10:00 Brussels; proposal creation and expiry use actual time.

## Start

This machine already has operator and reviewer tokens in .NET user secrets (`portops-ai-local`). Local copies are in ignored `artifacts/demo-access.txt` and `artifacts/demo-reviewer-access.txt`. Do not commit or share those files publicly.

```sh
dotnet run --project src/PortOps.Api --launch-profile demo -- --urls http://localhost:5089
```

The launch profile defaults to 5088; this machine uses 5089 because AccessFlow owns 5088.

On a fresh machine, set distinct `DemoAuth__Keys__northstar` and `DemoAuth__ReviewerKeys__northstar` environment variables before starting. Generate each with `openssl rand -hex 24`. The operator investigates and prepares drafts; the reviewer can also decide on them.

The workspace includes **Zo doorloop je de demo** with the same basic route. Newly prepared or refreshed concepts use Dutch hold descriptions; stored versions retain the exact text originally reviewed.

## Flow without a model

1. Open http://localhost:5089 and enter the operator token.
2. Inspect six Northstar vehicles, five attention items and one elapsed loading deadline.
3. Select DEMO-003. Its damage hold has no known completion time. Open source details and the fictional work instructions.
4. Click **Conceptbericht voorbereiden**. The new draft appears under **Actievoorstellen**. Read its exact body, sources, simulation destination and expiry. No model was needed: server rules prepared this text.
5. Sign out and sign in using the reviewer token. The persisted proposal remains available for this customer.
6. Expand **Volledig concept en bronnen controleren**, read it, tick the review checkbox and click **Goedkeuren en verzending simuleren**.
7. Inspect the audit log and simulation receipt. No actual email was sent. Refreshing or retrying approval cannot produce a second delivery.
8. Optionally create a second draft and reject it. An expired draft can be refreshed into a new version; review that new version before approving.

## Planning without a model

Open **Voorraad en verwachte aankomsten**. With 7 days selected, Northstar shows 6 stock vehicles, 2 pickup-ready vehicles, 3 vehicles with holds and 2 requiring source review. Expected arrivals contain 12 vehicles with a known current volume and one plan with unknown volume. These are separate from current stock.

Switch to 1 day: stock remains 6, while only IN-101 remains in the arrival list. Expand its sources to compare the older planning time with the current vessel ETA. Click a stock vehicle to investigate its readiness and owner. No arrival or readiness indicator promises a pickup appointment.

## Flow with a model

Configure the provider using [agent-setup.md](agent-setup.md), restart, then ask:

- “Welke voertuigen vragen vandaag aandacht?”
- “Maak een planningsoverzicht voor de komende 7 dagen: voorraad, afhaling, blokkades en inkomende volumes.”
- “Waarom is DEMO-004 niet klaar voor afhaling?”
- “Welke fictieve procedure geldt bij een schadeblokkade?”
- “Maak een conceptbericht voor DEMO-003. Nog niets versturen.”

The agent has six tools and can prepare a draft, but cannot approve it. Missing model configuration disables chat while the direct operational and review flows remain usable. Source membership validation is not a guarantee of factual correctness; run and manually review the evaluation cases before making quality claims.

## Inspect the recorded workflow

As reviewer, open **04 / Technische monitoring** and press **Vernieuwen** after creating or reviewing a proposal. Expand the relevant request to inspect its steps and duration. **Download meetgegevens** saves a customer-scoped JSON report. Counts reset at server restart; the proposal audit log remains durable. See [monitoring](monitoring.md) for details.

## Remaining visual acceptance checks

On desktop and a narrow mobile viewport, walk through login, planning horizon selection, vehicle sources, draft creation, reviewer login, exact-version approval, audit and monitoring. Check keyboard focus, horizontal planning-table scrolling, expanded sources and readable long proposal text. Also inspect empty lists and interrupted network requests. These browser checks are pending; passing API tests alone does not establish visual usability.
