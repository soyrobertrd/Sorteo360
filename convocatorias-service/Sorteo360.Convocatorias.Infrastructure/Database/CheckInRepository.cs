using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sorteo360.Convocatorias.Core.CheckIns;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Platform.Database;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Infrastructure.Database;

/// <inheritdoc cref="ICheckInRepository" />
public sealed class CheckInRepository : ICheckInRepository
{
    /// <summary>Cuántas veces sella un escaneo al que una deshecha le borró el sello a mitad de camino.</summary>
    private const int MaxStampAttempts = 2;

    private readonly ConvocatoriasDbContext _context;
    private readonly ILogger<CheckInRepository> _logger;

    public CheckInRepository(ConvocatoriasDbContext context, ILogger<CheckInRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<Maybe<CheckInTarget>, BaseError>> FindByQrCodeAsync(
        string qrCode,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "buscar la inscripción del código QR", async () =>
        {
            // ux_registration_qr_code sirve esta lectura: el código es único en todo el servicio.
            var target = await _context.Registrations
                .AsNoTracking()
                .Where(registration => registration.QrCode == qrCode)
                .Select(registration => new CheckInTarget(registration.Id, registration.EventId))
                .FirstOrDefaultAsync(cancellationToken);

            return target is null ? Maybe<CheckInTarget>.None : Maybe.From(target);
        });

    /// <inheritdoc />
    public Task<Result<Maybe<CheckInTarget>, BaseError>> FindByIdAsync(
        Guid registrationId,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "buscar la inscripción por su id", async () =>
        {
            var target = await _context.Registrations
                .AsNoTracking()
                .Where(registration => registration.Id == registrationId)
                .Select(registration => new CheckInTarget(registration.Id, registration.EventId))
                .FirstOrDefaultAsync(cancellationToken);

            return target is null ? Maybe<CheckInTarget>.None : Maybe.From(target);
        });

    /// <inheritdoc />
    public Task<Result<CheckInStamp, BaseError>> StampAsync(
        Guid registrationId,
        DateTimeOffset now,
        Guid checkedInById,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "hacer el check-in", async () =>
        {
            // Una deshecha que entra entre el UPDATE y la relectura deja la fila sin check-in; el
            // escaneo vuelve a sellar una vez en vez de fallar. Que eso pase dos veces seguidas
            // con el mismo código no es una carrera de la puerta, es un error.
            for (var attempt = 1; attempt <= MaxStampAttempts; attempt++)
            {
                // La condición viaja dentro del UPDATE (spec 09 §3.2): de dos escaneos a la vez,
                // uno sella y el otro no cambia nada, y el sello que queda es el del primero.
                var changed = await _context.Registrations
                    .Where(registration => registration.Id == registrationId && registration.CheckedInAt == null)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(registration => registration.CheckedInAt, now)
                            .SetProperty(registration => registration.CheckedInById, checkedInById)
                            .SetProperty(registration => registration.UpdatedAt, now),
                        cancellationToken);

                var row = await _context.Registrations
                    .AsNoTracking()
                    .Where(registration => registration.Id == registrationId)
                    .Select(registration => new
                    {
                        registration.Id,
                        registration.FirstName,
                        registration.LastName,
                        registration.Company,
                        registration.Reference,
                        registration.CheckedInAt,
                        registration.CheckedInById,
                    })
                    .SingleAsync(cancellationToken);

                if (row.CheckedInAt is { } checkedInAt && row.CheckedInById is { } stampedById)
                {
                    return new CheckInStamp(
                        changed == 1,
                        row.Id,
                        row.FirstName,
                        row.LastName,
                        row.Company,
                        row.Reference,
                        checkedInAt,
                        stampedById);
                }
            }

            throw new InvalidOperationException(
                $"La inscripción {registrationId} perdió el check-in {MaxStampAttempts} veces seguidas al sellarla.");
        });

    /// <inheritdoc />
    public Task<Result<Maybe<CheckInClearance>, BaseError>> ClearAsync(
        Guid eventId,
        Guid registrationId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "deshacer el check-in", async () =>
        {
            var current = await _context.Registrations
                .AsNoTracking()
                .Where(registration => registration.Id == registrationId && registration.EventId == eventId)
                .Select(registration => new { registration.CheckedInAt, registration.CheckedInById })
                .FirstOrDefaultAsync(cancellationToken);

            if (current is null)
                return Maybe<CheckInClearance>.None;

            if (current.CheckedInAt is null)
                return Maybe.From(new CheckInClearance(null));

            // La condición va dentro del UPDATE y nombra el check-in leído: si otra deshecha llegó
            // primero, esta no borra nada y no se registra dos veces; y si además alguien volvió a
            // escanear, ese check-in nuevo no es el que se quiso deshacer y se queda.
            var cleared = await _context.Registrations
                .Where(registration => registration.Id == registrationId
                    && registration.EventId == eventId
                    && registration.CheckedInAt == current.CheckedInAt
                    && registration.CheckedInById == current.CheckedInById)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(registration => registration.CheckedInAt, (DateTimeOffset?)null)
                        .SetProperty(registration => registration.CheckedInById, (Guid?)null)
                        .SetProperty(registration => registration.UpdatedAt, now),
                    cancellationToken);

            return Maybe.From(new CheckInClearance(cleared == 1
                ? new ClearedCheckIn(current.CheckedInAt.Value, current.CheckedInById!.Value)
                : null));
        });
}
