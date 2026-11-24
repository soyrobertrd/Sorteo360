using System.Net;
using System.Text.Json;
using Sorteo360.Convocatorias.Tests.Integration.CampaignEndpoints;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.Notifications;

/// <summary>
/// La entrega de punta a punta (PGTI-177): la campaña sale por el transporte <c>http</c>, cada fila
/// guarda el id que devolvió el 202, y las dos lecturas de destinatarios le preguntan a
/// notification-service por esos ids.
/// </summary>
public sealed class DeliveryStatusTests : IClassFixture<HttpNotificationFactory>
{
    private readonly HttpNotificationFactory _factory;

    public DeliveryStatusTests(HttpNotificationFactory factory)
    {
        _factory = factory;
        _factory.Crm.ServeDefaults();
        _factory.Notifications.ServeDefaults();
    }

    [Fact]
    public async Task ListContactCampaigns_ReturnsWhatNotificationServiceKnows_WhenTheCampaignWasHandedOff()
    {
        var (ana, luis, marta) = (Contact("Ana"), Contact("Luis"), Contact("Marta"));
        var campaign = await LaunchToAsync(ana, luis, marta);

        // Un rebote sin id de correlación queda huérfano del otro lado: la fila de Marta sigue en sent.
        _factory.Notifications.ServeDelivery(_factory.Notifications.IssuedIds[ana.Email], "delivered");
        _factory.Notifications.ServeDelivery(_factory.Notifications.IssuedIds[luis.Email], "bounced");
        _factory.Notifications.ServeDelivery(_factory.Notifications.IssuedIds[marta.Email], "sent");

        var rows = await GetArrayAsync(
            $"/v1/convocatorias/campaign-recipients?contactId={ana.Id}&contactId={luis.Id}&contactId={marta.Id}");

        DeliveryOf(rows, "contactId", ana.Id, campaign).ShouldBe("delivered");
        DeliveryOf(rows, "contactId", luis.Id, campaign).ShouldBe("bounced");
        DeliveryOf(rows, "contactId", marta.Id, campaign).ShouldBe("sent");
    }

    [Fact]
    public async Task ListCampaignRecipients_ReturnsSpamReportAndUnsubscribed_WhenThePeopleReacted()
    {
        var (ana, luis) = (Contact("Ana"), Contact("Luis"));
        var campaign = await LaunchToAsync(ana, luis);

        _factory.Notifications.ServeDelivery(_factory.Notifications.IssuedIds[ana.Email], "delivered", "complained");
        _factory.Notifications.ServeDelivery(_factory.Notifications.IssuedIds[luis.Email], "delivered", "unsubscribed");

        var page = await GetAsync($"/v1/convocatorias/campaigns/{campaign}/recipients");
        var rows = page.GetProperty("items").EnumerateArray().ToList();

        rows.Single(r => r.GetProperty("email").GetString() == ana.Email)
            .GetProperty("deliveryStatus").GetString().ShouldBe("spam_report");
        rows.Single(r => r.GetProperty("email").GetString() == luis.Email)
            .GetProperty("deliveryStatus").GetString().ShouldBe("unsubscribed");
    }

    private static StubContact Contact(string firstName) =>
        new(Guid.NewGuid(), firstName, "Pérez", $"{firstName.ToLowerInvariant()}.{Guid.NewGuid():N}@acme.com",
            CanBeEmailed: true, SuppressionReason: null);

    private async Task<Guid> LaunchToAsync(params StubContact[] contacts)
    {
        _factory.Crm.ServeContacts(contacts);
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAndLaunchAsync(client, EventsStub.EventId);
        await CampaignTestClient.WaitUntilSentAsync(client, campaign.Id);
        return campaign.Id;
    }

    private async Task<JsonElement> GetAsync(string route)
    {
        var response = await _factory.CreateClient().GetAsync(route);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private async Task<List<JsonElement>> GetArrayAsync(string route) =>
        [.. (await GetAsync(route)).EnumerateArray()];

    private static string? DeliveryOf(List<JsonElement> rows, string key, Guid id, Guid campaignId) =>
        rows.Single(r => r.GetProperty(key).GetGuid() == id && r.GetProperty("campaignId").GetGuid() == campaignId)
            .GetProperty("deliveryStatus").GetString();
}
