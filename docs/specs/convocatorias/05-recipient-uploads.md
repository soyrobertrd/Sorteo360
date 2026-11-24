# Recipient Uploads — Spec

A coordinator drops a spreadsheet on a draft campaign. It parses, shows what will happen to every
row, and only on confirmation does convocatorias create the unmatched contacts in CRM.

Creating them silently means one typo'd column puts junk in the system of record, and the staging
screen is where suppression and duplicate warnings have to surface anyway.

## 1. An upload attaches to a draft campaign

```
POST /v1/convocatorias/campaigns/{id}/recipient-uploads          # stages
POST /v1/convocatorias/campaigns/{id}/recipient-uploads/confirm  # commits
```

Both need `campaigns:write` and both sit behind the **draft-only guard** `03` §5 already specifies,
so an upload onto a campaign that has been launched is refused by the same rule that makes `sending`
read-only. Since removing approval left `draft` as the only editable state, that guard is now the
whole rule rather than one case of it.

A **free-standing contact import** was rejected on two grounds, the second decisive:

- `campaign_exclusion` is campaign-grained, so a free-standing import has nowhere to put a
  matched-and-suppressed row.
- Upload-created contacts are **unattached and carry no classification**, and the selection read
  filters on exactly `classification` and `companyCategory`. A free-standing import would create forty
  contacts that the only selection mechanism in the product cannot find again.

So an upload is a **second recipient source on a draft campaign**, beside CRM selection, and §6's
union rule lets both be used on one campaign.

## 2. What is stored, and what is not

**The uploaded file is parsed and discarded.** Its bytes are never written to storage. Storing them
would make convocatorias an ADR-0014 consumer for an audit benefit nobody asked for, and **the staged
rows are the better record**, because they are what the user confirmed while the bytes are only what
they sent.

Two tables. Both cascade-delete when the campaign is cancelled, and they part company on
confirmation:

| Table | Grain | Holds | On confirmation |
| ----- | ----- | ----- | --------------- |
| `campaign_upload_batch` | one per upload | `original_filename`, `detected_encoding`, `row_count`, the six bucket counts, `uploaded_at`, `uploaded_by_id`, `confirmed_at`, `confirmed_by_id` | **Survives.** Permanent |
| `campaign_upload_row` | one per data row | source row number, parsed fields, raw company text, outcome bucket, reason code, `contact_id` once created | **Deleted** |

**The batch row is permanent because another decision depends on it.** `08` defers
`onBehalfOfUserId` on `POST /v1/contacts`, so every uploaded contact is attributed to
`service-account-convocatorias-service`, and that deferral is acceptable only because this batch
records who confirmed. Deleting it would quietly withdraw the thing that made the deferral
acceptable, and the later change is back-fillable from exactly this record. Six integers and two user
ids is the whole cost.

The staged rows do not survive, which is where the bulk is. The bucket counts answer what anybody
asks afterwards — "we uploaded 340 rows and 12 were malformed" — without keeping 340 rows to say it.

**A second upload replaces the first**, and the constraint says so precisely: unique on `campaign_id`
**`WHERE confirmed_at IS NULL`**. One *unconfirmed* batch per campaign, many confirmed ones. Same
partial-index idiom as `email_normalized` among live rows.

Appending was rejected: it means reconciling two unconfirmed batches against each other, which is §5's
problem at a second grain, and the realistic reason somebody uploads twice is that the first file was
wrong. Two genuine lists compose by confirming the first and then uploading the second, because
confirmation frees the slot.

**No TTL and no cleanup job.** A TTL invents an expiry the user never sees coming, and a cleanup job
is the background process this platform keeps not having. An abandoned draft takes its batch with it
when it is cancelled.

## 3. File-level validation

A file-level failure rejects the whole upload, because it is a failure to produce a staging summary
at all. Everything else is a row (§4).

### 3.1 xlsx

Signature `50 4B 03 04`, **then a structural check**: open as `ZipArchive` and require an entry
`xl/workbook.xml`. The signature alone is not enough, because docx, pptx, jar and apk all begin with
those four bytes. That entry separates all of them, and it is less code than a `[Content_Types].xml`
override for the same outcome.

### 3.2 csv

**Magic bytes cannot identify a csv.** It is unmarked text and a BOM is not a csv marker, so the
validation *is* a decode-and-parse probe; the extension and declared content type only corroborate.

1. Read the first 8 KB.
2. Decode as **strict UTF-8** (`UTF8Encoding(false, throwOnInvalidBytes: true)`).
3. On `DecoderFallbackException`, fall back to **Windows-1252**.
4. Require at least one line splitting into two or more fields on a consistent delimiter.

**The detected encoding is surfaced on the staging summary.** An operator who sees "read as
Windows-1252" beside a mangled `José` has a lead; a silent fallback writes mojibake into the system of
record. One field, `detected_encoding`, on the batch.

### 3.3 Where the rule lives

**A purpose-built `SpreadsheetUploadPolicy` in this service.** Not an extension of events-service's
`FileSignatures`, and not a move into the chassis.

Events-service's policy is about images and PDFs served back to a browser; this one is about two
formats parsed once and thrown away. Its `Accepted` list, its HEIC case and its per-owner count quotas
are dead weight here. What is copied is the **shape**: `Result<T, BaseError>`, every check before a
byte is written, and the declared content type and extension used only to catch a contradiction, never
to decide.

## 4. Row outcomes: six buckets and a collapse

Every data row lands in exactly one bucket, and the buckets are disjoint so the counts reconcile:

```
data rows = matched + unmatched + suppressed + already_a_recipient
          + duplicate_in_file + malformed + collapsed_exact_duplicates
```

Evaluated in this order, first match wins:

| # | Bucket | Meaning | Confirmable |
| - | ------ | ------- | ----------- |
| 1 | `malformed` | Missing email, invalid email, missing name. Reason code per row | No |
| 2 | `duplicate_in_file` | §5 | No |
| 3 | `already_a_recipient` | Matched, and already a `campaign_recipient` on this campaign | No-op |
| 4 | `suppressed` | Matched, `canBeEmailed` false. Carries CRM's reason code | No, becomes an exclusion |
| 5 | `matched` | Found in CRM, mailable | Yes, as a recipient |
| 6 | `unmatched` | Not in CRM | Yes, created then a recipient |

`already_a_recipient` deliberately outranks `suppressed`: a person who opted out **after** selection
is already on the campaign, and removing them here would duplicate a job `04`'s launch-time re-check
already does at the only point where it is authoritative.

The `suppressed` bucket holds **matched-and-suppressed rows only**. An unmatched row is not in CRM and
cannot be suppressed.

**Row-level failures never block confirmation.** Rejecting a 400-row file over one typo is how people
end up editing spreadsheets by hand at midnight. And a file where **every** row is malformed is still
a *successful upload with zero confirmable rows*, because that is how a user discovers their column
mapping is wrong. A `400` tells them nothing.

## 5. Matching, and duplicates inside the file

**CRM normalises and convocatorias does not.** Convocatorias trims whitespace and sends the raw
address to `GET /v1/contacts?email=`; CRM lowercases and matches against `email_normalized`.

Implementing our own normalisation would put one rule in two codebases, which surfaces as a row
reported unmatched that then `409`s on confirmation.

The one exception is in-file dedupe, which needs a local key with no round trip. It uses **trim plus
lowercase only**, a conservative subset of anything CRM might do, so any drift makes us *under*-merge,
which surfaces as a `409`, which is already a defined non-failure. Drift the other way would silently
merge two people.

**No gmail dot-stripping and no `+tag` handling anywhere.** That is a decision about who counts as one
person, and it belongs to CRM's uniqueness rule or nowhere.

**A row with no email is `malformed`, reason `missing_email`.** It cannot be a recipient, and creating
an emailless contact from a campaign upload puts somebody in the system of record who cannot do the
job they were uploaded for. Note that `no_email` also exists as a *suppression* code; the two stay
apart because suppression is about a known contact and an unmatched emailless row is not a contact
yet. **Provisional**: the client's file may encode people with no address, and if that is common in
their real lists this is the rule to revisit first.

Two rows sharing a normalised email split into two cases, because only one of them contains a
decision:

| Case | Behaviour |
| ---- | --------- |
| Rows **identical** across all mapped columns after trim | Collapse to one and report as a count, "3 exact duplicate rows collapsed". Nobody has to pick between two identical rows |
| Rows share an email but **differ** anywhere else | **All of them** go to `duplicate_in_file` and none is created. The file disagrees with itself and we have no basis to break the tie |

The bucket names every row number in the group so the user can go and look.

First-wins was rejected: it picks a winner on no evidence and the user never learns the rows
disagreed. Last-wins is worse, making the outcome depend on spreadsheet row order in a way nobody can
see. Rejecting the file outright loses the copy-paste case, which is common and where collapsing is
obviously right.

**`duplicate_in_file` stays separate from `matched`**, because the two mean opposite things: "your
file has this person twice" is a mistake and "we already know this person" is good news.

## 6. The confirmation write

**One request from the browser. N requests inside.**

```
browser ──1 request──> nginx ──> convocatorias
                                      │
                                      └─ N sequential POSTs ──> crm-service
```

The inner calls go by Compose service name and never touch nginx, so `client_max_body_size` and
`proxy_read_timeout` do not apply to them. The outer request's duration is the sum of the inner ones,
which is what §7 derives the row ceiling from.

Confirmation is **synchronous, resumable and idempotent per row**:

- Each row moves to `created` with its returned `contact_id` as it succeeds, so **the batch is its own
  cursor**.
- A failure stops the loop and leaves the batch alive, partly done.
- The response is `503 CRM_UNAVAILABLE` with the completed count in the ProblemDetails extensions.
- A retry re-runs the same endpoint and skips every row already marked `created`, so nobody is
  double-created.
- **A `409` is a match, not an error.** It carries the existing contact; take the id, link, and
  continue, moving the row from "created" to "matched" in the summary. **Re-check `canBeEmailed` on
  the returned body**, because the contact behind a `409` may be suppressed and without that check the
  race quietly mails someone who opted out. The check is free, since the `409` already carries the
  contact.
- Matched rows and rows a `409` resolves are linked without a create. Suppressed rows become
  `campaign_exclusion` rows. `already_a_recipient` rows do nothing.

Contacts are created **unattached**, one POST each, `has_consent` true and
`consent_source = verbal_upload`, because the employee took verbal consent before adding the person to
the list. **The spreadsheet's company column stays raw text** on the staged row for a human to
reconcile in CRM afterwards. Auto-creating companies from a typo'd column is exactly what the staging
step exists to prevent, and company deduplication by domain is unbuilt with rules of its own.

**A partial confirmation is never rolled back.** Contacts created in CRM stay created: CRM is the
system of record and we have no authority to delete a person there to tidy up our own failed batch.

### 6.1 The circuit breaker is load-bearing

`ServiceResiliencePipeline` gives every `AddServiceClient` call a 10s attempt timeout, a 30s total, 2
retries at 200ms, and a circuit breaker at a 0.5 failure ratio over a minimum of 10 calls in a 30s
window, breaking for 15s. **The pipeline is a singleton shared across the loop.**

That is what makes the resumable design real rather than theoretical. A pathological 500 rows times
30s never happens: once CRM degrades, around row ten the circuit opens and every remaining row fails
immediately instead of burning half a minute each.

Two consequences not to rediscover:

1. **A broken circuit is a local exception, not a `503` from CRM.** The loop must map
   `BrokenCircuitException` onto the same `503 CRM_UNAVAILABLE`, or the failure mode we most expect
   reaches the user as a `500`.
2. A confirmation that trips the breaker also blocks the selection read and the mailability check for
   15 seconds. That is correct, CRM is genuinely sick, and it agrees with the launch path, which
   already refuses rather than degrades.

## 7. Limits

Memory does not cap an upload here (3 MB peak on 200k rows), so every number is derived from
something else.

| Limit | Value | What it actually protects |
| ----- | ----- | ------------------------- |
| File size | **5 MB** | Little. Generous, cheap, checked before a byte is read. Well inside nginx's `client_max_body_size 32m` |
| Data rows | **500** | The confirmation write |
| Decompressed bytes, xlsx | **100 MB** | The zip bomb |

**Why 500.** `frontend/nginx.conf` declares no `proxy_read_timeout`, so nginx's 60s default governs
the confirm call. At a conservative 50ms per inner POST, 60s buys roughly 1,200 rows before the browser
gets a **504 while the write is still running**, which is the worst available failure because the work
continues and the user cannot see what completed. The row ceiling and that timeout are one decision.
500 rows is 10 to 25s on the healthy path, which closes with margin.

**This is a placeholder with a named owner**, not a number derived from the domain: the client still
owes us their real row count. Erring low is justified by the asymmetry, since "your file has 800 rows,
the limit is 500" is recoverable and clear while a 504 halfway through a write is not. **Raising it
later is not just a bigger number**: it means declaring an explicit `proxy_read_timeout` on the
confirm route, in `nginx.conf` **and** in its uncompared twin in `frontend/vite.config.ts`, or making
confirmation paged.

**Why the decompressed budget stays loose.** 100 MB is 20x the size cap and well past any real
spreadsheet. It exists to catch a 100x-plus expansion, since the measured bomb was 497 KB to 500 MB,
and not to catch a big file, which the size and row caps already do. Tightening it would
false-positive on a legitimate wide sheet and produce an error nobody can act on. **Enforcement
splits**: the policy owns the number, the parser call site owns the counting, because the expanded
size is unknowable beforehand. A counting wrapper around the `ZipArchiveEntry` stream aborts past the
budget while reading.

### 7.1 Error codes

Three distinct size codes, not one. A user can act on "your file is too big" and cannot act on "your
file expands suspiciously".

| Code | When |
| ---- | ---- |
| `FILE_TOO_LARGE` | Over 5 MB |
| `UPLOAD_TOO_MANY_ROWS` | Over 500 data rows. The message carries the cap |
| `UPLOAD_EXPANDED_TOO_LARGE` | xlsx expansion past 100 MB |
| `UPLOAD_TOO_MANY_COLUMNS` | An xlsx sheet declares or uses a column past the 50th (security audit run-1) |
| `FILE_TYPE_NOT_ALLOWED` | Not an xlsx per §3.1, not a csv per §3.2 |
| `UPLOAD_NO_HEADER_ROW` | Parsed, but no recognisable header |
| `CRM_UNAVAILABLE` | §6, including a broken circuit |

### 7.2 Parser gotchas

- **Sep needs `Unescape = true`.** Quote detection is on by default; quote removal is not.
- **Never read a csv through a `StringReader`**: the UTF-8 BOM glues itself to the first column name.
  Use a `StreamReader` with `detectEncodingFromByteOrderMarks: true`.
- `CodePagesEncodingProvider` is registered at startup (`00` §8), or §3.2's Windows-1252 fallback
  throws.

## 8. Columns

**Blocked on the client, and not blocking.** The parser's column list, which columns are mandatory,
and whether the file carries a company all wait on the real sample file. Everything above is settled
independently of them.

Build against a provisional mapping of first name, last name, email, phone and company, keep the
mapping in one place, and expect to change it once rather than to design for it.

## Definition of done

- A valid xlsx and a valid csv both stage, and the summary's seven counts sum to the data row count.
- A docx renamed `.xlsx` is `FILE_TYPE_NOT_ALLOWED`; a zip bomb is `UPLOAD_EXPANDED_TOO_LARGE`; a
  501-row file is `UPLOAD_TOO_MANY_ROWS`.
- A Windows-1252 file with accented names stages, and `detected_encoding` says so on the summary.
- A file where every row is malformed stages successfully with zero confirmable rows.
- Three identical rows collapse to one with a count; three rows sharing an email but differing all land
  in `duplicate_in_file` naming their row numbers.
- Confirmation creates only unmatched rows, links matched ones, writes a `campaign_exclusion` per
  suppressed row, and leaves `already_a_recipient` rows untouched.
- A CRM stub failing from row 30 returns `503 CRM_UNAVAILABLE` with the completed count, and a retry
  creates only the remainder. Prove the first 29 are not created twice.
- A `409` on a suppressed contact links the row and excludes it rather than mailing them.
- A stub failing enough calls to open the circuit still returns `503 CRM_UNAVAILABLE`, not `500`.
- Uploading twice onto one draft replaces the first batch; the partial index proves it at the database
  level.
