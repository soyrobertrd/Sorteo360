using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sorteo360.Events.Core.Database;
using Sorteo360.Events.Core.Files;
using Sorteo360.Events.Core.Shared;
using Sorteo360.Platform.Database;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Infrastructure.Database;

public sealed class StoredFileRepository : IStoredFileRepository
{
    private readonly EventsDbContext _context;
    private readonly ILogger<StoredFileRepository> _logger;

    public StoredFileRepository(EventsDbContext context, ILogger<StoredFileRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        _context = context;
        _logger = logger;
    }

    public Task<Result<Maybe<StoredFile>, BaseError>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return DbResult.TryAsync<Maybe<StoredFile>>(_logger, "obtener el archivo", async () =>
        {
            var data = await _context.StoredFiles
                .AsNoTracking()
                .Where(f => f.Id == id)
                .FirstOrDefaultAsync(cancellationToken);

            return data is null
                ? Maybe<StoredFile>.None
                : Maybe.From(StoredFileMapper.ToDomain(data));
        });
    }

    public async Task<UnitResult<BaseError>> MarkMissingAsync(Guid id, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        // Se sella en una sola sentencia y solo donde todavía no había fecha. Lo que hace falta
        // conservar es el primer momento en que se detectó la ausencia: cada lectura posterior
        // vuelve a pasar por acá, y sobrescribir movería la fecha hasta el último intento.
        var result = await DbResult.TryAsync(_logger, "marcar el archivo como ausente", () =>
            _context.StoredFiles
                .Where(f => f.Id == id && f.MissingSince == null)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.MissingSince, now), cancellationToken));

        return result.IsFailure ? UnitResult.Failure(result.Error) : UnitResult.Success<BaseError>();
    }

    public async Task<UnitResult<BaseError>> ClearMissingAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await DbResult.TryAsync(_logger, "quitar la marca de ausencia del archivo", () =>
            _context.StoredFiles
                .Where(f => f.Id == id && f.MissingSince != null)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.MissingSince, (DateTimeOffset?)null), cancellationToken));

        return result.IsFailure ? UnitResult.Failure(result.Error) : UnitResult.Success<BaseError>();
    }

    public Task<Result<PagedResult<StoredFile>, BaseError>> ListMissingAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        return DbResult.TryAsync(_logger, "listar los archivos ausentes", async () =>
        {
            var query = _context.StoredFiles
                .AsNoTracking()
                .Where(f => f.MissingSince != null);

            var total = await query.CountAsync(cancellationToken);

            // La más reciente primero, y el id como desempate: sin él dos ausencias selladas en
            // el mismo instante podrían repartirse distinto entre dos páginas.
            var rows = await query
                .OrderByDescending(f => f.MissingSince)
                .ThenBy(f => f.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<StoredFile>(
                [.. rows.Select(StoredFileMapper.ToDomain)],
                total,
                page,
                pageSize);
        });
    }
}
