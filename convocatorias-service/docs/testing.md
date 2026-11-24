# convocatorias-service: test fixtures

> Moved from `convocatorias-service/CLAUDE.md` (PGTI-212), text unchanged except headings. A section named
> with "see ... below" may now live in a sibling file in this folder: `grep -n '^## ' convocatorias-service/docs/*.md` finds it.
> Spec numbers (`03`, `04` §1, ...) are files in `docs/specs/convocatorias/`.

## The fixtures

`Tests.Integration` carries the fixtures every later spec asserts through:
`ConvocatoriasApiFactory` and `ConvocatoriasApiDbFactory` (Testcontainers), `TestAuthHandler`,
and the three neighbours replaced by WireMock servers — `CrmStub`, `EventsStub`,
`NotificationStub`, all on `NeighbourStub`, which gives each one `GoDown()` and `GoSlow()`.
The neighbour is substituted, never the typed client: what has to run is the whole chain,
client plus chassis resilience plus cache plus degradation. **`CrmStub` answers the shapes the real
crm-service answers**, down to the `409` carrying its contact in an `existingContact` ProblemDetails
extension: a double that invents its own shape hides exactly the bug it exists to catch. It also
reports **`CallsSinceReset`**, its requests in arrival order, because the launch path's guarantee is
an order and no count can prove one; `FailOptOut()` and `FailMailability()` take down one route at a
time, since a whole neighbour down stops the launch at the first step and the second never runs.
`Tests.Unit` covers Core logic with no I/O: the seeded template copy, the placeholder rules, the
substitution engine, both campaign state catalogues, the upload policy, column map, row classifier
and spreadsheet reader, and the registration codes plus the rule that decides whether an event still
takes registrations. **`RegistrationPortalFactory` replaces the whole `ILoggerFactory`** rather than
hanging a sink on Serilog: Serilog is configured on the static `Log.Logger`, which every test class
shares while xUnit runs them in parallel, so a global sink would be overwritten by the next factory
built and the "no token in a log line" assertion would pass without having looked at anything.
