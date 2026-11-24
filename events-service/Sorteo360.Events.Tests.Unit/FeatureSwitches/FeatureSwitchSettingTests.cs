using Sorteo360.Events.Core.FeatureSwitches;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.FeatureSwitches;

public sealed class FeatureSwitchSettingTests
{
    [Theory]
    [InlineData("sorteo")]
    [InlineData("sorteo.raffles")]
    [InlineData("sorteo.winners-history")]
    [InlineData("events")]
    [InlineData("convocatorias.check-in")]
    public void IsValidKey_ReturnsTrue_ForSupportedModuleAndFeatureKeys(string key)
    {
        FeatureSwitchSetting.IsValidKey(key).ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Sorteo")]
    [InlineData("sorteo.raffles.extra")]
    [InlineData("1sorteo")]
    [InlineData("sorteo/raffles")]
    public void IsValidKey_ReturnsFalse_ForInvalidKeys(string key)
    {
        FeatureSwitchSetting.IsValidKey(key).ShouldBeFalse();
    }
}

