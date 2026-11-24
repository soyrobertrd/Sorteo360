namespace Sorteo360.Convocatorias.Core.Campaigns;

/// <summary>
/// Alguien que entró en el filtro de selección y quedó fuera, con su motivo. Una fila por
/// persona y por campaña (spec <c>03</c> §6.1).
/// <para>
/// No son contadores en <c>campaign</c>, porque la pregunta es "seleccioné 240 y veo 227,
/// ¿quién falta?" y un número no la contesta. Y tampoco es un cuarto estado de destinatario,
/// para que "destinatario" siga significando "alguien a quien pretendíamos escribirle" y
/// cualquier cuenta sobre esa tabla siga siendo honesta.
/// </para>
/// <para>
/// <b>La supresión del momento de la entrega no escribe acá</b>, solo marca la fila de
/// <see cref="CampaignRecipient"/>. Un sitio por persona y por campaña, o toda cuenta reporta
/// doble.
/// </para>
/// </summary>
public sealed record CampaignExclusion
{
    public Guid Id { get; init; }

    public Guid CampaignId { get; init; }

    public Guid ContactId { get; init; }

    public string FirstName { get; init; }

    public string LastName { get; init; }

    /// <summary>
    /// La dirección que traía el CRM, si traía alguna. Nula justamente cuando el motivo es
    /// <see cref="ExclusionReason.NoEmail"/>.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>Ver <see cref="ExclusionReason"/>.</summary>
    public string Reason { get; init; }

    public DateTimeOffset ExcludedAt { get; init; }

    public CampaignExclusion(
        Guid id,
        Guid campaignId,
        Guid contactId,
        string firstName,
        string lastName,
        string? email,
        string reason,
        DateTimeOffset excludedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (!ExclusionReason.All.Contains(reason))
            throw new ArgumentException($"Motivo de exclusión no admitido: '{reason}'.", nameof(reason));

        Id = id;
        CampaignId = campaignId;
        ContactId = contactId;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        Reason = reason;
        ExcludedAt = excludedAt;
    }

    /// <summary>Nombre completo, tal como lo traía el CRM al seleccionar.</summary>
    public string FullName => $"{FirstName} {LastName}";
}
