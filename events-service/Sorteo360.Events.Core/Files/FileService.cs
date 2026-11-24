using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.Database;
using Sorteo360.Events.Core.Errors;
using Sorteo360.Events.Core.Shared;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.Files;

namespace Sorteo360.Events.Core.Files;

public sealed class FileService : IFileService
{
    private readonly IStoredFileRepository _repository;
    private readonly IFileStore _fileStore;

    public FileService(IStoredFileRepository repository, IFileStore fileStore)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(fileStore);
        _repository = repository;
        _fileStore = fileStore;
    }

    public Task<Result<Maybe<StoredFile>, BaseError>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _repository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<Result<Maybe<StoredFileContent>, BaseError>> OpenContentAsync(Guid id, CancellationToken cancellationToken)
    {
        var lookup = await _repository.GetByIdAsync(id, cancellationToken);

        if (lookup.IsFailure)
            return Result.Failure<Maybe<StoredFileContent>, BaseError>(lookup.Error);

        if (lookup.Value.HasNoValue)
            return Result.Success<Maybe<StoredFileContent>, BaseError>(Maybe<StoredFileContent>.None);

        var file = lookup.Value.Value;

        try
        {
            var content = await _fileStore.OpenReadAsync(file.StorageKey, cancellationToken);

            if (file.MissingSince is not null)
            {
                // Volvió a aparecer: alguien lo restauró desde la papelera. Se borra la marca
                // para que deje de salir en el listado de faltantes, y la ficha que se devuelve
                // ya refleja el estado nuevo. Si el borrado falla, el repositorio ya lo registró
                // en el log y la descarga sigue siendo correcta; lo que queda mal es el listado
                // del operador, y la próxima descarga vuelve a intentarlo.
                await _repository.ClearMissingAsync(file.Id, cancellationToken);
                file = file with { MissingSince = null };
            }

            return Result.Success<Maybe<StoredFileContent>, BaseError>(
                Maybe.From(new StoredFileContent(file, content)));
        }
        catch (FileNotFoundInStoreException)
        {
            // El almacenamiento contestó, y contestó que no está: la pérdida es un hecho y queda
            // anotada con su fecha. Si la anotación no se puede escribir, el repositorio ya lo
            // registró en el log y el archivo sigue sin estar: lo que el cliente recibe no
            // depende de que la contabilidad haya salido bien.
            await _repository.MarkMissingAsync(file.Id, cancellationToken);

            return Result.Failure<Maybe<StoredFileContent>, BaseError>(FileErrors.FileMissing());
        }
        catch (FileStoreUnavailableException)
        {
            // Ninguna fila se toca acá. Un almacenamiento que no contesta no dice nada sobre si
            // el archivo existe, y darlo por perdido destruiría el registro de qué se perdió de
            // verdad justo cuando hace falta.
            return Result.Failure<Maybe<StoredFileContent>, BaseError>(FileErrors.FileStoreUnavailable());
        }
    }

    public Task<Result<PagedResult<StoredFile>, BaseError>> ListMissingAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        return _repository.ListMissingAsync(page, pageSize, cancellationToken);
    }
}
