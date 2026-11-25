# Events

Planning and execution of MITUR promotional events (roadshows, ferias, tradeshows,
webinars), including sourcing the companies that service them via per-category
quotes with an approval workflow.

> Naming: domain names are English platform-wide (ADR-0008). Prose is Spanish —
> the end-user UI, code comments, log messages, error text, and the API
> documentation `/scalar` renders. Stored codes are frozen as-is.
>
> Each entry below records both: the **English identifier** used everywhere the
> type system looks, and the **canonical Spanish rendering** used everywhere
> prose is written. `_Avoid_` lists apply to **identifiers only** — the Spanish
> word is not a mistake in a sentence, it is the required word.

## Language

**Event** — es. *evento*:
A MITUR promotional activity (roadshow, feria, tradeshow, webinar) with a
lifecycle from planning through close or cancellation. Closed and cancelled are its terminal
states: a terminal Event accepts no further change to itself or its cities.
_Avoid in identifiers_: Evento

**EventVisibility** — es. *visibilidad del evento*:
Whether an Event is advertised to strangers (`public`) or kept inside MITUR (`private`). It sits
on the Event and not on a Campaign, because it governs more than convocatorias and a per-campaign
flag would let one Event carry two campaigns that contradict each other. Required, with no
database default, and optional only in a create request, where it falls to `private`. A patch that
omits it keeps the stored value — the one field on that endpoint that omitting does not clear,
because until the event form grows a control every unrelated edit would unpublish an advertised
Event. `events:write` may change it at any time, including after a campaign has sent.
_Avoid in identifiers_: IsPublic, Visibilidad, Published, EventPrivacy

**Language** — es. *idioma*:
The language an Event runs in, one of six (`es`, `en`, `fr`, `de`, `pt`, `it`). Required: a
convocatoria picks its email template from it, so a null would block a launch rather than mean
anything. It is what the Event runs in, not what MITUR can email — a `de` Event is honest about
itself and fails at the launch gate naming the template that is missing. A create without one is
`es`; a patch with a null is `400 EVENT_LANGUAGE_REQUIRED`.
_Avoid in identifiers_: Idioma, Locale, Lang, Culture

**SeatingLayout** — es. *montaje del salón*:
How the room of an Event is arranged, one of six: `Banquet` (round tables), `Auditorium` (rows
facing a stage), `Conference` (rows plus a head table), `Cocktail` (standing, no seats), `UShape`
and `Classroom`. It decides whether the Event has seats and how they are shared out. Optional on
every Event: none means nobody has said yet, which is not `Cocktail`. It is not a
**SetupDate**, which the Spanish *montaje* on its own also names: always say *montaje del salón*.
`Auditorium` and `Cocktail` are the same arrangements a Venue's AuditoriumCapacity and
CocktailCapacity are measured in.
_Avoid in identifiers_: Montaje, RoomSetup, Arrangement, Layout (on its own), Theater

**EstimatedBudget / TotalCost** — es. *presupuesto estimado* / *costo total*:
What an Event was budgeted for, against what it ended up costing. The OPT types the
EstimatedBudget while planning; the TotalCost is computed at close from the final amount of
each approved Quote. Both are optional, no rule ties them together, and the service never
compares them — seeing them differ is the whole point of keeping both.
_Avoid in identifiers_: Presupuesto, Budget, CostoTotal, EstimatedCost

**OptOffice** — es. *OPT* / *oficina de promoción turística*:
One of MITUR's tourism promotion offices abroad. Every Event is organized by
exactly one. The term belongs to the CRM, which is its sole authority — Events
names an OptOffice but never defines, edits or ranks one.
_Avoid in identifiers_: Oficina, OficinaPromocionTuristica, OptRef

**OptContact** — es. *contacto de la OPT*:
The person at the organizing OptOffice designated as responsible for a given
Event. Usually the office's director, but not necessarily — it is a choice the
Event records, not a fact derived from the office. An Event may have none.
_Avoid in identifiers_: OptDirector, Director, Responsable, Encargado

**CrmDirectoryStatus** — es. *estado del directorio del CRM*:
How trustworthy the CRM-sourced names on a response are: `Ok` a current copy, `Stale` the last
good copy served because the CRM did not answer, `Unavailable` nothing ever cached, so the names
are null. One status per request, keeping the worst reported, covering **everything** Events
resolves against the CRM rather than one directory each — the same CRM answers them all, so their
failures are one failure. It reaches the client as `X-Crm-Directory-Status`, and as
`X-Opt-Directory-Status` with the same value, which is the former name kept until the frontend
retires it.
_Avoid in identifiers_: OptDirectoryStatus, DirectoryHealth, EstadoDirectorio, CrmStatus

**CompanySnapshot** — es. *copia de la empresa*:
The minimum identity of a Company as the cached company directory serves it: id, name, country,
city and a single "may this still be chosen" bit. It is what a read renders, never what a write
validates against, and never something Events stores in its own tables — it lives in the cache and
dies with the process. A Company the bulk directory omits is filled in one at a time and remembered,
which is the long tail of closed companies and not a symptom of anything.
_Avoid in identifiers_: CompanyCache, CompanyProjection, EmpresaSnapshot, CompanyDto

**ServiceSupplier** — es. *proveedor*:
One Company in a main ServiceSupplierCategory and any number of secondary ones: the Engagement a
company has when Events can propose and quote it. The client's own term. The company's commercial
identity — its name, country, city and address — belongs to the CRM and Events never answers for
it; what a ServiceSupplier owns is the hiring relationship. A hotel that is the venue and the
caterer at once is **one ServiceSupplier** with Venue as its main category and Alimentacion as a
secondary one (ADR-0034): it is offered in both pools, and each quote is still per category. A
company holds a category on one live ServiceSupplier only, main or secondary.
_Avoid in identifiers_: Proveedor, Supplier, Vendor, Provider

**The ownership split** (ADR-0015). A ServiceSupplier stores `CompanyId` and never a name, and the
line between the two services runs here:

| | Owner |
| --- | --- |
| Category, venue capacities, comments, photos, quote history, `IsHireable` | **Events** |
| Name, commercial name, country, city, address, website, description, contacts, documents | **CRM** |
| A maps link | Nobody — the client builds one from the address it resolved |

Two rules fall out of it. **Reads never fail over a name**: an unresolvable supplier comes back with
a null name and the directory status says why, rather than vanishing. **Writes validate live**: the
company id is checked against the CRM on every create and edit, never against the cached copy, so a
company registered two minutes ago is usable — `COMPANY_NOT_AVAILABLE` (409) when the id does not
resolve, `CRM_UNAVAILABLE` (503) when the CRM does not answer, and the two are never conflated. A
dormant or closed company still passes, because the same check guards editing and refusing one would
freeze the ficha of a supplier MITUR still hires. Events never writes to the CRM: the company is
created or picked against crm-service with the user's own token, so every company keeps a true record
of who registered it.

**Engagement** — es. *vinculación*:
What a Company does for MITUR, as opposed to what kind of business it is (its CompanyCategory, in the
CRM). Being a ServiceSupplier is one engagement and being a Coexhibitor is another; one Company may
have several at once, and a hotel is often both. A grouping word, not a record: each engagement is
its own thing with its own facts, and all they share is the Company they point at. Engagements are
Events' to know. The CRM never learns which of its companies are engaged, or how, so a CRM screen
that lists suppliers or co-exhibitors is reading Events, and goes dark while Events is down.
_Avoid in identifiers_: Role (a permission, in this platform), Capacity, Participation, Relationship

**Coexhibitor** — es. *coexpositor*:
A Company sharing MITUR's stand at one feria, an Event of that type. One Company at one Event: the
same company at next year's edition is a new Coexhibitor. The company's identity is the CRM's; what
belongs to the Coexhibitor is what happens at that fair — its stand, its main contact, how many
accreditations it gets, and how far its participation has gone.
_Avoid in identifiers_: Coexpositor, Exhibitor, CoExhibitor, Sponsor

**IsHireable** — es. *contratable*:
Whether a ServiceSupplier may still be chosen for new work. An unhireable one keeps its ficha
and every past quote it appears in; it simply stops being offerable as a new Candidate. It is
not deletion, and it is **not** the CRM's view of the same company: a dormant partner MITUR no
longer invites can be a venue it still hires, and a company MITUR happily markets to can be one
it will never hire again. The Spanish word is *contratable* and never *activo*, in prose as in
identifiers — *activo* is the CRM's word for the other flag, and borrowing it here would put the
collision back into the one place a user reads.
_Avoid in identifiers_: IsActive, Activo, Enabled, Disabled, Inactive

**UnhireableReason** — es. *motivo de no contratar*:
Why MITUR stopped hiring a ServiceSupplier, in its own words. Optional: a supplier can be
unhireable with no reason given. It explains the current mark and nothing older, so it goes away
the moment the supplier is hireable again; a reason never outlives the decision it explained, and
marking the supplier unhireable a second time starts from a blank reason.
_Avoid in identifiers_: InactiveReason, DisableReason, NoUseReason, Motivo

**ServiceSupplierEvaluation** — es. *evaluación del proveedor*:
How MITUR staff rate a ServiceSupplier's work at one Event: four fixed scores from 1 to 5
(service quality, punctuality, value for money, communication), whether they would hire it again,
and a comment. Only a supplier that won a quote at a closed Event can be rated for it, once per
Event: rating the same Event again corrects that rating and touches no other. Ratings accumulate
across Events into the supplier's average, which is what makes a well-rated supplier easier to pick
next time. "Would not hire again" is one person's view of one Event and never changes IsHireable;
that stays a deliberate decision.
_Avoid in identifiers_: Rating, Review, Survey, Evaluacion

**EvaluationReset** — es. *reinicio de evaluaciones*:
A director's decision that a ServiceSupplier's past ratings no longer describe it, as when the
company changes management. Every earlier ServiceSupplierEvaluation stays on record, marked as
before the reset, and stops counting: the average starts again from the next rated Event. It needs
a written reason, and it is rare on purpose, since it changes how the supplier looks in every
future pick.
_Avoid in identifiers_: Wipe, Clear, Purge, Delete

**ServiceCatalogueItem** — es. *servicio del proveedor*:
One service a ServiceSupplier offers, such as a room hired by the day or a layout priced per guest:
a name, a unit in the supplier's own words ("Día", "Pax") and a reference price in USD or EUR. The
supplier's **service catalogue** (es. *catálogo de servicios*) is the list of them, edited whole
with the ficha. The price is a reference and never fixes what a Quote costs. Each service keeps one
id for life, so an offer can point at it; it carries no category.
_Avoid in identifiers_: Product, Item, Offering, ServiceSupplierService, Servicio

**ServiceSupplierCategory** — es. *categoría de proveedor*:
The classification of what a ServiceSupplier offers (Venue, Ambientacion,
Audiovisuales…). Stored values keep the client's words. It is a fact about the **Engagement**, not
about the company, which is why it is Events' to define and has nothing to do with the CRM's
own company categories. A ServiceSupplier has one **main** category, fixed at creation, and may
have **secondary** ones (es. *categoría principal* / *secundaria*), edited freely and never
repeating the main one.
_Avoid in identifiers_: CategoriaProveedor, PrimaryCategory, AdditionalCategories

**AuditoriumCapacity / CocktailCapacity** — es. *aforo en auditorio* / *aforo en cóctel*:
How many people a Venue ServiceSupplier holds in each of its two layouts — seated
facing a stage, and standing. Both are maximums declared by the OPT, optional and
independent of each other; only a supplier with Venue as its main or a secondary category may
carry them. `Auditorium` translates the
client's *auditorio* rather than importing the industry term *theater style*.
_Avoid in identifiers_: Aforo, Capacidad, TheaterCapacity, MaxCapacity, Seating

**Quote** — es. *cotización*:
The single active quoting process for one (Event, category) pair, gathering one
to three Candidates and moving draft → submitted → approved through decision
attempts. Note the collision with everyday speech: when the client says *tres
cotizaciones* they are counting Candidates, or CandidateProposals, never Quotes.
A Quote is the process, and there is exactly one per category.
_Avoid in identifiers_: Cotización, CategoriaCotizacion, CategoryQuote, Quotation

**Candidate** — es. *candidato*:
One ServiceSupplier under consideration inside a Quote. It carries the supplier's
identity and nothing else: a price belongs to a proposal, and so do the recommendation and the
documents backing it. A supplier appears at most once per Quote, so
offering the same venue twice is two proposals on one Candidate, never two
Candidates; proposing a supplier that is already a Candidate is refused with
`CANDIDATE_SUPPLIER_ALREADY_QUOTED`, which carries the Candidate that holds it. Otherwise one
supplier could occupy all three slots and a Quote that compared nobody would read as fully
compared. It never has zero: without a price there is nothing to compare or approve, which is
why it is created with its first proposal and why the last one cannot be removed.
_Avoid in identifiers_: Candidato

**CandidateProposal** — es. *propuesta*:
One priced offer from one Candidate: its amount, its description, and its setup
and teardown dates. A Candidate carries one or more, because a single supplier often offers
genuinely different things — a venue quoting each of its salones — and each of those is a
separate price with its own written cotización behind it. Competing offers from *different*
suppliers are separate Candidates, not proposals. **The recommendation is one of them**: the OPT
recommends an offer, not a supplier, because a venue quoting three salones is quoting three
prices and recommending the venue would not say which one Dirección is being asked to approve.
At most one proposal per Quote carries it, counting every Candidate's. **Approval names one too**,
for the same reason: Dirección approves the salón that gets contracted, not the venue, so the
winner is a proposal; the Candidate that won is read off it and never travels on its own. The
FinalAmount then lands on that same proposal and on no other. **The written cotización backing
it is its own too**, a ProposalAttachment: three salones are three prices with three documents
behind them. There is no cap on how many a
Candidate holds — the max of three exists so that suppliers get compared, and several offers from
one supplier are not competing with each other.
_Avoid in identifiers_: Propuesta, Option, CandidateOption, Opcion, Variant, QuoteLine

**PickedService** — es. *servicio elegido*:
One ServiceCatalogueItem the OPT picked for a CandidateProposal, with a quantity, a unit price and,
if any, a discount and a tax, all in the proposal's currency. It is a copy: its name and price stay
as they were when picked, and the catalogue service it came from is only a link, which may outlive
the service. The picks are detail of the offer: the estimated amount is still typed by hand, and
approval never reads them.
_Avoid in identifiers_: SelectedService, QuoteLine, LineItem, ServicioSeleccionado

**SetupDate / TeardownDate** — es. *montaje* / *desmontaje*:
When the supplier behind a CandidateProposal comes in to set up, and when it takes everything
down. They belong to the proposal because two proposals from one supplier can want different
days. Both carry a time, both are optional on their own, and the only rule is that the teardown
is not earlier than the setup. Neither is checked against the Event's own dates: setting up
happens before the Event starts and taking down after it ends, which is why they are recorded
apart from it.
_Avoid in identifiers_: Montaje, Desmontaje, AssemblyDate, InstallDate, DismantleDate

**FinalAmount** — es. *monto final*:
What was actually contracted with the winning CandidateProposal of an approved Quote, as opposed
to the EstimatedAmount it was approved for. It exists only after approval and only on the winner,
and it is what the Event's TotalCost adds up. An amount at or below the approved EstimatedAmount,
in the same currency, takes effect the moment it is recorded; anything else first waits as a
PendingFinalAmount. The limit stays the EstimatedAmount even after a higher amount was approved.
Recording again the amount already in effect waits for no one; it is then backed by the FinalQuote
the new record names, or by none.
_Avoid in identifiers_: MontoFinal, MontoContratado, ContractAmount, ContractedAmount

**PendingFinalAmount** — es. *monto final pendiente de aprobación*:
A FinalAmount recorded above the winning offer's EstimatedAmount, or in another currency, waiting
for its own decision before it takes effect. Until then it adds nothing to the TotalCost: the
amount in effect, if any, stays as it was, and an Event with one cannot close. It does fix the
Event's currency, like an amount in effect. Someone with the approve permission
other than whoever recorded it approves or rejects it; recording again replaces it, and recording
an amount within the limit discards it. A rejected one leaves nothing behind, its FinalQuote
included.
_Avoid in identifiers_: ContratoPendiente, PendingContract, UnapprovedFinalAmount

**FinalQuote** — es. *cotización final*:
The ProposalAttachment of kind Quote that backs one FinalAmount or PendingFinalAmount: the
supplier's written cotización for the contracted price, as opposed to the one behind the
EstimatedAmount. It belongs to the amount it backs. When a PendingFinalAmount is rejected,
replaced or discarded without ever taking effect, its FinalQuote goes with it; the FinalQuote of
the amount in effect never goes with an amount change, though it can still be removed by hand. An
amount may have none. A FinalAmount recorded at the EstimatedAmount is backed by the quote behind
that EstimatedAmount: the price did not change, so the supplier's quote for it is the same paper.
_Avoid in identifiers_: ContractDocument, CotizacionFinal, FinalAmountAttachment

**QuoteDecision** — es. *decisión de la cotización*:
One decision attempt on a submitted Quote; records the outcome (approved,
rejected, withdrawn, discarded) and survives across rounds. An individual round
is an *intento* in Spanish prose. Only an approved attempt names a winner, and what it names is a
CandidateProposal.
_Avoid in identifiers_: Intento, CotizacionDecision

**QuoteApprover** — es. *aprobador*:
A person who may decide a submitted Quote for one OPT: whoever holds the approve permission and
belongs to that OPT. Not the OPT's director as the CRM lists it; those are different people, and
an OPT with no approver at all is normal. Submit and withdraw alerts go to them.
_Avoid in identifiers_: Aprobador, Director, QuoteDirector

**QuoteSubmitter** — es. *quien somete*:
The person who submitted one QuoteDecision attempt, which is not the person who last touched the
Quote. Approve and reject alerts go to them. Two attempts of one Quote can have different
submitters.
_Avoid in identifiers_: Solicitante, QuoteOwner

**RoadshowCity** — es. *ciudad del roadshow*:
The city an Event takes place in. **An Event has exactly one** (client, 2026-10-06): a roadshow
through three cities is three Events, not one Event with an itinerary. The name is left over from
when it was modelled as an itinerary stop. It is the place a Raffle's NotarialRecord states.
A city also keeps its own local time, which is the time a NotarialRecord prints, since a notary
compares it with their watch. A country does not settle that on its own, and neither does the
organizing OptOffice, which may stand for a whole region.
While its Event is open, whatever the Event's type, the city can be corrected (its name, country,
date or local time), even after a Raffle completed: a NotarialRecord reissued afterwards states the
corrected place and times, and earlier versions keep what they printed.
_Avoid in identifiers_: RoadshowCiudad

**SignatureRequest** — es. *solicitud de firma*:
One set of documents sent out through FirmaGOB to be signed or approved by an ordered list of
Signers. It exists from the moment it is queued, before FirmaGOB hears of it, and ends signed,
rejected, expired or never sent. A finished one is never reopened: asking again is a new
SignatureRequest.
_Avoid in identifiers_: SolicitudFirma, FirmaRequest, Envelope

**Signer** — es. *firmante*:
A person who must act on a SignatureRequest, either to sign it or to approve it, in a given
place in the order. Not a QuoteApprover; signing a document and deciding a Quote are unrelated.
Not a CertificateSigner either, whose name is only printed under a line.
_Avoid in identifiers_: Firmante, Approver, UserEntity

**StoredFile** — es. *archivo*:
A file the platform holds on someone's behalf, identified independently of whatever it is for.
It carries only what is true of the file itself: its kind, its size, when it arrived and who
brought it. It is written once and never altered — replacing a file produces a new StoredFile
rather than changing an existing one, so what was submitted earlier stays answerable.
_Avoid in identifiers_: Archivo, File, Blob, Upload, FileRef, Attachment

**ServiceSupplierPhoto** — es. *foto del proveedor*:
A photograph of what a ServiceSupplier offers, most often a Venue's rooms. It describes the
supplier and is reused by every Event that considers it; it never belongs to one Event.
_Avoid in identifiers_: Foto, ProveedorFoto, Photo, Image, VenuePhoto

**ProposalAttachment** — es. *adjunto de la propuesta*:
A document evidencing what one CandidateProposal offered, usually the supplier's written
cotización together with whatever arrived alongside it. It hangs off the proposal rather than the
Candidate or the Quote, because it justifies one price and no other: a venue's two salones are two
proposals with two written cotizaciones, and filing both on the candidate would not say which
backs which. Its quota is the proposal's too, so a supplier's other offers do not consume it.
Removing a proposal, or the candidate above it, takes its attachments with it and leaves the
files answering by id. Its ProposalAttachmentKind says which of the two things it is.
_Avoid in identifiers_: Adjunto, CandidateAttachment, CotizacionFoto, QuotePhoto, QuoteFile, Documento

**ProposalAttachmentKind** — es. *tipo de adjunto*:
What a ProposalAttachment is: the written quotation (`Quote`) or a photograph of what is being
proposed — the decoration, the room, the setup (`ProposalImage`). The file's content type cannot
answer this, because a scanned quotation is an image and a rendered proposal is a PDF. Everything
attached before the distinction existed is a Quote.
_Avoid in identifiers_: TipoAdjunto, AttachmentType, DocumentKind

**EventImage** — es. *imagen del evento*:
A photograph that belongs to one Event and describes it rather than any supplier: the cover that
heads it, or a photo of its promotional gallery. Its EventImageKind says which. Unlike a
ServiceSupplierPhoto, it is never reused by another Event.
_Avoid in identifiers_: Imagen, EventoFoto, EventPhoto, CoverPhoto, Banner

**EventImageKind** — es. *tipo de imagen del evento*:
What role an EventImage plays: the single image that heads the Event (`Cover`) or one more photo
of its promotional gallery (`Gallery`). The file's content type cannot answer this, because both
are a photograph. An Event has at most one cover, and uploading a second replaces the first.
_Avoid in identifiers_: TipoImagen, ImageType, EventPhotoType

**Raffle** — es. *sorteo*:
The prize draw held at an Event, in front of a notary and two witnesses. An Event has at most one.
It has no name and no date of its own: both are the Event's. Beside its prizes it holds how the
draw is shown on screen and the two rules that limit who may win twice, one prize per person and
one prize per company. The rules can change while it runs, and a change applies from the next draw.
_Avoid in identifiers_: Sorteo, Lottery, Draw (for the whole raffle), Giveaway

**RaffleState** — es. *estado del sorteo*:
Where a Raffle is in its life: `draft` while it is being set up, `in_progress` during the
evening, `completed` once the record is signed off. It moves only when an operator says so, and
nothing about its prizes or draws moves it. It moves forward, with one way back: while nothing has
been drawn, an operator can take an in-progress Raffle back to draft, and its Drum is discarded. Whether it is ready to start is
worked out when asked, not stored as a state.
_Avoid in identifiers_: EstadoSorteo, Status (for the raffle), Configured

**RaffleRound** — es. *ronda*:
A named grouping of Prizes within a Raffle, drawn in the order of its number. The number is the
order the evening runs in, always one to however many rounds there are with no gaps, and it is
the service's to assign, not the operator's. A round holding prizes cannot be removed; it is
emptied first.
_Avoid in identifiers_: Ronda, Round (bare, which also names a quote's submission round), Stage

**Prize** — es. *premio*:
What one winner takes home, handed over whole. "3 botellas de ron" is one Prize; five tote bags are
five Prizes with five winners and identical names. How many physical things a prize contains is
prose in its name, and nothing reads it. Its value is text printed as the operator wrote it, not an
amount.
_Avoid in identifiers_: Premio, Item, Reward, Quantity (on a prize)

**Sponsor** — es. *patrocinador*:
The Company that donates a Prize, optionally with one of its Contacts as the person behind it. A
fact about the Prize, not a record of its own, and not a Coexhibitor: the company's identity is the
CRM's, and the same company sponsoring five prizes is five sponsored Prizes. A sponsored Prize may
carry the sponsor's logo, which belongs to that Prize and leaves with the sponsor. A Prize is not
required to have one; a surprise prize whose sponsor is not in the CRM yet goes in unsponsored.
_Avoid in identifiers_: Patrocinador, Donor

**PrizeDraw** — es. *sorteo del premio*:
One attempt to give a Prize away: a winner picked from the Drum, at a stated moment, from a pool of a
stated size. A Prize can have several, because a draw can be voided and the Prize drawn again. At
most one is live at a time. A voided one is never removed, since the NotarialRecord lists every draw
in the order it happened. The winner, their RedemptionCode, their winner email and the two rules it
was drawn under belong to the draw, not to the Prize.
_Avoid in identifiers_: Award, PrizeAward, Win, DrawResult

**Void** — es. *anulación*:
An operator's statement that the winner of a live PrizeDraw will not take the Prize, with one of four
reasons: absent, declined, ineligible, or another reason that must be written out. The PrizeDraw is
never removed; it stays in the Prize's history with its reason, and the Prize can be drawn again.
The voided winner is out of every later draw in the Raffle, whatever the reason; the reason is
recorded, never weighed. A void never draws a new winner, and a Prize that has been handed over
cannot be voided.
_Avoid in identifiers_: Desierto, Cancel, Revoke, Undo, Redraw (a redraw is just another PrizeDraw)

**Attendee** — es. *asistente*:
A person checked in at the door of an Event, qualified for its Raffle or not. Events reads them
from convocatorias, which owns check-in, and once the Raffle starts it keeps its own frozen copy of
everyone who was at the door, so the record can account for each person who did not qualify as
well as each who did.
_Avoid in identifiers_: CheckIn (convocatorias' term), Asistente, Guest, Registrant

**Participant** — es. *participante*:
An Attendee who qualified for the Raffle, and nothing else. Every Participant is an Attendee; an
Attendee who did not qualify carries the reason they were left out.
_Avoid in identifiers_: Participante, Entrant, ParticipantSnapshot (for the frozen door list)

**Drum** — es. *bombo*:
The Participants of a Raffle, taken together: what each draw picks a winner from. It is fixed when
the Raffle starts, from the same frozen copy as the Attendees. It is not a RaffleVisualStyle, which is
only how the draw looks on screen.
_Avoid in identifiers_: Bombo, Tombola, Pool (that is the EligiblePool), ParticipantSnapshot

**EligiblePool** — es. *participantes elegibles*:
The Participants who may win a given Prize right now: the Drum, minus anyone with a voided
PrizeDraw anywhere in the Raffle, and, while the Raffle's rules say so, anyone already holding a
live PrizeDraw, or from the same company as someone who does. It is asked for per Prize, because it
is what that Prize's draw picks from; the rules today give every undrawn Prize the same pool. It is
recomputed for every draw and every read; a PrizeDraw records only how large it was.
_Avoid in identifiers_: Drum (for one prize), Candidates, Eligibles

**ExclusionReason** — es. *motivo de exclusión*:
Why an Attendee is not a Participant. A non-qualified Attendee has exactly one: the first that
applies, in a fixed order. So the reasons never overlap, and the people at the door always add up to
the Participants plus those left out.
_Avoid in identifiers_: MotivoExclusion, RejectionReason, Filter

**AttendeeOverride** — es. *decisión manual*:
An operator's decision to include or exclude one Attendee from the Drum, kept by the person's identity key
(their email, or their ID when they have no email) while the Raffle is being set up. It outranks the awards rule and the `.do` rule, never the four
that sit above any human: a walk-in, an Attendee with no CRM contact, one CRM has suspended, and one
CRM has blacklisted.
An include those rules beat is not removed; it loses, and the record says it was overruled.
_Avoid in identifiers_: ManualParticipant, Whitelist, Exception

**BulkExclusion** — es. *exclusión en lote*:
Many AttendeeOverrides of `excluded` written at once, from the live winners of another Raffle of
the account or from a list of emails and IDs. Only while the Raffle is in draft. Who cannot be found
is reported, never failed; each person can be put back one by one.
_Avoid in identifiers_: MassExclude, Blacklist

**AwardsMember** — es. *miembro de DO Travel Awards*:
A person enrolled in DO Travel Awards, the client's programme outside the platform, known to us only
by email address. Membership is a fact about the person, not about any Event or Raffle.
_Avoid in identifiers_: Award, Awards (bare: a prize won is a PrizeDraw), Miembro, Member (bare)

**AwardsList** — es. *lista de DO Travel Awards*:
The AwardsMembers in force: one list for the whole platform, replaced whole rather than added to.
Having no list is not the same as having an empty one. With no list, the awards rule is skipped and
the skip is reported. An empty list qualifies nobody.
_Avoid in identifiers_: Awards (bare), Roster, Whitelist, AwardsBatch (for the members themselves)

**Authorities** — es. *autoridades* (notario y testigos):
The notary and the two witnesses who attest a Raffle, each a name and an identity document. Always
exactly three people, because the client's process names three. They may be left blank while the
raffle is set up and run, and are required only to complete it, since the NotarialRecord is what
names them. A blank is never filled with a placeholder name.
_Avoid in identifiers_: Autoridades, Notario, Testigo, Witnesses (for all three), Officials

**CertificateSigner** — es. *firmante del certificado*:
A person whose name and title are printed under a signature line on a Raffle's prize certificates,
such as the Minister or the director of the OPT. A Raffle keeps at most two, in order, as the
starting point for every certificate it prints. What prints on a given certificate can differ;
only these defaults are kept. They are words on a keepsake, not a reference to anyone in the CRM,
and changing them never touches the NotarialRecord. Not a Signer: nothing is signed electronically.
_Avoid in identifiers_: Firmante, Signer (bare, as a type), Signatory, Signature

**NotarialRecord** — es. *acta notarial*:
The document a notary and two witnesses sign to attest a raffle's draw. It covers only what
happened in the room: who was in the drum, every draw including the voided ones, and the prizes
left undrawn. What it attests is fixed when the raffle completes. Only the particulars of the
notary and witnesses, and the local time of the place it was held, can be corrected afterwards;
the moments themselves never change, only the clock they are written in. A correction is a reissue: a new numbered
version beside the old ones, which are never removed. The current version is the newest one; a
reissue that fails leaves it unchanged. It says nothing about whether a winner
later collected their prize. Each handover is evidenced by its own record.
What the platform stores is the text the notary is given to sign. The ink-signed original is kept
on paper, outside the platform.
_Avoid in identifiers_: Acta, ActaNotarial, Certificate, Report, Minutes

**RedemptionCode** — es. *código de canje*:
The short reference a winner quotes or shows to collect a prize. It finds the draw, and does
nothing more. It is **not** a proof of entitlement: whoever holds it still has to identify
themselves at the handover, and the handover is the evidence. So a forwarded code alone collects
nothing. It belongs to one draw attempt, so a voided draw's code is dead, and a redraw issues a
new one. It never appears on the NotarialRecord.
_Avoid in identifiers_: CodigoCanje, Voucher, Ticket, Token, ClaimCode

**Handover** — es. *entrega*:
The moment a won prize physically leaves MITUR's hands, and the evidence that it did. It records
who received it, the identity document they showed, whether they are the winner or collecting on
the winner's behalf, their signature, and which operator handed it over. Collecting on someone's
behalf is allowed and stated, never inferred. A prize with a live handover is *redeemed*. A
handover recorded in error is reversed, not erased: it stays on record with who reversed it and
why, and the prize is back to awaiting collection.
_Avoid in identifiers_: Entrega, Canje, Delivery, Redemption (for the act), Pickup

**PickupInstructions** — es. *instrucciones de retiro*:
Where and until when a Raffle's winners collect their Prizes, in the operator's own words and
language. One for the whole Raffle, never one per Prize. Required to complete the Raffle, because
the WinnerEmail carries them and a winner with nowhere to go is the complaint the client hears.
_Avoid in identifiers_: InstruccionesRetiro, Delivery, Redemption, per-prize instructions

**WinnerEmail** — es. *correo al ganador*:
The one email the winner of each live PrizeDraw receives when the Raffle completes: never at the
draw, since a void may still follow, and never again, a reissue included. It goes to the address
the person was entered under, carries their RedemptionCode and the PickupInstructions, and is in
the Event's language when there is copy for it, otherwise in Spanish. Whether it was sent is
recorded on the draw; failing to send it never stops the Raffle completing.
_Avoid in identifiers_: AwardNotification, NotificarGanador, WinnerNotice, Award
## Terms owned elsewhere

**Company** (CRM) — es. *empresa*: the legal entity behind a ServiceSupplier, and behind an
OptOffice's partners. Events names a Company but never defines, edits or deduplicates one; its
name, country, city and address are the CRM's to answer for. A Company is not a supplier until
Events gives it a ServiceSupplier role in some category. See `crm-service/GLOSSARY.md`.

**CompanyContact** (CRM) — es. *contacto de la empresa*: a person's dated affiliation to a
Company, carrying their job title. It is what a supplier's contacts are. Events reads them and
never records its own copy of a person. See `crm-service/GLOSSARY.md`.

## Actions

The Spanish verbs are the client's own, recovered from the route segments that
preceded the English migration (ADR-0008). Prose uses them; routes do not.

| English (route, identifier) | Spanish (prose) |
| --------------------------- | --------------- |
| start / close / cancel      | iniciar / cerrar / cancelar |
| submit                      | someter |
| approve / reject            | aprobar / rechazar |
| withdraw / discard          | retirar / descartar |
| recommended                 | recomendado |
| final amount                | monto final |
| pool                        | pool (sin traducir) |
