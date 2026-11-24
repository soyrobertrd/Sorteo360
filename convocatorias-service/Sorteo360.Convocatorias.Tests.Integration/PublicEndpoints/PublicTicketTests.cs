using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.PublicEndpoints;

/// <summary>
/// La página de la entrada y la imagen del QR (spec <c>06</c> §2.3, §4.5, §4.6): dos lecturas
/// anónimas a las que se llega con un <c>qrCode</c>, sin iniciar sesión.
/// <para>
/// Se ejercita la cadena entera contra un events-service simulado por HTTP y un PostgreSQL de
/// verdad. <b>Cada test sirve su propio evento</b> con id propio: la base vive toda la clase.
/// </para>
/// </summary>
public sealed class PublicTicketTests : IClassFixture<RegistrationPortalFactory>
{
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly RegistrationPortalFactory _factory;

    public PublicTicketTests(RegistrationPortalFactory factory) => _factory = factory;

    #region La página de la entrada

    [Fact]
    public async Task GetTicket_ReturnsTheReferenceNameAndCompany_WhenTheCodeIsKnown()
    {
        var (eventId, client) = await AWalkInEventAsync();
        var registration = await BodyAsync(await RegistrationTestClient.RegisterAsync(
            client, eventId, email: Unique(), firstName: "Luis", lastName: "Peña", company: "Acme"));
        var qrCode = registration.GetProperty("qrCode").GetString()!;

        var body = await BodyAsync(await RegistrationTestClient.TicketAsync(client, qrCode));

        body.GetProperty("reference").GetString().ShouldBe(registration.GetProperty("reference").GetString());
        body.GetProperty("name").GetString().ShouldBe("Luis Peña");
        body.GetProperty("company").GetString().ShouldBe("Acme");
        body.GetProperty("event").GetProperty("id").GetGuid().ShouldBe(eventId);
    }

    /// <summary>
    /// La entrada es una ruta pública, así que carga el mismo recorte de la cartelera: ni
    /// presupuesto, ni costo, ni el aforo interno de la OPT.
    /// </summary>
    [Fact]
    public async Task GetTicket_ReturnsNoInternalNumbers_WhenItServes()
    {
        var (eventId, client) = await AWalkInEventAsync();
        var qrCode = await RegisterAndReadQrCodeAsync(client, eventId);

        var served = (await BodyAsync(await RegistrationTestClient.TicketAsync(client, qrCode)))
            .GetProperty("event");

        served.TryGetProperty("invitedCount", out _).ShouldBeFalse();
        served.TryGetProperty("estimatedBudget", out _).ShouldBeFalse();
        served.TryGetProperty("totalCost", out _).ShouldBeFalse();
        served.TryGetProperty("optContact", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task GetTicket_Returns404_WhenTheCodeIsUnknown()
    {
        _factory.Events.ServeEvents(PublicEvent(Guid.NewGuid()));
        var client = _factory.CreateClient();

        await ShouldBeRefusedAsync(await RegistrationTestClient.TicketAsync(client, "no-es-la-entrada-de-nadie"));
    }

    /// <summary>
    /// Quien ya tiene su entrada puede seguir mirándola aunque el evento se cancele: el escáner de
    /// la puerta no existe todavía y la entrada sigue en pie (spec <c>06</c> §4.6).
    /// </summary>
    [Fact]
    public async Task GetTicket_ReturnsTheTicket_WhenTheEventIsCancelled()
    {
        var eventId = Guid.NewGuid();
        _factory.Events.ServeEvents(PublicEvent(eventId));
        var client = _factory.CreateClient();
        var qrCode = await RegisterAndReadQrCodeAsync(client, eventId);

        // El evento se cancela después de que la persona ya se inscribió.
        _factory.Events.ServeEvents(PublicEvent(eventId) with { Status = StubEventStatus.Cancelled });

        var response = await RegistrationTestClient.TicketAsync(client, qrCode);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await BodyAsync(response)).GetProperty("event").GetProperty("id").GetGuid().ShouldBe(eventId);
    }

    #endregion

    #region La imagen del QR

    [Fact]
    public async Task GetQr_ReturnsAPng_WhenTheCodeIsKnown()
    {
        var (eventId, client) = await AWalkInEventAsync();
        var qrCode = await RegisterAndReadQrCodeAsync(client, eventId);

        var response = await RegistrationTestClient.QrAsync(client, qrCode);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");

        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.ShouldBeGreaterThan(0);
        bytes[..8].ShouldBe(PngMagic);
    }

    [Fact]
    public async Task GetQr_Returns404_WhenTheCodeIsUnknown()
    {
        _factory.Events.ServeEvents(PublicEvent(Guid.NewGuid()));
        var client = _factory.CreateClient();

        await ShouldBeRefusedAsync(await RegistrationTestClient.QrAsync(client, "no-es-la-entrada-de-nadie"));
    }

    /// <summary>
    /// El QR no lee el evento —codifica una URL armada solo con el código—, así que sigue
    /// sirviendo en un evento cancelado (spec <c>06</c> §4.5 y §4.6).
    /// </summary>
    [Fact]
    public async Task GetQr_ReturnsTheImage_WhenTheEventIsCancelled()
    {
        var eventId = Guid.NewGuid();
        _factory.Events.ServeEvents(PublicEvent(eventId));
        var client = _factory.CreateClient();
        var qrCode = await RegisterAndReadQrCodeAsync(client, eventId);

        _factory.Events.ServeEvents(PublicEvent(eventId) with { Status = StubEventStatus.Cancelled });

        var response = await RegistrationTestClient.QrAsync(client, qrCode);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync())[..8].ShouldBe(PngMagic);
    }

    #endregion

    #region Helpers

    private async Task<(Guid EventId, HttpClient Client)> AWalkInEventAsync()
    {
        var eventId = Guid.NewGuid();
        _factory.Events.ServeEvents(PublicEvent(eventId));
        return (eventId, _factory.CreateClient());
    }

    /// <summary>
    /// Inscribe a alguien y devuelve su <c>qrCode</c>, que es la llave de las dos rutas de acá.
    /// Se llega desde la cartelera, sin token: lo que se prueba es la lectura, no la invitación.
    /// </summary>
    private static async Task<string> RegisterAndReadQrCodeAsync(HttpClient client, Guid eventId)
    {
        var body = await BodyAsync(await RegistrationTestClient.RegisterAsync(client, eventId, email: Unique()));
        return body.GetProperty("qrCode").GetString()!;
    }

    private static async Task ShouldBeRefusedAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().ShouldBe(PublicRegistrationNotFoundError.ErrorCode);
    }

    private static StubEvent PublicEvent(Guid id) => new(
        id, "Roadshow Madrid 2026", EventsStub.PublicVisibility, "es", StubEventStatus.InProgress);

    private static string Unique() =>
        EmailAddressShape.Normalize($"p{Guid.NewGuid():N}@acme.com");

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    #endregion
}
