using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Reconciliation;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Database;

/// <summary>
/// Las lecturas de la pantalla de reconciliación (spec <c>07</c> §6), todas locales, y el único
/// acto que no reintenta nada: dar por revisada una ficha que no coincide (§6.1).
/// <para>
/// Cada listado pagina con el tope del servicio, 100, y ordena con el id al final para que la
/// página dos nunca repita una fila de la uno.
/// </para>
/// </summary>
public interface IReconciliationRepository
{
    /// <summary>Los cuatro conteos locales; el quinto, <c>uncancelledNotices</c>, sale nulo.</summary>
    Task<Result<ReconciliationCounts, BaseError>> CountAsync(CancellationToken cancellationToken);

    /// <summary>
    /// El <c>needsAttention</c> del resumen de un evento (spec <c>07</c> §2): sus inscripciones
    /// pendientes de sincronizar, sus fichas que no coinciden sin revisar y sus campañas varadas,
    /// con las mismas definiciones que los cubos. Las bajas pendientes no entran: son de la persona y
    /// de todo el servicio, y sumarlas pondría el mismo número en cada evento sin que ninguno fuera
    /// su dueño.
    /// </summary>
    Task<Result<int, BaseError>> CountNeedsAttentionAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>Sin contacto del CRM y con el sello del fallo: pendiente (spec <c>06</c> §4.3).</summary>
    Task<Result<PagedResult<RegistrationReviewRow>, BaseError>> ListPendingRegistrationSyncAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>Una ficha que no coincide y que nadie revisó todavía (spec <c>07</c> §6.1).</summary>
    Task<Result<PagedResult<RegistrationReviewRow>, BaseError>> ListDetailsDifferAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Sella <c>details_reviewed_at</c> y a quien revisó. Una segunda llamada conserva el primer
    /// sello. <see cref="Maybe{T}.None"/> es un id que no existe; una ficha que coincide es
    /// <c>REGISTRATION_DETAILS_DO_NOT_DIFFER</c> y no se sella.
    /// </summary>
    Task<Result<Maybe<RegistrationReviewRow>, BaseError>> AcknowledgeDetailsAsync(
        Guid registrationId,
        Guid reviewerId,
        CancellationToken cancellationToken);

    /// <summary>Dadas de baja en el portal y sin el sello del CRM (spec <c>07</c> §6.2).</summary>
    Task<Result<PagedResult<PendingUnsubscribeRow>, BaseError>> ListPendingUnsubscribesAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Las exclusiones de una campaña (spec <c>07</c> §6.4). <see cref="Maybe{T}.None"/> es una
    /// campaña que no existe, que no es lo mismo que una sin exclusiones.
    /// </summary>
    Task<Result<Maybe<PagedResult<CampaignExclusionRow>>, BaseError>> ListExclusionsAsync(
        Guid campaignId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// En <c>sending</c>, con alguna fila <c>pending</c> y lanzadas hace más que el umbral de
    /// <see cref="ReconciliationSettings"/> (spec <c>07</c> §6.3).
    /// </summary>
    Task<Result<PagedResult<StrandedCampaignRow>, BaseError>> ListStrandedCampaignsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Cuántas inscripciones sin <c>cancellation_notified_at</c> tiene cada uno de estos eventos.
    /// Solo vienen los que tienen alguna; qué eventos están cancelados lo sabe events-service.
    /// </summary>
    Task<Result<IReadOnlyDictionary<Guid, int>, BaseError>> CountUnnotifiedByEventAsync(
        IReadOnlyCollection<Guid> eventIds,
        CancellationToken cancellationToken);
}
