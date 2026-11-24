using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Events;
using Sorteo360.Convocatorias.Core.Notifications;
using Sorteo360.Convocatorias.Core.Portal;
using Sorteo360.Convocatorias.Core.Registrations;
using Sorteo360.Convocatorias.Core.Rendering;
using Sorteo360.Convocatorias.Core.Templates;
using Sorteo360.Platform.Errors;
using NSubstitute;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Registrations;

/// <summary>
/// El correo de confirmación de una inscripción (spec <c>06</c> §4.4): la plantilla en el idioma
/// del evento, con la referencia, la URL del QR y el enlace a la entrada, y nada de la campaña.
/// Arma las dos bolsas y se las pasa al transporte; no sustituye.
/// </summary>
public sealed class RegistrationConfirmationSenderTests
{
    private const string PortalBaseUrl = "https://app.pgti360.test";
    private const string QrCode = "qr-abc-123";
    private const string Reference = "REF-2026-00427";
    private const string RecipientEmail = "juan@expedia.com";

    private static readonly Guid EventId = Guid.NewGuid();

    private readonly ITemplateRepository _templates = Substitute.For<ITemplateRepository>();
    private readonly ITransactionalNotifier _notifier = Substitute.For<ITransactionalNotifier>();
    private readonly MovableClock _clock = new(new DateTimeOffset(2026, 9, 23, 14, 0, 0, TimeSpan.Zero));
    private readonly RegistrationConfirmationSender _sender;

    private TransactionalNotification? _sent;

    public RegistrationConfirmationSenderTests()
    {
        _notifier
            .SendAsync(Arg.Do<TransactionalNotification>(notification => _sent = notification), Arg.Any<CancellationToken>())
            .Returns(UnitResult.Success<BaseError>());

        _sender = new RegistrationConfirmationSender(
            _templates,
            _notifier,
            new PortalSettings { BaseUrl = PortalBaseUrl },
            _clock);
    }

    [Fact]
    public async Task SendAsync_ReturnsTheConfirmationInTheEventLanguage_WhenTheTemplateExists()
    {
        GivenTemplate("en");

        var result = await _sender.SendAsync(ARegistration(), AnEvent(language: "en"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _templates.Received(1).GetAsync(TemplateKey.RegistrationConfirmation, "en", Arg.Any<CancellationToken>());
        _sent.ShouldNotBeNull();
        _sent.Language.ShouldBe("en");
        _sent.Type.ShouldBe(RegistrationConfirmationSender.NotificationType);
        _sent.EventId.ShouldBe(EventId);
        _sent.RecipientEmail.ShouldBe(RecipientEmail);
        _sent.Subject.ShouldBe("Su registro a {{evento}} está confirmado");
    }

    [Fact]
    public async Task SendAsync_ReturnsTheReferenceAndQrInTheRecipientData_WhenItBuildsTheConfirmation()
    {
        GivenTemplate("es");

        await _sender.SendAsync(ARegistration(), AnEvent(), CancellationToken.None);

        _sent.ShouldNotBeNull();
        _sent.RecipientData[SupportedPlaceholder.RecipientName].ShouldBe("Juan");
        _sent.RecipientData[SupportedPlaceholder.Reference].ShouldBe(Reference);
        _sent.RecipientData[SupportedPlaceholder.QrImageUrl]
            .ShouldBe($"{PortalBaseUrl}/v1/public/registrations/{QrCode}/qr");
        _sent.RecipientData[SupportedPlaceholder.TicketLink]
            .ShouldBe($"{PortalBaseUrl}/publico/entrada/{QrCode}");
        _sent.SharedData[SupportedPlaceholder.EventName].ShouldBe("Roadshow Madrid 2026");
    }

    [Fact]
    public async Task SendAsync_ReturnsNothingAboutTheCampaign_WhenItBuildsTheConfirmation()
    {
        GivenTemplate("es");

        await _sender.SendAsync(ARegistration(), AnEvent(), CancellationToken.None);

        _sent.ShouldNotBeNull();
        _sent.RecipientData.ShouldNotContainKey(SupportedPlaceholder.RegistrationLink);
        _sent.RecipientData.ShouldNotContainKey(SupportedPlaceholder.UnsubscribeLink);
        _sent.SharedData.Keys.ShouldBe(
        [
            SupportedPlaceholder.EventName,
            SupportedPlaceholder.EventLocation,
            SupportedPlaceholder.EventDates,
        ],
        ignoreOrder: true);
    }

    [Fact]
    public async Task SendAsync_ReturnsAFailure_WhenTheTemplateIsMissing()
    {
        _templates.GetAsync(TemplateKey.RegistrationConfirmation, "es", Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<Template>, BaseError>(Maybe<Template>.None));

        var result = await _sender.SendAsync(ARegistration(), AnEvent(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(RegistrationConfirmationUnavailableError.ErrorCode);
        await _notifier.DidNotReceive().SendAsync(Arg.Any<TransactionalNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_ReturnsTheNotifierResult_WhenTheNotifierFails()
    {
        GivenTemplate("es");
        _notifier.SendAsync(Arg.Any<TransactionalNotification>(), Arg.Any<CancellationToken>())
            .Returns(UnitResult.Failure<BaseError>(new NotificationsUnavailableError("no respondió")));

        var result = await _sender.SendAsync(ARegistration(), AnEvent(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(NotificationsUnavailableError.ErrorCode);
    }

    [Fact]
    public async Task SendAsync_ReturnsAKeyNamingTheRegistration_WhenItBuildsTheConfirmation()
    {
        GivenTemplate("es");
        var registration = ARegistration();

        await _sender.SendAsync(registration, AnEvent(), CancellationToken.None);

        _sent.ShouldNotBeNull();
        _sent.IdempotencyKey.ShouldBe(
            NotificationIdempotencyKey.ForRegistrationConfirmation(registration.Id, _clock.GetUtcNow()));
    }

    /// <summary>
    /// Volver a inscribirse reenvía la confirmación a propósito, con un cuerpo idéntico. Con la
    /// misma clave el emisor la leería como repetición y no mandaría nada.
    /// </summary>
    [Fact]
    public async Task SendAsync_ReturnsANewKey_WhenTheSameRegistrationAsksForItAgain()
    {
        GivenTemplate("es");
        var registration = ARegistration();

        await _sender.SendAsync(registration, AnEvent(), CancellationToken.None);
        var first = _sent!.IdempotencyKey;

        _clock.Advance(TimeSpan.FromMinutes(3));
        await _sender.SendAsync(registration, AnEvent(), CancellationToken.None);

        _sent!.IdempotencyKey.ShouldNotBe(first);
    }

    private void GivenTemplate(string language) =>
        _templates.GetAsync(TemplateKey.RegistrationConfirmation, language, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<Template>, BaseError>(Maybe.From(ConfirmationTemplate(language))));

    private static Template ConfirmationTemplate(string language) => new(
        TemplateKey.RegistrationConfirmation,
        language,
        "Su registro a {{evento}} está confirmado",
        "<html>{{nombre}} {{referencia}} <img src=\"{{qr}}\"> <a href=\"{{enlace_entrada}}\">entrada</a></html>",
        TemplatePurpose.Transactional,
        isCampaignSelectable: false);

    private static Registration ARegistration() => new(
        id: Guid.NewGuid(),
        eventId: EventId,
        campaignRecipientId: null,
        firstName: "Juan",
        lastName: "Pérez",
        email: RecipientEmail,
        emailNormalized: RecipientEmail,
        phone: null,
        company: null,
        jobTitle: null,
        country: null,
        reference: Reference,
        qrCode: QrCode,
        contactDetailsDiffer: false,
        crmContactId: null,
        crmSyncFailedAt: null,
        createdAt: DateTimeOffset.UtcNow,
        updatedAt: DateTimeOffset.UtcNow);

    private static EventSummary AnEvent(string language = "es") => new()
    {
        Id = EventId,
        Name = "Roadshow Madrid 2026",
        Type = "roadshow",
        Status = "in_progress",
        Language = language,
        Visibility = "public",
        StartDate = new DateOnly(2026, 6, 12),
        EndDate = new DateOnly(2026, 6, 12),
        Cities = [new EventCity("Madrid", "España", new DateOnly(2026, 6, 12), 1)],
    };

    /// <summary>Un reloj que solo avanza cuando el test lo empuja.</summary>
    private sealed class MovableClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public void Advance(TimeSpan by) => _now = _now.Add(by);

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
