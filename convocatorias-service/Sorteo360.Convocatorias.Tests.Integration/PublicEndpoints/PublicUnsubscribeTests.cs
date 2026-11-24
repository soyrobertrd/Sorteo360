using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.PublicEndpoints;

/// <summary>
/// La baja del portal (spec <c>06</c> §6). <b>La puerta es convocatorias, el estado es el CRM</b>:
/// el clic marca la fila y empuja el opt-out al contacto, y con el CRM caído la baja igual triunfa
/// del lado de la persona y queda pendiente, que el próximo lanzamiento vacía.
/// <para>
/// El destinatario se siembra por SQL: lo que hace falta es una fila con su token de baja, no
/// toda la maquinaria de una campaña (ver <see cref="UnsubscribeTestClient"/>).
/// </para>
/// </summary>
public sealed class PublicUnsubscribeTests : IClassFixture<RegistrationPortalFactory>
{
    private readonly RegistrationPortalFactory _factory;

    public PublicUnsubscribeTests(RegistrationPortalFactory factory) => _factory = factory;

    /// <summary>
    /// El clic da de baja: se enmascara la dirección, se empuja el opt-out al contacto y la fila
    /// queda sincronizada. La respuesta nombra a quién dio de baja, enmascarado, para que quien
    /// recibió un reenvío vea una dirección que no es la suya (§2.4).
    /// </summary>
    [Fact]
    public async Task Unsubscribe_ReturnsMaskedAddressAndSyncsToCrm_WhenCrmIsUp()
    {
        _factory.Crm.ServeDefaults();
        var seeded = await UnsubscribeTestClient.InviteAsync(_factory, email: "invitada@acme.com");

        var response = await UnsubscribeTestClient.UnsubscribeAsync(_factory.CreateClient(), seeded.Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("maskedEmail").GetString().ShouldBe("i***@acme.com");

        _factory.Crm.OptedOutContactIds.ShouldContain(seeded.ContactId);

        var state = await UnsubscribeTestClient.StateAsync(_factory, seeded.RecipientId);
        state.UnsubscribedAt.ShouldNotBeNull();
        state.SyncedAt.ShouldNotBeNull();
    }

    /// <summary>
    /// Un token que no casa ninguna fila sale por un solo <c>404</c>, sin decir más: no hay nada
    /// más que un token pueda fallar acá.
    /// </summary>
    [Fact]
    public async Task Unsubscribe_ReturnsNotFound_WhenTheTokenIsUnknown()
    {
        _factory.Crm.ServeDefaults();

        var response = await UnsubscribeTestClient.UnsubscribeAsync(
            _factory.CreateClient(), "no-es-un-token-de-nadie");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// El CRM no está, y la baja <b>triunfa igual</b> del lado de la persona: <c>200</c> con la
    /// dirección enmascarada, y la fila marcada como pendiente —clic sí, sello no— que el próximo
    /// lanzamiento vacía (§6, espeja §4.3). Un <c>503</c> en un portal público por un servicio del
    /// que el desconocido nunca oyó es el error que esta degradación existe para no cometer.
    /// </summary>
    [Fact]
    public async Task Unsubscribe_MarksThePendingRowButStillSucceeds_WhenCrmIsDown()
    {
        var seeded = await UnsubscribeTestClient.InviteAsync(_factory, email: "invitada@acme.com");
        _factory.Crm.GoDown();

        var response = await UnsubscribeTestClient.UnsubscribeAsync(_factory.CreateClient(), seeded.Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("maskedEmail").GetString().ShouldBe("i***@acme.com");

        var state = await UnsubscribeTestClient.StateAsync(_factory, seeded.RecipientId);
        state.UnsubscribedAt.ShouldNotBeNull();
        state.SyncedAt.ShouldBeNull();
    }

    /// <summary>
    /// El segundo clic es idempotente: la misma dirección enmascarada, y <b>no vuelve a hablar con
    /// el CRM ni a resellar</b>. En fase 1 nada limpia una baja, así que una segunda visita sobre
    /// una fila ya sincronizada no tiene nada que empujar.
    /// </summary>
    [Fact]
    public async Task Unsubscribe_ReturnsTheSameAnswerWithoutCallingCrm_WhenClickedASecondTime()
    {
        _factory.Crm.ServeDefaults();
        var seeded = await UnsubscribeTestClient.InviteAsync(_factory, email: "invitada@acme.com");
        var client = _factory.CreateClient();

        var first = await UnsubscribeTestClient.UnsubscribeAsync(client, seeded.Token);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        _factory.Crm.ResetRequestLog();
        var second = await UnsubscribeTestClient.UnsubscribeAsync(client, seeded.Token);

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await second.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("maskedEmail").GetString().ShouldBe("i***@acme.com");
        _factory.Crm.RequestCount.ShouldBe(0);
    }

    /// <summary>
    /// Ninguna línea de log nombra el token, ni siquiera cuando el CRM falla y algo se registra:
    /// es la misma regla que en el registro, el token no puede filtrarse ni por un log (§2.1).
    /// </summary>
    [Fact]
    public async Task Unsubscribe_ReturnsNoLogLineWithTheToken_WhenCrmFails()
    {
        var seeded = await UnsubscribeTestClient.InviteAsync(_factory, email: "invitada@acme.com");
        _factory.Crm.GoDown();

        await UnsubscribeTestClient.UnsubscribeAsync(_factory.CreateClient(), seeded.Token);

        _factory.LoggedLines.ShouldNotBeEmpty();
        _factory.LoggedLines.ShouldAllBe(line => !line.Contains(seeded.Token, StringComparison.Ordinal));
    }
}
