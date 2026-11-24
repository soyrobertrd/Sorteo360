using System.Collections.Concurrent;
using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.Errors;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Tests.Integration.Infrastructure;

/// <summary>
/// La puerta de cada evento, programada por el test. Un evento sin programar responde como
/// convocatorias caída: <c>ATTENDANCE_UNAVAILABLE</c>, nunca una lista vacía.
/// </summary>
public sealed class FakeAttendanceSource : IAttendanceSource
{
    private readonly ConcurrentDictionary<Guid, Result<IReadOnlyList<Attendee>, BaseError>> _doors = new();
    private readonly ConcurrentDictionary<Guid, int> _calls = new();

    /// <summary>Cuántas veces se le preguntó por la puerta del evento: lo que prueba que no hubo copia.</summary>
    public int CallCount(Guid eventId) => _calls.GetValueOrDefault(eventId);

    /// <summary>Quién está en la puerta del evento desde ahora.</summary>
    public void SetDoor(Guid eventId, params Attendee[] attendees) =>
        _doors[eventId] = Result.Success<IReadOnlyList<Attendee>, BaseError>(attendees);

    /// <summary>La puerta del evento deja de contestar.</summary>
    public void Fail(Guid eventId) =>
        _doors[eventId] = Result.Failure<IReadOnlyList<Attendee>, BaseError>(RaffleErrors.AttendanceUnavailable());

    public Task<Result<IReadOnlyList<Attendee>, BaseError>> GetCheckedInAsync(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        _calls.AddOrUpdate(eventId, 1, (_, count) => count + 1);
        return Task.FromResult(_doors.TryGetValue(eventId, out var door)
            ? door
            : Result.Failure<IReadOnlyList<Attendee>, BaseError>(RaffleErrors.AttendanceUnavailable()));
    }

    /// <summary>Un invitado con contacto en el CRM: pasa todos los pasos salvo que su correo diga otra cosa.</summary>
    public static Attendee Invitee(string email, string firstName = "Nombre", string lastName = "Apellido") => new(
        email, firstName, lastName, "Agencia", Guid.NewGuid(), AttendeeOrigin.Invited,
        new DateTimeOffset(2026, 10, 1, 19, 0, 0, TimeSpan.Zero));

    /// <summary>Tantos invitados distintos como se pidan.</summary>
    public static Attendee[] Invitees(int count) =>
        Enumerable.Range(0, count).Select(i => Invitee($"persona{i}@agencia.com")).ToArray();
}
