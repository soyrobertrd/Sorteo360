using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Notifications;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Notifications;

/// <summary>
/// La clave de idempotencia nombra el envío, no la cosa sobre la que fue (spec <c>01</c> §3): el
/// mismo envío repetido lleva la misma clave, y un envío distinto una distinta.
/// </summary>
public sealed class NotificationIdempotencyKeyTests
{
    private static readonly Guid CampaignId = Guid.NewGuid();
    private static readonly Guid EventId = Guid.NewGuid();
    private static readonly Guid AnaId = Guid.NewGuid();
    private static readonly Guid LuisId = Guid.NewGuid();

    #region ForCampaignBatch

    [Fact]
    public void ForCampaignBatch_ReturnsTheSameKey_WhenTheSameBatchIsBuiltTwice()
    {
        var first = NotificationIdempotencyKey.ForCampaignBatch(Batch(("ana", "t-1"), ("luis", "t-2")));
        var second = NotificationIdempotencyKey.ForCampaignBatch(Batch(("ana", "t-1"), ("luis", "t-2")));

        second.ShouldBe(first);
    }

    [Fact]
    public void ForCampaignBatch_ReturnsADifferentKey_WhenTheTokensWereMintedAgain()
    {
        var first = NotificationIdempotencyKey.ForCampaignBatch(Batch(("ana", "t-1"), ("luis", "t-2")));
        var resumed = NotificationIdempotencyKey.ForCampaignBatch(Batch(("ana", "t-3"), ("luis", "t-4")));

        resumed.ShouldNotBe(first);
    }

    [Fact]
    public void ForCampaignBatch_ReturnsADifferentKey_WhenTheRecipientsDiffer()
    {
        var both = NotificationIdempotencyKey.ForCampaignBatch(Batch(("ana", "t-1"), ("luis", "t-2")));
        var one = NotificationIdempotencyKey.ForCampaignBatch(Batch(("ana", "t-1")));

        one.ShouldNotBe(both);
    }

    [Fact]
    public void ForCampaignBatch_ReturnsAKeyNamingTheCampaign_WhenItBuildsTheKey()
    {
        var key = NotificationIdempotencyKey.ForCampaignBatch(Batch(("ana", "t-1")));

        key.ShouldStartWith($"convocatorias:campaign:{CampaignId}:batch:");
    }

    #endregion

    #region ForRegistrationConfirmation

    [Fact]
    public void ForRegistrationConfirmation_ReturnsTheSameKey_WhenTheSameRequestIsKeyedTwice()
    {
        var registrationId = Guid.NewGuid();
        var requestedAt = new DateTimeOffset(2026, 9, 23, 14, 0, 0, TimeSpan.Zero);

        NotificationIdempotencyKey.ForRegistrationConfirmation(registrationId, requestedAt)
            .ShouldBe(NotificationIdempotencyKey.ForRegistrationConfirmation(registrationId, requestedAt));
    }

    [Fact]
    public void ForRegistrationConfirmation_ReturnsADifferentKey_WhenThePersonAsksForItAgain()
    {
        var registrationId = Guid.NewGuid();
        var first = new DateTimeOffset(2026, 9, 23, 14, 0, 0, TimeSpan.Zero);

        NotificationIdempotencyKey.ForRegistrationConfirmation(registrationId, first.AddMinutes(5))
            .ShouldNotBe(NotificationIdempotencyKey.ForRegistrationConfirmation(registrationId, first));
    }

    [Fact]
    public void ForRegistrationConfirmation_ReturnsAKeyNamingTheRegistration_WhenItBuildsTheKey()
    {
        var registrationId = Guid.NewGuid();

        NotificationIdempotencyKey.ForRegistrationConfirmation(registrationId, DateTimeOffset.UnixEpoch)
            .ShouldStartWith($"convocatorias:registration:{registrationId}:confirmation:");
    }

    #endregion

    #region ForCancellationNotice

    [Fact]
    public void ForCancellationNotice_ReturnsTheSameKey_WhenTheSameNoticeIsKeyedTwice()
    {
        var registrationId = Guid.NewGuid();

        NotificationIdempotencyKey.ForCancellationNotice(EventId, registrationId)
            .ShouldBe(NotificationIdempotencyKey.ForCancellationNotice(EventId, registrationId));
    }

    [Fact]
    public void ForCancellationNotice_ReturnsADifferentKey_WhenTheRegistrationDiffers()
    {
        NotificationIdempotencyKey.ForCancellationNotice(EventId, Guid.NewGuid())
            .ShouldNotBe(NotificationIdempotencyKey.ForCancellationNotice(EventId, Guid.NewGuid()));
    }

    [Fact]
    public void ForCancellationNotice_ReturnsAKeyNamingTheEventAndTheRegistration_WhenItBuildsTheKey()
    {
        var registrationId = Guid.NewGuid();

        NotificationIdempotencyKey.ForCancellationNotice(EventId, registrationId)
            .ShouldBe($"convocatorias:event:{EventId}:cancelled:registration:{registrationId}");
    }

    #endregion

    /// <summary>Un lote con un token por persona; Ana y Luis conservan su id entre lotes.</summary>
    private static SendBatchRequest Batch(params (string Name, string Token)[] recipients) =>
        new(
            CampaignId,
            EventId,
            "es",
            "Te invitamos",
            "<p>Hola {{nombre}}</p>",
            new Dictionary<string, string> { ["evento"] = "Roadshow Madrid 2026" },
            [.. recipients.Select(recipient => new SendBatchRecipient(
                recipient.Name == "ana" ? AnaId : LuisId,
                $"{recipient.Name}@example.com",
                null,
                new Dictionary<string, string>
                {
                    ["nombre"] = recipient.Name,
                    ["enlace_registro"] = $"https://app.pgti360.test/publico/registro#t={recipient.Token}",
                }))]);
}
