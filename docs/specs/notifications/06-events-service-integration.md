# 06 — events-service as the first caller

This spec is **events-service work**, not notification-service work. It is the other half of the
contract and the only thing that makes any of the rest visible.

**ADR-0011 is amended in the same PR as this lands.** Its Consecuencias are already rewritten; this
spec is what implements them.

## 1. Four moments notify, and one never does

`QuoteService` has five doors:

| Moment | Recipients | Why |
| --- | --- | --- |
| `SubmitAsync` | the approvers | somebody now has a decision to make |
| `ApproveAsync` | the attempt's **submitter** | the coordinator is waiting on an outcome |
| `RejectAsync` | the attempt's **submitter** | the mandatory reason is a to-do list |
| `WithdrawAsync` | the approvers | a pending item vanished from their queue |
| `DiscardAsync` | **nobody, ever** | it kills a `draft`; nobody was ever waiting on it |

The submitter is `QuoteDecision.SubmittedById` on **that attempt**, not the quote's `UpdatedById`,
and resolves through a direct Keycloak user read rather than the graph walk in §3.

> **Normative: withdraw notifies the approvers only, and deliberately not the original submitter
> when a colleague withdrew.** Nothing in `WithdrawAsync` restricts the action to the person who
> submitted, so that case is reachable — but a recipient rule that depends on comparing two user ids
> produces, as its first bug, mail to somebody about their own action. Two coordinators in one OPT
> share an office and will talk.

If MITUR balks at four templates, withdraw is the cheapest to drop. The other three are the workflow
itself.

## 2. Content: the why travels, the money does not

Full rejection reason in the body. The winning supplier's **name** on approval. **No amounts
anywhere.**

The test applied is **what can we never unsend**: a mailbox sits outside the application's
authorization and a forward cannot be recalled, and money is the only part of this workflow that
answers yes. Adding it later costs a template row.

**The reason travels in full rather than behind a link.** Making somebody log in to read one
sentence is exactly the habit that trains people to ignore the mail. Every email still carries the
deep link — the link is how they *act*, not how they find out.

> **Normative: language is hardcoded `"es"`, and `Event.Language` is never read.** It is the
> obvious-looking source and it is wrong twice over: it is **nullable**, and it describes the
> *event* — a fair in Madrid, a roadshow in NYC — not the person reading the alert. Reading it would
> mail a Dominican coordinator in English because her trade show is in New York.

> **Normative: these four templates carry no unsubscribe footer.** Every internal recipient is
> unconditionally mailable with no `{{enlace_baja}}`; you cannot let a coordinator unsubscribe from
> *"tu cotización fue rechazada."* Written down so nobody adds one later for consistency with the
> campaign templates.

## 3. Who the approvers are, and why Keycloak makes it harder than it looks

> **Normative: an approver is whoever holds the function role `quotes:approve` with a matching
> `opt_code`. Never CRM's `is_director`.**

**There are two different "directors" on this platform and they are not the same people.** CRM's
`is_director` is org-chart data **harvested from the public mitur.gob.do website**; those people may
hold no PGTI360 account at all. An email saying *go approve this*, sent to someone who cannot log
in, is worse than sending nothing: it looks handled and is not.

It also keeps ADR-0011's open MITUR question (*Dirección o un comité*) honestly open. Defining the
recipient as the CRM director would quietly answer it, in code, in the wrong direction.

**`central_admin` is not alerted.** `admin.pgti360` holds `quotes:approve` through `central_admin`
but carries **no `opt_code` at all**, so the intersection below drops them. A wildcard would mail one
person about all 18 offices, who would filter it within a week and kill the alert for everyone. The
rule is one sentence: **the alert goes to people whose job is this office.**

### The lookup (Keycloak 26.6)

> **The load-bearing finding: `GET /admin/realms/pgti360/roles/quotes:approve/users` returns `[]`.**
> That endpoint returns **directly assigned** holders only; it does not expand composites, and
> `quotes:approve` is never assigned directly — it is a member of the `opt_director` and
> `central_admin` composites. **No endpoint anywhere goes role → effective holders.** So we invert
> the graph ourselves.

| Step | Call | Cached |
| --- | --- | --- |
| 1 | `GET /roles` — brief representations carry the `composite` flag | Long TTL |
| 2 | `GET /roles/{name}/composites/realm` per candidate, inverting to `{opt_director, central_admin}` | Long TTL |
| 3 | `GET /roles/{name}/users` per ancestor found in step 2 | Short TTL |
| 4 | `GET /users?q=opt_code:{code}` | Short TTL |
| 5 | Intersect on user id, **in our code** | — |

**Eight calls cold, four with the role graph cached. No step is O(users)** — the shape of the role
graph sets the cost, not the size of the directory.

> **Normative: steps 1 and 2 are not ceremony, and the shortcut that deletes them is the bug.** The
> obvious optimisation is *"skip the graph walk, just query `opt_director` and `central_admin`."*
> That hardcodes two **composite job-title names** into events-service, which is exactly what
> ADR-0010 exists to prevent. Walking the graph means **the only role name in our code is
> `quotes:approve`**. The realm's role set is provisional — `opt_director` may be renamed or deleted
> once the client supplies the final set — and under the graph walk that costs a realm edit and
> **no code**. Under the shortcut it silently mails nobody.

**Three sharp edges. Each produces a wrong answer that looks right.**

1. **`opt_code` is stripped from every Admin API response body.** The realm seeds no user-profile
   configuration, so it is an *unmanaged* attribute under the default Disabled policy and is
   filtered out of responses. It works perfectly as a **filter** (`q=opt_code:miami` hits
   `USER_ATTRIBUTE` directly) and is **invisible when read back**. The whole approach survives
   purely because we filter on it and never read it back. **A developer debugging this will `GET`
   the user, see no `opt_code`, and conclude the directory data is broken.** Anything that ever
   needs to *display* or *group by* the OPT code must declare `opt_code` in the user profile first —
   a realm change, landing twice.
2. **`?q=` on the role endpoint is silently ignored, not rejected.** `GET /roles/{name}/users?q=…`
   returns `200` with every holder, unfiltered. The intersection must happen in our code, and **a
   test must pin it**, or a plausible-looking one-call "optimisation" mails every director on the
   planet.
3. **No service account holds any `realm-management` role today.** Every call above is a `403` until
   granted. The minimum is **`view-realm` + `view-users`** — not `realm-admin`. `view-realm` is the
   easy miss: `GET /roles/{name}/users` needs it via `requireView(roleContainer)`, which is not
   obvious from the endpoint's name.

Both endpoints default `max` to 100 and order by username ascending, so **paging must be explicit**;
there is no count endpoint for role members.

**Rejected: assigning `quotes:approve` directly as well as through the composite.** It collapses step
3 to one call and is genuinely tempting. It breaks ADR-0010's composite discipline, doubles the
maintenance of every approver, and creates a state where the composite and the direct grant can
disagree with no way to tell which is intended. Recorded because the next person will think of it.

### Caching and failure

> **Normative: the recipient lookup is a read that cannot fail.** It mirrors `IOptDirectory`'s
> existing split — `ResolveAsync` serves a stale cache and never fails, `GetOfficeAsync` queries
> live and must fail. This is the first kind. Serve the cached closure; with no cached answer at
> all, record it on the attempt row per §4 and move on. **Nothing retries, and the submission always
> succeeds.**

In-memory per instance per ADR-0007, following `CachedOptDirectory` + `OptDirectoryRefreshLock`.
**Two TTLs, not one:**

| What | Changes when | TTL |
| --- | --- | --- |
| The role closure (steps 1–2) | Someone edits the realm graph | Long — hours |
| Role membership and the `opt_code` filter (steps 3–4) | A person joins or leaves | Short — minutes |

A single TTL forces the short one and re-walks a graph that never moves. **There is no invalidation
hook**: ADR-0016 gives the console ownership of the realm, so a role edit ships through no deploy and
nothing can notify us. A realm change takes effect within one TTL, and that is the accepted cost.

## 4. Nobody resolved

Chile seeds with **no director**, and the `gestor` user carries **no `opt_code`**. Not hypothetical.

Because the caller resolves, an empty result means **events-service never calls
notification-service**, so no history row exists anywhere.

> **Normative: record it on the quote's own attempt row.** Notification history answers *what mail
> did we send?*, and mail we never attempted is not its business. But the *quote* must carry
> *nobody was notified*. The submission itself always succeeds.

Rejected: logging it and moving on (invisible), and mailing a fallback like `central_admin` (mails
Santo Domingo about a Chile quote).

## 5. The seam: `IApprovalGate` is deleted, not occupied

ADR-0011 kept the seam open *"por el que un notificador se puede colgar después."* Hanging a notifier
on it is the wrong move, and only the first of four reasons is cosmetic:

1. **Its name is a lie.** It says *request an approval*; ADR-0011 decided approval happens in-app.
   It is a mail seam wearing ADR-0005's label.
2. **Its return type is a trap.** `Result<string, BaseError>` invites the caller to branch, and
   nobody may. `QuoteService.SubmitAsync` fell into it already — **lines 329-331 return the gate's
   failure on a submit that actually succeeded**, so a broken email shows red on a submit that
   worked and the coordinator's natural retry answers `AlreadySubmitted`. The charter's *"a failed
   notification never fails a business transaction"* was written and never implemented.
3. **It takes the wrong argument.** It receives `Quote`; the key and the copy both need the
   **attempt**.
4. **It covers one moment of four.**

**`IQuoteNotifier` replaces it**, in events-service Core: `NotifySubmittedAsync`,
`NotifyApprovedAsync`, `NotifyRejectedAsync`, `NotifyWithdrawnAsync`, each taking the quote **and
its `QuoteDecision`**, each returning `Task<QuoteNotificationOutcome>` — a small record of
`(status, notificationId)` with status `sent | no_recipient | failed`.

> **Normative: never a `BaseError`.** A `BaseError` is what an endpoint hands a user, and this
> failure must never reach one. A return type with **no error channel** makes the swallow structural
> rather than a rule somebody re-breaks in six months.

**The seam stays domain-shaped.** Core says *"attempt 2 of this quote was rejected, reason R"*;
Infrastructure looks up the `template` row and POSTs. The three outcome moments share **one call
site inside `ResolveAsync`**, switching on the resolved attempt's `Outcome`, so the placement rule
is enforced in one place rather than three.

> **Normative: the call is post-commit and the outcome is swallowed.** After the write, never
> before. Calling inside a transaction was rejected on a fact, not a preference: `_repository.SubmitAsync`
> is a single conditional update with no ambient transaction, so there is nothing to enlist in.
>
> **The accepted loss, named rather than buried:** a crash between the commit and the POST loses that
> notification silently and permanently — nothing remembers it was owed, and the quote looks normal.

**`StubApprovalGate` is deleted with no events-side replacement.** The sink is Mailpit and
`AddServiceClient` reaches a Compose service name that is always up. A no-op notifier here would only
add a second place a notification can silently vanish; integration tests want the real HTTP path
against a stubbed neighbour, which is the `NeighbourStub` pattern convocatorias already establishes.

**No `BackgroundService` in events-service.** A dispatch queue would buy protection against a hang
that only happens when the downstream is already broken, and pay for it with a second place
notifications can be lost. Build that machinery once, in the service whose job it is (`03` §5).

## 6. `cotizacion_decision` takes four flat columns

`approval_ref_id` **could not simply be renamed**: an attempt is opened by a submit alert and closed
by an outcome alert, so **one attempt owns two notifications** and one column holds one pointer.

| Column | Replaces |
| --- | --- |
| `submit_notification_id` | `approval_ref_id`, renamed |
| `submit_notification_status` | new |
| `outcome_notification_id` | new |
| `outcome_notification_status` | new |

Status values match the outcome record: `sent | no_recipient | failed`.

A child table keyed by attempt and moment was rejected — the count is fixed at two by the workflow,
so it would be a join built to render two badges. Recording only the submit alert was rejected on the
sharper ground: **rejection is the moment a silent mail failure hurts most.**

The rename is free in practice: the column has never held anything but `stub-approval-<guid>`.

**The status is part of the quote detail contract, not an internal column.**
`QuoteDetailResponse.Attempts` already ships `QuoteDecisionResponse` to whatever renders the quote,
so the fields cost one line of contract and no new endpoint. **The badge itself is rendering and
belongs to `frontend/`**.

Two human routes, because there are two different questions: *"Did MITUR's mail work last night?"* is
the history screen filtered to `failed` (`05`); *"Why didn't the boss hear about **this** quote?"* is
the coordinator on the quote they submitted. **Nothing alerts and nobody is paged** — a failure is
discovered by looking. Proportionate at a handful of internal emails a day.

## 7. `type`, `template_key`, `about`, and the idempotency key

| `type` = `template_key` | Moment |
| --- | --- |
| `events.quote.submitted` | submit |
| `events.quote.approved` | approve |
| `events.quote.rejected` | reject |
| `events.quote.withdrawn` | withdraw |

**Identical names on purpose**, so nobody holds a mapping table in their head. A later divergence is
additive.

`about` carries **two** entries on every send: `{events, event, <eventId>}` and
`{events, quote, <quoteId>}`. **Not the attempt** — `about` is the query surface, and nobody will ever
ask for mail about attempt 2. The pointer to a specific send already lives on the attempt row.

> **Normative: the idempotency key is `events:quote:{quoteId}:attempt:{n}:{moment}`**, moment being
> `submitted | approved | rejected | withdrawn`. Derived from a natural key, never random. See `01`
> §3 for why every coarser version is silently broken.

## 8. Templates live in events-service

A `template` table in **events-service's** database, keyed `(template_key, language)`, holding
`subject` and `body_html`, seeded by `migrationBuilder.InsertData`. Copy convocatorias' pattern.

> **Normative: `InsertData`, never `HasData`.** `HasData` is model-managed and re-asserts its values
> on every migration, silently reverting a row a user edited through the endpoints that come later.

Rejected: **in-repo `.html` embedded resources** (cheaper today and diffable in a PR, but they make
the future edit endpoint a rewrite rather than an addition), and **files in git seeded into the
table** (two sources of truth and a sync bug with no owner).

**What is traded is git reviewability, not vendor lock-in** — there is no vendor-side asset left to
be locked into. A copy fix stops being a PR diff the day edit endpoints land. **Phase 1 keeps the
review surface by having no edit endpoint at all:** a copy change is a data migration and a deploy of
events-service.

The rule, so a third caller can apply it without re-litigating: **a template table is what a picker
and future editing buy. Absent both, a file would be cheaper — we take the table anyway for one
pattern platform-wide, because the client has stated MITUR staff will eventually manage templates
themselves.** (Honest limit: that statement was made about *campaign* templates. Extending it to
transactional alerts is our inference, and the table shape makes the inference free either way.)

**The Spanish copy itself is written during the build and shown to MITUR for review then.** Writing
four emails' worth of copy now would produce text nobody has approved against a process nobody has
confirmed. This spec fixes the four `template_key`s, the `data` keys and the subject shape; the prose
is build work.

**Golden-file tests in events-service** are what keep a copy change visible as a diff for as long as
phase 1 has no edit endpoint: a seeded row plus a fixed data bag renders to an approved body checked
into the repo.

## 9. The deep link and the from-address

Every one of the four emails links to `{App__BaseUrl}/eventos/{eventId}`, assembled in **one
helper**, so a route change is a one-line edit.

**`/eventos/{id}` is confirmed as the real route** (owner, 2026-09-13). It is a route of
`frontend/`, and its routes are what the link has to hit. **There is no per-category quote route**: quotes are an area inside the
event page, so the link lands the reader on the event and they find the category there. That is one
extra glance, not a dead end, **and it is why the helper takes the event id rather than the quote
id.**

From-address `no-reply@mitur.gob.do` via `Notifications__FromAddress`. **No `Reply-To`** — a
reply-to nobody monitors is worse than none at all, because a bounce at least tells the sender their
reply went nowhere.

**Client ask 6 (the ATL/NYC per-office from-address list) does not block these four sends.** That ask
is about **public campaign mail**, where which office is speaking is the point. These are internal
alerts about MITUR's own workflow, and the sending office is not information the reader needs.

## 10. Three environment variables, in both compose files

> **Normative. All three land in `docker-compose.yml` *and* `docker-compose.dokploy.yml`.** The
> Dokploy file is **not** an override layered on the base one — it is self-contained, because
> Dokploy does not chain compose files, and it is what stage and production actually deploy.

| Variable | `docker-compose.yml` | `docker-compose.dokploy.yml` |
| --- | --- | --- |
| `App__BaseUrl` | `http://localhost:${FRONTEND_PORT:-5175}` | `https://app.${DOMAIN}` |
| `Notifications__FromAddress` | `no-reply@mitur.gob.do` | `no-reply@mitur.gob.do` |
| `Services__Notifications__BaseUrl` | `http://notification-service:8080` | `http://notification-service:8080` |

> **`App__BaseUrl` is the dangerous one, and the danger is not that it is missing — it is that its
> value differs between the two files.** Copying the base line into the Dokploy file produces
> `http://localhost:5175`, which resolves inside the container to nothing, so every cotización email
> ships a link to a machine that is not the reader's. **Nothing fails:** the send succeeds, the `202`
> arrives, the history row says `delivered`, and the only symptom is a human clicking a dead link.

**Convocatorias already solved this exact problem and is the pattern to copy**, not merely a similar
case: `Portal__BaseUrl` sits in `docker-compose.yml` as `http://localhost:${FRONTEND_PORT:-5175}` and
in `docker-compose.dokploy.yml` as `https://app.${DOMAIN}`. Same shape, same trap, already avoided
once.

> **`App__BaseUrl` must be a separate variable from `Portal__BaseUrl`.** Convocatorias' portal is
> the **public** registration site; events-service's is the **internal** app. Collapsing them would
> mail MITUR staff into the public portal the day the two hostnames diverge.

`Services__Notifications__BaseUrl` is step 9 of the new-service runbook. It is listed here because
events-service's block in the Dokploy file **does not have it** (convocatorias' does), and the
checklist reads as if it were already covered.

**`scripts/compose-parity.sh` catches a missing key but cannot catch a wrong value** — it compares
keys deliberately, because values legitimately differ. So the two `App__BaseUrl` values are a review
item, not a test.

## Definition of done

- Submit, approve, reject and withdraw each POST exactly one notification; **`DiscardAsync` posts
  nothing** — assert with a spy that it is never called.
- Withdraw notifies the approvers and **not** the submitter, including when a different coordinator
  withdrew.
- `IApprovalGate` and `StubApprovalGate` are **gone from the codebase**; `IQuoteNotifier` has no
  error channel in its return type, and `QuoteService.SubmitAsync` no longer returns a notification
  failure to its caller.
- **A notification failure of every kind leaves the business operation successful**: force a
  timeout, a 500 and a transport exception, and assert the submit/approve/reject/withdraw all
  return success.
- The graph walk names **only** `quotes:approve` in code — grep the repository for `opt_director`
  and `central_admin` and find neither.
- A user holding `quotes:approve` through a composite **is** found; `?q=` on the role endpoint is
  ignored by Keycloak, so a test pins that the `opt_code` intersection happens in our code.
- An OPT with no resolvable approver (Chile) writes `no_recipient` on the attempt row and posts
  nothing, and the submit still succeeds.
- With Keycloak unreachable and a cold cache, submit still succeeds and the attempt row records the
  failure.
- The four columns replace `approval_ref_id`, and both notification statuses ship in
  `QuoteDetailResponse.Attempts`.
- The idempotency key includes the moment — assert that a submit alert and a reject alert on the
  **same attempt** produce **two** distinct batches.
- The `template` table is seeded by `InsertData`, four rows keyed `(template_key, "es")`, with
  golden-file tests over each rendered body.
- Every email links to `{App__BaseUrl}/eventos/{eventId}` from **one** helper, and no template
  contains a hardcoded host.
- **All three variables appear in `docker-compose.yml` and `docker-compose.dokploy.yml`, with
  `App__BaseUrl` reading `http://localhost:${FRONTEND_PORT:-5175}` in the first and
  `https://app.${DOMAIN}` in the second.** `scripts/compose-parity.sh` passes.
- ADR-0011's amended Consecuencias match what was built.
