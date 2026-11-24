# Glossary Map

## Contexts

- [Platform](./GLOSSARY.md) — cross-service concepts and staff portal navigation switches.
- [Events](./events-service/GLOSSARY.md) — events lifecycle, raffle setup, participant imports,
  live prize draws, winner handovers, and notarial PDF records.
- [Convocatorias](./convocatorias-service/GLOSSARY.md) — invitation campaigns, public RSVP /
  registration portal, and QR door check-in.
- [Notifications](./notification-service/GLOSSARY.md) — outbound email transport gateway and send
  history. Holds zero domain copy of its own.

## Relationships

- **Events ↔ Convocatorias — door check-in & event summary** (`docs/specs/convocatorias/09-check-in.md` §7):
  A raffle reads checked-in attendees from `convocatorias-service` using `events-service`'s
  client-credentials token (`check-in:service-read`), plus accepts check-in/participant webhooks.
  Convocatorias reads basic event details from `events-service`.
- **Any service → Notifications — sending email** (`docs/adr/0020`):
  `notification-service` is a stateless SMTP gateway + audit log. Callers render templates and
  send HTML + `{{placeholders}}` with an idempotency key; a failed email never fails a business
  transaction (e.g., raffle completion or prize draw).
- **Frontend → Notifications — reading the send history** (`docs/adr/0023`):
  The SPA reads `GET /v1/notifications` and `GET /v1/notifications/batches/{id}` with the user's
  token holding `notifications:read`.
