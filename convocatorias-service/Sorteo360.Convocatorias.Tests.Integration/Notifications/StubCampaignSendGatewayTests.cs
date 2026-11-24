using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Infrastructure.Database;
using Sorteo360.Convocatorias.Tests.Integration.CampaignEndpoints;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Sorteo360.Convocatorias.Web.CampaignEndpoints;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.Notifications;

/// <summary>
/// El transporte que hay hoy, de punta a punta: se lanza una campaña y se mira lo que quedó en
/// <c>stub_notification</c> (spec <c>04</c> §4.1).
/// <para>
/// <b>Acá no se sustituye ningún doble.</b> Detrás de la costura está el
/// <c>StubCampaignSendGateway</c> que registra la configuración de producción, porque lo que se
/// cobra es justamente lo que él escribe: el cuerpo ya rendido, el estado por persona y el hecho
/// de que su tabla no está atada a la nuestra.
/// </para>
/// </summary>
public sealed class StubCampaignSendGatewayTests : IClassFixture<StubNotificationFactory>
{
    /// <summary>
    /// Direcciones propias de esta clase. La tabla del stub no guarda el id de la campaña —a
    /// propósito, §4.1— así que la dirección es por donde un test encuentra sus filas.
    /// </summary>
    private static readonly StubContact Ana =
        new(Guid.NewGuid(), "Ana", "Pérez", "ana.stub@acme.com", CanBeEmailed: true, SuppressionReason: null);

    private static readonly StubContact Luis =
        new(Guid.NewGuid(), "Luis", "Gómez", "luis.stub@acme.com", CanBeEmailed: true, SuppressionReason: null);

    /// <summary>
    /// Una dirección que no puede existir. Es el único rechazo que el contrato admite en el momento
    /// de aceptar (§2.1), y llega hasta acá porque se copió del CRM al seleccionar.
    /// </summary>
    private static readonly StubContact Rota =
        new(Guid.NewGuid(), "Rosa", "Rota", "rosa@@roto.com", CanBeEmailed: true, SuppressionReason: null);

    private readonly StubNotificationFactory _factory;

    public StubCampaignSendGatewayTests(StubNotificationFactory factory) => _factory = factory;

    /// <summary>
    /// Una fila por persona, con el correo que habría salido: el asunto y el cuerpo ya
    /// sustituidos, sin un marcador de pie. El stub es el lado que sustituye, y este test es lo
    /// que lo demuestra.
    /// </summary>
    [Fact]
    public async Task SendAsync_WritesOneRenderedRowPerRecipient_WhenACampaignIsLaunched()
    {
        _factory.Crm.ServeContacts(Ana, Luis);

        var client = _factory.CreateClient();
        var campaign = await LaunchAsync(client);

        await WaitUntilSentAsync(client, campaign.Id);

        var rows = await RowsForAsync(Ana.Email, Luis.Email);

        rows.Count.ShouldBe(2);

        // Un lote, un id: las dos personas entraron en la misma llamada.
        rows.Select(row => row.BatchId).Distinct().Count().ShouldBe(1);

        rows.ShouldAllBe(row => row.Type == CampaignNotification.Type);
        rows.ShouldAllBe(row => row.Language == EventsStub.EventLanguage);
        rows.ShouldAllBe(row => row.Status == CampaignNotification.StatusAccepted);
        rows.ShouldAllBe(row => row.FailureReason == null);

        foreach (var row in rows)
        {
            // Nada de marcador: ni de la bolsa compartida ni de la de esa persona.
            row.Subject.ShouldNotContain("{{");
            row.BodyHtml.ShouldNotContain("{{");

            // Los tres valores del evento, que vienen de la bolsa compartida.
            row.Subject.ShouldContain(EventsStub.EventName);
            row.BodyHtml.ShouldContain(EventsStub.EventName);

            // Y el enlace de registro de esa persona, con su token en claro: es el punto de la
            // tabla, y la exposición que §4.2 acepta con su salida fijada.
            row.BodyHtml.ShouldContain(ServiceIdentityStub.PortalBaseUrl + "/publico/registro#t=");
        }

        // El saludo es de cada persona, no del lote.
        Row(rows, Ana.Email).BodyHtml.ShouldContain(Ana.FirstName);
        Row(rows, Luis.Email).BodyHtml.ShouldContain(Luis.FirstName);
        Row(rows, Ana.Email).BodyHtml.ShouldNotContain(Luis.FirstName);
    }

    /// <summary>
    /// El asunto sale sin escape y el cuerpo escapado, que es la asimetría del motor: el asunto es
    /// una cabecera y el cuerpo es marcado (spec <c>02</c> §5). Lo que se cobra es que el lado que
    /// sustituye —este— hereda esa regla y no la reinventa.
    /// </summary>
    [Fact]
    public async Task SendAsync_EscapesEveryValueItSubstitutes_WhenItRendersTheBody()
    {
        // Un nombre que es marcado. Llega de una hoja que sube una persona, así que es un camino
        // vivo hasta el correo.
        var script = Ana with
        {
            Id = Guid.NewGuid(),
            FirstName = "Juan <script>",
            Email = "juan.script@acme.com",
        };

        _factory.Crm.ServeContacts(script);

        var client = _factory.CreateClient();
        var campaign = await LaunchAsync(client);

        await WaitUntilSentAsync(client, campaign.Id);

        var row = Row(await RowsForAsync(script.Email), script.Email);

        row.BodyHtml.ShouldContain("Juan &lt;script&gt;");
        row.BodyHtml.ShouldNotContain("Juan <script>");
    }

    /// <summary>
    /// Un mensaje directo sale con el asunto y el texto de su fila, el texto escapado y con sus
    /// saltos de línea, el enlace de baja y ningún enlace de registro; y sus filas guardan solo el
    /// hash de baja, así que no hay token con el que inscribirse (PGTI-174).
    /// </summary>
    [Fact]
    public async Task SendAsync_WritesTheMessageAndNoRegistrationLink_WhenTheCampaignIsADirectMessage()
    {
        var reader = Ana with { Id = Guid.NewGuid(), Email = "ana.directo@acme.com" };

        _factory.Crm.ServeContacts(reader);

        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateDirectMessageAsync(
            client,
            subject: "Novedades & avisos",
            message: "Primera <línea>\nSegunda línea");

        (await CampaignTestClient.SelectAsync(client, campaign.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await CampaignTestClient.LaunchReadyAsync(client, campaign.Id);
        await WaitUntilSentAsync(client, campaign.Id);

        var row = Row(await RowsForAsync(reader.Email), reader.Email);

        row.Subject.ShouldBe("Novedades & avisos");
        row.BodyHtml.ShouldNotContain("{{");
        row.BodyHtml.ShouldContain("white-space:pre-line");
        row.BodyHtml.ShouldContain("Primera &lt;línea&gt;");
        row.BodyHtml.ShouldContain(ServiceIdentityStub.PortalBaseUrl + "/publico/baja#u=");
        row.BodyHtml.ShouldNotContain("/publico/registro");

        var tokens = (await CampaignTestClient.RecipientTokensAsync(_factory, campaign.Id)).Single();
        tokens.HasRegistrationToken.ShouldBeFalse();
        tokens.HasUnsubscribeToken.ShouldBeTrue();
    }

    /// <summary>
    /// Un rechazo por persona deja esa fila <c>failed</c> con su motivo, el resto
    /// <c>handed_off</c>, y <b>la campaña llega igual a <c>sent</c></b>: lo que la cierra es que no
    /// le quede ninguna <c>pending</c>, no que todo saliera bien.
    /// </summary>
    [Fact]
    public async Task SendAsync_LeavesThatRowFailedAndTheCampaignStillReachesSent_WhenOneAddressIsMalformed()
    {
        _factory.Crm.ServeContacts(Rota, Luis);

        var client = _factory.CreateClient();
        var campaign = await LaunchAsync(client);

        await WaitUntilSentAsync(client, campaign.Id);

        (await CampaignTestClient.RecipientSendStatusAsync(_factory, campaign.Id, Rota.Id))
            .ShouldBe(RecipientSendStatus.Failed);
        (await CampaignTestClient.RecipientFailureReasonAsync(_factory, campaign.Id, Rota.Id))
            .ShouldBe(CampaignNotification.ReasonAddressInvalid);

        (await CampaignTestClient.RecipientSendStatusAsync(_factory, campaign.Id, Luis.Id))
            .ShouldBe(RecipientSendStatus.HandedOff);

        // Y el rechazo también dejó su fila, con el estado del contrato y su motivo: es cómo se ve
        // a quién no se le escribió sin ir a mirar un log.
        var rejected = Row(await RowsForAsync(Rota.Email), Rota.Email);

        rejected.Status.ShouldBe(CampaignNotification.StatusRejected);
        rejected.FailureReason.ShouldBe(CampaignNotification.ReasonAddressInvalid);
    }

    /// <summary>
    /// Ninguna clave ajena, en ninguno de los dos sentidos. Es lo que mantiene a una tabla
    /// provisional fuera del esquema: la migración que traiga el cliente real la borra, y una
    /// clave ajena convertiría ese borrado en una conversación (§4.1).
    /// </summary>
    [Fact]
    public async Task StubNotificationTable_HasNoForeignKey_WhenTheMigrationIsApplied()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();

        var foreignKeys = await context.Database
            .SqlQuery<string>($"""
                SELECT conname AS "Value"
                FROM pg_constraint
                WHERE contype = 'f'
                  AND (conrelid = 'convocatorias.stub_notification'::regclass
                       OR confrelid = 'convocatorias.stub_notification'::regclass)
                """)
            .ToListAsync();

        foreignKeys.ShouldBeEmpty();
    }

    /// <summary>
    /// Las filas del stub de unas direcciones, que es lo único que las agrupa. Se leen por SQL,
    /// como el resto de la suite lee la base: la tabla del stub se borra con su propia migración,
    /// así que un test atado a su clase de persistencia se rompería ese día por el lado
    /// equivocado.
    /// </summary>
    private async Task<IReadOnlyList<StubRow>> RowsForAsync(params string[] emails)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();

        return await context.Database
            .SqlQuery<StubRow>($"""
                SELECT batch_id AS "BatchId",
                       recipient_email AS "RecipientEmail",
                       type AS "Type",
                       language AS "Language",
                       subject AS "Subject",
                       body_html AS "BodyHtml",
                       status AS "Status",
                       failure_reason AS "FailureReason"
                FROM convocatorias.stub_notification
                WHERE recipient_email = ANY({emails})
                """)
            .ToListAsync();
    }

    private static StubRow Row(IReadOnlyList<StubRow> rows, string email) =>
        rows.Single(row => row.RecipientEmail == email);

    /// <summary>Una fila de <c>stub_notification</c> tal como la lee un test.</summary>
    private sealed record StubRow(
        Guid BatchId,
        string RecipientEmail,
        string Type,
        string Language,
        string Subject,
        string BodyHtml,
        string Status,
        string? FailureReason);

    /// <summary>Un borrador con lista, lanzado.</summary>
    private static async Task<CampaignResponse> LaunchAsync(HttpClient client)
    {
        var campaign = await CampaignTestClient.CreateAsync(client, EventsStub.EventId);

        (await CampaignTestClient.SelectAsync(client, campaign.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var withRecipients = await CampaignTestClient.GetAsync(client, campaign.Id);
        withRecipients.RecipientCount.ShouldBeGreaterThan(0);

        (await CampaignTestClient.LaunchAsync(
            client,
            campaign.Id,
            new { expectedRecipientCount = withRecipients.RecipientCount })).StatusCode
            .ShouldBe(HttpStatusCode.Accepted);

        return withRecipients;
    }

    /// <summary>Sondea hasta <c>sent</c>, que es lo que hace la pantalla (§1.2).</summary>
    private static async Task WaitUntilSentAsync(HttpClient client, Guid campaignId)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);

        while (DateTimeOffset.UtcNow < deadline)
        {
            if ((await CampaignTestClient.GetAsync(client, campaignId)).State == CampaignState.Sent)
                return;

            await Task.Delay(100);
        }

        throw new TimeoutException($"La campaña {campaignId} no llegó a '{CampaignState.Sent}'.");
    }
}
