# events-service: cached directories (CRM and realm)

> Moved from `events-service/CLAUDE.md` (PGTI-213), text unchanged except headings. A section named
> with "see ... below" may now live in a sibling file in this folder: `grep -n '^## ' events-service/docs/*.md` finds it.

## The stale-tolerant cache — one type, four directories

`Infrastructure/Directories/` holds the machinery every cached directory shares, and it names no
origin because two of its users refresh against the CRM and two against the realm. A fifth, the
raffle's door list, refreshes against the attendance seam (see the raffle section below).

- `StaleTolerantCache` owns reading, writing, freshness and the refresh-lock dance. **The rule it
  exists to state once**: a copy outlives its own TTL. The TTL decides when to refresh, never when
  the entry expires — making it the expiry would destroy the last good copy exactly when the origin
  being down makes it necessary. The fetch time travels inside the value; the entry's own expiry is
  a long retention that only stops something looked up once from living forever.
- `CacheCopyPolicy` carries what genuinely differs per copy: its TTL, its retention, whether it
  takes a refresh lock, whether an unreadable response body counts as the origin being down, and
  whether an absence is worth storing. Each directory keeps its own cache keys and its own
  degradation vocabulary; `CacheOutcome` (`Fresh` / `Stale` / `Missing`) is all the cache says, and
  the caller translates.
- `CachedCopy<T>` distinguishes **no entry** from **an entry whose value is nothing**. Only
  `KeycloakQuoteSubmitterDirectory` stores an absence today, and collapsing the two would make it
  re-query the realm forever for an account that is gone.
- `DirectoryRefreshLock` has one subclass per directory — a shared lock would make refreshing
  companies wait on OPT, two queries that compete for nothing. Distinct types, not named instances,
  so the container tells them apart without keys.
- **It stays in events-service.** Promoting it into `shared/Sorteo360.Platform` is a decision
  worth making when a second service needs it, not a shape guessed from one consumer (ADR-0012).

## The CRM directories — this service consumes them, it does not own them

`docs/adr/0013` decided the OPT directory and `docs/adr/0015` the company one; what follows is
where they live in the code. They are two directories over **one** CRM, wired the same way on
purpose: one pattern to learn and one to maintain.

- `Core/OptDirectory/` — `IOptDirectory`, `OptOfficeInfo`, `OptStaffInfo`, `OptLookup`,
  `OptReference`. **No transport appears in any signature here**, which is what lets domain tests
  substitute the directory.
- `Core/CompanyDirectory/` — `ICompanyDirectory`, `CompanyInfo`, `CompanySnapshot` and
  `CompanyLookup`, same rule. Two operations for the same reason OPT has two: `ResolveAsync` is for
  display and cannot fail, `GetCompanyAsync` is for writes and queries live. Plus
  `ICompanyContactDirectory` and `CompanyContact`, a **separate** seam and not a third operation on
  the first: it has no snapshot to serve and no refresh to decide, and folding it in would make the
  four domain doubles that substitute `ICompanyDirectory` carry a method none of them calls.
  `IBankAccountDirectory` and `BankAccountInfo` are a third seam of the same kind (PGTI-259): one
  account of one company, live, asked only by a candidate write naming its payment account.
- `Core/CrmDirectory/` — `CrmDirectoryStatus` and `CrmDirectoryStatusContext`, deliberately outside
  `OptDirectory/` because they are **not** OPT's. One status per request covers everything resolved
  against the CRM: the same CRM answers every directory, so a status each would claim an
  independence that does not exist (ADR-0015).
- `Infrastructure/Crm/` — `CrmOptClient` and `CrmCompanyClient` (the only places a CRM route
  appears), their wire DTOs, `CrmCompanyContactDirectory` (live, no cache — see below),
  `CachedOptDirectory` and `CachedCompanyDirectory` (each one a snapshot, a lazy fill by id, and
  the degradation ladder) and `OptDirectorySettings` / `CompanyDirectorySettings`. The caching
  machinery itself is not here — see `Infrastructure/Directories/` below.
- `Web/Filters/CrmDirectoryStatusFilter` stamps `X-Crm-Directory-Status` on the whole events group
  and the whole service-suppliers group, so a new endpoint cannot forget it, **plus
  `X-Opt-Directory-Status` with the identical value** — the former name, kept working until the
  frontend retires it on its own card. Both are written from one computed value: two names for one
  fact, never two computations that could disagree. Its Spanish — the three rungs and why there are
  two names — lives once, in `Web/Filters/CrmDirectoryStatusDescription`, and both feature
  description files prepend their own sentence about which fields they resolve.
- **A response only carries the header if it actually served a resolved name.** That is why an event
  write carries it (it answers with the resolved detail) and a supplier write does not (it answers
  with a bare id or a 204): `CachedOptDirectory.GetOfficeAsync` reports `Ok` on a live hit and
  `CachedCompanyDirectory.GetCompanyAsync` deliberately reports nothing. Saying `ok` about a copy the
  response never touched is worse than saying nothing.

Ten rules worth knowing before changing any of it:

- **`events.event` holds `opt_office_id` and `opt_contact_id` and no names.** Never add a name
  column back, and never let one into the domain entity.
- **Reads must not fail over a display name.** The snapshot's TTL is *not* the cache entry's expiry
  — expiring the entry would destroy the last good copy exactly when the CRM being down makes it
  necessary. `name: null` with `X-Crm-Directory-Status: unavailable` is a correct 200.
- **Writes validate live and fail closed**: 409 `OPT_OFFICE_NOT_AVAILABLE` /
  `OPT_CONTACT_NOT_AVAILABLE` / `COMPANY_NOT_AVAILABLE`, 503 `CRM_UNAVAILABLE` for all three.
  Never validate a write from the snapshot, or an office or a company created minutes ago becomes
  unusable. Both cached directories keep that live path separate from the cached one for exactly
  this reason. A candidate's payment bank account is checked the same way, but answers **400**
  `CANDIDATE_BANK_ACCOUNT_NOT_AVAILABLE` (another company's or deactivated), because what is wrong is
  what was sent; `CRM_UNAVAILABLE` stays 503.
- **A closed company still passes the write check**, unlike a closed OPT office. The same check
  guards create and edit, and refusing one would freeze the ficha of a supplier MITUR still hires —
  the failure `docs/adr/0015` §"A closed company keeps resolving" exists to prevent. Only an id that
  never existed is refused.
- **`service_supplier` holds `company_id` and no name**, the same rule as `event`. A company gets
  one live ficha per category, backed by `ux_service_supplier_company_category` and answered as
  `SUPPLIER_COMPANY_ALREADY_REGISTERED` before the index has to. A secondary category counts too
  (ADR-0034): a company may not hold one category on two live fichas, main or secondary, and that
  clash is checked in the write's unit of work with no index behind it. **All five reads resolve that id
  against the cached company directory** — the detail, the list, the pool, the history and the
  supplier summary carried on a candidate inside the quote detail — and answer
  `company: {id, name, country, city, isActive}`, where every field but the id can be null.
  `CompanyRefResponse.From` is the only way to build that object, so no read can describe a company
  its own way. `company.isActive` is the CRM's commercial relationship and is never `isHireable`: a
  dormant company stays hireable, and neither flag is derived from the other.
- **Contacts are the one CRM field the snapshot does not carry, and only two reads ask for them.**
  The supplier detail fetches a company's whole list; every route returning a raffle names its
  prizes' sponsor contacts through `IContactNameDirectory` (`CrmContactNameDirectory`), one live
  `GET /v1/contacts?id=` per response and only when a prize has one, with the same `Stale` on
  failure (spec 00 §8). The sponsor write check reuses `ICompanyContactDirectory`: `None` there is
  `503 CRM_UNAVAILABLE`, a contact missing from the list is `409 COMPANY_CONTACT_NOT_AVAILABLE`.
  For the supplier detail: `CompanySnapshot` is five fields served whole on every listing; contacts are fetched per
  company, live, by `GET /v1/companies/{id}/contacts`, and the list, the pool and the history do not
  carry them — one request per row for a field none of those three screens draws. Nothing caches
  them either: a phone number from last week is worse than saying you do not know. So `contacts` is
  `null` when the CRM did not answer and `[]` when the company has nobody registered — two different
  answers, and conflating them leaves a ficha that looks contactless exactly when someone needs a
  number mid-event. A failed fetch reports `Stale` on the shared `CrmDirectoryStatusContext`, the
  same rule a failed fill-by-id follows, so the client keeps rendering one degradation notice
  instead of inspecting fields. **A contact who withdrew marketing consent is returned** — calling a
  vendor about work already booked is not marketing (`docs/adr/0015` §"Consent gates marketing, not
  operations"), so no consent flag reaches this service, where it could only invite a wrong filter.
- **The quote detail resolves at the edge, the history in the service**, and the difference is what
  each one has in hand. `QuoteWithCandidates` is the aggregate the writes also load and enforce
  their rules on, so a display name has no business on it; `ServiceSupplierHistory` is a read model
  and nothing else. So `QuoteEndpoints.MapToDetailAsync` resolves the distinct company ids of the
  candidates in one call and hands the lookup to `MapToCandidate`, which **takes it as a parameter**
  — a new projection of candidates does not compile without one, which is what keeps this surface
  from falling behind the other four. The five routes that return the detail (the `GET` plus the
  re-read from submit, approve, reject and withdraw) all go through it, and the group carries
  `CrmDirectoryStatusFilter` so the headers cannot be forgotten.
- **The history resolves and does nothing else.** `QuoteService.GetHistoryAsync` resolves the
  distinct company ids of the rows the repository returned and leaves the set exactly as it came:
  no filter, no reorder, and an unresolvable row keeps its place with a null company rather than
  sorting last as it does in the list. The reason is that the history compares amounts. A row a
  filter drops is money MITUR actually paid leaving the comparison with nobody the wiser, and the
  order is chronological because that is the question being asked. `country` was a query parameter
  here before ADR-0015 and deliberately did not come back with the resolve; reinstating it is its
  own decision, not a leftover.
- **The list and the pool filter, search and sort in memory, after the resolve.** `category` is the
  only filter that reaches the database, because it is the only one events stores; `country`, `city`
  and `searchTerm` — plus the ordering, which is by company name — are applied to the resolved set
  in `ServiceSupplierService`. Searching against a copy is safe here, unlike the event list, because
  events owns the complete set of supplier ids: a company with no supplier row cannot appear however
  stale the copy is, so a miss is a missing name, never a missing row. The rule that keeps it true:
  **an unresolvable supplier is returned, sorts last, and no identity filter drops it** — the
  filters narrow among the rows whose identity is known. Paging would happen after the resolve; the
  list is unpaged today. If volume ever makes that hurt, the fix is a read-only company projection
  inside events, deliberately and later.
- **events-service never writes to the CRM.** The frontend creates or picks the company against
  crm-service with the user's own token and posts the id here. A write proxy would lose which OPT
  registered each company, and that one cannot be undone later.

`OptDirectory__SnapshotTtlMinutes` and `CompanyDirectory__SnapshotTtlMinutes` (both default 15) tune
how long each copy is considered current. They are two knobs because the two copies do not age alike:
offices change a few times a year and companies are registered daily.

Integration tests drive the whole chain against a WireMock CRM (`CrmDirectoryStub`) rather than
substituting `ICrmOptClient` or `ICrmCompanyClient`, because the caching, the degradation and the
resilience pipeline only exist below that seam. The stub serves both route families and takes them
down separately: one CRM, but a test that tumbles the OPT directory is saying nothing about
companies. Its default is that **any** company id resolves, so the many tests that merely need a
supplier do not have to declare one; `ServeCompanies` switches to a closed world where anything
undeclared is a 404, and a `StubCrmCompany` with `ListedInDirectory: false` is the company the bulk
read omits and the fill-by-id path has to find. `ServeContacts` publishes a company's contacts, and a
company with none declared answers the empty page; `CompanyContactsRequestCount` is what proves the
list, the pool and the history never ask for them, since an absent JSON field cannot tell "not
requested" from "requested and dropped". **A class that takes the CRM down gets its own
factory**, because the chassis circuit breaker stays open for fifteen seconds and would otherwise
fail the next class for an outage that is not its own — that is why
`ServiceSupplierCompanyDegradationTests` sits apart from `ServiceSupplierCompanyResolutionTests`.
The same holds inside a degradation suite: its stale-copy test and its cold-cache test both take the
CRM down, so the cold-cache one lives in its own `...ColdCacheTests` class. Sharing a host makes the
suite pass or fail on the order xUnit picks, which a test rename can flip.

## Who approves a quote comes from the realm, never from the CRM

`docs/adr/0011` decided it and `docs/specs/notifications/06-events-service-integration.md` §3 has
the detail; what follows is where it lives in the code.

- `Core/Approvers/` — `IQuoteApproverDirectory` and `QuoteApprover`. No transport in the signature,
  same rule as the two CRM directories. It takes an **`opt_code`**, not an office id.
- `Infrastructure/Keycloak/` — `IKeycloakAdminClient` / `KeycloakAdminClient` (the only place a
  realm route appears), its wire DTOs, `KeycloakQuoteApproverDirectory` and
  `QuoteApproverSettings`. The `HttpClient` is the chassis's `AddServiceClient`, destination
  `Keycloak`, so the call carries this service's own realm identity.

Seven rules worth knowing before changing any of it:

- **An approver is whoever holds `quotes:approve` with a matching `opt_code`. Never CRM's
  `is_director`.** There are two different "directors" here and they are not the same people:
  CRM's is org-chart data harvested from the public mitur.gob.do site, and those people may hold no
  PGTI360 account at all. Mail telling somebody to go approve something, sent to somebody who
  cannot log in, is worse than sending nothing — it looks handled and is not.
- **`central_admin` is not an approver, and no code says so.** `admin.pgti360` reaches
  `quotes:approve` through it but carries no `opt_code` at all, so the intersection drops them. A
  wildcard would mail one person about all 18 offices, who would filter it within a week.
- **`GET /roles/quotes:approve/users` returns `[]`, and that is not a bug.** It answers directly
  assigned holders only, it does not expand composites, and the role is never assigned directly. No
  endpoint anywhere goes role → effective holders, so `WalkRoleGraphAsync` inverts the graph: list
  the roles, read each composite's children, walk up from `quotes:approve` to its ancestors at any
  depth.
- **The only role name in production code is `quotes:approve`**, enforced by
  `ApproverRoleNamesInCodeTests`. The tempting shortcut — skip the walk, query `opt_director` and
  `central_admin` — hardcodes two composite job titles, which is what `docs/adr/0010` exists to
  prevent. The realm's role set is provisional: under the walk a rename costs a realm edit and no
  code, under the shortcut it silently mails nobody. Also rejected: granting `quotes:approve`
  directly *as well as* through the composite, which lets the two disagree with no way to tell
  which was intended.
- **`opt_code` is stripped from every Admin API response body**, because the realm seeds no
  user-profile configuration and it is an unmanaged attribute under the default Disabled policy. It
  works perfectly as a *filter* (`q=opt_code:miami`) and is invisible when read back — **the whole
  approach survives because we filter on it and never read it back.** Anybody debugging this will
  `GET` the user, see no `opt_code`, and conclude the directory is broken. It is not. Displaying or
  grouping by the code would need `opt_code` declared in the user profile first, a realm change
  landing twice.
- **`?q=` on the role-members endpoint is silently ignored, not rejected** — `200` with every
  holder, unfiltered. The intersection happens in our code and
  `FindApproversAsync_ReturnsOnlyTheOptHolder_WhenTheRoleEndpointIgnoresTheFilter` pins it, or a
  plausible one-call "optimisation" mails every director on the planet. Both endpoints also default
  `max` to 100 with no count endpoint, so paging is explicit.
- **This read cannot fail, and an empty answer is legitimate.** It is `ResolveAsync`'s half of
  `IOptDirectory`'s split, not `GetOfficeAsync`'s: serve the cached closure, and with nothing cached
  answer nobody and let the caller record it. Nothing retries. Chile seeds with no director and its
  `gestor` carries no `opt_code`, so an OPT with no approver is the normal case, not a symptom. A
  disabled holder and one with no email address are excluded for the same reason the CRM director
  is.

`QuoteApprovers__RoleGraphTtlMinutes` (default 360) and `QuoteApprovers__MembershipTtlMinutes`
(default 5) are **two TTLs on purpose**: the role closure only changes when somebody edits the realm
in the console, membership changes when a person joins or leaves. One TTL would force the short one
and re-walk a graph that never moves. **There is no invalidation hook** — `docs/adr/0016` gives the
console ownership of the realm, so a role edit ships through no deploy and nothing can notify us. A
realm change takes effect within one TTL, and that is the accepted cost.

The service account needs **`view-realm` and `view-users`, not `realm-admin`**. `view-realm` is the
easy miss: `GET /roles/{name}/users` needs it via `requireView(roleContainer)`, which the endpoint's
name does not suggest. Both are in `infra/keycloak/pgti360-realm.json` and as a console step in
`docs/deploy-with-dokploy.md` §8.
