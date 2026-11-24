# events-service: raffles

> Moved from `events-service/CLAUDE.md` (PGTI-213), text unchanged except headings. A section named
> with "see ... below" may now live in a sibling file in this folder: `grep -n '^## ' events-service/docs/*.md` finds it.

## The raffle reads two seams, and draft reads go through a copy

`docs/specs/raffles/` is the spec; this is where it lives in the code.

- **Two seams in `Core/Raffles/`.** `IAttendanceSource` (who is at the door) is
  `Infrastructure/Convocatorias/ConvocatoriasAttendanceSource`, which reads convocatorias'
  check-in list (`GET v1/convocatorias/events/{eventId}/check-ins`, `check-in:service-read`, spec
  `09` §7) through `AddServiceClient(..., "Convocatorias")`. It is all or nothing: any failed status
  (404 included), an open circuit, an unreadable body, a missing `items`, a row missing a required
  field or an `origin` other than `invited` / `referred` / `walk_in` fails the whole read as
  `ATTENDANCE_UNAVAILABLE`, never a partial or empty list. `registrationId` is dropped; the
  identity is the normalized email. `IAwardsMembership` (the DO Travel Awards list) is a placeholder,
  `NoAwardsList`, until PGTI-115: it answers `None`, the awards step is skipped, and the raffle read
  says so with `awards_list_not_in_force`. `None` is "no list", never "an empty list".
- **Its integration tests substitute convocatorias, not the client**: `ConvocatoriasCheckInStub`
  (WireMock) behind `ConvocatoriasCheckInApiDbFactory`. Suites that test what the raffle does with
  the list use `RaffleAttendanceApiDbFactory` and its `FakeAttendanceSource` instead. Everywhere
  else `Services:Convocatorias:BaseUrl` points at `http://convocatorias.invalid`.
- **Cached vs live attendance is two seams, not a flag.** The draft reads (`GET …/raffle` readiness,
  `GET …/raffle/attendees`) use `IRecentAttendance` → `Infrastructure/Raffles/CachedAttendance`: one
  copy of the door list per event on `StaleTolerantCache`, fresh for
  `RaffleAttendance__CacheTtlSeconds` (default 30), kept 12 h. PGTI-118's `start` must use
  `IAttendanceSource` directly: a frozen drum from a stale copy cannot be corrected. The cache holds
  the door list only; awards, overrides and CRM are applied on every read.
- **Attendance degradation travels in the body**, `attendanceStatus` + `attendanceFetchedAt`, never
  on `X-Crm-Directory-Status`: that header speaks for the CRM alone.
- **The pipeline is `Core/Raffles/Qualification.Classify`**, pure, first match wins, in the order of
  `ExclusionReason`. The 5,000-attendee ceiling is counted in `RaffleService`, not in the seam.
- **Unknown is not empty.** With no copy of the door list the counts and the list are `null` and the
  only blocking reason is `attendance_unavailable`, never `no_qualified_attendees` beside it.
- **The CRM blacklist is a third seam, `IContactMailability`, asked live on every draft read and
  never cached** (`Infrastructure/Crm/CrmContactMailability`, batches of 1,000 in series, all or
  nothing). It gates on CRM's invitation answer, never the email one (PGTI-190): `canBeInvited:
  false` excludes, as `suspended` when that is the reason and `do_not_invite` for any other, and a
  row without `canBeInvited` fails the whole check as `CRM_UNAVAILABLE`. `opted_out` qualifies; an
  id CRM leaves out is `contact_unresolved`. With CRM down the read classifies without that step, blocks on
  `crm_unavailable` and says `unavailable` on `X-Crm-Directory-Status` — the two raffle reads carry
  that filter, and so do `start`, `reopen`, `complete` and `reissue`, because they return the raffle
  with its sponsors named (`RaffleResponse.FromAsync`, the one way to build it). The prize writes do
  not: they return ids only. Nothing asked, no header.
- **A prize's sponsor is ids only** (`sponsor_company_id`, `sponsor_contact_id`, a check constraint
  keeping the contact with a company), validated live in `RaffleService.ValidateSponsorAsync` after
  the raffle's own rules, and named at the edge by `RaffleSponsorNames`, never on `RaffleDetail`.
  The acta reads the company name from the cached directory in `ResolveSponsorNamesAsync`; a name
  it cannot find at all is `503 CRM_UNAVAILABLE` and the generation writes nothing (spec 03 §4.2
  point 9).
- **Overrides (`raffle_attendee_override`) are checked in spec 01 §9.1's order** by
  `RaffleService.SetOverrideAsync`, reading the door through `IAttendanceSource` (live), not the
  copy. The integration suites that take the CRM down sit in their own classes and create their
  events first: the chassis circuit is one per destination, so tripping it on mailability also
  fails the OPT check every event create makes.

- **A handover hangs off the draw, never the prize** (`prize_handover`, spec 03 §3). The repository
  loads each draw's handovers into `PrizeDraw.Handovers`, so `PrizeState` (`undrawn`/`drawn`/`redeemed`)
  and `PrizeDrawStatus` (`drawn`/`redeemed`/`voided`, per draw) are both derived from the draws alone.
  Handover and reverse lock the raffle like draw and void, decide in `PrizeHandingOver`, and pass on a
  `completed` raffle and a closed event (`Raffle.EnsureStarted`, not `EnsureInProgress`). The handover
  saves the signature's bytes **before** the lock and decides the prize's state once, under it; a
  refusal discards the bytes. The signature is PNG only, 1 MB, a constant in `UploadPolicy`.
- **The winner email is the quote alerts' rules with one exception: it reads `Event.Language`**
  (PGTI-124, spec 03 §5). `RaffleService.NotifyWinnersAsync` runs after `complete` commits, **outside the
  request**: the endpoint enqueues it on `RaffleWinnerNotificationWorker` (an in-memory channel, with the
  completer's account) because 10,000 winners take minutes (A31). The `complete` response therefore
  carries no email outcome; reread the raffle to see it. The worker rereads
  the raffle and the event with `CancellationToken.None`, and calls `IRaffleWinnerNotifier` once per
  live draw in prize order. Each outcome lands on the draw (`winner_notification_status` / `_id`) with
  `CancellationToken.None`. The template is the event's language, then `es`, and the request carries
  the language used. The key is `events:raffle:{raffleId}:draw:{drawId}:won`; `reissue` never sends.
- **`GET /v1/prize-draws/by-code/{code}` normalises, then matches exactly** (`RedemptionCode.TryNormalize`):
  never `LIKE`, never a name, never a redeem. Malformed input is `404`, like an unknown code.
- **Receiver documents and signature ids are readable by staff and service accounts only**, through
  the prize shape of `GET …/raffle` and through `/v1/files/{id}/content`: both sit on the fallback
  policy, which requires `platform:staff` or `platform:service` (ADR-0025, PGTI-126), and door staff
  hold neither. No raffle route needs its own read policy for it (spec 03 §7).
