using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Infrastructure.Notifications;
using Sorteo360.Convocatorias.Tests.Unit.Neighbours;
using Sorteo360.Platform.Errors;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Notifications;

/// <summary>
/// La traducción de la avería del envío de un lote. El cable y el emparejado se prueban contra el
/// stub de verdad en la suite de integración; acá solo lo que no se puede provocar desde allí sin
/// esperar: una caída, un tiempo agotado y un host que se para a mitad del envío.
/// </summary>
public sealed class HttpCampaignSendGatewayTests
{
    private readonly RecordingLogger<HttpCampaignSendGateway> _logger = new();

    [Fact]
    public async Task SendAsync_ReturnsNotificationsUnavailable_WhenTheSenderDoesNotAnswer()
    {
        var result = await SendAsync(new HttpRequestException("caído"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(NotificationsUnavailableError.ErrorCode);
        _logger.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task SendAsync_ReturnsNotificationsUnavailable_WhenTheSenderTimesOut()
    {
        var result = await SendAsync(new TaskCanceledException("tardó demasiado"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(NotificationsUnavailableError.ErrorCode);
        _logger.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task SendAsync_Throws_WhenTheHostIsStopping()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<TaskCanceledException>(
            () => SendAsync(new TaskCanceledException("se paró el host"), cancellation.Token));

        _logger.Errors.ShouldBeEmpty();
    }

    private async Task<Result<SendBatchResult, BaseError>> SendAsync(
        Exception failure,
        CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient(new ThrowingHandler(failure))
        {
            BaseAddress = new Uri("http://notification-service/")
        };

        var gateway = new HttpCampaignSendGateway(
            httpClient,
            Options.Create(new NotificationSettings { FromAddress = "no-reply@sorteo360.local" }),
            _logger);

        return await gateway.SendAsync(Batch(), cancellationToken);
    }

    private static SendBatchRequest Batch() =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "es",
            "Te invitamos",
            "<p>Hola {{nombre}}</p>",
            new Dictionary<string, string>(),
            [new SendBatchRecipient(Guid.NewGuid(), "ana@example.com", "Ana Pérez", new Dictionary<string, string> { ["nombre"] = "Ana" })]);
}
