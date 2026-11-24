using Sorteo360.Events.Core.Branding;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Raffles;

public sealed class BrandingSettingsTests
{
    [Fact]
    public void Defaults_ReturnsSorteo360PaletteAndBrandIdentity()
    {
        var now = new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

        var defaults = GlobalBrandingSettings.Defaults(now);

        defaults.AppName.ShouldBe("Sorteo360");
        defaults.PrimaryColor.ShouldBe("#0d3b7d");
        defaults.AccentColor.ShouldBe("#1ea2dc");
        defaults.UpdatedAt.ShouldBe(now);
    }

    [Theory]
    [InlineData("#0D3B7D", "#111111", "#0d3b7d")]
    [InlineData("invalid", "#1ea2dc", "#1ea2dc")]
    [InlineData("#fff", "#0d3b7d", "#0d3b7d")]
    [InlineData(null, "#0d3b7d", "#0d3b7d")]
    public void NormalizeHexColor_ReturnsLowercaseHexOrFallback(string? input, string fallback, string expected)
    {
        GlobalBrandingSettings.NormalizeHexColor(input, fallback).ShouldBe(expected);
    }
}

