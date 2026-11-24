namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Un asistente del sorteo tal como queda escrito al arrancar: una fila por persona en la puerta,
/// calificada o no (spec 01 §4.1). Lleva los nombres y no solo ids —la excepción acotada de ADR-0022
/// a ADR-0013— y una copia entera de la decisión manual, nota incluida, para que una sola lectura
/// responda al acta sin un join.
/// <para>
/// La lectura del borrador devuelve esta misma forma, calculada y sin guardar: un solo tipo a los
/// dos lados del congelado.
/// </para>
/// <para>
/// El <see cref="Id"/> nace acá, al clasificar, y es el de la fila que escribe el arranque: estable
/// desde entonces, el sorteo de premios ordena el bombo por él (spec 02 §2). En la lectura del
/// borrador no se guarda ni significa nada.
/// </para>
/// </summary>
public sealed record RaffleAttendee(
    Guid Id,
    string EmailNormalized,
    Guid? ContactId,
    string FirstName,
    string LastName,
    string? Company,
    DateTimeOffset CheckedInAt,
    bool Qualified,
    string? ExclusionReason,
    string? ManualDecision,
    Guid? ManualDecidedById,
    DateTimeOffset? ManualDecidedAt,
    string? ManualNote,
    string? OverruledReason,
    DateTimeOffset? ReinstatedAt = null,
    Guid? ReinstatedById = null)
{
    /// <summary>
    /// Cómo se lo nombra en la pantalla y en el sorteo: nombre y apellido, sin espacios sobrantes si
    /// falta alguno.
    /// </summary>
    public string DisplayName => $"{FirstName} {LastName}".Trim();

    /// <summary>
    /// Verdadero si el participante tiene algún sorteo anulado que no ha sido cubierto por una
    /// reactivación posterior. Si se le reactivó y volvió a ser anulado después, vuelve a quedar excluido.
    /// </summary>
    public bool IsVoidedAndNotReinstated(IReadOnlyList<PrizeDraw> draws)
    {
        ArgumentNullException.ThrowIfNull(draws);

        DateTimeOffset? latestVoid = null;
        foreach (var draw in draws)
        {
            if (draw.IsLive || draw.WinnerAttendeeId != Id || draw.VoidedAt is not { } voidedAt)
                continue;

            if (latestVoid is null || voidedAt > latestVoid.Value)
                latestVoid = voidedAt;
        }

        if (latestVoid is null)
            return false;

        return ReinstatedAt is null || ReinstatedAt.Value < latestVoid.Value;
    }

    /// <summary>
    /// Verdadero si el participante tuvo al menos una anulación y su reactivación vigente es igual o
    /// posterior a todas sus anulaciones.
    /// </summary>
    public bool IsCurrentlyReinstated(IReadOnlyList<PrizeDraw> draws)
    {
        ArgumentNullException.ThrowIfNull(draws);

        if (ReinstatedAt is not { } reinstatedAt)
            return false;

        var hasVoid = false;
        foreach (var draw in draws)
        {
            if (draw.IsLive || draw.WinnerAttendeeId != Id || draw.VoidedAt is not { } voidedAt)
                continue;

            hasVoid = true;
            if (reinstatedAt < voidedAt)
                return false;
        }

        return hasVoid;
    }

    /// <summary>La fila de un asistente ya clasificado, con la decisión manual que lo nombra.</summary>
    public static RaffleAttendee From(QualifiedAttendee classified)
    {
        ArgumentNullException.ThrowIfNull(classified);

        var attendee = classified.Attendee;
        var manual = classified.Override;
        return new RaffleAttendee(
            Guid.NewGuid(),
            attendee.EmailNormalized,
            attendee.ContactId,
            attendee.FirstName,
            attendee.LastName,
            attendee.Company,
            attendee.CheckedInAt,
            classified.Qualified,
            classified.ExclusionReason,
            manual?.Decision,
            manual?.DecidedById,
            manual?.DecidedAt,
            manual?.Note,
            classified.OverruledReason);
    }
}
