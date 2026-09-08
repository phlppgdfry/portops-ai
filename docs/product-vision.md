# PortOps AI — original product vision

PortOps AI is an independent portfolio project: one logistics operations agent for a fictional Belgian RoRo terminal. It helps dispatchers, operations coordinators and customer-service staff investigate vehicles and bookings, explain exceptions with evidence and prepare actions for human approval.

## Why this project exists

Demonstrate an end-to-end AI engineering workflow alongside .NET backend skills: tool calling, operational rules, procedure retrieval, customer authorization, human approval, model evaluations and observable execution. The portfolio should explain engineering decisions and measured outcomes, not merely show a chat window or list technologies.

The product question is: **Which vehicles and bookings need attention today, why, and what should we do next?**

## First complete demonstration

1. Ask which vehicles and bookings need attention.
2. Read customer-scoped operational data through controlled tools.
3. Investigate holds, readiness, deadlines and vessel-call information.
4. Retrieve a relevant synthetic procedure when necessary.
5. Explain findings with source IDs, observation times and explicit unknowns.
6. Prepare an action or customer message.
7. Have an authorized human review the exact proposal before simulated execution.

The current implementation is an intermediate milestone. Consult [progress](progress.md) for delivered features and outstanding work.

## Generalized practitioner insights

Informal discovery suggested frequent time/status questions, especially readiness for pickup; manual planning preparation; exceptions involving processing, damage or checks; and occasional disagreement between systems during new processes. Existing status portals may already answer simple questions, so value must come from investigation, preparation and exception handling.

These are discovery inputs rather than validated requirements or measured productivity claims. The product remains the broader Logistics Operations Agent. Planning and customer-service workflows are applications within that product.

Potential later workflows include:

- Preparing a customer planning brief from inventory, expected arrivals, pickup readiness and open holds.
- Explaining why a vehicle is not ready, without inventing a release time.
- Showing differences between an operational source and a planning snapshot.
- Preparing a synthetic customs-document request after a defined event, validating required vehicle identifiers and amounts, and routing it for review.
- Routing pricing and contractual questions to the responsible person.

The exact planning columns, source ownership, event triggers and approval responsibilities still need validation. The customs example concerns preparing a request, not implementing a customs declaration.

## Boundaries and responsibilities

| Component | Responsibility |
| --- | --- |
| Operational/TOS adapter | Read records and preserve provenance |
| Domain code | Compute counts, deadlines and readiness; validate data |
| AI agent | Select tools, combine retrieved information and explain findings |
| Procedure retrieval | Supply relevant versioned synthetic documents |
| Authenticated application | Enforce customer scope and action permissions |
| Human reviewer | Approve the exact proposed action |
| Tests and model evaluations | Check software behavior and real-model performance separately |

Arrival, discharge, processing completion and pickup release are separate events. Uncertainty and conflicting observations stay visible. No calibrated delay probabilities are claimed without supporting data and evaluation.

## Repository and integration decisions

`portops-ai` is a standalone public repository. The earlier shipment-tracking-platform was considered as a possible source of reusable components; the current PortOps foundation was implemented independently. Do not claim reuse without a code review.

Start with synthetic fixtures and local acceptance tests. A future TOS adapter can use an authorized API, test environment or export when available. No commercial TOS access or company partnership is implied. MCP and cloud deployment follow a verified local workflow.

Public documentation retains generalized insights only. Interview transcripts, identities, named customer incidents, commercial values and internal documents are outside the public demo.

## Intended portfolio explanation

“I built an AI assistant for RoRo terminal operations that investigates operational exceptions through scoped tools, explains findings with sources and prepares actions for human approval.”

The repository should eventually include a reproducible demo, architecture explanation, actual evaluation results, execution traces and a short demonstration video. Azure supports the chosen .NET/cloud portfolio direction; it does not define the product itself.
