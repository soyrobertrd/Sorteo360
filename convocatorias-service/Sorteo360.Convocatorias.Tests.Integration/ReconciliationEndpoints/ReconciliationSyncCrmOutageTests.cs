using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Sorteo360.Convocatorias.Tests.Integration.PublicEndpoints;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.ReconciliationEndpoints;

/// <summary>
/// Los dos reintentos con el CRM todavía caído. <b>Acá sí vale la regla del <c>503</c></b>: la
/// ruta pública la rompe a propósito porque del otro lado hay un desconocido, y del otro lado de
/// estos botones está nuestro propio personal, para quien se escribió (spec <c>06</c> §4.3).
/// <para>
/// Clase aparte, con su propia fábrica: cada llamada contra un CRM caído son tres intentos
/// fallidos, y el cortacircuitos del chasis es un singleton que los recuerda quince segundos.
/// </para>
/// </summary>
public sealed class ReconciliationSyncCrmOutageTests : IClassFixture<RegistrationPortalFactory>
{
    private readonly RegistrationPortalFactory _factory;

    public ReconciliationSyncCrmOutageTests(RegistrationPortalFactory factory) => _factory = factory;

    [Fact]
    public async Task CrmSync_Returns503AndLeavesTheRowPending_WhenCrmIsStillDown()
    {
        var pending = await ReconciliationSyncTestClient.SeedPendingRegistrationAsync(_factory);
        var before = await ReconciliationSyncTestClient.CrmStateAsync(_factory, pending.Id);
        _factory.Crm.GoDown();

        var response = await ReconciliationSyncTestClient.CrmSyncAsync(_factory.CreateClient(), pending.Id);

        await ShouldBeCrmUnavailableAsync(response);
        (await ReconciliationSyncTestClient.CrmStateAsync(_factory, pending.Id)).ShouldBe(before);
    }

    [Fact]
    public async Task UnsubscribeSync_Returns503AndLeavesTheUnsubscribePending_WhenCrmIsStillDown()
    {
        var seeded = await UnsubscribeTestClient.InviteAsync(_factory);
        await ReconciliationSyncTestClient.MarkUnsubscribePendingAsync(_factory, seeded.RecipientId);
        var before = await UnsubscribeTestClient.StateAsync(_factory, seeded.RecipientId);
        _factory.Crm.GoDown();

        var response = await ReconciliationSyncTestClient.UnsubscribeSyncAsync(_factory.CreateClient(), seeded.RecipientId);

        await ShouldBeCrmUnavailableAsync(response);
        var after = await UnsubscribeTestClient.StateAsync(_factory, seeded.RecipientId);
        after.UnsubscribedAt.ShouldBe(before.UnsubscribedAt);
        after.SyncedAt.ShouldBeNull();
    }

    private static async Task ShouldBeCrmUnavailableAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().ShouldBe(CrmUnavailableError.ErrorCode);
    }
}
