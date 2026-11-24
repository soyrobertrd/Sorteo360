using System.Net;
using System.Net.Http.Json;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Sorteo360.Convocatorias.Web.CampaignEndpoints;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.CampaignEndpoints;

/// <summary>
/// Construir la lista desde el CRM (spec <c>03</c> §6). La cadena entera va por HTTP contra el
/// CRM simulado: el cliente tipado, la tubería de resiliencia del chasis y la degradación, que
/// es lo que no corre si se reemplaza la interfaz por un doble.
/// </summary>
public sealed class CampaignRecipientSelectionTests : IClassFixture<ConvocatoriasApiDbFactory>
{
    private readonly ConvocatoriasApiDbFactory _factory;

    public CampaignRecipientSelectionTests(ConvocatoriasApiDbFactory factory)
    {
        _factory = factory;
        _factory.Crm.ServeDefaults();
    }

    /// <summary>
    /// La mezcla de la definición de hecho: contactable, sin correo y dado de baja. Se
    /// materializa solo el contactable, y los otros dos dejan una exclusión cada uno con su
    /// motivo.
    /// </summary>
    [Fact]
    public async Task Select_ReturnsOnlyTheMailableAddedWithCountsThatAddUp_WhenTheDirectoryIsMixed()
    {
        _factory.Crm.ServeContacts(
            new StubContact(Guid.NewGuid(), "Ana", "Pérez", "ana@acme.com", CanBeEmailed: true, SuppressionReason: null),
            new StubContact(Guid.NewGuid(), "Luis", "Gómez", "baja@acme.com", CanBeEmailed: false, "opted_out"),
            new StubContact(Guid.NewGuid(), "Sara", "Díaz", "vetada@acme.com", CanBeEmailed: false, "do_not_invite"),
            new StubContact(Guid.NewGuid(), "Iván", "Ruiz", Email: null!, CanBeEmailed: false, "no_email"));

        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        var selection = await SelectAsync(client, campaign.Id);

        selection.Matched.ShouldBe(4);
        selection.Added.ShouldBe(1);
        selection.Excluded.ShouldBe(3);
        selection.Skipped.ShouldBe(0);
        (selection.Added + selection.Excluded + selection.Skipped).ShouldBe(selection.Matched);

        selection.ExcludedByReason[ExclusionReason.OptedOut].ShouldBe(1);
        selection.ExcludedByReason[ExclusionReason.DoNotInvite].ShouldBe(1);
        selection.ExcludedByReason[ExclusionReason.NoEmail].ShouldBe(1);

        selection.Campaign.RecipientCount.ShouldBe(1);
        selection.Campaign.ExclusionCount.ShouldBe(3);
    }

    [Fact]
    public async Task Select_ReturnsAnExclusionRowPerContactWithItsReason_WhenContactsAreUnmailable()
    {
        _factory.Crm.ServeContacts(
            new StubContact(Guid.NewGuid(), "Luis", "Gómez", "baja@acme.com", CanBeEmailed: false, "opted_out"),
            new StubContact(Guid.NewGuid(), "Sara", "Díaz", "otra@acme.com", CanBeEmailed: false, "opted_out"));

        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        await SelectAsync(client, campaign.Id);

        (await CampaignTestClient.CountExclusionsAsync(_factory, campaign.Id, ExclusionReason.OptedOut)).ShouldBe(2);
    }

    /// <summary>
    /// Un motivo que este servicio no conoce no revienta la selección ni inventa una regla
    /// propia: se guarda como <c>contact_unavailable</c>. La precedencia es del CRM.
    /// </summary>
    [Fact]
    public async Task Select_ReturnsContactUnavailable_WhenTheSuppressionReasonIsUnknown()
    {
        _factory.Crm.ServeContacts(
            new StubContact(Guid.NewGuid(), "Nuevo", "Motivo", "raro@acme.com", CanBeEmailed: false, "hard_bounced"));

        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        var selection = await SelectAsync(client, campaign.Id);

        selection.ExcludedByReason[ExclusionReason.ContactUnavailable].ShouldBe(1);
    }

    /// <summary>
    /// Un contacto suspendido llega del CRM con <c>canBeEmailed: false</c> y el motivo
    /// <c>suspended</c>, que este servicio no conoce: queda fuera como <c>contact_unavailable</c>,
    /// contado entre las exclusiones y no perdido. El CRM lo informa sin filtrarlo de la lista.
    /// </summary>
    [Fact]
    public async Task Select_ReturnsTheSuspendedContactExcludedAsContactUnavailable_WhenTheCrmReportsItSuspended()
    {
        _factory.Crm.ServeContacts(
            new StubContact(Guid.NewGuid(), "Ana", "Pérez", "ana@acme.com", CanBeEmailed: true, SuppressionReason: null),
            new StubContact(Guid.NewGuid(), "Lucía", "Mejía", "suspendida@acme.com", CanBeEmailed: false, "suspended"));

        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        var selection = await SelectAsync(client, campaign.Id);

        selection.Matched.ShouldBe(2);
        selection.Added.ShouldBe(1);
        selection.Excluded.ShouldBe(1);
        selection.ExcludedByReason[ExclusionReason.ContactUnavailable].ShouldBe(1);
        (await CampaignTestClient.CountExclusionsAsync(_factory, campaign.Id, ExclusionReason.ContactUnavailable)).ShouldBe(1);
    }

    /// <summary>
    /// Seleccionar dos veces compone en vez de duplicar: nadie entra dos veces, y quien ya
    /// estaba se cuenta en <c>skipped</c>. Es lo que impide que una segunda selección borre en
    /// silencio una lista que alguien depuró a mano.
    /// </summary>
    [Fact]
    public async Task Select_ReturnsEveryoneSkipped_WhenRunASecondTime()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        var first = await SelectAsync(client, campaign.Id);
        var second = await SelectAsync(client, campaign.Id);

        second.Matched.ShouldBe(first.Matched);
        second.Added.ShouldBe(0);
        second.Excluded.ShouldBe(0);
        second.Skipped.ShouldBe(first.Matched);
        second.Campaign.RecipientCount.ShouldBe(first.Campaign.RecipientCount);
        second.Campaign.ExclusionCount.ShouldBe(first.Campaign.ExclusionCount);
    }

    /// <summary>
    /// Nadie está como destinatario y como exclusión a la vez: es lo que mantiene honesta
    /// cualquier cuenta sobre las dos tablas.
    /// </summary>
    [Fact]
    public async Task Select_ReturnsTheRecipientSkippedNotExcluded_WhenItTurnsUnmailableBetweenSelections()
    {
        var contactId = Guid.NewGuid();

        _factory.Crm.ServeContacts(
            new StubContact(contactId, "Ana", "Pérez", "ana@acme.com", CanBeEmailed: true, SuppressionReason: null));

        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        await SelectAsync(client, campaign.Id);

        _factory.Crm.ServeContacts(
            new StubContact(contactId, "Ana", "Pérez", "ana@acme.com", CanBeEmailed: false, "opted_out"));

        var second = await SelectAsync(client, campaign.Id);

        second.Added.ShouldBe(0);
        second.Excluded.ShouldBe(0);
        second.Skipped.ShouldBe(1);
        second.Campaign.RecipientCount.ShouldBe(1);
        second.Campaign.ExclusionCount.ShouldBe(0);
    }

    /// <summary>
    /// Una exclusión no es para siempre. Con la selección aditiva, dejarla puesta significaría
    /// que a quien se dio de baja y volvió no se le puede meter nunca más en esta campaña, y no
    /// hay ninguna ruta que quite una exclusión.
    /// </summary>
    [Fact]
    public async Task Select_ReturnsTheContactPromotedOutOfTheExclusions_WhenItBecomesMailableAgain()
    {
        var contactId = Guid.NewGuid();

        _factory.Crm.ServeContacts(
            new StubContact(contactId, "Ana", "Pérez", "ana@acme.com", CanBeEmailed: false, "opted_out"));

        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        var first = await SelectAsync(client, campaign.Id);

        first.Excluded.ShouldBe(1);
        first.Campaign.RecipientCount.ShouldBe(0);
        first.Campaign.ExclusionCount.ShouldBe(1);

        _factory.Crm.ServeContacts(
            new StubContact(contactId, "Ana", "Pérez", "ana@acme.com", CanBeEmailed: true, SuppressionReason: null));

        var second = await SelectAsync(client, campaign.Id);

        second.Added.ShouldBe(1);
        second.Skipped.ShouldBe(0);
        second.Campaign.RecipientCount.ShouldBe(1);

        // Y no queda como las dos cosas a la vez: una persona está en la campaña una sola vez.
        second.Campaign.ExclusionCount.ShouldBe(0);
        (await CampaignTestClient.CountExclusionsAsync(_factory, campaign.Id, ExclusionReason.OptedOut)).ShouldBe(0);
    }

    /// <summary>
    /// Quien sigue sin poder contactarse conserva su exclusión y su motivo, sin reescribirla.
    /// </summary>
    [Fact]
    public async Task Select_ReturnsTheExclusionUntouched_WhenTheContactIsStillUnmailable()
    {
        var contactId = Guid.NewGuid();

        _factory.Crm.ServeContacts(
            new StubContact(contactId, "Luis", "Gómez", "baja@acme.com", CanBeEmailed: false, "opted_out"));

        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        await SelectAsync(client, campaign.Id);
        var second = await SelectAsync(client, campaign.Id);

        second.Added.ShouldBe(0);
        second.Excluded.ShouldBe(0);
        second.Skipped.ShouldBe(1);
        second.Campaign.ExclusionCount.ShouldBe(1);
        (await CampaignTestClient.CountExclusionsAsync(_factory, campaign.Id, ExclusionReason.OptedOut)).ShouldBe(1);
    }

    [Fact]
    public async Task Select_ReturnsACrmUrlRepeatingEachFilterOncePerValue_WhenFiltersAreSent()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        var response = await client.PostAsJsonAsync($"{CampaignTestClient.Route}/{campaign.Id}/recipients/select", new
        {
            classifications = new[] { "prensa", "touroperador" },
            companyCategories = new[] { "mayorista" },
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var url = _factory.Crm.LastContactListUrl;

        url.ShouldNotBeNull();
        url.ShouldContain("classification=prensa");
        url.ShouldContain("classification=touroperador");
        url.ShouldContain("companyCategory=mayorista");
    }

    #region Degradación

    /// <summary>
    /// Con el CRM caído la selección falla entera: una lista no se puede construir de la nada.
    /// Es la desviación deliberada de la escalera de ADR-0013 que anota ADR-0017.
    /// </summary>
    [Fact]
    public async Task Select_Returns503AndWritesNothing_WhenTheCrmIsDown()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        _factory.Crm.GoDown();

        var response = await CampaignTestClient.SelectAsync(client, campaign.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);

        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        problem!["code"].ToString().ShouldBe("CRM_UNAVAILABLE");

        var after = await CampaignTestClient.GetAsync(client, campaign.Id);
        after.RecipientCount.ShouldBe(0);
        after.ExclusionCount.ShouldBe(0);
        after.State.ShouldBe(CampaignState.Draft);
    }

    #endregion

    #region Selección por ids (PGTI-179)

    /// <summary>
    /// Solo entran los elegidos, con la misma exclusión que el filtro, y un id que el CRM no
    /// devuelve no cuenta en <c>matched</c> ni deja fila.
    /// </summary>
    [Fact]
    public async Task Select_ReturnsOnlyTheChosenContactsWithTheSameExclusions_WhenContactIdsAreGiven()
    {
        var mailable = new StubContact(Guid.NewGuid(), "Ana", "Pérez", "ana@acme.com", CanBeEmailed: true, SuppressionReason: null);
        var optedOut = new StubContact(Guid.NewGuid(), "Luis", "Gómez", "baja@acme.com", CanBeEmailed: false, "opted_out");
        var notChosen = new StubContact(Guid.NewGuid(), "Sara", "Díaz", "sara@acme.com", CanBeEmailed: true, SuppressionReason: null);
        _factory.Crm.ServeContacts(mailable, optedOut, notChosen);

        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        var selection = await SelectByIdAsync(client, campaign.Id, mailable.Id, optedOut.Id, mailable.Id, Guid.NewGuid());

        selection.Matched.ShouldBe(2);
        selection.Added.ShouldBe(1);
        selection.Excluded.ShouldBe(1);
        selection.ExcludedByReason[ExclusionReason.OptedOut].ShouldBe(1);
        (await CampaignTestClient.RecipientEmailsAsync(_factory, campaign.Id)).ShouldBe(["ana@acme.com"]);
        _factory.Crm.LastContactListUrl.ShouldNotBeNull().ShouldContain($"id={mailable.Id}");
    }

    [Fact]
    public async Task Select_ReturnsTheChosenContactAdded_WhenTheCampaignHasNoEvent()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateDirectMessageAsync(client);

        var selection = await SelectByIdAsync(client, campaign.Id, CrmStub.ContactId);

        selection.Added.ShouldBe(1);
        selection.Campaign.RecipientCount.ShouldBe(1);
    }

    /// <summary>
    /// Una lista vacía no es "sin filtro": eso traería el directorio entero a una campaña que
    /// alguien quería mandar a tres personas.
    /// </summary>
    [Theory]
    [InlineData("""{ "contactIds": [] }""")]
    [InlineData("""{ "contactIds": ["00000000-0000-0000-0000-000000000000"] }""")]
    [InlineData("""{ "contactIds": ["2c7e3f4a-6b1d-4e8f-9a0b-1c2d3e4f5061"], "classifications": ["vip"] }""")]
    [InlineData("""{ "contactIds": ["2c7e3f4a-6b1d-4e8f-9a0b-1c2d3e4f5061"], "companyCategories": ["hotel"] }""")]
    public async Task Select_Returns400AndAsksTheCrmNothing_WhenTheContactIdsAreInvalid(string body)
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        var response = await client.PostAsync(
            $"{CampaignTestClient.Route}/{campaign.Id}/recipients/select",
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        _factory.Crm.LastContactListUrl.ShouldBeNull();
    }

    [Fact]
    public async Task Select_Returns400_WhenGivenOneThousandAndOneContactIds()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        var response = await client.PostAsJsonAsync(
            $"{CampaignTestClient.Route}/{campaign.Id}/recipients/select",
            new { contactIds = Enumerable.Range(0, 1001).Select(_ => Guid.NewGuid()).ToArray() });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Selección por lista de contactos (PGTI-173)

    private const string CallerOptCode = "madrid";

    /// <summary>
    /// Entran los miembros de la lista con la misma exclusión que el filtro, y la lista se lee
    /// por la ruta del CRM con la identidad de este servicio, no por el listado acotado a la OPT.
    /// </summary>
    [Fact]
    public async Task Select_ReturnsTheListMembersWithTheSameExclusions_WhenTheListIsTheCallersOpt()
    {
        var mailable = new StubContact(Guid.NewGuid(), "Ana", "Pérez", "ana@acme.com", CanBeEmailed: true, SuppressionReason: null);
        var optedOut = new StubContact(Guid.NewGuid(), "Luis", "Gómez", "baja@acme.com", CanBeEmailed: false, "opted_out");
        var notListed = new StubContact(Guid.NewGuid(), "Sara", "Díaz", "sara@acme.com", CanBeEmailed: true, SuppressionReason: null);
        _factory.Crm.ServeContacts(mailable, optedOut, notListed);
        var (listId, optOfficeId) = (Guid.NewGuid(), Guid.NewGuid());
        _factory.Crm.ServeContactList(listId, optOfficeId, mailable.Id, optedOut.Id);
        _factory.Crm.ServeOptOffice(optOfficeId, CallerOptCode);

        var client = ClientWithOpt(CallerOptCode);
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        var response = await SelectByListAsync(client, campaign.Id, listId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var selection = await response.Content.ReadFromJsonAsync<RecipientSelectionResponse>();
        selection.ShouldNotBeNull();
        selection.Matched.ShouldBe(2);
        selection.Added.ShouldBe(1);
        selection.ExcludedByReason[ExclusionReason.OptedOut].ShouldBe(1);
        (await CampaignTestClient.RecipientEmailsAsync(_factory, campaign.Id)).ShouldBe(["ana@acme.com"]);
        _factory.Crm.CallsSinceReset.ShouldContain($"GET /v1/contact-lists/{listId}");
    }

    /// <summary>
    /// Sin OPT (un administrador central) no hay lista que sea suya, y al CRM no se le pregunta.
    /// </summary>
    [Fact]
    public async Task Select_Returns403OptNotAssignedAndAsksTheCrmNothing_WhenTheCallerHasNoOpt()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        var response = await SelectByListAsync(client, campaign.Id, Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).ShouldContain(OptNotAssignedError.ErrorCode);
        _factory.Crm.CallsSinceReset.ShouldBeEmpty();
    }

    /// <summary>La lista de otra OPT se contesta igual que una que no existe.</summary>
    [Fact]
    public async Task Select_Returns404ContactListNotFoundAndWritesNothing_WhenTheListIsAnotherOpts()
    {
        var (listId, optOfficeId) = (Guid.NewGuid(), Guid.NewGuid());
        _factory.Crm.ServeContactList(listId, optOfficeId, CrmStub.ContactId);
        _factory.Crm.ServeOptOffice(optOfficeId, "miami");

        var client = ClientWithOpt(CallerOptCode);
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        var response = await SelectByListAsync(client, campaign.Id, listId);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldContain(ContactListNotFoundError.ErrorCode);
        (await CampaignTestClient.RecipientEmailsAsync(_factory, campaign.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Select_Returns404ContactListNotFound_WhenTheCrmDoesNotHaveTheList()
    {
        var client = ClientWithOpt(CallerOptCode);
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        var response = await SelectByListAsync(client, campaign.Id, Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldContain(ContactListNotFoundError.ErrorCode);
    }

    /// <summary>La selección no se degrada: sin CRM no hay lista que leer (ADR-0017).</summary>
    [Fact]
    public async Task Select_Returns503_WhenTheCrmIsDownWhileReadingTheList()
    {
        var client = ClientWithOpt(CallerOptCode);
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());
        _factory.Crm.GoDown();

        var response = await SelectByListAsync(client, campaign.Id, Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Theory]
    [InlineData("""{ "contactListId": "00000000-0000-0000-0000-000000000000" }""")]
    [InlineData("""{ "contactListId": "5b1e3f4a-6b1d-4e8f-9a0b-1c2d3e4f5061", "contactIds": ["2c7e3f4a-6b1d-4e8f-9a0b-1c2d3e4f5061"] }""")]
    [InlineData("""{ "contactListId": "5b1e3f4a-6b1d-4e8f-9a0b-1c2d3e4f5061", "classifications": ["vip"] }""")]
    [InlineData("""{ "contactListId": "5b1e3f4a-6b1d-4e8f-9a0b-1c2d3e4f5061", "companyCategories": ["hotel"] }""")]
    public async Task Select_Returns400AndAsksTheCrmNothing_WhenTheContactListIdIsInvalidOrMixed(string body)
    {
        var client = ClientWithOpt(CallerOptCode);
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());
        _factory.Crm.ServeDefaults();

        var response = await client.PostAsync(
            $"{CampaignTestClient.Route}/{campaign.Id}/recipients/select",
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        _factory.Crm.CallsSinceReset.ShouldBeEmpty();
    }

    private HttpClient ClientWithOpt(string optCode)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.OptCodeHeader, optCode);
        return client;
    }

    private static Task<HttpResponseMessage> SelectByListAsync(HttpClient client, Guid campaignId, Guid contactListId) =>
        client.PostAsJsonAsync(
            $"{CampaignTestClient.Route}/{campaignId}/recipients/select",
            new { contactListId });

    #endregion

    [Fact]
    public async Task Select_Returns404_WhenTheCampaignIsUnknown()
    {
        var response = await CampaignTestClient.SelectAsync(_factory.CreateClient(), Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static async Task<RecipientSelectionResponse> SelectByIdAsync(
        HttpClient client,
        Guid campaignId,
        params Guid[] contactIds)
    {
        var response = await client.PostAsJsonAsync(
            $"{CampaignTestClient.Route}/{campaignId}/recipients/select",
            new { contactIds });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await response.Content.ReadFromJsonAsync<RecipientSelectionResponse>()
            ?? throw new InvalidOperationException("La selección no devolvió cuentas.");
    }

    private static async Task<RecipientSelectionResponse> SelectAsync(HttpClient client, Guid campaignId)
    {
        var response = await CampaignTestClient.SelectAsync(client, campaignId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await response.Content.ReadFromJsonAsync<RecipientSelectionResponse>()
            ?? throw new InvalidOperationException("La selección no devolvió cuentas.");
    }
}
