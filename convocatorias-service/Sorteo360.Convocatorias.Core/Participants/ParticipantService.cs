using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.Notifications;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Participants;

/// <inheritdoc />
/// <remarks>
/// <b>La unión corre en memoria, no en SQL.</b> Un evento tiene cientos de personas, pocos miles a
/// lo sumo, y la búsqueda sin tildes y el orden sobre la fila ya unida no se pueden escribir contra
/// dos tablas antes de unirlas. Si un evento llegara a decenas de miles, esto es lo que cambia.
/// </remarks>
public sealed class ParticipantService : IParticipantService
{
    private readonly IParticipantRepository _repository;
    private readonly IDeliveryStatusReader _delivery;

    public ParticipantService(IParticipantRepository repository, IDeliveryStatusReader delivery)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(delivery);

        _repository = repository;
        _delivery = delivery;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<Participant>, BaseError>> ListAsync(
        Guid eventId,
        ParticipantFilter filter,
        CancellationToken cancellationToken)
    {
        var sources = await _repository.ReadSourcesAsync(eventId, filter.CampaignId, cancellationToken);

        if (sources.IsFailure)
            return sources.Error;

        IReadOnlyList<Participant> rows =
        [
            .. ParticipantMerge.Merge(sources.Value.Recipients, sources.Value.Registrations).Where(filter.Matches),
        ];

        return Result.Success<IReadOnlyList<Participant>, BaseError>(rows);
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<Participant>, BaseError>> ListPageAsync(
        Guid eventId,
        ParticipantFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var rows = await ListAsync(eventId, filter, cancellationToken);

        if (rows.IsFailure)
            return rows.Error;

        var paged = Page(rows.Value, page, pageSize);

        // Solo la lista de una campaña lleva la entrega: es la misma lectura que la de abajo, y una
        // prueba compara los dos cuerpos.
        return filter.CampaignId is null ? paged : await WithDeliveryAsync(paged, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<Maybe<PagedResult<Participant>>, BaseError>> ListCampaignRecipientsPageAsync(
        Guid campaignId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var sources = await _repository.ReadCampaignSourcesAsync(campaignId, cancellationToken);

        if (sources.IsFailure)
            return sources.Error;

        if (!sources.Value.TryGetValue(out var found))
            return Maybe<PagedResult<Participant>>.None;

        var filter = new ParticipantFilter(CampaignId: campaignId);
        IReadOnlyList<Participant> rows =
        [
            .. ParticipantMerge.Merge(found.Recipients, found.Registrations).Where(filter.Matches),
        ];

        return Maybe.From(await WithDeliveryAsync(Page(rows, page, pageSize), cancellationToken));
    }

    /// <summary>
    /// La entrega de las filas de una página, en una sola lectura a notification-service: una página
    /// son a lo sumo cien personas, y cien ids son una llamada (PGTI-177).
    /// </summary>
    private async Task<PagedResult<Participant>> WithDeliveryAsync(
        PagedResult<Participant> page,
        CancellationToken cancellationToken)
    {
        var known = await _delivery.ReadAsync(
            [.. page.Items.SelectMany(row => row.NotificationIds).Distinct()],
            cancellationToken);

        return page with
        {
            Items = [.. page.Items.Select(row => row with
            {
                DeliveryStatus = CampaignDeliveryStatus.Of(row.SendStatus, row.NotificationIds, known),
            })],
        };
    }

    /// <summary>Recorta la lista entera a la página pedida, con el tamaño topado.</summary>
    private static PagedResult<Participant> Page(IReadOnlyList<Participant> rows, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, IParticipantService.MaxPageSize);

        return new PagedResult<Participant>(
            [.. rows.Skip((page - 1) * pageSize).Take(pageSize)],
            rows.Count,
            page,
            pageSize);
    }
}
