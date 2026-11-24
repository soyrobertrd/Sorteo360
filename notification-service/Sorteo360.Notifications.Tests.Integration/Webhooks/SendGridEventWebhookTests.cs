using System.Net;
using System.Text;
using System.Text.Json;
using Sorteo360.Notifications.Core.Notifications;
using Sorteo360.Notifications.Core.Webhooks;
using Sorteo360.Notifications.Tests.Integration.History;
using Sorteo360.Notifications.Tests.Integration.Infrastructure;
using Sorteo360.Notifications.Web.WebhookEndpoints;
using Shouldly;

namespace Sorteo360.Notifications.Tests.Integration.Webhooks;

/// <summary>
/// El webhook de eventos de punta a punta: la verificación, la deduplicación, la correlación y lo
/// que cada evento mueve.
/// <para>
/// Los tests corren en serie (<c>DisableParallelization</c> vía la colección) porque comparten un
/// contenedor y cada uno vacía las tablas antes de empezar.
/// </para>
/// </summary>
public sealed class SendGridEventWebhookTests : IClassFixture<NotificationsWebhookFactory>, IAsyncLifetime
{
    /// <summary>
    /// Los dos GUID que nombra el lote capturado. Están escritos en el archivo, así que el test
    /// siembra filas con <b>estos</b> ids en vez de reescribir el archivo: reescribirlo rompería el
    /// "byte por byte" que es el punto de ese test.
    /// </summary>
    private static readonly Guid FixtureMaria = Guid.Parse("55555555-5555-5555-5555-555555555551");

    private static readonly Guid FixtureJose = Guid.Parse("55555555-5555-5555-5555-555555555552");

    private readonly NotificationsWebhookFactory _factory;
    private readonly NotificationHistorySeed _seed;
    private readonly WebhookDatabase _database;

    public SendGridEventWebhookTests(NotificationsWebhookFactory factory)
    {
        _factory = factory;
        _seed = new NotificationHistorySeed(factory.ConnectionString);
        _database = new WebhookDatabase(factory.ConnectionString);
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _database.ResetAsync();
        _factory.Smtp.Reset();
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    // ---------------------------------------------------------------------------------------
    // Verificación: el orden es toda la especificación.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task EventWebhook_Returns200StoresAndMovesTheRows_WhenSignedWithTheConfiguredKey()
    {
        await SeedFixtureRecipientsAsync();

        var response = await Post(_factory.SignedRequest(CapturedBatch()));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Un evento por entrada del lote, ni uno más ni uno menos.
        (await _database.CountEventsAsync()).ShouldBe(3);
        (await _database.CountEventsForAsync(FixtureMaria)).ShouldBe(2);
        (await _database.CountEventsForAsync(FixtureJose)).ShouldBe(1);

        // Y las filas de persona quedaron donde el lote dice.
        (await _database.RecipientAsync(FixtureMaria)).DeliveryStatus.ShouldBe(DeliveryStatus.Delivered);
        (await _database.RecipientAsync(FixtureJose)).DeliveryStatus.ShouldBe(DeliveryStatus.Bounced);
    }

    /// <summary>
    /// El cambio de espaciado que la especificación pide: se firma el archivo tal cual y se manda
    /// el <b>mismo objeto</b> re-serializado. Los bytes difieren y el lote tiene que rebotar.
    /// <para>
    /// <b>Lo que este test NO prueba, y hay que saberlo:</b> no distingue un endpoint que verifica
    /// los bytes de uno que normalizara antes de verificar —los dos contestan 403 acá—. El que
    /// prueba <i>eso</i> es el de arriba: si el endpoint re-serializara antes de verificar, un
    /// cuerpo firmado y mandado idéntico fallaría, y el 200 se caería. Los dos juntos cierran la
    /// caja; ninguno solo.
    /// </para>
    /// </summary>
    [Fact]
    public async Task EventWebhook_Returns403_WhenTheBodyIsReserialized()
    {
        await SeedFixtureRecipientsAsync();

        var captured = CapturedBatch();
        var reserialized = JsonSerializer.Serialize(JsonDocument.Parse(captured).RootElement);

        reserialized.ShouldNotBe(captured);

        var response = await Post(_factory.SignedRequest(reserialized, signedBody: captured));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _database.CountEventsAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task EventWebhook_Returns403_WhenTheSignatureIsBad()
    {
        // Un 200 silencioso tiraría cada evento para siempre, y el fallo que de verdad va a ocurrir
        // es nuestra propia clave mal configurada. Con un 403, el reintento rodante de 24 horas del
        // proveedor se vuelve un plazo de gracia gratis.
        var response = await Post(_factory.SignedRequest(
            CapturedBatch(), signature: Convert.ToBase64String([1, 2, 3, 4])));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _database.CountEventsAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task EventWebhook_Returns403_WhenTheTimestampIsElevenMinutesOld()
    {
        var response = await Post(_factory.SignedRequest(
            CapturedBatch(), signedAt: DateTimeOffset.UtcNow.AddMinutes(-11)));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _database.CountEventsAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task EventWebhook_Returns200_WhenTheTimestampIsNineMinutesOld()
    {
        await SeedFixtureRecipientsAsync();

        var response = await Post(_factory.SignedRequest(
            CapturedBatch(), signedAt: DateTimeOffset.UtcNow.AddMinutes(-9)));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(SendGridEventWebhookEndpoint.SignatureHeader)]
    [InlineData(SendGridEventWebhookEndpoint.TimestampHeader)]
    public async Task EventWebhook_Returns403_WhenEitherSignatureHeaderIsMissing(string header)
    {
        var request = _factory.SignedRequest(CapturedBatch());
        request.Headers.Remove(header);

        (await Post(request)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _database.CountEventsAsync()).ShouldBe(0);
    }

    [Theory]
    [InlineData("ayer")]
    [InlineData("999999999999")]
    [InlineData("-999999999999")]
    [InlineData("9223372036854775807")]
    public async Task EventWebhook_Returns403_WhenTheTimestampIsNotAValidInstant(string timestamp)
    {
        // El 500 importa más que el 403: este paso corre ANTES de verificar la firma, así que
        // cualquiera de internet lo alcanza sin material criptográfico ninguno. Un número que
        // DateTimeOffset no sabe representar lanzaba acá, y la excepción salía sin capturar.
        var request = _factory.SignedRequest(CapturedBatch());
        request.Headers.Remove(SendGridEventWebhookEndpoint.TimestampHeader);
        request.Headers.Add(SendGridEventWebhookEndpoint.TimestampHeader, timestamp);

        var response = await Post(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _database.CountEventsAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task EventWebhook_Returns413_WhenTheBodyIsThreeMegabytes()
    {
        // El tope importa porque este endpoint no tiene autenticación previa: sin él, cualquiera en
        // internet elige cuánta memoria le pedimos al proceso. Con Content-Length se corta ahí
        // mismo; que además no se lea el cuerpo es la forma del código y no algo que un assert de
        // estado pueda demostrar, así que el nombre no lo promete.
        var oversized = "[" + new string('x', 3 * 1024 * 1024) + "]";

        var response = await Post(_factory.SignedRequest(oversized));

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        (await _database.CountEventsAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task EventWebhook_Returns413_WhenAnOversizedBodyHasNoContentLength()
    {
        // El otro camino, y el único por el que un cuerpo grande puede llegar de verdad a memoria:
        // troceado, sin Content-Length que mirar. Ahí el tope se le fija al servidor y la lectura
        // se corta a los 2 MB. Sin este test, esa rama no la recorría nadie.
        var oversized = "[" + new string('x', 3 * 1024 * 1024) + "]";

        var request = _factory.SignedRequest(oversized);
        request.Content = new ChunkedContent(Encoding.UTF8.GetBytes(oversized));

        // Sin esto el test seguiría pasando por la cabecera y no probaría la rama que dice probar.
        request.Content.Headers.ContentLength.ShouldBeNull();

        var response = await Post(request);

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        (await _database.CountEventsAsync()).ShouldBe(0);
    }

    [Fact]
    public void MaxBodyBytes_ReturnsTwoMegabytes_WhenRead() =>
        // El proveedor agrupa en lotes de 768 KB: dos megas es holgura de sobra, y cualquier cosa
        // por encima no es un lote suyo.
        SendGridEventWebhookEndpoint.MaxBodyBytes.ShouldBe(2 * 1024 * 1024);

    [Fact]
    public async Task EventWebhook_Returns400_WhenASignedBatchIsMalformed()
    {
        // Bien firmado y mal formado es un problema de contrato, no de autenticación: 400, y el
        // reintento del proveedor lo vuelve a traer para que se note.
        var response = await Post(_factory.SignedRequest("""{"no":"soy un lote"}"""));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _database.CountEventsAsync()).ShouldBe(0);
    }

    // ---------------------------------------------------------------------------------------
    // Deduplicación y orden.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task EventWebhook_ReturnsNoDuplicateEvents_WhenAnIdenticalBatchIsReposted()
    {
        await SeedFixtureRecipientsAsync();

        (await Post(_factory.SignedRequest(CapturedBatch()))).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Post(_factory.SignedRequest(CapturedBatch()))).StatusCode.ShouldBe(HttpStatusCode.OK);

        // El reintento del proveedor haciendo lo que se diseñó para que hiciera: nada.
        (await _database.CountEventsAsync()).ShouldBe(3);
    }

    [Fact]
    public async Task EventWebhook_ReturnsOneStoredEvent_WhenABatchWithNoEventIdIsReposted()
    {
        // Es el caso de los rebotes asíncronos, que llegan sin identificador del proveedor.
        var recipient = await OpenSendAsync("jose@ejemplo.com");

        var batch = Batch(Event(
            "bounce", "jose@ejemplo.com", 1757800500, sgEventId: null, recipientId: recipient));

        await Post(_factory.SignedRequest(batch));
        await Post(_factory.SignedRequest(batch));

        (await _database.CountEventsAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task EventWebhook_ReturnsDelivered_WhenAProcessedArrivesAfterADelivered()
    {
        // El desorden es estructural, no un borde: los lotes se agrupan y cada evento reintenta por
        // su cuenta durante 24 horas.
        var recipient = await OpenSendAsync("maria@sorteo360.local");

        await Post(_factory.SignedRequest(Batch(Event(
            "delivered", "maria@sorteo360.local", 1757800060, "sg-delivered", recipient))));

        await Post(_factory.SignedRequest(Batch(Event(
            "processed", "maria@sorteo360.local", 1757800000, "sg-processed", recipient))));

        (await _database.RecipientAsync(recipient)).DeliveryStatus.ShouldBe(DeliveryStatus.Delivered);
        (await _database.CountEventsForAsync(recipient)).ShouldBe(2);
    }

    [Fact]
    public async Task EventWebhook_ReturnsAComplaintOnADeliveredRow_WhenASpamreportArrives()
    {
        // Normativo: un reporte de spam significa que el correo SÍ se entregó. La entrega y la
        // reacción de quien recibe son dos hechos distintos y no comparten columna.
        var recipient = await OpenSendAsync("maria@sorteo360.local");

        await Post(_factory.SignedRequest(Batch(Event(
            "delivered", "maria@sorteo360.local", 1757800060, "sg-delivered", recipient))));

        await Post(_factory.SignedRequest(Batch(Event(
            "spamreport", "maria@sorteo360.local", 1757800600, "sg-spam", recipient))));

        var row = await _database.RecipientAsync(recipient);

        row.RecipientAction.ShouldBe(RecipientAction.Complained);
        row.DeliveryStatus.ShouldBe(DeliveryStatus.Delivered);
    }

    [Fact]
    public async Task EventWebhook_ReturnsTheEventStoredAndTheStatusUnchanged_WhenTheTypeIsUnrecognised()
    {
        // El proveedor agrega tipos cuando quiere. Guardarlos es lo que evita una migración el día
        // que alguien encienda la apertura o el clic: los eventos ya estarían en la tabla.
        var recipient = await OpenSendAsync("maria@sorteo360.local");

        await Post(_factory.SignedRequest(Batch(Event(
            "open", "maria@sorteo360.local", 1757800700, "sg-open", recipient))));

        (await _database.EventTypesAsync()).ShouldContain("open");
        (await _database.RecipientAsync(recipient)).DeliveryStatus.ShouldBe(DeliveryStatus.Sent);
    }

    // ---------------------------------------------------------------------------------------
    // Correlación: nunca adivinar, nunca descartar.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task EventWebhook_ReturnsAnOrphan_WhenTheEventNamesAGuidWeNeverIssued()
    {
        // Stage y producción comparten cuenta del proveedor, así que esto pasa de verdad.
        await Post(_factory.SignedRequest(Batch(Event(
            "bounce", "desconocida@ejemplo.com", 1757800800, "sg-huerfano", Guid.NewGuid()))));

        (await _database.CountEventsAsync()).ShouldBe(1);
        (await _database.CountOrphansAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task EventWebhook_ReturnsAnOrphanVisibleOnTheOrphanRoute_WhenTheEventIsUncorrelated()
    {
        await Post(_factory.SignedRequest(Batch(Event(
            "bounce", "desconocida@ejemplo.com", 1757800800, "sg-visible", Guid.NewGuid()))));

        var response = await _factory.CreateClient().GetAsync("/v1/notifications/orphan-events");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldContain("sg-visible");
    }

    [Fact]
    public async Task EventWebhook_ReturnsTheEventLinkedByEmail_WhenExactlyOneSendIsOpen()
    {
        var recipient = await OpenSendAsync("maria@sorteo360.local");

        await Post(_factory.SignedRequest(Batch(Event(
            "bounce", "maria@sorteo360.local", 1757800900, "sg-por-direccion", recipientId: null))));

        (await _database.CountOrphansAsync()).ShouldBe(0);
        (await _database.CountEventsForAsync(recipient)).ShouldBe(1);
        (await _database.RecipientAsync(recipient)).DeliveryStatus.ShouldBe(DeliveryStatus.Bounced);
    }

    [Fact]
    public async Task EventWebhook_ReturnsAnOrphan_WhenTwoSendsAreOpenForTheAddress()
    {
        // Elegir uno sería adivinar, y adivinar escribe un rebote falso en el sistema de registro.
        var first = await OpenSendAsync("maria@sorteo360.local");
        var second = await OpenSendAsync("maria@sorteo360.local");

        await Post(_factory.SignedRequest(Batch(Event(
            "bounce", "maria@sorteo360.local", 1757801000, "sg-ambiguo", recipientId: null))));

        (await _database.CountOrphansAsync()).ShouldBe(1);
        (await _database.RecipientAsync(first)).DeliveryStatus.ShouldBe(DeliveryStatus.Sent);
        (await _database.RecipientAsync(second)).DeliveryStatus.ShouldBe(DeliveryStatus.Sent);
    }

    // ---------------------------------------------------------------------------------------
    // Las bajas se registran Y se hacen cumplir.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task EventWebhook_ReturnsADroppedRowOnTheNextSend_WhenAnUnsubscribeArrives()
    {
        var recipient = await OpenSendAsync("maria@sorteo360.local");

        await Post(_factory.SignedRequest(Batch(Event(
            "unsubscribe", "maria@sorteo360.local", 1757801100, "sg-baja", recipient))));

        (await _database.SuppressionReasonAsync("maria@sorteo360.local")).ShouldNotBeNull();
        (await _database.RecipientAsync(recipient)).RecipientAction.ShouldBe(RecipientAction.Unsubscribed);

        // Y el siguiente envío a esa dirección se registra como dropped en vez de no registrarse:
        // una supresión silenciosa es justo la mentira que este servicio no puede contar.
        var batchId = await _seed.AddBatchAsync("cotizacion.enviada", []);
        var next = await _seed.AddRecipientAsync(batchId, "maria@sorteo360.local");

        await _factory.DrainAsync();

        (await _database.RecipientAsync(next)).DeliveryStatus.ShouldBe(DeliveryStatus.Dropped);
        _factory.Smtp.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task EventWebhook_ReturnsADroppedRowOnTheNextSend_WhenASpamreportArrives()
    {
        var recipient = await OpenSendAsync("jose@ejemplo.com");

        await Post(_factory.SignedRequest(Batch(Event(
            "spamreport", "jose@ejemplo.com", 1757801200, "sg-queja", recipient))));

        (await _database.SuppressionReasonAsync("jose@ejemplo.com")).ShouldNotBeNull();
    }

    [Fact]
    public async Task EventWebhook_ReturnsTheAddressStillInCirculation_WhenABounceArrives()
    {
        // Una persona puede estar suprimida en el CRM por consentimiento y acá por buzón. Mezclarlas
        // haría que una dirección mal escrita se viera como un consentimiento retirado, y eso no se
        // recupera desde la pantalla de consentimiento.
        var recipient = await OpenSendAsync("jose@ejemplo.com");

        await Post(_factory.SignedRequest(Batch(Event(
            "bounce", "jose@ejemplo.com", 1757801300, "sg-rebote", recipient))));

        (await _database.SuppressionReasonAsync("jose@ejemplo.com")).ShouldBeNull();
    }

    // ---------------------------------------------------------------------------------------

    private Task<HttpResponseMessage> Post(HttpRequestMessage request) =>
        _factory.CreateClient().SendAsync(request);

    /// <summary>
    /// El lote del archivo, leído <b>tal cual</b>. Nada lo reescribe ni lo normaliza: lo que se
    /// firma y lo que se manda son estos bytes, y eso es la propiedad que importa.
    /// <para>
    /// El contenido está armado a partir de los eventos de ejemplo que el proveedor publica en su
    /// documentación, con espaciado de JSON formateado —que es lo que hace que una
    /// re-serialización se note—. No es una captura de tráfico real: la primera y única llega
    /// cuando se confirme el webhook contra stage, y eso no cambia una línea de código.
    /// </para>
    /// </summary>
    private static string CapturedBatch() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Webhooks", "Fixtures", "sendgrid-event-batch.json"));

    /// <summary>Las dos filas que el lote capturado nombra, con sus GUID del archivo.</summary>
    private async Task SeedFixtureRecipientsAsync()
    {
        var batchId = await _seed.AddBatchAsync("cotizacion.enviada", []);

        await _seed.AddRecipientAsync(
            batchId, "maria@sorteo360.local", DeliveryStatus.Sent, id: FixtureMaria);

        await _seed.AddRecipientAsync(
            batchId, "jose@ejemplo.com", DeliveryStatus.Sent, id: FixtureJose);
    }

    /// <summary>Una fila abierta: entregada al proveedor y sin respuesta suya todavía.</summary>
    private async Task<Guid> OpenSendAsync(string email)
    {
        var batchId = await _seed.AddBatchAsync("cotizacion.enviada", []);

        return await _seed.AddRecipientAsync(batchId, email, DeliveryStatus.Sent);
    }

    /// <summary>Un lote con los eventos dados.</summary>
    private static string Batch(params string[] events) => "[" + string.Join(",", events) + "]";

    /// <summary>
    /// Un cuerpo que se manda troceado: al no poder calcular su longitud, HttpClient omite
    /// <c>Content-Length</c> y usa <c>Transfer-Encoding: chunked</c>. Es la única forma de llegar al
    /// camino en que el tope lo tiene que aplicar el servidor y no una cabecera.
    /// </summary>
    private sealed class ChunkedContent : HttpContent
    {
        private readonly byte[] _bytes;

        public ChunkedContent(byte[] bytes)
        {
            _bytes = bytes;
            Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(_bytes, 0, _bytes.Length);

        /// <summary>Devolver false es lo que deja la petición sin <c>Content-Length</c>.</summary>
        protected override bool TryComputeLength(out long length)
        {
            length = 0;

            return false;
        }
    }

    /// <summary>Una entrada del lote, con la forma que manda el proveedor.</summary>
    private static string Event(
        string eventType,
        string email,
        long timestamp,
        string? sgEventId,
        Guid? recipientId)
    {
        var fields = new List<string>
        {
            $"\"event\":\"{eventType}\"",
            $"\"email\":\"{email}\"",
            $"\"timestamp\":{timestamp}"
        };

        if (sgEventId is not null)
            fields.Add($"\"sg_event_id\":\"{sgEventId}\"");

        if (recipientId is { } id)
            fields.Add($"\"recipient_id\":\"{id}\"");

        return "{" + string.Join(",", fields) + "}";
    }
}
