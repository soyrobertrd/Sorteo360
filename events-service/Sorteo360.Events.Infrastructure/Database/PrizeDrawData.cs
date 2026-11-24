namespace Sorteo360.Events.Infrastructure.Database;

/// <summary>
/// Entidad de persistencia para la tabla <c>events.prize_draw</c> (un sorteo de un premio, vivo o
/// anulado, con la copia del ganador y su código de retiro).
/// </summary>
internal sealed class PrizeDrawData
{
    public Guid Id { get; set; }
    public Guid PrizeId { get; set; }
    public Guid WinnerAttendeeId { get; set; }
    public string WinnerName { get; set; } = string.Empty;
    public string? WinnerCompany { get; set; }
    public string WinnerEmail { get; set; } = string.Empty;
    public DateTimeOffset DrawnAt { get; set; }
    public Guid DrawnById { get; set; }
    public int EligiblePoolSize { get; set; }
    public bool OnePrizePerPerson { get; set; }
    public bool OnePrizePerCompany { get; set; }
    public string RedemptionCode { get; set; } = string.Empty;
    public string? VoidCode { get; set; }
    public string? VoidNote { get; set; }
    public DateTimeOffset? VoidedAt { get; set; }
    public Guid? VoidedById { get; set; }
    public string? WinnerNotificationId { get; set; }
    public string? WinnerNotificationStatus { get; set; }
}
