using System.Net;
using System.Text.Json;
using Sorteo360.Convocatorias.Core.Registrations;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Sorteo360.Convocatorias.Tests.Integration.PublicEndpoints;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.Notifications;

/// <summary>
/// La confirmación de una inscripción por el transporte de verdad: se inscribe una persona y se
/// mira el JSON que salió al cable. Es la hermana transaccional de
/// <see cref="HttpCampaignSendGatewayTests"/>, contra el mismo notification-service simulado.
/// </summary>
public sealed class HttpTransactionalNotifierTests : IClassFixture<HttpNotificationFactory>
{
    private readonly HttpNotificationFactory _factory;

    public HttpTransactionalNotifierTests(HttpNotificationFactory factory)
    {
        _factory = factory;
        _factory.Notifications.ServeDefaults();
    }

    /// <summary>
    /// Volver a inscribirse con la misma dirección reenvía la confirmación a propósito, con el
    /// mismo cuerpo. Las dos llevan quién firma, y cada una su clave: con la misma, el emisor
    /// leería el reenvío como una repetición y contestaría <c>202</c> sin mandar nada.
    /// </summary>
    [Fact]
    public async Task Register_ReturnsTheConfirmationWithItsOwnKey_WhenThePersonRegistersTwice()
    {
        var eventId = Guid.NewGuid();
        _factory.Events.ServeEvents(new StubEvent(
            eventId, "Roadshow Madrid 2026", EventsStub.PublicVisibility, "es", StubEventStatus.InProgress));
        var client = _factory.CreateClient();
        var email = EmailAddressShape.Normalize($"p{Guid.NewGuid():N}@acme.com");

        (await RegistrationTestClient.RegisterAsync(client, eventId, token: null, email: email)).StatusCode
            .ShouldBe(HttpStatusCode.Created);
        (await RegistrationTestClient.RegisterAsync(client, eventId, token: null, email: email)).StatusCode
            .ShouldBe(HttpStatusCode.OK);

        var confirmations = _factory.Notifications.PostedBatches
            .Select(body => JsonDocument.Parse(body).RootElement.Clone())
            .ToList();

        confirmations.Count.ShouldBe(2);
        confirmations.ShouldAllBe(batch =>
            batch.GetProperty("type").GetString() == RegistrationConfirmationSender.NotificationType);
        confirmations.ShouldAllBe(batch =>
            batch.GetProperty("fromAddress").GetString() == ServiceIdentityStub.FromAddress);

        var keys = confirmations.Select(batch => batch.GetProperty("idempotencyKey").GetString()).ToList();

        keys.ShouldAllBe(key => key!.StartsWith("convocatorias:registration:", StringComparison.Ordinal));
        keys.Distinct().Count().ShouldBe(2);
    }
}
