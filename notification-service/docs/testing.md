# notification-service: testing

> Moved from `notification-service/CLAUDE.md` (PGTI-215), text unchanged except headings. A section named
> with "see ... below" or "§..." may now live in a sibling file in this folder: `grep -n '^## ' notification-service/docs/*.md` finds it.

## Tests

`Tests.Integration/Infrastructure/` carries the fixtures every later spec asserts through:

| Fixture | Serves |
| --- | --- |
| `NotificationsApiFactory` | the host in `IntegrationTesting`, no real Postgres |
| `NotificationsApiDbFactory` | the same host against a Testcontainers `postgres:17-alpine`, schema applied through the real EF migrations |
| `TestAuthHandler` | a principal with a chosen set of roles, plus `X-Test-Anonymous` for the 401 cases and `X-Test-Username` for a service-account token |
| `FakeSmtpTransport` | an in-process SMTP leg that fails on demand — the only way to produce a `535` |
| `FakeSmtpSessionFactory` | plugs that transport into the worker and records what the worker built |
| `NotificationsDrainFactory` | the API + real Postgres + the fake SMTP leg, for the worker's rules |
| `MailpitApiFactory` | the API + real Postgres + a real Mailpit container, for the one byte test |
| `WebhookSigningKey` | an ECDSA P-256 pair generated **in memory** per run — the dev keypair is never committed |
| `NotificationsWebhookFactory` | the API + real Postgres + that key configured + the fake SMTP leg, for the webhook |

`FakeSmtpTransport` is a fake **on purpose**, not a compromise: Mailpit accepts everything, so the
worker's retry table cannot be driven against a real server. The **bytes** are proved by exactly
one end-to-end test against a Mailpit container (`Sending/MailpitEndToEndTests`), which is what no
fake can do — and it is one test, deliberately.

**The `BackgroundService` does not run under `IntegrationTesting`**, for the same reason migrations
do not: a test that wants to drain calls `INotificationDrainService.DrainAsync` itself and asserts
the result, instead of sleeping until a tick happens. It is also what lets the concurrency test
launch two drainers at the same instant on purpose.

> **`convocatorias-service`'s empty test shells are the named mistake not to repeat.** Every
> Definition of Done in `docs/specs/notifications/` lists assertions, not intentions.
