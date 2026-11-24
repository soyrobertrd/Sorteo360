using Sorteo360.Notifications.Core.History;
using Sorteo360.Notifications.Core.Notifications;
using Shouldly;

namespace Sorteo360.Notifications.Tests.Unit.History;

/// <summary>
/// La regla normativa del historial: sin webhook, el estado de una fila no pasa de <c>sent</c>.
/// <para>
/// Es lo que impide que una pantalla lea el <c>sent</c> permanente de stage como una cola
/// atascada, y lo que impide que un evento llegado por otra vía mueva una fila que nadie está
/// siguiendo.
/// </para>
/// </summary>
public sealed class DeliveryTrackingTests
{
    [Theory]
    [InlineData(DeliveryStatus.Delivered)]
    [InlineData(DeliveryStatus.Bounced)]
    [InlineData(DeliveryStatus.Deferred)]
    public void Reported_ReturnsSent_WhenAProviderStatusHasNoTracking(string stored)
    {
        DeliveryTracking.Reported(stored, trackingExpected: false).ShouldBe(DeliveryStatus.Sent);
    }

    [Theory]
    [InlineData(DeliveryStatus.Queued)]
    [InlineData(DeliveryStatus.Sent)]
    [InlineData(DeliveryStatus.Dropped)]
    [InlineData(DeliveryStatus.Failed)]
    public void Reported_ReturnsTheStoredStatus_WhenTheServiceWritesItItself(string stored)
    {
        DeliveryTracking.Reported(stored, trackingExpected: false).ShouldBe(stored);
    }

    [Theory]
    [InlineData(DeliveryStatus.Queued)]
    [InlineData(DeliveryStatus.Sent)]
    [InlineData(DeliveryStatus.Delivered)]
    [InlineData(DeliveryStatus.Bounced)]
    [InlineData(DeliveryStatus.Deferred)]
    [InlineData(DeliveryStatus.Dropped)]
    [InlineData(DeliveryStatus.Failed)]
    public void Reported_ReturnsEveryStatusAsStored_WhenTrackingIsExpected(string stored)
    {
        DeliveryTracking.Reported(stored, trackingExpected: true).ShouldBe(stored);
    }

    [Fact]
    public void ProviderDerived_ReturnsExactlyTheWebhookOnlyStatuses_WhenRead()
    {
        DeliveryTracking.ProviderDerived.ShouldBe(
            [DeliveryStatus.Delivered, DeliveryStatus.Bounced, DeliveryStatus.Deferred],
            ignoreOrder: true);
    }

    [Fact]
    public void ProviderDerived_ReturnsOnlyStatusesTheColumnAdmits_WhenRead()
    {
        DeliveryTracking.ProviderDerived.ShouldBeSubsetOf(DeliveryStatus.All);
    }
}
