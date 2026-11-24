using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Sorteo360.Convocatorias.Core.Crm;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Sorteo360.Convocatorias.Tests.Integration.PublicEndpoints;
using Sorteo360.Convocatorias.Web.Auth;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.ReconciliationEndpoints;

/// <summary>
/// Los dos botones que terminan a mano una escritura varada en el CRM (spec <c>07</c> §6.1 y §6.2).
/// <para>
/// <b>Las filas pendientes se siembran por SQL</b> y no pasando por la ruta pública con el CRM
/// caído: cada llamada contra un CRM caído son tres intentos fallidos contra el cortacircuitos del
/// chasis, y unos pocos lo abrirían sobre los tests vecinos. Que la ruta pública deja la fila
/// pendiente ya lo cobra <c>RegistrationCrmSyncTests</c>; los casos con el CRM todavía caído viven
/// en <see cref="ReconciliationSyncCrmOutageTests"/>, con su propia fábrica.
/// </para>
/// </summary>
public sealed class ReconciliationSyncTests : IClassFixture<RegistrationPortalFactory>
{
    private readonly RegistrationPortalFactory _factory;

    public ReconciliationSyncTests(RegistrationPortalFactory factory) => _factory = factory;

    #region crm-sync

    /// <summary>
    /// El mismo alta que hace la inscripción pública: con su consentimiento de
    /// <c>self_registration</c>, y la fila pasa a sincronizada.
    /// </summary>
    [Fact]
    public async Task CrmSync_ReturnsTheRegistrationSynced_WhenAPendingRowReachesCrm()
    {
        _factory.Crm.ServeDefaults();
        var pending = await ReconciliationSyncTestClient.SeedPendingRegistrationAsync(_factory, "Juan", "Pérez");

        var response = await ReconciliationSyncTestClient.CrmSyncAsync(_factory.CreateClient(), pending.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetGuid().ShouldBe(pending.Id);
        body.GetProperty("crmContactId").ValueKind.ShouldBe(JsonValueKind.String);
        body.GetProperty("contactDetailsDiffer").GetBoolean().ShouldBeFalse();
        body.GetProperty("canBeEmailed").GetBoolean().ShouldBeTrue();

        var created = _factory.Crm.ContactCreateBodies.ShouldHaveSingleItem();
        created.Email.ShouldBe(pending.Email);
        created.FirstName.ShouldBe("Juan");
        created.HasConsent.ShouldBeTrue();
        created.ConsentSource.ShouldBe(ConsentSources.SelfRegistration);

        var state = await ReconciliationSyncTestClient.CrmStateAsync(_factory, pending.Id);
        state.ContactId.ShouldBe(body.GetProperty("crmContactId").GetGuid());
        state.FailedAt.ShouldNotBeNull();
    }

    /// <summary>
    /// Un <c>409</c> es una coincidencia y no un error: se enlaza la ficha que devolvió el CRM, no
    /// se le escribe nada, y su <c>canBeEmailed</c> se vuelve a mirar sobre ese mismo cuerpo.
    /// </summary>
    [Fact]
    public async Task CrmSync_ReturnsTheRowLinkedWithTheConflictsVerdict_WhenCrmAnswersWithAnExistingContact()
    {
        _factory.Crm.ServeDefaults();
        var pending = await ReconciliationSyncTestClient.SeedPendingRegistrationAsync(_factory, "J.", "Perez");
        var existing = new StubContact(Guid.NewGuid(), "Juan", "Pérez", pending.Email, CanBeEmailed: false, "opt_out");
        _factory.Crm.ServeContactCreateConflict(existing);

        var response = await ReconciliationSyncTestClient.CrmSyncAsync(_factory.CreateClient(), pending.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("crmContactId").GetGuid().ShouldBe(existing.Id);
        body.GetProperty("contactDetailsDiffer").GetBoolean().ShouldBeTrue();
        body.GetProperty("canBeEmailed").GetBoolean().ShouldBeFalse();
        _factory.Crm.SuccessfulContactCreateCount.ShouldBe(0);
    }

    /// <summary>Pulsarlo dos veces no cuesta una segunda alta: la segunda no llama al CRM.</summary>
    [Fact]
    public async Task CrmSync_ReturnsTheRowUnchangedWithoutCallingCrm_WhenItIsAlreadySynced()
    {
        _factory.Crm.ServeDefaults();
        var pending = await ReconciliationSyncTestClient.SeedPendingRegistrationAsync(_factory);
        var client = _factory.CreateClient();
        var first = await (await ReconciliationSyncTestClient.CrmSyncAsync(client, pending.Id))
            .Content.ReadFromJsonAsync<JsonElement>();

        _factory.Crm.ResetRequestLog();
        var response = await ReconciliationSyncTestClient.CrmSyncAsync(client, pending.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("crmContactId").GetGuid().ShouldBe(first.GetProperty("crmContactId").GetGuid());
        body.GetProperty("canBeEmailed").ValueKind.ShouldBe(JsonValueKind.Null);
        _factory.Crm.RequestCount.ShouldBe(0);
    }

    [Fact]
    public async Task CrmSync_Returns404_WhenTheRegistrationDoesNotExist()
    {
        var response = await ReconciliationSyncTestClient.CrmSyncAsync(_factory.CreateClient(), Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    #endregion

    #region unsubscribe-sync

    /// <summary>
    /// El mismo empujón que la baja pública: el opt-out llega al contacto y la fila se sella, con
    /// la hora del clic intacta.
    /// </summary>
    [Fact]
    public async Task UnsubscribeSync_ReturnsTheRecipientSynced_WhenAPendingUnsubscribeReachesCrm()
    {
        _factory.Crm.ServeDefaults();
        var seeded = await UnsubscribeTestClient.InviteAsync(_factory);
        await ReconciliationSyncTestClient.MarkUnsubscribePendingAsync(_factory, seeded.RecipientId);
        var clickedAt = (await UnsubscribeTestClient.StateAsync(_factory, seeded.RecipientId)).UnsubscribedAt;

        var response = await ReconciliationSyncTestClient.UnsubscribeSyncAsync(_factory.CreateClient(), seeded.RecipientId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetGuid().ShouldBe(seeded.RecipientId);
        body.GetProperty("contactId").GetGuid().ShouldBe(seeded.ContactId);
        body.GetProperty("unsubscribeSyncedAt").ValueKind.ShouldBe(JsonValueKind.String);
        _factory.Crm.OptedOutContactIds.ShouldBe([seeded.ContactId]);

        var state = await UnsubscribeTestClient.StateAsync(_factory, seeded.RecipientId);
        state.UnsubscribedAt.ShouldBe(clickedAt);
        state.SyncedAt.ShouldNotBeNull();
    }

    /// <summary>
    /// Esta ruta no da de baja a nadie: sin clic no hay nada que sincronizar, y empujar la baja
    /// sería el fallo de cumplimiento que la columna existe para impedir (spec <c>06</c> §6).
    /// </summary>
    [Fact]
    public async Task UnsubscribeSync_Returns409AndPushesNothing_WhenTheRecipientNeverUnsubscribed()
    {
        _factory.Crm.ServeDefaults();
        var seeded = await UnsubscribeTestClient.InviteAsync(_factory);

        var response = await ReconciliationSyncTestClient.UnsubscribeSyncAsync(_factory.CreateClient(), seeded.RecipientId);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().ShouldBe(RecipientNotUnsubscribedError.ErrorCode);
        _factory.Crm.OptedOutContactIds.ShouldBeEmpty();
        (await UnsubscribeTestClient.StateAsync(_factory, seeded.RecipientId)).UnsubscribedAt.ShouldBeNull();
    }

    [Fact]
    public async Task UnsubscribeSync_ReturnsTheRecipientUnchangedWithoutCallingCrm_WhenItIsAlreadySynced()
    {
        _factory.Crm.ServeDefaults();
        var seeded = await UnsubscribeTestClient.InviteAsync(_factory);
        await UnsubscribeTestClient.UnsubscribeAsync(_factory.CreateClient(), seeded.Token);
        var syncedAt = (await UnsubscribeTestClient.StateAsync(_factory, seeded.RecipientId)).SyncedAt;

        _factory.Crm.ResetRequestLog();
        var response = await ReconciliationSyncTestClient.UnsubscribeSyncAsync(_factory.CreateClient(), seeded.RecipientId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("unsubscribeSyncedAt").GetDateTimeOffset().ShouldBe(syncedAt!.Value);
        _factory.Crm.RequestCount.ShouldBe(0);
    }

    [Fact]
    public async Task UnsubscribeSync_Returns404_WhenTheRecipientDoesNotExist()
    {
        var response = await ReconciliationSyncTestClient.UnsubscribeSyncAsync(_factory.CreateClient(), Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    #endregion

    /// <summary>
    /// Las dos acciones son <c>campaigns:write</c> (spec <c>07</c> §1): quien solo lee, o quien
    /// solo lanza, no las pulsa.
    /// </summary>
    [Theory]
    [InlineData("registrations", "crm-sync", AuthorizationPolicies.ConvocatoriasRead)]
    [InlineData("registrations", "crm-sync", AuthorizationPolicies.CampaignsLaunch)]
    [InlineData("recipients", "unsubscribe-sync", AuthorizationPolicies.ConvocatoriasRead)]
    [InlineData("recipients", "unsubscribe-sync", AuthorizationPolicies.CampaignsLaunch)]
    public async Task SyncActions_Returns403_WhenTheCallerLacksCampaignsWrite(string resource, string action, string role)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/convocatorias/{resource}/{Guid.NewGuid()}/{action}");
        request.Headers.Add(TestAuthHandler.RolesHeader, role);

        var response = await _factory.CreateClient().SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
