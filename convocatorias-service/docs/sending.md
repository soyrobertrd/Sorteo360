# convocatorias-service: sending

> Moved from `convocatorias-service/CLAUDE.md` (PGTI-212), text unchanged except headings. A section named
> with "see ... below" may now live in a sibling file in this folder: `grep -n '^## ' convocatorias-service/docs/*.md` finds it.
> Spec numbers (`03`, `04` §1, ...) are files in `docs/specs/convocatorias/`.

## The handoff worker

The platform's first background execution, and it has an ADR because of it (`docs/adr/0018`).
`Core/Campaigns/ICampaignHandoffQueue` is an in-process `Channel<Guid>`; `CampaignHandoffService`
is what happens to one campaign; `Web/Handoff/CampaignHandoffWorker` is the `BackgroundService`
that joins them. Five things are worth knowing before touching any of it:

- **`Enqueue` takes a `Guid` and nothing else, and that signature is the feature.** A
  `CancellationToken` parameter on a Minimal API handler binds to `HttpContext.RequestAborted`, so
  a token handed down from the launch endpoint would kill the batch loop the moment the browser tab
  closes. Twelve handlers in `ServiceSupplierEndpoints.cs` take that parameter, which is where the
  habit comes from. The worker runs on its `stoppingToken`, which is the host's shutdown token.
  A unit test pins the signature and an integration test holds the transport until after the `202`
  has been read, which is the moment `RequestAborted` fires.
- **It is not a polling loop and there is no startup sweep.** `ReadAllAsync` blocks on an empty
  channel, so the worker consumes nothing until somebody launches. A sweep over the database was
  considered and rejected: it fires on every deploy and would cheerfully resume a campaign whose
  event was cancelled an hour earlier.
- **Durability is deliberately not improved.** The channel is in memory, so a redeploy mid-send
  strands the campaign in `sending` with rows on both sides. That was already true of the
  synchronous path. `resume-handoff` (`04` §5) is what closes it and `strandedCampaigns` (`07` §6)
  is what puts it in front of a person. Do not reach for a durable queue here.
- **What moves a campaign to `sent` is that no row is left `pending`**, and nothing else, so a list
  whose remainder was suppressed lands correctly without anyone counting. Both that condition and
  "still `sending`" travel **inside** the `UPDATE`, rather than being read and then written: a
  cancel committing in that window would otherwise be silently overwritten, and unlike the launch
  there is no human reading a status code to catch it.
- **The state is re-read between batches**, not once at the start. A 3,000-invitee list is three
  calls, so a cancel landing after the first batch has to stop the other two: that is what
  allowing cancel during `sending` is for.
- **A failed batch records nothing.** Its rows stay `pending` and the campaign stays `sending`,
  which is what lets a resume walk the unsent tail instead of mailing the first four hundred twice.
  A per-recipient rejection is the other thing entirely: it travels inside an accepted batch and is
  terminal for that person.

## What the worker assembles, and what it deliberately does not

`CampaignHandoffService` builds the whole `SendBatchRequest` and hands it over. Four rules govern
what goes in it:

- **It assembles, it does not substitute.** The subject and the body travel with their placeholders
  intact, the event's three values travel once in `sharedData`, and each person carries only
  `nombre`, `enlace_registro` and `enlace_baja`. Splitting rather than merging is what keeps the
  event name off the wire a thousand times, and substitution is the receiving side's job
  (`04` §2). `ISubstitutionEngine` is **not** on this path. A key in both bags is a `400` on the
  other side, never a precedence rule, so nothing here may put one in two places.
- **The event's values come off the campaign row, never from events-service.** They are stamped
  inside the launch request, which is the only place they can be read. Re-reading them on the
  worker would turn an events outage into a campaign stuck in `sending`, and would let a resume a
  week later render text that differs from the first email the same campaign already sent. This
  stores names owned by another service, the same exception `campaign_recipient` already takes for
  the CRM name and address: a snapshot on the send path is not a display lookup, because an email
  has to keep saying what it said. The three columns travel together, enforced by a check.
- **The two tokens are minted here, and their hashes are written in the same transaction that
  records the outcome.** Writing them before the gateway call looks safer and is not: a refused
  batch would leave its whole chunk `pending` holding hashes whose plaintext is gone, and since
  re-minting over a hash is forbidden — it would kill the link in an email that did go out — those
  people could never be mailed at all. A refused batch is the common resume case. With the write
  where it is, a `pending` row simply has no hash, and the next run mints fresh. The window this
  accepts is small and stated: the gateway accepted and the write then failed, so that email's link
  is dead and the same rows come back `pending` for a second, working one.
- **So a `pending` row that already holds a hash means one thing**: that write landed only partly,
  and its email went out with a working link. It reaches no gateway a second time, is recorded
  `handed_off`, and keeps its hash.

The plaintext token never touches the database, and nothing retains the per-recipient bag: a
link is a live credential (`06` §2.1). `Portal__BaseUrl` is where the links hang from, validated on
start because a malformed one fails nowhere — the email goes out and the symptom is a person
clicking a dead link days later.

## The send transport

Two implementations behind one setting, `Notifications__Transport`
(`docs/specs/convocatorias/04-send-and-handoff.md` §4): `stub` binds `StubCampaignSendGateway` and
its table, `http` binds `HttpCampaignSendGateway` through `AddServiceClient`. Both exist.

- **The setting has no default and absence crash-loops the service.** `CampaignSendTransport.Require`
  throws on absent, blank or unknown, and it runs inside `AddDomainServices`, so the failure is a
  container that will not start. Binding the stub when the notification base URL is merely absent was
  rejected as fail-open — the reasoning is on `CampaignSendTransport` and is not repeated here. The
  key sits in **both** compose files so `scripts/compose-parity.sh` passes; the base file carries a
  dev default and the deploy file is fail-fast, which is each file's own convention.
- **`StubCampaignSendGateway` is a real implementation, not a mock.** It is the side that
  substitutes, because §2 puts substitution on the receiving side, and it reuses the same
  `ISubstitutionEngine` that guards the seeded copy: both sides speak one flat, literal,
  HTML-escaped dialect on purpose. It writes one `stub_notification` row per recipient with the
  rendered body and answers the full per-recipient shape of §2.1.
- **It rejects exactly one thing, a malformed address**, which §2.1 says is the only rejection
  available at accept time. That is why `failed` is near-empty, and why the shape check lives in
  `Core/Shared/EmailAddressShape` shared with the upload classifier rather than copied.
- **`stub_notification` is tied to nothing.** No foreign key in either direction and no campaign id;
  what it holds is what a notification-service history row would hold. A test asserts that over
  `pg_constraint`, because a temporary table quietly becoming schema is how it stops being
  temporary. Its migration runs in **every** environment and the table stays empty where the
  transport is `http`: a migration branching on environment would break the
  single-schema-authority rule to avoid one dead table.
- **While the transport is `stub`, the database holds plaintext registration tokens** beside their
  own hashes, because a rendered body embeds `{{enlace_registro}}` and `06` stores only the hash.
  Accepted with its exit pinned in §4.2: a redacted body cannot be clicked, so it would destroy both
  the demo and the only end-to-end test of the registration link; stage carries no real invitee
  data; and stage flips to `http` the day notification-service ships. The migration that lands the
  real client drops the table, the stub class and the setting with it.
- **`HttpCampaignSendGateway` pairs the response by position and checks it.** §2.1 answers per
  recipient keyed by the **address**, and the address is not our identity — two rows can carry the
  same one. So `results[i]` pairs with `recipients[i]`, and a response of a different length or with
  addresses that do not line up fails the batch whole: rows stay `pending` and a person resumes,
  which beats guessing which of two identical addresses a result meant.
- **`idempotencyKey` names the send and is never random** (notifications spec `01` §3). The three
  shapes live in `Core/Notifications/NotificationIdempotencyKey`. An invite batch is the campaign
  plus a SHA-256 of its recipients **with their freshly minted tokens**: the chassis retry resends
  the same bytes and reuses the key, while resume re-mints both tokens, which makes it a different
  send with a different body, so it gets a different key. Campaign plus batch number would be the
  same key with another body, a `409` on every resume. A confirmation is the registration plus the
  moment it was requested, because re-registering resends an identical body on purpose and a
  registration-only key would be replayed without mailing. A cancellation notice is the event plus
  the registration, one per person.
- **With `http`, `Notifications__FromAddress` is required at startup** (`NotificationSettings`,
  `ValidateOnStart`); `Notifications__FromName` is optional. notification-service answers `400` to a
  send with no sender, so a missing value would accept campaigns and mail nobody. `stub` ignores both.
- **A campaign goes out from its OPT's sender email when there is one (PGTI-168).** The campaign's
  OPT is the launcher's `opt_code`. The launch looks it up in crm `GET /v1/opts` (live offices,
  exact code) and copies `senderEmail` onto `campaign.sender_email`, so resume and retry sign with the
  same address and the worker never calls the CRM. No `opt_code` (central_admin), an office the CRM
  no longer lists, or one without a sender email all leave the column null, and the batch goes out
  from `Notifications__FromAddress`. The CRM down at that step is the launch's usual
  `503 CRM_UNAVAILABLE`. `FromName`, confirmations and cancellation notices are untouched.
- **The cap and the duplicate-key rule are one guard, `NotificationBatchContract`, run by both
  transports.** They are the receiver's rules, so what stands for the receiver enforces them, and a
  stub more permissive than the real service would stop stage being a rehearsal for production.
  `MaxRecipients` is also what the handoff service chunks with: the number that splits and the
  number that refuses cannot be two constants.
- **In tests the default is a transport that never answers** (`StalledSendGateway`, wired by
  `ConvocatoriasApiDbFactory.ConfigureSendTransport`). Delivery runs on a worker, so any class
  asserting on the state just after the `202` would otherwise be racing it.
  `CampaignHandoffFactory` overrides that hook with its recording double,
  `StubNotificationFactory` overrides it with nothing so the production stub is what runs, and
  `HttpNotificationFactory` does the same after flipping the setting to `http`, so the real client
  talks to `NotificationStub` over HTTP.

## Resume and retry

`POST /campaigns/{id}/resume-handoff` and `POST /campaigns/{id}/retry-failed`, both
`campaigns:write`, both `202`, both enqueueing onto the same worker (`04` §5, `03` §7).

- **Nothing in this service retries on its own, by decision.** These two routes are the only way out
  of a half-done send, and `strandedCampaigns` (`07` §6.3) is the screen that will put one in front
  of a person.
- **`resume-handoff` writes nothing.** The unsent tail already *is* what sits in `pending`, and
  `ReadHandoffAsync` only returns those rows, so resuming is re-enqueuing the id and no more. That
  is what makes pressing it twice harmless and why this service does not depend on the receiver's
  idempotency in the common case. `sending` only; anything else is `409 CAMPAIGN_NOT_SENDING`.
- **`retry-failed` moves `failed` rows back to `pending`** and is allowed on a campaign that is
  `sending` **or `sent`** — and `sent` is its normal case, because what closes a campaign is having
  no `pending` row left, so a campaign carrying rejections is already closed. **It does not reopen
  the campaign**: `sent` stays terminal, the `03` §2 transition table gains no edge, and what changed
  is that the worker delivers a campaign that is `sending` or `sent`
  (`CampaignHandoff.IsDeliverable`) and `CompleteHandoffAsync` closes from either. A `failed` row
  holds no token hash, so it is mintable again and no live link is broken.
- **The reopening is a repository operation, not a worker one.** The worker's queue is `pending` and
  nothing else; teaching it a second notion of "what to send" is how the resume cursor stops being
  one thing.
