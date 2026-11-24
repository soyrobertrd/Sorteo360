using Sorteo360.Events.Core.Raffles;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Raffles;

/// <summary>Cuándo dos empresas escritas a mano son la misma (spec 02 §1.1).</summary>
public sealed class CompanyKeyTests
{
    [Theory]
    [InlineData("Turismo  Pérez", "turismo perez")]
    [InlineData("  Hotel\tSol ", "HOTEL SOL")]
    [InlineData("Viajes Caribeños", "viajes caribenos")]
    public void Normalize_ReturnsTheSameKey_WhenTheyDifferOnlyInCaseAccentsOrSpaces(string left, string right)
    {
        CompanyKey.Normalize(left).Value.ShouldBe(CompanyKey.Normalize(right).Value);
    }

    [Fact]
    public void Normalize_ReturnsDifferentKeys_WhenOneHasASuffix()
    {
        CompanyKey.Normalize("MITUR").Value.ShouldNotBe(CompanyKey.Normalize("Mitur SRL").Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public void Normalize_ReturnsNone_WhenTheCompanyIsBlank(string? blank)
    {
        CompanyKey.Normalize(blank).HasNoValue.ShouldBeTrue();
    }
}
