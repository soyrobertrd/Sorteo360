using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Participants;
using Sorteo360.Convocatorias.Core.Tracking;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Tracking;

/// <summary>Los conteos del resumen del evento sobre la fila ya unida (spec <c>07</c> §2).</summary>
public sealed class TrackingRollupTests
{
    [Fact]
    public void Count_ReturnsEachInviteeUnderTheirSendStatusOnly_WhenInviteesSitInEveryStatus()
    {
        var rollup = TrackingRollup.Count(
        [
            Invitee(RecipientSendStatus.HandedOff),
            Invitee(RecipientSendStatus.HandedOff),
            Invitee(RecipientSendStatus.Pending),
            Invitee(RecipientSendStatus.Failed),
            Invitee(RecipientSendStatus.Suppressed),
            WalkIn(),
        ]);

        rollup.People.ShouldBe(new TrackingPeople(Invited: 2, PendingSend: 1, Failed: 1, Suppressed: 1));
    }

    [Fact]
    public void Count_ReturnsRegistrationsSplitByOrigin_WhenEveryOriginRegistered()
    {
        var rollup = TrackingRollup.Count(
        [
            Invitee(RecipientSendStatus.HandedOff, registered: true),
            Invitee(RecipientSendStatus.HandedOff),
            Referred(),
            WalkIn(),
            WalkIn(),
        ]);

        rollup.Registrations.ShouldBe(new TrackingRegistrations(Total: 4, FromInvitation: 1, Referred: 1, WalkIn: 2, CheckedIn: 0));
    }

    /// <summary>
    /// Las inscripciones con check-in (spec <c>09</c> §6), de cualquier origen. Nadie sin inscripción
    /// tiene check-in, así que no hay invitado que contar.
    /// </summary>
    [Fact]
    public void Count_ReturnsTheRegistrationsWithACheckIn_WhenSomeOfEveryOriginCheckedIn()
    {
        var rollup = TrackingRollup.Count(
        [
            Invitee(RecipientSendStatus.HandedOff, registered: true) with { CheckedInAt = DateTimeOffset.UtcNow },
            Invitee(RecipientSendStatus.HandedOff, registered: true),
            Referred() with { CheckedInAt = DateTimeOffset.UtcNow },
            WalkIn() with { CheckedInAt = DateTimeOffset.UtcNow },
            WalkIn(),
            Invitee(RecipientSendStatus.HandedOff),
        ]);

        rollup.Registrations.CheckedIn.ShouldBe(3);
    }

    /// <summary>
    /// Solo quien recibió el correo y no se inscribió (§2). Las entradas libres nunca: en un evento
    /// público concurrido el número se volvería negativo. Tampoco quien no llegó a recibirlo, que no
    /// está en <c>invited</c>.
    /// </summary>
    [Fact]
    public void Count_ReturnsOnlyHandedOffInviteesWithoutARegistration_WhenWalkInsAndFailedInviteesExist()
    {
        var rollup = TrackingRollup.Count(
        [
            Invitee(RecipientSendStatus.HandedOff, registered: true),
            Invitee(RecipientSendStatus.HandedOff),
            Invitee(RecipientSendStatus.HandedOff),
            Invitee(RecipientSendStatus.Failed),
            Invitee(RecipientSendStatus.Suppressed),
            WalkIn(),
            WalkIn(),
            Referred(),
        ]);

        rollup.NotRegistered.ShouldBe(2);
    }

    [Fact]
    public void Count_ReturnsZeros_WhenTheEventHasNobody()
    {
        var rollup = TrackingRollup.Count([]);

        rollup.People.ShouldBe(new TrackingPeople(0, 0, 0, 0));
        rollup.Registrations.ShouldBe(new TrackingRegistrations(0, 0, 0, 0, 0));
        rollup.NotRegistered.ShouldBe(0);
    }

    #region Ayudas

    private static Participant Invitee(string sendStatus, bool registered = false) => Row(
        ParticipantOrigin.Invited, sendStatus, registered);

    private static Participant Referred() => Row(ParticipantOrigin.Referred, ParticipantSendStatus.None, registered: true);

    private static Participant WalkIn() => Row(ParticipantOrigin.WalkIn, ParticipantSendStatus.None, registered: true);

    private static Participant Row(string origin, string sendStatus, bool registered) => new(
        $"{Guid.NewGuid():N}@acme.com", "Juan", "Pérez", null, origin, sendStatus, registered,
        registered ? DateTimeOffset.UtcNow : null, null, null, [], false, false);

    #endregion
}
