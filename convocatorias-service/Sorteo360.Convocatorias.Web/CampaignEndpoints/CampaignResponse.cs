namespace Sorteo360.Convocatorias.Web.CampaignEndpoints;

/// <summary>
/// Una campaña tal como sale por la API. Lleva las dos cuentas porque toda pantalla que la
/// muestre necesita las dos, y porque <c>recipientCount</c> es exactamente el número que quien
/// lanza vuelve a mandar en <c>expectedRecipientCount</c>.
/// </summary>
public sealed record CampaignResponse
{
    public required Guid Id { get; init; }

    /// <summary>El evento que convoca. Nulo en un mensaje directo.</summary>
    public required Guid? EventId { get; init; }

    /// <summary>El asunto de un mensaje directo. Nulo en una campaña de evento.</summary>
    public string? Subject { get; init; }

    /// <summary>El texto de un mensaje directo. Nulo en una campaña de evento.</summary>
    public string? Message { get; init; }

    public required string TemplateKey { get; init; }

    public required string Language { get; init; }

    /// <summary><c>draft</c>, <c>sending</c>, <c>sent</c> o <c>cancelled</c>.</summary>
    public required string State { get; init; }

    /// <summary>Cuántos destinatarios lleva. Es el número que se manda al lanzar.</summary>
    public required int RecipientCount { get; init; }

    /// <summary>Cuántas personas quedaron fuera al seleccionar.</summary>
    public required int ExclusionCount { get; init; }

    /// <summary>Quién apretó lanzar, por su id de Keycloak. Nulo mientras siga en borrador.</summary>
    public Guid? LaunchedById { get; init; }

    public DateTimeOffset? LaunchedAt { get; init; }

    /// <summary>
    /// Cuándo dejó de quedar una fila pendiente. Lo estampa el worker de la entrega, así que
    /// sigue nulo justo después de lanzar.
    /// </summary>
    public DateTimeOffset? SentAt { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}
