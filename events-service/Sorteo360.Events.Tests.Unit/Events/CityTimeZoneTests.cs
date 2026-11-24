using Sorteo360.Events.Core.Events;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Events;

/// <summary>La zona horaria de una ciudad acepta solo un id IANA que el sistema resuelva.</summary>
/// <remarks>
/// Un id de Windows también resuelve en .NET, pero no es el formato que el contrato promete, y
/// aceptarlo dejaría en la base dos maneras de escribir la misma zona.
/// </remarks>
public sealed class CityTimeZoneTests
{
    [Theory]
    [InlineData("America/Chicago")]
    [InlineData("America/Santo_Domingo")]
    [InlineData("Europe/Madrid")]
    public void Create_AcceptsTheId_WhenItIsAnIanaZone(string id)
    {
        var result = CityTimeZone.Create(id);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(id);
    }

    [Theory]
    [InlineData("Eastern Standard Time")]
    [InlineData("Mars/Olympus")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("../../../etc/passwd")]
    [InlineData("/etc/passwd")]
    public void Create_RefusesTheId_WhenItIsNotAKnownIanaZone(string? id)
    {
        var result = CityTimeZone.Create(id);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("TIME_ZONE_UNKNOWN");
    }

    [Fact]
    public void CreateOrDefault_ReturnsSantoDomingo_WhenNoIdIsGiven()
    {
        CityTimeZone.CreateOrDefault(null).Value.ShouldBe(CityTimeZone.Default);
    }

    [Fact]
    public void CreateOrDefault_RefusesTheId_WhenItIsEmpty()
    {
        // Una zona presente pero vacía no es una omisión: no se completa con la de Santo Domingo.
        CityTimeZone.CreateOrDefault("").Error.Code.ShouldBe("TIME_ZONE_UNKNOWN");
    }

    [Fact]
    public void CreateOrDefault_AcceptsTheId_WhenItIsAnIanaZone()
    {
        CityTimeZone.CreateOrDefault("America/Chicago").Value.Id.ShouldBe("America/Chicago");
    }

    [Fact]
    public void Default_IsSantoDomingo()
    {
        // Es la hora que imprimió toda acta emitida antes de que la ciudad tuviera zona.
        CityTimeZone.Default.Id.ShouldBe("America/Santo_Domingo");
    }

    [Fact]
    public void Info_StatesTheOffsetOfTheZone()
    {
        var chicago = CityTimeZone.Create("America/Chicago").Value;

        chicago.Info.GetUtcOffset(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero))
            .ShouldBe(TimeSpan.FromHours(-6));
    }
}
