using System.Net;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace Sorteo360.Platform.Http;

/// <summary>
/// Construye la tubería de resiliencia con la que se llama a otro servicio: tiempo de
/// espera total, reintentos con backoff exponencial y jitter, cortacircuitos y tiempo de
/// espera por intento. Los valores viven aquí, junto al handler que los ejecuta, y no en
/// el registro de dependencias.
/// </summary>
internal static class ServiceResiliencePipeline
{
    /// <summary>Tiempo máximo de un intento individual contra el destino.</summary>
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Tiempo máximo de la llamada completa, reintentos incluidos.</summary>
    private static readonly TimeSpan TotalTimeout = TimeSpan.FromSeconds(30);

    private const int MaxRetryAttempts = 2;
    private static readonly TimeSpan RetryBaseDelay = TimeSpan.FromMilliseconds(200);

    private const int CircuitMinimumThroughput = 10;
    private const double CircuitFailureRatio = 0.5;
    private static readonly TimeSpan CircuitSamplingDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CircuitBreakDuration = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Tubería para un destino concreto. Se comparte entre peticiones —el cortacircuitos
    /// necesita memoria de lo ocurrido— y por eso vive como singleton y no en el handler,
    /// que la factory de clientes recicla cada pocos minutos.
    /// </summary>
    /// <param name="serviceName">Destino al que se llama; solo se usa para los logs.</param>
    /// <param name="logger">Registro de reintentos y aperturas de circuito.</param>
    public static ResiliencePipeline<HttpResponseMessage> Build(string serviceName, ILogger logger)
    {
        var shouldHandle = new PredicateBuilder<HttpResponseMessage>()
            // Un fallo al obtener el token es del emisor, no del destino: ni se reintenta
            // aquí (el handler del token ya reintenta lo que tiene sentido reintentar) ni
            // debe contar como avería del destino y abrirle el circuito estando sano.
            .Handle<HttpRequestException>(static ex => ex is not ServiceAuthenticationException)
            .Handle<TimeoutRejectedException>()
            .HandleResult(static response => IsTransientFailure(response.StatusCode));

        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddTimeout(TotalTimeout)
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                ShouldHandle = shouldHandle,
                MaxRetryAttempts = MaxRetryAttempts,
                Delay = RetryBaseDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "Reintento {Attempt} contra el servicio {ServiceName}: {Outcome}.",
                        args.AttemptNumber + 1,
                        serviceName,
                        args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString());

                    // La respuesta descartada no llega a nadie: si no se libera aquí,
                    // su conexión queda retenida hasta el recolector.
                    args.Outcome.Result?.Dispose();
                    return default;
                },
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                ShouldHandle = shouldHandle,
                FailureRatio = CircuitFailureRatio,
                MinimumThroughput = CircuitMinimumThroughput,
                SamplingDuration = CircuitSamplingDuration,
                BreakDuration = CircuitBreakDuration,
                OnOpened = args =>
                {
                    logger.LogError(
                        "Se abre el circuito hacia el servicio {ServiceName} durante {BreakDuration}.",
                        serviceName,
                        args.BreakDuration);
                    return default;
                },
                OnClosed = _ =>
                {
                    logger.LogInformation("Se cierra el circuito hacia el servicio {ServiceName}.", serviceName);
                    return default;
                },
            })
            .AddTimeout(AttemptTimeout)
            .Build();
    }

    /// <summary>
    /// Respuestas que merecen otro intento: el destino no está listo, está saturado o cortó
    /// por tiempo. Un 4xx (salvo 408 y 429) es su respuesta definitiva y repetirla no la cambia.
    /// </summary>
    private static bool IsTransientFailure(HttpStatusCode statusCode) =>
        (int)statusCode >= 500
        || statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests;
}
