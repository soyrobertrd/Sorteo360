# ADR-0028 — The raffle's draw phase serialises on a row lock, not on the raffle's xmin

- Status: Accepted
- Date: 2026-10-04

## Context

A raffle's rules span its prizes: one prize per person, one prize per company, and an `ineligible`
void that reaches every later draw. So two writes on two *different* prizes can still conflict, and
the serialisation has to be per raffle (`02` §4).

Spec `02` §4, as first written, serialised the draw phase by stamping the `raffle` row so its `xmin`
token covered the write, the idiom `QuoteRepository` uses for a quote's children. It rejected
`SELECT … FOR UPDATE` "as a second concurrency idiom in a service that has one". By the time the
draw was built that was no longer true: `RaffleRepository.LockRaffleAsync` already takes
`FOR UPDATE` on the raffle row for round create/delete, `start` and the overrides (PGTI-118
Decision 9).

## Decision

Every raffle write that reads a draw-phase guard and then acts on it **locks the `raffle` row with
`FOR UPDATE`** (`LockRaffleAsync`) at the start of its transaction, re-reads, decides, and commits:
`draw`, `void`, `reopen`, `handover`, `handover/reverse` and `complete`, and — added by PGTI-120 —
prize update and delete and round update, whose `PRIZE_HAS_DRAWS` guard reads the draws. None of them stamps
`raffle.updated_at` or `updated_by_id`; those keep meaning "the raffle's setup changed", and each
draw-phase row carries its own `*_at` / `*_by_id`. The raffle's `xmin` stays, for ADR-0009's
whole-body `PATCH` only.

## Why

- **Every race ends in a domain code.** The second request waits, then runs against the committed
  state, so it answers what it would have answered alone: `RAFFLE_POOL_EXHAUSTED`,
  `PRIZE_ALREADY_DRAWN`, `RAFFLE_HAS_DRAWS`, `PRIZE_ALREADY_REDEEMED`. Under the stamp the same
  races answered `CONCURRENCY_CONFLICT`, or `DB_CONFLICT` when EF happened to flush the
  `prize_draw` insert before the raffle update.
- **The race tests are deterministic.** Under the stamp, the cross-prize race only produced
  `CONCURRENCY_CONFLICT` if the two transactions overlapped; a test could not force that.
- **The two idioms cannot be mixed safely.** A stamped verb only notices writers that change the
  raffle row. A `reopen` that stamped would count zero draws, let a locked draw commit, and still
  pass its `xmin` check, then delete the frozen attendee the new draw points at.
- **A stamped draw would collide with setup edits.** A mid-evening `PATCH` of the visual style,
  sent with an `If-Match` taken before a draw, would fail for a reason unrelated to the edit.

## Consequences

- Draw and void no longer answer `CONCURRENCY_CONFLICT`. The frontend refetches on any `409` and
  branches on the code (`02` §9).
- A second press waits for the first transaction instead of failing fast. One laptop drives the
  draw, so the wait is a double-click's length.
- Reversing this means re-adding a stamp to nine verbs and re-writing their race tests.
