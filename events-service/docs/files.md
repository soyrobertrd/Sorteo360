# events-service: file storage

> Moved from `events-service/CLAUDE.md` (PGTI-213), text unchanged except headings. A section named
> with "see ... below" may now live in a sibling file in this folder: `grep -n '^## ' events-service/docs/*.md` finds it.

## File storage — the seam is the chassis's, the tables are this service's

> The link tables are in `docs/ARCHITECTURE.md` §4 and their routes in §5; this file keeps the
> rules and the reasons behind them.

`docs/adr/0014` is the decision; what follows is where it lives in the code.

- `Core/Files/` — `StoredFile` (what storage knows), `StoredFileContent`, `IFileService`,
  `FileService`. The only thing from outside that appears here is the chassis `IFileStore`; no
  provider concept reaches this layer, and a storage key is an opaque string nothing interprets.
- `Core/Files/` also holds the **upload rules**: `IUploadPolicy` / `UploadPolicy` and
  `UploadLimits`. The content inspection itself (size, magic bytes → `FileSignature`, HEIC,
  contradiction) is the chassis `FileContentInspector`; `UploadPolicy` maps its `FileRejection`
  to the codes below and adds the per-owner quotas and the images-only narrowing.
- `Core/Errors/FileErrors` — `FILE_MISSING` (404) and `FILE_STORE_UNAVAILABLE` (503).
- `Core/Errors/FileUploadErrors` — `FILE_TOO_LARGE`, `FILE_TYPE_NOT_ALLOWED`, `FILE_TYPE_MISMATCH`
  and `FILE_TYPE_HEIC_NOT_SUPPORTED` (400), plus `SUPPLIER_PHOTO_LIMIT_REACHED`,
  `PROPOSAL_ATTACHMENT_LIMIT_REACHED` and `EVENT_GALLERY_IMAGE_LIMIT_REACHED` (409).
- `Core/Errors/ProposalAttachmentErrors` — `CANDIDATE_PROPOSAL_NOT_FOUND` and
  `PROPOSAL_ATTACHMENT_NOT_FOUND` (404), plus `PROPOSAL_ATTACHMENT_KIND_NOT_SUPPORTED` (400).
- `Core/Errors/EventImageErrors` — `EVENT_NOT_FOUND` and `EVENT_IMAGE_NOT_FOUND` (404), plus
  `EVENT_IMAGE_KIND_NOT_SUPPORTED` (400).
- `Infrastructure/Database/` — `stored_file` and its repository, plus `StoredFileReferences`,
  `proposal_attachment` and its repository, and `ProposalAttachmentLoader`, the one query that
  reads an attachment with its file (shared by that repository and `QuoteRepository`); the same
  pair for `event_image`, whose `EventImageLoader` is shared by `EventImageRepository` and
  `EventRepository`.
- `Web/FileEndpoints/` — `GET /v1/files/{id}`, `/content` and `GET /v1/files/missing`, flat by
  file id so any consumer holding one can read it. Everything else is **owner-scoped** and lives with the owner:
  `Web/ServiceSupplierEndpoints/` carries `POST`, `GET` and `DELETE` on
  `/v1/service-suppliers/{id}/photos`, and `Web/QuoteEndpoints/ProposalAttachmentEndpoints`
  carries the same three on `/v1/proposals/{proposalId}/attachments`, and
  `Web/EventEndpoints/EventImageEndpoints` the same three on `/v1/events/{eventId}/images`. Each
  is governed by its owner's policy — `service-suppliers:write`, `quotes:write` and
  `events:write`. A prize's sponsor logo is the one link that is a column rather than a table:
  `Web/RaffleEndpoints/PrizeEndpoints` carries `POST` and `DELETE` on
  `/v1/events/{eventId}/raffle/prizes/{prizeId}/logo` under `raffles:write`, and the logo is read
  with the raffle.

Ten rules worth knowing before changing any of it:

- **A file is referenced one way, and files never travel in an owner's JSON body.** A photo link
  and an attachment link both carry a required `stored_file_id` and nothing else; there is no
  external URL, and no placeholder reference survives anywhere. The consequence is that a
  ServiceSupplier cannot be created with its photos in one payload — bytes cannot be uploaded to a
  row that does not exist yet — so saving is two steps: create the supplier, then upload each
  file. Editing a supplier no longer touches its gallery, which the old replace-the-array rule on
  that payload did. Proposals work the same way.
- **Files are immutable; links are mutable.** Replacing a photo uploads a new `stored_file` and
  repoints the link — bytes are never overwritten, so nothing cached anywhere goes stale.
- **`StoredFileReferences` must list every table with a foreign key to `stored_file`.** A unit
  test derives the real set from the EF model and fails when the two disagree. That registry is
  what a later retention sweep asks "is this file still referenced?", and a table missing from
  it would have the sweep delete live files.
- **Bytes land before the row does**, so a failed link is compensated by deleting them — the
  chassis `DiscardOrphanBytesAsync`, called by `ServiceSupplierService.AddPhotoAsync`,
  `ProposalAttachmentService.AddAsync`, `EventImageService.AddAsync`,
  `RaffleService.SetPrizeSponsorLogoAsync` and `SignatureService.SendAsync`. The last one saves
  several files before its one insert, so a failure or a cancellation partway discards every file
  already written. What survives that is an orphan blob, never an orphan row.
- **One writer keeps its bytes when the row fails: the signed copy of a signature request.**
  `SignatureDrainService` saves what FirmaGOB hands over, and the middleware hands it over only
  once (PGTI-281, T3). If the commit then fails, the bytes are the only copy anywhere, so they are
  never discarded: the error log names their storage keys, and a person recovers them with the
  runbook. The row stays `Completing` and its next download answers `404`, so it ends `DownloadLost`.
- **A proposal's attachments belong to the CandidateProposal, not to the Candidate or the
  Quote.** They justify one price and no other's, so `IProposalAttachmentService` is deliberately
  **not** part of `IQuoteService`: none of the quote lifecycle rules apply to a document.
  Attaching does not require `draft` and does not stamp the event, because an attachment records
  what was already decided rather than changing it. They do travel with the proposal on read —
  `GetQuote` returns each proposal's `attachments`, loaded by `ProposalAttachmentLoader` — because
  the written quotation is read beside the amount it justifies; `CandidateWithServiceSupplier`
  carries them as one flat list per candidate and the response mapper splits it by proposal. The
  route is flat (`/v1/proposals/{id}`) rather than nested under the event and category: a proposal
  id is unique service-wide, and whoever holds one already got it from the quote detail. Each
  attachment carries a `ProposalAttachmentKind` — `Quote` or `ProposalImage` — which is **not**
  the sniffed content type below: a scanned quotation is an image and a rendered proposal is a
  PDF, so the bytes cannot answer it and the uploader says so. Omitting it means `Quote`, which is
  what every row written before the column meant. The per-proposal quota counts both kinds
  together: it exists to catch a wrong click, and that does not depend on what the `kind` claims.
  **Removing a proposal soft-deletes its attachments in the same transaction**, and removing a
  candidate does it for every one of its proposals: a link under a dead proposal is unreachable by
  any read while the retention sweep still counts it as a reference.
- **A final amount may name one of its own proposal's attachments as its FinalQuote** (PGTI-364,
  `events-service/docs/quotes.md`). That is not a file in an owner's body: the link already exists,
  and the two `candidate_proposal` columns point at `proposal_attachment`, not at `stored_file`, so
  `StoredFileReferences` does not change. A pending amount that never takes effect takes its
  FinalQuote with it, as an ordinary soft-delete of the link. The pointers are only followed through
  `deleted_at IS NULL`, so removing an attachment by hand needs no lock and touches no proposal: a
  pointer to a removed link reads as `null`. Both foreign keys are `ON DELETE SET NULL`, which says
  the same thing should a link ever be hard-deleted. A FinalQuote replaced on the amount in effect
  stays linked and still counts against the per-proposal quota (`events-service/docs/quotes.md`).
- **An Event's images belong to the Event, and the cover reaches the list projection.** An
  `EventImage` carries an `EventImageKind` — `Cover` or `Gallery` — which the bytes cannot answer,
  since both are a JPEG. Omitting it means `Gallery`, deliberately: the cover is unique, so
  falling into it by default would replace one nobody asked to replace. **An event has at most one
  cover, and uploading a second replaces the first** rather than being rejected — changing the
  cover is one click, and demanding the old one be removed first leaves the event with none if the
  second call fails. The replaced link is soft-deleted in the same transaction that writes the new
  one, and a partial unique index (`kind = 'Cover' AND deleted_at IS NULL`) is what serialises two
  simultaneous uploads. The per-event quota counts **gallery photos only**: the cover's own rule
  already bounds it, and counting it would leave the gallery one short of the number it states.
  Only images pass here — `InspectImageAsync` refuses a PDF with the shared `FILE_TYPE_NOT_ALLOWED`
  code, because what is uploaded to an event is shown on a page rather than downloaded. The images
  travel with the Event on read: `GetEventById` and every mutation return `images` (cover first),
  and `ListEvents` returns `cover` per row so a grid renders without a detail read per event.
- **A prize's sponsor logo is a nullable column, `prize.sponsor_logo_file_id`, not a link table.**
  A prize has at most one logo and nothing else hangs off it, so replacing is repointing the column
  and removing is nulling it; the old file keeps its row and bytes either way, as a replaced cover
  does. `ck_prize_sponsor_logo_needs_company` holds it to a sponsored prize, and the upload refuses
  an unsponsored one with `PRIZE_NOT_SPONSORED` before a byte is stored. It locks with the prize:
  `SetupGuards.EnsureCanSetSponsorLogo` runs before the save and again under the raffle lock. A
  prize `PATCH` that changes or clears the sponsor company nulls the column **in the same `UPDATE`**,
  comparing against the row's company rather than the one the service read, so a logo uploaded in
  between survives an edit that keeps the company. Only images pass, as on an event.
- **Removing a photo, an attachment or an event image unlinks it and never deletes anything.**
  `RemovePhotoAsync`, `ProposalAttachmentService.RemoveAsync` and `EventImageService.RemoveAsync`
  soft-delete the link row and leave `stored_file` and the bytes untouched, so the file still
  answers by id. That is what keeps the retention sweep a choice rather than a rescue. The owner
  id is part of the removal's filter, not just the route: without it, anyone holding a photo,
  attachment or image id could unlink it through an owner of their own.
- **The kind of a file is decided by its bytes, never by the claim.** The declared content type
  and the extension are both chosen by whoever uploads, so `UploadPolicy` sniffs the header
  (through the chassis `FileContentInspector`) and refuses a file whose content contradicts either
  one rather than silently correcting it — the name is what gets shown later. What is stored and
  re-served is the sniffed type. Every rule is checked **before** `IFileStore.SaveAsync`, so a refusal leaves nothing behind; the per-owner
  quota is what makes `CountPhotosAsync` / `CountAsync` a pre-check rather than a compensating
  delete. HEIC is recognised precisely so it can be refused with its own code and a message
  naming the iPhone setting to change.

- **A missing file and an unreachable store are never conflated.** The store answering "not
  there" is a loss: 404 `FILE_MISSING`, and `stored_file.missing_since` is stamped on the first
  read that finds the hole — later reads keep that first moment rather than moving it. The store
  not answering is an outage: 503 `FILE_STORE_UNAVAILABLE`, and **no row is written**, because
  marking files missing on a restart would destroy the record of what was actually lost. A
  restored file clears its own mark on the next successful read, which is what keeps
  `GET /v1/files/missing` — the operator's list of what to pull out of the store's trash —
  worth reading. Lists never hide a missing file: it keeps its name and upload time and comes
  back with `missing: true`, so a page renders a placeholder rather than breaking.

`FileStore__Provider` (`filesystem` today), `FileStore__Filesystem__RootPath`,
`FileStore__MaxSizeBytes`, `FileStore__MaxPhotosPerServiceSupplier`,
`FileStore__MaxAttachmentsPerProposal` and `FileStore__MaxGalleryImagesPerEvent` are the
configuration — the section carries both the
chassis's provider wiring and this service's limits, because they deploy together. Compose gives
the local stack a named volume for the root path. Integration tests run the real filesystem
provider on a temp folder rather than substituting `IFileStore`, because writing bytes, hashing
them and failing on a missing file is precisely what needs exercising. The one exception is the
outage: `FaultyFileStore` **wraps** that real provider so `FileStoreDegradationTests` can take it
down and time it out, since a temp folder never falls over on its own.
