# ADR-0009 — PATCH carries the complete editable representation

> **Partially superseded by [ADR-0031](0031-collaborator-identity-documents-are-sealed-by-the-crm-and-disks-are-encrypted-instead-of-tde.md)**
> (decision 11): a sealed value (a cédula, a passport or visa number, a bank account number)
> omitted from a `PATCH`, or from an item of a `PUT` that replaces a whole list, keeps the stored
> one. Everything else here still holds, including the
> `event.visibility` carve-out.

- Status: Partially superseded by ADR-0031
- Date: 2026-07-26

## Context

Both services expose `PATCH` for edits, and in both, the handler copies every field
from the request onto the persistence row: `ProveedorRepository.UpdateAsync` assigns
`Empresa`, `Contacto`, `Direccion`, `UbicacionMapaUrl`, `Comentarios`, and `Pais`
unconditionally, and `OptOfficeRepository.UpdateOfficeAsync` does the same for the OPT
directory. A field the caller omits is therefore written as null or as the CLR default,
not left alone. `PATCH` here has always meant *replace the editable fields*.

Nothing wrote that down, and the gap surfaced as a defect. `UpdateOptStaffRequest`
declared `IsDirector` as a non-nullable `bool`, so an admin correcting a typo in a
director's `role` without resending `isDirector: true` silently demoted them — destroying
exactly the flag that a partial unique index exists to protect, and that the OPT directory
spec calls "the dependable way to find the director". JSON omission and an explicit
`false` were indistinguishable by the time the request record was materialised.

The same shape is latent in `eventos-service`. `MoneyRequest.Amount` is a non-nullable
`decimal`, reached through both `UpdateCandidatoRequest.MontoEstimado` and
`UpdateMontoFinalRequest.MontoFinal`. It causes no harm today only because
`MoneyRequestValidator` requires `GreaterThan(0)`, which happens to reject the zero an
omitted field would produce. That is an accident of a domain rule, not a design: the next
field whose valid range includes its own default will not be so lucky.

Two questions were tangled here — what `PATCH` means, and whether a field's absence can be
told apart from its zero value. Only the second one loses data.

## Decision

**A `PATCH` body carries the complete editable representation of the resource, and no
field may take an implicit default.**

1. **Full replace.** The body is the complete set of editable fields. What it contains is
   what the resource holds afterwards; an omitted *optional* field is cleared, not
   preserved. Callers read, modify, and write back the whole representation.
2. **No silent defaults in update requests.** Every property of an update request type
   whose type is a value type — `bool`, `int`, `decimal`, `Guid`, `DateTime`,
   `DateTimeOffset`, enums — is declared **nullable**. A field the domain requires is then
   made **required by its validator**: its omission is a 400, never a guessed value. A field
   the domain allows to be absent stays optional, and rule 1 applies unchanged — omitting it
   clears it. Nullability is what rule 2 is for; requiredness is the domain's call. Reference
   types already have this property for free: absent and explicit null both arrive as null,
   and the validator decides whether that is acceptable.
3. **Create requests may keep non-nullable value types** where the CLR default is also the
   domain default. Creation has no prior state to destroy. `CreateOptStaffRequest`
   keeping `IsDirector` as a plain `bool` is correct — a new person is not a director
   unless the caller says so.

Rule 2 is the one that prevents data loss. Rules 1 and 3 record what the code already
does, so that the next person does not have to infer it from a repository method.

The first optional value-type field on an update path is `UpdateServiceSupplierRequest`'s two
venue capacities: `int?` with no `.NotNull()`, because `null` is the domain's own "not
specified" and omitting a capacity is how it gets cleared.

## Consequences

- **`crm-service` is the first application, already implemented.**
  `UpdateOptStaffRequest.IsDirector` is `bool?` with a `.NotNull()` rule, and
  `UpdateStaff_Returns400_WhenIsDirectorIsMissing` fails without it.
- **`eventos-service` has one latent violator and no live bug.** A sweep of every
  `*Request.cs` in both services found three non-nullable value types:
  `AddCandidatoRequest.ProveedorId` and `CreateOptStaffRequest.IsDirector` are on create
  paths and are fine under rule 3; `MoneyRequest.Amount` is the only rule-2 violator.
  Making it `decimal?` with `.NotNull()` changes no behaviour, because `GreaterThan(0)`
  already rejects the default — it converts an accidental guard into an intended one.
  Do it the next time eventos is opened; the English domain rename (ADR-0008) is the
  natural moment.
- **The verb is semantically loose, and we accept that.** RFC 5789 describes `PATCH` as a
  set of changes; a full representation is properly `PUT`. Correcting it would break
  `frontend/src/api/client.ts` and the Bruno collection for no behavioural gain, so it does
  not justify a break on its own. If the eventos rename breaks those wire contracts anyway,
  that is the cheap moment to switch the verb.
- **Last-write-wins across the whole resource.** Two admins editing different fields of the
  same office overwrite each other rather than merging. Accepted while the admin population
  is small. If that stops holding, the answer is optimistic concurrency — an ETag or an
  `xmin` check — not partial updates, which narrow the window without closing it.
- **True partial updates are deferred, not rejected.** They need machinery to distinguish
  an absent field from an explicit null in System.Text.Json — an `Optional<T>` wrapper with
  a converter, or `JsonPatchDocument` — and no consumer has needed it. Revisit when a UI
  edits one field at a time, or when a client cannot read before it writes. Rule 2 holds
  either way, so adopting it now costs nothing later.
- **One field is carved out of rule 1, and it is named here rather than in a service file.**
  `event.visibility` (convocatorias spec `01` §1, 2026-09-15) is preserved when omitted from
  `PATCH /v1/events/{id}` instead of being cleared. The reason is the one kind of harm rule 1
  cannot cause anywhere else: the field decides whether a thing is visible to the public
  internet, and the event form has no control for it yet, so every unrelated edit — a corrected
  name, a shifted date — would unpublish an advertised event, silently and repeatedly. Rule 1
  assumes the caller can read the whole representation and write it back; a caller that does not
  know the field exists cannot. The carve-out ends when the form grows the control, and until then
  `UpdateEventDto.Visibility` is nullable precisely to make "not stated" representable.
  **It is one field, not a new semantics**, so this ADR is amended rather than superseded — but
  the next one takes the supersede, because two exceptions are a rule nobody wrote down.
- New services inherit this by default. A service that genuinely needs partial semantics
  supersedes this ADR rather than diverging quietly.
