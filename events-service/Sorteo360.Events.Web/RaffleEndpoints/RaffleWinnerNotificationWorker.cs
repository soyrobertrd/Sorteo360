using System.Security.Claims;
using System.Threading.Channels;
using Sorteo360.Events.Core.Raffles;

namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>
/// Manda los correos de los ganadores después de completar, fuera de la petición (A31): con 10.000
/// ganadores el envío tarda minutos y colgaba <c>POST …/complete</c> hasta pasar el tiempo del proxy.
/// <para>
/// La cola vive en memoria. Si el proceso se cae a mitad, los sorteos ya avisados quedan con su
/// estado y el resto en nulo, que es lo mismo que ya pasaba con el envío en línea (spec 03 §5.1).
/// </para>
/// </summary>
public sealed class RaffleWinnerNotificationWorker : BackgroundService
{
    private readonly Channel<(Guid EventId, Guid AccountId)> _queue = Channel.CreateUnbounded<(Guid, Guid)>();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RaffleWinnerNotificationWorker> _logger;

    public RaffleWinnerNotificationWorker(IServiceScopeFactory scopeFactory, ILogger<RaffleWinnerNotificationWorker> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Encola los correos del sorteo del evento. Lleva la cuenta de quien completó: fuera de la
    /// petición no hay usuario y el filtro de cuenta escondería el evento.
    /// </summary>
    public void Enqueue(Guid eventId, Guid accountId) => _queue.Writer.TryWrite((eventId, accountId));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var (eventId, accountId) in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var httpContext = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
            try
            {
                // El acceso a la cuenta lee el claim del HttpContext; este es el de quien completó.
                httpContext.HttpContext = new DefaultHttpContext
                {
                    RequestServices = scope.ServiceProvider,
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("account_id", accountId.ToString())], "worker")),
                };

                await scope.ServiceProvider.GetRequiredService<IRaffleService>().NotifyWinnersAsync(eventId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "No se pudieron mandar los correos de los ganadores del evento {EventId}.", eventId);
            }
            finally
            {
                httpContext.HttpContext = null;
            }
        }
    }
}
