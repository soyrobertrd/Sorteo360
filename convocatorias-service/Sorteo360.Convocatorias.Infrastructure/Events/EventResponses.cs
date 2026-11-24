namespace Sorteo360.Convocatorias.Infrastructure.Events;

/// <summary>
/// Una página del listado tal como la devuelve <c>GET /v1/events</c> de events-service.
/// </summary>
/// <param name="Items">Los eventos de esta página.</param>
/// <param name="Total">Cuántos casan con el filtro en total.</param>
/// <param name="Page">Página devuelta, 1-based.</param>
/// <param name="PageSize">Tamaño de página que aplicó events-service, topado por él en 100.</param>
public sealed record EventPageResponse(
    List<EventItemResponse> Items,
    int Total,
    int Page,
    int PageSize);

/// <summary>
/// Un evento de events-service, recortado a lo que convocatorias usa.
/// <para>
/// Es a propósito un subconjunto: el presupuesto, el costo total, los invitados previstos y el
/// contacto de la OPT viajan por el cable y se descartan acá. Ese es el costo aceptado de leer
/// los endpoints que ya existen en vez de pedir una proyección propia (spec <c>01</c> §4), y
/// dejarlos fuera de este tipo es lo que impide que lleguen al portal por descuido.
/// </para>
/// <para>
/// <c>Cities</c> es nullable por defensa, no porque falte: los dos endpoints de events-service las
/// traen. Un listado que algún día deje de emitirlas tiene que llegar acá como lista vacía y no
/// como excepción, porque una cartelera sin ciudades sigue siendo una cartelera.
/// </para>
/// </summary>
public sealed record EventItemResponse(
    Guid Id,
    string Name,
    string Type,
    string Status,
    string Language,
    string Visibility,
    DateOnly StartDate,
    DateOnly EndDate,
    string? ExternalUrl,
    string? Description,
    OptRefResponse? Opt,
    List<EventCityResponse>? Cities,
    DateTimeOffset? UpdatedAt = null);

/// <summary>
/// La referencia a la OPT del evento: el id que events-service guarda y el nombre que el CRM
/// resuelve al leer. <c>Name</c> nulo significa directorio caído, no evento sin organizador.
/// </summary>
public sealed record OptRefResponse(Guid Id, string? Name);

/// <summary>Una ciudad del recorrido, tal como la emite el detalle del evento.</summary>
public sealed record EventCityResponse(
    string Name,
    string Country,
    DateOnly? Date,
    int? DisplayOrder);
