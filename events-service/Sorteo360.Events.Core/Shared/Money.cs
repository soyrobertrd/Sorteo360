namespace Sorteo360.Events.Core.Shared;

public sealed record Money
{
    public decimal Amount { get; init; }
    public Currency Currency { get; init; }

    public Money(decimal amount, Currency currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>
    /// El mayor de un conjunto de montos, o <c>null</c> si el conjunto está vacío o mezcla
    /// monedas. No hay tipo de cambio en la plataforma, así que USD y EUR no se comparan:
    /// devolver el mayor de una de las dos daría un techo por debajo del real.
    /// </summary>
    public static Money? Highest(IEnumerable<Money> amounts)
    {
        ArgumentNullException.ThrowIfNull(amounts);

        Money? highest = null;

        foreach (var amount in amounts)
        {
            if (highest is null)
            {
                highest = amount;
                continue;
            }

            if (amount.Currency != highest.Currency)
                return null;

            if (amount.Amount > highest.Amount)
                highest = amount;
        }

        return highest;
    }
}
