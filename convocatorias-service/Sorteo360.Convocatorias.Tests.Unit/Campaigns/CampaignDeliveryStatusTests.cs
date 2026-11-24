using Sorteo360.Convocatorias.Core.Campaigns;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Campaigns;

public sealed class CampaignDeliveryStatusTests
{
    private static readonly Guid First = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Second = Guid.Parse("00000000-0000-0000-0000-000000000002");

    [Theory]
    [InlineData("delivered", "none", CampaignDeliveryStatus.Delivered)]
    [InlineData("bounced", "none", CampaignDeliveryStatus.Bounced)]
    [InlineData("delivered", "complained", CampaignDeliveryStatus.SpamReport)]
    [InlineData("delivered", "unsubscribed", CampaignDeliveryStatus.Unsubscribed)]
    [InlineData("sent", "none", CampaignDeliveryStatus.Sent)]
    [InlineData("queued", "none", CampaignDeliveryStatus.Sent)]
    [InlineData("deferred", "none", CampaignDeliveryStatus.Sent)]
    [InlineData("dropped", "none", CampaignDeliveryStatus.Sent)]
    [InlineData("failed", "none", CampaignDeliveryStatus.Sent)]
    public void FromNotification_ReturnsTheShownStatus_WhenNotificationServiceReportsBothAxes(
        string deliveryStatus,
        string recipientAction,
        string expected) =>
        CampaignDeliveryStatus.FromNotification(deliveryStatus, recipientAction).ShouldBe(expected);

    [Theory]
    [InlineData(RecipientSendStatus.Pending)]
    [InlineData(RecipientSendStatus.Failed)]
    [InlineData(RecipientSendStatus.Suppressed)]
    public void Of_ReturnsNull_WhenTheRowWasNeverHandedOff(string sendStatus) =>
        CampaignDeliveryStatus.Of(sendStatus, [First], Known((First, CampaignDeliveryStatus.Delivered))).ShouldBeNull();

    [Fact]
    public void Of_ReturnsSent_WhenTheRowKeptNoMessageId() =>
        CampaignDeliveryStatus.Of(RecipientSendStatus.HandedOff, [], Known()).ShouldBe(CampaignDeliveryStatus.Sent);

    [Fact]
    public void Of_ReturnsSent_WhenNotificationServiceDoesNotKnowTheId() =>
        CampaignDeliveryStatus.Of(RecipientSendStatus.HandedOff, [First], Known()).ShouldBe(CampaignDeliveryStatus.Sent);

    [Fact]
    public void Of_ReturnsTheReaction_WhenOneMessageWasDeliveredAndAnotherReportedAsSpam() =>
        CampaignDeliveryStatus.Of(
                RecipientSendStatus.HandedOff,
                [First, Second],
                Known((First, CampaignDeliveryStatus.Delivered), (Second, CampaignDeliveryStatus.SpamReport)))
            .ShouldBe(CampaignDeliveryStatus.SpamReport);

    private static Dictionary<Guid, string> Known(params (Guid Id, string Status)[] entries) =>
        entries.ToDictionary(entry => entry.Id, entry => entry.Status);
}
