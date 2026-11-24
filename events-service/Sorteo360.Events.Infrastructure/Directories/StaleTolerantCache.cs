using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Sorteo360.Events.Infrastructure.Directories;

/// <summary>
/// La máquina que comparten los directorios que guardan una copia de un origen externo.
/// <para>
/// <b>Una copia sobrevive a su propia vigencia.</b> Esa es la regla entera, y está dicha acá una
/// sola vez. Si el TTL fuera la expiración de la entrada, al vencer se destruiría la copia justo
/// cuando el origen caído la hace necesaria; en cambio se guarda con una retención larga, la fecha
/// de obtención viaja dentro del valor, y renovar es una decisión que se toma al leer. Una
/// renovación fallida no es un error: se sirve la copia anterior por vieja que sea.
/// </para>
/// <para>
/// Lo que cada directorio conserva suyo —porque de verdad difiere— son sus vigencias, sus
/// retenciones, sus claves, qué fallos se traga, si toma cerrojo y cómo traduce el resultado a su
/// propio vocabulario de degradación. Todo eso viaja en <see cref="CacheCopyPolicy"/>.
/// </para>
/// <para>
/// Vive en events-service y no en el chasis compartido a propósito: ningún otro servicio la
/// necesita todavía, y elegir su forma definitiva con un solo consumidor sería adivinar
/// (ADR-0012).
/// </para>
/// </summary>
internal sealed class StaleTolerantCache
{
    private readonly IDistributedCache _cache;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    /// <param name="cache">Dónde viven las copias.</param>
    /// <param name="timeProvider">El reloj con el que se mide la vigencia.</param>
    /// <param name="logger">
    /// El del directorio que la construye, no uno propio: así una entrada ilegible se anota bajo la
    /// categoría del directorio que la leyó, que es lo que dice qué copia está corrupta.
    /// </param>
    internal StaleTolerantCache(IDistributedCache cache, TimeProvider timeProvider, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _cache = cache;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc cref="ReadOrRefreshAsync{T}(string, CacheCopyPolicy, Func{T?, bool}?, Func{CancellationToken, Task{T?}}, Action{Exception}, CancellationToken)" />
    internal Task<CacheLookup<T>> ReadOrRefreshAsync<T>(
        string cacheKey,
        CacheCopyPolicy policy,
        Func<CancellationToken, Task<T?>> refreshAsync,
        Action<Exception> logFailure,
        CancellationToken cancellationToken)
        where T : class =>
        ReadOrRefreshAsync(cacheKey, policy, stillApplies: null, refreshAsync, logFailure, cancellationToken);

    /// <summary>
    /// Sirve la copia guardada si sirve tal cual, y si no, renueva: toma el turno, relee por si
    /// otra petición se adelantó, pregunta al origen y guarda.
    /// </summary>
    /// <param name="cacheKey">Dónde vive esta copia.</param>
    /// <param name="policy">Cómo se comporta esta copia en concreto.</param>
    /// <param name="stillApplies">
    /// Una condición extra para que la copia guardada sirva, más allá de su vigencia. Hoy solo la
    /// usa el directorio de aprobadores, cuya copia de personas guarda de qué cierre de roles
    /// salió y deja de servir si el cierre cambió. No incluye el TTL: eso lo mide esta clase.
    /// <para>
    /// Recibe el valor tal cual, <b>ausencia incluida</b>. Declarar buena una ausencia por su
    /// cuenta sería la trampa exacta que esta clase existe para evitar: una copia que guarda "no
    /// hay nadie" y además tiene condición extra se serviría toda su vigencia aunque la condición
    /// hubiera dejado de cumplirse. Quién decide qué vale una ausencia es quien la guarda.
    /// </para>
    /// </param>
    /// <param name="refreshAsync">Cómo se obtiene una copia nueva. Puede contestar nada.</param>
    /// <param name="logFailure">
    /// Qué anotar si el origen no contesta. Es un delegado y no un texto para que cada plantilla de
    /// log siga siendo constante y pueda llevar sus propios campos.
    /// </param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    internal async Task<CacheLookup<T>> ReadOrRefreshAsync<T>(
        string cacheKey,
        CacheCopyPolicy policy,
        Func<T?, bool>? stillApplies,
        Func<CancellationToken, Task<T?>> refreshAsync,
        Action<Exception> logFailure,
        CancellationToken cancellationToken)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(refreshAsync);
        ArgumentNullException.ThrowIfNull(logFailure);

        var cached = await ReadAsync<T>(cacheKey, cancellationToken);
        if (IsUsable(cached, policy, stillApplies))
            return new CacheLookup<T>(CacheOutcome.Fresh, cached!.Value, cached.FetchedAt);

        if (policy.RefreshLock is null)
            return await RefreshAsync(cacheKey, policy, cached, refreshAsync, logFailure, cancellationToken);

        await policy.RefreshLock.WaitAsync(cancellationToken);
        try
        {
            // Otra petición pudo renovar mientras se esperaba el turno; releer evita repetir su
            // consulta al origen.
            var latest = await ReadAsync<T>(cacheKey, cancellationToken);
            if (IsUsable(latest, policy, stillApplies))
                return new CacheLookup<T>(CacheOutcome.Fresh, latest!.Value, latest.FetchedAt);

            return await RefreshAsync(cacheKey, policy, latest, refreshAsync, logFailure, cancellationToken);
        }
        finally
        {
            policy.RefreshLock.Release();
        }
    }

    /// <summary>
    /// Pregunta al origen y guarda lo que conteste. Si el origen falla de una forma que esta copia
    /// se traga, se sirve <paramref name="previous"/> por vencida que esté, y solo se informa
    /// <see cref="CacheOutcome.Missing"/> cuando nunca hubo ninguna.
    /// </summary>
    private async Task<CacheLookup<T>> RefreshAsync<T>(
        string cacheKey,
        CacheCopyPolicy policy,
        CachedCopy<T>? previous,
        Func<CancellationToken, Task<T?>> refreshAsync,
        Action<Exception> logFailure,
        CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            var value = await refreshAsync(cancellationToken);

            // Una ausencia se guarda solo si esta copia vive de guardarla. Donde no, volver a
            // preguntar es barato y lo que falta hoy puede aparecer mañana.
            var fetchedAt = _timeProvider.GetUtcNow();

            if (value is not null || policy.CachesAbsence)
                await WriteAsync(cacheKey, new CachedCopy<T>(fetchedAt, value), policy.Retention, cancellationToken);

            return new CacheLookup<T>(CacheOutcome.Fresh, value, fetchedAt);
        }
        catch (Exception exception) when (IsTolerated(exception, policy))
        {
            logFailure(exception);

            return previous is not null
                ? new CacheLookup<T>(CacheOutcome.Stale, previous.Value, previous.FetchedAt)
                : new CacheLookup<T>(CacheOutcome.Missing, null);
        }
    }

    /// <summary>
    /// Qué fallo del origen se traga esta copia. El chasis garantiza que todo fallo de red, de
    /// autenticación o de estado sale como <see cref="HttpRequestException"/>. La
    /// <see cref="JsonException"/> es un 200 con un cuerpo que no se puede leer, que es el mismo
    /// problema visto desde otro ángulo, pero solo algunas copias la tragan: ensancharlo a todas
    /// convertiría un error de contrato en una lectura silenciosamente vieja.
    /// </summary>
    private static bool IsTolerated(Exception exception, CacheCopyPolicy policy) =>
        exception is HttpRequestException
        || (policy.SwallowsUnreadableResponses && exception is JsonException);

    /// <summary>
    /// Decide si la copia guardada sirve tal cual: que exista, que no haya superado su vigencia, y
    /// que pase la condición extra si esta copia tiene una.
    /// </summary>
    private bool IsUsable<T>(CachedCopy<T>? copy, CacheCopyPolicy policy, Func<T?, bool>? stillApplies)
        where T : class
    {
        if (copy is null || _timeProvider.GetUtcNow() - copy.FetchedAt >= policy.Ttl)
            return false;

        return stillApplies is null || stillApplies(copy.Value);
    }

    /// <summary>Lee y deserializa una copia, o <c>null</c> si no está o no se puede leer.</summary>
    private async Task<CachedCopy<T>?> ReadAsync<T>(string cacheKey, CancellationToken cancellationToken)
        where T : class
    {
        var payload = await _cache.GetAsync(cacheKey, cancellationToken);
        if (payload is null)
            return null;

        try
        {
            return JsonSerializer.Deserialize<CachedCopy<T>>(payload, JsonSerializerOptions.Web);
        }
        catch (JsonException exception)
        {
            // Una entrada que no se puede leer es una entrada que no existe: se descarta y se
            // renueva, en lugar de tumbar la lectura por el formato de una caché.
            _logger.LogWarning(exception, "Entrada de caché ilegible en {CacheKey}; se descarta.", cacheKey);
            return null;
        }
    }

    /// <summary>
    /// Guarda una copia con la retención que le toca. La vigencia no se expresa acá: viaja dentro
    /// del valor, para que una copia vencida siga existiendo.
    /// </summary>
    private Task WriteAsync<T>(string cacheKey, CachedCopy<T> copy, TimeSpan retention, CancellationToken cancellationToken)
        where T : class
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(copy, JsonSerializerOptions.Web);

        return _cache.SetAsync(
            cacheKey,
            payload,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = retention },
            cancellationToken);
    }
}

/// <summary>
/// Cómo se comporta una copia concreta. Todo lo que hay acá difiere de verdad entre directorios;
/// lo que comparten es la máquina que lo aplica.
/// </summary>
/// <param name="Ttl">Cuánto vale la copia antes de que haya que renovarla.</param>
/// <param name="Retention">
/// Cuánto sobrevive la entrada, que no tiene nada que ver con su vigencia: es solo el techo que
/// impide que algo consultado una vez ocupe memoria para siempre.
/// </param>
/// <param name="RefreshLock">
/// El cerrojo que serializa la renovación, o <c>null</c> si esta copia no toma ninguno. Los
/// rellenos por id no lo toman: son la cola larga de un directorio, no la renovación de la copia
/// completa que veinticinco lecturas simultáneas dispararían a la vez.
/// </param>
/// <param name="SwallowsUnreadableResponses">
/// Si un cuerpo que no se puede leer cuenta como origen caído. Ver
/// <c>StaleTolerantCache.IsTolerated</c>.
/// </param>
/// <param name="CachesAbsence">
/// Si guardar "no hay nada" es una respuesta que vale la pena guardar. Quien se fue del realm
/// resuelve a nada y esa nada se guarda; una oficina que hoy no está en el CRM se vuelve a pedir.
/// </param>
internal sealed record CacheCopyPolicy(
    TimeSpan Ttl,
    TimeSpan Retention,
    DirectoryRefreshLock? RefreshLock = null,
    bool SwallowsUnreadableResponses = false,
    bool CachesAbsence = false);

/// <summary>Qué tan vigente es lo que devolvió una lectura.</summary>
internal enum CacheOutcome
{
    /// <summary>Salió de una copia vigente o de una renovación que funcionó.</summary>
    Fresh,

    /// <summary>La renovación falló y se sirvió la copia anterior.</summary>
    Stale,

    /// <summary>La renovación falló y nunca hubo copia.</summary>
    Missing,
}

/// <summary>
/// Lo que una lectura devolvió y de dónde salió. Cada directorio traduce el
/// <see cref="CacheOutcome"/> a su propio vocabulario de degradación; esta clase no conoce
/// ninguno. No confundir con <see cref="CachedCopy{T}"/>, que es lo que se guarda: esto es lo que
/// sale de leer.
/// <para>
/// <c>FetchedAt</c> es cuándo se obtuvo lo servido: la de la copia vieja si se sirvió vencida, y
/// nula solo con <see cref="CacheOutcome.Missing"/>. La necesita quien muestra la antigüedad de lo
/// que sirve, como la lista de la puerta del sorteo.
/// </para>
/// </summary>
internal readonly record struct CacheLookup<T>(CacheOutcome Outcome, T? Value, DateTimeOffset? FetchedAt = null)
    where T : class;

/// <summary>
/// Una copia guardada con el momento en que se obtuvo.
/// <para>
/// <c>Value</c> puede ser <c>null</c>, y eso es distinto de que no haya entrada: la entrada
/// existe y dice que el origen contestó que no hay nada. Confundir los dos casos haría que una
/// copia que guarda una ausencia volviera a preguntar en cada lectura para siempre.
/// </para>
/// </summary>
internal sealed record CachedCopy<T>(DateTimeOffset FetchedAt, T? Value)
    where T : class;
