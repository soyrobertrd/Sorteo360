# 02 — Rendering: substitution, escaping, and the two MIME parts

The smallest spec in the set, and the one with the most rules per line. It covers turning a caller's
`subject` + `body` + data into the bytes of one message.

**notification-service holds zero copy and renders all of it.** It has no template table, no seed
migration, no default body, no `TemplateRef`. The consequence, accepted out loud in ADR-0020: **it
cannot send an email of its own** — no "your batch failed" note to an operator, no self-generated
anything. The day it needs one is a new decision, not a loophole in this one. Until then an
operational failure surfaces through logs and `05`'s read API.

**Why C# rendering rather than vendor templates:** not a preference and not a rejection. SendGrid's
Dynamic Transactional Templates are **unreachable over SMTP** — their own Danger callout reads *"The
X-SMTPAPI does not support Dynamic Transactional Templates."* The client's transport choice removed
the option rather than losing an argument to it. There is no vendor-side asset left, so there is no
vendor lock-in to trade against either.

## 1. The syntax is a contract

**Flat, literal `{{key}}` replacement. Nothing else.** No conditionals, no loops, no helpers, no
`{{{raw}}}` escape hatch.

- Anything past plain substitution would be an untestable bet: SendGrid documents *"most but not
  all"* of Handlebars with the exclusions unenumerated, and we are not using their engine anyway.
- Flat `{{key}}` sits inside every candidate engine's subset, so the engine stays replaceable.

Cost accepted: there is no `{{#if empresa}}`, so a missing optional value renders as whatever
neutral fallback the **caller** put in `data`, rather than dropping the sentence around it.

> **Normative: every substituted value is HTML-escaped.** `{{nombre}}` arrives from a
> client-uploaded spreadsheet, so `Juan <script>` or a stray `&` is a live path from an xlsx cell
> into an email body.

**Resolution order is one flat lookup, not a cascade.** A key present in both `sharedData` and a
recipient's `data` was already rejected at `01` §2 with a `400`, so by the time rendering runs the
two dictionaries are disjoint and there is no precedence question to answer.

## 2. An unresolved placeholder is a `400`, before the `202`

Scan `subject` and `body` for every `{{key}}` occurrence and check each against `sharedData` plus
**each** recipient's `data`. A miss rejects the whole request, naming the key
(`NOTIFICATION_PLACEHOLDER_UNRESOLVED`). Nothing is persisted and nothing is sent.

This is the map's own rule applied to rendering: **after the `202` there is nobody left to tell.**

> **Normative: the key names are never this service's business.** It does not hold, validate
> against, or know a list of legal placeholders. It only asks whether every key the caller's own
> body mentions has a value the caller also supplied.

That is why convocatorias' closed seven-key set is validated **in convocatorias**, against its own
seeded rows, by its own test — and why a caller's neutral-greeting fallback happens caller-side, so
the key is already present in `data` by the time the request is built and this check passes.

## 3. Two MIME parts, one authored body

Every send goes out as `multipart/alternative`.

> **Normative: nobody authors copy twice.** The `text/plain` alternative is **derived** from the
> already-rendered HTML at send time. There is no second body on the wire, no second column, and no
> second thing for a copy fix to forget.

This resolves a standing tension: a second authored body doubles every template, while an
HTML-only message scores worse with spam filters. Deriving costs one converter and no template work
at all. **MimeKit ships one**; use it rather than writing a stripper.

**No shared shell or branded letterhead.** A template is one whole email and goes out byte for byte
after substitution. The two jobs a wrapper would have had are already placed elsewhere: campaign
arte lives inside the body, and `{{enlace_baja}}` is a body placeholder. Duplication across two
emails is not duplication worth a mechanism; build a letterhead the day a second and third caller
genuinely duplicate one.

## 4. Language

`language` is **required on every send, with no default**. events-service's transactional alerts
will always pass `"es"`, and that is fine: one shape for every sender, and a history row that
explains itself years later. An optional field here produces a nullable column nobody can interpret.

The service does not *use* `language` for anything — it stores it. Choosing copy per language is the
caller's job, keyed `(template_key, language)` in the caller's own table.

## 5. Language of the copy itself

Everything in this repo is English. **The email copy is Spanish and stays Spanish**, alongside code
comments and log messages. That is ADR-0008's rule, not an exception to it: audience decides.

## Definition of done

- `{{a}} y {{b}}` with `a` in `sharedData` and `b` in a recipient's `data` renders both.
- A value containing `<script>` and `&` appears HTML-escaped in the rendered HTML part.
- A `{{key}}` present in the body with no value anywhere returns `400
  NOTIFICATION_PLACEHOLDER_UNRESOLVED` **naming the key**, and creates nothing. Cover a key missing
  for only *one* recipient of several.
- A `{{key}}` in the **subject** is checked the same way as one in the body.
- A key present in `data` but absent from the body is ignored and does not fail.
- The sent message is `multipart/alternative` with **both** parts present and **distinct**, and the
  text part contains no tags.
- `{{` appearing with no closing braces, and an unknown-but-unused key, neither crash nor reject.
- The engine has no branch on `type`, no template lookup and no hard-coded key list — assert by
  rendering a body with an invented placeholder the service has never seen.
