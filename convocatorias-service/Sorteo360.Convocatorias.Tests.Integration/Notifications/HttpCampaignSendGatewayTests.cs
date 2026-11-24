using System.Net;
using System.Text.Json;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Tests.Integration.CampaignEndpoints;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Sorteo360.Convocatorias.Web.CampaignEndpoints;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.Notifications;

/// <summary>
/// El transporte de verdad, de punta a punta: se lanza una campaña y se mira el JSON que salió al
/// cable contra lo que fija la spec <c>04</c> §2.
/// <para>
/// <b>Acá no se dobla el cliente.</b> Detrás de la costura está el <c>HttpCampaignSendGateway</c>
/// de producción y enfrente un notification-service simulado que habla HTTP, porque lo que se
/// cobra es el contrato: el cuerpo, el <c>about</c>, el emparejado de la respuesta y qué deja en
/// las filas cada desenlace.
/// </para>
/// </summary>
public sealed class HttpCampaignSendGatewayTests : IClassFixture<HttpNotificationFactory>
{
    private static readonly StubContact Ana =
        new(Guid.NewGuid(), "Ana", "Pérez", "ana.wire@acme.com", CanBeEmailed: true, SuppressionReason: null);

    private static readonly StubContact Luis =
        new(Guid.NewGuid(), "Luis", "Gómez", "luis.wire@acme.com", CanBeEmailed: true, SuppressionReason: null);

    private readonly HttpNotificationFactory _factory;

    public HttpCampaignSendGatewayTests(HttpNotificationFactory factory)
    {
        _factory = factory;
        _factory.Crm.ServeDefaults();
        _factory.Notifications.ServeDefaults();
    }

    /// <summary>
    /// Los ocho campos de la spec <c>04</c> §2, con las dos bolsas separadas: el nombre del evento
    /// una vez para todo el lote y lo de cada persona en la suya. Es lo que mantiene ese nombre
    /// fuera del cable mil veces.
    /// </summary>
    [Fact]
    public async Task SendAsync_PostsTheWireContract_WhenItHandsOffABatch()
    {
        _factory.Crm.ServeContacts(Ana, Luis);

        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAndLaunchAsync(client, EventsStub.EventId);

        await CampaignTestClient.WaitUntilSentAsync(client, campaign.Id);

        var batch = SingleBatch();

        batch.GetProperty("type").GetString().ShouldBe(CampaignNotification.Type);
        batch.GetProperty("language").GetString().ShouldBe(EventsStub.EventLanguage);

        // Quién firma: sin fromAddress el emisor contesta 400 a cada lote.
        batch.GetProperty("fromAddress").GetString().ShouldBe(ServiceIdentityStub.FromAddress);
        batch.GetProperty("fromName").GetString().ShouldBe(ServiceIdentityStub.FromName);

        // El asunto y el cuerpo viajan con sus marcadores intactos: sustituir es trabajo del lado
        // que recibe (§2).
        batch.GetProperty("subject").GetString().ShouldNotBeNull().ShouldContain("{{");
        batch.GetProperty("body").GetString().ShouldNotBeNull().ShouldContain("{{");

        // La bolsa compartida lleva los tres valores del evento, y solo esos.
        var shared = batch.GetProperty("sharedData");
        shared.GetProperty("evento").GetString().ShouldBe(EventsStub.EventName);
        shared.TryGetProperty("nombre", out _).ShouldBeFalse();

        var recipients = batch.GetProperty("recipients").EnumerateArray().ToList();
        recipients.Count.ShouldBe(2);

        var ana = recipients.Single(recipient => recipient.GetProperty("email").GetString() == Ana.Email);
        var data = ana.GetProperty("data");

        // El nombre completo va aparte del marcador: es el de la cabecera To y el del historial.
        ana.GetProperty("name").GetString().ShouldBe("Ana Pérez");

        data.GetProperty("nombre").GetString().ShouldBe(Ana.FirstName);
        data.GetProperty("enlace_registro").GetString()
            .ShouldStartWith(ServiceIdentityStub.PortalBaseUrl + "/publico/registro#t=");
        data.GetProperty("enlace_baja").GetString().ShouldNotBeNullOrWhiteSpace();

        // Y nada del evento repetido en la bolsa de una persona: eso sería la clave duplicada que
        // el contrato rechaza.
        data.TryGetProperty("evento", out _).ShouldBeFalse();

        // Sobre qué fue este correo: la campaña que lo mandó y el evento que convoca.
        var about = batch.GetProperty("about").EnumerateArray().ToList();
        about.Count.ShouldBe(2);
        about.ShouldAllBe(entry => entry.GetProperty("sourceService").GetString() == "convocatorias");

        about.Single(entry => entry.GetProperty("entityType").GetString() == "campaign")
            .GetProperty("entityId").GetString().ShouldBe(campaign.Id.ToString());
        about.Single(entry => entry.GetProperty("entityType").GetString() == "event")
            .GetProperty("entityId").GetString().ShouldBe(EventsStub.EventId.ToString());

        batch.GetProperty("idempotencyKey").GetString().ShouldNotBeNull()
            .ShouldStartWith($"convocatorias:campaign:{campaign.Id}:batch:");
    }

    /// <summary>
    /// La campaña que lanza alguien de una OPT sale desde la dirección de envío de esa OPT, y con
    /// el nombre visible de siempre (PGTI-168).
    /// </summary>
    [Fact]
    public async Task SendAsync_PostsTheOptSenderEmailAsFromAddress_WhenTheLauncherOptHasOne()
    {
        _factory.Crm.ServeContacts(Ana);
        _factory.Crm.ServeOptDirectory("madrid", "campanas.madrid@sorteo360.local");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.OptCodeHeader, "madrid");

        var campaign = await CampaignTestClient.CreateAndLaunchAsync(client, EventsStub.EventId);

        await CampaignTestClient.WaitUntilSentAsync(client, campaign.Id);

        var batch = SingleBatch();

        batch.GetProperty("fromAddress").GetString().ShouldBe("campanas.madrid@sorteo360.local");
        batch.GetProperty("fromName").GetString().ShouldBe(ServiceIdentityStub.FromName);
    }

    /// <summary>
    /// Un mensaje directo lleva el asunto y el texto en la bolsa compartida, a cada persona sin
    /// enlace de registro, y en <c>about</c> solo la campaña: no convoca a ningún evento (PGTI-174).
    /// </summary>
    [Fact]
    public async Task SendAsync_PostsTheCampaignAloneInAbout_WhenTheCampaignIsADirectMessage()
    {
        _factory.Crm.ServeContacts(Ana);

        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateDirectMessageAsync(client, subject: "Novedades", message: "Texto");

        (await CampaignTestClient.SelectAsync(client, campaign.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await CampaignTestClient.LaunchReadyAsync(client, campaign.Id);
        await CampaignTestClient.WaitUntilSentAsync(client, campaign.Id);

        var batch = SingleBatch();

        batch.GetProperty("subject").GetString().ShouldBe("{{asunto}}");

        var shared = batch.GetProperty("sharedData");
        shared.GetProperty("asunto").GetString().ShouldBe("Novedades");
        shared.GetProperty("mensaje").GetString().ShouldBe("Texto");
        shared.TryGetProperty("evento", out _).ShouldBeFalse();

        var data = batch.GetProperty("recipients").EnumerateArray().Single().GetProperty("data");
        data.TryGetProperty("enlace_registro", out _).ShouldBeFalse();
        data.GetProperty("enlace_baja").GetString().ShouldNotBeNullOrWhiteSpace();

        var about = batch.GetProperty("about").EnumerateArray().Single();
        about.GetProperty("entityType").GetString().ShouldBe("campaign");
        about.GetProperty("entityId").GetString().ShouldBe(campaign.Id.ToString());
    }

    /// <summary>
    /// <b>La clave nombra el envío.</b> Los reintentos del chasis contra un <c>503</c> mandan el
    /// mismo lote y llevan la misma clave, así que un intento que sí aterrizó no se escribe dos
    /// veces. Reanudar vuelve a acuñar los tokens, que es otro envío con otro cuerpo, y lleva otra:
    /// con la misma, el emisor contestaría <c>409</c> y la campaña no se cerraría nunca.
    /// </summary>
    [Fact]
    public async Task ResumeHandoff_ReturnsANewKey_WhenTheRetriedBatchReusedItsOwn()
    {
        _factory.Crm.ServeContacts(Ana, Luis);
        _factory.Notifications.ServeUnavailableFromCall(1);

        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAndLaunchAsync(client, EventsStub.EventId);

        await WaitUntilNoBatchIsInFlightAsync();

        var retried = KeysPosted();
        retried.Count.ShouldBeGreaterThan(1, "el chasis tenía que reintentar el 503");
        retried.Distinct().ShouldHaveSingleItem();

        _factory.Notifications.ServeBatchAccepted();

        (await CampaignTestClient.ResumeHandoffAsync(client, campaign.Id)).StatusCode
            .ShouldBe(HttpStatusCode.Accepted);

        await CampaignTestClient.WaitUntilSentAsync(client, campaign.Id);

        KeysPosted().Last().ShouldNotBe(retried[0]);
    }

    /// <summary>
    /// Un <c>rejected</c> por persona deja esa fila <c>failed</c> con el motivo que mandó el
    /// emisor, el resto <c>handed_off</c>, y la campaña llega igual a <c>sent</c>: lo que la
    /// cierra es que no le quede ninguna <c>pending</c>.
    /// </summary>
    [Fact]
    public async Task SendAsync_MapsEachOutcomeOntoItsRow_WhenTheBatchRejectsOneRecipient()
    {
        _factory.Crm.ServeContacts(Ana, Luis);
        _factory.Notifications.Reject(Ana.Email);

        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAndLaunchAsync(client, EventsStub.EventId);

        await CampaignTestClient.WaitUntilSentAsync(client, campaign.Id);

        (await CampaignTestClient.RecipientSendStatusAsync(_factory, campaign.Id, Ana.Id))
            .ShouldBe(RecipientSendStatus.Failed);
        (await CampaignTestClient.RecipientFailureReasonAsync(_factory, campaign.Id, Ana.Id))
            .ShouldBe(NotificationStub.InvalidAddressReason);

        (await CampaignTestClient.RecipientSendStatusAsync(_factory, campaign.Id, Luis.Id))
            .ShouldBe(RecipientSendStatus.HandedOff);
    }

    /// <summary>
    /// <b>La distinción de la que vive el cursor de reanudación.</b> Antes del <c>202</c> no
    /// existe historia del otro lado, así que una petición que se cae entera no anota a nadie: las
    /// filas se quedan <c>pending</c> y la campaña en <c>sending</c>, esperando a una persona
    /// (§2.1 y §5).
    /// </summary>
    [Fact]
    public async Task SendAsync_RecordsNothingAndLeavesTheCampaignSending_WhenTheWholeRequestFails()
    {
        _factory.Crm.ServeContacts(Ana, Luis);
        _factory.Notifications.ServeBadRequest("NOTIFICATION_BATCH_TOO_LARGE");

        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAndLaunchAsync(client, EventsStub.EventId);

        await WaitUntilNoBatchIsInFlightAsync();

        (await CampaignTestClient.GetAsync(client, campaign.Id)).State.ShouldBe(CampaignState.Sending);

        (await CampaignTestClient.RecipientStatusesAsync(_factory, campaign.Id))
            .ShouldAllBe(status => status == RecipientSendStatus.Pending);
    }

    /// <summary>
    /// El emisor contesta <c>503</c>: nadie se anota, la campaña se queda <c>sending</c>, y
    /// <c>resume-handoff</c> vuelve a mandar esa cola entera hasta cerrarla. Es la diferencia
    /// entre un lote que se cayó y una persona a la que el emisor rechazó: lo primero deja la fila
    /// intacta, lo segundo es terminal (§2.1 y §5).
    /// </summary>
    [Fact]
    public async Task SendAsync_LeavesTheTailForAResume_WhenTheSenderAnswers503()
    {
        _factory.Crm.ServeContacts(Ana, Luis);
        _factory.Notifications.ServeUnavailableFromCall(1);

        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAndLaunchAsync(client, EventsStub.EventId);

        await WaitUntilNoBatchIsInFlightAsync();

        (await CampaignTestClient.GetAsync(client, campaign.Id)).State.ShouldBe(CampaignState.Sending);
        (await CampaignTestClient.RecipientStatusesAsync(_factory, campaign.Id))
            .ShouldAllBe(status => status == RecipientSendStatus.Pending);

        // El emisor vuelve en sí y la persona aprieta el botón.
        _factory.Notifications.ServeBatchAccepted();

        (await CampaignTestClient.ResumeHandoffAsync(client, campaign.Id)).StatusCode
            .ShouldBe(HttpStatusCode.Accepted);

        await CampaignTestClient.WaitUntilSentAsync(client, campaign.Id);

        (await CampaignTestClient.RecipientStatusesAsync(_factory, campaign.Id))
            .ShouldAllBe(status => status == RecipientSendStatus.HandedOff);
    }

    /// <summary>La clave de cada lote que llegó en este test, en orden.</summary>
    private List<string?> KeysPosted() =>
    [
        .. _factory.Notifications.PostedBatches.Select(body =>
            JsonDocument.Parse(body).RootElement.GetProperty("idempotencyKey").GetString()),
    ];

    /// <summary>El único lote de este test, ya leído como JSON.</summary>
    private JsonElement SingleBatch()
    {
        var batches = _factory.Notifications.PostedBatches;

        batches.Count.ShouldBe(1);

        return JsonDocument.Parse(batches[0]).RootElement.Clone();
    }

    /// <summary>
    /// Espera a que el worker deje de llamar al vecino. Afirmar "no se anotó nada" enseguida sería
    /// una carrera contra él, y el lote que falla pasa además por los reintentos del chasis.
    /// </summary>
    private async Task WaitUntilNoBatchIsInFlightAsync()
    {
        var seen = -1;

        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);

        while (DateTimeOffset.UtcNow < deadline)
        {
            var count = _factory.Notifications.PostedBatches.Count;

            if (count > 0 && count == seen)
                return;

            seen = count;

            await Task.Delay(500);
        }

        throw new TimeoutException("El worker siguió llamando a notification-service.");
    }
}
