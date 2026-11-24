using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sorteo360.Events.Core.Database;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Files;
using Sorteo360.Platform.Database;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Infrastructure.Database;

public sealed class EventImageRepository : IEventImageRepository
{
    private readonly EventsDbContext _context;
    private readonly ILogger<EventImageRepository> _logger;

    public EventImageRepository(EventsDbContext context, ILogger<EventImageRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        _context = context;
        _logger = logger;
    }

    public Task<Result<Maybe<int>, BaseError>> CountAsync(Guid eventId, EventImageKind kind, CancellationToken cancellationToken)
    {
        return DbResult.TryAsync<Maybe<int>>(_logger, "contar las imágenes del evento", async () =>
        {
            // Un evento sin imágenes y uno que no existe cuentan cero, así que hay que preguntar
            // por él aparte: sin eso, subir a un id inventado escribiría los bytes antes de
            // descubrirlo.
            if (!await EventExistsAsync(eventId, cancellationToken))
                return Maybe<int>.None;

            var count = await _context.EventImages
                .AsNoTracking()
                .CountAsync(i => i.EventId == eventId && i.Kind == kind, cancellationToken);

            return Maybe.From(count);
        });
    }

    public Task<Result<bool, BaseError>> AddAsync(StoredFile file, EventImage image, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(image);

        return DbResult.TryResultAsync<bool>(_logger, "agregar la imagen del evento", async () =>
        {
            // La existencia se comprueba en la misma transacción que escribe: dejar que falle la
            // FK convertiría un evento inexistente en un 409, cuando lo que pasó es que el
            // recurso no está.
            if (!await EventExistsAsync(image.EventId, cancellationToken))
                return Result.Success<bool, BaseError>(false);

            // La portada que entra reemplaza a la que hubiera. Se da de baja en la misma
            // transacción que escribe la nueva, así que un fallo no deja al evento sin ninguna, y
            // sus bytes se quedan igual que si se hubiera quitado a mano.
            if (image.Kind is EventImageKind.Cover)
                await UnlinkCurrentCoverAsync(image.EventId, image.CreatedById, image.CreatedAt, cancellationToken);

            _context.StoredFiles.Add(StoredFileMapper.ToData(file));
            _context.EventImages.Add(MapToData(image));

            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success<bool, BaseError>(true);
        });
    }

    public Task<Result<Maybe<IReadOnlyList<EventImageWithFile>>, BaseError>> ListAsync(
        Guid eventId,
        EventImageKind? kind,
        CancellationToken cancellationToken)
    {
        return DbResult.TryAsync<Maybe<IReadOnlyList<EventImageWithFile>>>(_logger, "listar las imágenes del evento", async () =>
        {
            if (!await EventExistsAsync(eventId, cancellationToken))
                return Maybe<IReadOnlyList<EventImageWithFile>>.None;

            var images = await EventImageLoader.LoadAsync(_context, [eventId], kind, cancellationToken);

            return Maybe.From(images.TryGetValue(eventId, out var forEvent) ? forEvent : []);
        });
    }

    public Task<Result<bool, BaseError>> SoftDeleteAsync(
        Guid eventId,
        Guid imageId,
        Guid removedById,
        CancellationToken cancellationToken)
    {
        return DbResult.TryAsync<bool>(_logger, "quitar la imagen del evento", async () =>
        {
            // El evento es parte del filtro y no solo de la ruta: sin él, cualquiera con un id de
            // imagen podría desenlazarla desde otro evento.
            var data = await _context.EventImages
                .Where(i => i.Id == imageId && i.EventId == eventId)
                .FirstOrDefaultAsync(cancellationToken);

            if (data is null)
                return false;

            var now = DateTimeOffset.UtcNow;
            data.DeletedAt = now;
            data.UpdatedAt = now;
            data.UpdatedById = removedById;

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        });
    }

    /// <summary>
    /// Da de baja la portada viva del evento, si tiene una. No borra nada: el archivo y sus bytes
    /// se quedan, igual que al quitarla a mano.
    /// </summary>
    private async Task UnlinkCurrentCoverAsync(
        Guid eventId,
        Guid replacedById,
        DateTimeOffset replacedAt,
        CancellationToken cancellationToken)
    {
        var current = await _context.EventImages
            .Where(i => i.EventId == eventId && i.Kind == EventImageKind.Cover)
            .FirstOrDefaultAsync(cancellationToken);

        if (current is null)
            return;

        current.DeletedAt = replacedAt;
        current.UpdatedAt = replacedAt;
        current.UpdatedById = replacedById;
    }

    /// <summary>
    /// Indica si el evento existe. Se pregunta aparte de contar o de escribir porque un evento
    /// inexistente y uno sin imágenes son indistinguibles por el conteo, y porque dejar que falle
    /// la FK convertiría un recurso ausente en un conflicto.
    /// </summary>
    private Task<bool> EventExistsAsync(Guid eventId, CancellationToken cancellationToken)
    {
        return _context.Events
            .AsNoTracking()
            .AnyAsync(e => e.Id == eventId, cancellationToken);
    }

    /// <summary>Convierte la entidad de dominio en la fila que se escribe.</summary>
    private static EventImageData MapToData(EventImage i)
    {
        return new EventImageData
        {
            Id           = i.Id,
            EventId      = i.EventId,
            StoredFileId = i.StoredFileId,
            Kind         = i.Kind,
            CreatedAt    = i.CreatedAt,
            UpdatedAt    = i.UpdatedAt,
            CreatedById  = i.CreatedById,
            UpdatedById  = i.UpdatedById,
            DeletedAt    = i.DeletedAt
        };
    }
}
