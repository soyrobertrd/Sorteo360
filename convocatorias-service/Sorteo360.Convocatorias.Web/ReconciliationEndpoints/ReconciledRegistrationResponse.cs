namespace Sorteo360.Convocatorias.Web.ReconciliationEndpoints;

/// <summary>
/// Una inscripción después de reintentar su sincronización con el CRM (spec <c>07</c> §6.1).
/// <para>
/// <c>crmSyncFailedAt</c> se conserva al sincronizar, como historia: lo que dice si está
/// pendiente es la pareja, no ese campo solo.
/// </para>
/// </summary>
public sealed record ReconciledRegistrationResponse
{
    public Guid Id { get; init; }

    public Guid EventId { get; init; }

    public string Reference { get; init; } = string.Empty;

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    /// <summary>El contacto del CRM al que quedó atada. Puesto significa sincronizada.</summary>
    public Guid? CrmContactId { get; init; }

    public DateTimeOffset? CrmSyncFailedAt { get; init; }

    /// <summary>Lo que escribió no coincide con la ficha del CRM; se revisa a mano.</summary>
    public bool ContactDetailsDiffer { get; init; }

    /// <summary>
    /// Lo que el CRM dijo de la ficha al reintentar, o nulo si ya estaba sincronizada y no se le
    /// preguntó. No se guarda: una persona suprimida puede inscribirse igual (spec <c>06</c> §6).
    /// </summary>
    public bool? CanBeEmailed { get; init; }
}
