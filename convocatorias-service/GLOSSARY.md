# Convocatorias

Inviting people to a MITUR event and tracking what became of each invitation:
the email campaigns that go out, the public portal where an invitee registers
with no login, and the read that answers who was invited, who registered, and
who did not.

> Naming: domain names are English platform-wide (ADR-0008). Prose is Spanish —
> the end-user UI, code comments, log messages, error text, the email copy
> itself, and the API documentation `/scalar` renders. Stored codes are frozen
> as-is.
>
> Each entry below records both: the **English identifier** used everywhere the
> type system looks, and the **canonical Spanish rendering** used everywhere
> prose is written. `_Avoid_` lists apply to **identifiers only** — the Spanish
> word is not a mistake in a sentence, it is the required word.
>
> **The context keeps its Spanish name.** "Convocatorias" is the service's name
> in the folder, the Compose service, the database and the route prefix. An
> English label for the context alone would give one thing two names. Every term
> inside it is English.

## Campaigns

**Campaign** — es. *campaña*:
One email going out to one list of people, in one language, from one Template, usually for
one Event. An Event has many. **A Campaign may have no Event**: a direct message to one or
more contacts, with no registration link and so nothing to register for; it keeps every
consent and suppression check an Event's campaign has. A Campaign is history: once launched, what it says and
who it reached never changes. Nobody approves one; whoever holds the role launches it.
**Its list is _selected_ by a person from CRM**, by hand or from a CRM ContactList — that is what separates it from a
CancellationNotice, whose list is derived from rows we already hold.
_Avoid in identifiers_: Campana, Convocatoria, Blast, Mailing

**CancellationNotice** — es. *aviso de cancelación*:
The one email every Registration on an Event gets when that Event is cancelled. Not a
Campaign: nobody selects its list, it has no draft, no launch and no unsubscribe footer,
and it is suppression-exempt, because somebody who opted out of marketing still has to
hear that the event they signed up for is off. Fired by the Event's **cancellation**,
never by its closure — a closed Event happened, and telling its attendees it was
cancelled would be a lie.
_Avoid in identifiers_: CancellationCampaign, Cancelacion, Abort, Notice (unqualified)

**Launch** — es. *lanzar*:
Putting a Campaign out: one person, one call, `draft` to `sending`. The campaign-grain
verb, and the reason `send` is never used at this grain. A Campaign is launched; a
CampaignRecipient is sent to, which is what `send_status` and the send gateway mean.
The two never swap.
_Avoid in identifiers_: Send, Enviar, Dispatch, Publish, Blast

**CampaignRecipient** — es. *destinatario*:
One person on one Campaign's list. Holds the CRM contact id for identity plus the
name and address **snapshotted at send time**, because a contact edited next March
must not rewrite what was sent in September. Carries its own send state, which
never derives from the Campaign's.
_Avoid in identifiers_: Destinatario, Invitee, Recipient (unqualified), Contact

**CampaignExclusion** — es. *exclusión*:
A person who was selected for a Campaign and deliberately not mailed, with the
reason recorded (`no_email`, `opted_out`, `do_not_invite`, `contact_unavailable`).
Never a CampaignRecipient: a recipient is someone we intended to mail, and keeping
the two apart is what stops every count double-reporting.
_Avoid in identifiers_: Excluido, Skipped, Filtered, Suppressed

**DirectMessage** — es. *mensaje directo*:
A Campaign with no Event: a subject and a message written for one or more contacts, with no
registration link and nothing to register for. Still a Campaign, so every consent and
suppression check applies. It goes out in the `direct_message` Template, which nobody picks by hand.
_Avoid in identifiers_: Email (unqualified), Message (unqualified), Blast, Mensaje

**CompanyRegistration** — es. *registro de aliados*:
A Campaign with no Event that asks each contact to describe themselves and their company. What
they submit lands in CRM as a Company, a Contact and the affiliation between them. The link
never closes, and a forwarded link registers the colleague too.
_Avoid in identifiers_: PartnerRegistration, Census, Censo, Padron, DataCensus, Onboarding

**SupplierIntake** — es. *carga de proveedores*:
A one-time drive to fill the supplier directory. Each OPT office gets one secret link, and its
staff use it to send the suppliers that office already works with, each one signed with the name
of the OptStaff who sent it, picked from the office's active staff as HR carries them. What they send becomes, at once and with no review, a Company with its
contacts and bank account in CRM, plus a ServiceSupplier in Events. A supplier that is already
there gets the missing parts added, not a second copy. Not a Campaign: nobody gets mailed, and the
link names an office, not a person. Not a CompanyRegistration: the people filling it work for MITUR
and describe someone else's company.
_Avoid in identifiers_: SupplierOnboarding, SupplierRegistration, SupplierCensus, Carga, Intake (unqualified)

**SupplierIntakeSend** — es. *envío*:
One supplier sent through a SupplierIntake link: which link, which OptStaff signed it, and which
Company it wrote or added to. A resend for a company already there is a send of its own.
_Avoid in identifiers_: Submission (that is the form the send carries), Registration (that is a
person signing up for an Event), Upload

**CampaignUpload** — es. *carga de destinatarios*:
A spreadsheet of people dropped on a draft Campaign. It is read, shown back as a review of
what will happen to every row, and thrown away; only confirming it creates the missing CRM
contacts and adds them to the list. The batch that records who confirmed is kept; its rows are not.
_Avoid in identifiers_: Import, Carga, Excel, Csv (for the concept). The code also says
`RecipientUpload*` for the routes and the service; that name is fine there, not a second concept.

**Template** — es. *plantilla*:
One whole email — subject and body — in one language, keyed `(templateKey, language)`.
Seeded by a data migration and not editable through the API. Placeholders are derived
from the body, never entered.
_Avoid in identifiers_: Plantilla, EmailTemplate, Layout

**Arte** — es. *arte*:
The campaign artwork. Not an entity: it is an `<img src>` inside the Template body,
changed the way copy is changed. The term is listed only so nobody builds a table for it.

## Registration

**PublicEvent** — es. *cartelera*:
An Event as a stranger sees it on the public listing and its detail page: a narrowed copy of
what events-service holds, never the Event itself. Only public, active Events are listed,
and a closed one still has its detail page so a shared link keeps working.
_Avoid in identifiers_: Cartelera, Listing, Catalog, EventProxy

**Registration** — es. *registro*:
One person's confirmed intent to attend one **Event**, holding what they typed
themselves. Hangs off the Event, never the Campaign, so a person who received two
Campaigns cannot register twice. Its link to the CampaignRecipient whose token
brought them is nullable. Survives the Event's cancellation untouched as a row, and is
the audience of the CancellationNotice.
_Avoid in identifiers_: Registro, Inscription, Signup, Attendance, Booking

**RegistrationToken** — es. *enlace de registro*:
The credential in an invitation link that identifies the CampaignRecipient it was
minted for. Stored only as a SHA-256 hash and never written to a log. On a private
Event it admits one Registration; on a public one it admits many and each records
which invitee's link it came through.
_Avoid in identifiers_: Token (unqualified), InviteCode, Link

**RegistrationWindow** — es. *inscripciones*:
Whether an Event is still taking new Registrations. Open by default and shut by a
person when MITUR has enough people, with the Event still going ahead and nothing
sent to anybody. Reversible, unlike a cancellation. It is the one thing convocatorias
records about an Event rather than about a Campaign, and it is emphatically **not**
the Event's closure: `EventStatus.closed` and `cerrar` mean the Event already
happened, which is the opposite situation. No capacity number shuts it.
_Avoid in identifiers_: Closed or Close on its own, Cerrar, Cierre, Convocatoria, Capacity, Limit.
Qualified by Registrations it is the spec's own vocabulary and fine: `close-registrations`,
`registrations_closed_at`, `REGISTRATIONS_CLOSED` (`06` §5).

**Reference** — es. *código de registro*:
The human-readable code on a Registration (`MITUR-2026-00427`: the year, then a
sequence). Something to quote at a desk, carrying no authority. Deliberately not the
QrCode, which is a credential and unreadable aloud.
_Avoid in identifiers_: Code, RegistrationCode, Folio

**QrCode** — es. *código QR*:
The opaque code a Registration's QR image encodes, unrelated to its id. It is the
credential presented at the door: scanning it is how a CheckIn happens. Presenting it
again never makes a second one.
_Avoid in identifiers_: Qr, Ticket, BadgeCode

**CheckIn** — es. *check-in*:
The holder of a Registration was admitted at the door of its Event. At most one per
Registration at a time, however many times the QrCode is scanned; a repeat reports the
first. A CheckIn can be **undone** by anyone who can make one: it is then as if it never
happened, and the next scan makes a fresh one. An undo never reaches a raffle that has
already started.
Names the act at the door, not a kind of person, which is why it is allowed where the
nouns below are not. The raffle in events-service calls a checked-in person an
*attendee*; that word stays on its side of the boundary.
_Avoid in identifiers_: Attendance, Attendee, Asistente, Admission, Entry, Scan; Revert
and Cancel for the undo (*Cancel* belongs to Events)

**DoorList** — es. *lista de acceso*:
The Registrations of one Event as door staff see them: name, company, Reference and whether
each has a CheckIn, so a person with no QrCode to show can be found and checked in by hand. An
email can be searched for but is never shown. Not the Participant list, which is a
coordinator's read: it has no invitees who never registered, no send status, no referrers.
_Avoid in identifiers_: AttendeeList, GuestList, Participants, Lista

**DoorStaff** — es. *personal de la puerta*:
Whoever checks Registrations in at an Event's door, whether a coordinator or someone who can do
nothing else on the platform. Names a job at the door, not everyone working the Event.
_Avoid in identifiers_: Usher, Gatekeeper, Scanner (for the person), Attendant; *personal del
evento* in Spanish prose, which reads as everyone working the Event

## Tracking

**Participant** — es. *participante*:
One person's whole relationship with one Event, merged across every Campaign that
reached them and any Registration they made. The unit of the tracking read, and a
read model rather than a table. Joined on the trimmed, lowercased email address,
because CRM decrees an email is a person.
_Avoid in identifiers_: Invitado, Asistente, Attendee, Person, Guest

**Origin** — es. *procedencia*:
How a Participant arrived: `invited` (their own address is a recipient of one of the
Event's launched Campaigns, whatever became of the send), `referred` (not a recipient
themselves, but they registered through someone else's forwarded invitation, and that
referring recipient is recorded), or `walk_in` (neither: they registered from the public
list). Invited wins over referred. `invited` here is not the **Invited** count: a
suppressed or failed recipient has origin `invited` but is not counted as Invited.
_Avoid in identifiers_: Source, Channel, Via

**Invited** — es. *convocado*:
A person with at least one recipient row **handed off** to the sender across the
Event's non-draft Campaigns. Once per person however many Campaigns reached them.
A failed, suppressed or not-yet-sent recipient is not invited, and neither is anyone
on a draft. This is the number the client quotes back, so nothing else folds into it.
Not to be confused with **Origin** `invited`, which ignores the send.
_Avoid in identifiers_: Contacted, Reached, Sent, Notified

**NotRegistered** — es. *sin registrar*:
Invited people with no Registration. Never counts walk-ins, or it would go negative
on a well-attended public Event.
_Avoid in identifiers_: Pending, NoShow, Missing, Unregistered

**DeliveryStatus** — es. *estado de entrega*:
What became of a CampaignRecipient's email after it was handed off: `sent`, `delivered`,
`bounced`, `spam_report` or `unsubscribed`. notification-service owns it; this service only
reads it. Separate from the send status, which says whether we handed the email off at all.
Opened and clicked are not part of it, on purpose.
_Avoid in identifiers_: Status (unqualified), SendStatus, Tracking, Opened

**NeedsAttention** — es. *requiere atención*:
Work only a human can finish, because nothing in this service retries on its own:
a Registration whose CRM contact never landed, an unsubscribe CRM has not been told
about, typed details that disagree with the contact already on file, or a Campaign
stranded mid-handoff with recipients nobody mailed.
_Avoid in identifiers_: Errors, Failures, Queue, Retry

## Terms owned elsewhere

**Event** — es. *evento*:
Owned by events-service, which is its sole authority. This service reads one and never stores
one: no event table, no snapshot, no name copied onto a Campaign. Two fields of it decide things
here — its **Visibility**, which is what puts it on the public cartelera, and its **Language**,
which picks the Template a Campaign goes out in. `EventSummary` is the narrow read this service
keeps in memory for the length of a request, and what it leaves out — the budget, the total cost,
the invited count, the OPT contact — is what can never reach a stranger by accident.
_Avoid in identifiers_: Evento, EventRef, EventData, LocalEvent

**Organizer** — es. *organizador*:
The **name** of the OptOffice running an Event, as the public detail shows it. Only the name, and
never the id: an id is of no use to a stranger and naming the office is the whole of what the page
says. It is resolved by events-service against the CRM, so it can be null on a `200` — the
directory was down, not the Event unorganized — and the page omits the block rather than drawing
a hole.
_Avoid in identifiers_: Opt, OptOffice, OptRef, Organiser, Host
