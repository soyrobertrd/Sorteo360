# ADR-0014 — File attachments live behind a storage seam, with Nextcloud as the store

- Status: Partially superseded by ADR-0031
- Date: 2026-08-19

> **Partially superseded by [ADR-0031](0031-collaborator-identity-documents-are-sealed-by-the-crm-and-disks-are-encrypted-instead-of-tde.md)**,
> for Collaborator identity documents only. Those are sealed before they reach the store, hard
> deleted after replacement or departure instead of kept, virus-scanned by the service rather
> than by Nextcloud, readable only with a dedicated role, and never cached by the browser.
> Everything below still holds for every other file.

> **One name changed after the fact; the decisions below still hold.** A Candidate gained several
> priced offers, so the quote attachment moved down one level: `candidate_attachment` is
> `proposal_attachment`, its owner is a `CandidateProposal`, the route is
> `/v1/proposals/{proposalId}/attachments`, and the quota of 10 counts per offer rather than per
> candidate. Everything the ADR decides — the `stored_file` core, typed links over a polymorphic
> table, immutable files with mutable links, the seam, the constraints — is unchanged. Read
> "Candidate" as "CandidateProposal" wherever this document names the attachment's owner.

## Context

Two tables have carried references to files that do not exist. `events.service_supplier_photo`
stores `file_ref_id` and `url` under a check constraint requiring at least one of them, and
`events.candidate` stores a single `quote_photo_ref`. Nothing was ever written behind those
columns: there is no upload, no storage, and no download. A venue's photos and a supplier's
quote document — the two things an OPT coordinator actually needs in order to choose — have
been unrepresentable since the schema was written.

v12 designed a `files-service` (blob storage on SMB/NFS, signed tokens, nginx `X-Accel-Redirect`
offload). It was deferred with the rest of the platform fleet (`docs/ARCHITECTURE.md` §8), and
nothing replaced it.

The platform has no object store. It runs on Docker Compose on-premises in every environment,
one database per service inside a shared PostgreSQL instance, and its only durable volume is
postgres's. Three storage options were offered: OneDrive, a file server, and an ImageKit free
account. During the decision a Nextcloud instance was stood up on the stage server and the
client chose it.

Three further consumers are already on the roadmap — convocatorias (arte), pagos (facturas)
and reuniones (perfiles) — so the shape chosen here is the shape they inherit.

## Decision

- **Storage facts and business facts live in different tables.** `events.stored_file` records
  only what storage knows: provider, storage key, content type, size, checksum, who uploaded it
  and when. Typed link rows record what a file is *for* — `service_supplier_photo` gains
  `stored_file_id`, and a new `candidate_attachment` replaces `candidate.quote_photo_ref`.
  - **Not one polymorphic `attachment(owner_type, owner_id)` table.** PostgreSQL cannot
    foreign-key a column that means two things, so a single table trades enforced referential
    integrity for application-enforced convention, and deleting an owner leaves rows nothing
    stops. Typed links keep real foreign keys, and let photos carry display order while quote
    attachments carry their own semantics.

- **Files are immutable; links are mutable.** Replacing a file uploads a new `stored_file` and
  repoints the link. Bytes are never overwritten in place.
  - Overwriting a key would strand every CDN edge and browser cache that already fetched it,
    recoverable only by a purge call that may not land. A new key has nothing stale to
    invalidate. It also keeps the original answerable: what a supplier quoted before revising.

- **Unlinking soft-deletes the link and keeps the bytes.** Consistent with this service's
  existing stance that the supplier pool is history and is never hard-deleted. A future
  retention sweep is additive, and is why three things exist now: `deleted_at` on the link row,
  `Delete` on the storage seam with no caller today, and **one registry that knows every table
  referencing `stored_file`**, guarded by a test that fails when a new link table is added
  without registering it. Without that registry, "is this file still referenced?" is a `UNION`
  somebody forgets to extend, and the sweep deletes live files.

- **A venue's photos belong to the ServiceSupplier, not to an Event.** A venue is a
  ServiceSupplierCategory, and its photos describe the venue itself — reusable across every
  event that shortlists it. A Candidate may carry many attachments, because a real quote arrives
  as a PDF plus scans.

- **`Core` sees only `IFileStore`.** The seam and its implementations —
  `NextcloudFileStore`, `FilesystemFileStore` — are technical scaffolding and live in
  `shared/Sorteo360.Platform` (ADR-0012), so the three roadmap consumers reference them
  rather than copy them. The tables, endpoints and rules stay in events-service, because
  `stored_file` is the `events` schema and one database per service means it cannot be shared.
  **The word "Nextcloud" appears in exactly two places: `Infrastructure` and `Program.cs`.**
  No signature in `Core` mentions a share id, a WebDAV path or a provider concept; a file id is
  an opaque string the domain never interprets. This is the third application of the seam
  discipline already used for `IApprovalGate` (ADR-0005) and `IOptDirectory` (ADR-0013).

- **Nextcloud is the production store, and the app owns its tree exclusively.** A service
  account owns `/pgti360/`; humans have no write access, and read access only if shared
  explicitly. This is what keeps a file manager usable as app storage: without it, someone
  tidying a folder in the web UI dangles every stored key at once.
  - **References are Nextcloud's file id, not a path**, so a move or rename inside the instance
    does not break them. The path is kept as a human-readable hint with no authority.
  - The service account authenticates with a **revocable app password**, never the account
    password, so it cannot log into the web UI and can be rotated alone.

- **events-service reaches Nextcloud over the shared Docker network, not over its public
  domain.** Nextcloud runs as its own Compose project on the same host and joins
  `dokploy-network`, which every application on that server joins, so both containers already
  share a network. It is addressed by its Compose alias:

  ```
  FileStore__Nextcloud__BaseUrl = http://nextcloud/remote.php/dav
  ```

  - The alias comes from the service name in Nextcloud's own Compose file, so it survives a
    redeploy. The full container name does not: it carries a generated project suffix.
  - The alternative — `https://files.<domain>/remote.php/dav` — sends every upload and every
    download out to the reverse proxy, terminates TLS and comes back, for traffic between two
    containers on one machine, and makes a certificate or DNS problem an upload outage. It stays
    documented as the fallback because switching is one environment variable and no code: the
    seam is what makes the route an operational choice rather than an architectural one.
  - Bytes travel unencrypted on that bridge, which is the posture events-service already has
    with crm-service and with postgres.
  - The cost is a dependency on infrastructure nobody on this project owns. `dokploy-network`
    belongs to the deployment tool and the Nextcloud application to whoever deployed it, so
    either can be renamed or torn down without notice. Stored references survive that — they are
    file ids, and the fallback route needs one variable — but uploads and downloads stop until
    the address is corrected.

- **All bytes stream through events-service, role-checked.** No public share links.
  - Nextcloud's public links are persistent share *objects* that accumulate per file, are
    independently revocable by any admin, and punch a hole into the tree just locked down —
    while delivering none of the caching that made public delivery attractive, because
    Nextcloud is not a CDN. `ETag` and `Cache-Control` recover most of the practical benefit
    without exposing anything.

- **Upload is one multipart call per owner**, not upload-then-link. A generic `POST /v1/files`
  would make "uploaded but never linked" a routine outcome of closing a tab, requiring the very
  sweeper this decision defers, and would raise a question the owner-scoped route answers for
  free: who may upload a file attached to nothing. Reads are flat —
  `GET /v1/files/{id}` and `/content` serve any consumer that holds an id.
  - Orphan *blobs* remain possible if the bytes land and the transaction then fails. A
    compensating delete covers the common case, and the app-only tree makes a later sweep
    feasible at all, since anything in the tree without a `stored_file` row is by definition
    orphaned.

- **Constraints are enforced by content, not by claim.** 25 MB per file, 20 photos per
  ServiceSupplier, 10 attachments per Candidate; `image/jpeg`, `image/png`, `image/webp`,
  `application/pdf` only. The type is decided by **magic-byte sniffing**, never the declared
  `Content-Type` or the file extension, both of which the caller controls; downloads carry
  `Content-Disposition: attachment` and `X-Content-Type-Options: nosniff`. **HEIC is rejected**
  with an explanatory message: iPhones shoot it by default and Chrome and Firefox cannot render
  it, so accepting it would store files half the users see as broken.
  - **The size and type rules live in the chassis** (`FileContentInspector`, PGTI-182), so every
    service that accepts files applies the same 25 MB and the same formats by reference. The
    per-owner counts and the error codes stay in each service.
  - **Virus scanning is Nextcloud's, not this service's.** Its antivirus app scans WebDAV
    writes, so scanning is an infrastructure decision rather than C#. A quarantined file becomes
    a dangling reference, which the degradation below already covers.

- **A missing file and an unreachable store are different, and are never conflated.**
  Nextcloud answering 404 means the file is gone: **404 `FILE_MISSING`**, and `missing_since` is
  stamped on the row. Nextcloud unreachable or erroring means an outage: **503
  `FILE_STORE_UNAVAILABLE`**, with **no side effect on any row**. Marking files missing because
  a server restarted would destroy the record of what was lost exactly when it matters — the
  same reasoning that keeps the OPT snapshot's TTL separate from its cache expiry (ADR-0013).
  - **List endpoints keep returning a missing file's metadata** with `"missing": true`. The
    filename, uploader and date are what identify what needs restoring from Nextcloud's trash.
    Reads degrade; they do not fail.
  - **The stamp records the first moment, and a restore clears it.** Every later read of a lost
    file walks the same path, so re-stamping would drag the date forward to the last attempt and
    lose when it actually went missing. In the other direction, the first read that finds the
    bytes back clears the mark: without that, a file pulled out of the trash stays flagged
    forever and the list of what to restore fills with things already restored, which is how a
    list stops being read.
  - **`GET /v1/files/missing` is the operator's view of that list** — paged, most recently lost
    first, carrying each file's name, type, size, uploader and both dates. Nextcloud's trash is
    browsed by name and date, so those are what a person needs in hand before opening it. It
    takes the same authenticated-user posture as every other read here, for the reason below.

- **`url` is dropped, and with it the check constraint.** A file is referenced one way. An
  externally-hosted link escapes every rule above: it is public and uncontrolled, it rots
  without any `missing_since` to detect it, and it obliges every renderer to carry a second
  path that nothing tests.

- **Attachments inherit the read posture of the record they document** — an authenticated user,
  per the fallback policy. No new read role. events-service has no read authorization at all
  today, so `GET /v1/quotes` already discloses every candidate's estimated amount; restricting
  the PDF that states that amount while leaving the amount itself open would be theatre.
  Procurement confidentiality is a real question and is filed as its own decision.

- **ImageKit and MinIO are deferred, and OneDrive is rejected.** ImageKit remains the right
  future home for public, cached, transformable delivery of venue photos — thumbnails and format
  conversion are what it does well, and it becomes an additive provider behind the same seam.
  MinIO was the stronger technical fit for on-prem object storage before Nextcloud was chosen.
  OneDrive was rejected outright: Graph throttles aggressively, expiring anonymous links depend
  on tenant policy the platform does not control, and it has Nextcloud's rearrangement hazard
  without the ability to lock the tree down.

- **A filesystem provider ships alongside the Nextcloud one**, used for local development and
  integration tests and never in production. It costs almost nothing, needs no credentials, and
  because it is so unlike WebDAV — no HTTP, no file ids, no share links — it is what proves the
  seam is genuinely abstract rather than Nextcloud-shaped. The provider is selected by
  `FileStore__Provider`, not by `ASPNETCORE_ENVIRONMENT`, so any environment can point at either.

## Consequences

- **The events API breaks on both sides, in one release.** `quotePhotoRef` leaves
  `AddCandidateRequest`, `UpdateCandidateRequest` and `CandidateResponse`. The `photos[]` array
  leaves `CreateServiceSupplierRequest` and `UpdateServiceSupplierRequest` entirely, and
  `ServiceSupplierPhotoResponse` is reshaped from `{ id, fileRefId, url, date }` to
  `{ id, storedFileId, fileName, contentType, sizeBytes, createdAt, missing }`, and is what
  `GET /v1/service-suppliers/{id}` returns too, so the detail and the photo list agree.
- **Photos stop being a field and become a sub-resource.** A ServiceSupplier can no longer be
  created with its photos in one payload, because multipart bytes cannot be uploaded to a row
  that does not exist yet: create the supplier, then upload each file. This also ends the
  ADR-0009 replace-the-array semantics for photos, so replacing one photo no longer silently
  drops the other four.
- The demo `frontend/` and the production SPA are exempt from the same-PR consumer rule and
  catch up on their own card. A Spanish migration guide is published for that work.
- **The migration is destructive**, dropping `url`, `file_ref_id` and `quote_photo_ref`. Stage
  runs `development` with test data only and production is not running the stack, so no backfill
  is written. This does not transfer to any future column drop.
- Every photo view is now traffic and memory through events-service, on a server already tight
  enough that .NET builds are killed for RAM. Streaming rather than buffering bounds it, and no
  production SPA exists yet to load it. This is the cost that ImageKit later removes.
- **Nextcloud becomes a production dependency of events-service.** Its availability, its backup,
  its retention windows and its antivirus configuration are now platform operations concerns,
  and the instance must become sanctioned infrastructure rather than an ad-hoc deployment.
- **The two Nextcloud assumptions this decision rests on were verified on stage** against
  Nextcloud 32.0.14.1, on 2026-08-23, before any provider code was written.
  - `oc:fileid` is **stable**: the same id survived a rename and a move into another folder.
  - An id **resolves back to a file**. WebDAV has no fetch-by-id verb, so this was the unstated
    half of referencing by id; Nextcloud's DAV `SEARCH` endpoint accepts `oc:fileid` in a `where`
    clause and answers with the file's current path. The provider therefore reads the stored path
    first and falls back to `SEARCH` only on a 404, costing one request in the common case.
- Contract tests against real Nextcloud are trait-tagged and run locally on demand. CI runs only
  the four `Tests.Unit` projects — an accepted cost constraint of a private repository on the
  free plan, not an oversight — so nothing automated will catch a Nextcloud upgrade that changes
  WebDAV behaviour.
