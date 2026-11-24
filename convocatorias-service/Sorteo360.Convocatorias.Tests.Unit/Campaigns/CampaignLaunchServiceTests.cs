using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Crm;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.DTOs;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Events;
using Sorteo360.Convocatorias.Core.Templates;
using Sorteo360.Platform.Errors;
using NSubstitute;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Campaigns;

/// <summary>
/// El paso 1 del lanzamiento (spec <c>04</c> §1): lo que se puede negar sin llamar a nadie. Lo que
/// se cobra acá es el orden, que el resto de la suite no ve: un rechazo del paso 1 no le pregunta
/// nada al CRM ni a events-service, ni salta, ni encola.
/// </summary>
public sealed class CampaignLaunchServiceTests
{
    private readonly ICampaignRepository _repository = Substitute.For<ICampaignRepository>();
    private readonly ICrmContactDirectory _crm = Substitute.For<ICrmContactDirectory>();
    private readonly IEventDirectory _events = Substitute.For<IEventDirectory>();
    private readonly IRegistrationWindowRepository _windows = Substitute.For<IRegistrationWindowRepository>();
    private readonly ICampaignHandoffQueue _queue = Substitute.For<ICampaignHandoffQueue>();
    private readonly CampaignLaunchService _service;

    private readonly Guid _campaignId = Guid.NewGuid();

    public CampaignLaunchServiceTests()
    {
        _service = new CampaignLaunchService(_repository, _crm, _events, _windows, _queue);
    }

    #region La plantilla que falta

    [Fact]
    public async Task LaunchAsync_ReturnsCampaignTemplateNotFound_WhenThePreflightHasNoTemplate()
    {
        GivenPreflight(Preflight(CampaignState.Draft, hasTemplate: false, language: "en"));

        var result = await _service.LaunchAsync(Launch(expectedRecipientCount: 1), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<CampaignTemplateNotFoundError>();
        error.TemplateKey.ShouldBe(TemplateKey.RegistrationInvite);
        error.Language.ShouldBe("en");
        error.Kind.ShouldBe(ErrorKind.Invalid);

        await ShouldHaveTouchedNothingAfterTheReadAsync();
    }

    /// <summary>
    /// Una campaña que ya salió contesta por su estado y no por su plantilla: lanzarla otra vez es
    /// el error que hay que decir, y arreglar la plantilla no lo arreglaría.
    /// </summary>
    [Fact]
    public async Task LaunchAsync_ReturnsTransitionNotAllowed_WhenTheCampaignIsNotADraftAndHasNoTemplate()
    {
        GivenPreflight(Preflight(CampaignState.Sending, hasTemplate: false, language: "en"));

        var result = await _service.LaunchAsync(Launch(expectedRecipientCount: 1), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<CampaignTransitionNotAllowedError>();

        await ShouldHaveTouchedNothingAfterTheReadAsync();
    }

    /// <summary>
    /// Sin destinatarios y sin plantilla, gana la plantilla: es la negativa que no se arregla
    /// seleccionando a nadie, y decirla primero evita armar una lista para un correo que no existe.
    /// </summary>
    [Fact]
    public async Task LaunchAsync_ReturnsCampaignTemplateNotFound_WhenTheCampaignAlsoHasNoRecipients()
    {
        GivenPreflight(Preflight(CampaignState.Draft, hasTemplate: false, language: "en", recipientCount: 0));

        var result = await _service.LaunchAsync(Launch(expectedRecipientCount: 0), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<CampaignTemplateNotFoundError>();
    }

    #endregion

    #region El mensaje directo

    /// <summary>
    /// Un mensaje directo no tiene evento: se salta la lectura del evento y la de la ventana, y
    /// pasa igual por el vaciado de bajas y la recomprobación del CRM, que es lo que lo hace una
    /// campaña y no un atajo (PGTI-174).
    /// </summary>
    [Fact]
    public async Task LaunchAsync_ReturnsTheLaunchWithoutReadingAnEvent_WhenTheCampaignIsADirectMessage()
    {
        var contactId = Guid.NewGuid();

        GivenPreflight(new CampaignLaunchPreflight(
            CampaignState.Draft,
            EventId: null,
            TemplateKey.DirectMessage,
            "es",
            HasTemplate: true,
            [contactId]));

        _repository.ListUnsyncedUnsubscribesAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<Guid>, BaseError>([]));
        _crm.CheckMailabilityAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<CrmMailability>, BaseError>([new CrmMailability(contactId, true, null)]));
        _repository.LaunchAsync(
                Arg.Any<LaunchCampaignDto>(),
                Arg.Any<IReadOnlyList<RecipientSuppression>>(),
                Arg.Any<CampaignEventValues?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<CampaignSummary>, BaseError>(Maybe<CampaignSummary>.None));

        var result = await _service.LaunchAsync(Launch(expectedRecipientCount: 1), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        await _repository.Received(1).ListUnsyncedUnsubscribesAsync(Arg.Any<CancellationToken>());
        await _crm.Received(1).CheckMailabilityAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>());
        await _events.DidNotReceive().GetEventAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _windows.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).LaunchAsync(
            Arg.Any<LaunchCampaignDto>(),
            Arg.Any<IReadOnlyList<RecipientSuppression>>(),
            null,
            null,
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region El remitente

    [Fact]
    public async Task LaunchAsync_ReturnsTheLaunchWithoutASenderEmail_UsingGeneralAddress()
    {
        GivenADirectMessageReadyToLaunch();

        var result = await _service.LaunchAsync(Launch(expectedRecipientCount: 1, optCode: "madrid"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await ShouldHaveLaunchedWithSenderAsync(null);
    }

    #endregion

    #region Helpers

    /// <summary>Un mensaje directo con un destinatario contactable: llega al salto sin leer ningún evento.</summary>
    private void GivenADirectMessageReadyToLaunch()
    {
        var contactId = Guid.NewGuid();

        GivenPreflight(new CampaignLaunchPreflight(
            CampaignState.Draft,
            EventId: null,
            TemplateKey.DirectMessage,
            "es",
            HasTemplate: true,
            [contactId]));

        _repository.ListUnsyncedUnsubscribesAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<Guid>, BaseError>([]));
        _crm.CheckMailabilityAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<CrmMailability>, BaseError>([new CrmMailability(contactId, true, null)]));
        _repository.LaunchAsync(
                Arg.Any<LaunchCampaignDto>(),
                Arg.Any<IReadOnlyList<RecipientSuppression>>(),
                Arg.Any<CampaignEventValues?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<CampaignSummary>, BaseError>(Maybe<CampaignSummary>.None));
    }

    private Task ShouldHaveLaunchedWithSenderAsync(string? senderEmail) =>
        _repository.Received(1).LaunchAsync(
            Arg.Any<LaunchCampaignDto>(),
            Arg.Any<IReadOnlyList<RecipientSuppression>>(),
            Arg.Any<CampaignEventValues?>(),
            senderEmail,
            Arg.Any<CancellationToken>());

    private void GivenPreflight(CampaignLaunchPreflight preflight) =>
        _repository.ReadLaunchPreflightAsync(_campaignId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<CampaignLaunchPreflight>, BaseError>(Maybe.From(preflight)));

    private static CampaignLaunchPreflight Preflight(
        string state,
        bool hasTemplate,
        string language,
        int recipientCount = 1) =>
        new(
            state,
            Guid.NewGuid(),
            TemplateKey.RegistrationInvite,
            language,
            hasTemplate,
            [.. Enumerable.Range(0, recipientCount).Select(_ => Guid.NewGuid())]);

    private LaunchCampaignDto Launch(int expectedRecipientCount, string? optCode = null) =>
        new(_campaignId, expectedRecipientCount, Guid.NewGuid(), optCode);

    private async Task ShouldHaveTouchedNothingAfterTheReadAsync()
    {
        await _repository.DidNotReceive().ListUnsyncedUnsubscribesAsync(Arg.Any<CancellationToken>());
        await _crm.DidNotReceive().CheckMailabilityAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>());
        await _events.DidNotReceive().GetEventAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().LaunchAsync(
            Arg.Any<LaunchCampaignDto>(),
            Arg.Any<IReadOnlyList<RecipientSuppression>>(),
            Arg.Any<CampaignEventValues>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
        _queue.DidNotReceive().Enqueue(Arg.Any<Guid>());
    }

    #endregion
}
