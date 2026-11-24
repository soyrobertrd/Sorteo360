# notification-service: sending: the drain worker, SMTP, rendering

> Moved from `notification-service/CLAUDE.md` (PGTI-215), text unchanged except headings. A section named
> with "see ... below" or "§..." may now live in a sibling file in this folder: `grep -n '^## ' notification-service/docs/*.md` finds it.

## Sending

Six tables now, not four. The four of the record, plus two the worker needs:

| Table | Holds | Lifetime |
| --- | --- | --- |
| `notification_payload` | one recipient's merged substitution values, as `jsonb` | **deleted in the same transaction that makes the send terminal** |
| `notification_suppression` | an address the provider reported dead or hostile | permanent; written by `04`'s webhook, read here |

> **`notification_payload` is the one place in this service that holds a `data` value.** The send
> happens after the `202`, so the worker has to substitute at send time, and the record
> deliberately stores no `data`. Keeping the values in their own short-lived row is what preserves
> the guarantee that matters: a registration token lives exactly as long as its own send, and none
> of the four record tables ever contains one. `SendNotificationTests` sweeps the four; the worker
> tests sweep all five **after** the drain.

**There is no stub adapter and there must not be one.** Mailpit is a real SMTP server, so the
production path — MailKit, `MailboxAddress`, `DATA`, `X-SMTPAPI`, one transaction per recipient —
runs against it unchanged. `ISmtpSessionFactory` is a composition seam with **one** implementation;
the only other one is `FakeSmtpTransport`, in the test project, because Mailpit accepts everything
and cannot produce a `535` on demand. A logging adapter behind the interface would leave connection,
STARTTLS, AUTH and MIME assembly running for the first time in production.

**Three rules the worker exists to keep:**

- **`FOR UPDATE SKIP LOCKED`, one row per transaction.** Compose runs one container per service so
  a single drainer holds today, but a second replica would double-send every pending row. Status is
  written as **each** SMTP transaction finishes, never once at the end of a batch — a batch can die
  part-way and one write at the end would lose what already went out.
- **Retry is SMTP's rule, not ours.** `4xx` later, `5xx` never, `535` terminal **and** logged at
  error because it is our API key and no retry fixes it. Everything stops after one hour: mail about
  a decision made yesterday has negative value.
- **Nothing opens a socket until there is a message to send.** The one-hour check and the
  suppression check both answer without a connection, so a batch of only-suppressed addresses
  connects to nothing at all. The session is opened by a callback passed into the attempt, not
  handed to it already open — that is what keeps the ordering honest rather than merely intended.
  A row the worker **cannot** classify is still settled as a transient failure: leaving it untouched
  would put it back at the head of the queue every five seconds for an hour, hiding every row
  behind it.

**The startup guard inspects the credential, not the hostname.** With `Smtp__AllowExternalRelay`
false and `Smtp__Password` non-empty the host refuses to start (`SmtpRelayGuard`, wired through
`ValidateOnStart`). The API key *is* the SMTP password, so without it a real send cannot succeed by
any route — a hostname check would be a guess about DNS, this is not a guess.

**`List-Unsubscribe` comes from `enlace_baja`.** The service cannot tell an internal recipient from
an external one and must not, so it reads the only thing it can see: the unsubscribe link the caller
put in that person's `data`. Present → header; absent → no header. Same key the body names as
`{{enlace_baja}}`, so the header and the visible link cannot drift apart.

## Rendering

- **The substitution engine is `Core/Rendering/`**, and it is pure: flat literal `{{key}}`,
  HTML-escaped values in the body (never in the subject, which is a header), and the `text/plain`
  alternative derived from the rendered HTML with MimeKit's HTML tokenizer. No conditionals, no
  loops, no helpers, no `{{{raw}}}`, no hard-coded key list — a placeholder this service has never
  seen renders like any other.
