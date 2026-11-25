using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.Database;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Infrastructure.Raffles;

/// <summary>
/// Fuente de asistencia en vivo basada en el padrón persistido del sorteo en Postgres:
/// devuelve los participantes con check-in activo sin depender de convocatorias-service ni del CRM.
/// </summary>
public sealed class RosterAttendanceSource : IAttendanceSource
{
    private readonly IRaffleParticipantRepository _participants;

    public RosterAttendanceSource(IRaffleParticipantRepository participants)
    {
        ArgumentNullException.ThrowIfNull(participants);
        _participants = participants;
    }

    public Task<Result<IReadOnlyList<Attendee>, BaseError>> GetCheckedInAsync(
        Guid eventId,
        CancellationToken cancellationToken) =>
        GetAttendeesAsync(eventId, requiresCheckIn: true, cancellationToken);

    public async Task<Result<IReadOnlyList<Attendee>, BaseError>> GetAttendeesAsync(
        Guid eventId,
        bool requiresCheckIn,
        CancellationToken cancellationToken)
    {
        var listResult = await _participants.ListByEventIdAsync(eventId, cancellationToken);
        if (listResult.IsFailure)
            return Result.Failure<IReadOnlyList<Attendee>, BaseError>(listResult.Error);

        var source = requiresCheckIn
            ? listResult.Value.Where(p => p.CheckedIn)
            : listResult.Value;

        IReadOnlyList<Attendee> attendees = source
            .Select(MapToAttendee)
            .ToList();

        return Result.Success<IReadOnlyList<Attendee>, BaseError>(attendees);
    }

    private static Attendee MapToAttendee(RaffleParticipant participant)
    {
        var (firstName, lastName) = SplitFullName(participant.FullName);

        return new Attendee(
            IdentityKey: participant.IdentityKey,
            FirstName: firstName,
            LastName: lastName,
            Company: participant.RuleGroup,
            ContactId: null,
            Origin: AttendeeOrigin.Invited,
            CheckedInAt: participant.CheckedInAt ?? participant.UpdatedAt);
    }

    private static (string FirstName, string LastName) SplitFullName(string fullName)
    {
        var trimmed = fullName.Trim();
        var separator = trimmed.IndexOf(' ');
        if (separator <= 0)
            return (trimmed, string.Empty);

        return (trimmed[..separator].Trim(), trimmed[(separator + 1)..].Trim());
    }
}

