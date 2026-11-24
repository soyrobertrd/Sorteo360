namespace Sorteo360.Convocatorias.Infrastructure.Database;

/// <summary>
/// Fila de <c>registration</c>: una persona inscrita en un evento.
/// <para>
/// Única en <c>(event_id, email_normalized)</c>, <b>en la base y no en memoria</b> (spec
/// <c>06</c> §3 y §4.1). Es lo único de esta spec que la comprobación previa no puede sostener:
/// dos peticiones simultáneas con la misma dirección pasan las dos por cualquier <c>SELECT</c>
/// que se haga antes.
/// </para>
/// </summary>
internal sealed class RegistrationData
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    /// <summary>Nula en quien llegó desde la cartelera sin ninguna invitación (§1.1).</summary>
    public Guid? CampaignRecipientId { get; set; }

    public required string FirstName { get; set; }

    public required string LastName { get; set; }

    public required string Email { get; set; }

    /// <summary>La clave de unicidad. Recorte y minúsculas, nada más.</summary>
    public required string EmailNormalized { get; set; }

    public string? Phone { get; set; }

    /// <summary>Texto libre: no apunta a ninguna empresa del CRM.</summary>
    public string? Company { get; set; }

    public string? JobTitle { get; set; }

    public string? Country { get; set; }

    /// <summary><c>MITUR-&lt;año&gt;-&lt;secuencia&gt;</c>, el que se imprime (§2.3).</summary>
    public required string Reference { get; set; }

    /// <summary>32 bytes en base64url, único. Lo que codifica el QR (§2.3).</summary>
    public required string QrCode { get; set; }

    /// <summary>Lo que escribió no coincide con el contacto que ya estaba en el CRM (§4.2).</summary>
    public bool ContactDetailsDiffer { get; set; }

    /// <summary>Puesta significa sincronizada con el CRM (§4.3).</summary>
    public Guid? CrmContactId { get; set; }

    /// <summary>Puesta y sin <see cref="CrmContactId"/> significa pendiente de sincronizar (§4.3).</summary>
    public DateTimeOffset? CrmSyncFailedAt { get; set; }

    public DateTimeOffset? CancellationNotifiedAt { get; set; }

    /// <summary>
    /// Cuándo alguien revisó una ficha que no coincide (spec <c>07</c> §6.1). Puesta, la fila sale
    /// del cubo aunque <see cref="ContactDetailsDiffer"/> siga en verdadero: el arreglo es del CRM.
    /// </summary>
    public DateTimeOffset? DetailsReviewedAt { get; set; }

    /// <summary>Id de Keycloak de quien la revisó, sin foreign key.</summary>
    public Guid? DetailsReviewedById { get; set; }

    /// <summary>
    /// Cuándo se admitió en la puerta a quien la tiene (spec <c>09</c> §2). Nula es que no vino, o
    /// que todavía no llegó. Es la asistencia que lee la ficha del contacto (PGTI-170).
    /// </summary>
    public DateTimeOffset? CheckedInAt { get; set; }

    /// <summary>Id de Keycloak de quien hizo el check-in. Nula exactamente cuando lo es <see cref="CheckedInAt"/>.</summary>
    public Guid? CheckedInById { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public CampaignRecipientData? CampaignRecipient { get; set; }
}
