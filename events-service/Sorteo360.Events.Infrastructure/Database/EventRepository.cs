using System.Linq.Expressions;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sorteo360.Events.Core.Database;
using Sorteo360.Events.Core.DTOs;
using Sorteo360.Events.Core.Errors;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Shared;
using Sorteo360.Platform.Database;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Infrastructure.Database;

public sealed class EventRepository : IEventRepository
{
    /// <summary>El carácter con el que se escapan los comodines de un <c>ILIKE</c>.</summary>
    private const string LikeEscapeCharacter = "\\";

    /// <summary>La posición del estado en el ciclo de vida del evento.</summary>
    private static readonly Expression<Func<EventData, int>> StatusRank = e =>
        e.Status == EventStatus.Planning   ? 0 :
        e.Status == EventStatus.InProgress ? 1 :
        e.Status == EventStatus.Closed     ? 2 : 3;

    /// <summary>
    /// Cero si el evento tiene costo total y uno si no. Se ordena siempre ascendente, así que los
    /// eventos sin costo quedan al final vengan como vengan los demás.
    /// </summary>
    private static readonly Expression<Func<EventData, int>> TotalCostNullRank = e =>
        e.TotalCost == null ? 1 : 0;

    /// <summary>La moneda del costo total como posición: USD primero.</summary>
    private static readonly Expression<Func<EventData, int>> TotalCostCurrencyRank = e =>
        e.TotalCost!.Currency == Currency.USD ? 0 : 1;

    /// <summary>El monto del costo total, nulo cuando el evento no tiene.</summary>
    private static readonly Expression<Func<EventData, decimal?>> TotalCostAmount = e =>
        (decimal?)e.TotalCost!.Amount;

    private readonly EventsDbContext _context;
    private readonly ILogger<EventRepository> _logger;

    public EventRepository(EventsDbContext context, ILogger<EventRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        _context = context;
        _logger = logger;
    }

    public Task<Result<Guid, BaseError>> CreateAsync(Event @event, RoadshowCity city, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(city);

        return DbResult.TryAsync<Guid>(_logger, "crear el evento", async () =>
        {
            var data = MapToEventData(@event);
            data.AccountId = _context.CurrentAccountId;
            data.WebhookSecret = Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            _context.Events.Add(data);
            _context.RoadshowCities.Add(MapToRoadshowCityData(city));
            await _context.SaveChangesAsync(cancellationToken);
            return @event.Id;
        });
    }

    public Task<Result<Maybe<EventWithCities>, BaseError>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return DbResult.TryAsync<Maybe<EventWithCities>>(_logger, "obtener el evento", async () =>
        {
            // AsNoTracking evita el overhead de change tracking en consultas de solo lectura.
            // El filtro global HasQueryFilter (deleted_at IS NULL) se aplica automáticamente.
            var eventData = await _context.Events
                .AsNoTracking()
                .Where(e => e.Id == id)
                .FirstOrDefaultAsync(cancellationToken);

            if (eventData is null)
                return Maybe<EventWithCities>.None;

            var cityRows = await _context.RoadshowCities
                .AsNoTracking()
                .Where(c => c.EventId == id)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync(cancellationToken);

            var images = await EventImageLoader.LoadAsync(_context, [id], kind: null, cancellationToken);

            var @event = MapToEvent(eventData);
            var cities = cityRows.Select(MapToRoadshowCity).ToList();

            return Maybe.From(new EventWithCities(
                @event,
                cities,
                images.TryGetValue(id, out var forEvent) ? forEvent : []));
        });
    }

    public Task<Result<Maybe<string>, BaseError>> GetStatusAsync(Guid eventId, CancellationToken cancellationToken)
    {
        return DbResult.TryAsync<Maybe<string>>(_logger, "obtener el estado del evento", async () =>
        {
            var status = await _context.Events
                .AsNoTracking()
                .Where(e => e.Id == eventId)
                .Select(e => e.Status)
                .FirstOrDefaultAsync(cancellationToken);

            return status is null ? Maybe<string>.None : Maybe.From(status);
        });
    }

    public Task<Result<Maybe<EventWithCities>, BaseError>> StartAsync(Guid eventId, Guid startedById, CancellationToken cancellationToken)
    {
        return DbResult.TryAsync<Maybe<EventWithCities>>(_logger, "iniciar el evento", async () =>
        {
            // Se carga con tracking (sin AsNoTracking) para que SaveChanges persista los cambios.
            // El filtro global (deleted_at IS NULL) excluye los eventos eliminados.
            var eventData = await _context.Events
                .Where(e => e.Id == eventId)
                .FirstOrDefaultAsync(cancellationToken);

            if (eventData is null)
                return Maybe<EventWithCities>.None;

            eventData.Status = EventStatus.InProgress;
            eventData.UpdatedById = startedById;
            eventData.UpdatedAt = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            return Maybe.From(await BuildEventWithCitiesAsync(eventData, cancellationToken));
        });
    }

    public Task<Result<Maybe<EventWithCities>, BaseError>> CancelAsync(Guid eventId, string reason, Guid cancelledById, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reason);

        return DbResult.TryAsync<Maybe<EventWithCities>>(_logger, "cancelar el evento", async () =>
        {
            // Se carga con tracking (sin AsNoTracking) para que SaveChanges persista los cambios.
            // El filtro global (deleted_at IS NULL) excluye los eventos eliminados.
            var eventData = await _context.Events
                .Where(e => e.Id == eventId)
                .FirstOrDefaultAsync(cancellationToken);

            if (eventData is null)
                return Maybe<EventWithCities>.None;

            var now = DateTimeOffset.UtcNow;

            eventData.Status = EventStatus.Cancelled;
            eventData.CancellationReason = reason;
            eventData.UpdatedById = cancelledById;
            eventData.UpdatedAt = now;

            await _context.SaveChangesAsync(cancellationToken);

            return Maybe.From(await BuildEventWithCitiesAsync(eventData, cancellationToken));
        });
    }

    public Task<Result<Maybe<EventWithCities>, BaseError>> UpdateAsync(Event @event, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);

        return DbResult.TryResultAsync<Maybe<EventWithCities>>(_logger, "actualizar el evento", async () =>
        {
            // Se carga con tracking (sin AsNoTracking) para que SaveChanges persista los cambios.
            // El filtro global (deleted_at IS NULL) excluye los eventos eliminados.
            var eventData = await _context.Events
                .Where(e => e.Id == @event.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (eventData is null)
                return Result.Success<Maybe<EventWithCities>, BaseError>(Maybe<EventWithCities>.None);

            // El servicio leyó el evento sin tracking y esta lectura es otra, así que xmin solo cubre
            // el tramo desde acá hasta SaveChanges. Si en el medio el evento se cerró o se canceló,
            // la edición se decidió sobre una lectura vieja y se rechaza aquí.
            if (!EventStatus.Active.Contains(eventData.Status))
                return Result.Failure<Maybe<EventWithCities>, BaseError>(EventErrors.NotEditable());

            // Solo los campos que una edición es dueña de cambiar. El estado, el costo total y la
            // razón de cancelación no se copian nunca: un inicio que ocurrió en el medio sigue
            // valiendo, y el evento editado traería el estado viejo.
            eventData.Name            = @event.Name;
            eventData.StartDate       = @event.StartDate;
            eventData.EndDate         = @event.EndDate;
            eventData.Language        = @event.Language;
            eventData.Visibility      = @event.Visibility;
            eventData.SeatingLayout   = @event.SeatingLayout;
            eventData.ExternalUrl     = @event.ExternalUrl;
            eventData.Description     = @event.Description;
            eventData.InvitedCount    = @event.InvitedCount;
            eventData.EstimatedBudget = @event.EstimatedBudget;
            eventData.UpdatedById     = @event.UpdatedById;
            eventData.UpdatedAt       = @event.UpdatedAt;

            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success<Maybe<EventWithCities>, BaseError>(
                Maybe.From(await BuildEventWithCitiesAsync(eventData, cancellationToken)));
        });
    }

    public Task<Result<Maybe<EventWithCities>, BaseError>> CloseAsync(
        Guid eventId,
        Guid closedById,
        CancellationToken cancellationToken)
    {
        return DbResult.TryResultAsync<Maybe<EventWithCities>>(_logger, "cerrar el evento", async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            var status = await EventRowLock.LockAsync(_context, eventId, cancellationToken);
            if (status.HasNoValue)
                return Result.Success<Maybe<EventWithCities>, BaseError>(Maybe<EventWithCities>.None);
            if (status.Value != EventStatus.InProgress)
                return Result.Failure<Maybe<EventWithCities>, BaseError>(EventErrors.NotClosable());

            // Se carga con tracking (sin AsNoTracking) para que SaveChanges persista los cambios.
            var eventData = await _context.Events
                .Where(e => e.Id == eventId)
                .SingleAsync(cancellationToken);

            eventData.Status = EventStatus.Closed;
            eventData.UpdatedById = closedById;
            eventData.UpdatedAt = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result.Success<Maybe<EventWithCities>, BaseError>(
                Maybe.From(await BuildEventWithCitiesAsync(eventData, cancellationToken)));
        });
    }

    /// <summary>
    /// Arma el agregado <see cref="EventWithCities"/> a partir de la fila de evento ya
    /// cargada, leyendo sus ciudades ordenadas por display_order (solo lectura).
    /// </summary>
    private async Task<EventWithCities> BuildEventWithCitiesAsync(EventData eventData, CancellationToken cancellationToken)
    {
        var cityRows = await _context.RoadshowCities
            .AsNoTracking()
            .Where(c => c.EventId == eventData.Id)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync(cancellationToken);

        var images = await EventImageLoader.LoadAsync(_context, [eventData.Id], kind: null, cancellationToken);

        return new EventWithCities(
            MapToEvent(eventData),
            cityRows.Select(MapToRoadshowCity).ToList(),
            images.TryGetValue(eventData.Id, out var forEvent) ? forEvent : []);
    }

    public Task<Result<PagedResult<EventWithCover>, BaseError>> ListAsync(ListEventsDto filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        return DbResult.TryAsync<PagedResult<EventWithCover>>(_logger, "listar eventos", async () =>
        {
            // AsNoTracking: consulta de solo lectura. El filtro global (deleted_at IS NULL)
            // se aplica automáticamente.
            var query = _context.Events.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(filter.Type))
                query = query.Where(e => e.Type == filter.Type);

            // 'status' tiene prioridad; si no, 'active' filtra por el conjunto de estados
            // (expandido a comparaciones explícitas para que EF lo traduzca a SQL sin problemas).
            if (!string.IsNullOrWhiteSpace(filter.Status))
                query = query.Where(e => e.Status == filter.Status);
            else if (filter.Active is not null)
                query = filter.Active.Value
                    ? query.Where(e => e.Status == EventStatus.Planning || e.Status == EventStatus.InProgress)
                    : query.Where(e => e.Status == EventStatus.Closed || e.Status == EventStatus.Cancelled);

            if (filter.StartDateFrom is not null)
                query = query.Where(e => e.StartDate >= filter.StartDateFrom.Value);

            if (filter.StartDateTo is not null)
                query = query.Where(e => e.StartDate <= filter.StartDateTo.Value);

            if (!string.IsNullOrWhiteSpace(filter.Visibility))
                query = query.Where(e => e.Visibility == filter.Visibility);

            // La búsqueda va contra el nombre del evento y nada más, sin distinguir mayúsculas ni
            // exigir que el término esté al principio. Los comodines del término se escapan: quien
            // escribe "100%" busca ese texto, no "cualquier cosa".
            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var pattern = $"%{EscapeLikeWildcards(filter.SearchTerm.Trim())}%";
                query = query.Where(e => EF.Functions.ILike(e.Name, pattern, LikeEscapeCharacter));
            }

            var total = await query.CountAsync(cancellationToken);

            var rows = await ApplySort(query, filter.SortField, filter.SortOrder)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync(cancellationToken);

            // Solo la portada, y solo la de los eventos de esta página: la galería entera es una
            // lectura del detalle, y una página de veinticinco eventos enseña veinticinco
            // imágenes.
            var covers = await EventImageLoader.LoadCoversAsync(
                _context,
                rows.Select(e => e.Id).ToList(),
                cancellationToken);

            // Las ciudades de la página entera en una sola consulta, no una por fila: un roadshow
            // se identifica por dónde va, y sin esto una cartelera de veinticinco tendría que leer
            // veinticinco detalles para decirlo.
            var pageIds = rows.Select(e => e.Id).ToList();
            var cities = (await _context.RoadshowCities
                    .AsNoTracking()
                    .Where(c => pageIds.Contains(c.EventId))
                    .OrderBy(c => c.DisplayOrder)
                    .ToListAsync(cancellationToken))
                .GroupBy(c => c.EventId)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<RoadshowCity>)group.Select(MapToRoadshowCity).ToList());

            var items = rows
                .Select(row => new EventWithCover(
                    MapToEvent(row),
                    covers.GetValueOrDefault(row.Id),
                    cities.GetValueOrDefault(row.Id, [])))
                .ToList();

            return new PagedResult<EventWithCover>(items, total, filter.Page, filter.PageSize);
        });
    }

    public Task<Result<IReadOnlyList<DoorEvent>, BaseError>> ListOverlappingAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        return DbResult.TryAsync<IReadOnlyList<DoorEvent>>(_logger, "listar los eventos de la puerta", async () =>
        {
            // Dos rangos se cruzan cuando cada uno empieza antes de que el otro termine. El filtro
            // global (deleted_at IS NULL) se aplica automáticamente, en eventos y en ciudades.
            var rows = await _context.Events
                .AsNoTracking()
                .Where(e => e.Status != EventStatus.Cancelled && e.StartDate <= to && e.EndDate >= from)
                .OrderBy(e => e.StartDate)
                .ThenBy(e => e.Name)
                .ToListAsync(cancellationToken);

            var ids = rows.Select(e => e.Id).ToList();
            var cities = (await _context.RoadshowCities
                    .AsNoTracking()
                    .Where(c => ids.Contains(c.EventId))
                    .OrderBy(c => c.DisplayOrder)
                    .ToListAsync(cancellationToken))
                .GroupBy(c => c.EventId)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<RoadshowCity>)group.Select(MapToRoadshowCity).ToList());

            return rows
                .Select(row => new DoorEvent(MapToEvent(row), cities.GetValueOrDefault(row.Id, [])))
                .ToList();
        });
    }

    /// <summary>
    /// Neutraliza los comodines de <c>LIKE</c> dentro del término buscado. La barra invertida va
    /// primero: escaparla después convertiría en comodín lo que se acaba de escapar.
    /// </summary>
    private static string EscapeLikeWildcards(string term) => term
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);

    /// <summary>
    /// Traduce el campo y el sentido pedidos a la cláusula de orden. Cada campo tiene la suya
    /// escrita a mano, que es lo que hace del conjunto de campos uno cerrado.
    /// <para>
    /// El desempate es siempre el mismo —creación descendente y luego id—, para que dos páginas
    /// consecutivas de un mismo listado no puedan traer la misma fila dos veces ni saltarse una.
    /// </para>
    /// </summary>
    private static IQueryable<EventData> ApplySort(
        IQueryable<EventData> query,
        EventSortField field,
        SortOrder direction)
    {
        var ascending = direction is SortOrder.Asc;

        var ordered = field switch
        {
            EventSortField.Name => ascending
                ? query.OrderBy(e => e.Name)
                : query.OrderByDescending(e => e.Name),

            EventSortField.StartDate => ascending
                ? query.OrderBy(e => e.StartDate)
                : query.OrderByDescending(e => e.StartDate),

            // Por el ciclo de vida y no por el alfabeto: ordenar los estados como texto daría
            // cancelled, closed, in_progress, planning, que no es un orden de nada.
            EventSortField.Status => ascending
                ? query.OrderBy(StatusRank)
                : query.OrderByDescending(StatusRank),

            EventSortField.TotalCost => OrderByTotalCost(query, ascending),

            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Campo de orden sin cláusula.")
        };

        return ordered
            .ThenByDescending(e => e.CreatedAt)
            .ThenBy(e => e.Id);
    }

    /// <summary>
    /// El orden por costo total: primero la moneda y después el monto. La plataforma no convierte,
    /// así que comparar 900 EUR con 1.000 USD por el número pelado sería mentir; agrupando por
    /// moneda, cada comparación es entre cifras comparables.
    /// <para>
    /// El sentido invierte el monto y nada más. La moneda es el agrupamiento, no una cifra que
    /// pueda ir al revés: USD queda primero en los dos sentidos, igual que un evento sin costo
    /// total queda último en los dos —todavía no cerró, y no tiene lugar en una lista de importes—.
    /// </para>
    /// </summary>
    private static IOrderedQueryable<EventData> OrderByTotalCost(IQueryable<EventData> query, bool ascending)
    {
        var grouped = query
            .OrderBy(TotalCostNullRank)
            .ThenBy(TotalCostCurrencyRank);

        return ascending
            ? grouped.ThenBy(TotalCostAmount)
            : grouped.ThenByDescending(TotalCostAmount);
    }

    public Task<Result<bool, BaseError>> UpdateCityAsync(RoadshowCity city, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(city);

        return DbResult.TryAsync<bool>(_logger, "actualizar la ciudad", async () =>
        {
            // Se carga con tracking (sin AsNoTracking) para que SaveChanges persista los cambios.
            // El filtro global (deleted_at IS NULL) excluye las ciudades eliminadas.
            var data = await _context.RoadshowCities
                .Where(c => c.Id == city.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (data is null)
                return false;

            // Reemplazo completo de los campos mutables, igual que el PATCH del evento.
            data.Name       = city.Name;
            data.Country         = city.Country;
            data.TimeZone        = city.TimeZone;
            data.Date        = city.Date;
            data.DisplayOrder = city.DisplayOrder;
            data.UpdatedById  = city.UpdatedById;
            data.UpdatedAt    = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        });
    }

    /// <summary>
    /// Convierte la entidad de dominio <see cref="Event"/> a la clase de persistencia
    /// <see cref="EventData"/>. El value object <see cref="Money"/> se mapea como tipo owned.
    /// </summary>
    private static EventData MapToEventData(Event @event)
    {
        return new EventData
        {
            Id                   = @event.Id,
            Type                 = @event.Type,
            Name                 = @event.Name,
            StartDate            = @event.StartDate,
            EndDate              = @event.EndDate,
            Status               = @event.Status,
            Language             = @event.Language,
            Visibility           = @event.Visibility,
            SeatingLayout        = @event.SeatingLayout,
            ExternalUrl          = @event.ExternalUrl,
            Description          = @event.Description,
            InvitedCount         = @event.InvitedCount,
            EstimatedBudget      = @event.EstimatedBudget,
            TotalCost            = @event.TotalCost,
            CancellationReason   = @event.CancellationReason,
            CreatedAt            = @event.CreatedAt,
            UpdatedAt            = @event.UpdatedAt,
            CreatedById          = @event.CreatedById,
            UpdatedById          = @event.UpdatedById,
            DeletedAt            = @event.DeletedAt
        };
    }

    /// <summary>
    /// Convierte la clase de persistencia <see cref="EventData"/> a la entidad de dominio
    /// <see cref="Event"/>. El value object <see cref="Money"/> se mapea como tipo owned.
    /// </summary>
    private static Event MapToEvent(EventData data)
    {
        return new Event(
            id:                  data.Id,
            type:                data.Type,
            name:                data.Name,
            startDate:           data.StartDate,
            endDate:             data.EndDate,
            status:              data.Status,
            language:            data.Language,
            visibility:          data.Visibility,
            seatingLayout:       data.SeatingLayout,
            externalUrl:         data.ExternalUrl,
            description:         data.Description,
            invitedCount:        data.InvitedCount,
            estimatedBudget:     data.EstimatedBudget,
            totalCost:           data.TotalCost,
            cancellationReason:  data.CancellationReason,
            createdAt:           data.CreatedAt,
            updatedAt:           data.UpdatedAt,
            createdById:         data.CreatedById,
            updatedById:         data.UpdatedById,
            deletedAt:           data.DeletedAt);
    }

    /// <summary>
    /// Convierte la entidad de dominio <see cref="RoadshowCity"/> a la clase de persistencia
    /// <see cref="RoadshowCityData"/>.
    /// </summary>
    private static RoadshowCityData MapToRoadshowCityData(RoadshowCity city)
    {
        return new RoadshowCityData
        {
            Id            = city.Id,
            EventId      = city.EventId,
            Name          = city.Name,
            Country       = city.Country,
            TimeZone      = city.TimeZone,
            Date          = city.Date,
            DisplayOrder  = city.DisplayOrder,
            CreatedAt     = city.CreatedAt,
            UpdatedAt     = city.UpdatedAt,
            CreatedById   = city.CreatedById,
            UpdatedById   = city.UpdatedById,
            DeletedAt     = city.DeletedAt
        };
    }

    /// <summary>
    /// Convierte la clase de persistencia <see cref="RoadshowCityData"/> a la entidad de dominio
    /// <see cref="RoadshowCity"/>.
    /// </summary>
    private static RoadshowCity MapToRoadshowCity(RoadshowCityData data)
    {
        return new RoadshowCity(
            id:           data.Id,
            eventId:     data.EventId,
            name:         data.Name,
            country:      data.Country,
            timeZone:     data.TimeZone,
            date:         data.Date,
            displayOrder: data.DisplayOrder,
            createdAt:    data.CreatedAt,
            updatedAt:    data.UpdatedAt,
            createdById:  data.CreatedById,
            updatedById:  data.UpdatedById,
            deletedAt:    data.DeletedAt);
    }
}
