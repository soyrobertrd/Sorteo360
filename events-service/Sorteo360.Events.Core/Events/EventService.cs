using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.Database;
using Sorteo360.Events.Core.DTOs;
using Sorteo360.Events.Core.Errors;
using Sorteo360.Events.Core.Shared;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Events;

public sealed class EventService : IEventService
{
    private readonly IEventRepository _repository;
    private readonly TimeProvider _clock;

    public EventService(
        IEventRepository repository,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(clock);
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result<Guid, BaseError>> CreateAsync(CreateEventDto dto, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (!Event.IsValidEstimatedBudget(dto.EstimatedBudget))
            return Result.Failure<Guid, BaseError>(EventErrors.BudgetAmountInvalid());

        var now = DateTimeOffset.UtcNow;
        var eventId = Guid.NewGuid();

        var @event = new Event(
            id: eventId,
            type: dto.Type,
            name: dto.Name,
            startDate: dto.StartDate,
            endDate: dto.EndDate,
            status: EventStatus.Planning,
            language: dto.Language,
            visibility: dto.Visibility,
            seatingLayout: dto.SeatingLayout,
            externalUrl: dto.ExternalUrl,
            description: dto.Description,
            invitedCount: dto.InvitedCount,
            estimatedBudget: dto.EstimatedBudget,
            totalCost: null,
            cancellationReason: null,
            createdAt: now,
            updatedAt: now,
            createdById: dto.CreatedById,
            updatedById: dto.CreatedById,
            deletedAt: null);

        var city = new RoadshowCity(
            id: Guid.NewGuid(),
            eventId: eventId,
            name: dto.CityName,
            country: dto.CityCountry,
            timeZone: dto.CityTimeZone.Id,
            date: null,
            displayOrder: 1,
            createdAt: now,
            updatedAt: now,
            createdById: dto.CreatedById,
            updatedById: dto.CreatedById,
            deletedAt: null);

        return await _repository.CreateAsync(@event, city, cancellationToken);
    }

    public Task<Result<Maybe<EventWithCities>, BaseError>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _repository.GetByIdAsync(id, cancellationToken);
    }

    public Task<Result<PagedResult<EventWithCover>, BaseError>> ListAsync(ListEventsDto filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return _repository.ListAsync(filter, cancellationToken);
    }

    public Task<Result<IReadOnlyList<DoorEvent>, BaseError>> ListDoorAsync(CancellationToken cancellationToken)
    {
        // Un día de margen a cada lado: cubre montar la víspera, una noche que pasa de medianoche y
        // las cuatro horas de República Dominicana contra el día UTC (spec convocatorias/10 §4).
        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        return _repository.ListOverlappingAsync(today.AddDays(-1), today.AddDays(1), cancellationToken);
    }

    public async Task<Result<Maybe<EventWithCities>, BaseError>> StartAsync(Guid id, Guid startedById, CancellationToken cancellationToken)
    {
        var loadResult = await _repository.GetByIdAsync(id, cancellationToken);
        if (loadResult.IsFailure)
            return Result.Failure<Maybe<EventWithCities>, BaseError>(loadResult.Error);
        if (loadResult.Value.HasNoValue)
            return Result.Success<Maybe<EventWithCities>, BaseError>(Maybe<EventWithCities>.None);

        var @event = loadResult.Value.Value.Event;

        // Transición manual planning → in_progress; desde cualquier otro estado es conflicto.
        if (@event.Status is not EventStatus.Planning)
            return Result.Failure<Maybe<EventWithCities>, BaseError>(
                new BaseError("El evento solo puede iniciarse desde el estado planning.", "EVENT_NOT_STARTABLE", ErrorKind.Conflict));

        return await _repository.StartAsync(id, startedById, cancellationToken);
    }

    public async Task<Result<Maybe<EventWithCities>, BaseError>> CancelAsync(Guid id, string reason, Guid cancelledById, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reason);

        var loadResult = await _repository.GetByIdAsync(id, cancellationToken);
        if (loadResult.IsFailure)
            return Result.Failure<Maybe<EventWithCities>, BaseError>(loadResult.Error);
        if (loadResult.Value.HasNoValue)
            return Result.Success<Maybe<EventWithCities>, BaseError>(Maybe<EventWithCities>.None);

        var @event = loadResult.Value.Value.Event;

        // Se cancela desde cualquier estado no terminal (planning / in_progress).
        if (!EventStatus.Active.Contains(@event.Status))
            return Result.Failure<Maybe<EventWithCities>, BaseError>(
                new BaseError("El evento ya está cerrado o cancelado.", "EVENT_NOT_CANCELLABLE", ErrorKind.Conflict));

        return await _repository.CancelAsync(id, reason, cancelledById, cancellationToken);
    }

    public async Task<Result<Maybe<EventWithCities>, BaseError>> UpdateAsync(UpdateEventDto dto, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var loadResult = await _repository.GetByIdAsync(dto.Id, cancellationToken);
        if (loadResult.IsFailure)
            return Result.Failure<Maybe<EventWithCities>, BaseError>(loadResult.Error);
        if (loadResult.Value.HasNoValue)
            return Result.Success<Maybe<EventWithCities>, BaseError>(Maybe<EventWithCities>.None);

        // El evento decide qué admite la edición.
        var updated = loadResult.Value.Value.Event.Update(dto, _clock.GetUtcNow());
        if (updated.IsFailure)
            return Result.Failure<Maybe<EventWithCities>, BaseError>(updated.Error);

        return await _repository.UpdateAsync(updated.Value, cancellationToken);
    }

    // Solo se cierra un evento iniciado (in_progress): uno en planning nunca comenzó y uno
    // terminal ya no admite transiciones. El repositorio lo comprueba bajo el bloqueo del evento.
    public Task<Result<Maybe<EventWithCities>, BaseError>> CloseAsync(Guid id, Guid closedById, CancellationToken cancellationToken)
        => _repository.CloseAsync(id, closedById, cancellationToken);

    public async Task<Result<Maybe<RoadshowCity>, BaseError>> UpdateCityAsync(UpdateCityDto dto, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var loadResult = await LoadEventForCityChangesAsync(dto.EventId, cancellationToken);
        if (loadResult.IsFailure)
            return Result.Failure<Maybe<RoadshowCity>, BaseError>(loadResult.Error);
        if (loadResult.Value.HasNoValue)
            return Result.Success<Maybe<RoadshowCity>, BaseError>(Maybe<RoadshowCity>.None);

        var existing = loadResult.Value.Value.Cities.FirstOrDefault(c => c.Id == dto.CityId);
        if (existing is null)
            return Result.Success<Maybe<RoadshowCity>, BaseError>(Maybe<RoadshowCity>.None);

        var updated = existing with
        {
            Name = dto.Name,
            Country = dto.Country,
            // Corregir la zona se permite también después de completado un sorteo: los momentos no
            // cambian, solo el reloj en que se escriben, y la próxima reemisión del acta lo recoge.
            // Rige la misma guarda que para el resto de la ciudad, así que se puede en un evento
            // de cualquier tipo que no esté cerrado ni cancelado (PGTI-335).
            TimeZone = dto.TimeZone.Id,
            Date = dto.Date,
            DisplayOrder = dto.DisplayOrder,
            UpdatedAt = DateTimeOffset.UtcNow,
            UpdatedById = dto.UpdatedById
        };

        var updateResult = await _repository.UpdateCityAsync(updated, cancellationToken);
        if (updateResult.IsFailure)
            return Result.Failure<Maybe<RoadshowCity>, BaseError>(updateResult.Error);

        return updateResult.Value
            ? Result.Success<Maybe<RoadshowCity>, BaseError>(Maybe.From(updated))
            : Result.Success<Maybe<RoadshowCity>, BaseError>(Maybe<RoadshowCity>.None);
    }

    /// <summary>
    /// Carga el evento con sus ciudades y exige que admita el cambio, o devuelve None si el evento no existe.
    /// </summary>
    /// <remarks>
    /// El evento no debe estar en un estado terminal, sea del tipo que sea: corregir su única
    /// ciudad no cambia cuántas tiene (PGTI-335, PGTI-336).
    /// </remarks>
    private async Task<Result<Maybe<EventWithCities>, BaseError>> LoadEventForCityChangesAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var loadResult = await _repository.GetByIdAsync(eventId, cancellationToken);
        if (loadResult.IsFailure)
            return loadResult;

        if (loadResult.Value.HasNoValue)
            return loadResult;

        var @event = loadResult.Value.Value.Event;

        if (!EventStatus.Active.Contains(@event.Status))
            return Result.Failure<Maybe<EventWithCities>, BaseError>(EventErrors.NotEditable());

        return loadResult;
    }
}
