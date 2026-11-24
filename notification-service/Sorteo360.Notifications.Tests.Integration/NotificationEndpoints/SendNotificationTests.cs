using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Sorteo360.Notifications.Tests.Integration.Infrastructure;
using Sorteo360.Notifications.Web.Auth;
using Shouldly;

namespace Sorteo360.Notifications.Tests.Integration.NotificationEndpoints;

/// <summary>
/// <c>POST /v1/notifications</c> de punta a punta hasta el 202, contra la base real y las
/// migraciones reales.
/// <para>
/// El 202 es la línea, así que cada test de rechazo comprueba las dos mitades: el código que
/// contesta y que no quedó ni una fila.
/// </para>
/// </summary>
public sealed class SendNotificationTests : IClassFixture<NotificationsApiDbFactory>
{
    private const string Route = "/v1/notifications";

    private readonly NotificationsApiDbFactory _factory;
    private readonly NotificationsDatabase _database;

    public SendNotificationTests(NotificationsApiDbFactory factory)
    {
        _factory = factory;
        _database = new NotificationsDatabase(factory.ConnectionString);
    }

    #region El camino bueno

    [Fact]
    public async Task Send_Returns202WithLocationAndWritesTheRecord_WhenThePostIsValid()
    {
        var key = Key();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(Route, Payload(
            key,
            subject: "Cotización pendiente — {{evento}}",
            body: """<html><p>{{categoria}}: <a href="{{enlace}}">Ver</a></p></html>""",
            sharedData: new Dictionary<string, string>
            {
                ["evento"] = "Feria de Turismo Madrid 2026",
                ["categoria"] = "Ambientación",
                ["enlace"] = "https://app.pgti360.sorteo360.local/eventos/xyz-789"
            },
            recipients: [Person("maria@sorteo360.local"), Person("jose@sorteo360.local")],
            about:
            [
                About("events", "event", "xyz-789"),
                About("events", "quote", "abc-123")
            ]));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var accepted = await Accepted(response);
        response.Headers.Location!.ToString().ShouldBe($"/v1/notifications/batches/{accepted.BatchId}");

        accepted.Results.Count.ShouldBe(2);
        accepted.Results.Select(result => result.Recipient).ShouldBe(["maria@sorteo360.local", "jose@sorteo360.local"]);
        accepted.Results.ShouldAllBe(result => result.Status == "accepted");
        accepted.Results.ShouldAllBe(result => result.Id != null);

        (await _database.CountBatchesAsync(key)).ShouldBe(1);
        (await _database.CountAboutAsync(accepted.BatchId)).ShouldBe(2);
        (await _database.CountRecipientsAsync(accepted.BatchId)).ShouldBe(2);

        var storedIds = await _database.RecipientIdsAsync(accepted.BatchId);
        storedIds.Order().ShouldBe(accepted.Results.Select(result => result.Id!.Value).Order());
    }

    [Fact]
    public async Task Send_Returns202_WhenThePeopleAreExactlyAtTheCap()
    {
        var key = Key();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(Route, Payload(
            key,
            recipients: [.. Enumerable.Range(1, 1000).Select(index => Person($"persona{index}@sorteo360.local"))]));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var accepted = await Accepted(response);
        accepted.Results.Count.ShouldBe(1000);
        (await _database.CountRecipientsAsync(accepted.BatchId)).ShouldBe(1000);
    }

    [Fact]
    public async Task Send_Returns202_WhenAnUnknownFieldAndDataKeyAreSent()
    {
        // Un cliente más nuevo tiene que poder hablarle a un servicio más viejo: lo que no se
        // conoce se ignora, nunca se rechaza.
        var key = Key();
        var client = _factory.CreateClient();

        var payload = new Dictionary<string, object?>(Payload(key))
        {
            ["campoQueNadieConoce"] = "algo"
        };

        payload["sharedData"] = new Dictionary<string, string> { ["claveQueSobra"] = "valor" };

        var response = await client.PostAsJsonAsync(Route, payload);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await _database.CountBatchesAsync(key)).ShouldBe(1);
    }

    #endregion

    #region El nombre de la persona

    [Fact]
    public async Task Send_Returns202AndStoresTheTrimmedName_WhenARecipientCarriesAName()
    {
        var key = Key();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(Route, Payload(
            key,
            recipients:
            [
                Named("maria@sorteo360.local", "  María González  "),
                Named("jose@sorteo360.local", "   "),
                Person("ana@sorteo360.local")
            ]));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        // Un nombre vacío cuenta como ausente: nunca se guarda una cadena en blanco.
        var names = await _database.RecipientNamesAsync((await Accepted(response)).BatchId);
        names["maria@sorteo360.local"].ShouldBe("María González");
        names["jose@sorteo360.local"].ShouldBeNull();
        names["ana@sorteo360.local"].ShouldBeNull();
    }

    [Fact]
    public async Task Send_Returns400WithThePayloadCode_WhenANameIsLongerThan200Characters()
    {
        var key = Key();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(Route, Payload(
            key,
            recipients: [Named("maria@sorteo360.local", new string('a', 201))]));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Code(response)).ShouldBe("NOTIFICATION_PAYLOAD_INVALID");
        (await _database.CountBatchesAsync(key)).ShouldBe(0);
    }

    [Fact]
    public async Task Send_Returns409_WhenTheSameKeyArrivesWithAnotherName()
    {
        var key = Key();
        var client = _factory.CreateClient();

        await client.PostAsJsonAsync(Route, Payload(key, recipients: [Named("maria@sorteo360.local", "María González")]));
        var response = await client.PostAsJsonAsync(Route, Payload(key, recipients: [Named("maria@sorteo360.local", "María Pérez")]));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Code(response)).ShouldBe("NOTIFICATION_IDEMPOTENCY_KEY_REUSED");
        (await _database.CountBatchesAsync(key)).ShouldBe(1);
    }

    #endregion

    #region Lo que se guarda, y lo que no

    [Fact]
    public async Task Send_ReturnsAStoredBodyWithItsPlaceholders_WhenAccepted()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(Route, Payload(
            Key(),
            subject: "Aviso de {{evento}}",
            body: "<p>Hola {{nombre}}</p>",
            sharedData: new Dictionary<string, string> { ["evento"] = "Feria", ["nombre"] = "María" }));

        var accepted = await Accepted(response);

        (await _database.BodyAsync(accepted.BatchId)).ShouldBe("<p>Hola {{nombre}}</p>");
        (await _database.SubjectAsync(accepted.BatchId)).ShouldBe("Aviso de {{evento}}");
    }

    [Fact]
    public async Task Send_ReturnsNoTableHoldingADataValue_WhenAccepted()
    {
        // El valor lleva un token reconocible y se busca en todas las columnas de las cuatro tablas
        // del registro. Si algún día alguien agrega una columna que guarde data, este test lo dice.
        // La quinta tabla, notification_payload, sí lo contiene y queda fuera a propósito: no es
        // registro, es lo que el worker necesita para sustituir, y se borra en la misma transacción
        // que deja el envío en un estado terminal. Que después de mandar no quede nada lo comprueba
        // Sending/DrainWorkerTests.DrainAsync_ReturnsARowWithNoSubstitutionValues_WhenTheRowIsSent.
        const string token = "TOKEN-QUE-NO-DEBE-QUEDAR-GUARDADO-7f3a";
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(Route, Payload(
            Key(),
            body: "<p>{{credencial}}</p>",
            recipients: [Person("maria@sorteo360.local", ("credencial", token))]));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var rows = await _database.EveryRowAsTextAsync();
        rows.ShouldNotBeEmpty();
        rows.ShouldAllBe(row => !row.Contains(token));
    }

    [Fact]
    public async Task Send_ReturnsTheTrackingStampedAtInsert_WhenTheSettingIsFlippedLater()
    {
        var client = _factory.CreateClient();

        // Sin webhook configurado: la fila nace diciendo que no espera noticias de entrega.
        var withoutWebhook = await Accepted(await client.PostAsJsonAsync(Route, Payload(Key())));
        (await _database.TrackingExpectedAsync(withoutWebhook.BatchId)).ShouldBeFalse();

        // Se enciende el webhook y se acepta otro envío, contra la misma base.
        using var flipped = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting("SendGrid:EventWebhook:Enabled", "true"));

        var withWebhook = await Accepted(await flipped.CreateClient().PostAsJsonAsync(Route, Payload(Key())));
        (await _database.TrackingExpectedAsync(withWebhook.BatchId)).ShouldBeTrue();

        // Y la fila de antes sigue diciendo lo que era verdad cuando se escribió.
        (await _database.TrackingExpectedAsync(withoutWebhook.BatchId)).ShouldBeFalse();
    }

    #endregion

    #region Idempotencia

    [Fact]
    public async Task Send_ReturnsTheSameIdsAndCreatesNothing_WhenTheKeyAndBodyRepeat()
    {
        var key = Key();
        var client = _factory.CreateClient();
        var payload = Payload(key, recipients: [Person("maria@sorteo360.local"), Person("jose@sorteo360.local")]);

        var first = await Accepted(await client.PostAsJsonAsync(Route, payload));
        var response = await client.PostAsJsonAsync(Route, payload);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var second = await Accepted(response);
        second.BatchId.ShouldBe(first.BatchId);
        second.Results.Select(result => result.Id).ShouldBe(first.Results.Select(result => result.Id));

        (await _database.CountBatchesAsync(key)).ShouldBe(1);
        (await _database.CountRecipientsAsync(first.BatchId)).ShouldBe(2);
        (await _database.CountAboutAsync(first.BatchId)).ShouldBe(1);
    }

    [Fact]
    public async Task Send_Returns409_WhenTheKeyRepeatsWithAnotherBody()
    {
        var key = Key();
        var client = _factory.CreateClient();

        await client.PostAsJsonAsync(Route, Payload(key, body: "<p>Uno</p>"));
        var response = await client.PostAsJsonAsync(Route, Payload(key, body: "<p>Dos</p>"));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Code(response)).ShouldBe("NOTIFICATION_IDEMPOTENCY_KEY_REUSED");
        (await _database.CountBatchesAsync(key)).ShouldBe(1);
    }

    [Fact]
    public async Task Send_ReturnsTwoIdentical202sAndOneBatch_WhenTwoIdenticalPostsRace()
    {
        // La carrera la resuelve el índice único, no una comprobación previa: quien pierde vuelve a
        // leer y contesta lo de quien ganó. Un 500 acá sería el fallo.
        var key = Key();
        var client = _factory.CreateClient();
        var payload = Payload(key, recipients: [Person("maria@sorteo360.local"), Person("jose@sorteo360.local")]);

        var first = client.PostAsJsonAsync(Route, payload);
        var second = client.PostAsJsonAsync(Route, payload);

        var responses = await Task.WhenAll(first, second);

        responses.ShouldAllBe(response => response.StatusCode == HttpStatusCode.Accepted);

        var bodies = await Task.WhenAll(responses.Select(Accepted));
        bodies[1].BatchId.ShouldBe(bodies[0].BatchId);
        bodies[1].Results.Select(result => result.Id).ShouldBe(bodies[0].Results.Select(result => result.Id));

        (await _database.CountBatchesAsync(key)).ShouldBe(1);
        (await _database.CountRecipientsAsync(bodies[0].BatchId)).ShouldBe(2);
    }

    #endregion

    #region Los rechazos, y que no dejan rastro

    [Fact]
    public async Task Send_Returns400WithItsCodeAndStoresNothing_WhenThePayloadBreaksTheShape()
    {
        var key = Key();
        var client = _factory.CreateClient();

        var payload = new Dictionary<string, object?>(Payload(key));
        payload.Remove("subject");

        var response = await client.PostAsJsonAsync(Route, payload);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Code(response)).ShouldBe("NOTIFICATION_PAYLOAD_INVALID");
        (await _database.CountBatchesAsync(key)).ShouldBe(0);
    }

    [Fact]
    public async Task Send_Returns400WithThePayloadCode_WhenTheLanguageIsOutsideTheSix()
    {
        var key = Key();
        var client = _factory.CreateClient();

        var payload = new Dictionary<string, object?>(Payload(key)) { ["language"] = "eu" };

        var response = await client.PostAsJsonAsync(Route, payload);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Code(response)).ShouldBe("NOTIFICATION_PAYLOAD_INVALID");
        (await _database.CountBatchesAsync(key)).ShouldBe(0);
    }

    [Fact]
    public async Task Send_Returns400WithItsCodeAndStoresNothing_WhenOnePersonIsOverTheCap()
    {
        var key = Key();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(Route, Payload(
            key,
            recipients: [.. Enumerable.Range(1, 1001).Select(index => Person($"persona{index}@sorteo360.local"))]));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Code(response)).ShouldBe("NOTIFICATION_RECIPIENT_CAP_EXCEEDED");
        (await _database.CountBatchesAsync(key)).ShouldBe(0);
    }

    [Fact]
    public async Task Send_Returns400NamingTheKeyAndStoresNothing_WhenAKeyIsInBothDictionaries()
    {
        var key = Key();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(Route, Payload(
            key,
            body: "<p>{{evento}}</p>",
            sharedData: new Dictionary<string, string> { ["evento"] = "Feria" },
            recipients: [Person("maria@sorteo360.local", ("evento", "Otra feria"))]));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await Problem(response);
        problem["code"]!.GetValue<string>().ShouldBe("NOTIFICATION_DATA_KEY_COLLISION");
        problem["dataKey"]!.GetValue<string>().ShouldBe("evento");
        (await _database.CountBatchesAsync(key)).ShouldBe(0);
    }

    [Fact]
    public async Task Send_Returns400NamingThePlaceholderAndStoresNothing_WhenItHasNoValue()
    {
        var key = Key();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(Route, Payload(key, body: "<p>Hola {{nombre}}</p>"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await Problem(response);
        problem["code"]!.GetValue<string>().ShouldBe("NOTIFICATION_PLACEHOLDER_UNRESOLVED");
        problem["placeholder"]!.GetValue<string>().ShouldBe("nombre");
        (await _database.CountBatchesAsync(key)).ShouldBe(0);
    }

    [Fact]
    public async Task Send_Returns400_WhenAPlaceholderIsMissingForOnlyOnePerson()
    {
        var key = Key();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(Route, Payload(
            key,
            body: "<p>Hola {{nombre}}</p>",
            recipients:
            [
                Person("maria@sorteo360.local", ("nombre", "María")),
                Person("jose@sorteo360.local")
            ]));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await Problem(response);
        problem["code"]!.GetValue<string>().ShouldBe("NOTIFICATION_PLACEHOLDER_UNRESOLVED");
        problem["detail"]!.GetValue<string>().ShouldContain("jose@sorteo360.local");
        (await _database.CountBatchesAsync(key)).ShouldBe(0);
    }

    [Fact]
    public async Task Send_Returns400_WhenAPlaceholderInTheSubjectHasNoValue()
    {
        var key = Key();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(Route, Payload(key, subject: "Cotización de {{evento}}"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Code(response)).ShouldBe("NOTIFICATION_PLACEHOLDER_UNRESOLVED");
        (await _database.CountBatchesAsync(key)).ShouldBe(0);
    }

    [Fact]
    public async Task Send_Returns202WithThatRecipientRejected_WhenAnAddressIsInvalid()
    {
        var key = Key();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(Route, Payload(
            key,
            recipients: [Person("maria@sorteo360.local"), Person("no-such@@bad")]));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var accepted = await Accepted(response);
        accepted.Results[0].Status.ShouldBe("accepted");
        accepted.Results[1].Status.ShouldBe("rejected");
        accepted.Results[1].Reason.ShouldBe("RECIPIENT_ADDRESS_INVALID");
        accepted.Results[1].Id.ShouldBeNull();

        // El envío existe y su historial cuenta solo a la persona que se va a intentar.
        (await _database.CountRecipientsAsync(accepted.BatchId)).ShouldBe(1);
    }

    #endregion

    #region Autorización

    [Fact]
    public async Task Send_Returns401_WhenCalledWithoutAToken()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, Route)
        {
            Content = JsonContent.Create(Payload(Key()))
        };
        request.Headers.Add(TestAuthHandler.AnonymousHeader, "true");

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Send_Returns403_WhenTheTokenLacksTheSendRole()
    {
        var key = Key();
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, Route)
        {
            Content = JsonContent.Create(Payload(key))
        };
        request.Headers.Add(TestAuthHandler.RolesHeader, AuthorizationPolicies.NotificationsRead);

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _database.CountBatchesAsync(key)).ShouldBe(0);
    }

    #endregion

    #region Helpers

    /// <summary>Una clave de idempotencia propia de cada test: la base es una y se comparte.</summary>
    private static string Key() => $"tests:notification:{Guid.NewGuid()}";

    private static Dictionary<string, object?> Payload(
        string idempotencyKey,
        string? subject = null,
        string? body = null,
        Dictionary<string, string>? sharedData = null,
        IReadOnlyList<Dictionary<string, object?>>? recipients = null,
        IReadOnlyList<Dictionary<string, object?>>? about = null) =>
        new()
        {
            ["type"] = "events.quote.submitted",
            ["language"] = "es",
            ["fromAddress"] = "no-reply@sorteo360.local",
            ["fromName"] = "MITUR",
            ["subject"] = subject ?? "Cotización pendiente de aprobación",
            ["body"] = body ?? "<html><p>Hola</p></html>",
            ["sharedData"] = sharedData,
            ["recipients"] = recipients ?? [Person("maria@sorteo360.local")],
            ["about"] = about ?? [About("events", "event", "xyz-789")],
            ["idempotencyKey"] = idempotencyKey
        };

    private static Dictionary<string, object?> Person(string email, params (string Key, string Value)[] data) =>
        new()
        {
            ["email"] = email,
            ["data"] = data.Length == 0
                ? null
                : data.ToDictionary(pair => pair.Key, pair => pair.Value)
        };

    /// <summary>Una persona con el nombre al que va dirigido el correo.</summary>
    private static Dictionary<string, object?> Named(string email, string name) =>
        new(Person(email)) { ["name"] = name };

    private static Dictionary<string, object?> About(string sourceService, string entityType, string entityId) =>
        new()
        {
            ["sourceService"] = sourceService,
            ["entityType"] = entityType,
            ["entityId"] = entityId
        };

    private static async Task<AcceptedBody> Accepted(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<AcceptedBody>())!;

    private static async Task<System.Text.Json.Nodes.JsonNode> Problem(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>())!;

    private static async Task<string> Code(HttpResponseMessage response)
    {
        var problem = await Problem(response);

        return problem["code"]!.GetValue<string>();
    }

    /// <summary>La respuesta del 202, tal como la lee quien llama.</summary>
    private sealed record AcceptedBody(Guid BatchId, IReadOnlyList<AcceptedRecipient> Results);

    private sealed record AcceptedRecipient(Guid? Id, string Recipient, string Status, string? Reason);

    #endregion
}
