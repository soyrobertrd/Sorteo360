# ADR-0007 — Redis caching deferred until a consumer and multi-instance need exist

- Status: Accepted
- Date: 2026-07-16

## Context
The Compose stack ran a `redis` container (AOF persistence, a volume, a
healthcheck) and every service's `DataServicesConfiguration` connected a
`ConnectionMultiplexer` synchronously at startup, registering
`AddStackExchangeRedisCache` with a fallback to `AddDistributedMemoryCache()`
if the connection failed. No code anywhere in the platform consumes
`IDistributedCache` — the wiring existed without a caller. First launch is a
single-instance on-premises deployment: there is no scale-out yet, and the
in-memory `IDistributedCache` implementation is sufficient for a single
process.

## Decision
- Redis is removed from the Compose stack (all environments) and from every
  service's dependencies: `StackExchange.Redis` and
  `Microsoft.Extensions.Caching.StackExchangeRedis` are dropped from
  `Directory.Packages.props` and the Infrastructure project references; the
  `RedisHealthCheck` classes are deleted.
- Each service keeps the `IDistributedCache` seam, registered via
  `AddDistributedMemoryCache()`, so a future distributed backend swap is
  config-only (no call-site changes).
- Re-adding Redis is in scope once a real caching consumer exists **and**
  the platform needs more than one instance of a service — whichever comes
  first triggers this ADR's re-evaluation.

## Consequences
- Local/stage/prod Compose files no longer provision, publish ports for, or
  depend on a `redis` service; `.env.example`'s `REDIS_PORT` is gone.
- `/_ready` and `/_health` never depended on Redis for eventos-service (it was
  tagged `cache`, non-blocking) and continue to depend only on PostgreSQL.
- Re-adding Redis when needed is ~10 lines per service (restore the package
  references, the `AddStackExchangeRedisCache` registration, and the
  `redis` Compose service) — the removal does not foreclose scale-out.

## Note — the OPT directory snapshot was evaluated as a trigger (2026-08-06)

The first real `IDistributedCache` consumer arrived with ADR-0013: events-service
caches a snapshot of the CRM's OPT directory to resolve display names. It was
tested against this ADR's two conditions and does **not** trigger re-adding Redis.

- Half the trigger is met — there is now a caching consumer — but the other half
  is not: each service still runs a single replica, so there is nothing for a
  shared cache to share.
- A shared cache would not remove the failure that actually matters here. A cold
  start with the CRM down leaves the snapshot empty whether it lives in memory or
  in Redis; what saves the read is serving the last good copy, and that is a
  policy in the caller, not a property of the backend.
- The seam held. The snapshot goes through `IDistributedCache` with no
  Redis-specific call sites, so the backend remains a registration-line swap.

Status unchanged: this records that the decision was tested, not that it changed.
**The trigger to revisit is events-service running more than one replica** — at
that point each instance keeps its own snapshot and its own cold start, and the
shared backend starts buying something.

## Note — the company directory snapshot was evaluated as a trigger (2026-08-25)

The second `IDistributedCache` consumer arrived with ADR-0015: events-service caches a snapshot of
the CRM's company directory to resolve supplier identity. Tested against this ADR's two conditions,
it does **not** trigger re-adding Redis. Each service still runs a single replica, so a shared
cache would have nothing to share, and the seam held again: the snapshot goes through
`IDistributedCache` with no Redis-specific call sites.

One argument here is genuinely new and the OPT evaluation did not have it. The note above claims a
cold start with the CRM down leaves the snapshot empty "whether it lives in memory or in Redis."
That holds only if Redis restarted too. **Redis with AOF in its own container survives an
events-service redeploy**, so the copy would outlive the deploy that today wipes it. With eighteen
OPT offices that was worth nothing. With a company directory refilled on every release it may not
be, and the cold-start cost is now paid by whoever opens the supplier screen first after a deploy.

Nothing changed, and this is not yet enough to act on. It is recorded so the revisit is not
argued from memory. A research ticket carries it: measure the snapshot size and the cold-start
cost against a Redis container, and decide whether this ADR's trigger should read "more than one
replica **or** a snapshot too expensive to refill on every deploy."
