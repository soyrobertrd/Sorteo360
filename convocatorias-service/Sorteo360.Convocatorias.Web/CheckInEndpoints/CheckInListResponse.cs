namespace Sorteo360.Convocatorias.Web.CheckInEndpoints;

/// <summary>
/// La lista de check-ins de la rifa (spec <c>09</c> §7): todas las filas, envueltas en
/// <c>items</c> y sin paginar.
/// </summary>
public sealed record CheckInListResponse
{
    public required IReadOnlyList<CheckInListItem> Items { get; init; }
}

/// <summary>
/// Una persona que entró al evento. <b>Lleva correo</b>, a diferencia de toda respuesta de la puerta:
/// es la identidad de la rifa, y solo la lee una cuenta de servicio.
/// </summary>
public sealed record CheckInListItem
{
    public required Guid RegistrationId { get; init; }

    /// <summary>La dirección recortada y en minúsculas.</summary>
    public required string EmailNormalized { get; init; }

    public required string FirstName { get; init; }

    public required string LastName { get; init; }

    public required string? Company { get; init; }

    /// <summary>El contacto del CRM de quien se inscribió, o nulo si su escritura al CRM está pendiente.</summary>
    public required Guid? ContactId { get; init; }

    /// <summary><c>invited</c>, <c>referred</c> o <c>walk_in</c>, como en la lista de participantes.</summary>
    public required string Origin { get; init; }

    public required DateTimeOffset CheckedInAt { get; init; }
}
