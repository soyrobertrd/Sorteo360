using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sorteo360.Platform.Http;
using Shouldly;

namespace Sorteo360.Platform.Tests.Unit.Http;

/// <summary>Lo que registra <c>AddProxyHeaders</c> para <c>UseForwardedHeaders</c>.</summary>
/// <remarks>
/// Se mira cuántos saltos de proxy se recorren en <c>X-Forwarded-For</c>, que es lo único que
/// cambia entre servicios, y de quién se acepta la cabecera, que es lo mismo para todos. Una
/// red pública en esa lista dejaría que cualquiera en internet se inventara su IP.
/// </remarks>
public sealed class ProxyHeadersConfigurationTests
{
    /// <summary>Las opciones que quedan registradas tras llamar a <c>AddProxyHeaders</c>.</summary>
    private static ForwardedHeadersOptions OptionsFor(int proxyHopCount)
    {
        var services = new ServiceCollection();
        services.AddProxyHeaders(proxyHopCount);

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void AddProxyHeaders_SetsTheForwardLimit_WhenGivenAHopCount(int proxyHopCount)
    {
        var options = OptionsFor(proxyHopCount);

        options.ForwardLimit.ShouldBe(proxyHopCount);
    }

    [Fact]
    public void AddProxyHeaders_ReadsTheCallerIpAndScheme_WhenAProxyForwardsThem()
    {
        var options = OptionsFor(1);

        options.ForwardedHeaders.ShouldBe(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
    }

    [Fact]
    public void AddProxyHeaders_TrustsOnlyPrivateNetworks_WhenAnyHopCountIsGiven()
    {
        var options = OptionsFor(1);

        options.KnownProxies.ShouldBeEmpty();
        options.KnownIPNetworks
            .Select(network => $"{network.BaseAddress}/{network.PrefixLength}")
            .ShouldBe(
                ["10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "127.0.0.0/8", "::1/128"],
                ignoreOrder: true);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddProxyHeaders_Throws_WhenTheHopCountIsNotPositive(int proxyHopCount)
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentOutOfRangeException>(() => services.AddProxyHeaders(proxyHopCount));
    }
}
