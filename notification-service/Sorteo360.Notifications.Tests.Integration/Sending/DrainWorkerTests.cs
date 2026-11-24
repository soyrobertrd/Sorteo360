using System.Net;
using System.Net.Http.Json;
using Sorteo360.Notifications.Core.Notifications;
using Sorteo360.Notifications.Core.Sending;
using Sorteo360.Notifications.Tests.Integration.Infrastructure;
using Sorteo360.Notifications.Tests.Integration.NotificationEndpoints;
using Shouldly;

namespace Sorteo360.Notifications.Tests.Integration.Sending;

/// <summary>
/// Las reglas del worker contra la base real: qué se reintenta, qué no, qué pasa con una dirección
/// suprimida y qué pasa cuando dos drenadores miran la misma cola.
/// <para>
/// La pata SMTP es el doble porque Mailpit acepta todo lo que se le mande y nunca contesta un 535,
/// así que la tabla de reintentos no se puede ejercitar contra un servidor real. Los bytes los
/// prueba el único test de punta a punta contra Mailpit.
/// </para>
/// </summary>
public sealed class DrainWorkerTests : IClassFixture<NotificationsDrainFactory>, IAsyncLifetime
{
    private const string Route = "/v1/notifications";

    private readonly NotificationsDrainFactory _factory;
    private readonly NotificationsDatabase _database;

    public DrainWorkerTests(NotificationsDrainFactory factory)
    {
        _factory = factory;
        _database = new NotificationsDatabase(factory.ConnectionString);
    }

    /// <summary>
    /// La base se comparte entre los tests de esta clase, así que cada uno arranca con la cola
    /// vacía y sin supresiones de los anteriores.
    /// </summary>
    public async Task InitializeAsync()
    {
        _factory.Smtp.Reset();
        _factory.Smtp.SendDelay = TimeSpan.Zero;

        await _database.ClearSuppressionsAsync();
        await _factory.DrainAsync();
        _factory.Smtp.Reset();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    #region El camino bueno

    [Fact]
    public async Task DrainAsync_ReturnsOneTransactionAndStatusPerPerson_WhenABatchIsSent()
    {
        var batchId = await Post(
            subject: "Cotización de {{evento}}",
            body: "<html><p>Hola {{nombre}}</p></html>",
            sharedData: new Dictionary<string, string> { ["evento"] = "Feria de Madrid" },
            recipients: [Person("maria@sorteo360.local", ("nombre", "María")), Person("jose@sorteo360.local", ("nombre", "José"))]);

        (await _factory.DrainAsync()).ShouldBe(2);

        // Una transacción por persona, sin cc ni bcc: eso es lo nativo sobre SMTP.
        _factory.Smtp.Sent.Count.ShouldBe(2);
        _factory.Smtp.Sent.Select(email => email.ToAddress)
            .ShouldBe(["maria@sorteo360.local", "jose@sorteo360.local"], ignoreOrder: true);

        // Una sola conexión para las dos: reutilizarla no es una optimización, es la forma.
        _factory.Smtp.OpenedSessions.ShouldBe(1);

        var rows = await _database.RecipientsAsync(batchId);
        rows["maria@sorteo360.local"].DeliveryStatus.ShouldBe(DeliveryStatus.Sent);
        rows["jose@sorteo360.local"].DeliveryStatus.ShouldBe(DeliveryStatus.Sent);

        // El "250 Ok: queued as <token>" es lo primero que pide el soporte del proveedor.
        rows.Values.ShouldAllBe(row => row.SmtpQueuedAs!.StartsWith("250 Ok: queued as "));
        rows.Values.ShouldAllBe(row => row.SentAt != null);
    }

    [Fact]
    public async Task DrainAsync_ReturnsTwoSendsWithTheNameOnlyOnTheNamedOne_WhenOneRecipientCarriesAName()
    {
        await Post(recipients:
        [
            new Dictionary<string, object?>(Person("maria@sorteo360.local")) { ["name"] = "María González" },
            Person("jose@sorteo360.local")
        ]);

        (await _factory.DrainAsync()).ShouldBe(2);

        var sent = _factory.Smtp.Sent.ToDictionary(email => email.ToAddress);
        sent["maria@sorteo360.local"].ToName.ShouldBe("María González");
        sent["jose@sorteo360.local"].ToName.ShouldBeNull();
    }

    [Fact]
    public async Task DrainAsync_ReturnsTheBodySubstitutedAtSendTime_WhenTheRowIsSent()
    {
        var batchId = await Post(
            subject: "Aviso de {{evento}}",
            body: "<html><p>Hola {{nombre}}, sobre {{evento}}</p></html>",
            sharedData: new Dictionary<string, string> { ["evento"] = "Feria de Madrid" },
            recipients: [Person("maria@sorteo360.local", ("nombre", "María"))]);

        // Lo guardado sigue teniendo los marcadores: la sustitución no toca el registro.
        (await _database.BodyAsync(batchId)).ShouldContain("{{nombre}}");

        await _factory.DrainAsync();

        var sent = _factory.Smtp.Sent.ShouldHaveSingleItem();
        sent.Subject.ShouldBe("Aviso de Feria de Madrid");
        sent.HtmlBody.ShouldBe("<html><p>Hola María, sobre Feria de Madrid</p></html>");

        // La parte de texto se deriva del HTML ya sustituido: nadie escribe la copia dos veces.
        sent.TextBody.ShouldBe("Hola María, sobre Feria de Madrid");
        sent.TextBody.ShouldNotContain("<");
    }

    [Fact]
    public async Task DrainAsync_ReturnsARowWithNoSubstitutionValues_WhenTheRowIsSent()
    {
        const string token = "TOKEN-EFIMERO-QUE-MUERE-AL-MANDAR-9d2c";

        var batchId = await Post(
            body: "<p>{{credencial}}</p>",
            recipients: [Person("maria@sorteo360.local", ("credencial", token))]);

        // Antes de mandar el valor existe, porque es lo único con lo que el worker puede sustituir.
        (await _database.CountPayloadsAsync(batchId)).ShouldBe(1);

        await _factory.DrainAsync();

        // Y en cuanto el envío deja de ser reintentable, deja de existir. Ninguna de las cinco
        // tablas lo contiene ya.
        (await _database.CountPayloadsAsync(batchId)).ShouldBe(0);

        var rows = await _database.EveryRowIncludingPayloadAsTextAsync();
        rows.ShouldNotBeEmpty();
        rows.ShouldAllBe(row => !row.Contains(token));
    }

    [Fact]
    public async Task DrainAsync_ReturnsTheUnsubscribeLinkOnlyToExternalRecipients_WhenBothKindsAreSent()
    {
        await Post(
            body: "<p>Hola</p>",
            recipients:
            [
                Person("maria@sorteo360.local"),
                Person("juan@expedia.com", ("enlace_baja", "https://app.sorteo360.local/baja?t=abc"))
            ]);

        await _factory.DrainAsync();

        var internalEmail = _factory.Smtp.Sent.Single(email => email.ToAddress == "maria@sorteo360.local");
        var externalEmail = _factory.Smtp.Sent.Single(email => email.ToAddress == "juan@expedia.com");

        internalEmail.UnsubscribeUrl.ShouldBeNull();
        externalEmail.UnsubscribeUrl.ShouldBe("https://app.sorteo360.local/baja?t=abc");
    }

    #endregion

    #region La tabla de reintentos

    [Fact]
    public async Task DrainAsync_ReturnsARowScheduledForRetry_WhenTheRelayAnswers4xx()
    {
        _factory.Smtp.Transport.FailNextWith(451, "Try again later");

        var batchId = await Post(recipients: [Person("maria@sorteo360.local")]);

        await _factory.DrainAsync();

        var row = (await _database.RecipientsAsync(batchId))["maria@sorteo360.local"];
        row.DeliveryStatus.ShouldBe(DeliveryStatus.Queued);
        row.AttemptCount.ShouldBe(1);
        row.NextAttemptAt.ShouldNotBeNull();
        row.LastError.ShouldNotBeNull().ShouldContain("451");

        // Y cuando toca, sale.
        await _database.MakeDueAsync(batchId);
        await _factory.DrainAsync();

        (await _database.RecipientsAsync(batchId))["maria@sorteo360.local"]
            .DeliveryStatus.ShouldBe(DeliveryStatus.Sent);
    }

    [Fact]
    public async Task DrainAsync_ReturnsAFailedRow_WhenTheRelayAnswers5xx()
    {
        _factory.Smtp.Transport.FailAlwaysWith(550, "No such mailbox");

        var batchId = await Post(recipients: [Person("no-existe@sorteo360.local")]);

        await _factory.DrainAsync();

        var row = (await _database.RecipientsAsync(batchId))["no-existe@sorteo360.local"];
        row.DeliveryStatus.ShouldBe(DeliveryStatus.Failed);
        row.NextAttemptAt.ShouldBeNull();
        row.LastError.ShouldNotBeNull().ShouldContain("550");

        // Una fila terminal no se vuelve a recoger nunca.
        _factory.Smtp.Transport.Reset();
        (await _factory.DrainAsync()).ShouldBe(0);
        _factory.Smtp.Transport.AttemptCount.ShouldBe(0);
    }

    [Fact]
    public async Task DrainAsync_ReturnsAFailedRowWithNoValues_WhenTheRelayAnswers535()
    {
        _factory.Smtp.Transport.FailAlwaysWith(SmtpFailureClassifier.AuthenticationFailed, "Authentication failed");

        var batchId = await Post(recipients: [Person("maria@sorteo360.local")]);

        await _factory.DrainAsync();

        var row = (await _database.RecipientsAsync(batchId))["maria@sorteo360.local"];
        row.DeliveryStatus.ShouldBe(DeliveryStatus.Failed);
        row.NextAttemptAt.ShouldBeNull();
        row.LastError.ShouldNotBeNull().ShouldContain("535");

        (await _database.CountPayloadsAsync(batchId)).ShouldBe(0);
    }

    [Fact]
    public async Task DrainAsync_ReturnsAFailedRow_WhenAnHourHasPassed()
    {
        _factory.Smtp.Transport.FailAlwaysWith(451, "Try again later");

        var batchId = await Post(recipients: [Person("maria@sorteo360.local")]);

        // La fila lleva más de una hora en cola: correo rancio es peor que correo ausente.
        await _database.BackdateQueuedAtAsync(batchId, TimeSpan.FromHours(2));

        await _factory.DrainAsync();

        var row = (await _database.RecipientsAsync(batchId))["maria@sorteo360.local"];
        row.DeliveryStatus.ShouldBe(DeliveryStatus.Failed);
        row.LastError.ShouldBe(SendRetryPolicy.GaveUpReason);

        // Ni siquiera se abrió el socket: la hora se comprueba antes de que exista conexión.
        _factory.Smtp.OpenedSessions.ShouldBe(0);
        _factory.Smtp.Transport.AttemptCount.ShouldBe(0);
    }

    [Fact]
    public async Task DrainAsync_ReturnsTheStatusOfWhatAlreadyWentOut_WhenTheBatchDiesPartWay()
    {
        // Una muere de forma terminal y la otra sale. Si el estado se escribiera una sola vez al
        // final del lote, la que salió se perdería.
        //
        // El fallo se fija por dirección y no por orden de llegada: las dos filas del lote nacen
        // con el mismo queued_at, así que la cola las desempata por id —un GUID— y cuál se toma
        // primero no está garantizado.
        _factory.Smtp.Transport.FailForAddressWith("no-existe@sorteo360.local", 550, "No such mailbox");

        var batchId = await Post(recipients:
        [
            Person("no-existe@sorteo360.local"),
            Person("maria@sorteo360.local")
        ]);

        await _factory.DrainAsync();

        var rows = await _database.RecipientsAsync(batchId);
        rows["no-existe@sorteo360.local"].DeliveryStatus.ShouldBe(DeliveryStatus.Failed);
        rows["maria@sorteo360.local"].DeliveryStatus.ShouldBe(DeliveryStatus.Sent);
    }

    #endregion

    #region La lista de supresión

    [Fact]
    public async Task DrainAsync_ReturnsADroppedRowAndSendsTheRest_WhenOneAddressIsSuppressed()
    {
        await _database.SuppressAsync("juan@expedia.com", "unsubscribe");

        var batchId = await Post(recipients:
        [
            Person("juan@expedia.com"),
            Person("maria@sorteo360.local")
        ]);

        await _factory.DrainAsync();

        var rows = await _database.RecipientsAsync(batchId);

        // Queda la fila diciéndolo, que es lo contrario de saltársela en silencio: el historial
        // tiene que poder contestar "¿y por qué Juan no lo recibió?".
        rows["juan@expedia.com"].DeliveryStatus.ShouldBe(DeliveryStatus.Dropped);
        rows["juan@expedia.com"].LastError.ShouldBe("unsubscribe");
        rows["juan@expedia.com"].AttemptCount.ShouldBe(0);

        rows["maria@sorteo360.local"].DeliveryStatus.ShouldBe(DeliveryStatus.Sent);

        // Solo salió un mensaje, y no fue el suyo.
        _factory.Smtp.Sent.ShouldHaveSingleItem().ToAddress.ShouldBe("maria@sorteo360.local");
    }

    [Fact]
    public async Task DrainAsync_ReturnsDroppedRowsAndNoSession_WhenEveryAddressIsSuppressed()
    {
        await _database.SuppressAsync("juan@expedia.com", "unsubscribe");
        await _database.SuppressAsync("ana@expedia.com", "spamreport");

        var batchId = await Post(recipients:
        [
            Person("juan@expedia.com"),
            Person("ana@expedia.com")
        ]);

        (await _factory.DrainAsync()).ShouldBe(2);

        // La supresión se comprueba antes de que exista conexión, así que acá no se abrió ninguna.
        // Es la mitad de la regla que el nombre del test de arriba prometía y no comprobaba.
        _factory.Smtp.OpenedSessions.ShouldBe(0);
        _factory.Smtp.Transport.AttemptCount.ShouldBe(0);

        var rows = await _database.RecipientsAsync(batchId);
        rows.Values.ShouldAllBe(row => row.DeliveryStatus == DeliveryStatus.Dropped);
    }

    #endregion

    #region Fallos que no son del servidor

    [Fact]
    public async Task DrainAsync_ReturnsARowScheduledForRetry_WhenAFailureIsUnclassifiable()
    {
        // Un fallo que no es del servidor SMTP: la sesión ni siquiera se puede abrir de forma que
        // el worker sepa leer. Sin gastar intento, esta fila volvería a ser la primera de la cola
        // en cada vuelta y taparía a las demás durante una hora.
        _factory.Smtp.FailToOpenWith(new InvalidOperationException("el transporte explotó"));

        var batchId = await Post(recipients: [Person("maria@sorteo360.local")]);

        (await _factory.DrainAsync()).ShouldBe(1);

        var row = (await _database.RecipientsAsync(batchId))["maria@sorteo360.local"];
        row.AttemptCount.ShouldBe(1);
        row.NextAttemptAt.ShouldNotBeNull();
        row.LastError.ShouldNotBeNull().ShouldContain("el transporte explotó");

        // Y como ya tiene hora futura, la vuelta siguiente no la vuelve a recoger: la cola avanza.
        _factory.Smtp.Reset();
        (await _factory.DrainAsync()).ShouldBe(0);
    }

    #endregion

    #region Dos drenadores

    [Fact]
    public async Task DrainAsync_ReturnsEachRowSentExactlyOnce_WhenTwoDrainersRunConcurrently()
    {
        var batchId = await Post(recipients:
        [
            Person("a@sorteo360.local"), Person("b@sorteo360.local"), Person("c@sorteo360.local"),
            Person("d@sorteo360.local"), Person("e@sorteo360.local"), Person("f@sorteo360.local")
        ]);

        // Sin esto las entregas son instantáneas, los dos drenadores no llegan a solaparse y el
        // test pasaría sin haber ejercitado el SKIP LOCKED.
        _factory.Smtp.SendDelay = TimeSpan.FromMilliseconds(40);

        var drained = await Task.WhenAll(_factory.DrainAsync(), _factory.DrainAsync());

        // Entre los dos resolvieron las seis, y ninguna dos veces.
        drained.Sum().ShouldBe(6);
        _factory.Smtp.Transport.AttemptCount.ShouldBe(6);
        _factory.Smtp.Sent.Select(email => email.ToAddress).Distinct().Count().ShouldBe(6);

        var rows = await _database.RecipientsAsync(batchId);
        rows.Count.ShouldBe(6);
        rows.Values.ShouldAllBe(row => row.DeliveryStatus == DeliveryStatus.Sent);
        rows.Values.ShouldAllBe(row => row.AttemptCount == 1);
    }

    #endregion

    #region Helpers

    private async Task<Guid> Post(
        string? subject = null,
        string? body = null,
        Dictionary<string, string>? sharedData = null,
        IReadOnlyList<Dictionary<string, object?>>? recipients = null)
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(Route, new Dictionary<string, object?>
        {
            ["type"] = "events.quote.submitted",
            ["language"] = "es",
            ["fromAddress"] = "no-reply@sorteo360.local",
            ["fromName"] = "MITUR",
            ["subject"] = subject ?? "Cotización pendiente de aprobación",
            ["body"] = body ?? "<html><p>Hola</p></html>",
            ["sharedData"] = sharedData,
            ["recipients"] = recipients ?? [Person("maria@sorteo360.local")],
            ["about"] = new[] { About() },
            ["idempotencyKey"] = $"tests:drain:{Guid.NewGuid()}"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var accepted = await response.Content.ReadFromJsonAsync<AcceptedBody>();

        return accepted!.BatchId;
    }

    private static Dictionary<string, object?> Person(string email, params (string Key, string Value)[] data) =>
        new()
        {
            ["email"] = email,
            ["data"] = data.Length == 0 ? null : data.ToDictionary(pair => pair.Key, pair => pair.Value)
        };

    private static Dictionary<string, object?> About() =>
        new()
        {
            ["sourceService"] = "events",
            ["entityType"] = "event",
            ["entityId"] = "xyz-789"
        };

    private sealed record AcceptedBody(Guid BatchId);

    #endregion
}
