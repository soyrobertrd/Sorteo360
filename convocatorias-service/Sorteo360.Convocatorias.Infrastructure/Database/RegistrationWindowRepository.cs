using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.Registrations;
using Sorteo360.Platform.Database;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Infrastructure.Database;

/// <inheritdoc cref="IRegistrationWindowRepository" />
/// <remarks>
/// <b>Las dos escrituras deciden dentro de su propia sentencia</b> si hay algo que cambiar, y no
/// leyendo antes: dos cierres simultáneos pasarían los dos por un <c>SELECT</c> previo y el segundo
/// pisaría el sello del primero, que es justo lo que "idempotente en efecto" prohíbe (§5.2).
/// </remarks>
public sealed class RegistrationWindowRepository : IRegistrationWindowRepository
{
    private readonly ConvocatoriasDbContext _context;
    private readonly ILogger<RegistrationWindowRepository> _logger;
    private readonly TimeProvider _clock;

    public RegistrationWindowRepository(
        ConvocatoriasDbContext context,
        ILogger<RegistrationWindowRepository> logger,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(clock);

        _context = context;
        _logger = logger;
        _clock = clock;
    }

    /// <inheritdoc />
    public Task<Result<RegistrationWindow, BaseError>> GetAsync(Guid eventId, CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "leer la ventana de inscripción", () => ReadAsync(eventId, cancellationToken));

    /// <inheritdoc />
    public Task<Result<IReadOnlySet<Guid>, BaseError>> ListShutAsync(
        IReadOnlyCollection<Guid> eventIds,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync<IReadOnlySet<Guid>>(_logger, "listar las ventanas cerradas", async () =>
        {
            if (eventIds.Count == 0)
                return new HashSet<Guid>();

            var shut = await _context.RegistrationWindows
                .AsNoTracking()
                .Where(w => eventIds.Contains(w.EventId) && w.RegistrationsClosedAt != null)
                .Select(w => w.EventId)
                .ToListAsync(cancellationToken);

            return shut.ToHashSet();
        });

    /// <inheritdoc />
    public Task<Result<RegistrationWindow, BaseError>> CloseRegistrationsAsync(
        Guid eventId,
        Guid userId,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "cerrar la ventana de inscripción", async () =>
        {
            var now = _clock.GetUtcNow();

            // La fila nace con el primer cierre. Si ya existe, solo se sella cuando está abierta:
            // cerrar lo cerrado no toca ni la hora ni quién.
            await _context.Database.ExecuteSqlAsync($"""
                INSERT INTO convocatorias.registration_window
                    (event_id, registrations_closed_at, registrations_closed_by_id)
                VALUES ({eventId}, {now}, {userId})
                ON CONFLICT (event_id) DO UPDATE
                SET registrations_closed_at = EXCLUDED.registrations_closed_at,
                    registrations_closed_by_id = EXCLUDED.registrations_closed_by_id
                WHERE registration_window.registrations_closed_at IS NULL
                """, cancellationToken);

            return await ReadAsync(eventId, cancellationToken);
        });

    /// <inheritdoc />
    public Task<Result<RegistrationWindow, BaseError>> ReopenRegistrationsAsync(
        Guid eventId,
        Guid userId,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "reabrir la ventana de inscripción", async () =>
        {
            var now = _clock.GetUtcNow();

            // Sin fila no hay nada que reabrir, y no se escribe una: la ausencia ya es abierta.
            await _context.RegistrationWindows
                .Where(w => w.EventId == eventId && w.RegistrationsClosedAt != null)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(w => w.RegistrationsClosedAt, (DateTimeOffset?)null)
                        .SetProperty(w => w.ReopenedAt, now)
                        .SetProperty(w => w.ReopenedById, userId),
                    cancellationToken);

            return await ReadAsync(eventId, cancellationToken);
        });

    private async Task<RegistrationWindow> ReadAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var row = await _context.RegistrationWindows
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.EventId == eventId, cancellationToken);

        return row is null
            ? RegistrationWindow.Open(eventId)
            : new RegistrationWindow(
                row.EventId,
                row.RegistrationsClosedAt,
                row.RegistrationsClosedById,
                row.ReopenedAt,
                row.ReopenedById);
    }
}
