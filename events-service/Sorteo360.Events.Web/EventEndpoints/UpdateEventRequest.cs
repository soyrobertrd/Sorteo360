namespace Sorteo360.Events.Web.EventEndpoints;

public record UpdateEventRequest
{
    public string? Name { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }

    /// <summary>
    /// Idioma en el que corre el evento. <b>Obligatorio</b>: este PATCH reemplaza la
    /// representación editable completa, así que omitirlo lo borraría, y un evento sin idioma no
    /// tiene plantilla con la que convocar. Mandarlo nulo es 400 con código propio.
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    /// <c>public</c> o <c>private</c>. El único campo opcional que omitir <b>no</b> borra: sin
    /// valor, el evento conserva la visibilidad que tenía. Ver <c>UpdateEventDto</c>.
    /// </summary>
    public string? Visibility { get; init; }

    /// <summary>Trae el montaje del salón, uno de <c>SeatingLayout.All</c>.</summary>
    /// <remarks>
    /// Omitirlo borra el montaje del salón, como a todo campo opcional de esta edición, porque el
    /// PATCH reemplaza la representación editable completa.
    /// </remarks>
    public string? SeatingLayout { get; init; }

    public string? ExternalUrl { get; init; }
    public string? Description { get; init; }
    public int? InvitedCount { get; init; }

    /// <summary>
    /// Lo presupuestado para el evento. Omitirlo lo borra, como a todo campo opcional de esta
    /// edición: el PATCH reemplaza la representación editable completa.
    /// </summary>
    public MoneyRequest? EstimatedBudget { get; init; }
}
