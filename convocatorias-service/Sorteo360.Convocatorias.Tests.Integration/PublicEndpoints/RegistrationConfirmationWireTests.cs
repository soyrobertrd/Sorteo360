using System.Net;
using System.Text.Json;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.PublicEndpoints;

/// <summary>
/// La confirmación de una inscripción por el transporte real (<c>http</c>): lo que se mira es el
/// JSON que salió al cable hacia notification-service.
/// </summary>
public sealed class RegistrationConfirmationWireTests : IClassFixture<HttpNotificationFactory>
{
    private readonly HttpNotificationFactory _factory;

    public RegistrationConfirmationWireTests(HttpNotificationFactory factory)
    {
        _factory = factory;
        _factory.Notifications.ServeDefaults();
    }

    [Fact]
    public async Task Register_Returns201AndPostsTheFullNameAsTheRecipientName_WhenTheConfirmationGoesOut()
    {
        var eventId = Guid.NewGuid();
        _factory.Events.ServeEvents(new StubEvent(
            eventId, "Roadshow Madrid 2026", EventsStub.PublicVisibility, "es", StubEventStatus.InProgress));
        var client = _factory.CreateClient();
        var email = EmailAddressShape.Normalize($"p{Guid.NewGuid():N}@acme.com");

        var response = await RegistrationTestClient.RegisterAsync(
            client, eventId, token: null, email: email, firstName: "Luis", lastName: "Peña");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var batch = JsonDocument.Parse(_factory.Notifications.PostedBatches.ShouldHaveSingleItem()).RootElement;
        var recipient = batch.GetProperty("recipients").EnumerateArray().ShouldHaveSingleItem();

        recipient.GetProperty("email").GetString().ShouldBe(email);
        recipient.GetProperty("name").GetString().ShouldBe("Luis Peña");

        // El marcador del cuerpo sigue siendo solo el nombre de pila.
        recipient.GetProperty("data").GetProperty("nombre").GetString().ShouldBe("Luis");
    }
}
