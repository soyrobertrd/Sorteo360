using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Events;
using Sorteo360.Convocatorias.Core.Notifications;
using Sorteo360.Convocatorias.Core.Registrations;
using Sorteo360.Convocatorias.Core.Rendering;
using Sorteo360.Convocatorias.Core.Templates;
using Sorteo360.Platform.Errors;
using NSubstitute;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Registrations;

/// <summary>
/// El aviso de cancelación (spec <c>06</c> §4.7): lee el evento, se niega si no está cancelado, y
/// le manda <c>event_cancelled</c> a cada inscripción que todavía no lo recibió, sellándola.
/// </summary>
public sealed class CancellationNoticeServiceTests
{
    private static readonly Guid EventId = Guid.NewGuid();

    private readonly IEventDirectory _events = Substitute.For<IEventDirectory>();
    private readonly ITemplateRepository _templates = Substitute.For<ITemplateRepository>();
    private readonly IRegistrationRepository _registrations = Substitute.For<IRegistrationRepository>();
    private readonly ITransactionalNotifier _notifier = Substitute.For<ITransactionalNotifier>();
    private readonly CancellationNoticeService _service;

    private readonly List<TransactionalNotification> _sent = [];

    public CancellationNoticeServiceTests()
    {
        _notifier
            .SendAsync(Arg.Do<TransactionalNotification>(_sent.Add), Arg.Any<CancellationToken>())
            .Returns(UnitResult.Success<BaseError>());

        _registrations
            .ClaimCancellationNoticeAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<bool, BaseError>(true));

        _registrations
            .ReleaseCancellationNoticeAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(UnitResult.Success<BaseError>());

        GivenTemplate("es");

        _service = new CancellationNoticeService(_events, _templates, _registrations, _notifier);
    }

    [Fact]
    public async Task NotifyAsync_ReturnsEveryRegistrationNotified_WhenTheEventIsCancelled()
    {
        GivenEvent(EventStatus.Cancelled);
        var ana = ARegistration("Ana", "ana@acme.com", "REF-2026-00001");
        var luis = ARegistration("Luis", "luis@acme.com", "REF-2026-00002");
        GivenAwaiting(ana, luis);

        var result = await _service.NotifyAsync(EventId, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Notified.ShouldBe(2);
        result.Value.Failed.ShouldBe(0);
        _sent.Select(n => n.RecipientEmail).ShouldBe(["ana@acme.com", "luis@acme.com"]);
        await _registrations.Received(1).ClaimCancellationNoticeAsync(ana.Id, Arg.Any<CancellationToken>());
        await _registrations.Received(1).ClaimCancellationNoticeAsync(luis.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyAsync_ReturnsOneNotified_WhenTheNoticeIsAddressedToTheRegistrationFullName()
    {
        GivenEvent(EventStatus.Cancelled);
        GivenAwaiting(ARegistration("Ana", "ana@acme.com", "REF-2026-00001"));

        var result = await _service.NotifyAsync(EventId, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Notified.ShouldBe(1);
        _sent.ShouldHaveSingleItem().RecipientName.ShouldBe("Ana Pérez");
    }

    [Fact]
    public async Task NotifyAsync_ReturnsSuccessAfterSendingOnlyTheNameAndReference_WhenItBuildsTheNotice()
    {
        GivenEvent(EventStatus.Cancelled);
        GivenAwaiting(ARegistration("Ana", "ana@acme.com", "REF-2026-00001"));

        await _service.NotifyAsync(EventId, CancellationToken.None);

        var notice = _sent.ShouldHaveSingleItem();
        notice.Type.ShouldBe(CancellationNoticeService.NotificationType);
        notice.EventId.ShouldBe(EventId);
        notice.Language.ShouldBe("es");
        notice.Subject.ShouldBe("{{evento}} ha sido cancelado");
        notice.RecipientData.Keys.ShouldBe(
            [SupportedPlaceholder.RecipientName, SupportedPlaceholder.Reference],
            ignoreOrder: true);
        notice.RecipientData[SupportedPlaceholder.Reference].ShouldBe("REF-2026-00001");
        notice.SharedData[SupportedPlaceholder.EventName].ShouldBe("Roadshow Madrid 2026");
    }

    /// <summary>
    /// Una clave por inscrito y no una por evento: con la del evento sola, el emisor se tragaría
    /// el aviso de todos menos el primero.
    /// </summary>
    [Fact]
    public async Task NotifyAsync_ReturnsEveryRegistrationNotifiedWithItsOwnKey_WhenTwoAwaitTheNotice()
    {
        GivenEvent(EventStatus.Cancelled);
        var ana = ARegistration("Ana", "ana@acme.com", "REF-2026-00001");
        var luis = ARegistration("Luis", "luis@acme.com", "REF-2026-00002");
        GivenAwaiting(ana, luis);

        await _service.NotifyAsync(EventId, CancellationToken.None);

        _sent.Select(notice => notice.IdempotencyKey).ShouldBe(
        [
            NotificationIdempotencyKey.ForCancellationNotice(EventId, ana.Id),
            NotificationIdempotencyKey.ForCancellationNotice(EventId, luis.Id),
        ]);
    }

    [Theory]
    [InlineData(EventStatus.InProgress)]
    [InlineData(EventStatus.Closed)]
    public async Task NotifyAsync_ReturnsEventNotCancelledAndMailsNobody_WhenTheEventIsNotCancelled(string status)
    {
        GivenEvent(status);
        GivenAwaiting(ARegistration("Ana", "ana@acme.com", "REF-2026-00001"));

        var result = await _service.NotifyAsync(EventId, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(EventNotCancelledError.ErrorCode);
        _sent.ShouldBeEmpty();
        await _registrations.DidNotReceive().ClaimCancellationNoticeAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyAsync_ReturnsEventNotCancelled_WhenEventsServiceDoesNotKnowTheEvent()
    {
        _events.GetEventAsync(EventId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<EventSummary>, BaseError>(Maybe<EventSummary>.None));

        var result = await _service.NotifyAsync(EventId, CancellationToken.None);

        result.Error.Code.ShouldBe(EventNotCancelledError.ErrorCode);
        _sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task NotifyAsync_ReturnsEventsUnavailableAndMailsNobody_WhenEventsServiceIsDown()
    {
        _events.GetEventAsync(EventId, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<Maybe<EventSummary>, BaseError>(new EventsUnavailableError()));

        var result = await _service.NotifyAsync(EventId, CancellationToken.None);

        result.Error.Code.ShouldBe(EventsUnavailableError.ErrorCode);
        _sent.ShouldBeEmpty();
        await _registrations.DidNotReceive()
            .ListAwaitingCancellationNoticeAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyAsync_ReturnsNoticeUnavailableAndClaimsNobody_WhenTheTemplateIsMissing()
    {
        GivenEvent(EventStatus.Cancelled, language: "fr");
        GivenAwaiting(ARegistration("Ana", "ana@acme.com", "REF-2026-00001"));
        _templates.GetAsync(TemplateKey.EventCancelled, "fr", Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<Template>, BaseError>(Maybe<Template>.None));

        var result = await _service.NotifyAsync(EventId, CancellationToken.None);

        result.Error.Code.ShouldBe(CancellationNoticeUnavailableError.ErrorCode);
        _sent.ShouldBeEmpty();
        await _registrations.DidNotReceive().ClaimCancellationNoticeAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Dos pulsaciones a la vez: la fila que la otra ya reclamó no se le manda dos veces.
    /// </summary>
    [Fact]
    public async Task NotifyAsync_ReturnsZeroNotified_WhenAnotherCallAlreadyClaimedTheRow()
    {
        GivenEvent(EventStatus.Cancelled);
        var ana = ARegistration("Ana", "ana@acme.com", "REF-2026-00001");
        GivenAwaiting(ana);
        _registrations.ClaimCancellationNoticeAsync(ana.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Success<bool, BaseError>(false));

        var result = await _service.NotifyAsync(EventId, CancellationToken.None);

        result.Value.Notified.ShouldBe(0);
        result.Value.Failed.ShouldBe(0);
        _sent.ShouldBeEmpty();
    }

    /// <summary>
    /// Un envío que no salió devuelve la fila a sin avisar, que es donde la ve el cubo de
    /// <c>07</c> §6.5 y donde la encuentra la próxima pulsación.
    /// </summary>
    [Fact]
    public async Task NotifyAsync_ReturnsAFailedCountAndReleasesTheClaim_WhenTheNotifierFails()
    {
        GivenEvent(EventStatus.Cancelled);
        var ana = ARegistration("Ana", "ana@acme.com", "REF-2026-00001");
        var luis = ARegistration("Luis", "luis@acme.com", "REF-2026-00002");
        GivenAwaiting(ana, luis);
        _notifier.SendAsync(Arg.Is<TransactionalNotification>(n => n.RecipientEmail == "ana@acme.com"), Arg.Any<CancellationToken>())
            .Returns(UnitResult.Failure<BaseError>(new NotificationsUnavailableError("no respondió")));

        var result = await _service.NotifyAsync(EventId, CancellationToken.None);

        result.Value.Notified.ShouldBe(1);
        result.Value.Failed.ShouldBe(1);
        await _registrations.Received(1).ReleaseCancellationNoticeAsync(ana.Id, Arg.Any<CancellationToken>());
        await _registrations.DidNotReceive().ReleaseCancellationNoticeAsync(luis.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyAsync_ReturnsTheExceptionAfterReleasingTheClaim_WhenTheNotifierThrows()
    {
        GivenEvent(EventStatus.Cancelled);
        var ana = ARegistration("Ana", "ana@acme.com", "REF-2026-00001");
        GivenAwaiting(ana);
        _notifier.SendAsync(Arg.Any<TransactionalNotification>(), Arg.Any<CancellationToken>())
            .Returns<Task<UnitResult<BaseError>>>(_ => throw new InvalidOperationException("boom"));

        await Should.ThrowAsync<InvalidOperationException>(() => _service.NotifyAsync(EventId, CancellationToken.None));

        await _registrations.Received(1).ReleaseCancellationNoticeAsync(ana.Id, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Si ni siquiera se pudo quitar el sello, esa persona queda sellada sin correo y fuera del cubo:
    /// la pasada se corta con el error para que quien llama se entere.
    /// </summary>
    [Fact]
    public async Task NotifyAsync_ReturnsTheReleaseError_WhenTheSendFailsAndTheReleaseFails()
    {
        GivenEvent(EventStatus.Cancelled);
        var ana = ARegistration("Ana", "ana@acme.com", "REF-2026-00001");
        var luis = ARegistration("Luis", "luis@acme.com", "REF-2026-00002");
        GivenAwaiting(ana, luis);
        _notifier.SendAsync(Arg.Any<TransactionalNotification>(), Arg.Any<CancellationToken>())
            .Returns(UnitResult.Failure<BaseError>(new NotificationsUnavailableError("no respondió")));
        _registrations.ReleaseCancellationNoticeAsync(ana.Id, Arg.Any<CancellationToken>())
            .Returns(UnitResult.Failure<BaseError>(new CancellationNoticeUnavailableError("base caída")));

        var result = await _service.NotifyAsync(EventId, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(CancellationNoticeUnavailableError.ErrorCode);
        await _registrations.DidNotReceive().ClaimCancellationNoticeAsync(luis.Id, Arg.Any<CancellationToken>());
    }

    #region Ayudas

    private void GivenEvent(string status, string language = "es") =>
        _events.GetEventAsync(EventId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<EventSummary>, BaseError>(Maybe.From(AnEvent(status, language))));

    private void GivenAwaiting(params Registration[] registrations) =>
        _registrations.ListAwaitingCancellationNoticeAsync(EventId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<Registration>, BaseError>(registrations));

    private void GivenTemplate(string language) =>
        _templates.GetAsync(TemplateKey.EventCancelled, language, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<Template>, BaseError>(Maybe.From(new Template(
                TemplateKey.EventCancelled,
                language,
                "{{evento}} ha sido cancelado",
                "<html>{{nombre}} {{evento}} {{fecha}} {{referencia}}</html>",
                TemplatePurpose.Transactional,
                isCampaignSelectable: false))));

    private static Registration ARegistration(string firstName, string email, string reference) => new(
        id: Guid.NewGuid(),
        eventId: EventId,
        campaignRecipientId: null,
        firstName: firstName,
        lastName: "Pérez",
        email: email,
        emailNormalized: email,
        phone: null,
        company: null,
        jobTitle: null,
        country: null,
        reference: reference,
        qrCode: Guid.NewGuid().ToString("N"),
        contactDetailsDiffer: false,
        crmContactId: null,
        crmSyncFailedAt: null,
        createdAt: DateTimeOffset.UtcNow,
        updatedAt: DateTimeOffset.UtcNow);

    private static EventSummary AnEvent(string status, string language) => new()
    {
        Id = EventId,
        Name = "Roadshow Madrid 2026",
        Type = "roadshow",
        Status = status,
        Language = language,
        Visibility = "public",
        StartDate = new DateOnly(2026, 6, 12),
        EndDate = new DateOnly(2026, 6, 12),
        Cities = [new EventCity("Madrid", "España", new DateOnly(2026, 6, 12), 1)],
    };

    #endregion
}
