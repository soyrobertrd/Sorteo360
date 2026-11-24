using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Errors;

/// <summary>
/// Rechazos propios del sorteo de un evento. Los del evento que lo contiene —que no exista, o que
/// ya esté cerrado o cancelado— son los mismos códigos que usan las cotizaciones, y se reutilizan.
/// </summary>
public static class RaffleErrors
{
    /// <summary>
    /// El evento ya tiene su sorteo: hay a lo sumo uno por evento. El servicio lo comprueba antes
    /// de escribir para responder con este código; el índice único es quien lo garantiza.
    /// </summary>
    public static BaseError AlreadyExists() => new(
        "El evento ya tiene un sorteo.",
        "RAFFLE_ALREADY_EXISTS",
        ErrorKind.Conflict);

    /// <summary>El evento existe pero todavía no tiene sorteo.</summary>
    public static BaseError NotFound() => new(
        "El evento no tiene sorteo.",
        "RAFFLE_NOT_FOUND",
        ErrorKind.NotFound);

    /// <summary>
    /// El sorteo ya se completó: el acta está generada y lo que atestigua quedó fijo, así que no
    /// admite cambios.
    /// </summary>
    public static BaseError Completed() => new(
        "El sorteo ya está completado y no admite cambios.",
        "RAFFLE_COMPLETED",
        ErrorKind.Conflict);

    /// <summary>
    /// Reemitir el acta de un sorteo que no se completó: no hay acta que corregir hasta que el
    /// completar genera la primera (spec 03 §4.5).
    /// </summary>
    public static BaseError NotCompleted() => new(
        "El sorteo todavía no está completado: el acta solo se puede reemitir después de completarlo.",
        "RAFFLE_NOT_COMPLETED",
        ErrorKind.Conflict);

    /// <summary>
    /// El sorteo no está en curso: en draft todavía no hay bombo congelado del que sortear, y el
    /// bombo de un sorteo completado no se lee (spec 02 §5, §7).
    /// </summary>
    public static BaseError NotInProgress() => new(
        "El sorteo no está en curso.",
        "RAFFLE_NOT_IN_PROGRESS",
        ErrorKind.Conflict);

    /// <summary>
    /// Una anulación sobre un premio sin ganador vivo: nunca se sorteó, o lo último que tuvo ya se
    /// anuló (spec 02 §7).
    /// </summary>
    public static BaseError PrizeNotDrawn() => new(
        "Este premio no tiene un ganador que anular.",
        "PRIZE_NOT_DRAWN",
        ErrorKind.Conflict);

    /// <summary>
    /// El sorteo vivo del premio ya tiene una entrega viva: ni se entrega dos veces ni se anula. Lo
    /// único que lo devuelve es la reversión (spec 03 §3.4).
    /// </summary>
    public static BaseError PrizeAlreadyRedeemed() => new(
        "Este premio ya fue entregado.",
        "PRIZE_ALREADY_REDEEMED",
        ErrorKind.Conflict);

    /// <summary>
    /// Una reversión sobre un premio sin entrega viva: no se sorteó, o se sorteó y nadie lo retiró
    /// todavía (spec 03 §3.4).
    /// </summary>
    public static BaseError PrizeNotRedeemed() => new(
        "Este premio no tiene una entrega que revertir.",
        "PRIZE_NOT_REDEEMED",
        ErrorKind.Conflict);

    /// <summary>
    /// Ningún sorteo tiene ese código, o lo escrito no llega a ser un código: al mostrador no le dice
    /// nada distinguirlos (spec 03 §2.4). Un código anulado sí existe y se responde.
    /// </summary>
    public static BaseError RedemptionCodeNotFound() => new(
        "No existe ningún premio con ese código de canje.",
        "REDEMPTION_CODE_NOT_FOUND",
        ErrorKind.NotFound);

    /// <summary>
    /// El premio ya tiene un sorteo, vivo o anulado: el acta lo nombra, así que ni se edita ni se
    /// elimina (spec 00 §4).
    /// </summary>
    public static BaseError PrizeHasDraws() => new(
        "Este premio ya se sorteó y no se puede modificar ni eliminar.",
        "PRIZE_HAS_DRAWS",
        ErrorKind.Conflict);

    /// <summary>
    /// Un logo para un premio sin patrocinador: el logo es de la empresa que lo patrocina, y sin ella
    /// no hay de quién mostrarlo (spec 00 §8).
    /// </summary>
    public static BaseError PrizeNotSponsored() => new(
        "Este premio no tiene patrocinador. Elija la empresa patrocinadora antes de subir su logo.",
        "PRIZE_NOT_SPONSORED",
        ErrorKind.Conflict);

    /// <summary>
    /// La ronda tiene un premio ya sorteado. Mismo código que el del premio, con una prosa sobre la
    /// ronda: su nombre es un encabezado del acta que se leyó en voz alta (spec 00 §4).
    /// </summary>
    public static BaseError RoundHasDraws() => new(
        "Esta ronda tiene premios ya sorteados y no se puede modificar.",
        "PRIZE_HAS_DRAWS",
        ErrorKind.Conflict);

    /// <summary>
    /// Reabrir un sorteo que ya sorteó algo, aunque se haya anulado: desde el primer sorteo toda
    /// corrección es una anulación, que deja rastro (spec 02 §6).
    /// </summary>
    public static BaseError HasDraws() => new(
        "El sorteo ya tiene premios sorteados y no se puede reabrir.",
        "RAFFLE_HAS_DRAWS",
        ErrorKind.Conflict);

    /// <summary>
    /// Completar sin las seis autoridades: el acta que se genera al completar las nombra a cada una
    /// con una línea para firmar, y un blanco nunca se rellena con un nombre inventado (spec 00 §6).
    /// </summary>
    public static BaseError AuthoritiesRequired() => new(
        "Para completar el sorteo debe registrar el nombre y el documento de identidad del notario y de los dos testigos.",
        "RAFFLE_AUTHORITIES_REQUIRED",
        ErrorKind.Conflict);

    /// <summary>
    /// Completar sin decir dónde y hasta cuándo se retiran los premios: es lo que el correo al
    /// ganador le dice, y sale al completar (spec 03 §5.3).
    /// </summary>
    public static BaseError PickupInstructionsRequired() => new(
        "Para completar el sorteo debe indicar dónde y hasta cuándo se retiran los premios.",
        "RAFFLE_PICKUP_INSTRUCTIONS_REQUIRED",
        ErrorKind.Conflict);

    /// <summary>
    /// La ronda no es de este sorteo. Una ronda de otro sorteo responde igual que una inexistente:
    /// un identificador ajeno nunca confirma que exista en otra parte.
    /// </summary>
    public static BaseError RoundNotFound() => new(
        "La ronda no existe en este sorteo.",
        "ROUND_NOT_FOUND",
        ErrorKind.NotFound);

    /// <summary>El premio no es de este sorteo, con la misma regla que la ronda.</summary>
    public static BaseError PrizeNotFound() => new(
        "El premio no existe en este sorteo.",
        "PRIZE_NOT_FOUND",
        ErrorKind.NotFound);

    /// <summary>
    /// La ronda todavía tiene premios. No se eliminan en cascada: el operador decide si los mueve
    /// a otra ronda o los elimina.
    /// </summary>
    public static BaseError RoundNotEmpty() => new(
        "La ronda tiene premios. Muévalos o elimínelos antes de eliminar la ronda.",
        "ROUND_NOT_EMPTY",
        ErrorKind.Conflict);

    /// <summary>
    /// No se pudo leer en vivo quién está registrado en la puerta. Es una caída y no un conflicto
    /// de estado: el mismo pedido puede salir bien en unos minutos.
    /// </summary>
    public static BaseError AttendanceUnavailable() => new(
        "No se pudo obtener la lista de asistentes registrados en puerta. Intente de nuevo en unos minutos.",
        "ATTENDANCE_UNAVAILABLE",
        ErrorKind.Unavailable);

    /// <summary>
    /// La puerta tiene más asistentes de los que un sorteo admite. Se rechaza en voz alta en lugar
    /// de recortar: un bombo recortado es un acta equivocada.
    /// </summary>
    public static BaseError AttendanceTooManyRows() => new(
        "El evento tiene más de 25.000 asistentes registrados en puerta, el máximo que admite un sorteo.",
        "ATTENDANCE_TOO_MANY_ROWS",
        ErrorKind.Conflict);

    /// <summary>
    /// El sorteo ya arrancó: su lista de participantes quedó congelada y no hay nada que decidir
    /// a mano (spec 01 §9).
    /// </summary>
    public static BaseError NotDraft() => new(
        "El sorteo ya arrancó; su lista de participantes no admite cambios.",
        "RAFFLE_NOT_DRAFT",
        ErrorKind.Conflict);

    /// <summary>
    /// Arrancar dejaría el bombo vacío: nadie en la puerta, todos fuera, o una lista de DO Travel
    /// Awards que no coincide con nadie. Sin cuentas: la lectura de asistentes ya explica por qué
    /// (spec 01 §9).
    /// </summary>
    public static BaseError NoParticipants() => new(
        "Nadie en la puerta cumple los requisitos del sorteo, así que no hay a quién sortear.",
        "RAFFLE_NO_PARTICIPANTS",
        ErrorKind.Conflict);

    /// <summary>
    /// Una inclusión manual que nombra a quien llegó sin invitación. Dejarla pasar reabriría, de a
    /// una persona, el hueco que la regla cierra (spec 01 §2).
    /// </summary>
    public static BaseError ParticipantWalkIn() => new(
        "Esta persona se registró sin invitación y no puede entrar al sorteo.",
        "PARTICIPANT_WALK_IN",
        ErrorKind.Conflict);

    /// <summary>
    /// Una inclusión manual sobre alguien que el CRM no conoce: sin contacto, o con uno que el CRM
    /// ya no devuelve. Sería la única persona del bombo que nadie comprobó (spec 01 §3.1, §3.2).
    /// </summary>
    public static BaseError ParticipantContactUnresolved() => new(
        "Esta persona no tiene contacto en el CRM, así que no se puede comprobar si se le puede invitar.",
        "PARTICIPANT_CONTACT_UNRESOLVED",
        ErrorKind.Conflict);

    /// <summary>
    /// Una inclusión manual sobre alguien que el CRM tiene suspendido. No se escribe nada: como la
    /// lista negra, la suspensión está por encima de la persona (spec 01 §3, §6).
    /// </summary>
    public static BaseError ParticipantSuspended() => new(
        "Esta persona está suspendida en el CRM y no puede entrar al sorteo.",
        "PARTICIPANT_SUSPENDED",
        ErrorKind.Conflict);

    /// <summary>
    /// Una inclusión manual sobre alguien que el CRM tiene vetado. No se escribe nada: la lista
    /// negra está por encima de la persona (spec 01 §3).
    /// </summary>
    public static BaseError ParticipantDoNotInvite() => new(
        "El CRM tiene marcada a esta persona para no invitarla.",
        "PARTICIPANT_DO_NOT_INVITE",
        ErrorKind.Conflict);

    /// <summary>Una decisión manual sobre alguien que no está en la puerta del evento.</summary>
    public static BaseError ParticipantNotCheckedIn() => new(
        "Esta persona no está registrada en la puerta del evento.",
        "PARTICIPANT_NOT_CHECKED_IN",
        ErrorKind.Conflict);

    /// <summary>
    /// No se pudo preguntar al CRM a quién se puede invitar. Mismo código que el de los directorios
    /// —es el mismo CRM caído, y quien reintenta no necesita saber qué se consultaba—, con una
    /// prosa que no habla de un directorio que el sorteo no consulta.
    /// </summary>
    public static BaseError CrmUnavailable() => new(
        "No se pudo consultar el CRM. Intente de nuevo en unos minutos.",
        "CRM_UNAVAILABLE",
        ErrorKind.Unavailable);

    /// <summary>
    /// Reactivar a un participante que no tiene ninguna anulación activa que levantar.
    /// </summary>
    public static BaseError AttendeeNotVoided() => new(
        "El participante no tiene una anulación activa que reactivar.",
        "RAFFLE_ATTENDEE_NOT_VOIDED",
        ErrorKind.Conflict);
}
