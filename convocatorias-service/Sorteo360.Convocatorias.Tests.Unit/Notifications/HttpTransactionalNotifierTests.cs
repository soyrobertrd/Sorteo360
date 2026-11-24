using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Notifications;
using Sorteo360.Convocatorias.Infrastructure.Notifications;
using Sorteo360.Convocatorias.Tests.Unit.Neighbours;
using Sorteo360.Platform.Errors;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Notifications;

/// <summary>
/// La traducción de la avería de un correo transaccional: una caída o un tiempo agotado es un
/// fallo que quien llama registra y deja pasar, y un visitante que se va no es una caída.
/// </summary>
public sealed class HttpTransactionalNotifierTests
{
    private readonly RecordingLogger<HttpTransactionalNotifier> _logger = new();

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
    public async Task SendAsync_Throws_WhenTheCallerWalkedAway()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<TaskCanceledException>(
            () => SendAsync(new TaskCanceledException("quien preguntaba se fue"), cancellation.Token));

        _logger.Errors.ShouldBeEmpty();
    }

    private async Task<UnitResult<BaseError>> SendAsync(Exception failure, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient(new ThrowingHandler(failure))
        {
            BaseAddress = new Uri("http://notification-service/")
        };

        var notifier = new HttpTransactionalNotifier(
            httpClient,
            Options.Create(new NotificationSettings { FromAddress = "no-reply@sorteo360.local" }),
            _logger);

        return await notifier.SendAsync(Notification(), cancellationToken);
    }

    private static TransactionalNotification Notification() =>
        new(
            "registration_confirmation",
            "es",
            Guid.NewGuid(),
            "convocatorias:registration:abc:confirmation:1",
            "Tu inscripción",
            "<p>Hola {{nombre}}</p>",
            new Dictionary<string, string>(),
            "ana@example.com",
            "Ana Pérez",
            new Dictionary<string, string> { ["nombre"] = "Ana" });
}
