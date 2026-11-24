# Visibility, Language and the events-service Contract — Spec

The cross-service change. `Event` gains a `visibility` field and its `language` becomes required,
convocatorias grows a typed events client, and a stranger gets a narrowed view of a public event
that never touches events-service.

Per the root `CLAUDE.md`, a cross-service contract change lands in **one PR** across every producer
and consumer. The demo `frontend/` is the one exception and catches up on its own card.

## Decisions behind this model

- **Visibility belongs on the event, not the campaign.** The client framed it as "two types of
  events", it governs more than convocatorias, and a campaign-level flag would let one event carry
  contradictory campaigns.
- **The language of a convocatoria is dictated by the event.** Not by the contact, not by the
  campaign. An event in Paris can be set to English and every convocatoria under it goes out in
  English.
- **There is no venue on `Event`.** Location is `RoadshowCity` rows. A venue is a `ServiceSupplier`
  reachable only through an approved candidate on the event's per-event `Venue` quote.
- **Reads on events-service carry no policy**, only the fallback requiring an authenticated
  principal. What convocatorias needs is an audience mapper, not a role.

## 1. `EventVisibility`

A `static class` of string constants with an `All` set, matching `EventType`, `EventStatus`,
`AllowedLanguage` and `ServiceSupplierCategory`:

```csharp
public static class EventVisibility
{
    public const string Public  = "public";
    public const string Private = "private";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Public, Private
    };
}
```

A boolean `IsPublic` was rejected: it breaks the pattern four other sets already follow and cannot
grow the third value that eventually shows up, unlisted-and-reachable-by-link.

- `Visibility` is **non-nullable** on `Event`, `EventData` and both response DTOs.
- **On create it is optional and defaults to `private`.** The column is non-nullable, the request
  field is not. Roberto's event form has no visibility control, so a required field would break his
  create the day this ships.
- **Every existing row is backfilled to `private`** in the same migration, with **no database
  default**, so a new row must state its value.

The trade this looks like it poses, disclosure against wrongly hiding an advertised event, is empty:
the portal does not exist yet, so no event in any database has ever been advertised through this
system. No hand-correction list. Being wrong costs one PATCH per event.

## 2. `Language` becomes required

It already exists: nullable string, closed set of six in `AllowedLanguage` (`es`, `en`, `fr`, `de`,
`pt`, `it`), validated at the edge and already on both responses. Two changes:

- **Non-nullable**, existing nulls backfilled to `es`, new events defaulting to `es`. `02`'s
  no-fallback template rule turns null into a launch-blocking value, and with only `es` authored on
  day one a database full of nulls makes every campaign unlaunchable until someone edits each event
  by hand. That reads as a bug.
- **`PATCH /v1/events/{id}` with `language: null` returns `400`** with a machine code, rather than
  ignoring the null and keeping the stored value. That endpoint is a replace and not a patch —
  `EventRepository` assigns `eventData.Language = dto.Language` unconditionally, so an omitted field
  clears the value today — and ignoring null would make one field behave unlike every other field on
  the same endpoint. **This is the one behaviour change reaching an existing client**, which is why
  the frontend card ships alongside.

**The value set stays at six.** The field means "what language this event runs in", which is true
independently of what we can email. A campaign for a `de` event fails at the launch gate with a code
naming the missing template key, which is the honest failure. Both frontend branches already carry
that exact union and Roberto's form already defaults a new event to `es`.

## 3. Neither field freezes

`events:write` may change visibility and language at any time, including after a campaign has sent.

Freezing would mean events-service asking convocatorias whether a campaign exists, which inverts the
dependency: convocatorias depends on events, never the reverse. It is not needed anyway, because
`03`'s state machine locks the template key and language at launch and snapshots addresses at
handoff.

Two effects, documented rather than prevented:

- **Public to private** makes the public page `404` while invitation tokens keep resolving. Token
  resolution is per registration and never consults the event's visibility.
- **A language change** affects only campaigns launched afterwards. A draft picks up the new language; a campaign already `sending` or `sent` keeps what it was launched with (`03` §5).

## 4. What convocatorias reads

**The existing endpoints, unchanged in shape.** No projection endpoint for this consumer.

| Read | Endpoint | For |
| ---- | -------- | --- |
| One event | `GET /v1/events/{id}` | the campaign email body, the portal detail |
| Filtered list | `GET /v1/events?visibility=public&active=true` | the portal listing |

`visibility` is added to `EventDetailResponse`, `EventListItemResponse` and `ListEventsRequest`,
alongside the existing `Type`, `Status`, `Active`, date range, `SearchTerm` and `OptOfficeId`
filters.

A second endpoint buys nothing: convocatorias maps into its own narrow DTO regardless, and two shapes
double what a schema change must keep in step. The accepted cost is that the detail response carries
`EstimatedBudget` and `TotalCost` over the wire to a consumer that never republishes them.

**Authentication** is this service's own client-credentials token through `AddServiceClient`, with an
`audiencia-events-api` mapper on the `convocatorias-service` client, mirroring the `audiencia-crm-api`
mapper events-service already has.

**The exposure this creates, stated rather than implied.** Because events-service reads carry no
policy, that token reads every event including budgets, quotes and suppliers, with no role at all.
Accepted for this PR. Adding an `events:read` role means touching every read endpoint plus every
human composite in the realm, which is a security decision about events-service and earns its own
ticket rather than riding along in a feature PR.

### 4.1 Where `{{lugar}}` comes from

**The event's `RoadshowCity` rows: name and country, joined for a multi-city roadshow.**

Deriving it from the approved candidate on the `Venue` quote was rejected. The quote is per event and
category with no city link, the venue is usually not approved when the save-the-date goes out, and an
email body that silently depends on a procurement decision is a trap.

Consequence to accept: one campaign per event means one email covering every city, and `{{lugar}}` in
that email is a list.

If the client wants the actual hall name, the additive fix is a nullable free-text `VenueName` on
`RoadshowCity`, which is where a venue belongs since a roadshow uses a different hall per city. Not
built now.

## 5. What a stranger sees

Convocatorias republishes a narrowed view at `GET v{version}/public/events` and
`GET v{version}/public/events/{id}`, both anonymous. **events-service is never reachable from the
public internet**, and it keeps its fallback policy with no exception to audit.

| Field | List | Detail |
| ----- | ---- | ------ |
| `id`, `name`, `type`, `startDate`, `endDate`, `language` | yes | yes |
| `registrationOpen` | yes | yes |
| `cities` (name, country, timeZone, date, order) | yes | yes |
| `externalUrl` | yes | yes |
| `description` | no | yes |
| OPT **name** only, no id | no | yes |

Everything else stays inside events-service on this path: `estimatedBudget`, `totalCost`,
`invitedCount`, `optContact`, `cancellationReason` and every audit field.

**`registrationOpen` is the one field on these responses that does not come from events-service.** It
is convocatorias' own registration window (`06` §5), left-joined onto each event, and it is here
rather than on `Event` because no events-service code would ever branch on it. A closed window does
not remove the event from either response; it only changes this field and the two write endpoints.

**Status narrows it further.** The public list serves `EventStatus.Active` only (`planning`,
`in_progress`). The public detail also serves `closed`, so a link shared last week still resolves
after the event ends. `cancelled` is a `404` on both, under one code that does not say why.

**Images are out of phase 1.** Their bytes live behind the authenticated `/v1/files/{id}/content`, so
publishing them means a public byte route with its own caching, sizing and abuse story. The portal
already falls back to the stock photos in `features/publico/coverImages.ts`.

### 5.1 Two fields Roberto's public pages read that must not survive

- **`invitedCount`** is rendered as venue capacity in the detail highlights and again as a stat. It
  is the OPT's internal planning target, not a capacity and not for strangers.
- **`mapEmbedUrl`** has never existed on the API. It is a frontend invention, the same shape as
  `registrationType` in `06`.

## 6. Degradation

Three reads, three audiences, three answers.

| Read | When events-service is unreachable |
| ---- | ---------------------------------- |
| Campaign launch (name, dates, cities, language, visibility) | Hard fail, `503 EVENTS_UNAVAILABLE`. No email goes out with a hole in it |
| Public list and detail | Hard fail, `503` to the visitor. **No cache in phase 1** — a cached public list is a cached visibility decision, which is how a private event stays visible for five minutes after someone hides it |
| The OPT name inside the event read | Genuine ADR-0013 display data. When `opt.name` is null with a degraded `X-Opt-Directory-Status`, the public detail **omits the organizer block** rather than rendering a placeholder. The page still serves |

## 7. events-service never reads campaign actuals

No `actualInvited` on the event read, and no call from events-service to convocatorias in any
direction, ever. `InvitedCount` stays the OPT's planned target; the sent count lives only on
convocatorias' own screens. A screen wanting both numbers side by side calls two APIs, which is a
frontend composition problem and not a service dependency.

## 8. The one-PR scope

1. **events-service** — `EventVisibility`; `Visibility` on `Event`, `EventData`, `CreateEventDto`,
   `UpdateEventDto`, both requests and both responses; `Language` non-nullable through the same
   chain; `visibility` on `ListEventsRequest`; validators for both; one migration adding the column,
   backfilling `private`, and backfilling `es` for null languages.
2. **convocatorias-service** — the typed events client through `AddServiceClient`,
   `Services__Events__BaseUrl`, the launch-time read with its `503 EVENTS_UNAVAILABLE`, the narrow
   public DTO, and `GET v{version}/public/events` plus `/{id}`.
3. **Realm, in both places** — the `audiencia-events-api` mapper on the `convocatorias-service`
   client in `infra/keycloak/pgti360-realm.json`, and the matching console step in the stage runbook.
4. **Compose** — `Services__Events__BaseUrl` in `docker-compose.yml` **and**
   `docker-compose.dokploy.yml`.
5. **Frontend catch-up card**, Roberto's own, may lag: a visibility control on the event form; stop
   sending `language: null`; drop `invitedCount` and `mapEmbedUrl` from the public pages; point the
   public pages at the convocatorias routes.

## Definition of done

- A new event created without `visibility` is `private`; every pre-existing row is `private` after
  the migration.
- `PATCH` with `language: null` returns `400` with a machine code, and the events-service tests that
  previously relied on clearing it are updated rather than deleted.
- `GET /v1/events?visibility=public` filters, and both responses carry the field.
- convocatorias reads an event with its own token and gets `503 EVENTS_UNAVAILABLE` when the events
  stub is down, on both the launch path and the public reads.
- `GET v{version}/public/events` returns only public, active events, and never returns
  `estimatedBudget`, `totalCost`, `invitedCount` or `optContact`.
- `GET v{version}/public/events/{id}` serves a `closed` event and `404`s a `cancelled` one.
- With the OPT directory degraded, the public detail omits the organizer block and still returns
  `200`.
