using Sorteo360.Convocatorias.Core.Campaigns;

namespace Sorteo360.Convocatorias.Web.Handoff;

/// <summary>
/// La primera ejecución en segundo plano de la plataforma (ADR-0018). Drena el canal de ids de
/// campaña que llena el lanzamiento y entrega los lotes de cada una.
/// <para>
/// <b>No es un sondeo.</b> <c>ReadAllAsync</c> se queda esperando sobre un canal vacío, así que
/// este worker no consume nada mientras nadie lanza: no hay temporizador, ni intervalo, ni
/// barrido de la base al arrancar. El barrido se consideró y se descartó: se dispararía en cada
/// despliegue y reanudaría tan contento una campaña cuyo evento se canceló hace una hora.
/// </para>
/// <para>
/// <b>El token es el del host, nunca el de una petición.</b> El <c>stoppingToken</c> de un
/// <see cref="BackgroundService"/> se cancela cuando el host se para —el mismo momento del ciclo
/// de vida que <c>ApplicationStopping</c>—, mientras que un <c>CancellationToken</c> declarado en
/// un manejador de Minimal API es <c>HttpContext.RequestAborted</c> y se cancela al cerrarse la
/// pestaña. Ese es el fallo entero que ADR-0018 existe para impedir, y por eso
/// <see cref="ICampaignHandoffQueue.Enqueue"/> no admite token: no hay por dónde colarlo.
/// </para>
/// </summary>
public sealed class CampaignHandoffWorker : BackgroundService
{
    private readonly ICampaignHandoffQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CampaignHandoffWorker> _logger;

    public CampaignHandoffWorker(
        ICampaignHandoffQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<CampaignHandoffWorker> logger)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var campaignId in _queue.DequeueAllAsync(stoppingToken))
            {
                await HandOffAsync(campaignId, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // El host se está parando. Lo que quede sin entregar deja su campaña en `sending` con
            // filas pendientes, que es el hueco de durabilidad que ADR-0018 acepta a conciencia:
            // lo cierra una persona con `resume-handoff`, no una cola durable.
            _logger.LogInformation("El worker de entrega se detiene con el host.");
        }
    }

    /// <summary>
    /// Una campaña, en su propio ámbito: el <c>DbContext</c> es de ámbito y el de la petición que
    /// lanzó se cerró al escribirse la respuesta.
    /// <para>
    /// <b>Nada de lo que pase acá tumba el worker.</b> Una campaña que falla deja sus filas
    /// pendientes y la siguiente del canal se entrega igual: un bucle que muere con la primera
    /// excepción dejaría de mandar correos sin que nadie se entere hasta el próximo despliegue.
    /// </para>
    /// </summary>
    private async Task HandOffAsync(Guid campaignId, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();

            // Acá no se comprueba que haya transporte. Que falte no es un caso: la elección se
            // resuelve al arrancar y sin ella el host no se construye (spec 04 §4), así que
            // preguntarlo por campaña solo daría a entender que arrancar sin transporte es
            // posible.
            var handoff = scope.ServiceProvider.GetRequiredService<ICampaignHandoffService>();

            var result = await handoff.HandOffAsync(campaignId, stoppingToken);

            if (result.IsFailure)
            {
                _logger.LogError(
                    "La entrega de la campaña {CampaignId} se detuvo: {Error}",
                    campaignId,
                    result.Error.Message);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "La entrega de la campaña {CampaignId} falló.", campaignId);
        }
    }
}
