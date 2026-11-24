namespace Sorteo360.Events.Infrastructure.Database;

/// <summary>
/// Contador mensual de sorteos creados por cuenta (§9.4): no se recalcula sobre sorteos vivos,
/// porque un sorteo borrado sigue contando en el mes calendario.
/// </summary>
internal sealed class AccountMonthlyUsageData
{
    public Guid AccountId { get; set; }
    public string YearMonth { get; set; } = string.Empty;
    public int RafflesCreated { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

