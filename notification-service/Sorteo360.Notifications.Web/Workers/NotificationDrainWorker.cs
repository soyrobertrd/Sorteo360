using Sorteo360.Notifications.Core.Sending;

namespace Sorteo360.Notifications.Web.Workers;

/// <summary>
/// El worker que vacía la cola de envíos. Es el primer <c>BackgroundService</c> del monorepo, y
/// vive acá porque este es el servicio cuyo trabajo es mandar.
/// <para>
/// Mandar ocurre fuera de la petición porque el transporte obliga: mil personas son mil
/// transacciones SMTP secuenciales, con latencia por mensaje que nadie publica y sin techo. Dentro
/// de la petición eso es un tiempo agotado ambiguo para quien llama, que es justo el caso difícil
/// de idempotencia.
/// </para>
/// </summary>
public sealed class NotificationDrainWorker : BackgroundService
{
    /// <summary>
    /// Cada cuánto se mira la cola. No es configuración: no hay ninguna decisión de despliegue
    /// detrás, y una séptima variable <c>Smtp__*</c> tendría que viajar a los tres compose para no
    /// decir nada.
    /// </summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    /// <summary>Espera tras una vuelta que se rompió entera, para no golpear un servidor caído.</summary>
    private static readonly TimeSpan ErrorBackoff = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationDrainWorker> _logger;

    public NotificationDrainWorker(IServiceScopeFactory scopeFactory, ILogger<NotificationDrainWorker> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("El worker de envío arrancó; revisa la cola cada {Interval}", PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            var wait = PollInterval;

            try
            {
                // Un scope por vuelta: el DbContext es scoped y una vuelta es una unidad de
                // trabajo, aunque dentro escriba fila por fila.
                await using var scope = _scopeFactory.CreateAsyncScope();
                var drain = scope.ServiceProvider.GetRequiredService<INotificationDrainService>();

                var drained = await drain.DrainAsync(stoppingToken);

                if (drained > 0)
                    _logger.LogInformation("Vuelta del worker: {Drained} envíos resueltos", drained);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // El worker no se muere: un fallo acá es el correo parado, y el correo parado se
                // descubre mirando. Se registra, se espera más y se vuelve a intentar.
                _logger.LogError(ex, "La vuelta del worker de envío falló entera; se reintenta");
                wait = ErrorBackoff;
            }

            try
            {
                await Task.Delay(wait, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("El worker de envío se detuvo");
    }
}
