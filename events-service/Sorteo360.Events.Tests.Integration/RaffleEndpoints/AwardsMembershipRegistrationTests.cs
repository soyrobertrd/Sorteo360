using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Infrastructure.Raffles;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

/// <summary>
/// Lo que el host resuelve como lista de DO Travel Awards hasta PGTI-115: el marcador que contesta
/// "sin lista", y no una lista vacía que dejaría a todos fuera del bombo.
/// </summary>
public sealed class AwardsMembershipRegistrationTests : IClassFixture<EventsApiFactory>
{
    private readonly EventsApiFactory _factory;

    public AwardsMembershipRegistrationTests(EventsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetInForceAsync_ReturnsNone_WhenResolvedFromTheHost()
    {
        using var scope = _factory.Services.CreateScope();
        var membership = scope.ServiceProvider.GetRequiredService<IAwardsMembership>();

        var list = await membership.GetInForceAsync(CancellationToken.None);

        membership.ShouldBeOfType<NoAwardsList>();
        list.HasNoValue.ShouldBeTrue();
    }
}
