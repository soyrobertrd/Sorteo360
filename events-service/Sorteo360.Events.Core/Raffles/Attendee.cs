namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Una persona registrada en la puerta del evento, tal como la entrega la lista de asistencia en
/// vivo: calificada para el sorteo o no, porque clasificarla es trabajo del sorteo y no de quien
/// la entrega.
/// <para>
/// La identidad es <paramref name="IdentityKey"/> (ver <see cref="PersonIdentity"/>): el correo
/// normalizado si lo tiene, o su ID si no. <paramref name="ContactId"/> viaja con ella
/// solo como el identificador con el que se le pregunta al CRM, y es nulo mientras la escritura del
/// contacto de quien se registró sigue pendiente en el CRM.
/// </para>
/// <para>
/// No es la fila congelada del sorteo. Lo que se escribe al arrancar es otro tipo,
/// <c>RaffleAttendee</c>, con su propia tabla; este es solo lo que se lee de la puerta.
/// </para>
/// </summary>
public sealed record Attendee(
    string IdentityKey,
    string FirstName,
    string LastName,
    string? Company,
    Guid? ContactId,
    AttendeeOrigin Origin,
    DateTimeOffset CheckedInAt)
{
    /// <summary>El correo de la persona, o nulo si entró solo con su ID.</summary>
    public string? Email => PersonIdentity.EmailOf(IdentityKey);
}

/// <summary>
/// Cómo llegó a registrarse quien está en la puerta. Lo calcula convocatorias y aquí solo se lee:
/// volver a deducirlo de este lado de la frontera es lo que antes contaba a los referidos como
/// invitados por accidente.
/// </summary>
public enum AttendeeOrigin
{
    /// <summary>Se registró con la invitación que recibió.</summary>
    Invited,

    /// <summary>Se registró con una invitación que otra persona le reenvió.</summary>
    Referred,

    /// <summary>Se registró en un evento público sin haber recibido ninguna invitación.</summary>
    WalkIn,
}
