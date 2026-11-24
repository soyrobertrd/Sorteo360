using Sorteo360.Convocatorias.Core.Events;

namespace Sorteo360.Convocatorias.Web.PublicEndpoints;

/// <summary>
/// El recorte de un evento para el portal, en un solo sitio (spec <c>01</c> §5).
/// <para>
/// Lo usan la cartelera y las dos rutas de inscripción, que devuelven el mismo evento. Escrito
/// dos veces es como una copia gana un campo que la otra no tiene, y acá lo que se está
/// eligiendo es qué ve un desconocido: el presupuesto, el costo, los invitados previstos y el
/// contacto de la OPT no pueden salir por ninguna de las tres.
/// </para>
/// </summary>
internal static class PublicEventMapper
{
    /// <summary>Lo que lleva un evento en el listado.</summary>
    public static PublicEventResponse ToListItem(EventSummary summary, bool registrationOpen) => new()
    {
        RegistrationOpen = registrationOpen,
        Id          = summary.Id,
        Name        = summary.Name,
        Type        = summary.Type,
        Language    = summary.Language,
        StartDate   = summary.StartDate,
        EndDate     = summary.EndDate,
        ExternalUrl = summary.ExternalUrl,
        Cities      = ToCities(summary)
    };

    /// <summary>
    /// Lo mismo, más la descripción y el nombre de quien organiza. <paramref name="registrationOpen"/>
    /// lo pasa solo el detalle público; las rutas de inscripción y de la entrada no leen la ventana.
    /// </summary>
    public static PublicEventDetailResponse ToDetail(EventSummary summary, bool? registrationOpen = null) => new()
    {
        RegistrationOpen = registrationOpen,
        Id          = summary.Id,
        Name        = summary.Name,
        Type        = summary.Type,
        Language    = summary.Language,
        StartDate   = summary.StartDate,
        EndDate     = summary.EndDate,
        ExternalUrl = summary.ExternalUrl,
        Cities      = ToCities(summary),
        Description = summary.Description,
        Organizer   = summary.OptName
    };

    /// <summary>
    /// Las ciudades del evento, en el orden que trae el dueño. <c>DisplayOrder</c> viaja en la
    /// respuesta en vez de ordenarse acá: es events-service quien decide ese orden, y ordenar
    /// por nuestra cuenta sería una segunda opinión sobre un dato de otro.
    /// </summary>
    private static IReadOnlyList<PublicEventCityResponse> ToCities(EventSummary summary) =>
        summary.Cities
            .Select(city => new PublicEventCityResponse
            {
                Name         = city.Name,
                Country      = city.Country,
                Date         = city.Date,
                DisplayOrder = city.DisplayOrder
            })
            .ToArray();
}
