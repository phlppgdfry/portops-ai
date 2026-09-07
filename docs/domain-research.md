# Domain research

Research date: 2026-09-07. Scope: initial public-source check, not an exhaustive study or validation of internal terminal procedures.

## Public evidence

| Source | Supported observation | Design implication (our inference) |
| --- | --- | --- |
| [ICO: Services](https://www.icoterminals.com/en/services) | Services span ship handling, vehicle processing, terminal handling, breakbulk, agency and logistics. | Keep the product wider than a customer mailbox assistant. |
| [ICO: Terminal handling](https://www.icoterminals.com/en/services/terminal-handling) | Terminal activities include loading/unloading, storage, truck gates and rail handling; terminals have multiple transport connections. | Model vehicle journeys and terminal events separately from ocean voyages. |
| [Port of Antwerp-Bruges: RoRo & automotive](https://www.portofantwerpbruges.com/en/business/cargo/roro-automotive) | Automotive services include pre-delivery inspection, repairs, accessory installation and treatments. | Processing jobs and holds are useful entities for a synthetic operational model. |

Public service descriptions establish domain context. They do not disclose TOS schemas, integration access, customer permissions, release rules or actual operating procedures. The port's sailing-list description concerns maritime connections; it does not establish access to a real-time vessel ETA feed.

## Discovery hypotheses

An informal practitioner conversation reported by the project owner suggests that time/status questions, readiness for pickup, manual planning preparation and new-process exceptions are useful scenarios. These observations are not representative measurements of the sector and do not establish an endorsed customer requirement.

Retain only generalized insights in public project material. Do not include the interview transcript, the person's identity, named customer incidents, contracts or commercial values.

## Synthetic demo assumptions

- RFP means “ready for pickup” in this demo; its exact operational definition is adapter-specific.
- Pickup readiness and readiness for loading onto a vessel are separate concepts.
- Vessel arrival, discharge completion, processing completion and release are distinct events.
- A vessel being discharged does not automatically release every vehicle for pickup.
- Holds have recorded reasons, owners and timestamps; expected completion may be unknown.
- A booked departure has an explicit configured deadline. No universal terminal cut-off is claimed.
- Demo urgency is a documented rule category, not a calibrated probability of delay.
- Contradictory sources remain visible until reconciled; the model does not silently choose one as truth.

## Remaining validation

Determine the exact fields required for a useful planning brief, which source owns each field, how freshness is judged, and which actions each role may approve. These can start as documented demo policies and later be checked with practitioners.

Any customs-related workflow initially prepares a synthetic request for review. It does not implement a customs declaration or claim legal completeness.
