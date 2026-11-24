using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Infrastructure.Database;
using Sorteo360.Convocatorias.Tests.Integration.CampaignEndpoints;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.ReconciliationEndpoints;

/// <summary>
/// Las campañas varadas (spec <c>07</c> §6.3): en <c>sending</c>, con alguna fila <c>pending</c> y
/// lanzadas hace más que el umbral. Es lo que deja un redespliegue a mitad de envío, porque la cola
/// del worker vive en memoria (ADR-0018).
/// <para>
/// El redespliegue se simula en dos pasos: un primer lote que falla deja la campaña en
/// <c>sending</c> con todo <c>pending</c>, y el SQL marca una fila como ya entregada y atrasa
/// <c>launched_at</c>. Ninguna ruta produce ese estado, y es justo el que el cubo existe para ver.
/// </para>
/// </summary>
public sealed class StrandedCampaignTests : IClassFixture<CampaignHandoffFactory>
{
    private const string BucketRoute = "/v1/convocatorias/reconciliation/stranded-campaigns?pageSize=100";

    private static readonly StubContact Ana =
        new(Guid.NewGuid(), "Ana", "Pérez", "ana.stranded@acme.com", CanBeEmailed: true, SuppressionReason: null);

    private static readonly StubContact Luis =
        new(Guid.NewGuid(), "Luis", "Gómez", "luis.stranded@acme.com", CanBeEmailed: true, SuppressionReason: null);

    private readonly CampaignHandoffFactory _factory;

    public StrandedCampaignTests(CampaignHandoffFactory factory)
    {
        _factory = factory;
        _factory.Crm.ServeDefaults();
        _factory.Gateway.Reset();
    }

    [Fact]
    public async Task ResumeHandoff_Returns202AndClearsTheStrandedCampaignWithoutRemailing_WhenAHandedOffRowExists()
    {
        var client = _factory.CreateClient();
        var campaignId = await StrandAsync(client, launchedMinutesAgo: 60);

        var row = await BucketRowAsync(client, campaignId);
        row.ShouldNotBeNull();
        row.Value.GetProperty("eventId").GetGuid().ShouldBe(EventsStub.EventId);
        row.Value.GetProperty("launchedAt").GetDateTimeOffset().ShouldBeLessThan(DateTimeOffset.UtcNow.AddMinutes(-59));
        var recipients = row.Value.GetProperty("recipients");
        recipients.GetProperty("pending").GetInt32().ShouldBe(1);
        recipients.GetProperty("handedOff").GetInt32().ShouldBe(1);
        (await client.GetFromJsonAsync<JsonElement>("/v1/convocatorias/reconciliation"))
            .GetProperty("strandedCampaigns").GetInt32().ShouldBeGreaterThanOrEqualTo(1);

        (await CampaignTestClient.ResumeHandoffAsync(client, campaignId)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await CampaignTestClient.WaitUntilSentAsync(client, campaignId);

        _factory.Gateway.Requests.Count.ShouldBe(2);
        _factory.Gateway.Requests[1].Recipients.ShouldHaveSingleItem().Email.ShouldBe(Luis.Email);
        (await BucketRowAsync(client, campaignId)).ShouldBeNull();
    }

    /// <summary>
    /// Un worker vivo a mitad de lote no es una campaña varada: el umbral es lo que las separa, sin
    /// tabla de arriendos ni latido (spec <c>07</c> §6.3).
    /// </summary>
    [Fact]
    public async Task StrandedCampaigns_ReturnsNoRowForTheCampaign_WhenItWasLaunchedWithinTheThreshold()
    {
        var client = _factory.CreateClient();
        var campaignId = await StrandAsync(client, launchedMinutesAgo: 0);

        (await BucketRowAsync(client, campaignId)).ShouldBeNull();
    }

    /// <summary>
    /// Lanza a Ana y a Luis con un primer lote que falla, y deja a Ana como si su lote hubiera
    /// llegado antes del corte.
    /// </summary>
    private async Task<Guid> StrandAsync(HttpClient client, int launchedMinutesAgo)
    {
        _factory.Crm.ServeContacts(Ana, Luis);
        _factory.Gateway.FailNextCalls(1);

        var campaign = await CampaignTestClient.CreateAndLaunchAsync(client, EventsStub.EventId);
        await WaitUntilAsync(() => _factory.Gateway.Requests.Count >= 1);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();
        await context.Database.ExecuteSqlAsync($"""
            UPDATE convocatorias.campaign_recipient SET send_status = {RecipientSendStatus.HandedOff}
            WHERE campaign_id = {campaign.Id} AND email = {Ana.Email}
            """);
        await context.Database.ExecuteSqlAsync($"""
            UPDATE convocatorias.campaign
            SET launched_at = now() - make_interval(mins => {launchedMinutesAgo})
            WHERE id = {campaign.Id}
            """);

        return campaign.Id;
    }

    private static async Task<JsonElement?> BucketRowAsync(HttpClient client, Guid campaignId)
    {
        var body = await client.GetFromJsonAsync<JsonElement>(BucketRoute);

        return body.GetProperty("items").EnumerateArray()
            .Where(item => item.GetProperty("campaignId").GetGuid() == campaignId)
            .Select(item => (JsonElement?)item)
            .SingleOrDefault();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);

        while (!condition())
        {
            if (DateTimeOffset.UtcNow > deadline)
                throw new TimeoutException("La condición no se cumplió a tiempo.");

            await Task.Delay(100);
        }
    }
}
