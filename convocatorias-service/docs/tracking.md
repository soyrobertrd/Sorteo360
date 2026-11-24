# convocatorias-service: tracking and reconciliation

> Moved from `convocatorias-service/CLAUDE.md` (PGTI-212), text unchanged except headings. A section named
> with "see ... below" may now live in a sibling file in this folder: `grep -n '^## ' convocatorias-service/docs/*.md` finds it.
> Spec numbers (`03`, `04` §1, ...) are files in `docs/specs/convocatorias/`.

## The participants list

`GET v{version}/convocatorias/events/{eventId}/participants`, `convocatorias:read`
(`docs/specs/convocatorias/07-tracking-and-reconciliation.md` §3). One row per person per event.

- **The merge is `Core/Participants/ParticipantMerge`, and it is the seam.** `IParticipantService.ListAsync`
  returns every merged, filtered, sorted row; the rollup and the CSV call that rather than merging
  again. The route only pages it.
- **The key is the trimmed lowercased email and nothing else.** Invited at one address and
  registered at another is two rows, on purpose; the test says so.
- **Drafts appear only under `?campaignId=`**, and with it the read is that campaign's recipients
  only, so `sendStatus` is that campaign's.
- **`GET .../campaigns/{campaignId}/recipients` is the same read found by campaign** (PGTI-178):
  `IParticipantRepository.ReadCampaignSourcesAsync` looks the event up from the campaign, and a
  direct message, with none, merges no registrations. For an event campaign the body is identical
  to `?campaignId=`, and a test compares the two strings. Paging only; unknown campaign is `404`.
- **The merge runs in memory**: an event is hundreds of people, and search without accents and the
  total order (lastName, firstName, email) run over the merged row.
- **`.../participants.csv` is the same read, unpaged** (§4). It takes `ListParticipantsRequest`
  (paging ignored) and writes `ListAsync`'s rows through `Core/Participants/ParticipantCsv`: UTF-8
  with BOM, Spanish headers, RFC 4180, a `'` before a cell that starts like a formula. The filename
  carries the **event id**, not its name, because tracking reads never call events-service (§7).
  **The delimiter is an open item** settled on a MITUR laptop; it is `ParticipantCsv.Delimiter`.
- **`checkedInAt` is the Registration's CheckIn** (`09` §6, PGTI-128): `RegistrationSource` carries
  `checked_in_at`, `ParticipantMerge` copies it, and it is null on a row with no Registration or no
  CheckIn. The CSV writes it last, as *Fecha de check-in*, in the same UTC format as *Fecha de
  inscripción*. No-show has no field: it is registered minus checked in.

## The tracking rollup

`GET v{version}/convocatorias/events/{eventId}/tracking`, `convocatorias:read` (`07` §2). It composes
reads that already exist and computes nothing of its own twice.

- **People and registration counts are `TrackingRollup.Count` over `IParticipantService.ListAsync`**,
  so a person reached by two campaigns counts once, under their merged send status, and drafts are out.
  `notRegistered` is `handed_off` and unregistered: never walk-ins, referred rows or failed sends.
  `registrations.checkedIn` counts the rows with a `checkedInAt`; Registrations are unique per
  `(event_id, email_normalized)`, so it equals the door list's `totals.checkedIn`, and a test says so.
- **`needsAttention` is `IReconciliationRepository.CountNeedsAttentionAsync`**, the bucket predicates
  filtered by event: pending CRM syncs, unreviewed `detailsDiffer`, stranded campaigns. Never
  pending unsubscribes, which are service-wide, and never the window.
- **The window is `{ open, closedAt, closedById }`**, the `07` §2 shape and not the window routes'
  response: `reopenedAt` would trip the "no `opened`" sweep (`07` §5), which runs once over the
  tracking and reconciliation bodies in `TrackingEndpointsTests`.
- **No neighbour call.** An unknown event id is `200` with zeros and an open window.

## The events a contact attended

`GET v{version}/convocatorias/check-ins?contactId=a&contactId=b`, `convocatorias:read` (PGTI-170).
One row `{ contactId, eventId, checkedInAt }` per contact and event, newest first. It is the CRM's
**Participation** (`crm-service/GLOSSARY.md`): attending is a CheckIn, not an invitation and not a
Registration. The contact ficha, the contact list's count and a company's Eventos tab (PGTI-171)
read it.

- **The contact is the registration's own `crm_contact_id`** (`09` §7.1). A walk-in with no CRM
  contact counts for nobody, and a referral counts for the colleague, not for whoever forwarded.
- **1 to 100 ids per call**, one query: a contact list page or a company's contacts is one request.
  An id with no check-ins has no rows; this service never asks the CRM whether it exists.
- **`checkedInAt` is the date the contact was there.** No event name or place: those are
  events-service's, and the reader resolves them by `eventId`. Two check-ins of one contact at one
  event (two addresses) are one row with the earlier time.
- **The columns landed here, ahead of the scan.** `AddCheckIn` is `09` §2 whole: `checked_in_at`,
  `checked_in_by_id`, `ck_registration_check_in_pair`, `ix_registration_event_checked_in`, plus
  `ix_registration_contact_checked_in` for this read. The door writes them (`docs/check-in.md`).

## The campaigns a contact received

`GET v{version}/convocatorias/campaign-recipients?contactId=a&contactId=b`, `convocatorias:read`
(PGTI-176). One row `{ contactId, campaignId, eventId, subject, launchedAt, sendStatus, registered }`
per contact and launched campaign that had it on its list, newest first. A company's Convocatorias
tab reads it for every contact the company ever had and keeps the rows launched while each one was
affiliated with it.

- **Launched campaigns only**, `LaunchedCampaign.Is`: a draft's rows are people nobody mailed yet.
- **`launchedAt` is the send date.** It is the campaign's stamp; a recipient has no send time of its own.
- **`registered` is the `07` §3.1 join**: a registration on the campaign's event at the address the
  recipient was mailed, normalised by `EmailAddressShape.Normalize`. Null for a direct message,
  which has no event; that row carries its `subject` instead.
- **Same 1 to 100 cap as the check-in read**, shared through `ListContactCheckInsRequestValidator.MaxContactIds`.
  No index on `campaign_recipient.contact_id` yet: the read scans the table, which is fine at today's volume.
- **`deliveryStatus` is the provider's half** (PGTI-177), beside `sendStatus`, which stays: see
  "Delivery status" below.

## Delivery status

What happened to a mail after `handed_off`: `sent`, `delivered`, `bounced`, `spam_report` or
`unsubscribed`. It is on two reads only, the row above and `.../campaigns/{campaignId}/recipients`
(and so `?campaignId=` on the participants list, whose body must match it); the event-wide list,
the CSV and the tracking rollup do not carry it, because one row there mixes campaigns.

- **The id is kept at send.** `HttpCampaignSendGateway` stores the per-recipient `id` of the `202`
  in `campaign_recipient.notification_id`. Nullable: rows from before, rejected rows and the stub
  transport have none and read `sent`.
- **notification-service owns the status and is read live**, `GET /v1/notifications?id=` in chunks of
  100 by `IDeliveryStatusReader` (`HttpDeliveryStatusReader`), with `notifications:read` on this
  service's account; the account only sees `convocatorias` rows. `CampaignDeliveryStatus` folds its
  two axes into one: the reaction (unsubscribed, complained) beats the delivery (bounced,
  delivered), and anything else is `sent`. Opened and clicked are off by design.
- **It degrades, never fails**: notification-service down, a `403`, an unreadable body or a call
  past its own 3-second budget is a warning in the log and every row reads `sent`. The budget is a
  caller cancellation on purpose: the reader shares the `Notifications` circuit breaker with both
  send transports, and Polly does not count a caller's cancellation, so a slow history read cannot
  open the circuit on campaign sends. A bounce with no correlation id is an orphan over there, so the row
  stays `sent` too. With `stub`, `StubDeliveryStatusReader` knows nothing; locally Mailpit sends no
  events, so everything stays `sent`.
- **Null means never handed off.** On the participants rows the field is then left out of the JSON
  rather than sent as null (`07` §5).

## The reconciliation screen

Nothing here retries on its own, so `07` §6 is the one place stranded writes become visible.
`GET v{version}/convocatorias/reconciliation` returns five counts, and each bucket is **its own
route** (`pending-registration-sync`, `pending-unsubscribes`, `details-differ`, `stranded-campaigns`,
`uncancelled-notices`) returning a `PagedResponse`, so each declares its row type and an unknown
bucket is a routing `404`. Plus `GET .../campaigns/{campaignId}/exclusions` (`07` §6.4) and
`POST .../registrations/{id}/acknowledge-details` (`campaigns:write`). Reads are `convocatorias:read`.

- **The rows are Core read models served as they are** (`Core/Reconciliation/ReconciliationRows.cs`);
  a Web copy would match them field for field.
- **Acknowledging stamps and never clears the flag.** `details_reviewed_at` and
  `details_reviewed_by_id` take the row out of the bucket while `contact_details_differ` stays true:
  the fix belongs to CRM. The stamp is a conditional `UPDATE`, so a second press keeps the first
  reviewer; a row whose details match is `409 REGISTRATION_DETAILS_DO_NOT_DIFFER`.
- **Stranded is deliberately dumb** (`07` §6.3): `sending`, a `pending` row, and `launched_at` older
  than `Reconciliation__StrandedCampaignAfter` (a `TimeSpan`, 15 minutes by default, in both compose
  files). No lease table and no heartbeat.
- **`uncancelledNotices` is the one read that is not local.** Only events-service knows what was
  cancelled, so it reads `GET /v1/events?status=cancelled` when somebody opens the screen, never on
  a timer (`06` §4.7 rejected the poll). `cancelledAt` is that event's `updatedAt`: cancelling is
  terminal and events-service refuses every edit afterwards. With events-service down the summary
  still answers, with that one count `null`, and the bucket itself is `503 EVENTS_UNAVAILABLE`.
