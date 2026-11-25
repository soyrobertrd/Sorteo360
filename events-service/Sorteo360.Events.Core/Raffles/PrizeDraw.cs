namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Un sorteo de un premio: quién salió, de un bombo de cuántos, cuándo y quién apretó el botón. Lleva
/// el nombre, la empresa y el correo del ganador copiados de la fila congelada —la excepción acotada
/// de ADR-0022 a ADR-0013—, así que el acta se lee sin un join.
/// <para>
/// Una anulación no borra la fila: le pone el motivo, la nota, cuándo y quién (spec 02 §3). Viva es
/// la que no tiene anulación, y un premio tiene a lo sumo una viva.
/// </para>
/// <para>
/// <see cref="OnePrizePerPerson"/> y <see cref="OnePrizePerCompany"/> son las reglas bajo las que se
/// sorteó: las del sorteo al momento del bombo. Pueden cambiar en curso, así que el acta las lee de
/// cada sorteo y no del sorteo entero (spec 02 §1.2).
/// </para>
/// </summary>
public sealed record PrizeDraw(
    Guid Id,
    Guid PrizeId,
    Guid WinnerAttendeeId,
    string WinnerName,
    string? WinnerCompany,
    string WinnerIdentityKey,
    DateTimeOffset DrawnAt,
    Guid DrawnById,
    int EligiblePoolSize,
    bool OnePrizePerPerson,
    bool OnePrizePerCompany,
    string RedemptionCode,
    string? VoidCode,
    string? VoidNote,
    DateTimeOffset? VoidedAt,
    Guid? VoidedById)
{
    /// <summary>
    /// Sus entregas, vivas y revertidas, de la más nueva a la más vieja (spec 03 §3.4). Las carga el
    /// repositorio junto con el sorteo; un sorteo recién escrito no tiene ninguna.
    /// </summary>
    public IReadOnlyList<PrizeHandover> Handovers { get; init; } = [];

    /// <summary>
    /// El lote de notification-service que llevó el correo al ganador, o <c>null</c> si no hubo envío
    /// (spec 03 §5.2).
    /// </summary>
    public string? WinnerNotificationId { get; init; }

    /// <summary>
    /// Cómo terminó el correo al ganador: <c>sent</c>, <c>no_recipient</c> o <c>failed</c>. Nulo
    /// mientras no se intentó, que es lo que dice para siempre un sorteo anulado o uno de un sorteo
    /// que no se completó.
    /// </summary>
    public string? WinnerNotificationStatus { get; init; }

    /// <summary>Si sigue en pie: nadie la anuló.</summary>
    public bool IsLive => VoidedAt is null;

    /// <summary>Número de boleto del participante ganador (A33).</summary>
    public string? WinnerTicketNumber { get; init; }

    /// <summary>Código de empleado del participante ganador (A33).</summary>
    public string? WinnerEmployeeCode { get; init; }

    /// <summary>Departamento del participante ganador (A33).</summary>
    public string? WinnerDepartment { get; init; }

    /// <summary>Ubicación del participante ganador (§10).</summary>
    public string? WinnerLocation { get; init; }

    /// <summary>Nombre y apellido por separado, si el padrón los trae (§10).</summary>
    public string? WinnerFirstName { get; init; }

    public string? WinnerLastName { get; init; }

    /// <summary>La entrega que nadie revirtió, si la hay.</summary>
    public PrizeHandover? LiveHandover => Handovers.FirstOrDefault(h => h.IsLive);
}
