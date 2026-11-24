# Notifications

Getting a finished email out of the platform and keeping an honest record of what happened
to it. A caller writes the email and says who gets it; this context substitutes the
placeholders, opens the SMTP socket, and records one row per person — including the
refusals.

> Naming: domain names are English platform-wide (ADR-0008). Prose is Spanish —
> the end-user UI, code comments, log messages, error text, and the API
> documentation `/scalar` renders. Stored codes are frozen as-is.
>
> Each entry below records both: the **English identifier** used everywhere the
> type system looks, and the **canonical Spanish rendering** used everywhere
> prose is written. `_Avoid_` lists apply to **identifiers only** — the Spanish
> word is not a mistake in a sentence, it is the required word.
>
> **This context has no vocabulary of its own about what the mail says.** There is no
> Template, no Evento, no Cotización, no OPT here, and there never will be. If a word
> from another context is needed to name something in this one, the design has drifted
> (ADR-0020).

## The send

**NotificationBatch** — es. *lote*:
One accepted `POST`, and the unit the history is browsed by. Holds what the email said —
`type`, from-address, subject, body — for every person on it. **The body is stored
pre-substitution**, still carrying its `{{placeholders}}`: a live credential lives only in
`data`, which is stored nowhere, so no token can ever be inside a stored body. The safety
is structural, not a scrubbing rule somebody can weaken later. The accepted cost is that
the history shows what a batch *said*, not the precise bytes one named person received.
_Avoid in identifiers_: Lote, Campaign, Blast, Mailing, Message (unqualified)

**NotificationType** — es. *tipo*:
A lowercase dotted label, `<service>.<thing>.<what happened>` — `events.quote.submitted`.
**A history label and nothing else**: stored verbatim, no catalog, no lookup, no foreign
key, never an enum serialised as a number. Rows about a retired type still read correctly,
and reordering an enum cannot silently rewrite the meaning of old rows.
_Avoid in identifiers_: Tipo, Kind, Category, TemplateKey, EventName

**NotificationRecipient** — es. *destinatario*:
One person on one NotificationBatch, and the grain of everything that happens afterwards:
one SMTP transaction, one row, one opaque GUID. There is **no `cc` and no `bcc`, ever**, so
a recipient row always means exactly one person. Carries its own delivery state, which
never derives from the batch's. May carry the **name the mail was addressed to**: a fact of
the send, like the subject, frozen at send time and never looked up again. It is not the
`nombre` placeholder value, which is a data-bag entry the record does not keep.
_Avoid in identifiers_: Destinatario, Recipient (unqualified), Address, Contact, Person

**IdempotencyKey** — es. *clave de idempotencia*:
Caller-minted, derived from a natural key, never random, never expiring. **It names the
send, not the thing the send is about**: `events:quote:{quoteId}:attempt:{n}:{moment}`,
with the caller prefix because the key space is global. A repeat replays the original ids
inside a `202` — never a `409`, which would be a false alarm about a request that worked.
This is what makes the chassis's blind, verb-agnostic retry safe for a write.
_Avoid in identifiers_: RequestId, DedupeKey, CorrelationId, Nonce

**NotificationPayload** — es. *valores de sustitución*:
The flat `{key -> value}` bag one NotificationRecipient is rendered with — `sharedData` and
that person's own `data` already merged, which is safe because a key in both was rejected
with a `400` before anything was written. It exists because **the send happens after the
`202`**: the worker has to substitute at send time and the record deliberately stores no
`data`. It is the one place in this service that holds a `data` value, and it is **deleted
in the same transaction that makes the send terminal** — sent, dropped or failed. So a
registration token lives exactly as long as its own send does, and none of the four record
tables ever contains one.
_Avoid in identifiers_: Data, Variables, Context, Model, Payload (unqualified)

**NotificationAbout** — es. *sobre qué*:
What a batch is about: `(sourceService, entityType, entityId)`, one row per entity,
indexed. It answers the client's actual question — *show me everything sent about the
Feria de Madrid*. **It never leaves the platform**: entity ids are never put in
`unique_args`, which cannot be erased once sent.
_Avoid in identifiers_: Subject, Reference, Context, Metadata, Tag

## What happened to it

**DeliveryStatus** — es. *estado de entrega*:
What the **transport** did: `queued` → `sent` → `delivered` / `bounced` / `dropped` /
`deferred` / `failed`. We write the first two; the webhook writes the rest. A `dropped` row
is a refusal that was **recorded, never silently skipped**. Separate from RecipientAction on
purpose: conflating "the mail arrived" with "the person reacted" makes a complaint look like
a failure.
_Avoid in identifiers_: Status (unqualified), Estado, State, Result

**RecipientAction** — es. *acción del destinatario*:
What the **person** did: `none` / `unsubscribed` / `complained` (later `opened`, `clicked`).
Written only by the webhook. A `spamreport` sets `complained` and **leaves `delivery_status`
at `delivered`** — the mail did arrive, and that is a different fact.
_Avoid in identifiers_: Action (unqualified), Engagement, Response, Feedback

**NotificationEvent** — es. *evento*:
One webhook event from the provider, stored raw alongside its derived meaning. An
unrecognised event type is **stored and ignored** when deriving status, never rejected.
_Avoid in identifiers_: Evento, WebhookPayload, Callback, Hook

**Orphan** — es. *evento huérfano*:
A NotificationEvent whose `recipient_id` is null: an event we could not attach to a send we
know about. **Never guess, never drop.** Not a separate table — a linked event and an orphan
are the same kind of thing, so they share one bucket and one query, and an orphan can later
be linked by filling the column in. It is also what makes one shared provider account across
two environments safe: an event for a GUID this environment never issued simply becomes an
orphan.
_Avoid in identifiers_: Unmatched, Unlinked, Dangling, Huerfano

**Suppression** — es. *supresión*:
An address this service refuses to send to, fed by `unsubscribe` and `spamreport` events.
Checked **before the socket opens**, and a refusal becomes a `dropped` row. This is
**deliverability**, and it is deliberately **not** the CRM's `opted_out`, which is a consent
fact about a person: conflating a bounced mailbox with a withdrawn consent would make a
typo'd address look like a decision, unrecoverable through the consent UI. A suppression
never syncs back to the CRM.
_Avoid in identifiers_: OptOut, Blocklist, Blacklist, Unsubscribe (as a noun), DoNotEmail

**DrainWorker** — es. *worker de drenaje*:
The background loop that takes NotificationRecipient rows that are `queued` and due, and
sends them. Claims one row at a time with `FOR UPDATE SKIP LOCKED`, so two drainers send
each row exactly once, and writes each row's status as **its own** SMTP transaction
finishes — never once at the end of a batch, which would lose what already went out when a
batch dies part-way. One reused connection, drained sequentially. Retry is SMTP's own rule
and not ours: `4xx` later, `5xx` never, `535` terminal and loud, and everything stops after
one hour, because mail about a decision made yesterday has negative value.
_Avoid in identifiers_: Sender, Dispatcher, Queue (unqualified), Processor, Job

**TrackingExpected** — es. *seguimiento esperado*:
Stamped on each batch **at insert** from configuration, never read as a global flag at query
time, so a row written while no webhook existed stays honest after someone enables one.
**When it is false, `delivery_status` never passes `sent`** — otherwise a permanent `sent`
would read as a stuck queue. Stage is deliberately such an environment.
_Avoid in identifiers_: TrackingEnabled, HasWebhook, WebhookOn

## Who owns which question

**Resolution** — es. *resolución*:
Answering *who is this person and what is their address?* It belongs to the **caller**, the
only party that knows what a role or a campaign means. This context never resolves.
_Avoid in identifiers_: Lookup, Targeting, Audience

**Deliverability** — es. *entregabilidad*:
Answering *can this address receive mail at all?* It belongs **here**, because the answer
arrives on this context's webhook and reaches nobody else. Suppression is its record.
_Avoid in identifiers_: Consent, OptOut, Mailability

## The two kinds of recipient, and why this context cannot tell them apart

**InternalRecipient** — es. *destinatario interno*:
A MITUR person: address of record is Keycloak, always mailable, **no unsubscribe link**.

**ExternalRecipient** — es. *destinatario externo*:
A CRM contact: address of record is `email_normalized`, gated by `canBeEmailed`, and the
body carries `{{enlace_baja}}`.

> **Normative: both arrive here as `{ email, data? }` and this context must not be able to
> distinguish them.** Every difference lives in the caller, which is the only party that
> knows what a role or a campaign means. The moment the pipe can tell, it has started
> deciding who deserves to be told — which is the caller's decision, not its own.

The one place the difference surfaces is the `List-Unsubscribe` header, and it surfaces
**without** the distinction being made here: the header is present when the recipient's
values carry `enlace_baja` and absent when they do not. That is the same boundary seen from
the only side this service can reach — an external recipient brings the link because their
mail needs it, an internal one never does — so the caller stays the only party that decided
anything.

_Avoid in identifiers_: User, Staff, Member, Subscriber
