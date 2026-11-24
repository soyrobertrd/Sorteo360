using Sorteo360.Convocatorias.Core.Shared;

namespace Sorteo360.Convocatorias.Core.Campaigns;

/// <summary>
/// Un correo que sale a una lista de personas, para un evento, en un idioma, desde una
/// plantilla. Recorre cuatro estados; sus destinatarios recorren otros cuatro, y los dos ejes
/// nunca se derivan uno del otro.
/// <para>
/// <b>Nadie aprueba una campaña.</b> Quien tiene <c>campaigns:launch</c> la lanza, en una sola
/// llamada, y sale. El cliente retiró el paso de aprobación el 2026-09-07 (ADR-0017), así que
/// no hay <c>QuoteDecision</c> que copiar del vertical de referencia: no queda decisión que
/// registrar.
/// </para>
/// <para>
/// <b>No hay tabla de lanzamientos.</b> <see cref="LaunchedById"/> y <see cref="LaunchedAt"/>
/// contestan quién lo apretó y cuándo, que es toda la pregunta que una auditoría de
/// lanzamiento iba a contestar, y una fila por campaña no se puede contradecir a sí misma como
/// sí se desacopla una tabla de historial de la columna de estado que tiene al lado.
/// </para>
/// </summary>
public sealed record Campaign
{
    public Guid Id { get; init; }

    /// <summary>
    /// El evento que se convoca. Una campaña es de uno solo; un evento tiene varias. Nulo en un
    /// mensaje directo, que no convoca a nada (ver <see cref="CampaignDirectMessage"/>).
    /// </summary>
    public Guid? EventId { get; init; }

    /// <summary>El asunto de un mensaje directo. Nulo en una campaña de evento.</summary>
    public string? Subject { get; init; }

    /// <summary>El texto de un mensaje directo, plano. Nulo en una campaña de evento.</summary>
    public string? Message { get; init; }

    /// <summary>Qué correo sale. Se congela en el lanzamiento (spec <c>03</c> §5).</summary>
    public string TemplateKey { get; init; }

    /// <summary>En qué idioma. Se congela en el lanzamiento, igual que la plantilla.</summary>
    public string Language { get; init; }

    /// <summary>Dónde está en el flujo. Ver <see cref="CampaignState"/>.</summary>
    public string State { get; init; }

    /// <summary>
    /// Quién apretó lanzar: el id de Keycloak, un Guid pelado y sin foreign key, porque la
    /// identidad es de Keycloak y no de esta base.
    /// </summary>
    public Guid? LaunchedById { get; init; }

    /// <summary>Cuándo se apretó.</summary>
    public DateTimeOffset? LaunchedAt { get; init; }

    /// <summary>
    /// Cuándo dejó de quedar una fila <c>pending</c>. Lo estampa el worker que drena los lotes
    /// (spec <c>04</c> §1), nunca la petición de lanzamiento. El hueco entre este momento y
    /// <see cref="LaunchedAt"/> es exactamente la entrega, que es un número que vale la pena
    /// poder leer.
    /// </summary>
    public DateTimeOffset? SentAt { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public Guid CreatedById { get; init; }

    public Guid UpdatedById { get; init; }

    public Campaign(
        Guid id,
        Guid? eventId,
        string templateKey,
        string language,
        string state,
        Guid? launchedById,
        DateTimeOffset? launchedAt,
        DateTimeOffset? sentAt,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        Guid createdById,
        Guid updatedById,
        string? subject = null,
        string? message = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        ArgumentException.ThrowIfNullOrWhiteSpace(state);

        if (!AllowedLanguage.All.Contains(language))
            throw new ArgumentException($"Idioma no admitido: '{language}'.", nameof(language));

        if (!CampaignState.All.Contains(state))
            throw new ArgumentException($"Estado no admitido: '{state}'.", nameof(state));

        Id = id;
        EventId = eventId;
        Subject = subject;
        Message = message;
        TemplateKey = templateKey;
        Language = language;
        State = state;
        LaunchedById = launchedById;
        LaunchedAt = launchedAt;
        SentAt = sentAt;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        CreatedById = createdById;
        UpdatedById = updatedById;
    }

    /// <summary>
    /// Admite cambios: plantilla, idioma, destinatarios y subidas. Es la guarda entera en la
    /// que se apoyan la edición, la selección y las cargas de la spec <c>05</c>.
    /// </summary>
    public bool IsDraft => State == CampaignState.Draft;

    /// <summary>Se está entregando: de solo lectura, y todavía se puede cancelar.</summary>
    public bool IsSending => State == CampaignState.Sending;

    /// <summary>Ya no se mueve más.</summary>
    public bool IsTerminal => CampaignState.IsTerminal(State);
}
