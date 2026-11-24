using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sorteo360.Convocatorias.Core.CheckIns;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Platform.Database;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Infrastructure.Database;

/// <inheritdoc cref="IDoorListRepository" />
public sealed class DoorListRepository : IDoorListRepository
{
    private readonly ConvocatoriasDbContext _context;
    private readonly ILogger<DoorListRepository> _logger;

    public DoorListRepository(ConvocatoriasDbContext context, ILogger<DoorListRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<DoorListRow>, BaseError>> ReadAsync(
        Guid eventId,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync<IReadOnlyList<DoorListRow>>(_logger, "leer la lista de acceso del evento", async () =>
            // Una sola lectura de las columnas que hacen falta: la búsqueda, el orden y los totales
            // corren en memoria sobre ella (DoorListService).
            await _context.Registrations
                .AsNoTracking()
                .Where(registration => registration.EventId == eventId)
                .Select(registration => new DoorListRow(
                    registration.Id,
                    registration.FirstName,
                    registration.LastName,
                    registration.Company,
                    registration.Reference,
                    registration.EmailNormalized,
                    registration.CheckedInAt))
                .ToListAsync(cancellationToken));
}
