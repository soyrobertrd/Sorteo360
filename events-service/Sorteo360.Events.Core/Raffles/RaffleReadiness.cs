namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Si el sorteo puede arrancar y por qué no, más los avisos que no lo frenan (spec 01 §8.1). Son
/// dos listas porque el cliente hace dos cosas distintas con ellas: una desactiva el botón y la
/// otra es un aviso.
/// <para>
/// Solo un borrador tiene un arranque para el que estar listo: fuera de <c>draft</c>,
/// <paramref name="IsReadyToStart"/> y <paramref name="BlockingReasons"/> son nulos.
/// <paramref name="Warnings"/> sigue, por <c>no_prizes</c> y por <c>awards_list_not_in_force</c>, que
/// entonces se lee de la lista que quedó escrita al arrancar.
/// </para>
/// </summary>
public sealed record RaffleReadiness(
    bool? IsReadyToStart,
    IReadOnlyList<string>? BlockingReasons,
    IReadOnlyList<string> Warnings);

/// <summary>Lo que impide arrancar el sorteo.</summary>
public static class RaffleBlockingReason
{
    /// <summary>El bombo está vacío. Solo cuando de verdad se leyó una lista de la puerta.</summary>
    public const string NoQualifiedAttendees = "no_qualified_attendees";

    /// <summary>La puerta no contesta y no hay ninguna copia: no se sabe quién está.</summary>
    public const string AttendanceUnavailable = "attendance_unavailable";

    /// <summary>La puerta pasa del techo de asistentes que admite un sorteo.</summary>
    public const string AttendanceTooManyRows = "attendance_too_many_rows";

    /// <summary>
    /// El CRM no contestó: la lista se clasificó sin el paso de la lista negra, así que puede
    /// contar a alguien que el CRM habría rechazado.
    /// </summary>
    public const string CrmUnavailable = "crm_unavailable";
}

/// <summary>Lo que conviene saber antes de arrancar, sin frenarlo.</summary>
public static class RaffleWarning
{
    /// <summary>No hay lista de DO Travel Awards en vigor, y el paso se está saltando.</summary>
    public const string AwardsListNotInForce = "awards_list_not_in_force";

    /// <summary>
    /// La lista de la puerta que se muestra es una copia vieja: la última lectura falló. Solo en
    /// draft: después del congelado nada es una copia.
    /// </summary>
    public const string AttendanceCopyStale = "attendance_copy_stale";

    /// <summary>El sorteo no tiene premios. No frena: se pueden agregar en curso.</summary>
    public const string NoPrizes = "no_prizes";
}

/// <summary>El sorteo con sus rondas y premios, y si está listo para arrancar.</summary>
public sealed record RaffleDetail(RaffleWithRounds Raffle, RaffleReadiness Readiness);

/// <summary>
/// La lista de la puerta clasificada, con lo que el operador necesita para confiar en ella: de
/// cuándo es, qué tan vigente, y qué lista de DO Travel Awards se aplicó.
/// <para>
/// En draft se calcula en cada lectura; fuera de draft son las filas que escribió el arranque, con
/// la hora del arranque como la de la lectura. Sin ninguna copia de la puerta,
/// <paramref name="Attendees"/> es nulo: no se sabe quién está, y eso no es lo mismo que nadie.
/// </para>
/// </summary>
public sealed record RaffleAttendees(
    string AttendanceStatus,
    DateTimeOffset? AttendanceFetchedAt,
    Guid? AwardsBatchId,
    IReadOnlyList<RaffleAttendee>? Attendees);
