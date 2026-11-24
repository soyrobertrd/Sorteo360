using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Infrastructure.Database;
using Sorteo360.Events.Web.RaffleEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Raffles;

/// <summary>
/// La cantidad existe solo al crear premios: dice cuántas filas idénticas hacer, y después cada
/// premio es uno. Un <c>Quantity</c> en un tipo de edición invitaría a "editar la cantidad", que no
/// significa nada, así que se afirma como ausencia sobre todo tipo de Core, Infrastructure y Web.
/// </summary>
public sealed class PrizeQuantityAbsenceTests
{
    [Fact]
    public void GetTypes_ReturnsNoUpdateTypeWithQuantity_WhenScanningTheService()
    {
        var offenders = new[] { typeof(Prize).Assembly, typeof(RaffleRepository).Assembly, typeof(UpdatePrizeRequest).Assembly }
            .SelectMany(a => a.GetTypes())
            .Where(t => t.Name.Contains("Update", StringComparison.Ordinal))
            .Where(t => t.GetProperty("Quantity") is not null)
            .Select(t => t.FullName)
            .ToList();

        offenders.ShouldBeEmpty();
    }

    [Fact]
    public void GetProperty_ReturnsNull_WhenAskingThePrizeForQuantity()
    {
        typeof(Prize).GetProperty("Quantity").ShouldBeNull();
    }
}
