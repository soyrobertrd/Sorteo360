using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Infrastructure.Directories;

namespace Sorteo360.Events.Infrastructure.Raffles;

/// <summary>
/// La lista de la puerta para las lecturas del borrador, en una copia por evento sobre
/// <see cref="StaleTolerantCache"/>: una lectura fallida sirve la copia anterior por vieja que sea.
/// <para>
/// Guarda solo la lista de la puerta, nunca el bombo calculado: DO Travel Awards, las decisiones
/// manuales y el CRM se aplican en cada lectura, así que lo único que puede atrasarse son las
/// llegadas (spec 01 §8).
/// </para>
/// </summary>
public sealed class CachedAttendance : IRecentAttendance
{
    /// <summary>Prefijo de la clave de la copia de cada evento.</summary>
    internal const string CacheKeyPrefix = "raffle-attendance:event:";

    /// <summary>
    /// Cuánto sobrevive una copia, que no es su vigencia. Una noche y no los treinta días de los
    /// directorios: es una lista de personas con nombre en un solo evento.
    /// </summary>
    private static readonly TimeSpan Retention = TimeSpan.FromHours(12);

    private readonly IAttendanceSource _source;
    private readonly RaffleAttendanceSettings _settings;
    private readonly RaffleAttendanceRefreshLock _refreshLock;
    private readonly StaleTolerantCache _copies;
    private readonly ILogger<CachedAttendance> _logger;

    public CachedAttendance(
        IAttendanceSource source,
        IDistributedCache cache,
        IOptions<RaffleAttendanceSettings> settings,
        RaffleAttendanceRefreshLock refreshLock,
        TimeProvider timeProvider,
        ILogger<CachedAttendance> logger)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(refreshLock);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _source = source;
        _settings = settings.Value;
        _refreshLock = refreshLock;
        _copies = new StaleTolerantCache(cache, timeProvider, logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<RecentAttendance> GetAsync(Guid eventId, CancellationToken cancellationToken) =>
        GetAsync(eventId, requiresCheckIn: true, cancellationToken);

    /// <inheritdoc />
    public async Task<RecentAttendance> GetAsync(Guid eventId, bool requiresCheckIn, CancellationToken cancellationToken)
    {
        var cacheKey = requiresCheckIn
            ? CacheKeyPrefix + eventId
            : $"{CacheKeyPrefix}{eventId}:all";

        var read = await _copies.ReadOrRefreshAsync<List<Attendee>>(
            cacheKey,
            new CacheCopyPolicy(_settings.CacheTtl, Retention, _refreshLock),
            token => ReadLiveAsync(eventId, requiresCheckIn, token),
            exception => _logger.LogWarning(
                exception,
                "No se pudo leer la lista de la puerta del evento {EventId}; se sirve la última copia disponible.",
                eventId),
            cancellationToken);

        return read.Outcome switch
        {
            CacheOutcome.Fresh => new RecentAttendance(AttendanceStatus.Ok, read.FetchedAt, read.Value),
            CacheOutcome.Stale => new RecentAttendance(AttendanceStatus.Stale, read.FetchedAt, read.Value),
            _ => new RecentAttendance(AttendanceStatus.Unavailable, null, null),
        };
    }

    private async Task<List<Attendee>?> ReadLiveAsync(Guid eventId, bool requiresCheckIn, CancellationToken cancellationToken)
    {
        var result = requiresCheckIn
            ? await _source.GetCheckedInAsync(eventId, cancellationToken)
            : await _source.GetAttendeesAsync(eventId, requiresCheckIn: false, cancellationToken);
        if (result.IsFailure)
            throw new HttpRequestException(result.Error.Message);

        return result.Value.ToList();
    }
}
