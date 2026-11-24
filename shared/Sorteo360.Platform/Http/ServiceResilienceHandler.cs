using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Sorteo360.Platform.Http;

/// <summary>
/// Ejecuta la petición saliente dentro de la tubería de resiliencia del destino (tiempos
/// de espera, reintentos y cortacircuitos) y traduce lo que Polly lanza a
/// <see cref="HttpRequestException"/>.
/// <para>
/// La traducción es lo que mantiene la frontera: quien escribe un cliente tipado captura
/// <see cref="HttpRequestException"/> y traduce a <see cref="Errors.ErrorKind.Unavailable"/>
/// sin conocer Polly ni sus tipos.
/// </para>
/// </summary>
public sealed class ServiceResilienceHandler : DelegatingHandler
{
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;
    private readonly string _serviceName;

    public ServiceResilienceHandler(ResiliencePipeline<HttpResponseMessage> pipeline, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        _pipeline = pipeline;
        _serviceName = serviceName;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _pipeline.ExecuteAsync(
                async token => await base.SendAsync(request, token),
                cancellationToken);
        }
        catch (TimeoutRejectedException ex)
        {
            throw new HttpRequestException(
                $"El servicio '{_serviceName}' no respondió dentro del tiempo de espera.", ex);
        }
        catch (BrokenCircuitException ex)
        {
            // El cortacircuitos está abierto: se corta aquí en lugar de encolar peticiones
            // contra un destino que se sabe caído.
            throw new HttpRequestException(
                $"El servicio '{_serviceName}' está marcado como no disponible tras fallos consecutivos.", ex);
        }
    }
}
