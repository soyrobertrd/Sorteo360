namespace Sorteo360.Convocatorias.Core.Reconciliation;

// Las filas de la pantalla de reconciliación (spec 07 §6). Son proyecciones de lectura hechas para
// esa pantalla y salen tal cual por la API: una copia en Web sería idéntica campo por campo. Cada
// una lleva su evento cuando tiene uno, y ninguna trae el nombre del evento (07 §7).

/// <summary>
/// Los cinco conteos. <see cref="UncancelledNotices"/> es nulo cuando events-service no contestó:
/// es el único que depende de él, y los otros cuatro no tienen por qué caerse con él.
/// </summary>
public sealed record ReconciliationCounts(
    int PendingRegistrationSync,
    int PendingUnsubscribes,
    int DetailsDiffer,
    int StrandedCampaigns,
    int? UncancelledNotices);

/// <summary>
/// Una inscripción que espera a una persona: pendiente de sincronizar con el CRM (§6.1), o con una
/// ficha que no coincide. Las dos comparten forma porque comparten pantalla.
/// </summary>
public sealed record RegistrationReviewRow(
    Guid Id,
    Guid EventId,
    string FirstName,
    string LastName,
    string Email,
    string Reference,
    Guid? CrmContactId,
    DateTimeOffset? CrmSyncFailedAt,
    bool ContactDetailsDiffer,
    DateTimeOffset? DetailsReviewedAt,
    Guid? DetailsReviewedById,
    DateTimeOffset CreatedAt);

/// <summary>Una baja que el CRM todavía no sabe (spec <c>07</c> §6.2), con la campaña del enlace.</summary>
/// <remarks><c>EventId</c> es nulo cuando la campaña del enlace fue un mensaje directo.</remarks>
public sealed record PendingUnsubscribeRow(
    Guid RecipientId,
    Guid CampaignId,
    Guid? EventId,
    Guid ContactId,
    string FirstName,
    string LastName,
    string Email,
    DateTimeOffset UnsubscribedAt);

/// <summary>
/// Alguien que la selección dejó fuera de una campaña (spec <c>07</c> §6.4): la copia del nombre, el
/// código del motivo y cuándo. El español de cada código lo pone quien lo muestra.
/// </summary>
public sealed record CampaignExclusionRow(
    Guid Id,
    Guid ContactId,
    string FirstName,
    string LastName,
    string? Email,
    string Reason,
    DateTimeOffset ExcludedAt);

/// <summary>
/// Una campaña varada (spec <c>07</c> §6.3). El reparto de sus filas es la prueba: los números que
/// no cierran están acá en vez de ir a buscarlos.
/// </summary>
/// <remarks><c>EventId</c> es nulo en un mensaje directo.</remarks>
public sealed record StrandedCampaignRow(
    Guid CampaignId,
    Guid? EventId,
    string TemplateKey,
    DateTimeOffset LaunchedAt,
    RecipientStatusSplit Recipients);

/// <summary>Las filas de una campaña por estado de entrega.</summary>
public sealed record RecipientStatusSplit(int Pending, int HandedOff, int Failed, int Suppressed);

/// <summary>
/// Un evento cancelado con inscritos a los que nadie avisó (spec <c>07</c> §6.5). La acción es
/// volver a correr el aviso, que no le escribe a nadie dos veces.
/// </summary>
public sealed record UncancelledNoticeRow(Guid EventId, DateTimeOffset? CancelledAt, int Unnotified);
