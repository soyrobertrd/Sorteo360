using System.Net;
using System.Net.Http.Json;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Sorteo360.Convocatorias.Web.CampaignEndpoints;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.CampaignEndpoints;

/// <summary>
/// El CRM se cae en medio del lanzamiento, en cada uno de sus dos pasos (spec <c>04</c> §3 y
/// §3.1). La respuesta es la misma —<c>503 CRM_UNAVAILABLE</c>— porque para quien la lee es el
/// mismo hecho: el CRM no está, y nadie fue entregado.
/// <para>
/// <b>No se entrega a nadie</b> y la campaña se queda en borrador. Media campaña mandada contra
/// una lista de bajas sin verificar es peor que ninguna, y ese es exactamente el desenlace que
/// tener los dos pasos dentro de la petición, por delante del salto a <c>sending</c>, existe
/// para impedir.
/// </para>
/// <para>
/// Va en su propia clase porque un vecino caído deja huella: el chasis reintenta y su
/// cortacircuitos es un singleton compartido. Cada clase de test trae su propia aplicación, así
/// que acá el circuito nace cerrado y muere con ella.
/// </para>
/// </summary>
public sealed class CampaignLaunchCrmOutageTests : IClassFixture<ConvocatoriasApiDbFactory>
{
    private static readonly StubContact Ana =
        new(Guid.NewGuid(), "Ana", "Pérez", "ana.outage@acme.com", CanBeEmailed: true, SuppressionReason: null);

    private static readonly StubContact Luis =
        new(Guid.NewGuid(), "Luis", "Gómez", "luis.outage@acme.com", CanBeEmailed: true, SuppressionReason: null);

    private readonly ConvocatoriasApiDbFactory _factory;

    public CampaignLaunchCrmOutageTests(ConvocatoriasApiDbFactory factory) => _factory = factory;

    [Fact]
    public async Task Launch_Returns503AndLaunchesNobody_WhenTheCrmFailsAtTheFlush()
    {
        var (client, campaign, count) = await CampaignWithRecipientsAsync();

        await CampaignTestClient.UnsubscribeRecipientAsync(_factory, campaign.Id, Ana.Id);

        _factory.Crm.FailOptOut();

        var response = await CampaignTestClient.LaunchAsync(
            client,
            campaign.Id,
            new { expectedRecipientCount = count });

        await ShouldBeCrmUnavailableAsync(response);
        await ShouldStillBeAnUntouchedDraftAsync(client, campaign.Id);

        // El sello no se escribe: repetir un empujón que sí llegó sale gratis, mientras que
        // sellar uno que no llegó deja a quien se dio de baja recibiendo la campaña siguiente.
        (await CampaignTestClient.CountUnsyncedUnsubscribesAsync(_factory, campaign.Id)).ShouldBe(1);
    }

    /// <summary>
    /// El vaciado llega y la recomprobación no. Hace falta tumbar solo esa ruta: con el vecino
    /// entero caído el lanzamiento se para en el paso anterior y esta rama no se ejercita nunca.
    /// </summary>
    [Fact]
    public async Task Launch_Returns503AndLaunchesNobody_WhenTheCrmFailsAtTheMailabilityCheck()
    {
        var (client, campaign, count) = await CampaignWithRecipientsAsync();

        _factory.Crm.FailMailability();

        var response = await CampaignTestClient.LaunchAsync(
            client,
            campaign.Id,
            new { expectedRecipientCount = count });

        await ShouldBeCrmUnavailableAsync(response);
        await ShouldStillBeAnUntouchedDraftAsync(client, campaign.Id);
    }

    /// <summary>
    /// Quien lanza trae OPT y el CRM no contesta al buscar su dirección de envío (PGTI-168). Es el
    /// mismo 503 de los otros pasos del CRM: la campaña no sale desde una dirección adivinada.
    /// </summary>
    [Fact]
    public async Task Launch_Returns503AndLaunchesNobody_WhenTheCrmFailsAtTheSenderLookup()
    {
        var (client, campaign, count) = await CampaignWithRecipientsAsync();
        client.DefaultRequestHeaders.Add(TestAuthHandler.OptCodeHeader, "madrid");

        _factory.Crm.FailOptDirectory();

        var response = await CampaignTestClient.LaunchAsync(
            client,
            campaign.Id,
            new { expectedRecipientCount = count });

        await ShouldBeCrmUnavailableAsync(response);
        await ShouldStillBeAnUntouchedDraftAsync(client, campaign.Id);
    }

    private async Task<(HttpClient Client, CampaignResponse Campaign, int Count)> CampaignWithRecipientsAsync()
    {
        _factory.Crm.ServeContacts(Ana, Luis);

        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, EventsStub.EventId);

        (await CampaignTestClient.SelectAsync(client, campaign.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var withRecipients = await CampaignTestClient.GetAsync(client, campaign.Id);

        withRecipients.RecipientCount.ShouldBe(2);

        return (client, withRecipients, withRecipients.RecipientCount);
    }

    private static async Task ShouldBeCrmUnavailableAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);

        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        problem.ShouldNotBeNull();
        problem["code"].ToString().ShouldBe(CrmUnavailableError.ErrorCode);
    }

    private async Task ShouldStillBeAnUntouchedDraftAsync(HttpClient client, Guid campaignId)
    {
        var campaign = await CampaignTestClient.GetAsync(client, campaignId);

        campaign.State.ShouldBe(CampaignState.Draft);
        campaign.LaunchedById.ShouldBeNull();
        campaign.LaunchedAt.ShouldBeNull();

        (await CampaignTestClient.RecipientStatusesAsync(_factory, campaignId))
            .ShouldAllBe(status => status == RecipientSendStatus.Pending);
    }
}
