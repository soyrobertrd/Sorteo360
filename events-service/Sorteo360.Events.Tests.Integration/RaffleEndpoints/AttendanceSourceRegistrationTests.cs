using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Infrastructure.Raffles;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

public sealed class AttendanceSourceRegistrationTests : IClassFixture<EventsApiFactory>
{
    private readonly EventsApiFactory _factory;

    public AttendanceSourceRegistrationTests(EventsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void AttendanceSource_ReturnsTheRosterSource_WhenResolvedFromTheHost()
    {
        using var scope = _factory.Services.CreateScope();

        var source = scope.ServiceProvider.GetRequiredService<IAttendanceSource>();

        source.ShouldBeOfType<RosterAttendanceSource>();
    }
}
