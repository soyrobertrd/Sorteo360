using System.Net;
using System.Net.Http.Json;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Sorteo360.Convocatorias.Web.Auth;
using Sorteo360.Convocatorias.Web.CampaignEndpoints;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.CampaignEndpoints;

/// <summary>
/// Las dos salidas de un envío a medias (spec <c>04</c> §5, <c>03</c> §7).
/// <para>
/// <b>Nada en este servicio reintenta solo, a propósito.</b> Un lote que se cae deja sus filas
/// <c>pending</c> y ahí se quedan hasta que una persona aprieta el botón, así que estas dos rutas
/// son la única salida de una campaña atascada y lo que <c>strandedCampaigns</c> (<c>07</c> §6.3)
/// le pondrá delante.
/// </para>
/// </summary>
public sealed class CampaignResumeTests : IClassFixture<CampaignHandoffFactory>
{
    private static readonly StubContact Ana =
        new(Guid.NewGuid(), "Ana", "Pérez", "ana.resume@acme.com", CanBeEmailed: true, SuppressionReason: null);

    private static readonly StubContact Luis =
        new(Guid.NewGuid(), "Luis", "Gómez", "luis.resume@acme.com", CanBeEmailed: true, SuppressionReason: null);

    private readonly CampaignHandoffFactory _factory;

    public CampaignResumeTests(CampaignHandoffFactory factory)
    {
        _factory = factory;
        _factory.Crm.ServeDefaults();
        _factory.Gateway.Reset();
    }

    /// <summary>
    /// El lote se cae entero, las filas se quedan <c>pending</c> y la campaña en <c>sending</c>.
    /// La reanudación vuelve a mandar esa cola y la campaña se cierra. El <c>202</c> es por lo
    /// mismo que el del lanzamiento: la entrega ocurre después, en el mismo worker.
    /// </summary>
    [Fact]
    public async Task ResumeHandoff_HandsOffThePendingTailAndReturns202_WhenTheFirstAttemptFailed()
    {
        _factory.Crm.ServeContacts(Ana, Luis);
        _factory.Gateway.FailNextCalls(1);

        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAndLaunchAsync(client, EventsStub.EventId);

        await WaitUntilBatchesAsync(1);

        // Nada anotado: antes del 202 del emisor no existe historia, así que la cola queda entera.
        (await CampaignTestClient.GetAsync(client, campaign.Id)).State.ShouldBe(CampaignState.Sending);
        (await CampaignTestClient.RecipientStatusesAsync(_factory, campaign.Id))
            .ShouldAllBe(status => status == RecipientSendStatus.Pending);

        var resume = await CampaignTestClient.ResumeHandoffAsync(client, campaign.Id);

        resume.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        resume.Headers.Location!.ToString().ShouldEndWith(campaign.Id.ToString());

        await CampaignTestClient.WaitUntilSentAsync(client, campaign.Id);

        (await CampaignTestClient.RecipientStatusesAsync(_factory, campaign.Id))
            .ShouldAllBe(status => status == RecipientSendStatus.HandedOff);

        // El segundo lote llevó la cola entera, que en este caso son las dos filas: ninguna se
        // había llegado a anotar.
        _factory.Gateway.Requests.Count.ShouldBe(2);
        _factory.Gateway.Requests[1].Recipients.Count.ShouldBe(2);
    }

    /// <summary>
    /// La cola sin entregar solo existe en <c>sending</c>. Sobre un borrador no hay nada que
    /// desatascar, y decirlo con un código es lo que separa "no se puede todavía" de "ese id no
    /// existe".
    /// </summary>
    [Fact]
    public async Task ResumeHandoff_Refuses_WhenTheCampaignIsStillADraft()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, EventsStub.EventId);

        var resume = await CampaignTestClient.ResumeHandoffAsync(client, campaign.Id);

        resume.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOf(resume)).ShouldBe(CampaignNotSendingError.ErrorCode);
    }

    [Fact]
    public async Task ResumeHandoff_ReturnsNotFound_WhenTheCampaignDoesNotExist()
    {
        var client = _factory.CreateClient();

        (await CampaignTestClient.ResumeHandoffAsync(client, Guid.NewGuid())).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Desatascar no es decidir: pide <c>campaigns:write</c> y no <c>campaigns:launch</c>, porque
    /// manda la cola de una lista que alguien ya lanzó (spec <c>03</c> §7).
    /// </summary>
    [Fact]
    public async Task ResumeHandoff_IsRefusedToAReader_WhenTheCallerLacksTheWriteRole()
    {
        var reader = _factory.CreateClient();
        reader.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, AuthorizationPolicies.ConvocatoriasRead);

        (await CampaignTestClient.ResumeHandoffAsync(reader, Guid.NewGuid())).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Lo mismo para la otra ruta: las dos piden <c>campaigns:write</c>, y un lector no desatasca.
    /// </summary>
    [Fact]
    public async Task RetryFailed_IsRefusedToAReader_WhenTheCallerLacksTheWriteRole()
    {
        var reader = _factory.CreateClient();
        reader.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, AuthorizationPolicies.ConvocatoriasRead);

        (await CampaignTestClient.RetryFailedAsync(reader, Guid.NewGuid())).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// <b>El caso normal de <c>retry-failed</c>.</b> Una campaña con filas rechazadas ya está
    /// <c>sent</c>, porque lo que la cierra es que no le quede ninguna pendiente. La ruta reabre
    /// esa fila, el worker la entrega y la campaña se vuelve a cerrar: no vuelve atrás, sigue en
    /// <c>sent</c>.
    /// </summary>
    [Fact]
    public async Task RetryFailed_MailsTheRejectedRowAgain_WhenTheCampaignIsAlreadySent()
    {
        _factory.Crm.ServeContacts(Ana, Luis);

        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, EventsStub.EventId);

        (await CampaignTestClient.SelectAsync(client, campaign.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // La identidad de la fila es suya, no la del contacto: se lee de la base antes de lanzar.
        var recipientIds = await CampaignTestClient.RecipientIdsAsync(_factory, campaign.Id);
        recipientIds.Count.ShouldBe(2);

        foreach (var recipientId in recipientIds)
            _factory.Gateway.Reject(recipientId);

        await CampaignTestClient.LaunchReadyAsync(client, campaign.Id);
        await CampaignTestClient.WaitUntilSentAsync(client, campaign.Id);

        (await CampaignTestClient.RecipientStatusesAsync(_factory, campaign.Id))
            .ShouldAllBe(status => status == RecipientSendStatus.Failed);

        // La dirección se corrigió en el CRM y el emisor ya las acepta.
        _factory.Gateway.AcceptEveryone();

        var retry = await CampaignTestClient.RetryFailedAsync(client, campaign.Id);

        retry.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await retry.Content.ReadFromJsonAsync<CampaignResponse>())!.State.ShouldBe(CampaignState.Sent);

        await WaitUntilEveryRowIsAsync(campaign.Id, RecipientSendStatus.HandedOff);

        (await CampaignTestClient.GetAsync(client, campaign.Id)).State.ShouldBe(CampaignState.Sent);
    }

    /// <summary>
    /// Sin ninguna fila rechazada no hace nada, y responde igual. Pedirlo de más no es un error:
    /// el botón está en una pantalla que alguien refresca a mano.
    /// </summary>
    [Fact]
    public async Task RetryFailed_Returns202AndChangesNothing_WhenNoRowWasRejected()
    {
        _factory.Crm.ServeContacts(Ana, Luis);

        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAndLaunchAsync(client, EventsStub.EventId);

        await CampaignTestClient.WaitUntilSentAsync(client, campaign.Id);

        (await CampaignTestClient.RetryFailedAsync(client, campaign.Id)).StatusCode.ShouldBe(HttpStatusCode.Accepted);

        (await CampaignTestClient.RecipientStatusesAsync(_factory, campaign.Id))
            .ShouldAllBe(status => status == RecipientSendStatus.HandedOff);
    }

    /// <summary>
    /// Desde un borrador no: nunca salió, así que no hay ningún rechazo que deshacer.
    /// </summary>
    [Fact]
    public async Task RetryFailed_Refuses_WhenTheCampaignIsStillADraft()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, EventsStub.EventId);

        var retry = await CampaignTestClient.RetryFailedAsync(client, campaign.Id);

        retry.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOf(retry)).ShouldBe(CampaignNotSendingError.ErrorCode);
    }

    [Fact]
    public async Task RetryFailed_ReturnsNotFound_WhenTheCampaignDoesNotExist()
    {
        var client = _factory.CreateClient();

        (await CampaignTestClient.RetryFailedAsync(client, Guid.NewGuid())).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static async Task<string?> CodeOf(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        return problem!["code"].ToString();
    }

    private async Task WaitUntilBatchesAsync(int count) =>
        await WaitUntilAsync(() => Task.FromResult(_factory.Gateway.Requests.Count >= count));

    private async Task WaitUntilEveryRowIsAsync(Guid campaignId, string status) =>
        await WaitUntilAsync(async () =>
            (await CampaignTestClient.RecipientStatusesAsync(_factory, campaignId))
                .All(row => row == status));

    /// <summary>
    /// Sondea, que es lo que hace la pantalla: la entrega corre en un worker, así que afirmar
    /// enseguida sería una carrera contra él (spec <c>04</c> §1.2).
    /// </summary>
    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await condition())
                return;

            await Task.Delay(100);
        }

        throw new TimeoutException("La condición no se cumplió a tiempo.");
    }
}
