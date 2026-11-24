using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.CheckIns;

/// <inheritdoc />
public sealed class CheckInService : ICheckInService
{
    private readonly ICheckInRepository _repository;
    private readonly TimeProvider _clock;
    private readonly ILogger<CheckInService> _logger;

    public CheckInService(ICheckInRepository repository, TimeProvider clock, ILogger<CheckInService> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _repository = repository;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<CheckIn, BaseError>> CheckInAsync(
        Guid eventId,
        string qrCode,
        Guid checkedInById,
        CancellationToken cancellationToken)
    {
        var found = await _repository.FindByQrCodeAsync(qrCode, cancellationToken);

        return await CheckInFoundAsync(eventId, found, new CheckInCodeNotFoundError(), checkedInById, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<CheckIn, BaseError>> CheckInByRegistrationAsync(
        Guid eventId,
        Guid registrationId,
        Guid checkedInById,
        CancellationToken cancellationToken)
    {
        var found = await _repository.FindByIdAsync(registrationId, cancellationToken);

        return await CheckInFoundAsync(eventId, found, new RegistrationNotFoundError(), checkedInById, cancellationToken);
    }

    /// <summary>
    /// Lo que el escaneo y la lista de acceso comparten una vez buscada la inscripción, cada uno a
    /// su manera (spec <c>09</c> §5): de quién es, el sello y la respuesta. Solo cambia el error de
    /// no encontrarla.
    /// </summary>
    private async Task<Result<CheckIn, BaseError>> CheckInFoundAsync(
        Guid eventId,
        Result<Maybe<CheckInTarget>, BaseError> found,
        BaseError notFound,
        Guid checkedInById,
        CancellationToken cancellationToken)
    {
        if (found.IsFailure)
            return Result.Failure<CheckIn, BaseError>(found.Error);

        if (found.Value.HasNoValue)
            return Result.Failure<CheckIn, BaseError>(notFound);

        var target = found.Value.Value;

        // Una entrada de otro evento es un error que la puerta tiene que atajar, no seguir: la
        // terminal está puesta en un evento, y cambiarse sola al de la entrada es lo que se prohíbe.
        if (target.EventId != eventId)
            return Result.Failure<CheckIn, BaseError>(new CheckInWrongEventError());

        var stamped = await _repository.StampAsync(
            target.RegistrationId, _clock.GetUtcNow(), checkedInById, cancellationToken);

        return stamped.Map(stamp => new CheckIn(
            stamp.RegistrationId,
            stamp.FirstName,
            stamp.LastName,
            stamp.Company,
            stamp.Reference,
            stamp.CheckedInAt,
            stamp.CheckedInById,
            AlreadyCheckedIn: !stamp.Changed));
    }

    /// <inheritdoc />
    public async Task<UnitResult<BaseError>> UndoAsync(
        Guid eventId,
        Guid registrationId,
        Guid undoneById,
        CancellationToken cancellationToken)
    {
        var cleared = await _repository.ClearAsync(eventId, registrationId, _clock.GetUtcNow(), cancellationToken);

        if (cleared.IsFailure)
            return UnitResult.Failure(cleared.Error);

        if (cleared.Value.HasNoValue)
            return UnitResult.Failure<BaseError>(new RegistrationNotFoundError());

        var clearance = cleared.Value.Value;

        // La base olvida el check-in deshecho; esta línea es lo único que recuerda quién lo hizo y
        // quién lo deshizo (spec 09 §4, enmendada el 2026-10-08). Sin nada borrado no hay qué contar.
        if (clearance.Cleared is { } checkIn)
        {
            _logger.LogInformation(
                "Se deshizo el check-in de la inscripción {RegistrationId} del evento {EventId}: lo había hecho {CheckedInById} el {CheckedInAt} y lo deshizo {UndoneById}.",
                registrationId,
                eventId,
                checkIn.CheckedInById,
                checkIn.CheckedInAt,
                undoneById);
        }

        return UnitResult.Success<BaseError>();
    }
}
