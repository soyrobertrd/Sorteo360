using Sorteo360.Events.Core.Shared;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Shared;

/// <summary>
/// El techo de un conjunto de montos. La regla que importa es la que no devuelve número:
/// sin tipo de cambio, dos monedas distintas no se comparan, y un techo que ignorara una
/// de las dos diría menos de lo que cuesta.
/// </summary>
public sealed class MoneyTests
{
    [Fact]
    public void Highest_ReturnsTheLargestAmount_WhenEveryAmountIsUsd()
    {
        var highest = Money.Highest([Usd(5000), Usd(7200), Usd(4200)]);

        highest.ShouldNotBeNull();
        highest!.Amount.ShouldBe(7200m);
        highest.Currency.ShouldBe(Currency.USD);
    }

    [Fact]
    public void Highest_ReturnsTheOnlyAmount_WhenThereIsOne()
    {
        Money.Highest([Usd(5000)])!.Amount.ShouldBe(5000m);
    }

    [Fact]
    public void Highest_ReturnsNull_WhenThereAreNoAmounts()
    {
        // Una cotización sin candidatos no tiene techo, y cero es un precio.
        Money.Highest([]).ShouldBeNull();
    }

    [Fact]
    public void Highest_ReturnsNull_WhenTheCurrenciesMix()
    {
        Money.Highest([Usd(5000), new Money(4200m, Currency.EUR)]).ShouldBeNull();
    }

    [Fact]
    public void Highest_ReturnsTheLargestEurAmount_WhenEveryAmountIsEur()
    {
        var highest = Money.Highest([new Money(5000m, Currency.EUR), new Money(6100m, Currency.EUR)]);

        highest!.Amount.ShouldBe(6100m);
        highest.Currency.ShouldBe(Currency.EUR);
    }

    [Fact]
    public void Highest_Throws_WhenTheSequenceIsMissing()
    {
        Should.Throw<ArgumentNullException>(() => Money.Highest(null!));
    }

    private static Money Usd(decimal amount) => new(amount, Currency.USD);
}
