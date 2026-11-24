using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.Database;
using Sorteo360.Events.Core.DTOs;
using Sorteo360.Events.Core.Errors;
using Sorteo360.Events.Core.Files;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.Files;

namespace Sorteo360.Events.Core.Events;

public sealed class EventImageService : IEventImageService
{
    private readonly IEventImageRepository _repository;
    private readonly IFileStore _fileStore;
    private readonly IUploadPolicy _uploadPolicy;

    public EventImageService(
        IEventImageRepository repository,
        IFileStore fileStore,
        IUploadPolicy uploadPolicy)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(fileStore);
        ArgumentNullException.ThrowIfNull(uploadPolicy);
        _repository = repository;
        _fileStore = fileStore;
        _uploadPolicy = uploadPolicy;
    }

    /// <inheritdoc />
    public async Task<Result<Maybe<EventImageWithFile>, BaseError>> AddAsync(
        AddEventImageDto dto,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // Todo lo que puede rechazar la subida se pregunta antes de escribir un solo byte: un
        // archivo rechazado no deja nada que barrer, y un evento inexistente tampoco. El conteo
        // es lo que responde por la existencia del evento, venga del tipo que venga.
        var imageCount = await _repository.CountAsync(dto.EventId, dto.Kind, cancellationToken);

        if (imageCount.IsFailure)
            return Result.Failure<Maybe<EventImageWithFile>, BaseError>(imageCount.Error);

        if (imageCount.Value.HasNoValue)
            return Result.Success<Maybe<EventImageWithFile>, BaseError>(Maybe<EventImageWithFile>.None);

        // El cupo es solo de la galería. La portada no se acumula: la que entra reemplaza a la
        // que hubiera, así que nunca hay una segunda que contar.
        if (dto.Kind is EventImageKind.Gallery)
        {
            var withinLimit = _uploadPolicy.CheckEventGalleryImageCount(imageCount.Value.Value);

            if (withinLimit.IsFailure)
                return Result.Failure<Maybe<EventImageWithFile>, BaseError>(withinLimit.Error);
        }

        var inspected = await _uploadPolicy.InspectImageAsync(dto.Content, dto.FileName, dto.ContentType, cancellationToken);

        if (inspected.IsFailure)
            return Result.Failure<Maybe<EventImageWithFile>, BaseError>(inspected.Error);

        // Lo que se guarda y lo que se vuelve a servir es el tipo que dicen los bytes, no el que
        // declaró quien subió el archivo.
        var contentType = inspected.Value.ContentType;

        StoredFileLocation location;

        try
        {
            location = await _fileStore.SaveAsync(dto.Content, dto.FileName, contentType, cancellationToken);
        }
        catch (FileStoreUnavailableException)
        {
            return Result.Failure<Maybe<EventImageWithFile>, BaseError>(FileErrors.FileStoreUnavailable());
        }

        var now = DateTimeOffset.UtcNow;

        var file = new StoredFile(
            id: Guid.NewGuid(),
            provider: location.Provider,
            storageKey: location.StorageKey,
            fileName: dto.FileName,
            contentType: contentType,
            sizeBytes: location.SizeBytes,
            checksum: location.Checksum,
            missingSince: null,
            createdAt: now,
            createdById: dto.CreatedById);

        var image = new EventImage(
            id: Guid.NewGuid(),
            eventId: dto.EventId,
            storedFileId: file.Id,
            kind: dto.Kind,
            createdAt: now,
            updatedAt: now,
            createdById: dto.CreatedById,
            updatedById: dto.CreatedById,
            deletedAt: null);

        var linked = await _repository.AddAsync(file, image, cancellationToken);

        if (linked.IsFailure)
        {
            await _fileStore.DiscardOrphanBytesAsync(location.StorageKey);
            return Result.Failure<Maybe<EventImageWithFile>, BaseError>(linked.Error);
        }

        if (!linked.Value)
        {
            await _fileStore.DiscardOrphanBytesAsync(location.StorageKey);
            return Result.Success<Maybe<EventImageWithFile>, BaseError>(Maybe<EventImageWithFile>.None);
        }

        return Result.Success<Maybe<EventImageWithFile>, BaseError>(
            Maybe.From(new EventImageWithFile(image, file)));
    }

    /// <inheritdoc />
    public Task<Result<Maybe<IReadOnlyList<EventImageWithFile>>, BaseError>> ListAsync(
        Guid eventId,
        EventImageKind? kind,
        CancellationToken cancellationToken)
    {
        return _repository.ListAsync(eventId, kind, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UnitResult<BaseError>> RemoveAsync(
        Guid eventId,
        Guid imageId,
        Guid removedById,
        CancellationToken cancellationToken)
    {
        // Solo se da de baja el enlace. Los bytes y su ficha se quedan: borrarlos acá le quitaría
        // a la purga de retención la decisión de cuándo (y si) se van.
        var removed = await _repository.SoftDeleteAsync(eventId, imageId, removedById, cancellationToken);

        if (removed.IsFailure)
            return UnitResult.Failure(removed.Error);

        return removed.Value
            ? UnitResult.Success<BaseError>()
            : UnitResult.Failure(EventImageErrors.ImageNotFound());
    }
}
