# Email Templates and Substitution — Spec

A template is one whole email, subject and body, in one language. The user selects one; in phase 1
nobody edits one. Artwork lives inside the body. The substitution engine that fills a template is
here too, because the placeholder rules and the engine are one decision.

## Decisions behind this model

- **The client's rule is a product rule**: a fixed set, the user selects one, nobody edits, stated
  explicitly so the system does not end up with fifty templates.
- **The frontend does the opposite.** `PlantillasConvocatoriaPage` (1,929 lines) and
  `PlantillaEditorModal` (760 lines) are a drag-and-drop block builder backed by `localStorage`, and
  they are identical on `origin/frontend`, so unlearning them is real frontend work. The half worth
  keeping is Roberto's catalog, keyed `(idioma, tipo)` with `{ asunto, cuerpo }` entries.
- **Convocatorias owns the copy** because email copy about convocatorias is domain. ADR-0017.

## 1. The `template` table

Seeded once by a data migration. **No create, update or delete endpoint in phase 1.**

| Column | Notes |
| ------ | ----- |
| `template_key` | part of the key |
| `language` | part of the key, one of the six in `AllowedLanguage` |
| `subject` | substituted like the body |
| `body_html` | the complete email, artwork included |
| `purpose` | a label, `campaign` or `transactional` |
| `is_campaign_selectable` | whether it appears in the picker |

Unique on `(template_key, language)`. One flat list with a `purpose` label, **not** a
`TemplateCatalog` entity. A catalog per `EventType` is the plausible future; it would arrive as a
column and a query, which is exactly why inventing the entity now would be the expensive mistake.

No plain-text alternative body, which doubles the copy every template needs and which no requirement
mentions. No `required_placeholders` (§2). No `arte_url` (§4).

**The table ships now precisely so the endpoints are additive.** The client stated that MITUR staff
will eventually manage templates themselves. That is a stated requirement rather than a hypothetical,
so the model ships and the policy waits. The cost accepted is that Spanish copy stops being diffable
in a PR the day those endpoints land.

Adding a template today costs a data migration plus a deploy. That friction is deliberate: a
self-service editor is the mechanism that produces fifty templates.

### 1.1 Seeding

**`migrationBuilder.InsertData`, never `HasData`.**

`HasData` is model-managed and re-asserts its values on every migration, so it would silently revert
a template a user edited through the future endpoints. Nothing in this repository does this yet, so
it is a fresh mistake available to be made.

### 1.2 The one endpoint

`GET /v1/convocatorias/templates`, `convocatorias:read`, returning `template_key`, `language`,
`purpose`, `is_campaign_selectable` and `subject`. **Not `body_html`**: the picker shows a name and a
subject line, and shipping a thousand-line HTML body per row into a dropdown is waste.

`?selectable=true` filters to what a campaign may actually choose, which is what
`is_campaign_selectable` exists for. Without this read, that column has nothing to serve and the
frontend has no way to build the picker the client asked for.

**No create, update or delete.** Those arrive with staff-managed templates, and this read is what
makes them additive rather than a first endpoint.

## 2. Placeholders are derived, never entered

Two things that are easy to conflate:

- **The supported set is code**, owned by developers, because filling a variable is code.
- **The used set is scanned out of the body** at seed time.

The rule: **every placeholder in a body must be in the supported set**, enforced by a test over the
seeded rows. `{{evneto}}` fails the build instead of reaching a thousand inboxes. Nobody types a
placeholder list into a form, and no `required_placeholders` column exists.

The supported set, seven entries:

| Placeholder | Grain | Filled from |
| ----------- | ----- | ----------- |
| `{{nombre}}` | recipient | the recipient's first name |
| `{{evento}}` | event | the event name |
| `{{lugar}}` | event | the event's `RoadshowCity` rows, name and country, joined |
| `{{fecha}}` | event | the event dates |
| `{{enlace_registro}}` | recipient | `Portal__BaseUrl` plus `#t=<token>` |
| `{{qr}}` | recipient | the QR image URL |
| `{{enlace_baja}}` | recipient | the unsubscribe URL, `#u=<token>` |

### 2.1 The inverted test for campaign templates

For a template whose `purpose` is `campaign`, the seeded-rows test **inverts**: `{{enlace_baja}}`
must be **present**, and missing is a build failure. A campaign email that cannot carry an
unsubscribe link is the failure this catches.

**Transactional templates are exempt and must not carry it.** `registration_confirmation` answers the
person's own action, so there is nothing to unsubscribe from.

It is the only transactional key in the day-one set (§7). `campaign_approval_request` was the second
one and is gone with the approval step it served.

### 2.2 An unresolved placeholder splits by grain

- **Event-level** (`{{evento}}`, `{{lugar}}`, `{{fecha}}`) is checked inside the launch request, at
  step 4 of `04` §1, before the flip to `sending`. A missing value **refuses the launch**, naming the
  field, so nobody sends "se celebrará en ." to a thousand people. It has to be checked before the
  `202`: after it there is nobody left to tell.
- **Recipient-level `{{nombre}}`** falls back to a neutral Spanish greeting, with the upload staging
  screen showing how many rows will get it. Blocking a 1,000-recipient campaign because one imported
  row carries only an email address is the tail wagging the dog. Skipping those recipients was
  rejected: it silently drops people the coordinator believes were invited.

## 3. Language

**Dictated by the `Event`** (`01` §2). A campaign cannot override it, and there is **no fallback to
Spanish** when a variant is missing: the launch is blocked until someone writes the copy. A Spanish
email to a Madrid trade-press contact is worse than a refused launch that names the missing template
key.

## 4. Arte

**Inside `body_html`, as an `https://` `<img src>`.** The artwork is the same for every roadshow
today, so it is changed the way copy is changed.

No `arte_url` column, no upload endpoint, no `stored_file` table. **convocatorias is not an ADR-0014
consumer.**

**No liveness check at send time, ever.** Checking a URL across a 1,000-recipient blast turns a flaky
CDN into a failed campaign, and an email client that cannot load the image already shows alt text.

The day a campaign needs its own artwork, that is either an `arte_url` on `campaign` or a real
upload, and only the second makes this service an ADR-0014 consumer. Not now.

## 5. The substitution engine

**Flat, literal key replacement. No conditionals, no loops, no helpers, no `{{{raw}}}` escape
hatch.** This is a contract, not an implementation detail, because `04` sends the body across a
service boundary.

- Flat `{{key}}` sits inside every candidate engine's subset, so nothing past plain substitution is
  an untestable bet on a provider's template dialect.
- **Updated 2026-09-12.** Both original reasons have expired, and the decision is stronger for it. One
  argued that anything past plain substitution was *"a bet on a provider nobody has chosen"* — the
  client has since chosen SendGrid. The other argued that flat keys *"keep the rendering location
  genuinely open"* — the transport is SMTP relay, and SendGrid's docs state plainly that
  *"The X-SMTPAPI does not support Dynamic Transactional Templates."* There is **no provider-side
  rendering location left to keep open**: we render, always. So the engine is ours, its dialect is
  ours to test, and flat literal substitution is now a choice about what a template author may write
  rather than a hedge against an unknown provider. See `04` §2 and
  `.scratch/notifications-map/research-sendgrid-smtp-relay.md`.

**Values are HTML-escaped into the body.** `{{nombre}}` arrives from a client-uploaded spreadsheet,
so `Juan <script>` or a stray `&` is a live path from an xlsx cell into an email body.

**Amended 2026-09-14: the subject is substituted by the same engine but not HTML-escaped.** An
earlier draft said "values are HTML-escaped on substitution" without qualifying where, which read as
covering both. It cannot: a subject is a header and no HTML parser reads it, so escaping would put
`Feria &amp; Turismo` in the inbox. What the subject gets instead is its line breaks flattened — a
subject has no legitimate ones, and a value carrying one could end that header and start another.
notification-service, which is what actually substitutes on the send path (`04` §2), already splits
the two this way, so the amendment aligns the sentence with the behaviour both sides ship.

Cost accepted: there is no `{{#if empresa}}`, so a missing optional value renders as the §2.2
fallback rather than dropping the sentence around it.

## 6. History is metadata, not copy

`campaign` carries `launched_by`, `sent_at`, `template_key` and `language`. `campaign_recipient`
carries the name and address snapshot. **There is no `body_html` snapshot anywhere.**

The accepted consequence, stated out loud: a typo fixed in March rewrites what a September campaign
appears to have said. That is fine for a tourism invitation and would not be fine for anything
contractual, which nothing here is. Reconstructing what one person received needs the `template` row
and their `campaign_recipient` row, and the template row may have moved since. Reconstructable, not
identical.

`04` §6 carries the sharper version of the same constraint: per-recipient data holds a live
credential, so nothing retains it.

## 7. The day-one set, provisional

Five keys, `es` only:

| Key | `purpose` | Selectable |
| --- | --------- | ---------- |
| `save_the_date` | campaign | yes |
| `registration_invite` | campaign | yes |
| `registration_confirmation` | transactional | no |
| `event_cancelled` | transactional | no |
| `direct_message` | campaign | no |

The set moved twice on 2026-09-07. `campaign_approval_request` was dropped with the approval step: it
was the email that asked a director to decide, and there is no decision to ask for. `event_cancelled`
arrived with `06` §4.7, the notice every registrant gets when an event is cancelled. It is
**transactional** because its list is derived from registrations rather than selected from CRM, which
is also why it carries no `{{enlace_baja}}`: §2.1's rule holds, and a person cannot unsubscribe from
being told the event they signed up for is off. Its body is generic — no cancellation reason, no link
to any other event — and the client owns the real copy.

`direct_message` arrived on 2026-10-03 with the campaign that has no event (`03` §1.1, PGTI-174).
Its subject is `{{asunto}}` and its body wraps `{{mensaje}}` in `white-space:pre-line`, so the text
the coordinator typed keeps its line breaks while being escaped like any value. It carries
`{{enlace_baja}}` and no `{{enlace_registro}}`. It is not selectable: a campaign takes it if and only
if it has no event. `asunto` and `mensaje` are a third grain, **campaign**: their value is the
campaign row's own, and create already refuses a direct message without them.

**The copy is not ours to invent.** MITUR already runs these emails in the system this platform
replaces, and the owner is retrieving them, in `es`, `en` and `fr`, since the portal renders three
languages off `evento.language`. The model above is settled regardless; the key list, the supported
set and the two §2.2 rules are provisional against what those templates turn out to contain.

The mock's remaining campaign types — Pase VIP, Prensa & Medios, Agenda & Programa, RSVP, Cambio de
Fecha, Cambio de Venue, Posposición, Cancelación, Carta de Agradecimiento, Encuesta, Highlights,
Certificado — have no client requirement behind them and are not in the day-one set.

## Definition of done

- `GET /templates?selectable=true` returns the two campaign keys and neither transactional one.
- The `template` table exists with the four `es` rows, inserted by `InsertData` and not `HasData`.
  Prove it: run a second, unrelated migration and confirm the seeded rows are not re-asserted.
- A test over the seeded rows fails when a body carries a placeholder outside the supported set.
- The same test fails when a `campaign` template lacks `{{enlace_baja}}`, and when a `transactional`
  one carries it.
- Substitution replaces every supported key, escapes its values, and leaves an unknown `{{...}}`
  sequence untouched rather than throwing.
- A missing event-level value is reported to the caller by field name; a missing `{{nombre}}` renders
  the neutral greeting.
- `subject` is substituted by the same engine as `body_html`, proven by one test.
