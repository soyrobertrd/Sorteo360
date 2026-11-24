using System.Net;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.PublicEndpoints;

/// <summary>
/// La confirmación con el transporte real (<c>http</c>) y notification-service caído (spec
/// <c>06</c> §4.4): la inscripción sigue en pie y contesta <c>201</c>, porque el QR está en la
/// pantalla y la referencia impresa. El correo llegará después.
/// <para>
/// Clase aparte con su propia factory porque tumba notification-service y la tubería de
/// resiliencia del chasis es un singleton: un circuito abierto se filtraría a la clase siguiente.
/// </para>
/// </summary>
public sealed class RegistrationConfirmationOutageTests : IClassFixture<HttpNotificationFactory>
{
    private readonly HttpNotificationFactory _factory;

    public RegistrationConfirmationOutageTests(HttpNotificationFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_Returns201_WhenTheNotificationTransportIsDown()
    {
        _factory.Notifications.GoDown();
        var eventId = Guid.NewGuid();
        _factory.Events.ServeEvents(new StubEvent(
            eventId, "Roadshow Madrid 2026", EventsStub.PublicVisibility, "es", StubEventStatus.InProgress));
        var client = _factory.CreateClient();
        var email = EmailAddressShape.Normalize($"p{Guid.NewGuid():N}@acme.com");

        var response = await RegistrationTestClient.RegisterAsync(client, eventId, token: null, email: email);

        // El correo no salió, pero la inscripción está guardada: la respuesta es la misma.
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await RegistrationTestClient.CountAsync(_factory, eventId)).ShouldBe(1);
    }
}
