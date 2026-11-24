# 03 — The drain worker, the SMTP leg, the guardrail, and the local dev loop

Everything after the `202`. This is where the mail actually leaves.

**Sending is out of band because the transport forces it.** A 1,000-recipient send is ~1,000
sequential SMTP transactions on a reused connection, and SendGrid publishes no throughput figure to
size that against. Inside a synchronous caller request that is not viable: unknown per-message
latency with no ceiling, and a mid-batch failure leaving the caller on an ambiguous timeout — the
exact idempotency case `01` §3 calls the hard one.

## 1. There is no stub adapter, and there never should have been one

**Mailpit is a real SMTP server.** The production path — MailKit, `MailboxAddress`, `DATA`, the
`X-SMTPAPI` header, one transaction per recipient — runs against it **unchanged**. The "switch to
real sending" is `Smtp__Host` saying `mailpit` or saying `smtp.sendgrid.net`, and no other line of
code or configuration knows the difference.

**An `ISmtpTransport` with a logging adapter behind it is rejected because it inverts the risk.** It
would leave connection, STARTTLS, AUTH and MIME assembly executing for the first time **in
production**, on the one run where nobody can watch them fail.

> **The only genuinely untested thing on the day SendGrid arrives should be SendGrid.**

## 2. The one place it does not run unchanged

Mailpit serves plaintext by default, and the SMTP research requires
`SecureSocketOptions.StartTls` — which **fails closed** — because SendGrid's docs are *silent* on
whether AUTH is refused on a plaintext session, and silence is not a guarantee.

> **Normative: `Smtp__RequireTls` (default `true`) selects `SecureSocketOptions.StartTls`; `false`
> selects `None`. Never `Auto`, never `None` by accident, never `StartTlsWhenAvailable`.** It is one
> `bool` in one factory, not a branch through the sending logic.

**Mailpit can do STARTTLS and AUTH** — `--smtp-tls-cert`, `--smtp-tls-key`,
`--smtp-require-starttls`, `--smtp-auth-accept-any` are all present — and that higher-fidelity
option is deliberately **not** taken. It costs a checked-in self-signed certificate plus a "trust
this bad certificate" callback, and **a certificate-validation escape hatch in the codebase is a
worse artifact than a plaintext dev sink**: the escape hatch is the kind of thing that survives into
production configuration, and the plaintext sink is not.

**The configuration section is `Smtp`, not `SendGrid`.** The documented fallback if MITUR's IT team
refuses to open an outbound port is the Web API v3 over 443, and a vendor-named section would have
to be renamed on the day we are already rewriting the transport. Full table in `00` §9.

## 3. The guardrail inspects the credential, not the hostname

> **Normative: when `Smtp__AllowExternalRelay` is `false` and `Smtp__Password` is non-empty, the
> service refuses to start.** Loud, fail-fast, one `if`, at startup, before a single row is drained.

**Why the credential and not the hostname.** A hostname check is a guess about where a name points:
DNS moves, a compose alias gets renamed, and `smtp.sendgrid.net` has twelve A records. A credential
check is not a guess — **the API key *is* the SMTP password**, so without it a send to SendGrid
cannot succeed by any route. The guard refuses the only configuration in which an accident is
physically possible.

**Why a flag and not `ASPNETCORE_ENVIRONMENT != Production`.** When IT opens 587 we will want
exactly one real smoke send **from stage**, to prove domain authentication and see a live
`250 Ok: queued as` token. An environment-name guard forces that send to be bought by relabelling
stage as Production — a lie that touches migrations, Scalar exposure and log levels. One explicit
variable buys it honestly, in a reviewed file, temporarily.

**An address allowlist was considered and demoted**, not forgotten. It is a domain check in a
service deliberately stripped of domain knowledge, and it fails in the wrong direction: *empty means
block everything* kills production the day someone forgets the variable. The guard is the sink.

## 4. Stage runs Mailpit permanently, and the firewall is not the reason

Stage has all four outbound SMTP ports dropped today, which makes a sink *necessary now*. **That is
not the argument, and lifting the firewall changes nothing.** The argument is that a stage run of
the cotización flow would mail Dirección a genuine-looking *"tu cotización fue rechazada."*

Two strings attached, both platform rules:

- **`MP_UI_AUTH="<user>:<password>"`** — measured: 401 without credentials, 200 with. Stage serves
  public HTTPS through Traefik, so an unauthenticated Mailpit would publish every email MITUR's
  stage ever sent, to the internet.
- **A named volume in `docker-compose.dokploy.yml`.** Mailpit stores messages in a SQLite file under
  `/data`. Without the volume the inbox lives in the container's writable layer and dies on
  redeploy **while the history rows still say `sent`** — precisely the failure the root `CLAUDE.md`
  warns about.

> **Normative: Mailpit is permanent.** It is the development and test sink forever and stage's
> guardrail forever. It is **not** scaffolding to be deleted once SendGrid works.

## 5. The worker

A `BackgroundService` draining `notification_recipient` rows that are not terminal. **It is the
monorepo's first** — verified: none exists in events-service, crm-service, convocatorias-service or
the chassis — so build the machinery once, here, in the service whose job it is.

**One reused connection, draining sequentially.** No parallelism and no throttling machinery: the
shape already protects us, because a SendGrid outage cannot fan out into a thousand parallel
retries. MailKit serialises on one client anyway.

> **Normative: take `SELECT … FOR UPDATE SKIP LOCKED` from day one.** Compose runs one container per
> service so a single drainer holds today, but a second replica would double-send every pending row.
> The clause costs one line now against a rewrite later.

**Suppression is checked before the socket opens** (§7), and a refusal is written as a history row
with `delivery_status = dropped` — **recorded, never silently dropped**.

**Per-recipient status is written as each transaction completes**, never once at the end of a batch:
an SMTP batch can fail part-way through and a single write at the end would lose the rows already
sent.

Log the `250 Ok: queued as <token>` string against the row in `smtp_queued_as`. It is the first
thing SendGrid support will ask for. It is **not** a contract field and **not** a proven join key.

### Per-message shape

- **One transaction per recipient.** No `cc`, no `bcc`. Over SMTP this is native behaviour.
- **`X-SMTPAPI` carries `unique_args`** holding the recipient's own opaque GUID and nothing else.
  The budget is 10,000 bytes, with a 1,000-character *line* limit that forces RFC 5322 folding.
  `unique_args` **cannot be erased once sent**, so no entity ids and no personal data go in it.
- **`List-Unsubscribe` is present for an external recipient and absent for an internal one.**

## 6. Retry: SMTP classifies it, we do not

> **Normative: `4xx` means try again later. `5xx` means never.** That rule is SMTP's own and is not
> ours to invent.

| Condition | Class |
| --- | --- |
| Connection refused, timeout, TLS failure, any `4xx` | Transient — retry |
| `550 no such mailbox` and every other `5xx` | Terminal — `failed`, with `last_error` |
| `535 auth failed` | Terminal **and loud** — it is our API key, not their mailbox |

**Give up after one hour** of exponential backoff, not a week. An alert about a quote decided
yesterday has negative value, and stale mail is worse than no mail.

**The caller never re-POSTs** — not on a 5xx, not on a timeout, not from a startup scan. The chassis
owns delivering the *request* (its two retries); this worker owns delivering the *mail*. The line is
the `202`, and maximum three HTTP attempts all collapse onto one persisted batch.

**The chassis Polly numbers are accepted unchanged and get no per-client override**: 10s attempt
timeout, 30s total, 2 retries, 200ms base delay, exponential with jitter, a circuit breaker over a
30s sample with a 15s break. One pipeline platform-wide. The reason not to raise the timeout is that
every retry is a duplicate-send risk, so the lever is the idempotency key, not a longer wait.

## 7. The suppression list

Fed by `unsubscribe` and `spamreport` events (`04`), it refuses a send **before opening the socket**.

**This is deliverability, not resolution, and the seam matters.** The pipe never decides who *should*
be told — that is the caller's. It refuses only addresses the provider has told **it** are dead or
hostile, because that answer arrives on its webhook and reaches nobody else.

Recording alone would function, since SendGrid auto-suppresses on their side. It is rejected because
the answer to *"why didn't Juan get this?"* would live in someone else's account, die on a provider
swap, and we would still burn a credit on every dropped send.

**A hard bounce does not sync back to CRM, and must not.** A person can be suppressed in CRM
(`opted_out`, a consent fact) and separately here (a bounce, a mailbox fact). Conflating them would
make a typo'd address look like a withdrawn consent, which is unrecoverable through the consent UI.

**Not readable on the history surface** (`05`): a suppression list is not a send history. Per-type
preferences, the GDPR-consent interaction and who may un-suppress are all open questions, not gaps
here.

## 8. The local dev loop

- **Mailpit renders real HTML in a real inbox UI**, so a demo shows the actual email rather than a
  log line. Nothing in the system is synthetic: the send really happened, over real SMTP.
- **Local stamps `tracking_expected = true`**, because the dev loop runs `04`'s signed fake-batch
  script, so rows walk the full ladder to `delivered` and the history screen demos complete.
- **Stage stamps `false`**, because nothing POSTs events there. Rows stop at `sent` and the screen
  shows *"delivery tracking not available in this environment."*

> **Normative: pointing the fake-batch script at stage is forbidden.** It would manufacture delivery
> confirmations for mail that was never delivered, written into the system of record and afterwards
> indistinguishable from real ones — the one lie this design must not tell, in the one table the
> client browses to find out whether their mail worked.

## 9. Tests — three layers, split by what each can prove

**1. Unit, no container.** The startup guard (`AllowExternalRelay=false` + a password ⇒ refuses to
start); the retry classification table in §6; the `one hour` bound; the suppression check.

**2. Integration, Postgres Testcontainer.** `FOR UPDATE SKIP LOCKED` under two concurrent drainers
produces no double-send; a terminal row is never picked up again; per-recipient status is durable
after a part-way batch failure.

**3. The SMTP leg, split in two on purpose.**

- **An in-process `FakeSmtpTransport`** drives the worker's rules. A fake is not a compromise here:
  it is the **only** way to produce a `535` on demand. Precedent: `CrmDirectoryStub`, `FileStoreStub`.
- **Exactly one end-to-end test against a Mailpit container**, reading the message back through
  Mailpit's HTTP API. It is the only thing in the suite that proves the **bytes**, which no fake
  can. Its read-back surface was **verified by running it** —
  `.scratch/notifications-map/prototype-mailpit-dev-loop.md` — which is how we know `Cc`, `Bcc` and
  `ListUnsubscribe` are first-class API fields and therefore directly assertable.

## Definition of done

- `Smtp__Host: mailpit` sends a real message that appears in the Mailpit UI, with **no code path
  differing from production except `Smtp__RequireTls`**.
- The startup guard refuses to start with `AllowExternalRelay=false` and a non-empty
  `Smtp__Password`, and a unit test asserts it.
- The worker drains sequentially over one reused connection, and two concurrent drainers against one
  queue send each row exactly once.
- `4xx` retries and `5xx` does not; `535` is terminal **and** logged at error; retries stop after one
  hour.
- A suppressed address produces a `notification_recipient` row with `delivery_status = dropped` —
  **not** a missing row and **not** a silent skip.
- The Mailpit end-to-end test asserts: one message per recipient; `Cc` and `Bcc` **empty**; both MIME
  parts present and distinct; the `X-SMTPAPI` `unique_args` header verbatim; `List-Unsubscribe`
  present for an external recipient and **absent** for an internal one.
- Stage's compose block has `MP_UI_AUTH` and a **named volume** for Mailpit, and `MP_UI_AUTH` is
  proven by a 401 without credentials.
- Mailpit carries **no Compose profile**, and a bare `docker compose up` starts it.
- All six `Smtp__*` variables are in all three compose files and `scripts/compose-parity.sh` passes.
