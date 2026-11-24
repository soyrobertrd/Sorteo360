using System.Text;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Sorteo360.Notifications.Core.Notifications;
using Sorteo360.Notifications.Core.Webhooks;
using Sorteo360.Platform.GlobalEndpoints;

namespace Sorteo360.Notifications.Web.WebhookEndpoints;

/// <summary>
/// El webhook de eventos del proveedor: <b>el único endpoint anónimo de terceros de la
/// plataforma</b>, y el motivo por el que este ticket lleva revisión de seguridad.
/// <para>
/// Existe porque SMTP es una puerta de una sola dirección. El servidor contesta
/// <c>250 Ok: queued as …</c> y la conversación se acaba; todo lo demás —entregado, diferido,
/// rebotado, reportado como spam, dado de baja— pasa después, en el proveedor, y este endpoint es
/// el único camino de vuelta. Sin él el historial <i>miente</i>: una supresión se vería como un
/// envío exitoso y la fila diría <c>sent</c> para siempre.
/// </para>
/// <para>
/// <b>La superficie a revisar son exactamente cinco cosas</b>, y las cinco están en
/// <see cref="Receive"/>, en este orden: el tope de 2 MB antes de leer, la lectura del cuerpo
/// crudo, la ventana de repetición de diez minutos, la verificación de la firma <b>antes</b> de
/// parsear, y la ausencia de cualquier política de Keycloak.
/// </para>
/// </summary>
public sealed class SendGridEventWebhookEndpoint : IEndpoint
{
    /// <summary>
    /// La ruta, fuera del prefijo versionado. Es la única de este servicio que Traefik publica, y
    /// lo hace sobre un host propio (<c>hooks.${DOMAIN}</c>) con esta ruta como prefijo: la API de
    /// envío y la de historial no son alcanzables desde internet ni por accidente.
    /// </summary>
    public const string Route = "/hooks/sendgrid/events";

    /// <summary>El nombre del endpoint, que es también como los tests lo encuentran.</summary>
    public const string EndpointName = "ReceiveSendGridEvents";

    /// <summary>
    /// Cabecera con la firma del lote, en base64. El nombre es del proveedor y lleva su marca
    /// anterior; no es un error de copia.
    /// </summary>
    public const string SignatureHeader = "X-Twilio-Email-Event-Webhook-Signature";

    /// <summary>Cabecera con la marca de tiempo firmada, en segundos Unix.</summary>
    public const string TimestampHeader = "X-Twilio-Email-Event-Webhook-Timestamp";

    /// <summary>
    /// El tope del cuerpo.
    /// <para>
    /// <b>Normativo: 2 MB, y se rechaza con 413 antes de leerlo a memoria.</b> El proveedor agrupa
    /// en lotes de 768 KB, así que dos megas es holgura de sobra y cualquier cosa por encima no es
    /// un lote suyo. El tope importa porque este endpoint no tiene autenticación previa: sin él,
    /// cualquiera en internet elige cuánta memoria le pedimos al proceso.
    /// </para>
    /// </summary>
    public const long MaxBodyBytes = 2 * 1024 * 1024;

    /// <inheritdoc />
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // Todo lo que necesita se registra en DomainServicesConfiguration, junto al resto.
    }

    /// <inheritdoc />
    public static void DefineEndpoints(IEndpointRouteBuilder app)
    {
        // .AllowAnonymous() es deliberado y es la quinta cosa de la revisión: el proveedor no es un
        // usuario del realm y no hay token que pueda traer. Lo que autentica el lote es la firma
        // ECDSA, verificada sobre los bytes tal como llegaron. Es la ÚNICA ruta no sanitaria del
        // servicio que sale de la fallback policy, y AnonymousRoutesTests lo fija.
        app.MapPost(Route, Receive)
            .AllowAnonymous()
            .WithName(EndpointName)
            .WithTags("Webhooks")
            .WithSummary(SendGridEventWebhookEndpointDescriptions.ReceiveSummary)
            .WithDescription(SendGridEventWebhookEndpointDescriptions.ReceiveDescription)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status413PayloadTooLarge);
    }

    /// <summary>
    /// El orden <b>es</b> la especificación, y cada paso está antes del siguiente por un motivo:
    /// <list type="number">
    /// <item>El tope de tamaño, <b>antes</b> de leer nada.</item>
    /// <item>El cuerpo crudo, como texto, sin tocarlo.</item>
    /// <item>La marca de tiempo y su ventana.</item>
    /// <item>La firma sobre <c>marca + cuerpo crudo</c>.</item>
    /// <item>Y recién ahí, el parseo.</item>
    /// </list>
    /// <para>
    /// Parsear antes de verificar rompería la verificación en silencio: el model binding vuelve a
    /// serializar, y lo que se firmó eran los bytes.
    /// </para>
    /// <para>
    /// <b>Es el único endpoint de la plataforma que registra en el log por su cuenta</b>, y no es
    /// un descuido de la convención: en todos los demás el fallo vuelve como error al cliente, que
    /// es una persona o un servicio hermano capaz de leerlo. Acá quien llama es SendGrid y no va a
    /// contarle a nadie que nuestra clave pública está mal; sin esta línea de log, esa avería no
    /// dejaría rastro en ningún lado.
    /// </para>
    /// </summary>
    private static async Task<IResult> Receive(
        HttpContext httpContext,
        [FromServices] IOptions<SendGridEventWebhookSettings> settings,
        [FromServices] INotificationEventIngest ingest,
        [FromServices] TimeProvider timeProvider,
        [FromServices] ILogger<SendGridEventWebhookEndpoint> logger,
        CancellationToken cancellationToken)
    {
        // 1. El tamaño, primero. Con Content-Length se corta sin leer un byte; sin él —un cuerpo
        //    troceado— el tope se le pone al servidor y Kestrel corta la lectura por nosotros.
        if (httpContext.Request.ContentLength > MaxBodyBytes)
        {
            logger.LogWarning(
                "Se rechaza un lote del webhook de {Length} bytes: el tope es {Max}",
                httpContext.Request.ContentLength,
                MaxBodyBytes);

            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var rawBody = await ReadRawBodyAsync(httpContext, cancellationToken);

        if (rawBody is null)
        {
            logger.LogWarning("Se rechaza un lote del webhook que excede el tope de {Max} bytes", MaxBodyBytes);

            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        // 2. Las dos cabeceras de la firma. Sin ellas no hay nada que verificar, y lo que no está
        //    firmado no entra.
        var signature = httpContext.Request.Headers[SignatureHeader].ToString();
        var timestamp = httpContext.Request.Headers[TimestampHeader].ToString();

        if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(timestamp))
            return Rejected(logger, "el lote llegó sin firma o sin marca de tiempo");

        // 3. La ventana de repetición. La marca se lee para comprobarla, pero lo que se firma y se
        //    verifica abajo es la CADENA tal como vino: reconstruirla desde el número podría no dar
        //    los mismos bytes. La lectura comprueba el rango además del formato, porque una fecha
        //    imposible construida sin comprobar sería una excepción sin capturar en el paso previo
        //    a verificar la firma.
        if (!EventWebhookSignature.TryReadSignedAt(timestamp, out var signedAt))
            return Rejected(logger, "la marca de tiempo del lote no es un instante válido");

        if (!EventWebhookSignature.IsFresh(signedAt, timeProvider.GetUtcNow()))
            return Rejected(logger, "la marca de tiempo del lote cae fuera de la ventana de repetición");

        // 4. La firma, sobre los bytes. Una clave sin configurar falla acá y falla cerrado: la
        //    alternativa sería aceptar cualquier cosa el día que alguien despliega a medias.
        if (!EventWebhookSignature.IsValid(settings.Value.PublicKey, timestamp, rawBody, signature))
        {
            // Un 403 y no un 200 silencioso. El fallo que de verdad va a pasar es nuestra propia
            // clave pública mal configurada, y un 200 tiraría cada evento para siempre; con un 403
            // el reintento rodante de 24 horas del proveedor se vuelve un plazo de gracia gratis.
            // A un falsificador de verdad tampoco le sirve de nada el 403.
            return Rejected(logger, "la firma del lote no corresponde a la clave pública configurada");
        }

        // 5. Y recién ahora se parsea.
        var events = SendGridEventBatch.TryParse(rawBody);

        if (events is null)
        {
            logger.LogWarning("El lote del webhook venía firmado pero mal formado");

            return Results.BadRequest();
        }

        var result = await ingest.IngestAsync(events, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        var summary = result.Value;

        logger.LogInformation(
            "Lote del webhook procesado: {Stored} nuevos, {Duplicates} repetidos, {Orphans} huérfanos, "
            + "{Suppressed} direcciones retiradas",
            summary.Stored,
            summary.Duplicates,
            summary.Orphans,
            summary.Suppressed);

        return Results.Ok();
    }

    /// <summary>
    /// El cuerpo tal como llegó, o <c>null</c> si se pasó del tope.
    /// <para>
    /// Se lee a mano y no por model binding: el binding parsea, y parsear antes de verificar es
    /// justo lo que la especificación prohíbe. El tope se le fija además al servidor, para el caso
    /// de un cuerpo troceado que llega sin <c>Content-Length</c> y del que no se sabe el tamaño
    /// hasta leerlo.
    /// </para>
    /// </summary>
    private static async Task<string?> ReadRawBodyAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (httpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
            limit.MaxRequestBodySize = MaxBodyBytes;

        try
        {
            // detectEncodingFromByteOrderMarks en false a propósito: por omisión es true, y entonces
            // un BOM al principio del cuerpo se lo come el lector. La firma del proveedor cubre esos
            // bytes, así que comérselos no abre nada pero sí rechazaría un lote legítimo.
            using var reader = new StreamReader(
                httpContext.Request.Body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true);

            return await reader.ReadToEndAsync(cancellationToken);
        }
        catch (BadHttpRequestException)
        {
            // Kestrel corta la lectura al pasarse del tope. Es el mismo hecho que el
            // Content-Length de más, y se contesta igual.
            return null;
        }
    }

    /// <summary>
    /// El 403 de toda falla de verificación, con el motivo solo en el log. Quien llama recibe el
    /// mismo cuerpo vacío en los cuatro casos: decirle cuál de los pasos falló es decirle cómo
    /// acercarse.
    /// </summary>
    private static IResult Rejected(ILogger logger, string reason)
    {
        logger.LogWarning("Se rechaza un lote del webhook de eventos: {Reason}", reason);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }
}
