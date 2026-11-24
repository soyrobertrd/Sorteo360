# ADR-0001 — Input DTOs are terse positional records; domain entities keep guards

- Status: Accepted
- Date: 2026-06-24

## Context
The house rule across services is: no primary constructors, and domain types
guard their invariants explicitly with `ArgumentNullException.ThrowIfNull` in
the constructor. Applied uniformly, that rule would also force every
service-input DTO (`CreateEventoDto`, `AddCandidatoDto`, …) to carry its own
null-guarded constructor — but every input DTO is already validated by a
FluentValidation validator at the Web boundary before it reaches Core, and the
domain entity it eventually builds (`Evento`, `Proveedor`, `CategoriaCotizacion`,
…) guards its own invariants independently. A third guard on the DTO itself
would be redundant: it sits in the middle of two guards that already cover it.

## Decision
Split the "no primary constructors / explicit guards" rule by **role**, not by
`record`-vs-`class` (both input DTOs and domain entities are C# records):

- **Input DTOs** — service-input types under `Core/DTOs`, validated by a
  FluentValidation validator at the Web boundary — are terse **positional
  records** with **no** null-guards.
- **Domain entities** (`Proveedor`, `Evento`, `CategoriaCotizacion`, …) keep
  explicit constructors with `ArgumentNullException.ThrowIfNull` guards; they
  defend their invariants independent of any caller.

This is the one scoped exception to "do not use primary constructors",
applied first in `eventos-service` and meant for sibling services too.

## Consequences
- Input DTOs stay one-line declarations (`public sealed record CreateEventoDto(...)`)
  instead of a full constructor body duplicating what the validator and the
  entity constructor already check.
- Positional records make constructor parameters PascalCase, so call sites
  using named arguments must use PascalCase (e.g. `Categoria:`, not
  `categoria:`).
- A DTO that is *not* immediately followed by both a validator and an entity
  constructor guard does not qualify for this exception and should use the
  standard guarded-constructor pattern instead.
