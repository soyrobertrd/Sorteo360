using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Events;
using Sorteo360.Convocatorias.Infrastructure.Neighbours;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Infrastructure.Events;

/// <inheritdoc cref="IEventDirectory" />
public sealed class EventDirectory : IEventDirectory
{
    private readonly IEventsClient _client;
    private readonly ILogger<EventDirectory> _logger;

    public EventDirectory(IEventsClient client, ILogger<EventDirectory> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(logger);

        _client = client;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<Maybe<EventSummary>, BaseError>> GetEventAsync(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        return await NeighbourCall.GuardAsync(
            async () =>
            {
                var response = await _client.GetEventAsync(eventId, cancellationToken);

                // Nulo es un 404: un id que events-service no conoce, y eso es un éxito vacío.
                return response is null
                    ? Result.Success<Maybe<EventSummary>, BaseError>(Maybe<EventSummary>.None)
                    : Result.Success<Maybe<EventSummary>, BaseError>(Maybe.From(ToSummary(response)));
            },
            new EventsUnavailableError(),
            _logger,
            "events-service",
            "leer el evento {EventId}",
            cancellationToken,
            eventId);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<EventSummary>, BaseError>> ListPublicEventsAsync(
        CancellationToken cancellationToken)
    {
        // El paginado, el tope y la traducción de la avería son del lector compartido; acá solo
        // queda qué se le pide a events-service y qué de lo que trae se publica.
        var listing = await NeighbourPageReader.ReadEveryPageAsync(
            fetchPageAsync: (pageNumber, pageSize, token) => _client.ListActiveEventsAsync(
                EventVisibility.Public,
                pageNumber,
                pageSize,
                token),
            itemsOf: response => response.Items,
            unavailable: new EventsUnavailableError(),
            logger: _logger,
            neighbour: "events-service",
            listing: "la cartelera pública",
            cancellationToken: cancellationToken);

        // El filtro ya lo aplicó events-service; se repite acá porque esto se publica a internet,
        // y una condición de más cuesta menos que un evento privado expuesto el día que la
        // semántica de aquel filtro cambie. Se aplica después de paginar a propósito: lo que
        // decide si una página está completa es lo que el vecino mandó, no lo que sobrevive acá.
        return listing.Map(items => (IReadOnlyList<EventSummary>)items
            .Select(ToSummary)
            .Where(summary => summary is { IsPublic: true, IsActive: true })
            .ToList());
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<CancelledEvent>, BaseError>> ListCancelledEventsAsync(
        CancellationToken cancellationToken)
    {
        var listing = await NeighbourPageReader.ReadEveryPageAsync(
            fetchPageAsync: (pageNumber, pageSize, token) => _client.ListEventsByStatusAsync(
                EventStatus.Cancelled,
                pageNumber,
                pageSize,
                token),
            itemsOf: response => response.Items,
            unavailable: new EventsUnavailableError(),
            logger: _logger,
            neighbour: "events-service",
            listing: "los eventos cancelados",
            cancellationToken: cancellationToken);

        // El filtro se repite acá, como en la cartelera: un evento vivo que se colara mandaría a
        // alguien a avisarle a sus inscritos que se canceló.
        return listing.Map(items => (IReadOnlyList<CancelledEvent>)items
            .Where(item => item.Status.Equals(EventStatus.Cancelled, StringComparison.OrdinalIgnoreCase))
            .Select(item => new CancelledEvent(item.Id, item.UpdatedAt))
            .ToList());
    }

    private static EventSummary ToSummary(EventItemResponse response) => new()
    {
        Id          = response.Id,
        Name        = response.Name,
        Type        = response.Type,
        Status      = response.Status,
        Language    = response.Language,
        Visibility  = response.Visibility,
        StartDate   = response.StartDate,
        EndDate     = response.EndDate,
        ExternalUrl = response.ExternalUrl,
        Description = response.Description,
        OptName     = response.Opt?.Name,
        Cities      = response.Cities?
            .Select(city => new EventCity(city.Name, city.Country, city.Date, city.DisplayOrder))
            .ToList() ?? []
    };
}
