using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Infrastructure.Neighbours;

/// <summary>
/// La única forma de este servicio de traducir la avería de transporte de un vecino a su propio
/// <see cref="Result"/>. Es dueña de los dos <c>catch</c> y de nada más: qué significa la
/// respuesta —un nulo, un 409, una ficha que ya no existe— lo sigue decidiendo quien llama, porque
/// cada llamada lo lee distinto y esas diferencias sostienen carga.
/// <para>
/// Existe por el guard de cancelación. Copiado en cada llamada parece relleno, y por eso mismo se
/// olvidó una vez y solo apareció en revisión. Acá está escrito una sola vez, y
/// <see cref="NeighbourPageReader"/> también pasa por acá.
/// </para>
/// </summary>
public static class NeighbourCall
{
    /// <summary>
    /// Ejecuta una llamada a un vecino y convierte su avería de transporte en
    /// <paramref name="unavailable"/>. Lo que la llamada devuelva, éxito o fallo, pasa intacto.
    /// </summary>
    /// <param name="callAsync">La llamada con su lectura del cuerpo; puede envolver varias peticiones.</param>
    /// <param name="unavailable">El fallo que quiere levantar quien llama.</param>
    /// <param name="logger">El registro de quien llama, para que la categoría siga siendo la suya.</param>
    /// <param name="neighbour">Cómo se nombra al vecino en el registro, p. ej. <c>El CRM</c>.</param>
    /// <param name="action">
    /// Qué se le estaba pidiendo, como plantilla de registro: p. ej. <c>leer el evento {EventId}</c>.
    /// Los valores van en <paramref name="actionArgs"/> y no interpolados, para que sigan siendo
    /// propiedades del registro.
    /// </param>
    /// <param name="cancellationToken">Token de cancelación de quien llama.</param>
    /// <param name="actionArgs">Los valores de los marcadores de <paramref name="action"/>.</param>
    public static Task<Result<T, BaseError>> GuardAsync<T>(
        Func<Task<Result<T, BaseError>>> callAsync,
        BaseError unavailable,
        ILogger logger,
        string neighbour,
        string action,
        CancellationToken cancellationToken,
        params object?[] actionArgs)
    {
        ArgumentNullException.ThrowIfNull(unavailable);

        return GuardCoreAsync(
            callAsync,
            () => Result.Failure<T, BaseError>(unavailable),
            logger,
            neighbour,
            action,
            actionArgs,
            cancellationToken);
    }

    /// <inheritdoc cref="GuardAsync{T}" />
    public static Task<UnitResult<BaseError>> GuardAsync(
        Func<Task<UnitResult<BaseError>>> callAsync,
        BaseError unavailable,
        ILogger logger,
        string neighbour,
        string action,
        CancellationToken cancellationToken,
        params object?[] actionArgs)
    {
        ArgumentNullException.ThrowIfNull(unavailable);

        return GuardCoreAsync(
            callAsync,
            () => UnitResult.Failure(unavailable),
            logger,
            neighbour,
            action,
            actionArgs,
            cancellationToken);
    }

    /// <summary>
    /// Los dos <c>catch</c>, escritos una sola vez para las dos formas de resultado. Quien llama
    /// dice cómo se fabrica su fallo; el resto es idéntico.
    /// </summary>
    private static async Task<TResult> GuardCoreAsync<TResult>(
        Func<Task<TResult>> callAsync,
        Func<TResult> unavailable,
        ILogger logger,
        string neighbour,
        string action,
        object?[] actionArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callAsync);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        object?[] args = [neighbour, .. actionArgs];

        try
        {
            return await callAsync();
        }
        catch (HttpRequestException exception)
        {
            // Sin escalera de degradación a propósito: lo que se pedía no se puede construir de
            // la nada, así que el fallo es de la petición entera (spec 03 §6.2, ADR-0017).
            logger.LogError(exception, "{Neighbour} no respondió al " + action, args);

            return unavailable();
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // El vecino aceptó la conexión y no contestó a tiempo. Para quien preguntaba es lo
            // mismo que si estuviera caído, y se lo dice igual. El guard es lo que separa eso de
            // que se haya ido quien preguntaba: sin él, un visitante que cierra la pestaña queda
            // registrado como una caída del vecino que nunca ocurrió.
            logger.LogError(exception, "{Neighbour} agotó el tiempo de espera al " + action, args);

            return unavailable();
        }
    }
}
