namespace Sorteo360.Events.Web.EventEndpoints;

public record CreateEventRequest
{
    public string? Type { get; init; }
    public string? Name { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }

    /// <summary>Idioma en el que corre el evento. Omitirlo lo crea en <c>es</c>.</summary>
    public string? Language { get; init; }

    /// <summary>
    /// <c>public</c> o <c>private</c>. Opcional aunque la columna no lo sea: omitirlo crea el
    /// evento <c>private</c>. Exigirlo rompería el formulario de eventos, que todavía no tiene
    /// control de visibilidad, y equivocarse cuesta un PATCH.
    /// </summary>
    public string? Visibility { get; init; }

    /// <summary>Trae el montaje del salón, uno de <c>SeatingLayout.All</c>.</summary>
    /// <remarks>Es opcional para todo tipo de evento: omitirlo crea el evento sin montaje del salón.</remarks>
    public string? SeatingLayout { get; init; }

    public string? ExternalUrl { get; init; }
    public string? Description { get; init; }
    public int? InvitedCount { get; init; }

    /// <summary>
    /// Lo presupuestado para el evento, como monto y moneda. Opcional: un evento puede planificarse
    /// antes de tener cifra.
    /// </summary>
    public MoneyRequest? EstimatedBudget { get; init; }

    public string? City { get; init; }
    public string? Country { get; init; }

    /// <summary>Id IANA de la zona horaria de la primera ciudad, como <c>America/Chicago</c>.</summary>
    /// <remarks>Omitirlo crea la primera ciudad en la hora de Santo Domingo.</remarks>
    public string? TimeZone { get; init; }
}
