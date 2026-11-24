using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Events;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Reconciliation;

/// <inheritdoc />
public sealed class ReconciliationService : IReconciliationService
{
    /// <summary>El mismo tope de página que los listados locales.</summary>
    private const int MaxPageSize = 100;

    private readonly IReconciliationRepository _repository;
    private readonly IEventDirectory _events;

    public ReconciliationService(IReconciliationRepository repository, IEventDirectory events)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(events);

        _repository = repository;
        _events = events;
    }

    /// <inheritdoc />
    public async Task<Result<ReconciliationCounts, BaseError>> CountAsync(CancellationToken cancellationToken)
    {
        var local = await _repository.CountAsync(cancellationToken);

        if (local.IsFailure)
            return local;

        var uncancelled = await UncancelledAsync(cancellationToken);

        // Solo la caída de events-service vuelve nulo el conteo; una avería de la base propia es
        // del servicio entero y sale como tal.
        if (uncancelled.IsFailure && uncancelled.Error is not EventsUnavailableError)
            return Result.Failure<ReconciliationCounts, BaseError>(uncancelled.Error);

        return local.Value with
        {
            UncancelledNotices = uncancelled.IsSuccess ? uncancelled.Value.Count : null
        };
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<UncancelledNoticeRow>, BaseError>> ListUncancelledNoticesAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        // Se pagina en memoria: son eventos cancelados con alguien sin avisar, un puñado, y el
        // orden por fecha de cancelación sale de events-service, no de esta base.
        return (await UncancelledAsync(cancellationToken)).Map(rows => new PagedResult<UncancelledNoticeRow>(
            [.. rows.Skip((page - 1) * pageSize).Take(pageSize)],
            rows.Count,
            page,
            pageSize));
    }

    private async Task<Result<IReadOnlyList<UncancelledNoticeRow>, BaseError>> UncancelledAsync(
        CancellationToken cancellationToken)
    {
        var cancelled = await _events.ListCancelledEventsAsync(cancellationToken);

        if (cancelled.IsFailure)
            return Result.Failure<IReadOnlyList<UncancelledNoticeRow>, BaseError>(cancelled.Error);

        if (cancelled.Value.Count == 0)
            return Result.Success<IReadOnlyList<UncancelledNoticeRow>, BaseError>([]);

        var unnotified = await _repository.CountUnnotifiedByEventAsync(
            [.. cancelled.Value.Select(cancelledEvent => cancelledEvent.Id)],
            cancellationToken);

        return unnotified.Map(counts => (IReadOnlyList<UncancelledNoticeRow>)cancelled.Value
            .Where(cancelledEvent => counts.ContainsKey(cancelledEvent.Id))
            .Select(cancelledEvent => new UncancelledNoticeRow(
                cancelledEvent.Id, cancelledEvent.CancelledAt, counts[cancelledEvent.Id]))
            .OrderByDescending(row => row.CancelledAt)
            .ThenBy(row => row.EventId)
            .ToList());
    }
}
