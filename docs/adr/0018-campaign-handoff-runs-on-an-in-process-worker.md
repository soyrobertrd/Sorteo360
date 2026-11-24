# ADR-0018 — a campaign launch returns 202 and hands the batches to an in-process worker

- Status: Accepted
- Date: 2026-09-07

## Context

convocatorias-service launches a campaign by posting recipient batches to notification-service,
chunked at 1,000. `docs/specs/convocatorias/04-send-and-handoff.md` §1 originally did all of it
inside the launch request, on the reasoning that notification-service returns `202`, so our side is
a handful of fast accepts rather than a thousand SMTP round trips, and that the platform has no
background jobs at all so adding the first one here would be out of proportion.

The client then removed the approval step (ADR-0017, amended), which collapsed submit and approve
into a single launch call and made the synchronous path the only path. Checking it before building
it turned up two problems, neither caused by the approval change and both latent in code nobody had
written yet.

**The request's cancellation token reaches the batch loop.** Endpoint handlers on this platform take
`CancellationToken cancellationToken` as a parameter, twelve of them in
`ServiceSupplierEndpoints.cs` alone. In Minimal APIs that binds to `HttpContext.RequestAborted`,
which fires when the client disconnects, and `ICampaignSendGateway.SendAsync` takes one as well. A
coordinator closing the browser during batch four of ten cancels batches five to ten.

**nginx severs the connection at 60 seconds.** `frontend/nginx.conf` sets no `proxy_read_timeout` on
any location, and `00-foundation.md` §3 has the two new convocatorias blocks copying the crm ones
verbatim. `04` §2.2 already predicted that a 3,000-invitee campaign is three sequential calls inside
one HTTP request.

Together they produce a user interface that lies. On a slow provider nginx returns `504` to the
browser while the service goes on to mail all 3,000 people correctly, so the coordinator sees a
failure for a campaign that succeeded, and the only thing a second attempt tells them is that the
campaign is no longer in `draft`.

The client's own description of how this gets used made the shape of the problem plain: someone
presses launch as the last thing they do that day and shuts the laptop. A path whose success takes
longer than the timeout in front of it is not synchronous, it is undisclosed.

## Decision

**The launch request ends at the flip to `sending`.** Everything a human can act on happens before
the response; everything else happens after it.

Inside the request:

1. Validate, including `expectedRecipientCount` and the two empty-campaign rules
2. Flush every unsynced unsubscribe to CRM
3. Re-check mailability for the whole list
4. Read the event from events-service
5. Flip the campaign to `sending`, write its id onto an in-process channel, and return
   `202 Accepted` with `Location: /v1/convocatorias/campaigns/{id}`

On the worker: mint the two tokens per recipient, snapshot name and address, render the body once,
post the batches chunked at 1,000, map the per-recipient results onto `send_status`, and land on
`sent` when no row is left `pending`. The client polls the campaign until it reads `sent`.

The worker is a `BackgroundService` draining a `Channel<Guid>`. It is not a polling loop:
`ReadAllAsync` blocks on an empty channel and consumes nothing until a launch enqueues something.
It runs on a token tied to `IHostApplicationLifetime.ApplicationStopping`, never on
`RequestAborted`, which is the whole point.

The split falls on the point of no return. Before step 5, cancelling costs nothing and every failure
is a status code the caller needs: `400` for an empty or stale list, `503` when CRM or events-service
is unreachable, a refusal when the event is cancelled. After step 5 the caller has no decision left
to make, and per-recipient rejections were always going to be read out of `send_status`.

**This is the platform's first background execution.** Verified rather than recalled: no
`BackgroundService`, `IHostedService`, `Channel` or `Task.Run` appears in any service's production
code today.

It is **not** the thing `04` §1 rejected. That rejected a startup scan, on the grounds that it fires
on every deploy and would cheerfully resume a campaign whose event was cancelled an hour earlier. A
channel that only a launch call writes to has neither property: it processes what this process was
explicitly told to send, during this process's lifetime, and an empty channel after a restart is the
correct state rather than a lost queue.

## Rejected

- **Keeping it synchronous and detaching the token.** Ignore `RequestAborted` past step 5 and raise
  `proxy_read_timeout` on the convocatorias location. The send would always finish, but the caller
  still waits minutes on a large campaign and still gets a `504` while it succeeds. It fixes the
  cancellation bug and leaves the lying interface in place.
- **`Task.Run` from the endpoint.** No worker class, no channel. The request's DI scope disposes the
  moment the response is written, taking the `DbContext` with it. Passing `IServiceScopeFactory`
  works around that and leaves unbounded concurrency against notification-service and exceptions
  that vanish with nothing observing the task.
- **Hangfire or Quartz.** The only option that survives a process restart, and rejected more
  reluctantly than the others. It brings its own Postgres tables created outside EF Core migrations,
  which breaks the platform's single-schema-authority rule, plus a dashboard to secure and a large
  dependency carried for one code path. The place durable retries actually earn their keep is inside
  notification-service, which is that map's question.
- **A message broker.** ADR-0017 already rejected it and nothing here changes that. None runs in any
  environment, and `docs/adding-a-new-service.md` still opens its async messaging section with
  "Nothing here exists yet".

## Consequences

- **Durability is unchanged, and the gap is deliberate.** A deploy landing mid-send kills the channel
  with the process, leaving the campaign in `sending` with rows still `pending`. That is identical to
  what the synchronous version did and is recoverable only by a human calling
  `POST /campaigns/{id}/resume-handoff`. This decision fixes the timeout and the misleading response,
  not the crash.
- **`07` §6 gains a fourth reconciliation bucket, `strandedCampaigns`.** Because the `202` makes
  "nobody is watching the batches" the designed behaviour rather than an accident, a stranded send
  needs a screen that lists it rather than an event rollup somebody happens to open. Its two actions,
  `resume-handoff` and `retry-failed`, already exist.
- **The nginx change is no longer needed.** Steps 1 to 5 are three CRM calls and one events call, so
  the 60-second default stops being a constraint and `frontend/nginx.conf` keeps its current shape.
- **`04` §1's "synchronous, with no worker" is reversed** and that spec is rewritten around the
  `202`. Its rejection of a startup scan stands untouched.
- **Roberto's screen polls.** Launch no longer returns a finished campaign, so the frontend shows
  `enviando` and polls `GET /campaigns/{id}` until the state reads `sent`. This lands on the
  convocatorias frontend card rather than the existing catch-up ones.
- **notification-service inherits the precedent.** When it needs retry and delivery feedback it will
  face this same question with durability actually on the line, and it should cite this record rather
  than rediscover it.
