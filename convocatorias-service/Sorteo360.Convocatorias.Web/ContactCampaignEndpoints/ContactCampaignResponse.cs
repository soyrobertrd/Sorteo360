namespace Sorteo360.Convocatorias.Web.ContactCampaignEndpoints;

/// <summary>
/// Una campaña que tuvo a un contacto en su lista. Sin nombre del evento: es de events-service, y
/// quien muestra la fila lo resuelve allá por el id (CLAUDE.md raíz, ADR-0013).
/// </summary>
public sealed record ContactCampaignResponse
{
    public required Guid ContactId { get; init; }

    public required Guid CampaignId { get; init; }

    /// <summary>El evento que convoca. Nulo en un mensaje directo.</summary>
    public required Guid? EventId { get; init; }

    /// <summary>El asunto de un mensaje directo. Nulo en una campaña de evento.</summary>
    public string? Subject { get; init; }

    /// <summary>Cuándo se lanzó la campaña: la fecha del envío.</summary>
    public required DateTimeOffset LaunchedAt { get; init; }

    /// <summary><c>pending</c>, <c>handed_off</c>, <c>failed</c> o <c>suppressed</c>.</summary>
    public required string SendStatus { get; init; }

    /// <summary>Si se inscribió en el evento. Nulo sin evento: no había en qué inscribirse.</summary>
    public required bool? Registered { get; init; }

    /// <summary>
    /// <c>sent</c>, <c>delivered</c>, <c>bounced</c>, <c>spam_report</c> o <c>unsubscribed</c>
    /// (PGTI-177). Nulo si no se entregó al proveedor: no hay correo del que decir nada.
    /// </summary>
    public required string? DeliveryStatus { get; init; }
}
