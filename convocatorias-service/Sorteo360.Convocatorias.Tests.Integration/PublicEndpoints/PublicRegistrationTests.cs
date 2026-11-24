using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.PublicEndpoints;

/// <summary>
/// El portal de registro (spec <c>06</c> §1, §1.1, §2, §4): un desconocido abre un enlace, manda
/// el formulario, y le vuelven un código impreso y un QR. Nadie inicia sesión.
/// <para>
/// Se ejercita la cadena entera contra un events-service simulado por HTTP y un PostgreSQL de
/// verdad: lo que hay que cobrar es la unicidad de la base y el recorte del evento, y ninguna de
/// las dos cosas la puede fallar un doble.
/// </para>
/// <para>
/// <b>Cada test sirve su propio evento</b>, con id propio. La base vive toda la clase, así que
/// compartir un evento haría que la inscripción de un test fuera el "ya existe" del siguiente.
/// </para>
/// </summary>
public sealed class PublicRegistrationTests : IClassFixture<RegistrationPortalFactory>
{
    private readonly RegistrationPortalFactory _factory;

    public PublicRegistrationTests(RegistrationPortalFactory factory) => _factory = factory;

    #region Resolver un enlace

    [Fact]
    public async Task Resolve_ReturnsTheEvent_WhenTheCallerIsAnonymous()
    {
        var (publicEvent, invitation) = await PublicInvitationAsync();
        var client = _factory.CreateClient();

        var response = await RegistrationTestClient.ResolveAsync(client, invitation.Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await BodyAsync(response)).GetProperty("event").GetProperty("id").GetGuid().ShouldBe(publicEvent);
    }

    /// <summary>
    /// El evento que sale por el portal es el mismo recorte de la cartelera: ni presupuesto, ni
    /// costo, ni el aforo interno de la OPT. Que viajen desde events-service hasta acá es lo que
    /// hace que esto pruebe algo.
    /// </summary>
    [Fact]
    public async Task Resolve_ReturnsNoInternalNumbers_WhenItServes()
    {
        var (_, invitation) = await PublicInvitationAsync();
        var client = _factory.CreateClient();

        var body = await BodyAsync(await RegistrationTestClient.ResolveAsync(client, invitation.Token));
        var served = body.GetProperty("event");

        served.TryGetProperty("invitedCount", out _).ShouldBeFalse();
        served.TryGetProperty("estimatedBudget", out _).ShouldBeFalse();
        served.TryGetProperty("totalCost", out _).ShouldBeFalse();
    }

    /// <summary>
    /// Un token privado está atado a una persona y llegó a su dirección, así que devolverle su
    /// nombre no le enseña nada que no supiera (spec <c>06</c> §2.2).
    /// </summary>
    [Fact]
    public async Task Resolve_ReturnsTheInviteePrefilled_WhenTheTokenIsPrivate()
    {
        var (_, invitation) = await PrivateInvitationAsync();
        var client = _factory.CreateClient();

        var body = await BodyAsync(await RegistrationTestClient.ResolveAsync(client, invitation.Token));
        var prefill = body.GetProperty("prefill");

        prefill.GetProperty("firstName").GetString().ShouldBe(invitation.FirstName);
        prefill.GetProperty("lastName").GetString().ShouldBe(invitation.LastName);
        prefill.GetProperty("email").GetString().ShouldBe(invitation.Email);
    }

    /// <summary>
    /// Un token público está pensado para reenviarse, así que rellenar sobre él le enseñaría a
    /// quien recibe el reenvío el nombre y la dirección del invitado original.
    /// </summary>
    [Fact]
    public async Task Resolve_ReturnsANullPrefill_WhenTheTokenIsPublic()
    {
        var (_, invitation) = await PublicInvitationAsync();
        var client = _factory.CreateClient();

        var body = await BodyAsync(await RegistrationTestClient.ResolveAsync(client, invitation.Token));

        body.GetProperty("prefill").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    /// <summary>Un token público admite cuantas resoluciones haga falta: se reenvía.</summary>
    [Fact]
    public async Task Resolve_Returns200EveryTime_WhenAPublicTokenIsResolvedRepeatedly()
    {
        var (_, invitation) = await PublicInvitationAsync();
        var client = _factory.CreateClient();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            (await RegistrationTestClient.ResolveAsync(client, invitation.Token)).StatusCode
                .ShouldBe(HttpStatusCode.OK);
        }
    }

    /// <summary>
    /// Uno privado admite exactamente una inscripción. Gastado sale por el mismo 404 que uno
    /// inventado: decir "ya te inscribiste" sería la única respuesta del portal que confirma que
    /// una dirección concreta está invitada.
    /// </summary>
    [Fact]
    public async Task Resolve_Returns404WithItsCode_WhenThePrivateTokenIsSpent()
    {
        var (privateEvent, invitation) = await PrivateInvitationAsync();
        var client = _factory.CreateClient();

        (await RegistrationTestClient.RegisterAsync(
            client, privateEvent, invitation.Token, email: Unique())).StatusCode
            .ShouldBe(HttpStatusCode.Created);

        await ShouldBeRefusedAsync(await RegistrationTestClient.ResolveAsync(client, invitation.Token));
    }

    [Fact]
    public async Task Resolve_Returns404WithItsCode_WhenTheTokenIsUnknown()
    {
        _factory.Events.ServeEvents(PublicEvent(Guid.NewGuid()));
        var client = _factory.CreateClient();

        await ShouldBeRefusedAsync(
            await RegistrationTestClient.ResolveAsync(client, "no-es-un-token-de-nadie"));
    }

    #endregion

    #region Inscribirse

    [Fact]
    public async Task Register_Returns201WithTheTwoCodesAndTheEvent_WhenTheTokenIsValid()
    {
        var (publicEvent, invitation) = await PublicInvitationAsync();
        var client = _factory.CreateClient();

        var response = await RegistrationTestClient.RegisterAsync(
            client, publicEvent, invitation.Token, email: Unique());

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await BodyAsync(response);
        body.GetProperty("reference").GetString().ShouldStartWith($"REF-{DateTime.UtcNow.Year}-");
        body.GetProperty("qrCode").GetString()!.Length.ShouldBe(43);
        body.GetProperty("event").GetProperty("id").GetGuid().ShouldBe(publicEvent);
    }

    /// <summary>
    /// El token de un evento público se reenvía, y cada inscripción anota a quién se lo
    /// mandamos: es lo que vuelve representable un reenvío en vez de dejarlo como un misterio.
    /// </summary>
    [Fact]
    public async Task Register_ReturnsRowsCarryingTheReferringRecipient_WhenTheTokenIsPublic()
    {
        var (publicEvent, invitation) = await PublicInvitationAsync();
        var client = _factory.CreateClient();
        var forwardee = Unique();
        var another = Unique();

        (await RegistrationTestClient.RegisterAsync(client, publicEvent, invitation.Token, email: forwardee))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
        (await RegistrationTestClient.RegisterAsync(client, publicEvent, invitation.Token, email: another))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        (await RegistrationTestClient.ReferringRecipientAsync(_factory, publicEvent, forwardee))
            .ShouldBe(invitation.RecipientId);
        (await RegistrationTestClient.ReferringRecipientAsync(_factory, publicEvent, another))
            .ShouldBe(invitation.RecipientId);
    }

    /// <summary>
    /// Quien llega desde la cartelera no recibió ningún correo. Es la mitad del camino que §1.1
    /// se negó a partir en dos endpoints, y deja <c>campaign_recipient_id</c> en nulo.
    /// </summary>
    [Fact]
    public async Task Register_Returns201WithNoReferringRecipient_WhenThereIsNoTokenOnAPublicEvent()
    {
        var walkIn = Guid.NewGuid();
        _factory.Events.ServeEvents(PublicEvent(walkIn));
        var client = _factory.CreateClient();
        var email = Unique();

        var response = await RegistrationTestClient.RegisterAsync(client, walkIn, token: null, email: email);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await RegistrationTestClient.ReferringRecipientAsync(_factory, walkIn, email)).ShouldBeNull();
    }

    /// <summary>
    /// A un evento privado no se entra sin invitación: su lista es lo único que lo hace privado.
    /// </summary>
    [Fact]
    public async Task Register_Returns404WithItsCode_WhenThereIsNoTokenOnAPrivateEvent()
    {
        var hidden = Guid.NewGuid();
        _factory.Events.ServeEvents(PrivateEvent(hidden));
        var client = _factory.CreateClient();

        await ShouldBeRefusedAsync(
            await RegistrationTestClient.RegisterAsync(client, hidden, token: null, email: Unique()));
    }

    /// <summary>Un token privado se gasta con la primera inscripción (spec <c>06</c> §2.2).</summary>
    [Fact]
    public async Task Register_Returns404WithItsCode_WhenAPrivateTokenIsUsedTwice()
    {
        var (privateEvent, invitation) = await PrivateInvitationAsync();
        var client = _factory.CreateClient();

        (await RegistrationTestClient.RegisterAsync(client, privateEvent, invitation.Token, email: Unique()))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        await ShouldBeRefusedAsync(
            await RegistrationTestClient.RegisterAsync(client, privateEvent, invitation.Token, email: Unique()));
    }

    /// <summary>
    /// Un token vale para su evento y para ningún otro. Sin esta comprobación, el token de
    /// cualquier evento público abriría la puerta de todos los privados.
    /// </summary>
    [Fact]
    public async Task Register_Returns404WithItsCode_WhenTheTokenBelongsToAnotherEvent()
    {
        var (_, invitation) = await PublicInvitationAsync();
        var elsewhere = Guid.NewGuid();
        _factory.Events.ServeEvents(PrivateEvent(elsewhere));
        var client = _factory.CreateClient();

        await ShouldBeRefusedAsync(
            await RegistrationTestClient.RegisterAsync(client, elsewhere, invitation.Token, email: Unique()));
    }

    #endregion

    #region La misma persona dos veces

    /// <summary>
    /// Un reenvío del formulario es lo que pasa cuando alguien perdió el correo, no un error:
    /// devuelve la inscripción que ya había con sus mismos códigos, no crea fila y no pisa nada
    /// (spec <c>06</c> §4.1).
    /// </summary>
    [Fact]
    public async Task Register_Returns200WithTheFirstRegistration_WhenTheSamePersonRegistersTwice()
    {
        var repeat = Guid.NewGuid();
        _factory.Events.ServeEvents(PublicEvent(repeat));
        var client = _factory.CreateClient();
        var email = Unique();

        var first = await BodyAsync(await RegistrationTestClient.RegisterAsync(
            client, repeat, email: email, firstName: "Juan"));

        var second = await RegistrationTestClient.RegisterAsync(
            client, repeat, email: email, firstName: "J.");

        second.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await BodyAsync(second);
        body.GetProperty("reference").GetString().ShouldBe(first.GetProperty("reference").GetString());
        body.GetProperty("qrCode").GetString().ShouldBe(first.GetProperty("qrCode").GetString());

        (await RegistrationTestClient.CountAsync(_factory, repeat)).ShouldBe(1);
        (await RegistrationTestClient.FirstNameAsync(_factory, repeat, email)).ShouldBe("Juan");
    }

    /// <summary>
    /// <b>La invitada de un evento privado puede reenviar su propio formulario.</b> Su token se
    /// gastó con la primera inscripción, así que la comprobación de "ya se usó" tiene que correr
    /// <b>después</b> de leer si esa dirección ya está inscrita; al revés, el caso que §4.1
    /// contesta con un 200 sería un 404 para la única persona que de verdad perdió el correo.
    /// </summary>
    [Fact]
    public async Task Register_Returns200WithTheFirstRegistration_WhenAPrivateInviteeRegistersTwice()
    {
        var (privateEvent, invitation) = await PrivateInvitationAsync();
        var client = _factory.CreateClient();
        var email = Unique();

        var first = await BodyAsync(await RegistrationTestClient.RegisterAsync(
            client, privateEvent, invitation.Token, email: email, firstName: "Ana"));

        var second = await RegistrationTestClient.RegisterAsync(
            client, privateEvent, invitation.Token, email: email, firstName: "A.");

        second.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await BodyAsync(second);
        body.GetProperty("reference").GetString().ShouldBe(first.GetProperty("reference").GetString());

        (await RegistrationTestClient.CountAsync(_factory, privateEvent)).ShouldBe(1);
        (await RegistrationTestClient.FirstNameAsync(_factory, privateEvent, email)).ShouldBe("Ana");
    }

    /// <summary>
    /// La unicidad es sobre la dirección normalizada, así que una mayúscula no cuela a la misma
    /// persona una segunda vez.
    /// </summary>
    [Fact]
    public async Task Register_Returns200AndNoNewRow_WhenTheAddressDiffersOnlyInCase()
    {
        var repeat = Guid.NewGuid();
        _factory.Events.ServeEvents(PublicEvent(repeat));
        var client = _factory.CreateClient();
        var email = Unique();

        await RegistrationTestClient.RegisterAsync(client, repeat, email: email);

        var second = await RegistrationTestClient.RegisterAsync(client, repeat, email: email.ToUpperInvariant());

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RegistrationTestClient.CountAsync(_factory, repeat)).ShouldBe(1);
    }

    /// <summary>La misma dirección en otro evento es otra persona a efectos de la regla.</summary>
    [Fact]
    public async Task Register_Returns201WithANewRow_WhenTheAddressIsOnAnotherEvent()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        _factory.Events.ServeEvents(PublicEvent(first), PublicEvent(second));
        var client = _factory.CreateClient();
        var email = Unique();

        (await RegistrationTestClient.RegisterAsync(client, first, email: email)).StatusCode
            .ShouldBe(HttpStatusCode.Created);
        (await RegistrationTestClient.RegisterAsync(client, second, email: email)).StatusCode
            .ShouldBe(HttpStatusCode.Created);

        (await RegistrationTestClient.CountAsync(_factory, first)).ShouldBe(1);
        (await RegistrationTestClient.CountAsync(_factory, second)).ShouldBe(1);
    }

    #endregion

    #region El token no se guarda ni se registra

    /// <summary>
    /// Un volcado de la base no entrega ni un enlace que funcione: la tabla guarda el hash y
    /// nunca el texto plano (spec <c>06</c> §2.1).
    /// </summary>
    [Fact]
    public async Task Register_ReturnsNoPlaintextTokenInTheTable_WhenARegistrationIsStored()
    {
        var (publicEvent, invitation) = await PublicInvitationAsync();
        var client = _factory.CreateClient();

        await RegistrationTestClient.RegisterAsync(client, publicEvent, invitation.Token, email: Unique());

        var stored = await RegistrationTestClient.EveryTextValueAsync(_factory);

        stored.ShouldNotBeEmpty();
        stored.ShouldNotContain(invitation.Token);
    }

    /// <summary>
    /// Ni una línea de log lo nombra. Es la otra mitad de la misma regla: el token no puede
    /// quedar escrito en disco en ningún sitio, y un log es disco.
    /// </summary>
    [Fact]
    public async Task Log_ReturnsNoLineWithTheToken_WhenResolvingAndRegistering()
    {
        var (publicEvent, invitation) = await PublicInvitationAsync();
        var client = _factory.CreateClient();

        await RegistrationTestClient.ResolveAsync(client, invitation.Token);
        await RegistrationTestClient.RegisterAsync(client, publicEvent, invitation.Token, email: Unique());
        await RegistrationTestClient.ResolveAsync(client, "un-token-que-no-existe");

        // Y los dos caminos de fallo, que es donde de verdad se escapa un valor: un rechazo del
        // portal, y un 400 del validador sobre un cuerpo que sí llevaba el token.
        await RegistrationTestClient.ResolveAsync(client, invitation.Token + "-roto");
        (await RegistrationTestClient.RegisterAsync(
            client, publicEvent, invitation.Token, email: "no-es-una-direccion")).StatusCode
            .ShouldBe(HttpStatusCode.BadRequest);

        // Que haya líneas es parte de la afirmación: sin esto el test pasaría por no haber
        // mirado nada.
        _factory.LoggedLines.ShouldNotBeEmpty();
        _factory.LoggedLines.ShouldAllBe(line => !line.Contains(invitation.Token, StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>Resolver es un <c>POST</c> y no un <c>GET</c></b>, que es toda la razón por la que el
    /// token no llega a ningún registro de acceso: nginx y Traefik anotan la línea de petición
    /// entera. Un <c>GET</c> con el token en la consulta lo dejaría escrito en los dos.
    /// </summary>
    [Fact]
    public async Task Resolve_Returns405_WhenCalledOverGet()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(
            $"{RegistrationTestClient.ResolveRoute}?token=deberia-no-existir");

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    }

    #endregion

    #region Eventos cancelados, cerrados y terminados

    /// <summary>
    /// Un evento cancelado desaparece de la cartelera, su detalle es 404 y no admite a nadie
    /// más (spec <c>06</c> §4.6).
    /// </summary>
    [Fact]
    public async Task PublicPortal_Returns404OnEverySurface_WhenTheEventIsCancelled()
    {
        var cancelled = Guid.NewGuid();
        var invitation = await InviteToAsync(cancelled, PublicEvent(cancelled) with { Status = StubEventStatus.Cancelled });
        var client = _factory.CreateClient();

        var listed = await client.GetFromJsonAsync<JsonElement>("/v1/public/events");
        listed.EnumerateArray().ShouldBeEmpty();

        (await client.GetAsync($"/v1/public/events/{cancelled}")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);

        await ShouldBeRefusedAsync(await RegistrationTestClient.ResolveAsync(client, invitation.Token));
        await ShouldBeRefusedAsync(
            await RegistrationTestClient.RegisterAsync(client, cancelled, invitation.Token, email: Unique()));
    }

    /// <summary>
    /// Un evento cerrado sigue sirviendo su detalle —es historia pública y un enlace compartido
    /// la semana pasada tiene que resolver— pero no admite a nadie más.
    /// <para>
    /// <b>Se cierra antes de su fecha de fin</b> a propósito: así lo que rechaza es el estado y
    /// no el calendario, que es la trampa contra la que avisa §4.6.
    /// </para>
    /// </summary>
    [Fact]
    public async Task PublicPortal_ReturnsTheDetailButRefusesRegistration_WhenTheEventIsClosed()
    {
        var closed = Guid.NewGuid();
        var invitation = await InviteToAsync(
            closed,
            PublicEvent(closed) with { Status = StubEventStatus.Closed, DaysUntilStart = 5 });
        var client = _factory.CreateClient();

        (await client.GetAsync($"/v1/public/events/{closed}")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var refusedResolve = await RegistrationTestClient.ResolveAsync(client, invitation.Token);
        var refusedRegister = await RegistrationTestClient.RegisterAsync(
            client, closed, invitation.Token, email: Unique());

        // El mismo código en las dos, que es lo que el portal necesita para pintar una sola
        // pantalla de "por acá no se entra".
        await ShouldBeRefusedAsync(refusedResolve);
        await ShouldBeRefusedAsync(refusedRegister);
    }

    /// <summary>
    /// Un token no caduca por su cuenta, pero deja de valer cuando el evento ya pasó
    /// (spec <c>06</c> §2.1). Acá el estado sigue siendo uno vivo: lo que rechaza es la fecha.
    /// </summary>
    [Fact]
    public async Task PublicPortal_Returns404WithItsCode_WhenTheEventIsPastItsEndDate()
    {
        var over = Guid.NewGuid();
        var invitation = await InviteToAsync(over, PublicEvent(over) with { DaysUntilStart = -30 });
        var client = _factory.CreateClient();

        await ShouldBeRefusedAsync(await RegistrationTestClient.ResolveAsync(client, invitation.Token));
        await ShouldBeRefusedAsync(
            await RegistrationTestClient.RegisterAsync(client, over, invitation.Token, email: Unique()));
    }

    #endregion

    #region Helpers

    /// <summary>
    /// El rechazo del portal: siempre <c>404</c> y siempre el mismo código, venga de donde
    /// venga. Es lo que impide que alguien deduzca del portal qué direcciones están invitadas.
    /// </summary>
    private static async Task ShouldBeRefusedAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().ShouldBe(PublicRegistrationNotFoundError.ErrorCode);
    }

    private async Task<(Guid EventId, SeededInvitation Invitation)> PublicInvitationAsync()
    {
        var eventId = Guid.NewGuid();
        return (eventId, await InviteToAsync(eventId, PublicEvent(eventId)));
    }

    private async Task<(Guid EventId, SeededInvitation Invitation)> PrivateInvitationAsync()
    {
        var eventId = Guid.NewGuid();
        return (eventId, await InviteToAsync(eventId, PrivateEvent(eventId)));
    }

    private async Task<SeededInvitation> InviteToAsync(Guid eventId, StubEvent served)
    {
        _factory.Events.ServeEvents(served);

        return await RegistrationTestClient.InviteAsync(_factory, eventId, email: Unique());
    }

    private static StubEvent PublicEvent(Guid id) => new(
        id, "Roadshow Madrid 2026", EventsStub.PublicVisibility, "es", StubEventStatus.InProgress);

    private static StubEvent PrivateEvent(Guid id) => new(
        id, "Gala privada", EventsStub.PrivateVisibility, "es", StubEventStatus.InProgress);

    /// <summary>
    /// Una dirección que no ha usado ningún otro test. La base vive toda la clase y la unicidad
    /// es de la base, así que una dirección compartida convertiría la inscripción de un test en
    /// el "ya existe" del siguiente.
    /// </summary>
    private static string Unique() =>
        EmailAddressShape.Normalize($"p{Guid.NewGuid():N}@acme.com");

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    #endregion
}
