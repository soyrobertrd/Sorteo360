using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Infrastructure.Crm;
using Sorteo360.Convocatorias.Tests.Unit.Neighbours;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Crm;

/// <summary>
/// Las dos llamadas al CRM que hace el lanzamiento: el vaciado de bajas y la recomprobación de
/// contactabilidad (spec <c>04</c> §3 y §3.1).
/// <para>
/// Lo que se cobra acá no es el mapeo sino los dos desenlaces que se parecen y no son lo mismo:
/// una respuesta <b>corta</b> —el CRM no devolvió a alguien— es un dato normal que acaba en una
/// supresión, y una respuesta <b>vacía de cuerpo</b> es una avería. Confundirlas suprimiría la
/// campaña entera en silencio.
/// </para>
/// </summary>
public sealed class CrmContactDirectoryLaunchTests
{
    private readonly ICrmContactsClient _client = Substitute.For<ICrmContactsClient>();
    private readonly RecordingLogger<CrmContactDirectory> _logger = new();
    private readonly CrmContactDirectory _directory;

    public CrmContactDirectoryLaunchTests() => _directory = new CrmContactDirectory(_client, _logger);

    #region El vaciado de bajas

    [Fact]
    public async Task OptOutAsync_ReturnsSuccessWithACampaignUnsubscribeSource_WhenCalled()
    {
        var contactId = Guid.NewGuid();

        var result = await _directory.OptOutAsync(contactId, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _client.Received(1).OptOutContactAsync(
            contactId,
            Arg.Is<CrmOptOutRequest>(request => request.Source == CrmOptOutSources.CampaignUnsubscribe),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// El CRM ya no tiene esa ficha. Lo que se le pedía —que sepa que esa persona se dio de baja—
    /// se cumple igual, porque a una ficha borrada no le escribe nadie.
    /// <para>
    /// Fallar acá sería la avería peor de las dos: el vaciado recorre todo el servicio y no sella
    /// lo que no llegó, así que esa fila volvería a intentarse en <b>cada</b> lanzamiento y los
    /// dejaría todos en 503 para siempre, sin que nadie pueda sacarla de en medio.
    /// </para>
    /// </summary>
    [Fact]
    public async Task OptOutAsync_ReturnsSuccess_WhenTheCrmNoLongerHasTheContact()
    {
        _client.OptOutContactAsync(Arg.Any<Guid>(), Arg.Any<CrmOptOutRequest>(), Arg.Any<CancellationToken>())
            .Returns(CrmOptOutOutcome.ContactGone);

        var result = await _directory.OptOutAsync(Guid.NewGuid(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task OptOutAsync_ReturnsCrmUnavailable_WhenTheCrmRefusesTheOptOut()
    {
        _client.OptOutContactAsync(Arg.Any<Guid>(), Arg.Any<CrmOptOutRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("caído"));

        var result = await _directory.OptOutAsync(Guid.NewGuid(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(CrmUnavailableError.ErrorCode);
        _logger.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task OptOutAsync_ReturnsCrmUnavailable_WhenTheCrmTimesOut()
    {
        _client.OptOutContactAsync(Arg.Any<Guid>(), Arg.Any<CrmOptOutRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("tardó demasiado"));

        var result = await _directory.OptOutAsync(Guid.NewGuid(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(CrmUnavailableError.ErrorCode);
    }

    [Fact]
    public async Task OptOutAsync_ThrowsTaskCanceledNotAnOutage_WhenTheCallerWalkedAwayMidFlush()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _client.OptOutContactAsync(Arg.Any<Guid>(), Arg.Any<CrmOptOutRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("quien preguntaba se fue"));

        await Should.ThrowAsync<TaskCanceledException>(
            () => _directory.OptOutAsync(Guid.NewGuid(), cancellation.Token));

        _logger.Errors.ShouldBeEmpty();
    }

    #endregion

    #region La recomprobación

    /// <summary>
    /// El tope de 1.000 es del CRM: pasarse es un 400, así que 2.500 destinatarios son tres
    /// llamadas y la respuesta llega unida.
    /// </summary>
    [Fact]
    public async Task CheckMailabilityAsync_ReturnsBatchesOfAThousandJoined_WhenTheListExceedsAThousand()
    {
        var contactIds = Enumerable.Range(0, 2500).Select(_ => Guid.NewGuid()).ToList();

        _client.CheckMailabilityAsync(Arg.Any<CrmMailabilityRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => Mailable(call.Arg<CrmMailabilityRequest>().Ids));

        var result = await _directory.CheckMailabilityAsync(contactIds, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Select(verdict => verdict.Id).ShouldBe(contactIds, ignoreOrder: true);

        var batchSizes = _client.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(ICrmContactsClient.CheckMailabilityAsync))
            .Select(call => ((CrmMailabilityRequest)call.GetArguments()[0]!).Ids.Count)
            .ToList();

        batchSizes.ShouldBe([CrmContactDirectory.MailabilityBatchSize, CrmContactDirectory.MailabilityBatchSize, 500]);
    }

    [Fact]
    public async Task CheckMailabilityAsync_ReturnsSuccessFromOneCall_WhenTheListIsExactlyAThousand()
    {
        var contactIds = Enumerable.Range(0, CrmContactDirectory.MailabilityBatchSize)
            .Select(_ => Guid.NewGuid())
            .ToList();

        _client.CheckMailabilityAsync(Arg.Any<CrmMailabilityRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => Mailable(call.Arg<CrmMailabilityRequest>().Ids));

        var result = await _directory.CheckMailabilityAsync(contactIds, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _client.Received(1).CheckMailabilityAsync(
            Arg.Any<CrmMailabilityRequest>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Sin destinatarios no hay nada que preguntar, y preguntarlo igual sería un 400 del CRM: su
    /// validador exige una lista y este servicio no le manda una vacía.
    /// </summary>
    [Fact]
    public async Task CheckMailabilityAsync_ReturnsEmptyWithoutAskingTheCrm_WhenTheListIsEmpty()
    {
        var result = await _directory.CheckMailabilityAsync([], CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
        await _client.DidNotReceive().CheckMailabilityAsync(
            Arg.Any<CrmMailabilityRequest>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Una respuesta corta no es una avería: el CRM no informa sobre una ficha que no devuelve, y
    /// quien llama convierte esa ausencia en <c>contact_unavailable</c>.
    /// </summary>
    [Fact]
    public async Task CheckMailabilityAsync_ReturnsAShortListNotAFailure_WhenTheAnswerMissesAnId()
    {
        var present = Guid.NewGuid();
        var missing = Guid.NewGuid();

        _client.CheckMailabilityAsync(Arg.Any<CrmMailabilityRequest>(), Arg.Any<CancellationToken>())
            .Returns([new CrmMailabilityResponse(present, true, null)]);

        var result = await _directory.CheckMailabilityAsync([present, missing], CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Select(verdict => verdict.Id).ShouldBe([present]);
    }

    [Fact]
    public async Task CheckMailabilityAsync_ReturnsTheReasonTheCrmSent_WhenTheVerdictIsSuppressed()
    {
        var contactId = Guid.NewGuid();

        _client.CheckMailabilityAsync(Arg.Any<CrmMailabilityRequest>(), Arg.Any<CancellationToken>())
            .Returns([new CrmMailabilityResponse(contactId, false, "opted_out")]);

        var result = await _directory.CheckMailabilityAsync([contactId], CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Single().CanBeEmailed.ShouldBeFalse();
        result.Value.Single().SuppressionReason.ShouldBe("opted_out");
    }

    /// <summary>
    /// Un 200 sin cuerpo no es "no se puede contactar a nadie" —eso sería una lista vacía de
    /// veredictos— sino un CRM que contestó sin decir nada. Tomarlo por respuesta suprimiría la
    /// campaña entera en silencio.
    /// </summary>
    [Fact]
    public async Task CheckMailabilityAsync_ReturnsCrmUnavailableNotASuppression_WhenA200HasNoBody()
    {
        _client.CheckMailabilityAsync(Arg.Any<CrmMailabilityRequest>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<CrmMailabilityResponse>?)null);

        var result = await _directory.CheckMailabilityAsync([Guid.NewGuid()], CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(CrmUnavailableError.ErrorCode);
        _logger.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task CheckMailabilityAsync_ReturnsCrmUnavailable_WhenTheCrmDoesNotAnswer()
    {
        _client.CheckMailabilityAsync(Arg.Any<CrmMailabilityRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("caído"));

        var result = await _directory.CheckMailabilityAsync([Guid.NewGuid()], CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(CrmUnavailableError.ErrorCode);
        _logger.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task CheckMailabilityAsync_ReturnsCrmUnavailable_WhenTheCrmTimesOut()
    {
        _client.CheckMailabilityAsync(Arg.Any<CrmMailabilityRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("tardó demasiado"));

        var result = await _directory.CheckMailabilityAsync([Guid.NewGuid()], CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(CrmUnavailableError.ErrorCode);
    }

    [Fact]
    public async Task CheckMailabilityAsync_ThrowsTaskCanceledNotAnOutage_WhenTheCallerWalkedAwayMidCheck()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _client.CheckMailabilityAsync(Arg.Any<CrmMailabilityRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("quien preguntaba se fue"));

        await Should.ThrowAsync<TaskCanceledException>(
            () => _directory.CheckMailabilityAsync([Guid.NewGuid()], cancellation.Token));

        _logger.Errors.ShouldBeEmpty();
    }

    #endregion

    private static IReadOnlyList<CrmMailabilityResponse> Mailable(IReadOnlyList<Guid> ids) =>
        [.. ids.Select(id => new CrmMailabilityResponse(id, true, null))];
}
