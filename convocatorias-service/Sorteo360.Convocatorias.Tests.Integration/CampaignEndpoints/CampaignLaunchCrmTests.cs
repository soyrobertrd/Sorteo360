using System.Net;
using System.Net.Http.Json;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Sorteo360.Convocatorias.Web.CampaignEndpoints;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.CampaignEndpoints;

/// <summary>
/// Los dos pasos del CRM dentro del lanzamiento (spec <c>04</c> §3 y §3.1): vaciar las bajas sin
/// sincronizar y recomprobar la contactabilidad de la lista entera.
/// <para>
/// <b>El orden es la funcionalidad.</b> Vaciar va primero porque una baja sin sincronizar hace
/// que el CRM siga contestando <c>canBeEmailed: true</c> por alguien que se dio de baja, y
/// entonces la recomprobación —que es exactamente la pregunta que esa baja tenía que
/// responder— le dice que sí. Por eso lo que se mira acá no son cuentas sino el orden en que
/// llegaron las peticiones.
/// </para>
/// <para>
/// El corte del CRM vive en <see cref="CampaignLaunchCrmOutageTests"/>, con su propia
/// aplicación: el cortacircuitos del chasis es un singleton y un vecino caído no tiene por qué
/// hacer fallar a las pruebas vecinas.
/// </para>
/// </summary>
public sealed class CampaignLaunchCrmTests : IClassFixture<ConvocatoriasApiDbFactory>
{
    private static readonly StubContact Ana =
        new(Guid.NewGuid(), "Ana", "Pérez", "ana.crm@acme.com", CanBeEmailed: true, SuppressionReason: null);

    private static readonly StubContact Luis =
        new(Guid.NewGuid(), "Luis", "Gómez", "luis.crm@acme.com", CanBeEmailed: true, SuppressionReason: null);

    private readonly ConvocatoriasApiDbFactory _factory;

    public CampaignLaunchCrmTests(ConvocatoriasApiDbFactory factory) => _factory = factory;

    #region El vaciado de bajas

    /// <summary>
    /// La baja sale hacia el CRM <b>antes</b> de la recomprobación. Al revés, el CRM contestaría
    /// que a esa persona sí se le puede escribir, porque todavía no sabría que se dio de baja.
    /// </summary>
    [Fact]
    public async Task Launch_Returns202AfterPushingTheOptOutFirst_WhenAnUnsubscribeIsUnsynced()
    {
        var client = ServeTwoMailableContacts();
        var (campaign, count) = await CampaignWithRecipientsAsync(client);

        await CampaignTestClient.UnsubscribeRecipientAsync(_factory, campaign.Id, Ana.Id);
        _factory.Crm.ResetRequestLog();

        var response = await CampaignTestClient.LaunchAsync(
            client,
            campaign.Id,
            new { expectedRecipientCount = count });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var calls = _factory.Crm.CallsSinceReset;
        var firstOptOut = FirstCallEndingIn(calls, "/opt-out");
        var firstMailability = FirstCallEndingIn(calls, "/mailability");

        firstOptOut.ShouldNotBeNull();
        firstMailability.ShouldNotBeNull();
        firstOptOut.Value.ShouldBeLessThan(firstMailability.Value);

        _factory.Crm.OptedOutContactIds.ShouldContain(Ana.Id);
    }

    [Fact]
    public async Task Launch_ReturnsNoUnsyncedUnsubscribes_WhenTheFlushLands()
    {
        var client = ServeTwoMailableContacts();
        var (campaign, count) = await CampaignWithRecipientsAsync(client);

        await CampaignTestClient.UnsubscribeRecipientAsync(_factory, campaign.Id, Ana.Id);

        (await CampaignTestClient.CountUnsyncedUnsubscribesAsync(_factory, campaign.Id)).ShouldBe(1);

        var response = await CampaignTestClient.LaunchAsync(
            client,
            campaign.Id,
            new { expectedRecipientCount = count });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        (await CampaignTestClient.CountUnsyncedUnsubscribesAsync(_factory, campaign.Id)).ShouldBe(0);
    }

    /// <summary>
    /// Sin bajas pendientes no sale ningún empujón. El vaciado es un paso del orden, no una
    /// llamada que se hace por si acaso.
    /// </summary>
    [Fact]
    public async Task Launch_ReturnsNoOptOutPushed_WhenNothingIsUnsynced()
    {
        var client = ServeTwoMailableContacts();
        var (campaign, count) = await CampaignWithRecipientsAsync(client);

        // La premisa se declara en vez de heredarse: el vaciado mira todo el servicio, así que una
        // baja que dejó pendiente otro test es una baja que este tendría que empujar.
        await CampaignTestClient.MarkEveryUnsubscribeSyncedAsync(_factory);
        _factory.Crm.ResetRequestLog();

        await CampaignTestClient.LaunchAsync(client, campaign.Id, new { expectedRecipientCount = count });

        _factory.Crm.OptedOutContactIds.ShouldBeEmpty();
        _factory.Crm.MailabilityCallCount.ShouldBe(1);
    }

    /// <summary>
    /// El CRM contesta 404 porque ya no tiene esa ficha: borrada en blando entre el clic en el
    /// enlace de baja y este lanzamiento. La baja se da por sellada y el lanzamiento sigue.
    /// <para>
    /// Tratarlo como avería sería la peor de las dos opciones: el vaciado recorre todo el
    /// servicio y no sella lo que no llegó, así que esa fila se reintentaría en <b>cada</b>
    /// lanzamiento posterior y los dejaría todos en 503 hasta que alguien toque la base a mano.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Launch_Returns202AndStampsTheUnsubscribe_WhenTheCrmNoLongerHasTheContact()
    {
        var client = ServeTwoMailableContacts();
        var (campaign, count) = await CampaignWithRecipientsAsync(client);

        await CampaignTestClient.UnsubscribeRecipientAsync(_factory, campaign.Id, Ana.Id);

        _factory.Crm.ServeOptOutContactGone();

        var response = await CampaignTestClient.LaunchAsync(
            client,
            campaign.Id,
            new { expectedRecipientCount = count });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        (await CampaignTestClient.CountUnsyncedUnsubscribesAsync(_factory, campaign.Id)).ShouldBe(0);
    }

    /// <summary>
    /// Validar es el paso 1 y el CRM son el 2 y el 3 (spec <c>04</c> §1). Una cuenta rancia no
    /// escribe nada (spec <c>03</c> §5.1), y un vaciado por delante de ella sí escribiría sus
    /// sellos, así que el orden no es cosmético.
    /// </summary>
    [Fact]
    public async Task Launch_Returns409WithoutCallingTheCrm_WhenTheCountIsStale()
    {
        var client = ServeTwoMailableContacts();
        var (campaign, count) = await CampaignWithRecipientsAsync(client);

        await CampaignTestClient.UnsubscribeRecipientAsync(_factory, campaign.Id, Ana.Id);
        _factory.Crm.ResetRequestLog();

        var response = await CampaignTestClient.LaunchAsync(
            client,
            campaign.Id,
            new { expectedRecipientCount = count + 13 });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ShouldCarryCodeAsync(response, "CAMPAIGN_RECIPIENTS_CHANGED");

        _factory.Crm.CallsSinceReset.ShouldBeEmpty();
        (await CampaignTestClient.CountUnsyncedUnsubscribesAsync(_factory, campaign.Id)).ShouldBe(1);
    }

    #endregion

    #region La recomprobación

    /// <summary>
    /// Seleccionar y lanzar no son el mismo instante. Quien se dio de baja en medio se queda en
    /// <c>suppressed</c> con el motivo del CRM, <b>la campaña sigue para el resto</b>, y quien
    /// lanza recibe su 202: suprimir a alguien no es un error y no lleva código.
    /// </summary>
    [Fact]
    public async Task Launch_Returns202WithThatRecipientSuppressed_WhenTheRecheckRefusesOne()
    {
        var client = ServeTwoMailableContacts();
        var (campaign, count) = await CampaignWithRecipientsAsync(client);

        _factory.Crm.ServeMailability(
            Ana with { CanBeEmailed = false, SuppressionReason = "opted_out" },
            Luis);

        var response = await CampaignTestClient.LaunchAsync(
            client,
            campaign.Id,
            new { expectedRecipientCount = count });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        (await CampaignTestClient.RecipientSendStatusAsync(_factory, campaign.Id, Ana.Id))
            .ShouldBe(RecipientSendStatus.Suppressed);
        (await CampaignTestClient.RecipientFailureReasonAsync(_factory, campaign.Id, Ana.Id))
            .ShouldBe(ExclusionReason.OptedOut);

        (await CampaignTestClient.RecipientSendStatusAsync(_factory, campaign.Id, Luis.Id))
            .ShouldBe(RecipientSendStatus.Pending);

        (await CampaignTestClient.GetAsync(client, campaign.Id)).State.ShouldBe(CampaignState.Sending);
    }

    /// <summary>
    /// El CRM no puede opinar sobre una ficha que no devuelve, así que el id que falta lo resuelve
    /// este servicio. El caso real es un contacto borrado en blando entre la selección y el
    /// lanzamiento.
    /// </summary>
    [Fact]
    public async Task Launch_ReturnsTheRecipientSuppressedAsContactUnavailable_WhenTheRecheckOmitsIt()
    {
        var client = ServeTwoMailableContacts();
        var (campaign, count) = await CampaignWithRecipientsAsync(client);

        // Luis vuelve; Ana no aparece en la respuesta en absoluto.
        _factory.Crm.ServeMailability(Luis);

        var response = await CampaignTestClient.LaunchAsync(
            client,
            campaign.Id,
            new { expectedRecipientCount = count });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        (await CampaignTestClient.RecipientSendStatusAsync(_factory, campaign.Id, Ana.Id))
            .ShouldBe(RecipientSendStatus.Suppressed);
        (await CampaignTestClient.RecipientFailureReasonAsync(_factory, campaign.Id, Ana.Id))
            .ShouldBe(ExclusionReason.ContactUnavailable);
    }

    /// <summary>
    /// Que el CRM los declare a todos no contactables es un rechazo y no una campaña que informa
    /// <c>sent</c> sin haberle escrito a nadie. Nada se escribe: ni el salto de estado ni una sola
    /// fila suprimida bajo una campaña que se queda en borrador.
    /// </summary>
    [Fact]
    public async Task Launch_Returns400AndWritesNothing_WhenTheRecheckRefusesEverybody()
    {
        var client = ServeTwoMailableContacts();
        var (campaign, count) = await CampaignWithRecipientsAsync(client);

        _factory.Crm.ServeMailability(
            Ana with { CanBeEmailed = false, SuppressionReason = "opted_out" },
            Luis with { CanBeEmailed = false, SuppressionReason = "do_not_invite" });

        var response = await CampaignTestClient.LaunchAsync(
            client,
            campaign.Id,
            new { expectedRecipientCount = count });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldCarryCodeAsync(response, "CAMPAIGN_HAS_NO_MAILABLE_RECIPIENTS");

        var campaignNow = await CampaignTestClient.GetAsync(client, campaign.Id);

        campaignNow.State.ShouldBe(CampaignState.Draft);
        campaignNow.LaunchedAt.ShouldBeNull();

        (await CampaignTestClient.RecipientStatusesAsync(_factory, campaign.Id))
            .ShouldAllBe(status => status == RecipientSendStatus.Pending);
    }

    #endregion

    /// <summary>
    /// Dos contactos contactables, que es lo que hace falta para que suprimir a uno deje una
    /// campaña que todavía tiene a quién escribirle.
    /// </summary>
    private HttpClient ServeTwoMailableContacts()
    {
        _factory.Crm.ServeContacts(Ana, Luis);
        return _factory.CreateClient();
    }

    private async Task<(CampaignResponse Campaign, int Count)> CampaignWithRecipientsAsync(HttpClient client)
    {
        var campaign = await CampaignTestClient.CreateAsync(client, EventsStub.EventId);

        (await CampaignTestClient.SelectAsync(client, campaign.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var withRecipients = await CampaignTestClient.GetAsync(client, campaign.Id);

        withRecipients.RecipientCount.ShouldBe(2);

        return (withRecipients, withRecipients.RecipientCount);
    }

    /// <summary>Dónde cayó la primera llamada a esa ruta, o <c>null</c> si no la hubo.</summary>
    private static int? FirstCallEndingIn(IReadOnlyList<string> calls, string suffix)
    {
        for (var index = 0; index < calls.Count; index++)
        {
            if (calls[index].EndsWith(suffix, StringComparison.Ordinal))
                return index;
        }

        return null;
    }

    private static async Task ShouldCarryCodeAsync(HttpResponseMessage response, string code)
    {
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        problem.ShouldNotBeNull();
        problem["code"].ToString().ShouldBe(code);
    }
}
