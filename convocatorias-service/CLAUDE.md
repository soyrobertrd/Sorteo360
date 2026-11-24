# convocatorias-service — Service Instructions

Campaigns, the template catalogue, recipient uploads, the public registration portal and the
tracking reads, in the `convocatorias` database; `http://convocatorias-service:8080` inside
Compose, `CONVOCATORIAS_PORT` (5185) from the host. `docs/dotnet-service-conventions.md` applies
in full and `shared/CLAUDE.md` describes the chassis; this file keeps only the rules specific to
this service. Each rule ends with the file that explains it. Doc paths are from the repo root;
`Core/`, `Infrastructure/`, `Web/` and `Tests.*` mean `convocatorias-service/Sorteo360.Convocatorias.<Layer>/`.

```bash
dotnet build convocatorias-service/Sorteo360.Convocatorias.slnx
dotnet test  convocatorias-service/Sorteo360.Convocatorias.slnx
```

## Need X, read Y

| Need                                                                               | Read                                              |
| ---------------------------------------------------------------------------------- | ------------------------------------------------- |
| What is built, the layer layout, adding a model, endpoint or role                  | `convocatorias-service/docs/overview.md`          |
| Templates, the campaign model and launch order, direct messages, recipient uploads | `convocatorias-service/docs/campaigns.md`         |
| Public events, the registration portal and window, confirmation, cancellation      | `convocatorias-service/docs/registration-portal.md` |
| The handoff worker, the batch it assembles, the send transport, resume and retry   | `convocatorias-service/docs/sending.md`           |
| Participants, the tracking rollup, contact history, delivery status, reconciliation | `convocatorias-service/docs/tracking.md`          |
| The door: the scan, its idempotency, the undo, the door list and check-in by id    | `convocatorias-service/docs/check-in.md`          |
| The supplier intake: office links, the public resolve, the daily cap              | `convocatorias-service/docs/supplier-intake.md`   |
| The integration-test fixtures and neighbour stubs                                  | `convocatorias-service/docs/testing.md`           |
| Every app role: what it does, which job and service accounts hold it               | `docs/roles.md`                                   |
| What a term means                                                                  | `convocatorias-service/GLOSSARY.md`               |
| What was asked (the `02`, `04` §1 cited in the docs are these files)                   | `docs/specs/convocatorias/`; the queue is `docs/specs/convocatorias/BUILD-ORDER.md` |

## Structure and growth

- `events-service` is the fully-built sibling to copy patterns from. details: `convocatorias-service/docs/overview.md`
- Domain-shaped code stays here: error catalogs in `Core/Errors/`, service filters in `Web/Filters/`, and only `DomainServicesConfiguration` in `Web/Configuration/`. details: `convocatorias-service/docs/overview.md`
- Roles, policies and the fallback policy live in `Web/Auth/AuthorizationPolicies.cs`; role names are domain and never move to the chassis. details: `convocatorias-service/docs/overview.md`
- A new endpoint goes under one of the two prefixes in `Web/Routing/ApiRoutes.cs`; `PublicSurfaceGuardTests` fails on `.AllowAnonymous()` outside `v{version:apiVersion}/public`. details: `convocatorias-service/docs/overview.md`
- The only anonymous routes outside the public prefix are `/_alive`, `/_ready`, `/_health`, `/scalar` and `/openapi/v1.json`; the network, not a role, keeps the docs off the internet. details: `convocatorias-service/docs/overview.md`
- A table names its schema: `ToTable("<table>", ConvocatoriasDbContext.SchemaName)`. details: `convocatorias-service/docs/overview.md`
- Deleting a role takes the same two realm edits as adding one (realm JSON plus console step), plus a delete step in `docs/deploy-with-dokploy.md`. details: `convocatorias-service/docs/overview.md`
- `OPT_NOT_ASSIGNED` is the only `ErrorKind.Forbidden` here. details: `convocatorias-service/docs/overview.md`

## Templates

- Seed with `migrationBuilder.InsertData`, never EF `HasData`, which would revert staff edits; `TemplateSeedIsNotModelManagedTests` fails otherwise. details: `convocatorias-service/docs/campaigns.md`
- A template row added after `AddTemplates` is inserted with `ON CONFLICT DO NOTHING` through `migrationBuilder.Sql` (`AddDirectMessage` is the example). details: `convocatorias-service/docs/campaigns.md`
- `Core/Templates/TemplateSeed.cs` is frozen: a copy change is a `TemplateSeed` edit, a new data migration, and the fingerprint hash in `TemplateSeedTests` updated in the same PR. details: `convocatorias-service/docs/campaigns.md`
- Placeholders are derived from the body, never entered; the supported set is `Core/Rendering/SupportedPlaceholder.cs`. A `campaign` body must carry `{{enlace_baja}}`, a `transactional` one must not. details: `convocatorias-service/docs/campaigns.md`
- Create, PATCH and launch refuse a `(templateKey, language)` pair with no row: `400 CAMPAIGN_TEMPLATE_NOT_FOUND`. details: `convocatorias-service/docs/campaigns.md`
- Substitution is flat, literal and HTML-escaped, with no conditionals, loops or raw escape: a contract shared with notification-service. details: `convocatorias-service/docs/campaigns.md`

## Campaigns

- Nobody approves a campaign (`docs/adr/0017`): no approval table, gate, state or role, and tests assert each absence. details: `convocatorias-service/docs/campaigns.md`
- `CampaignState` and `RecipientSendStatus` never derive from each other; `registered`, `opened`, `clicked`, `delivered` are never send statuses. The transition table lives in `CampaignState`. details: `convocatorias-service/docs/campaigns.md`
- State guards run inside the writing transaction in `CampaignRepository`, never in the endpoint. details: `convocatorias-service/docs/campaigns.md`
- No history table: `launched_by` / `launched_at` are the launch audit, and `sent_at` is stamped by the worker, never by the launch request. details: `convocatorias-service/docs/campaigns.md`
- Launch order is fixed and is the feature (`CampaignLaunchService`): validate, flush unsubscribes, mailability re-check, read the OPT sender email, read the event, read the registration window, flip. Tests pin the order. details: `convocatorias-service/docs/campaigns.md`
- The unsubscribe flush is service-wide and stamps in one write only if the whole flush landed; CRM's `404` on opt-out counts as landed. details: `convocatorias-service/docs/campaigns.md`
- Per-recipient suppression is not an error and carries no code; zero mailable is `CAMPAIGN_HAS_NO_MAILABLE_RECIPIENTS`, counted before anything is written. details: `convocatorias-service/docs/campaigns.md`
- CRM unreachable at selection or launch is `503 CRM_UNAVAILABLE` and writes nothing: a deliberate exception to the ADR-0013 ladder (`docs/adr/0017`). details: `convocatorias-service/docs/campaigns.md`
- Never copy CRM's suppression rule here: store the reason CRM sends, falling back to `contact_unavailable`. details: `convocatorias-service/docs/campaigns.md`
- Selection is additive and idempotent; an exclusion row is deleted when CRM now says the contact is mailable. details: `convocatorias-service/docs/campaigns.md`
- The CRM pager fails rather than truncates: an empty body or a full page at the ceiling is `503`. details: `convocatorias-service/docs/campaigns.md`
- Every paged neighbour read goes through `Infrastructure/Neighbours/NeighbourPageReader` and every neighbour call through `NeighbourCall`; never copy the loop or the `catch`. details: `convocatorias-service/docs/campaigns.md`
- No event if and only if the template is `direct_message`, which then sets `subject` and `message` (`Core/Campaigns/CampaignDirectMessage`, `400 CAMPAIGN_DIRECT_MESSAGE_MISMATCH`); its launch skips the event and window reads. details: `convocatorias-service/docs/campaigns.md`
- `direct_message` is a seeded `campaign` template, `es` only, and never selectable by hand. details: `convocatorias-service/docs/campaigns.md`
- `contactIds` (1 to 1,000) never travels beside a filter, and `contactListId` travels alone. The OPT check on a contact list is ours, in `Core/Crm/ContactListReader`, because this service's token sees every OPT. details: `convocatorias-service/docs/campaigns.md`

## Recipient uploads

- The column mapping is provisional and lives only in `Core/Uploads/UploadColumnMap.cs`. details: `convocatorias-service/docs/campaigns.md`
- The batch row is permanent and its rows are deleted on confirm; no TTL and no cleanup job. details: `convocatorias-service/docs/campaigns.md`
- Six buckets, in order, first match wins, held by a database CHECK; a row-level failure never blocks an upload. details: `convocatorias-service/docs/campaigns.md`
- A row needs both name columns; never invent a placeholder name for CRM. details: `convocatorias-service/docs/campaigns.md`
- CRM normalises email, we do not: in-file dedupe is trim plus lowercase only. details: `convocatorias-service/docs/campaigns.md`
- Confirmation is synchronous, resumable and idempotent per row; a CRM `409` is a match, and a partial confirmation is never rolled back. details: `convocatorias-service/docs/campaigns.md`
- The limits are code in `Core/Uploads/SpreadsheetUploadLimits.cs`; raising the row cap needs a `proxy_read_timeout` on confirm in `frontend/nginx.conf` and `frontend/vite.config.ts`. details: `convocatorias-service/docs/campaigns.md`

## Public events and the registration portal

- Event reads have no degradation ladder and no cache: events-service down is `503 EVENTS_UNAVAILABLE`. details: `convocatorias-service/docs/registration-portal.md`
- The public routes republish a narrowed `EventSummary`, never proxy events-service. details: `convocatorias-service/docs/registration-portal.md`
- Unknown, private, cancelled and malformed all answer `404 PUBLIC_EVENT_NOT_FOUND`; a `closed` event still serves its detail. details: `convocatorias-service/docs/registration-portal.md`
- The visibility and status filter runs twice, the second time in `EventDirectory`, after the paging. details: `convocatorias-service/docs/registration-portal.md`
- `registrationOpen` is the one public field not from events-service; the registration routes leave it null, and a null is not serialized. details: `convocatorias-service/docs/registration-portal.md`
- `EventPlaceholderValues` returns empty for a value it cannot fill, never plausible filler. details: `convocatorias-service/docs/registration-portal.md`
- Launch reads the event after the CRM steps and checks all three event placeholders (`400 TEMPLATE_EVENT_VALUES_MISSING`); cancelled is `409 CAMPAIGN_EVENT_CANCELLED`. details: `convocatorias-service/docs/registration-portal.md`
- Both portal routes are `POST` with the token in the body, never in a URL; `Resolve_Returns405_WhenCalledOverGet` guards it. details: `convocatorias-service/docs/registration-portal.md`
- A private event's token admits one registration and prefills; a public one admits many and prefills nothing. details: `convocatorias-service/docs/registration-portal.md`
- Every portal refusal is `404 PUBLIC_REGISTRATION_NOT_FOUND`, except a shut window, `409 REGISTRATIONS_CLOSED`. details: `convocatorias-service/docs/registration-portal.md`
- A repeat submit is a `200` with the existing registration; uniqueness is the `(event_id, email_normalized)` constraint, caught by name in `RegistrationRepository`. details: `convocatorias-service/docs/registration-portal.md`
- The token is optional on submit and there is one endpoint, not two: no token registers on a public event and is refused on a private one. details: `convocatorias-service/docs/registration-portal.md`
- The spent-token check runs after the existing-registration read. details: `convocatorias-service/docs/registration-portal.md`
- The `reference` number is one PostgreSQL sequence, not one per year. details: `convocatorias-service/docs/registration-portal.md`
- `UsePublicRateLimitPartitions` runs before `UseRateLimiter`. details: `convocatorias-service/docs/registration-portal.md`
- Registration creates the CRM contact and never overwrites one; a `409` is the taken-address path, and a name mismatch only sets `contact_details_differ` (`Core/Shared/PersonNameShape`). details: `convocatorias-service/docs/registration-portal.md`
- A phone goes to the CRM only in its shape (`Core/Shared/PhoneShape`, a copy of crm-service's `PhoneRules`): the portal answers a field 400, `CrmContactDirectory` drops it from an upload. details: `convocatorias-service/docs/registration-portal.md`
- CRM down never fails a registration: the row persists with `crm_sync_failed_at`, no enum. CRM is called before the row is written, and a repeat submit asks CRM nothing. details: `convocatorias-service/docs/registration-portal.md`
- Never name the registration window a bare `Closed` or `Close`. Absence means open, idempotency is decided inside the SQL statement, and launch refuses with `409 CAMPAIGN_REGISTRATIONS_CLOSED`. details: `convocatorias-service/docs/registration-portal.md`
- The window is read after the event read and the spent-token check, so those still hide behind the shared `404`. details: `convocatorias-service/docs/registration-portal.md`
- The confirmation email is a notification, not a campaign: sent from the endpoint, not `RegisterAsync`; its failure is logged and swallowed; a suppressed person still gets it; the sender never substitutes, the receiver does. details: `convocatorias-service/docs/registration-portal.md`
- The cancellation notice needs a `cancelled` event (`409 EVENT_NOT_CANCELLED`), skips suppression, claims each row before its send and releases it on failure, and its endpoint takes no `CancellationToken`. details: `convocatorias-service/docs/registration-portal.md`

## Sending

- `ICampaignHandoffQueue.Enqueue` takes a `Guid` and nothing else; never hand a request's `CancellationToken` to the worker. details: `convocatorias-service/docs/sending.md`
- No polling loop, no startup sweep and no durable queue (`docs/adr/0018`). details: `convocatorias-service/docs/sending.md`
- A campaign is `sent` when no row is `pending`; that condition and "still `sending`" travel inside the `UPDATE`. details: `convocatorias-service/docs/sending.md`
- The worker re-reads the campaign state between batches. details: `convocatorias-service/docs/sending.md`
- A failed batch records nothing; a per-recipient rejection is terminal for that person. details: `convocatorias-service/docs/sending.md`
- The worker assembles and never substitutes (`ISubstitutionEngine` is not on the path), and never puts one key in both `sharedData` and a recipient's data. details: `convocatorias-service/docs/sending.md`
- The event's values come off the campaign row, never from events-service. details: `convocatorias-service/docs/sending.md`
- Tokens are minted in the worker and their hashes written in the same transaction as the outcome; never re-mint over a hash; plaintext never reaches the database. details: `convocatorias-service/docs/sending.md`
- `Portal__BaseUrl` is validated on start. details: `convocatorias-service/docs/sending.md`
- `Notifications__Transport` has no default and absence crash-loops; the key sits in both compose files. With `http`, `Notifications__FromAddress` is required. details: `convocatorias-service/docs/sending.md`
- `StubCampaignSendGateway` is a real implementation that substitutes and rejects only a malformed address (`Core/Shared/EmailAddressShape`). details: `convocatorias-service/docs/sending.md`
- `stub_notification` has no foreign key either way, and its migration runs in every environment. details: `convocatorias-service/docs/sending.md`
- `HttpCampaignSendGateway` pairs results by position; a length or address mismatch fails the whole batch. details: `convocatorias-service/docs/sending.md`
- `idempotencyKey` is never random; its shapes live in `Core/Notifications/NotificationIdempotencyKey`. details: `convocatorias-service/docs/sending.md`
- The OPT sender email is copied onto `campaign.sender_email` at launch, so the worker never calls CRM. details: `convocatorias-service/docs/sending.md`
- `NotificationBatchContract` is the one guard for the cap and duplicate keys, on both transports, and `MaxRecipients` is the chunk size. details: `convocatorias-service/docs/sending.md`
- Nothing retries on its own. `resume-handoff` writes nothing and needs `sending`; `retry-failed` moves `failed` to `pending` on `sending` or `sent` and never reopens a campaign. details: `convocatorias-service/docs/sending.md`
- The worker delivers a campaign that is `sending` or `sent` (`CampaignHandoff.IsDeliverable`); moving `failed` rows back is a repository operation, never a worker one. details: `convocatorias-service/docs/sending.md`

## Tracking and reconciliation

- The merge is `Core/Participants/ParticipantMerge`; the rollup and the CSV call `IParticipantService.ListAsync` and never merge again. details: `convocatorias-service/docs/tracking.md`
- Drafts appear on the participants list only under `?campaignId=`, and then `sendStatus` is that campaign's alone. details: `convocatorias-service/docs/tracking.md`
- A participant's key is the trimmed lowercased email and nothing else. details: `convocatorias-service/docs/tracking.md`
- `.../campaigns/{campaignId}/recipients` answers the same body as `?campaignId=` on the participants list; a test compares the two. details: `convocatorias-service/docs/tracking.md`
- Tracking reads never call events-service: the CSV filename carries the event id, and an unknown event is `200` with zeros. details: `convocatorias-service/docs/tracking.md`
- The rollup's window is `{ open, closedAt, closedById }`, with no `reopenedAt`: a test fails any tracking or reconciliation body containing `opened`. details: `convocatorias-service/docs/tracking.md`
- The campaigns a contact received are launched campaigns only (`LaunchedCampaign.Is`). details: `convocatorias-service/docs/tracking.md`
- A check-in counts for the registration's own `crm_contact_id`; contact-history reads take 1 to 100 ids, sharing `ListContactCheckInsRequestValidator.MaxContactIds`. details: `convocatorias-service/docs/tracking.md`
- Delivery status is read live from notification-service and degrades to `sent`; its 3-second budget is a caller cancellation so it cannot open the shared circuit. A null status is left out of the JSON, not sent as `null`. details: `convocatorias-service/docs/tracking.md`
- Each reconciliation bucket is its own route; acknowledging stamps and never clears `contact_details_differ`. details: `convocatorias-service/docs/tracking.md`
- Stranded is deliberately dumb (`Reconciliation__StrandedCampaignAfter`, no lease, no heartbeat), and `uncancelledNotices` reads events-service when the screen opens, never on a timer. details: `convocatorias-service/docs/tracking.md`

## Check-in

- The scan's idempotency is the `WHERE checked_in_at IS NULL` inside the `UPDATE`, never a read-then-write; the undo's `UPDATE` names the stamp it read. details: `convocatorias-service/docs/check-in.md`
- The scan never calls events-service and never checks the Event's state. details: `convocatorias-service/docs/check-in.md`
- `qrCode` is `400` only when empty or over 256 characters; anything else unmatched is `404 CHECK_IN_CODE_NOT_FOUND`. details: `convocatorias-service/docs/check-in.md`
- `CHECK_IN_WRONG_EVENT` names nobody, and no scan, undo or door-list response carries an email; only the raffle's `check-in:service-read` list does. details: `convocatorias-service/docs/check-in.md`
- The raffle's list takes `origin` from `ParticipantOrigin.Of`, the participant merge's own rule; never a second copy, in SQL or elsewhere. details: `convocatorias-service/docs/check-in.md`
- The door list searches in memory: words AND'd over first name, last name and Reference, accent-insensitive; an email matches only whole and exact. details: `convocatorias-service/docs/check-in.md`
- An undo that cleared a CheckIn logs one Spanish line with both stamps' owners; there is no history table. details: `convocatorias-service/docs/check-in.md`

## Supplier intake

- The plaintext token is returned once, by create, and only `PortalToken.HashOf` is stored; the link is `PortalLink.SupplierIntake`, token in the `#t=` fragment. details: `convocatorias-service/docs/supplier-intake.md`
- One active link per office is the partial unique index `ux_supplier_intake_link_active_office`, caught by name (`409 SUPPLIER_INTAKE_LINK_ALREADY_ACTIVE`); rotation is deactivate then create, and deactivating keeps the first stamp. details: `convocatorias-service/docs/supplier-intake.md`
- Resolve reads the office and its `isActive && fromHr` staff live from CRM, no cache, sorted with the Spanish culture. details: `convocatorias-service/docs/supplier-intake.md`
- Unknown token, deactivated link, and an office CRM lost or closed are one `404 SUPPLIER_INTAKE_LINK_NOT_FOUND`; 300 sends since midnight UTC is `409 SUPPLIER_INTAKE_DAILY_CAP_REACHED`, never a new `429` kind. details: `convocatorias-service/docs/supplier-intake.md`
- The submit's order is fixed (`SupplierIntakeService.SubmitAsync`): link and cap, sender live from CRM, origin source by name, company match then create only if none, each contact then its affiliation, the bank account if sent, ServiceSupplier, the send row last; nothing is rolled back. details: `convocatorias-service/docs/supplier-intake.md`
- A resend fills gaps: a matched company is joined (its office kept), a repeat affiliation (`409 DB_CONFLICT`) is already there, a joined company with a live bank account keeps it, and Events gets only the categories the company lacks (`SupplierIntakeSupplier.Without`). details: `convocatorias-service/docs/supplier-intake.md`
- The confirm window's name and email checks open the link like resolve and never name a company or a contact; the name check is CRM's match without the website. details: `convocatorias-service/docs/supplier-intake.md`
- The submit's field rules are copies of the owners' validators (`WebsiteShape`, `ServiceSupplierCategories`, `PhoneShape`, `BankAccountShape`), keyed by camelCase body paths; change them with the owner. details: `convocatorias-service/docs/supplier-intake.md`
- Intake contacts are created with `hasConsent: false`, and a CRM `409` on an email links that contact without editing it. details: `convocatorias-service/docs/supplier-intake.md`

## Tests

- Substitute the neighbour with a WireMock stub on `NeighbourStub`, never the typed client. details: `convocatorias-service/docs/testing.md`
- `CrmStub` answers the shapes the real crm-service answers; never invent one. details: `convocatorias-service/docs/testing.md`
- `RegistrationPortalFactory` replaces the whole `ILoggerFactory`; never hang a global Serilog sink. details: `convocatorias-service/docs/testing.md`
- `EventsStub` serves the whole events-service response, and a class that takes it down gets its own factory. details: `convocatorias-service/docs/registration-portal.md`
- The default test transport is `StalledSendGateway`, which never answers. details: `convocatorias-service/docs/sending.md`
- A campaign that has to launch in a test convokes `EventsStub.EventId`, not a fresh `Guid`. details: `convocatorias-service/docs/registration-portal.md`
- `RecipientUploadCircuitTests` stays in its own class, because an open circuit would leak into its neighbours. details: `convocatorias-service/docs/campaigns.md`
