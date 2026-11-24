using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Infrastructure.Database;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Raffles;

/// <summary>
/// Verifica que un premio soporte patrocinador como empresa del CRM (<see cref="Prize.SponsorCompanyId"/>)
/// o como nombre libre de texto (<see cref="Prize.SponsorName"/>) para sorteos corporativos.
/// </summary>
public sealed class PrizeSponsorNameAbsenceTests
{
    [Fact]
    public void GetProperties_ExposesSponsorNameAlongsideSponsorCompanyId_OnPrizeAndPersistence()
    {
        var prizeData = typeof(RaffleRepository).Assembly.GetType("Sorteo360.Events.Infrastructure.Database.PrizeData")!;

        var names = typeof(Prize).GetProperties().Concat(prizeData.GetProperties()).Select(p => p.Name).ToList();

        names.ShouldContain(nameof(Prize.SponsorCompanyId));
        names.ShouldContain(nameof(Prize.SponsorName));
    }
}
