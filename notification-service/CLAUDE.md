# notification-service — Service Instructions

The platform's way out to email, in the `notifications` database; `http://notification-service:8080`
inside Compose, `NOTIFICATIONS_PORT` (5190) from the host. `docs/dotnet-service-conventions.md`
applies in full and `shared/CLAUDE.md` describes the chassis; this file keeps only the rules
specific to this service. Each rule ends with the file that explains it. Doc paths are from the
repo root; `Core/`, `Infrastructure/`, `Web/` and `Tests.*` mean
`notification-service/Sorteo360.Notifications.<Layer>/`.

```bash
dotnet build notification-service/Sorteo360.Notifications.slnx
dotnet test  notification-service/Sorteo360.Notifications.slnx
dotnet ef migrations add <Name> \
  --project notification-service/Sorteo360.Notifications.Infrastructure \
  --startup-project notification-service/Sorteo360.Notifications.Web \
  --output-dir Database/Migrations
```

## Need X, read Y

| Need                                                                                   | Read                                     |
| -------------------------------------------------------------------------------------- | ---------------------------------------- |
| What is built, the stack, layer layout, domain-shaped code, database, Mailpit           | `notification-service/docs/overview.md`  |
| Why it holds no copy, resolution vs. deliverability, the `202` line, `notifications:send` | `notification-service/docs/boundary.md`  |
| The drain worker, the SMTP seam, retry, the relay guard, `List-Unsubscribe`, rendering  | `notification-service/docs/sending.md`   |
| The SendGrid event webhook: its security surface, the ladder, ingest, the dev keypair   | `notification-service/docs/webhook.md`   |
| The history reads: read-only, who sees what, the tracking cap                           | `notification-service/docs/history.md`   |
| The test fixtures, the one Mailpit byte test, draining in tests                         | `notification-service/docs/testing.md`   |
| Every app role: what it does, which job and service accounts hold it                    | `docs/roles.md`                          |
| What a term means                                                                       | `notification-service/GLOSSARY.md`       |
| The boundary and its reasoning                                                          | `docs/adr/0020-notifications-live-in-a-standalone-service.md` |
| What was asked, and the build queue                                                     | `docs/specs/notifications/BUILD-ORDER.md` |

## The boundary

- When a vertical lands, add its paragraph to the matching `notification-service/docs/` file, and here only the rules it carries.
- No copy and no domain knowledge: no template table, no default body, no hard-coded placeholder names, no branch on `type`. A change that seems to need copy means the design has drifted; check ADR-0020 first. details: `notification-service/docs/boundary.md`
- `type` is a history label stored verbatim with no foreign key, so a retired type still reads. details: `notification-service/docs/boundary.md`
- It cannot send an email of its own, not even a failure alert; a failure is found by looking. details: `notification-service/docs/boundary.md`
- Resolution (who, which address) belongs to the caller; deliverability (can this address receive mail) belongs here. The pipe never decides who should be told. details: `notification-service/docs/boundary.md`
- It refuses only addresses the provider reported dead or hostile, and records the refusal as a history row, never dropped. details: `notification-service/docs/boundary.md`
- The pipe must not tell an internal recipient from an external one; both arrive as `{ email, name?, data? }`. details: `notification-service/docs/boundary.md`
- The `202` is the line: a malformed request is a `400` that stores nothing; a world failure (SMTP down, bounce, suppression) is a `202` stored with a status. details: `notification-service/docs/boundary.md`
- `notifications:send` is granted only to calling service accounts, never to a person and never to a composite; `RealmConfigurationTests` checks the file, not a live realm. details: `notification-service/docs/boundary.md`

## Structure

- This service calls nobody: no `ServiceAuth__*`, no `Services__*`, no confidential client in the realm. details: `notification-service/docs/overview.md`
- The chassis gains nothing for this service; everything it needs already ships there. details: `notification-service/docs/overview.md`
- Domain-shaped code stays here: `Core/Errors/`, `Web/GlobalEndpoints/`, `Web/Filters/`, `Web/Auth/`, and `DomainServicesConfiguration`, the only file in `Web/Configuration/`. details: `notification-service/docs/overview.md`
- Tables and columns are declared explicitly, `ToTable("<table>", NotificationsDbContext.SchemaName)` and `HasColumnName`, never left to EF conventions. details: `notification-service/docs/overview.md`
- A new entity: Core entity/service, `*Data` class plus `IEntityTypeConfiguration<T>` in `Infrastructure/Database/Configurations`, a `DbSet` on `NotificationsDbContext`, then a migration. details: `notification-service/docs/overview.md`
- A new role is a constant in `AuthorizationPolicies` (plus the realm twice, per the root rule). details: `notification-service/docs/overview.md`
- Mailpit is permanent (ADR-0020) and carries no Compose profile, because this service's `depends_on` gates on its health. details: `notification-service/docs/overview.md`
- The switch to real sending is `Smtp__Host`; the only other line that differs is `Smtp__RequireTls`. details: `notification-service/docs/overview.md`

## Sending

- `notification_payload` is the one place that holds a `data` value, deleted in the same transaction that makes the send terminal; none of the four record tables ever holds one. details: `notification-service/docs/sending.md`
- No stub adapter, ever: `ISmtpSessionFactory` has one real implementation; the only other is `FakeSmtpTransport`, in the tests. details: `notification-service/docs/sending.md`
- The worker is a thin loop (`Web/Workers/NotificationDrainWorker`); its rules live in `Core/Sending/NotificationDrainService`, which the tests drive. details: `notification-service/docs/sending.md`
- Claim with `FOR UPDATE SKIP LOCKED`, one row per transaction, and write status as each SMTP transaction finishes, never once per batch. details: `notification-service/docs/sending.md`
- Retry is SMTP's rule: `4xx` later, `5xx` never, `535` terminal and logged at error; everything stops after one hour. details: `notification-service/docs/sending.md`
- Nothing opens a socket until there is a message to send: the one-hour and suppression checks run first, and the session is opened by a callback passed into the attempt. details: `notification-service/docs/sending.md`
- A row the worker cannot classify is settled as a transient failure, never left untouched. details: `notification-service/docs/sending.md`
- `SmtpRelayGuard` (via `ValidateOnStart`) refuses to start with `Smtp__AllowExternalRelay` false and `Smtp__Password` non-empty; it checks the credential, not the hostname. details: `notification-service/docs/sending.md`
- `List-Unsubscribe` is present only when the recipient's `data` carries `enlace_baja`, the same key the body names. details: `notification-service/docs/sending.md`
- `Core/Rendering/` is pure: flat literal `{{key}}`, values HTML-escaped in the body but never in the subject; no conditionals, loops, helpers, `{{{raw}}}` or key list. details: `notification-service/docs/sending.md`
- The `text/plain` part is derived from the rendered HTML with MimeKit's HTML tokenizer. details: `notification-service/docs/sending.md`

## The webhook

- `POST /hooks/sendgrid/events` is the only anonymous inbound route from a third party: no Keycloak policy, ECDSA signature verified before the body is parsed. details: `notification-service/docs/webhook.md`
- `SendGridEventWebhookEndpoint.Receive` runs the 2 MB cap (on the raw-body read), the signature headers, the 10-minute replay window, the signature over the bytes, and only then the parse; parsing first breaks verification silently. details: `notification-service/docs/webhook.md`
- Any change to that surface goes to security review. details: `notification-service/docs/webhook.md`
- Its failures answer a bare `403`, `413` or `400` with no body and no machine `code` (the one exemption from the root rule); the reason goes to the log, the only endpoint that logs on its own. details: `notification-service/docs/webhook.md`
- Traefik publishes it with `Path`, never `PathPrefix`: a prefix lets `..` segments reach the anonymous doc surface. details: `notification-service/docs/webhook.md`
- Only `/_alive`, `/_ready`, `/_health` and the API docs are otherwise anonymous; `AnonymousRoutesTests` fails on a sixth, `FallbackPolicyProbeEndpoint` proves the fallback policy. details: `notification-service/docs/webhook.md`
- The webhook's rules are pure in `Core/Webhooks/`; the only Infrastructure piece is `NotificationEventIngest`: one transaction per batch, untargeted `ON CONFLICT DO NOTHING`, `FOR UPDATE` per recomputed row. details: `notification-service/docs/webhook.md`
- `delivery_status` (how far the mail got) and `recipient_action` (what the person did) never share a column; a `spamreport` sets `complained`. details: `notification-service/docs/webhook.md`
- Status is derived, never overwritten: `DeliveryLadder.Advance` takes a maximum, so a late `processed` cannot demote a `delivered`. details: `notification-service/docs/webhook.md`
- The dev keypair is generated, never committed (`.secrets/` is gitignored); `scripts/sendgrid-fake-events.sh` refuses any host but `localhost`. details: `notification-service/docs/webhook.md`

## The history

- The history is read-only: never add a POST, PUT, PATCH or DELETE under its routes; `HistoryIsReadOnlyTests` fails if you do. details: `notification-service/docs/history.md`
- `Core/History/HistoryScope` names no composite role; a person with `notifications:read` sees every `source_service`, a service account (`service-account-<clientId>`) only its own. details: `notification-service/docs/history.md`
- No OPT scoping: this service knows no `opt_code`. details: `notification-service/docs/history.md`
- The `tracking_expected` cap (`Core/History/DeliveryTracking`) is applied on read, never written; the reader applies it in SQL too, so a `deliveryStatus` filter matches the rows. details: `notification-service/docs/history.md`

## Tests

- The `BackgroundService` does not run under `IntegrationTesting`; a test calls `INotificationDrainService.DrainAsync` itself. details: `notification-service/docs/testing.md`
- The bytes are proved by exactly one Mailpit end-to-end test (`Sending/MailpitEndToEndTests`); the retry table runs on `FakeSmtpTransport`. details: `notification-service/docs/testing.md`
- The webhook test key is generated in memory per run (`WebhookSigningKey`). details: `notification-service/docs/testing.md`
- `SendNotificationTests` sweeps the four record tables for `data`; the worker tests sweep all five after the drain. details: `notification-service/docs/sending.md`
- Every test asserts; no empty shells (convocatorias' mistake). details: `notification-service/docs/testing.md`
