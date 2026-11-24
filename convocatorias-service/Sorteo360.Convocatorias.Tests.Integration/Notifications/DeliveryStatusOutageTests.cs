using System.Net;
using System.Text.Json;
using Sorteo360.Convocatorias.Tests.Integration.CampaignEndpoints;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.Notifications;

/// <summary>
/// notification-service caído al leer: la lectura contesta igual y todo lo entregado dice
/// <c>sent</c>. Clase y factory aparte porque el circuito abierto del chasis se filtraría a la
/// clase de al lado.
/// </summary>
public sealed class DeliveryStatusOutageTests : IClassFixture<HttpNotificationFactory>
{
    private readonly HttpNotificationFactory _factory;

    public DeliveryStatusOutageTests(HttpNotificationFactory factory) => _factory = factory;

    [Fact]
    public async Task ListContactCampaigns_Returns200WithSent_WhenNotificationServiceIsDown()
    {
        var ana = new StubContact(
            Guid.NewGuid(), "Ana", "Pérez", $"ana.{Guid.NewGuid():N}@acme.com", CanBeEmailed: true, SuppressionReason: null);
        _factory.Crm.ServeContacts(ana);
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAndLaunchAsync(client, EventsStub.EventId);
        await CampaignTestClient.WaitUntilSentAsync(client, campaign.Id);
        _factory.Notifications.ServeDelivery(_factory.Notifications.IssuedIds[ana.Email], "delivered");

        _factory.Notifications.GoDown();
        var response = await client.GetAsync($"/v1/convocatorias/campaign-recipients?contactId={ana.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var row = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.EnumerateArray().Single();
        row.GetProperty("deliveryStatus").GetString().ShouldBe("sent");
    }
}
