# Platform

Words that belong to no single service: the ones that describe how the Sorteo360 platform
meets external systems and staff portals. Each service's own vocabulary lives in its
`GLOSSARY.md`; see `GLOSSARY-MAP.md`.

> Same naming rule as every context (`ADR-0008`): the **English identifier** goes wherever the
> type system looks, the **Spanish rendering** wherever prose is written. `_Avoid_` lists apply to
> **identifiers only**.

## Language

**External System** — es. *sistema externo*:
Software outside Sorteo360 that integrates via webhooks or authenticated machine-to-machine APIs.
_Avoid in identifiers_: Vendor, Partner, ThirdParty

### The staff portal

**Module** — es. *módulo*:
A top-level area of the staff portal: Eventos, Convocatorias, Sorteo.
_Avoid in identifiers_: App, Area, Section

**Feature** — es. *función*:
One entry in a Module's sidebar, belonging to exactly one Module. Another Module's sidebar may
link to it, and that link follows the Feature's own switch rather than having one.
_Avoid in identifiers_: Screen, Page, MenuItem

**Feature Switch** — es. *interruptor*:
Whether a Module or a Feature is **enabled** or **disabled** for everyone in one environment. It
decides what the portal shows, never who may do what; that is a role's job.
_Avoid in identifiers_: Flag, FeatureFlag, Toggle
