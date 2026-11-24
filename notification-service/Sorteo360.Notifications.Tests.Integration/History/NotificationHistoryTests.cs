using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Sorteo360.Notifications.Core.Notifications;
using Sorteo360.Notifications.Tests.Integration.Infrastructure;
using Sorteo360.Notifications.Web.Auth;
using Shouldly;

namespace Sorteo360.Notifications.Tests.Integration.History;

/// <summary>
/// El historial de envíos contra la base real: las tres rutas de lectura, sus filtros, su
/// paginación y quién alcanza qué.
/// <para>
/// Cada test siembra lo suyo desde cero: los tests comparten el contenedor, y un total paginado no
/// prueba nada si arrastra las filas del anterior.
/// </para>
/// </summary>
public sealed class NotificationHistoryTests : IClassFixture<NotificationsApiDbFactory>
{
    private const string ListRoute = "/v1/notifications";
    private const string OrphansRoute = "/v1/notifications/orphan-events";
    private const string BatchesRoute = "/v1/notifications/batches";

    private const string EventsAccount = "service-account-events-service";
    private const string ConvocatoriasAccount = "service-account-convocatorias-service";

    private readonly NotificationsApiDbFactory _factory;
    private readonly NotificationHistorySeed _seed;

    public NotificationHistoryTests(NotificationsApiDbFactory factory)
    {
        _factory = factory;
        _seed = new NotificationHistorySeed(factory.ConnectionString);
    }

    #region La forma de la página

    [Fact]
    public async Task List_ReturnsTotalPagePageSizeAndItems_WhenAPageIsRequested()
    {
        await _seed.ResetAsync();
        var batch = await _seed.AddBatchAsync("events.quote.submitted", [("events", "quote", "q-1")]);
        await _seed.AddRecipientAsync(batch, "maria@sorteo360.local");

        var page = await ReadPage(await Read().GetAsync(ListRoute));

        page!["total"]!.GetValue<int>().ShouldBe(1);
        page["page"]!.GetValue<int>().ShouldBe(1);
        page["pageSize"]!.GetValue<int>().ShouldBe(25);
        page["items"]!.AsArray().Count.ShouldBe(1);
    }

    [Fact]
    public async Task List_Returns25RowsAndTheFullTotal_WhenNoPageSizeIsSent()
    {
        await _seed.ResetAsync();
        var batch = await _seed.AddBatchAsync("events.quote.submitted", [("events", "quote", "q-1")]);

        for (var index = 0; index < 30; index++)
            await _seed.AddRecipientAsync(batch, $"persona{index}@sorteo360.local");

        var page = await ReadPage(await Read().GetAsync(ListRoute));

        page!["pageSize"]!.GetValue<int>().ShouldBe(25);
        page["items"]!.AsArray().Count.ShouldBe(25);
        page["total"]!.GetValue<int>().ShouldBe(30);
    }

    [Fact]
    public async Task List_ReturnsTheRest_WhenTheSecondPageIsRequested()
    {
        await _seed.ResetAsync();
        var batch = await _seed.AddBatchAsync("events.quote.submitted", [("events", "quote", "q-1")]);

        for (var index = 0; index < 30; index++)
            await _seed.AddRecipientAsync(batch, $"persona{index}@sorteo360.local");

        var page = await ReadPage(await Read().GetAsync($"{ListRoute}?page=2"));

        page!["page"]!.GetValue<int>().ShouldBe(2);
        page["items"]!.AsArray().Count.ShouldBe(5);
    }

    [Fact]
    public async Task List_Returns200_WhenThePageSizeIsTheMaximum()
    {
        await _seed.ResetAsync();

        var response = await Read().GetAsync($"{ListRoute}?pageSize=100");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadPage(response))!["pageSize"]!.GetValue<int>().ShouldBe(100);
    }

    [Theory]
    [InlineData(101)]
    [InlineData(1000)]
    [InlineData(0)]
    public async Task List_Returns400_WhenThePageSizeIsOutsideTheLimits(int pageSize)
    {
        await _seed.ResetAsync();

        var response = await Read().GetAsync($"{ListRoute}?pageSize={pageSize}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_Returns400WithTheMachineCode_WhenThePageIsOversized()
    {
        await _seed.ResetAsync();

        var response = await Read().GetAsync($"{ListRoute}?pageSize=101");
        var problem = await response.Content.ReadFromJsonAsync<JsonNode>();

        problem!["code"]!.GetValue<string>().ShouldBe(Core.Errors.NotificationErrors.PayloadInvalidCode);
    }

    [Fact]
    public async Task ListOrphanEvents_Returns400WithTheMachineCode_WhenThePageIsOversized()
    {
        await _seed.ResetAsync();

        var response = await Read().GetAsync($"{OrphansRoute}?pageSize=101");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Los filtros

    [Fact]
    public async Task List_ReturnsOnlyThatType_WhenFilteredByType()
    {
        await _seed.ResetAsync();
        var submitted = await _seed.AddBatchAsync("events.quote.submitted", [("events", "quote", "q-1")]);
        var approved = await _seed.AddBatchAsync("events.quote.approved", [("events", "quote", "q-1")]);
        await _seed.AddRecipientAsync(submitted, "maria@sorteo360.local");
        await _seed.AddRecipientAsync(approved, "jose@sorteo360.local");

        var items = await ReadItems(await Read().GetAsync($"{ListRoute}?type=events.quote.approved"));

        items.Count.ShouldBe(1);
        Text(items[0], "email").ShouldBe("jose@sorteo360.local");
        Text(items[0], "type").ShouldBe("events.quote.approved");
    }

    [Fact]
    public async Task List_ReturnsOnlyThatStatus_WhenFilteredByDeliveryStatus()
    {
        await _seed.ResetAsync();
        var batch = await _seed.AddBatchAsync("events.quote.submitted", [("events", "quote", "q-1")]);
        await _seed.AddRecipientAsync(batch, "queued@sorteo360.local");
        await _seed.AddRecipientAsync(batch, "bounced@sorteo360.local", DeliveryStatus.Bounced);

        var items = await ReadItems(await Read().GetAsync($"{ListRoute}?deliveryStatus={DeliveryStatus.Bounced}"));

        items.Count.ShouldBe(1);
        Text(items[0], "email").ShouldBe("bounced@sorteo360.local");
    }

    [Fact]
    public async Task List_Returns400_WhenTheDeliveryStatusIsUnknown()
    {
        await _seed.ResetAsync();

        var response = await Read().GetAsync($"{ListRoute}?deliveryStatus=entregadisimo");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_ReturnsOnlyThatAction_WhenFilteredByRecipientAction()
    {
        await _seed.ResetAsync();
        var batch = await _seed.AddBatchAsync("events.quote.submitted", [("events", "quote", "q-1")]);
        await _seed.AddRecipientAsync(batch, "callada@sorteo360.local");
        await _seed.AddRecipientAsync(
            batch, "queja@sorteo360.local", DeliveryStatus.Delivered, RecipientAction.Complained);

        var items = await ReadItems(
            await Read().GetAsync($"{ListRoute}?recipientAction={RecipientAction.Complained}"));

        items.Count.ShouldBe(1);
        Text(items[0], "email").ShouldBe("queja@sorteo360.local");
    }

    [Fact]
    public async Task List_Returns400_WhenTheRecipientActionIsUnknown()
    {
        await _seed.ResetAsync();

        var response = await Read().GetAsync($"{ListRoute}?recipientAction=enfadadisima");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_ReturnsOnlyThatService_WhenFilteredBySourceService()
    {
        await _seed.ResetAsync();
        var fromEvents = await _seed.AddBatchAsync("events.quote.submitted", [("events", "quote", "q-1")]);
        var fromConvocatorias = await _seed.AddBatchAsync(
            "convocatorias.registro.confirmado", [("convocatorias", "campaign", "c-1")]);
        await _seed.AddRecipientAsync(fromEvents, "maria@sorteo360.local");
        await _seed.AddRecipientAsync(fromConvocatorias, "jose@sorteo360.local");

        var items = await ReadItems(await Read().GetAsync($"{ListRoute}?sourceService=convocatorias"));

        items.Count.ShouldBe(1);
        Text(items[0], "email").ShouldBe("jose@sorteo360.local");
    }

    [Fact]
    public async Task List_ReturnsOnlyTheRange_WhenFilteredByDate()
    {
        await _seed.ResetAsync();
        var batch = await _seed.AddBatchAsync("events.quote.submitted", [("events", "quote", "q-1")]);
        var old = new DateTimeOffset(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);
        var recent = new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

        await _seed.AddRecipientAsync(batch, "vieja@sorteo360.local", queuedAt: old);
        await _seed.AddRecipientAsync(batch, "reciente@sorteo360.local", queuedAt: recent);

        var items = await ReadItems(await Read().GetAsync(
            $"{ListRoute}?sentFrom=2026-05-01T00:00:00Z&sentTo=2026-07-01T00:00:00Z"));

        items.Count.ShouldBe(1);
        Text(items[0], "email").ShouldBe("reciente@sorteo360.local");
    }

    [Fact]
    public async Task List_Returns400_WhenTheRangeEndsBeforeItStarts()
    {
        await _seed.ResetAsync();

        var response = await Read().GetAsync(
            $"{ListRoute}?sentFrom=2026-07-01T00:00:00Z&sentTo=2026-05-01T00:00:00Z");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_ReturnsTheIntersection_WhenTwoFiltersAreSent()
    {
        await _seed.ResetAsync();
        var batch = await _seed.AddBatchAsync("events.quote.submitted", [("events", "quote", "q-1")]);
        var other = await _seed.AddBatchAsync("events.quote.rejected", [("events", "quote", "q-1")]);
        await _seed.AddRecipientAsync(batch, "maria@sorteo360.local", DeliveryStatus.Bounced);
        await _seed.AddRecipientAsync(batch, "jose@sorteo360.local");
        await _seed.AddRecipientAsync(other, "ana@sorteo360.local", DeliveryStatus.Bounced);

        var items = await ReadItems(await Read().GetAsync(
            $"{ListRoute}?type=events.quote.submitted&deliveryStatus={DeliveryStatus.Bounced}"));

        items.Count.ShouldBe(1);
        Text(items[0], "email").ShouldBe("maria@sorteo360.local");
    }

    #endregion

    #region La pregunta del cliente: todo lo que se mandó sobre esta entidad

    [Fact]
    public async Task List_ReturnsEveryRowOfEveryBatchAboutTheEntity_WhenFilteredByEntity()
    {
        await _seed.ResetAsync();

        // Dos envíos de dos tipos distintos sobre el mismo evento, y un tercero sobre otro.
        var submitted = await _seed.AddBatchAsync(
            "events.quote.submitted", [("events", "event", "feria-madrid"), ("events", "quote", "q-1")]);
        var approved = await _seed.AddBatchAsync(
            "events.quote.approved", [("events", "event", "feria-madrid"), ("events", "quote", "q-2")]);
        var otherEvent = await _seed.AddBatchAsync(
            "events.quote.submitted", [("events", "event", "feria-berlin")]);

        await _seed.AddRecipientAsync(submitted, "maria@sorteo360.local");
        await _seed.AddRecipientAsync(approved, "jose@sorteo360.local");
        await _seed.AddRecipientAsync(otherEvent, "ana@sorteo360.local");

        var items = await ReadItems(await Read().GetAsync(
            $"{ListRoute}?entityType=event&entityId=feria-madrid"));

        items.Count.ShouldBe(2);

        items.Select(item => Guid.Parse(Text(item, "batchId")!))
            .ShouldBe([submitted, approved], ignoreOrder: true);

        items.Select(item => Text(item, "type"))
            .ShouldBe(["events.quote.submitted", "events.quote.approved"], ignoreOrder: true);

        items.Select(item => Text(item, "email"))
            .ShouldNotContain("ana@sorteo360.local");
    }

    [Fact]
    public async Task List_ReturnsEveryEntityOfTheBatchOnTheRow_WhenTheBatchNamesSeveral()
    {
        await _seed.ResetAsync();
        var batch = await _seed.AddBatchAsync(
            "events.quote.submitted", [("events", "event", "feria-madrid"), ("events", "quote", "q-1")]);
        await _seed.AddRecipientAsync(batch, "maria@sorteo360.local");

        var items = await ReadItems(await Read().GetAsync(ListRoute));

        var about = items[0]!["about"]!.AsArray();
        about.Count.ShouldBe(2);
        about.Select(entry => Text(entry, "entityType")).ShouldBe(["event", "quote"], ignoreOrder: true);
        about.Select(entry => Text(entry, "sourceService")).ShouldAllBe(source => source == "events");
    }

    [Fact]
    public async Task List_Returns400_WhenOnlyHalfOfTheEntityFilterIsSent()
    {
        await _seed.ResetAsync();

        (await Read().GetAsync($"{ListRoute}?entityType=event")).StatusCode
            .ShouldBe(HttpStatusCode.BadRequest);

        (await Read().GetAsync($"{ListRoute}?entityId=feria-madrid")).StatusCode
            .ShouldBe(HttpStatusCode.BadRequest);
    }

    #endregion

    #region La dirección es exacta

    [Fact]
    public async Task List_ReturnsOnlyTheExactAddress_WhenFilteredByEmail()
    {
        await _seed.ResetAsync();
        var batch = await _seed.AddBatchAsync("events.quote.submitted", [("events", "quote", "q-1")]);
        await _seed.AddRecipientAsync(batch, "maria@sorteo360.local");
        await _seed.AddRecipientAsync(batch, "maria.perez@sorteo360.local");

        var items = await ReadItems(await Read().GetAsync($"{ListRoute}?email=maria@sorteo360.local"));

        items.Count.ShouldBe(1);
        Text(items[0], "email").ShouldBe("maria@sorteo360.local");
    }

    [Fact]
    public async Task List_ReturnsNothing_WhenFilteredByAFragmentOfAnAddress()
    {
        await _seed.ResetAsync();
        var batch = await _seed.AddBatchAsync("events.quote.submitted", [("events", "quote", "q-1")]);
        await _seed.AddRecipientAsync(batch, "maria@sorteo360.local");

        var items = await ReadItems(await Read().GetAsync($"{ListRoute}?email=maria"));

        items.ShouldBeEmpty();
    }

    [Fact]
    public async Task List_ReturnsNothing_WhenTheEmailFilterDiffersOnlyInCase()
    {
        await _seed.ResetAsync();
        var batch = await _seed.AddBatchAsync("events.quote.submitted", [("events", "quote", "q-1")]);
        await _seed.AddRecipientAsync(batch, "Maria@sorteo360.local");

        var items = await ReadItems(await Read().GetAsync($"{ListRoute}?email=maria@sorteo360.local"));

        items.ShouldBeEmpty();
    }

    #endregion

    #region Por id

    [Fact]
    public async Task List_ReturnsOnlyThoseRows_WhenFilteredByRepeatedIds()
    {
        await _seed.ResetAsync();
        var batch = await _seed.AddBatchAsync("convocatorias.campaign", [("convocatorias", "campaign", "c-1")]);
        var maria = await _seed.AddRecipientAsync(batch, "maria@sorteo360.local", deliveryStatus: DeliveryStatus.Delivered);
        var jose = await _seed.AddRecipientAsync(batch, "jose@sorteo360.local", deliveryStatus: DeliveryStatus.Bounced);
        await _seed.AddRecipientAsync(batch, "ana@sorteo360.local");

        var items = await ReadItems(await Read(ConvocatoriasAccount).GetAsync($"{ListRoute}?id={maria}&id={jose}"));

        items.Select(item => Text(item, "id")).ShouldBe([maria.ToString(), jose.ToString()], ignoreOrder: true);
    }

    [Fact]
    public async Task List_ReturnsNothing_WhenAServiceAccountAsksForAnotherServiceRowById()
    {
        await _seed.ResetAsync();
        var fromEvents = await _seed.AddBatchAsync("events.quote.submitted", [("events", "quote", "q-1")]);
        var maria = await _seed.AddRecipientAsync(fromEvents, "maria@sorteo360.local");

        var items = await ReadItems(await Read(ConvocatoriasAccount).GetAsync($"{ListRoute}?id={maria}"));

        items.ShouldBeEmpty();
    }

    [Fact]
    public async Task List_Returns400_WhenMoreThanAHundredIdsAreSent()
    {
        var ids = string.Join('&', Enumerable.Range(0, 101).Select(_ => $"id={Guid.NewGuid()}"));

        var response = await Read().GetAsync($"{ListRoute}?{ids}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    #endregion

    #region El envío

    [Fact]
    public async Task GetBatch_ReturnsTheStoredBodyWithItsPlaceholders_WhenTheBatchExists()
    {
        await _seed.ResetAsync();
        const string Body = """<html><p>Hola {{nombre}}, <a href="{{enlace}}">ver</a></p></html>""";
        var batch = await _seed.AddBatchAsync(
            "events.quote.submitted",
            [("events", "quote", "q-1")],
            subject: "Cotización de {{evento}}",
            body: Body);
        await _seed.AddRecipientAsync(batch, "maria@sorteo360.local");

        var response = await Read().GetAsync($"{BatchesRoute}/{batch}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var found = (await response.Content.ReadFromJsonAsync<JsonNode>())!;

        Text(found, "body").ShouldBe(Body);
        Text(found, "subject").ShouldBe("Cotización de {{evento}}");
        found["recipients"]!.AsArray().Count.ShouldBe(1);
        found["about"]!.AsArray().Count.ShouldBe(1);
    }

    [Fact]
    public async Task List_ReturnsTheRecipientName_WhenTheSendCarriedOne()
    {
        await _seed.ResetAsync();
        var batch = await _seed.AddBatchAsync("events.quote.submitted", [("events", "quote", "q-1")]);
        await _seed.AddRecipientAsync(batch, "maria@sorteo360.local", name: "María González");
        await _seed.AddRecipientAsync(batch, "jose@sorteo360.local");

        var items = await ReadItems(await Read().GetAsync(ListRoute));
        Text(items.Single(item => Text(item, "email") == "maria@sorteo360.local"), "name").ShouldBe("María González");
        Text(items.Single(item => Text(item, "email") == "jose@sorteo360.local"), "name").ShouldBeNull();
    }

    [Fact]
    public async Task GetBatch_ReturnsEachRecipientName_WhenTheSendCarriedOne()
    {
        await _seed.ResetAsync();
        var batch = await _seed.AddBatchAsync("events.quote.submitted", [("events", "quote", "q-1")]);
        await _seed.AddRecipientAsync(batch, "maria@sorteo360.local", name: "María González");
        await _seed.AddRecipientAsync(batch, "jose@sorteo360.local");

        var found = (await Read().GetFromJsonAsync<JsonNode>($"{BatchesRoute}/{batch}"))!;
        var recipients = found["recipients"]!.AsArray();
        Text(recipients.Single(item => Text(item, "email") == "maria@sorteo360.local"), "name").ShouldBe("María González");
        Text(recipients.Single(item => Text(item, "email") == "jose@sorteo360.local"), "name").ShouldBeNull();
    }

    [Fact]
    public async Task GetBatch_ReturnsTheBatch_WhenCalledAtTheLocationOfA202()
    {
        await _seed.ResetAsync();
        var client = _factory.CreateClient();

        var accepted = await client.PostAsJsonAsync(ListRoute, new Dictionary<string, object?>
        {
            ["type"] = "events.quote.submitted",
            ["language"] = "es",
            ["fromAddress"] = "no-reply@sorteo360.local",
            ["fromName"] = "MITUR",
            ["subject"] = "Cotización pendiente",
            ["body"] = "<html><p>Hola {{nombre}}</p></html>",
            ["sharedData"] = new Dictionary<string, string> { ["nombre"] = "María" },
            ["recipients"] = new[] { new Dictionary<string, object?> { ["email"] = "maria@sorteo360.local" } },
            ["about"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["sourceService"] = "events",
                    ["entityType"] = "quote",
                    ["entityId"] = "q-1"
                }
            },
            ["idempotencyKey"] = $"tests:history:{Guid.NewGuid()}"
        });

        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var location = accepted.Headers.Location!.ToString();
        var response = await Read().GetAsync(location);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var found = (await response.Content.ReadFromJsonAsync<JsonNode>())!;

        // Lo que se guardó es el cuerpo sin sustituir, no el que le llegó a la persona.
        Text(found, "body").ShouldBe("<html><p>Hola {{nombre}}</p></html>");
    }

    [Fact]
    public async Task GetBatch_Returns404_WhenTheBatchDoesNotExist()
    {
        await _seed.ResetAsync();

        var response = await Read().GetAsync($"{BatchesRoute}/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    #endregion

    #region Quién ve qué

    [Fact]
    public async Task List_ReturnsEverySourceService_WhenAPersonHoldsTheReadRole()
    {
        await _seed.ResetAsync();
        var fromEvents = await _seed.AddBatchAsync("events.quote.submitted", [("events", "quote", "q-1")]);
        var fromConvocatorias = await _seed.AddBatchAsync(
            "convocatorias.registro.confirmado", [("convocatorias", "campaign", "c-1")]);
        await _seed.AddRecipientAsync(fromEvents, "maria@sorteo360.local");
        await _seed.AddRecipientAsync(fromConvocatorias, "jose@sorteo360.local");

        var items = await ReadItems(await Read().GetAsync(ListRoute));

        items.Count.ShouldBe(2);
        items.Select(item => Text(item, "email"))
            .ShouldBe(["maria@sorteo360.local", "jose@sorteo360.local"], ignoreOrder: true);
    }

    [Fact]
    public async Task List_ReturnsOnlyItsOwnServiceRows_WhenTheCallerIsAServiceAccount()
    {
        await _seed.ResetAsync();
        var fromEvents = await _seed.AddBatchAsync("events.quote.submitted", [("events", "quote", "q-1")]);
        var fromConvocatorias = await _seed.AddBatchAsync(
            "convocatorias.registro.confirmado", [("convocatorias", "campaign", "c-1")]);
        await _seed.AddRecipientAsync(fromEvents, "maria@sorteo360.local");
        await _seed.AddRecipientAsync(fromConvocatorias, "jose@sorteo360.local");

        var items = await ReadItems(await Read(EventsAccount).GetAsync(ListRoute));

        items.Count.ShouldBe(1);
        Text(items[0], "email").ShouldBe("maria@sorteo360.local");
    }

    [Fact]
    public async Task GetBatch_Returns404_WhenAServiceAccountAsksForAnotherServiceBatch()
    {
        await _seed.ResetAsync();
        var fromConvocatorias = await _seed.AddBatchAsync(
            "convocatorias.registro.confirmado", [("convocatorias", "campaign", "c-1")]);
        await _seed.AddRecipientAsync(fromConvocatorias, "jose@sorteo360.local");

        // 404 y no 403: un 403 diría que el envío existe, y eso tampoco es suyo.
        (await Read(EventsAccount).GetAsync($"{BatchesRoute}/{fromConvocatorias}")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);

        (await Read(ConvocatoriasAccount).GetAsync($"{BatchesRoute}/{fromConvocatorias}")).StatusCode
            .ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task List_ReturnsOnlyItsOwnEntities_WhenAServiceAccountReadsASharedBatch()
    {
        await _seed.ResetAsync();

        // Un envío puede tratar de cosas de dos servicios. Alcanzarlo no es alcanzar los ids del
        // otro, así que la lista de entidades sale recortada a lo de quien pregunta.
        var shared = await _seed.AddBatchAsync(
            "events.quote.submitted",
            [("events", "quote", "q-1"), ("convocatorias", "campaign", "c-secreta")]);
        await _seed.AddRecipientAsync(shared, "maria@sorteo360.local");

        var scoped = await ReadItems(await Read(EventsAccount).GetAsync(ListRoute));

        scoped.Count.ShouldBe(1);

        var about = scoped[0]!["about"]!.AsArray();
        about.Count.ShouldBe(1);
        Text(about[0], "sourceService").ShouldBe("events");
        about.Select(entry => Text(entry, "entityId")).ShouldNotContain("c-secreta");

        // La persona con el rol sí ve las dos.
        var everything = await ReadItems(await Read().GetAsync(ListRoute));
        everything[0]!["about"]!.AsArray().Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetBatch_ReturnsOnlyItsOwnEntities_WhenAServiceAccountReadsASharedBatch()
    {
        await _seed.ResetAsync();
        var shared = await _seed.AddBatchAsync(
            "events.quote.submitted",
            [("events", "quote", "q-1"), ("convocatorias", "campaign", "c-secreta")]);
        await _seed.AddRecipientAsync(shared, "maria@sorteo360.local");

        var found = (await (await Read(EventsAccount).GetAsync($"{BatchesRoute}/{shared}"))
            .Content.ReadFromJsonAsync<JsonNode>())!;

        var about = found["about"]!.AsArray();
        about.Count.ShouldBe(1);
        Text(about[0], "sourceService").ShouldBe("events");
    }

    [Fact]
    public async Task GetBatch_ReturnsTheBatch_WhenAServiceAccountAsksForItsOwn()
    {
        await _seed.ResetAsync();
        var fromEvents = await _seed.AddBatchAsync("events.quote.submitted", [("events", "quote", "q-1")]);
        await _seed.AddRecipientAsync(fromEvents, "maria@sorteo360.local");

        var response = await Read(EventsAccount).GetAsync($"{BatchesRoute}/{fromEvents}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task List_Returns403_WhenTheTokenLacksTheReadRole()
    {
        await _seed.ResetAsync();
        var client = _factory.CreateClient();

        foreach (var route in new[] { ListRoute, OrphansRoute, $"{BatchesRoute}/{Guid.NewGuid()}" })
        {
            var request = new HttpRequestMessage(HttpMethod.Get, route);
            request.Headers.Add(TestAuthHandler.RolesHeader, AuthorizationPolicies.NotificationsSend);

            var response = await client.SendAsync(request);

            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, route);
        }
    }

    [Fact]
    public async Task List_Returns401_WhenCalledWithoutAToken()
    {
        await _seed.ResetAsync();
        var client = _factory.CreateClient();

        foreach (var route in new[] { ListRoute, OrphansRoute, $"{BatchesRoute}/{Guid.NewGuid()}" })
        {
            var request = new HttpRequestMessage(HttpMethod.Get, route);
            request.Headers.Add(TestAuthHandler.AnonymousHeader, "true");

            var response = await client.SendAsync(request);

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, route);
        }
    }

    #endregion

    #region Lo que la pantalla no puede dar por sentado

    [Fact]
    public async Task List_ReturnsTheTrackingFlagOnEveryRow_WhenRead()
    {
        await _seed.ResetAsync();
        var tracked = await _seed.AddBatchAsync(
            "events.quote.submitted", [("events", "quote", "q-1")], trackingExpected: true);
        var untracked = await _seed.AddBatchAsync(
            "events.quote.approved", [("events", "quote", "q-2")], trackingExpected: false);
        await _seed.AddRecipientAsync(tracked, "conseguimiento@sorteo360.local");
        await _seed.AddRecipientAsync(untracked, "sinseguimiento@sorteo360.local");

        var items = await ReadItems(await Read().GetAsync(ListRoute));

        items.Count.ShouldBe(2);
        items.ShouldAllBe(item => item!["deliveryTrackingAvailable"] != null);

        var byEmail = items.ToDictionary(item => Text(item, "email")!);
        byEmail["conseguimiento@sorteo360.local"]!["deliveryTrackingAvailable"]!.GetValue<bool>().ShouldBeTrue();
        byEmail["sinseguimiento@sorteo360.local"]!["deliveryTrackingAvailable"]!.GetValue<bool>().ShouldBeFalse();
    }

    [Fact]
    public async Task List_ReturnsNothingPastSent_WhenTheBatchIsUntracked()
    {
        await _seed.ResetAsync();
        var untracked = await _seed.AddBatchAsync(
            "events.quote.submitted", [("events", "quote", "q-1")], trackingExpected: false);
        var recipient = await _seed.AddRecipientAsync(
            untracked, "maria@sorteo360.local", DeliveryStatus.Sent, sentAt: DateTimeOffset.UtcNow);

        // Se le da de comer el evento que sí movería la fila de un envío con seguimiento.
        await _seed.ApplyProviderEventAsync(recipient, "delivered", DeliveryStatus.Delivered);

        var items = await ReadItems(await Read().GetAsync(ListRoute));

        items.Count.ShouldBe(1);
        Text(items[0], "deliveryStatus").ShouldBe(DeliveryStatus.Sent);
        items[0]!["deliveryTrackingAvailable"]!.GetValue<bool>().ShouldBeFalse();
    }

    [Fact]
    public async Task List_ReturnsTheProviderStatus_WhenTheBatchExpectedTracking()
    {
        await _seed.ResetAsync();
        var tracked = await _seed.AddBatchAsync(
            "events.quote.submitted", [("events", "quote", "q-1")], trackingExpected: true);
        var recipient = await _seed.AddRecipientAsync(
            tracked, "maria@sorteo360.local", DeliveryStatus.Sent, sentAt: DateTimeOffset.UtcNow);

        await _seed.ApplyProviderEventAsync(recipient, "delivered", DeliveryStatus.Delivered);

        var items = await ReadItems(await Read().GetAsync(ListRoute));

        Text(items[0], "deliveryStatus").ShouldBe(DeliveryStatus.Delivered);
    }

    [Fact]
    public async Task List_ReturnsNoRowReportingSent_WhenFilteredByDelivered()
    {
        await _seed.ResetAsync();
        var untracked = await _seed.AddBatchAsync(
            "events.quote.submitted", [("events", "quote", "q-1")], trackingExpected: false);
        var recipient = await _seed.AddRecipientAsync(untracked, "maria@sorteo360.local", DeliveryStatus.Sent);
        await _seed.ApplyProviderEventAsync(recipient, "delivered", DeliveryStatus.Delivered);

        // Lo que se filtra es lo que se ve: si la fila se reporta 'sent', 'delivered' no la trae.
        (await ReadItems(await Read().GetAsync($"{ListRoute}?deliveryStatus={DeliveryStatus.Delivered}")))
            .ShouldBeEmpty();

        (await ReadItems(await Read().GetAsync($"{ListRoute}?deliveryStatus={DeliveryStatus.Sent}")))
            .Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetBatch_ReturnsNoTrackingAndCappedRecipients_WhenTheBatchExpectedNoTracking()
    {
        await _seed.ResetAsync();
        var untracked = await _seed.AddBatchAsync(
            "events.quote.submitted", [("events", "quote", "q-1")], trackingExpected: false);
        var recipient = await _seed.AddRecipientAsync(untracked, "maria@sorteo360.local", DeliveryStatus.Sent);
        await _seed.ApplyProviderEventAsync(recipient, "bounce", DeliveryStatus.Bounced);

        var found = (await (await Read().GetAsync($"{BatchesRoute}/{untracked}"))
            .Content.ReadFromJsonAsync<JsonNode>())!;

        found["deliveryTrackingAvailable"]!.GetValue<bool>().ShouldBeFalse();
        Text(found["recipients"]!.AsArray()[0], "deliveryStatus").ShouldBe(DeliveryStatus.Sent);
    }

    #endregion

    #region Los huérfanos

    [Fact]
    public async Task ListOrphanEvents_ReturnsTheEventsThatNameNoRow_WhenRead()
    {
        await _seed.ResetAsync();
        await _seed.AddEventAsync("bounce", recipientId: null, sgEventId: "sg-1");
        await _seed.AddEventAsync("dropped", recipientId: null, sgEventId: "sg-2");

        var page = await ReadPage(await Read().GetAsync(OrphansRoute));

        page!["total"]!.GetValue<int>().ShouldBe(2);

        var items = page["items"]!.AsArray();
        items.Select(item => Text(item, "eventType")).ShouldBe(["bounce", "dropped"], ignoreOrder: true);
        items.Select(item => Text(item, "sgEventId")).ShouldBe(["sg-1", "sg-2"], ignoreOrder: true);
    }

    [Fact]
    public async Task ListOrphanEvents_ReturnsTheRawJsonAsJson_WhenRead()
    {
        await _seed.ResetAsync();
        await _seed.AddEventAsync(
            "bounce", payload: """{"event":"bounce","email":"nadie@ejemplo.com","reason":"550"}""");

        var items = (await ReadPage(await Read().GetAsync(OrphansRoute)))!["items"]!.AsArray();

        items[0]!["payload"]!["reason"]!.GetValue<string>().ShouldBe("550");
    }

    [Fact]
    public async Task ListOrphanEvents_ReturnsNoCorrelatedEvent_WhenTheEventNamesARow()
    {
        await _seed.ResetAsync();
        var batch = await _seed.AddBatchAsync("events.quote.submitted", [("events", "quote", "q-1")]);
        var recipient = await _seed.AddRecipientAsync(batch, "maria@sorteo360.local", DeliveryStatus.Sent);

        await _seed.AddEventAsync("delivered", recipient, sgEventId: "sg-linked");
        await _seed.AddEventAsync("bounce", recipientId: null, sgEventId: "sg-orphan");

        var orphans = (await ReadPage(await Read().GetAsync(OrphansRoute)))!["items"]!.AsArray();

        orphans.Count.ShouldBe(1);
        Text(orphans[0], "sgEventId").ShouldBe("sg-orphan");
    }

    [Fact]
    public async Task List_ReturnsNoOrphan_WhenOrphansExist()
    {
        await _seed.ResetAsync();
        await _seed.AddEventAsync("bounce", recipientId: null, sgEventId: "sg-orphan");

        var page = await ReadPage(await Read().GetAsync(ListRoute));

        page!["total"]!.GetValue<int>().ShouldBe(0);
        page["items"]!.AsArray().ShouldBeEmpty();
    }

    [Fact]
    public async Task ListOrphanEvents_ReturnsNothing_WhenTheCallerIsAServiceAccount()
    {
        await _seed.ResetAsync();
        await _seed.AddEventAsync("bounce", recipientId: null, sgEventId: "sg-orphan");

        var page = await ReadPage(await Read(EventsAccount).GetAsync(OrphansRoute));

        page!["total"]!.GetValue<int>().ShouldBe(0);
        page["items"]!.AsArray().ShouldBeEmpty();
    }

    #endregion

    #region Ayudas

    /// <summary>
    /// Un cliente que solo lleva el rol de lectura. Por defecto es una persona; con
    /// <paramref name="username"/> se puede pedir el token de una cuenta de servicio.
    /// </summary>
    private HttpClient Read(string? username = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            TestAuthHandler.RolesHeader, AuthorizationPolicies.NotificationsRead);

        if (username is not null)
            client.DefaultRequestHeaders.Add(TestAuthHandler.UsernameHeader, username);

        return client;
    }

    private static async Task<JsonNode?> ReadPage(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return await response.Content.ReadFromJsonAsync<JsonNode>();
    }

    private static async Task<IReadOnlyList<JsonNode?>> ReadItems(HttpResponseMessage response) =>
        [.. (await ReadPage(response))!["items"]!.AsArray()];

    private static string? Text(JsonNode? node, string property) => node?[property]?.GetValue<string>();

    #endregion
}
