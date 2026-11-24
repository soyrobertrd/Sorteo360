namespace Sorteo360.Convocatorias.Infrastructure.Database;

/// <summary>
/// Fila de <c>campaign_recipient</c>: una persona a la que esta campaña pretende escribirle,
/// con la copia de lo que se mandó al lado de su id.
/// <para>
/// Es única en <c>(campaign_id, contact_id)</c>, y la misma pareja es única en
/// <c>campaign_exclusion</c>: una persona está en una campaña una vez, como destinataria
/// <b>o</b> como exclusión y nunca como las dos, que es lo que mantiene honesta cualquier
/// cuenta sobre las dos tablas.
/// </para>
/// </summary>
internal sealed class CampaignRecipientData
{
    public Guid Id { get; set; }

    public Guid CampaignId { get; set; }

    public Guid ContactId { get; set; }

    public required string FirstName { get; set; }

    public required string LastName { get; set; }

    /// <summary>Lo que de verdad se mandó, no lo que diga el CRM el marzo que viene.</summary>
    public required string Email { get; set; }

    public required string SendStatus { get; set; }

    public string? FailureReason { get; set; }

    /// <summary>
    /// El id del mensaje que devolvió notification-service al aceptarlo (PGTI-177). Nulo en las
    /// filas anteriores, en las no entregadas y con el transporte de stub.
    /// </summary>
    public Guid? NotificationId { get; set; }

    /// <summary>
    /// SHA-256 del token de registro; el texto plano no se guarda nunca, así que un volcado de
    /// la base no entrega ningún enlace que funcione. Lo genera la entrega (spec <c>06</c> §2.1).
    /// </summary>
    public byte[]? RegistrationTokenHash { get; set; }

    /// <summary>SHA-256 del token de baja, separado del anterior (spec <c>06</c> §2.4).</summary>
    public byte[]? UnsubscribeTokenHash { get; set; }

    public DateTimeOffset? UnsubscribedAt { get; set; }

    /// <summary>Puesto significa que el CRM ya sabe de esa baja (spec <c>04</c> §3.1).</summary>
    public DateTimeOffset? UnsubscribeSyncedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public CampaignData? Campaign { get; set; }
}
