using System.Text.Json;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Sorteo360.Events.Core.Errors;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Infrastructure.Convocatorias;

/// <summary>
/// La puerta del evento, leída en vivo de convocatorias (spec 01 §7, spec 09 §7). Es todo o nada:
/// cualquier cosa que no sea una lista entera y legible es <c>ATTENDANCE_UNAVAILABLE</c>, nunca una
/// lista parcial ni una vacía. Una vacía es "nadie entró", y confundirla con "no se pudo saber"
/// congelaría un bombo vacío.
/// <para>
/// La procedencia la calcula convocatorias y acá solo se traduce: deducirla de este lado de la
/// frontera es lo que contaba a los referidos como invitados.
/// </para>
/// </summary>
public sealed class ConvocatoriasAttendanceSource : IAttendanceSource
{
    private readonly IConvocatoriasCheckInClient _client;
    private readonly ILogger<ConvocatoriasAttendanceSource> _logger;

    public ConvocatoriasAttendanceSource(IConvocatoriasCheckInClient client, ILogger<ConvocatoriasAttendanceSource> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(logger);

        _client = client;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<Attendee>, BaseError>> GetCheckedInAsync(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        ConvocatoriasCheckInList? list;

        try
        {
            list = await _client.ListCheckInsAsync(eventId, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            // También llega por acá el circuito abierto, que el chasis convierte en esta excepción.
            _logger.LogWarning(exception, "No se pudo leer la lista de check-ins del evento {EventId} en convocatorias.", eventId);
            return Unavailable();
        }
        catch (JsonException exception)
        {
            _logger.LogError(exception, "Convocatorias devolvió un cuerpo ilegible al leer los check-ins del evento {EventId}.", eventId);
            return Unavailable();
        }

        if (list?.Items is not { } rows)
        {
            _logger.LogError("Convocatorias devolvió los check-ins del evento {EventId} sin items.", eventId);
            return Unavailable();
        }

        var attendees = new List<Attendee>(rows.Count);

        foreach (var row in rows)
        {
            if (ToAttendee(row) is not { } attendee)
            {
                // Una fila ilegible tumba la lectura entera: saltarla dejaría fuera del bombo a alguien
                // que entró, sin que nadie se entere.
                _logger.LogError(
                    "Convocatorias devolvió en los check-ins del evento {EventId} una fila incompleta o con procedencia desconocida ({Origin}).",
                    eventId,
                    row.Origin);
                return Unavailable();
            }

            attendees.Add(attendee);
        }

        return attendees;
    }

    /// <summary>La fila como asistente, o nulo si le falta un campo obligatorio o su procedencia no se conoce.</summary>
    private static Attendee? ToAttendee(ConvocatoriasCheckIn row)
    {
        if (row is not { EmailNormalized: { } email, FirstName: { } firstName, LastName: { } lastName, CheckedInAt: { } checkedInAt }
            || ToOrigin(row.Origin) is not { } origin)
        {
            return null;
        }

        return new Attendee(email, firstName, lastName, row.Company, row.ContactId, origin, checkedInAt);
    }

    /// <summary>
    /// Los tres valores que convocatorias escribe, tal cual. Sin <c>JsonStringEnumConverter</c>: este
    /// servicio no lo tiene, y el nombre en la red no es el del enum.
    /// </summary>
    private static AttendeeOrigin? ToOrigin(string? origin) => origin switch
    {
        "invited" => AttendeeOrigin.Invited,
        "referred" => AttendeeOrigin.Referred,
        "walk_in" => AttendeeOrigin.WalkIn,
        _ => null,
    };

    private static Result<IReadOnlyList<Attendee>, BaseError> Unavailable() =>
        Result.Failure<IReadOnlyList<Attendee>, BaseError>(RaffleErrors.AttendanceUnavailable());
}
