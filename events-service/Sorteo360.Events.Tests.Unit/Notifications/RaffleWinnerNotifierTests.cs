using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sorteo360.Events.Core.Branding;
using Sorteo360.Events.Core.Database;
using Sorteo360.Platform.Errors;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Core.Templates;
using Sorteo360.Events.Infrastructure.Notifications;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Notifications;

/// <summary>
/// El correo al ganador visto desde arriba: qué idioma, qué lote y qué contesta cuando algo se cae
/// (spec 03 §5). El transporte real se ejercita en los tests de integración.
/// </summary>
public sealed class RaffleWinnerNotifierTests
{
    private static readonly Guid EventId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid RaffleId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid DrawId = new("33333333-3333-3333-3333-333333333333");
    private static readonly Guid BatchId = new("44444444-4444-4444-4444-444444444444");

    private readonly INotificationTemplateRepository _templates = Substitute.For<INotificationTemplateRepository>();
    private readonly INotificationClient _client = Substitute.For<INotificationClient>();
    private readonly RaffleWinnerNotifier _sut;

    public RaffleWinnerNotifierTests()
    {
        _sut = new RaffleWinnerNotifier(
            _templates,
            _client,
            Options.Create(new NotificationSettings { FromAddress = "no-reply@sorteo360.local" }),
            NullLogger<RaffleWinnerNotifier>.Instance);

        SetupTemplate(AllowedLanguage.Espanol);
        SetupTemplate(AllowedLanguage.Ingles);
        _client.SendAsync(default!, default).ReturnsForAnyArgs(new SendNotificationResponse(BatchId));
    }

    [Fact]
    public async Task NotifyWonAsync_ReturnsSentWithTheBatchId_WhenTheNeighbourAccepts()
    {
        var outcome = await NotifyAsync(Event(AllowedLanguage.Espanol));

        outcome.ShouldBe(QuoteNotificationOutcome.Sent(BatchId.ToString()));
    }

    [Fact]
    public async Task NotifyWonAsync_SendsTheEnglishTemplate_WhenTheEventIsInEnglish()
    {
        await NotifyAsync(Event(AllowedLanguage.Ingles));

        var request = SentRequest();
        request.Language.ShouldBe(AllowedLanguage.Ingles);
        request.Subject.ShouldBe($"subject-{AllowedLanguage.Ingles}");
        request.Body.ShouldBe($"body-{AllowedLanguage.Ingles}");
    }

    [Fact]
    public async Task NotifyWonAsync_SendsTheSpanishTemplate_WhenTheEventLanguageHasNoTemplate()
    {
        // El respaldo no es un rechazo: mandar en español es mejor que no mandar (spec 03 §5.4).
        await NotifyAsync(Event(AllowedLanguage.Frances));

        var request = SentRequest();
        request.Language.ShouldBe(AllowedLanguage.Espanol);
        request.Body.ShouldBe($"body-{AllowedLanguage.Espanol}");
        await _templates.Received(1).FindAsync(RaffleNotificationType.PrizeWon, AllowedLanguage.Frances, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyWonAsync_ReturnsFailed_WhenNoTemplateExistsAtAll()
    {
        _templates.FindAsync(default!, default!, default).ReturnsForAnyArgs((NotificationTemplate?)null);

        var outcome = await NotifyAsync(Event(AllowedLanguage.Frances));

        outcome.ShouldBe(QuoteNotificationOutcome.Failed());
        await _client.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
    }

    [Fact]
    public async Task NotifyWonAsync_ReturnsNoRecipient_WhenTheDrawHasNoEmail()
    {
        var outcome = await NotifyAsync(Event(AllowedLanguage.Espanol), Draw() with { WinnerIdentityKey = " " });

        outcome.ShouldBe(QuoteNotificationOutcome.NoRecipient());
        await _client.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
    }

    [Fact]
    public async Task NotifyWonAsync_ReturnsFailed_WhenTheClientThrows()
    {
        _client.SendAsync(default!, default).ThrowsAsyncForAnyArgs(new HttpRequestException("caído"));

        var outcome = await NotifyAsync(Event(AllowedLanguage.Espanol));

        outcome.ShouldBe(QuoteNotificationOutcome.Failed());
    }

    [Fact]
    public async Task NotifyWonAsync_ReturnsFailed_WhenTheRequestIsCancelled()
    {
        // Un timeout de HttpClient llega como cancelación; a esta altura el sorteo ya se completó.
        _client.SendAsync(default!, default).ThrowsAsyncForAnyArgs(new TaskCanceledException());

        var outcome = await NotifyAsync(Event(AllowedLanguage.Espanol));

        outcome.ShouldBe(QuoteNotificationOutcome.Failed());
    }

    [Fact]
    public async Task NotifyWonAsync_ReturnsFailed_WhenTheNeighbourReturnsNoBatch()
    {
        _client.SendAsync(default!, default).ReturnsForAnyArgs((SendNotificationResponse?)null);

        var outcome = await NotifyAsync(Event(AllowedLanguage.Espanol));

        outcome.ShouldBe(QuoteNotificationOutcome.Failed());
    }

    [Fact]
    public async Task NotifyWonAsync_SendsOneBatchToTheSnapshotWithTheDrawKeyAndAbout_WhenCalled()
    {
        await NotifyAsync(Event(AllowedLanguage.Espanol));

        var request = SentRequest();
        request.Type.ShouldBe("events.prize.won");
        request.IdempotencyKey.ShouldBe($"events:raffle:{RaffleId}:draw:{DrawId}:won");
        request.FromAddress.ShouldBe("no-reply@sorteo360.local");
        request.Recipients.ShouldHaveSingleItem();
        request.Recipients[0].Email.ShouldBe("maria@example.com");
        request.Recipients[0].Name.ShouldBe("María Rodríguez");
        request.About.Select(a => (a.SourceService, a.EntityType, a.EntityId)).ShouldBe(
            [("events", "event", EventId.ToString()), ("events", "raffle", RaffleId.ToString())]);
    }

    [Fact]
    public async Task NotifyWonAsync_SendsTheSixValues_WhenCalled()
    {
        await NotifyAsync(Event(AllowedLanguage.Espanol));

        SentRequest().SharedData.ShouldBe(new Dictionary<string, string>
        {
            ["nombre"] = "María Rodríguez",
            ["premio"] = "Estadía en Punta Cana",
            ["evento"] = "Feria de Turismo",
            ["fechas"] = "jueves, 12 de marzo de 2026 – sábado, 14 de marzo de 2026",
            ["codigo"] = "RD-7K3M-9QXP",
            ["organizador"] = "Sorteo360",
            ["instrucciones"] = "Stand de MITUR\nhasta el viernes",
        }, ignoreOrder: true);
    }

    [Fact]
    public async Task NotifyWonAsync_UsesEventBrandingOrganizer_WhenBrandingRepositoryReturnsCompanyName()
    {
        var branding = Substitute.For<IBrandingRepository>();
        branding.GetEventBrandingAsync(EventId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<EventBrandingSettings?, BaseError>(
                new EventBrandingSettings(EventId, "Grupo Banreservas", null, DateTimeOffset.UtcNow)));

        var notifier = new RaffleWinnerNotifier(
            _templates,
            _client,
            Options.Create(new NotificationSettings { FromAddress = "no-reply@sorteo360.local" }),
            NullLogger<RaffleWinnerNotifier>.Instance,
            branding);

        await notifier.NotifyWonAsync(Event(AllowedLanguage.Espanol), Raffle(), Prize(), Draw(), CancellationToken.None);

        SentRequest().SharedData["organizador"].ShouldBe("Grupo Banreservas");
    }

    [Fact]
    public async Task NotifyWonAsync_CapsTheRecipientName_WhenItIsLongerThanTheNeighbourAccepts()
    {
        await NotifyAsync(Event(AllowedLanguage.Espanol), Draw() with { WinnerName = new string('a', 250) });

        SentRequest().Recipients[0].Name!.Length.ShouldBe(200);
    }

    [Theory]
    [InlineData(AllowedLanguage.Espanol, "jueves, 12 de marzo de 2026")]
    [InlineData(AllowedLanguage.Ingles, "Thursday, March 12, 2026")]
    public void DatesOf_ReturnsOneDate_WhenTheEventLastsOneDay(string language, string expected)
    {
        var @event = Event(language) with { EndDate = new DateOnly(2026, 3, 12) };

        RaffleWinnerNotifier.DatesOf(@event, language).ShouldBe(expected);
    }

    [Theory]
    [InlineData(AllowedLanguage.Espanol, "jueves, 12 de marzo de 2026 – sábado, 14 de marzo de 2026")]
    [InlineData(AllowedLanguage.Ingles, "Thursday, March 12, 2026 – Saturday, March 14, 2026")]
    public void DatesOf_ReturnsARange_WhenTheEventLastsSeveralDays(string language, string expected)
    {
        RaffleWinnerNotifier.DatesOf(Event(language), language).ShouldBe(expected);
    }

    [Fact]
    public async Task NotifyWonAsync_FormatsTheDatesInTheTemplateLanguage_WhenFallingBackToSpanish()
    {
        await NotifyAsync(Event(AllowedLanguage.Frances));

        SentRequest().SharedData["fechas"].ShouldBe("jueves, 12 de marzo de 2026 – sábado, 14 de marzo de 2026");
    }

    private Task<QuoteNotificationOutcome> NotifyAsync(Event @event, PrizeDraw? draw = null) =>
        _sut.NotifyWonAsync(@event, Raffle(), Prize(), draw ?? Draw(), CancellationToken.None);

    private SendNotificationRequest SentRequest() =>
        (SendNotificationRequest)_client.ReceivedCalls().Single().GetArguments()[0]!;

    private void SetupTemplate(string language) =>
        _templates.FindAsync(RaffleNotificationType.PrizeWon, language, Arg.Any<CancellationToken>())
            .Returns(new NotificationTemplate(RaffleNotificationType.PrizeWon, language, $"subject-{language}", $"body-{language}"));

    private static Event Event(string language)
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();

        return new Event(
            id: EventId,
            type: EventType.Feria,
            name: "Feria de Turismo",
            startDate: new DateOnly(2026, 3, 12),
            endDate: new DateOnly(2026, 3, 14),
            status: EventStatus.InProgress,
            language: language,
            visibility: EventVisibility.Private,
            seatingLayout: null,
            externalUrl: null,
            description: null,
            invitedCount: null,
            estimatedBudget: null,
            totalCost: null,
            cancellationReason: null,
            createdAt: now,
            updatedAt: now,
            createdById: userId,
            updatedById: userId,
            deletedAt: null);
    }

    private static Raffle Raffle() =>
        Core.Raffles.Raffle.CreateDraft(EventId, Guid.NewGuid(), DateTimeOffset.UtcNow) with
        {
            Id = RaffleId,
            State = RaffleState.Completed,
            PickupInstructions = "Stand de MITUR\nhasta el viernes",
        };

    private static Prize Prize()
    {
        var now = DateTimeOffset.UtcNow;
        return new Prize(Guid.NewGuid(), Guid.NewGuid(), "Estadía en Punta Cana", null, "US$ 900", now, now, Guid.NewGuid(), Guid.NewGuid());
    }

    private static PrizeDraw Draw() => new(
        Id: DrawId,
        PrizeId: Guid.NewGuid(),
        WinnerAttendeeId: Guid.NewGuid(),
        WinnerName: "María Rodríguez",
        WinnerCompany: "Viajes Caribe",
        WinnerIdentityKey: "maria@example.com",
        DrawnAt: DateTimeOffset.UtcNow,
        DrawnById: Guid.NewGuid(),
        EligiblePoolSize: 12,
        OnePrizePerPerson: true,
        OnePrizePerCompany: false,
        RedemptionCode: "RD-7K3M-9QXP",
        VoidCode: null,
        VoidNote: null,
        VoidedAt: null,
        VoidedById: null);
}
