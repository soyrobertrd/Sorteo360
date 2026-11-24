namespace Sorteo360.Convocatorias.Infrastructure.Database;

/// <summary>
/// Fila de <c>campaign</c>: un envío, para un evento, en un idioma, desde una plantilla.
/// <para>
/// <b>No hay tabla de lanzamientos al lado.</b> <c>launched_by_id</c> y <c>launched_at</c>
/// contestan quién lo apretó y cuándo, y una fila por campaña no se puede desacoplar de la
/// columna de estado que tiene al lado como sí se desacopla una tabla de historial paralela
/// (spec <c>03</c> §1).
/// </para>
/// </summary>
internal sealed class CampaignData
{
    public Guid Id { get; set; }

    /// <summary>Nulo en un mensaje directo, que no convoca a nada.</summary>
    public Guid? EventId { get; set; }

    /// <summary>El asunto de un mensaje directo; nulo en una campaña de evento.</summary>
    public string? Subject { get; set; }

    /// <summary>El texto de un mensaje directo; nulo en una campaña de evento.</summary>
    public string? Message { get; set; }

    public required string TemplateKey { get; set; }

    public required string Language { get; set; }

    public required string State { get; set; }

    /// <summary>Id de Keycloak, sin foreign key: la identidad no vive en esta base.</summary>
    public Guid? LaunchedById { get; set; }

    public DateTimeOffset? LaunchedAt { get; set; }

    /// <summary>Lo estampa el worker que drena los lotes, nunca la petición de lanzamiento.</summary>
    public DateTimeOffset? SentAt { get; set; }

    /// <summary>
    /// Los tres valores del evento, copiados dentro de la petición de lanzamiento. Nulos mientras
    /// la campaña sea un borrador, y los tres a la vez: ver <see cref="Core.Campaigns.CampaignEventValues"/>.
    /// </summary>
    public string? EventName { get; set; }

    public string? EventLocation { get; set; }

    public string? EventDates { get; set; }

    /// <summary>
    /// Desde dónde sale la campaña: la dirección de envío de la OPT de quien la lanzó, copiada en
    /// el lanzamiento (PGTI-168). Nula si salió desde la dirección general.
    /// </summary>
    public string? SenderEmail { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Guid CreatedById { get; set; }

    public Guid UpdatedById { get; set; }

    public ICollection<CampaignRecipientData> Recipients { get; set; } = [];

    public ICollection<CampaignExclusionData> Exclusions { get; set; } = [];
}
