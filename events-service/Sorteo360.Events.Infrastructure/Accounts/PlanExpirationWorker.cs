using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sorteo360.Events.Core.Accounts;

namespace Sorteo360.Events.Infrastructure.Accounts;

/// <summary>
/// Revisa periódicamente cuentas cuyo período pagado venció (las baja a <c>free</c> sin borrar
/// datos) o vence en los próximos 7 días (envía aviso por correo, §9.5).
/// </summary>
public sealed class PlanExpirationWorker : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PlanExpirationWorker> _logger;

    public PlanExpirationWorker(IServiceScopeFactory scopeFactory, ILogger<PlanExpirationWorker> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<IPlanRequestService>();
                var result = await service.ProcessExpirationsAsync(stoppingToken);
                if (result.IsFailure)
                {
                    _logger.LogWarning("Fallo al revisar vencimientos de planes: {Code} {Message}",
                        result.Error.Code, result.Error.Message);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error inesperado en PlanExpirationWorker");
            }
        }
    }
}

