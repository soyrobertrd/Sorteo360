using System.Collections.Concurrent;
using System.Text.Json;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Sorteo360.Convocatorias.Tests.Integration.Infrastructure;

/// <summary>
/// events-service visto desde convocatorias-service: el detalle de un evento, el listado
/// filtrado por visibilidad que alimenta el portal y el alta de un ServiceSupplier de la carga de
/// proveedores.
/// <para>
/// Sirve la respuesta entera de events-service, presupuesto y aforo previsto incluidos, aunque
/// convocatorias no los use. Es a propósito: lo que prueba que el portal no los republica es que
/// hayan estado ahí para republicarse.
/// </para>
/// </summary>
public sealed class EventsStub : NeighbourStub
{
    private const string EventsPath = "/v1/events";
    private const string ServiceSuppliersPath = "/v1/service-suppliers";

    /// <summary>Un evento público y vivo, el que usan los tests que no van sobre eventos.</summary>
    public static readonly Guid EventId = Guid.Parse("4e9a5b6c-8d3f-4012-9c2d-3e4f50617283");

    public const string EventName = "Roadshow Madrid 2026";
    public const string EventLanguage = "es";
    public const string PublicVisibility = "public";
    public const string PrivateVisibility = "private";

    /// <summary>La OPT que organiza, resuelta por events-service contra el CRM.</summary>
    public const string OptName = "OPT Madrid";

    /// <summary>El aforo interno de la OPT. Nunca debería salir por el portal.</summary>
    public const int InvitedCount = 240;

    /// <summary>
    /// El <c>updatedAt</c> de todo evento servido. De un cancelado es cuándo se canceló: después
    /// de cancelar, events-service no deja editarlo.
    /// </summary>
    public static readonly DateTimeOffset UpdatedAt = new(2026, 9, 20, 10, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// El código con el que events-service rechaza una empresa que ya es proveedor en esa
    /// categoría (<c>ServiceSupplierErrors.CompanyAlreadyRegistered</c>).
    /// </summary>
    public const string CompanyAlreadyRegisteredCode = "SUPPLIER_COMPANY_ALREADY_REGISTERED";

    /// <summary>
    /// Las fichas vivas. Sobreviven a <c>ServeDefaults</c> por lo mismo que las empresas de
    /// <c>CrmStub</c>: un reenvío tiene que encontrar lo que ya quedó escrito.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, StubServiceSupplier> _suppliers = new();

    /// <summary>
    /// Toma el alta y la consulta de choque juntas, como la transacción de events-service: dos altas
    /// a la vez no pueden pasar las dos el chequeo.
    /// </summary>
    private readonly Lock _suppliersLock = new();

    public EventsStub() => ConfigureDefaults();

    /// <summary>Siembra una ficha de proveedor de esa empresa, con su categoría principal y sus secundarias.</summary>
    public void RememberServiceSupplier(Guid companyId, string category, params string[] secondaryCategories)
    {
        var supplier = new StubServiceSupplier(Guid.NewGuid(), companyId, category, secondaryCategories);
        _suppliers[supplier.Id] = supplier;
    }

    /// <summary>
    /// Los cuerpos de las altas de ServiceSupplier que recibió desde el último corte, en orden de
    /// llegada. Es con lo que se comprueba qué le mandó la carga de proveedores a events-service.
    /// </summary>
    public IReadOnlyList<JsonElement> ServiceSupplierCreateBodies =>
    [
        .. RequestsSinceReset
            .Select(entry => entry.RequestMessage!)
            .Where(request => string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase)
                && request.Path == ServiceSuppliersPath)
            .OrderBy(request => request.DateTime)
            .Select(request => JsonSerializer.Deserialize<JsonElement>(request.Body ?? "{}")),
    ];

    /// <inheritdoc />
    protected override void ConfigureDefaults() =>
        ServeEvents(new StubEvent(EventId, EventName, PublicVisibility, EventLanguage, StubEventStatus.InProgress));

    /// <summary>
    /// Publica estos eventos y descarta lo anterior. El listado solo trae los públicos y
    /// vivos, como el filtro que usa el portal; el detalle resuelve cualquiera por id.
    /// </summary>
    public void ServeEvents(params StubEvent[] events)
    {
        ResetServer();

        Server
            .Given(Request.Create().WithPath(EventsPath).UsingGet())
            .AtPriority(1)
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new
                {
                    items = events.Where(IsListed).Select(e => ToItem(e, withDetailFields: false)),
                    total = events.Count(IsListed),
                    page = 1,
                    pageSize = 100,
                }));

        // Los cancelados, que es lo que pregunta la reconciliación (spec 07 §6.5).
        Server
            .Given(Request.Create().WithPath(EventsPath).WithParam("status", StubEventStatus.Cancelled).UsingGet())
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new
                {
                    items = events.Where(e => e.Status == StubEventStatus.Cancelled)
                        .Select(e => ToItem(e, withDetailFields: false)),
                    total = events.Count(e => e.Status == StubEventStatus.Cancelled),
                    page = 1,
                    pageSize = 100,
                }));

        foreach (var singleEvent in events)
        {
            Server
                .Given(Request.Create().WithPath($"{EventsPath}/{singleEvent.Id}").UsingGet())
                .AtPriority(1)
                .RespondWith(Response.Create()
                    .WithStatusCode(200)
                    .WithBodyAsJson(ToItem(singleEvent, withDetailFields: true)));
        }

        // El alta de un ServiceSupplier contesta 201 con el id nuevo, como la ruta de verdad, que
        // devuelve solo el Guid en el cuerpo. Una empresa que ya tiene alguna de las categorías del
        // alta, principal o secundaria, es el 409 con código de events-service (ADR-0034).
        Server
            .Given(Request.Create().WithPath(ServiceSuppliersPath).UsingPost())
            .AtPriority(1)
            .RespondWith(Response.Create().WithCallback(request =>
            {
                var body = JsonSerializer.Deserialize<JsonElement>(request.Body ?? "{}");
                var secondaries = body.TryGetProperty("secondaryCategories", out var list) && list.ValueKind == JsonValueKind.Array
                    ? [.. list.EnumerateArray().Select(category => category.GetString()!)]
                    : Array.Empty<string>();
                var supplier = new StubServiceSupplier(
                    Guid.NewGuid(),
                    body.GetProperty("companyId").GetGuid(),
                    body.GetProperty("category").GetString()!,
                    secondaries);

                lock (_suppliersLock)
                {
                    if (_suppliers.Values.Any(held => held.CompanyId == supplier.CompanyId && held.Overlaps(supplier)))
                        return Json(409, new { status = 409, code = CompanyAlreadyRegisteredCode });

                    _suppliers[supplier.Id] = supplier;
                    return Json(201, supplier.Id);
                }
            }));

        // El listado de las fichas de una empresa, con la forma entera de una fila del listado de
        // events-service.
        Server
            .Given(Request.Create().WithPath(ServiceSuppliersPath).WithParam("companyId").UsingGet())
            .AtPriority(1)
            .RespondWith(Response.Create().WithCallback(request =>
            {
                var companyId = Guid.Parse(request.Query!["companyId"][0]);

                return Json(200, _suppliers.Values
                    .Where(supplier => supplier.CompanyId == companyId)
                    .Select(supplier => new
                    {
                        id = supplier.Id,
                        category = supplier.Category,
                        secondaryCategories = supplier.SecondaryCategories,
                        company = new { id = companyId, name = "Empresa del CRM", country = "República Dominicana", city = (string?)null, isActive = true },
                        auditoriumCapacity = (int?)null,
                        cocktailCapacity = (int?)null,
                        isHireable = true,
                        averageScore = (decimal?)null,
                        evaluationCount = 0,
                        createdAt = UpdatedAt,
                        updatedAt = UpdatedAt,
                    }));
            }));

        // Un id que events-service no conoce es 404, no una avería.
        Server
            .Given(Request.Create().WithPath($"{EventsPath}/*").UsingGet())
            .AtPriority(9)
            .RespondWith(Response.Create().WithStatusCode(404));
    }

    /// <summary>El listado de events-service filtra por visibilidad y por evento no terminado.</summary>
    private static bool IsListed(StubEvent singleEvent) =>
        singleEvent.Visibility == PublicVisibility
        && singleEvent.Status is StubEventStatus.Planning or StubEventStatus.InProgress;

    private static object ToItem(StubEvent singleEvent, bool withDetailFields) => new
    {
        id = singleEvent.Id,
        name = singleEvent.Name,
        visibility = singleEvent.Visibility,
        language = singleEvent.Language,
        status = singleEvent.Status,
        type = "Roadshow",
        startDate = singleEvent.StartDate.ToString("yyyy-MM-dd"),
        endDate = singleEvent.EndDate.ToString("yyyy-MM-dd"),
        externalUrl = "https://sorteo360.local/roadshow",
        // Lo que el portal no puede republicar. Viaja hasta acá porque convocatorias lee los
        // endpoints de events-service tal como son, sin proyección propia (spec 01 §4).
        invitedCount = InvitedCount,
        estimatedBudget = new { amount = 85000m, currency = "USD" },
        totalCost = new { amount = 91200m, currency = "USD" },
        opt = new { id = Guid.Parse("0a5e1b8c-4c1e-4f6b-9c2a-6d7e8f901234"), name = singleEvent.OptName },
        optContact = new { id = Guid.Parse("5b6c7d8e-9f01-4234-a567-89012345cdef"), name = "Ana Reyes" },
        description = withDetailFields ? singleEvent.Description : null,
        // Los dos endpoints de events-service traen ciudades; el listado las ganó junto con la
        // visibilidad, para que la cartelera pública pueda decir a dónde va cada roadshow.
        cities = (singleEvent.Cities ?? DefaultCities)
            .Select(city => new { name = city.Name, country = city.Country, date = city.Date, displayOrder = city.DisplayOrder }),
        updatedAt = UpdatedAt,
    };

    /// <summary>El recorrido que sirve un evento que no dice otra cosa.</summary>
    private static readonly StubCity[] DefaultCities =
    [
        new("Madrid", "España", "2026-06-12", 1),
    ];
}

/// <summary>Una ficha de proveedor que el stub tiene viva.</summary>
/// <param name="Id">El id de la ficha.</param>
/// <param name="CompanyId">La empresa del CRM.</param>
/// <param name="Category">La categoría principal.</param>
/// <param name="SecondaryCategories">Las otras categorías.</param>
public sealed record StubServiceSupplier(Guid Id, Guid CompanyId, string Category, IReadOnlyList<string> SecondaryCategories)
{
    /// <summary>Todas sus categorías, la principal primero.</summary>
    public IEnumerable<string> AllCategories => [Category, .. SecondaryCategories];

    /// <summary>Si comparte alguna categoría con otra ficha, que es lo que events-service rechaza.</summary>
    public bool Overlaps(StubServiceSupplier other) =>
        AllCategories.Intersect(other.AllCategories, StringComparer.OrdinalIgnoreCase).Any();
}

/// <summary>
/// Una ciudad del recorrido del evento simulado. De acá sale <c>{{lugar}}</c>, así que un evento
/// <b>sin</b> ninguna es el que deja ese marcador sin valor y rechaza el lanzamiento.
/// </summary>
/// <param name="Name">Nombre de la ciudad.</param>
/// <param name="Country">País.</param>
/// <param name="Date">El día que le toca, en ISO, o nulo si no se fijó.</param>
/// <param name="DisplayOrder">Su lugar en el recorrido.</param>
public sealed record StubCity(string Name, string Country, string? Date, int? DisplayOrder);

/// <summary>Los estados de un evento, tal como los emite events-service.</summary>
public static class StubEventStatus
{
    public const string Planning   = "planning";
    public const string InProgress = "in_progress";
    public const string Closed     = "closed";
    public const string Cancelled  = "cancelled";
}

/// <summary>
/// Un evento del events-service simulado. La visibilidad y el estado deciden si sale en el
/// listado del portal; el detalle por id lo resuelve igual, que es lo que separa "no es
/// público" de "no existe".
/// </summary>
/// <param name="Id">Su id.</param>
/// <param name="Name">Su nombre.</param>
/// <param name="Visibility">public o private.</param>
/// <param name="Language">El idioma en que corre.</param>
/// <param name="Status">Uno de <see cref="StubEventStatus"/>.</param>
/// <param name="OptName">
/// El nombre de la OPT que events-service resolvió. Nulo simula el directorio del CRM caído.
/// </param>
/// <param name="Description">El texto libre del evento.</param>
/// <param name="Cities">
/// El recorrido. Nulo sirve el de por omisión, una ciudad; una lista vacía sirve un evento sin
/// recorrido, que es el que deja <c>{{lugar}}</c> sin valor.
/// </param>
/// <param name="DaysUntilStart">
/// Cuántos días faltan para que empiece, contados desde hoy. <b>Relativo y no una fecha fija</b>
/// a propósito: el portal se niega a inscribir a nadie en un evento cuya última fecha ya pasó
/// (spec <c>06</c> §2.1), así que una constante en el calendario convertiría a todo evento de
/// prueba en uno terminado en cuanto esa fecha quedara atrás. Un número negativo sirve
/// justamente ese caso.
/// </param>
/// <param name="DurationInDays">Cuánto dura, para separar la fecha de inicio de la de fin.</param>
public sealed record StubEvent(
    Guid Id,
    string Name,
    string Visibility,
    string Language,
    string Status,
    string? OptName = EventsStub.OptName,
    string? Description = "Encuentro anual con la prensa especializada.",
    IReadOnlyList<StubCity>? Cities = null,
    int DaysUntilStart = 30,
    int DurationInDays = 2)
{
    /// <summary>Cuándo empieza, resuelto contra el día de hoy.</summary>
    public DateOnly StartDate => DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(DaysUntilStart);

    /// <summary>Cuándo termina, que es la fecha sobre la que el portal decide.</summary>
    public DateOnly EndDate => StartDate.AddDays(DurationInDays);
}
