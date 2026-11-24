namespace Sorteo360.Convocatorias.Core.Participants;

/// <summary>
/// Lo que la unión lee de un evento: sus destinatarios y sus inscripciones, tal como están en las
/// dos tablas. Todo es local; ninguna lectura de acá llama al CRM ni a events-service (spec
/// <c>07</c>, introducción).
/// </summary>
/// <param name="Recipients">Las filas de <c>campaign_recipient</c> que entran en la lista.</param>
/// <param name="Registrations">Todas las inscripciones del evento.</param>
public sealed record ParticipantSources(
    IReadOnlyList<RecipientSource> Recipients,
    IReadOnlyList<RegistrationSource> Registrations);

/// <summary>Una fila de <c>campaign_recipient</c>, con la copia del nombre y la dirección.</summary>
/// <param name="CampaignId">La campaña que la materializó.</param>
/// <param name="FirstName">El nombre copiado al seleccionar.</param>
/// <param name="LastName">El apellido copiado al seleccionar.</param>
/// <param name="Email">La dirección a la que se le escribió.</param>
/// <param name="SendStatus">Uno de <see cref="Campaigns.RecipientSendStatus"/>.</param>
/// <param name="CreatedAt">Cuándo se materializó, que ordena sus campañas.</param>
/// <param name="NotificationId">El id del mensaje en notification-service, si lo hay (PGTI-177).</param>
public sealed record RecipientSource(
    Guid CampaignId,
    string FirstName,
    string LastName,
    string Email,
    string SendStatus,
    DateTimeOffset CreatedAt,
    Guid? NotificationId = null);

/// <summary>Una fila de <c>registration</c>, con el nombre de quien le reenvió el enlace.</summary>
/// <param name="FirstName">Lo que escribió la persona.</param>
/// <param name="LastName">Lo que escribió la persona.</param>
/// <param name="Email">La dirección que escribió, tal cual.</param>
/// <param name="Company">Texto libre del formulario.</param>
/// <param name="Reference">El <c>MITUR-&lt;año&gt;-&lt;secuencia&gt;</c> impreso.</param>
/// <param name="RegisteredAt">Cuándo se inscribió.</param>
/// <param name="ReferredByName">
/// El nombre copiado en el destinatario cuyo enlace la trajo, o nulo si llegó sin enlace. Solo el
/// nombre: la atribución es el punto y la dirección de quien reenvió no le hace falta a nadie
/// (§3.2).
/// </param>
/// <param name="CrmSyncPending">El CRM no contestó al inscribirse y nadie la sincronizó todavía.</param>
/// <param name="DetailsDiffer">Lo que escribió no coincide con el contacto del CRM.</param>
/// <param name="CheckedInAt">Cuándo entró al evento, o nulo si no tiene check-in (spec <c>09</c> §6).</param>
public sealed record RegistrationSource(
    string FirstName,
    string LastName,
    string Email,
    string? Company,
    string Reference,
    DateTimeOffset RegisteredAt,
    string? ReferredByName,
    bool CrmSyncPending,
    bool DetailsDiffer,
    DateTimeOffset? CheckedInAt = null);
