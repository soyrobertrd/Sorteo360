using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Sorteo360.Convocatorias.Web.Routing;

namespace Sorteo360.Convocatorias.Web.RateLimiting;

/// <summary>
/// Los límites de peticiones del prefijo público, sobre el limitador que trae ASP.NET.
/// Solo el portal se limita: lo interno está detrás de Keycloak y de un rol, y ahí el
/// abuso tiene nombre y apellido.
/// <para>
/// Los contadores viven en la memoria del proceso, como el resto de la caché de la
/// plataforma. Corre un solo contenedor, así que por instancia es
/// exacto, y un redespliegue que los reinicie abre una ventana de segundos que nadie está
/// esperando. Redis se descartó por mérito: comparte contadores entre instancias, que no es
/// un problema que tengamos, y el limitador de ASP.NET no trae proveedor distribuido.
/// </para>
/// </summary>
public static class PublicRateLimiting
{
    /// <summary>Eje por token: la enumeración de tokens que rota de IP.</summary>
    public const string PerTokenPolicy = "public-per-token";

    /// <summary>Eje por correo: usar el portal para bombardear a un tercero.</summary>
    public const string PerEmailPolicy = "public-per-email";

    /// <summary>
    /// Clave de <c>HttpContext.Items</c> donde queda el token que particiona.
    /// El limitador corre antes del handler y su particionador es síncrono, así que no
    /// puede leer el cuerpo por su cuenta: lo deja <see cref="UsePublicRateLimitPartitions"/>,
    /// que corre por delante de él.
    /// Cuando nadie la escribe —un cuerpo que no es JSON, uno sin esa propiedad— este eje
    /// reparte por IP como el global, nunca sin límite.
    /// </summary>
    public const string TokenPartitionItemKey = "RateLimit:Token";

    /// <summary>Igual que <see cref="TokenPartitionItemKey"/>, para la dirección de correo.</summary>
    public const string EmailPartitionItemKey = "RateLimit:Email";

    /// <summary>
    /// Código máquina del 429. No pasa por la tabla <c>ErrorKind</c> → estado: 429 no es un
    /// fallo de dominio, lo emite el middleware antes de llegar a ningún servicio.
    /// </summary>
    public const string RateLimitedCode = "RATE_LIMITED";

    private const string RateLimitedMessage =
        "Demasiados intentos. Espera un momento y vuelve a intentarlo.";

    /// <summary>Inundación corriente contra el portal entero.</summary>
    public const int PerIpPermitLimit = 60;

    /// <summary>Enumeración de tokens: quien tiene el suyo no necesita probar veinte por minuto.</summary>
    private const int PerTokenPermitLimit = 10;

    /// <summary>
    /// Reenvío de correo a un tercero. Es el eje más estrecho porque cada permiso que
    /// concede es un mensaje que le llega a alguien que no lo pidió.
    /// </summary>
    private const int PerEmailPermitLimit = 5;

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Registra el limitador global sobre el prefijo público y las dos políticas con nombre
    /// que los endpoints del portal enganchan con <c>RequireRateLimiting</c>.
    /// </summary>
    public static IServiceCollection AddPublicRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                ApiRoutes.IsPublicPath(httpContext.Request.Path)
                    ? FixedWindow($"ip:{ClientKey(httpContext)}", PerIpPermitLimit)
                    : RateLimitPartition.GetNoLimiter("internal"));

            options.AddPolicy(PerTokenPolicy, httpContext =>
                FixedWindow(
                    $"token:{PartitionValue(httpContext, TokenPartitionItemKey)}",
                    PerTokenPermitLimit));

            options.AddPolicy(PerEmailPolicy, httpContext =>
                FixedWindow(
                    $"email:{PartitionValue(httpContext, EmailPartitionItemKey)}",
                    PerEmailPermitLimit));

            options.OnRejected = OnRejectedAsync;
        });

        return services;
    }

    private static RateLimitPartition<string> FixedWindow(string partitionKey, int permitLimit) =>
        RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = Window,
            QueueLimit = 0,
            AutoReplenishment = true,
        });

    /// <summary>
    /// La IP del cliente. Vale lo que valga <c>UseForwardedHeaders</c>: sin él todas las
    /// peticiones llegan desde el contenedor de nginx y el eje por IP se vuelve uno global.
    /// </summary>
    private static string ClientKey(HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    /// <summary>
    /// El valor que particiona, o la IP si el endpoint no dejó ninguno. Se cae a la IP y no
    /// a una clave compartida a propósito: una clave compartida convertiría el eje en un
    /// límite global que cualquiera puede agotar para todos.
    /// </summary>
    private static string PartitionValue(HttpContext httpContext, string itemKey) =>
        httpContext.Items.TryGetValue(itemKey, out var value) && value is string { Length: > 0 } text
            ? text
            : ClientKey(httpContext);

    /// <summary>
    /// El 429 que ve el portal: <c>Retry-After</c> para saber cuánto esperar y un código
    /// máquina para poder decirlo en español sin leer la prosa.
    /// </summary>
    private static ValueTask OnRejectedAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var metadata)
            ? metadata
            : Window;

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers.RetryAfter =
            ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

        return new ValueTask(Results.Problem(
            RateLimitedMessage,
            statusCode: StatusCodes.Status429TooManyRequests,
            extensions: new Dictionary<string, object?> { ["code"] = RateLimitedCode })
            .ExecuteAsync(context.HttpContext));
    }
}
