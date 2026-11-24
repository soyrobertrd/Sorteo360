using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Participants;

namespace Sorteo360.Convocatorias.Core.Tracking;

/// <summary>
/// Las personas invitadas por su estado de entrega. Cada persona cae en uno solo, el que más lejos
/// llegó entre sus campañas, así que dos campañas no la cuentan dos veces.
/// </summary>
/// <param name="Invited">
/// Entregadas al proveedor, una vez por persona. Es el número que el cliente repite, así que ningún
/// otro estado se pliega en él.
/// </param>
public sealed record TrackingPeople(int Invited, int PendingSend, int Failed, int Suppressed);

/// <summary>Las inscripciones del evento, por cómo llegó cada persona.</summary>
/// <param name="CheckedIn">
/// Las que tienen check-in (spec <c>09</c> §6). Una inscripción es una persona del evento, así que es
/// el mismo número que <c>totals.checkedIn</c> de la lista de acceso.
/// </param>
public sealed record TrackingRegistrations(int Total, int FromInvitation, int Referred, int WalkIn, int CheckedIn);

/// <summary>
/// Los conteos del resumen del evento que salen de la lista de participantes (spec <c>07</c> §2).
/// </summary>
public sealed record TrackingRollup(TrackingPeople People, TrackingRegistrations Registrations, int NotRegistered)
{
    /// <summary>
    /// Cuenta sobre las filas ya unidas de <see cref="IParticipantService.ListAsync"/>, que es la
    /// única unión: una persona es una fila, y los borradores ya vienen fuera.
    /// <para>
    /// <c>notRegistered</c> es de las mismas personas que <c>invited</c> y sin inscripción. Nunca
    /// cuenta entradas libres ni reenviadas, que no tienen estado de entrega, ni a quien el correo
    /// no le llegó a salir: no se le puede reclamar que no se inscribiera.
    /// </para>
    /// </summary>
    public static TrackingRollup Count(IReadOnlyCollection<Participant> participants)
    {
        var invited = participants.Where(p => p.SendStatus == RecipientSendStatus.HandedOff).ToList();
        int WithStatus(string status) => participants.Count(p => p.SendStatus == status);
        int RegisteredFrom(string origin) => participants.Count(p => p.Registered && p.Origin == origin);

        return new TrackingRollup(
            new TrackingPeople(
                invited.Count,
                WithStatus(RecipientSendStatus.Pending),
                WithStatus(RecipientSendStatus.Failed),
                WithStatus(RecipientSendStatus.Suppressed)),
            new TrackingRegistrations(
                participants.Count(p => p.Registered),
                RegisteredFrom(ParticipantOrigin.Invited),
                RegisteredFrom(ParticipantOrigin.Referred),
                RegisteredFrom(ParticipantOrigin.WalkIn),
                participants.Count(p => p.CheckedInAt is not null)),
            invited.Count(p => !p.Registered));
    }
}
