using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Reconciliation;

/// <summary>
/// La parte de la reconciliación que no es local: el cubo de <c>uncancelledNotices</c> (spec
/// <c>07</c> §6.5), que cruza los eventos cancelados de events-service con las inscripciones sin
/// aviso de acá, y el resumen que lo cuenta junto a los otros cuatro.
/// </summary>
public interface IReconciliationService
{
    /// <summary>
    /// Los cinco conteos. Con events-service caído <c>uncancelledNotices</c> sale nulo y los otros
    /// cuatro siguen: un conteo que no se puede saber no tumba la pantalla entera.
    /// </summary>
    Task<Result<ReconciliationCounts, BaseError>> CountAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Una fila por evento cancelado con inscritos sin avisar, del más reciente al más viejo. Con
    /// events-service caído es <c>503 EVENTS_UNAVAILABLE</c>: una lista vacía mentiría.
    /// </summary>
    Task<Result<PagedResult<UncancelledNoticeRow>, BaseError>> ListUncancelledNoticesAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
