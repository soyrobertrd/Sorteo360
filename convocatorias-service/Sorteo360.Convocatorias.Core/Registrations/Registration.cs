namespace Sorteo360.Convocatorias.Core.Registrations;

/// <summary>
/// Una persona inscrita en un evento. <b>Una fila por persona y por evento</b>, nunca por
/// campaña: quien recibió dos campañas del mismo evento no puede inscribirse dos veces
/// (spec <c>06</c> §4).
/// <para>
/// <b>No hay columna de estado.</b> Inscrito es que la fila exista, y los dos estados de
/// sincronización con el CRM salen de la pareja de columnas anulables de abajo. Un tercer estado
/// sería inventado en vez de observado.
/// </para>
/// <para>
/// <b>Y no guarda el token.</b> El token vive en <c>campaign_recipient</c>, y solo como hash;
/// lo que queda acá es <see cref="CampaignRecipientId"/>, que es a quién trajo el enlace. Un
/// volcado de esta tabla no entrega ni un enlace que funcione (spec <c>06</c> §2.1).
/// </para>
/// </summary>
public sealed record Registration
{
    public Guid Id { get; init; }

    /// <summary>El grano. Único junto con <see cref="EmailNormalized"/> (spec <c>06</c> §4.1).</summary>
    public Guid EventId { get; init; }

    /// <summary>
    /// El destinatario cuyo enlace la trajo. <b>Anulable a propósito</b>: quien llega desde la
    /// cartelera pública sin haber recibido ningún correo no tiene ninguno, y esa es la mitad
    /// del camino que la spec <c>06</c> §1.1 se negó a partir en dos endpoints.
    /// <para>
    /// En un evento público es además lo único que vuelve representable un reenvío: el token se
    /// diseñó para pasar de mano en mano, así que anotar a quién se lo mandamos es la diferencia
    /// entre una atribución y un misterio.
    /// </para>
    /// </summary>
    public Guid? CampaignRecipientId { get; init; }

    public string FirstName { get; init; }

    public string LastName { get; init; }

    /// <summary>Lo que la persona escribió, tal cual.</summary>
    public string Email { get; init; }

    /// <summary>
    /// La clave de unicidad: recortada y en minúsculas, y nada más. Es a propósito el mismo
    /// subconjunto conservador que usa el deduplicado de las cargas (spec <c>05</c>): sin quitar
    /// puntos de gmail y sin tratar <c>+etiqueta</c>, porque cada regla de más es una persona
    /// distinta a la que le negamos su inscripción.
    /// </summary>
    public string EmailNormalized { get; init; }

    public string? Phone { get; init; }

    /// <summary>Texto libre. No se resuelve contra ninguna empresa del CRM (spec <c>06</c> §4.1).</summary>
    public string? Company { get; init; }

    public string? JobTitle { get; init; }

    public string? Country { get; init; }

    /// <summary>
    /// <c>MITUR-&lt;año&gt;-&lt;secuencia&gt;</c>. Corto, humano, se imprime en la escarapela y se
    /// dicta por teléfono. <b>No autoriza nada</b>: quien lo tenga no puede hacer nada con él
    /// (spec <c>06</c> §2.3).
    /// </summary>
    public string Reference { get; init; }

    /// <summary>
    /// 32 bytes de azar en base64url. Es lo que codifica el QR y la clave por la que responde su
    /// URL. <b>Nunca se enseña como texto</b>: juntarlo con <see cref="Reference"/> daría o una
    /// escarapela ilegible o una credencial adivinable.
    /// </summary>
    public string QrCode { get; init; }

    /// <summary>
    /// Lo que escribió no coincide con el contacto que ya está en el CRM (spec <c>06</c> §4.2).
    /// Es lo que <c>07</c> saca como lista a revisar a mano, con el <c>PATCH</c> que ya existe:
    /// no sabemos si es una corrección o una errata, así que el CRM no se toca solo.
    /// </summary>
    public bool ContactDetailsDiffer { get; init; }

    /// <summary>
    /// El contacto del CRM al que quedó atada. <b>Puesto significa sincronizada</b>
    /// (spec <c>06</c> §4.3), lo haya creado esta inscripción o lo hubiera ya.
    /// </summary>
    public Guid? CrmContactId { get; init; }

    /// <summary>
    /// Cuándo el CRM no contestó. <b>Puesto y sin <see cref="CrmContactId"/> significa
    /// pendiente</b> (spec <c>06</c> §4.3).
    /// <para>
    /// Los dos a la vez es legal y no se guarda contra ello: un reintento que sale bien deja el
    /// sello atrás como historia, y la lectura de lo pendiente ya lo excluye porque pregunta por
    /// los dos campos y no por este solo.
    /// </para>
    /// </summary>
    public DateTimeOffset? CrmSyncFailedAt { get; init; }

    /// <summary>
    /// Cuándo se le avisó que el evento se canceló (spec <c>06</c> §4.7). <b>No es una columna de
    /// estado</b>: registra una acción que tomamos y que nada más guarda, y es lo que vuelve
    /// idempotente el aviso. Fuera del constructor a propósito: una inscripción nueva nunca llega
    /// avisada, y solo la escribe el aviso.
    /// </summary>
    public DateTimeOffset? CancellationNotifiedAt { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public Registration(
        Guid id,
        Guid eventId,
        Guid? campaignRecipientId,
        string firstName,
        string lastName,
        string email,
        string emailNormalized,
        string? phone,
        string? company,
        string? jobTitle,
        string? country,
        string reference,
        string qrCode,
        bool contactDetailsDiffer,
        Guid? crmContactId,
        DateTimeOffset? crmSyncFailedAt,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(emailNormalized);
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(qrCode);

        Id = id;
        EventId = eventId;
        CampaignRecipientId = campaignRecipientId;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        EmailNormalized = emailNormalized;
        Phone = phone;
        Company = company;
        JobTitle = jobTitle;
        Country = country;
        Reference = reference;
        QrCode = qrCode;
        ContactDetailsDiffer = contactDetailsDiffer;
        CrmContactId = crmContactId;
        CrmSyncFailedAt = crmSyncFailedAt;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>Nombre completo, tal como lo escribió.</summary>
    public string FullName => $"{FirstName} {LastName}";

    /// <summary>Llegó por un enlace de invitación y no desde la cartelera.</summary>
    public bool CameFromAnInvitation => CampaignRecipientId is not null;
}
