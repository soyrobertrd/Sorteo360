namespace Sorteo360.Convocatorias.Core.Campaigns;

/// <summary>
/// Una persona a la que esta campaña pretende escribirle. Una fila por contacto y por campaña,
/// y la misma persona no puede estar además como exclusión: es lo que mantiene honesta
/// cualquier cuenta sobre las dos tablas (spec <c>03</c> §1).
/// <para>
/// <b>Id y copia, las dos cosas a propósito.</b> <see cref="ContactId"/> es identidad; el
/// nombre y la dirección son lo que de verdad se mandó. Si alguien edita su correo en el CRM
/// el marzo que viene, el registro del envío de septiembre no se puede reescribir solo. Por
/// eso leer una campaña ya enviada no llama al CRM en absoluto.
/// </para>
/// </summary>
public sealed record CampaignRecipient
{
    public Guid Id { get; init; }

    public Guid CampaignId { get; init; }

    /// <summary>La identidad, que vive en el CRM.</summary>
    public Guid ContactId { get; init; }

    public string FirstName { get; init; }

    public string LastName { get; init; }

    /// <summary>
    /// La dirección a la que se escribió. Nunca es nula: la selección solo materializa a quien
    /// el CRM declara contactable, y sin dirección no lo es.
    /// </summary>
    public string Email { get; init; }

    /// <summary>Ver <see cref="RecipientSendStatus"/>.</summary>
    public string SendStatus { get; init; }

    /// <summary>
    /// Por qué se rechazó o se suprimió esta fila. Nulo mientras no haya pasado ninguna de las
    /// dos cosas.
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// Hash SHA-256 del token de registro. El texto plano se arma en el envío y no se vuelve a
    /// necesitar, así que un volcado de la base no entrega ningún enlace que funcione (spec
    /// <c>06</c> §2.1). Nulo hasta la entrega, que es quien lo genera.
    /// </summary>
    public byte[]? RegistrationTokenHash { get; init; }

    /// <summary>
    /// Hash del token de baja, separado del de registro a propósito: un token de registro
    /// público está pensado para reenviarse, así que compartirlo haría que el clic de quien
    /// recibe el reenvío diera de baja a la persona original (spec <c>06</c> §2.4).
    /// </summary>
    public byte[]? UnsubscribeTokenHash { get; init; }

    /// <summary>Cuándo hizo clic en el enlace de baja.</summary>
    public DateTimeOffset? UnsubscribedAt { get; init; }

    /// <summary>
    /// Cuándo se le empujó esa baja al CRM. Que esté puesto es lo que significa "el CRM ya lo
    /// sabe"; una baja sin sincronizar no es inocua, y por eso el lanzamiento las vacía todas
    /// antes de preguntar (spec <c>04</c> §3.1).
    /// </summary>
    public DateTimeOffset? UnsubscribeSyncedAt { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public CampaignRecipient(
        Guid id,
        Guid campaignId,
        Guid contactId,
        string firstName,
        string lastName,
        string email,
        string sendStatus,
        string? failureReason,
        byte[]? registrationTokenHash,
        byte[]? unsubscribeTokenHash,
        DateTimeOffset? unsubscribedAt,
        DateTimeOffset? unsubscribeSyncedAt,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(sendStatus);

        if (!RecipientSendStatus.All.Contains(sendStatus))
            throw new ArgumentException($"Estado de envío no admitido: '{sendStatus}'.", nameof(sendStatus));

        Id = id;
        CampaignId = campaignId;
        ContactId = contactId;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        SendStatus = sendStatus;
        FailureReason = failureReason;
        RegistrationTokenHash = registrationTokenHash;
        UnsubscribeTokenHash = unsubscribeTokenHash;
        UnsubscribedAt = unsubscribedAt;
        UnsubscribeSyncedAt = unsubscribeSyncedAt;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>Nombre completo, tal como se guardó en el momento del envío.</summary>
    public string FullName => $"{FirstName} {LastName}";

    /// <summary>Todavía se le espera escribir.</summary>
    public bool IsPending => SendStatus == RecipientSendStatus.Pending;
}
