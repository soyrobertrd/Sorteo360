using System.Text.Json;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Convocatorias.Web.Routing;

namespace Sorteo360.Convocatorias.Web.RateLimiting;

/// <summary>
/// Lo que llena las claves de partición que <see cref="PublicRateLimiting"/> reparte: el token y
/// la dirección de correo, leídos del cuerpo de la petición.
/// <para>
/// Vive aparte de las políticas a propósito. Ahí se decide <b>cuánto</b> se permite y por qué eje;
/// acá se lee un cuerpo JSON con tope y se rebobina. Son dos motivos distintos para tocar un
/// archivo, y juntos era uno solo que cambiaba por los dos.
/// </para>
/// </summary>
public static class PublicRateLimitPartitions
{
    /// <summary>
    /// Los nombres de las dos propiedades del cuerpo que particionan. Son las que escribe el
    /// portal en <c>resolve</c> y en el formulario de inscripción.
    /// </summary>
    private const string TokenProperty = "token";

    private const string EmailProperty = "email";

    /// <summary>
    /// Cuánto cuerpo se lee para sacar la clave. Un formulario de inscripción no llega ni a
    /// una décima parte; lo que esto impide es que alguien haga que el limitador —que corre
    /// antes de cualquier tope de tamaño del endpoint— se coma un cuerpo enorme por petición.
    /// Un cuerpo más largo no se lee y esa petición reparte por IP.
    /// </summary>
    private const int MaxPartitionBodyBytes = 8 * 1024;

    /// <summary>
    /// Deja en <c>HttpContext.Items</c> el token y la dirección que particionan los dos ejes de
    /// la spec <c>06</c> §3, leyéndolos del cuerpo.
    /// <para>
    /// <b>Tiene que ir antes de <c>UseRateLimiter</c>.</b> El particionador del limitador es
    /// síncrono y corre antes de que exista un endpoint, así que no puede leer el cuerpo por su
    /// cuenta; dejarlo desde el handler llegaría una petición tarde, porque para entonces el
    /// permiso de esta ya se pidió.
    /// </para>
    /// <para>
    /// <b>Y por eso los dos ejes existen.</b> Sin ellos, enganchar la política por correo a la
    /// inscripción sería un segundo límite por IP mucho más estrecho que el global, y una
    /// oficina entera inscribiéndose desde una sola salida a internet rebotaría a la sexta
    /// persona.
    /// </para>
    /// </summary>
    public static IApplicationBuilder UsePublicRateLimitPartitions(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (context, next) =>
        {
            await ReadPartitionKeysAsync(context);
            await next(context);
        });
    }

    /// <summary>
    /// Lee las dos claves del cuerpo, si el cuerpo es un JSON de un tamaño razonable. Cualquier
    /// tropiezo —no es JSON, está mal formado, no trae esas propiedades— deja las claves sin
    /// poner, y entonces el eje reparte por IP. <b>Nunca falla la petición</b>: esto decide
    /// cómo se cuenta un límite, no si el cuerpo sirve, y eso lo dice el validador después.
    /// </summary>
    private static async Task ReadPartitionKeysAsync(HttpContext context)
    {
        var request = context.Request;

        if (!HttpMethods.IsPost(request.Method)
            || !ApiRoutes.IsPublicPath(request.Path)
            || !request.HasJsonContentType()
            || request.ContentLength > MaxPartitionBodyBytes)
        {
            return;
        }

        // El cuerpo se lee una vez y se rebobina: detrás viene el binder del endpoint, que
        // espera encontrarlo entero y desde el principio.
        request.EnableBuffering();

        try
        {
            // Se lee con tope propio y no confiando en `Content-Length`: una petición en
            // `chunked` no lo declara, y saltarse el eje por eso lo dejaría sin más trabajo que
            // no mandar la cabecera.
            var body = await ReadCappedAsync(request.Body, context.RequestAborted);

            if (body is null)
                return;

            using var document = JsonDocument.Parse(body);

            if (document.RootElement.ValueKind is not JsonValueKind.Object)
                return;

            CapturePartitionKey(
                context,
                PublicRateLimiting.TokenPartitionItemKey,
                document.RootElement,
                TokenProperty,
                NormalizeToken);

            CapturePartitionKey(
                context,
                PublicRateLimiting.EmailPartitionItemKey,
                document.RootElement,
                EmailProperty,
                EmailAddressShape.Normalize);
        }
        catch (JsonException)
        {
            // Un cuerpo mal formado no es asunto de acá: lo contesta el binder con su 400.
        }
        finally
        {
            request.Body.Position = 0;
        }
    }

    /// <summary>
    /// Hasta <see cref="MaxPartitionBodyBytes"/> del cuerpo, o <c>null</c> si hay más. Devolver
    /// <c>null</c> deja la petición repartiendo por IP, que es el mismo sitio al que cae
    /// cualquier otro cuerpo que no se entiende.
    /// </summary>
    private static async Task<byte[]?> ReadCappedAsync(Stream body, CancellationToken cancellationToken)
    {
        var buffer = new byte[MaxPartitionBodyBytes + 1];
        var read = await body.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken);

        return read > MaxPartitionBodyBytes ? null : buffer[..read];
    }

    /// <summary>
    /// Guarda el valor de una propiedad del cuerpo como clave de partición, normalizado con lo
    /// que le pase quien llama.
    /// <para>
    /// <b>La propiedad se busca sin distinguir mayúsculas</b>, porque el binder de Minimal APIs
    /// tampoco las distingue: un cuerpo con <c>"Email"</c> se inscribe igual, y una búsqueda
    /// sensible acá lo dejaría sin clave y repartiendo por IP. El eje se esquivaría escribiendo
    /// el nombre del campo con otra caja.
    /// </para>
    /// </summary>
    private static void CapturePartitionKey(
        HttpContext context,
        string itemKey,
        JsonElement body,
        string property,
        Func<string, string> normalize)
    {
        var match = body
            .EnumerateObject()
            .FirstOrDefault(candidate =>
                string.Equals(candidate.Name, property, StringComparison.OrdinalIgnoreCase));

        if (match.Value.ValueKind is not JsonValueKind.String)
            return;

        var value = match.Value.GetString();

        if (string.IsNullOrWhiteSpace(value))
            return;

        context.Items[itemKey] = normalize(value);
    }

    /// <summary>
    /// Lo que se le hace al token para usarlo de clave: recortarlo y nada más. <b>Distingue
    /// mayúsculas</b> y tiene que seguir haciéndolo: dos tokens que solo se diferencien en una
    /// son dos tokens distintos.
    /// </summary>
    private static string NormalizeToken(string value) => value.Trim();
}
