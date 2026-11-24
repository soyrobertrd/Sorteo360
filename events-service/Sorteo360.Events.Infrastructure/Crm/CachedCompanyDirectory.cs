using CSharpFunctionalExtensions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sorteo360.Events.Core.CompanyDirectory;
using Sorteo360.Events.Core.CrmDirectory;
using Sorteo360.Events.Core.Errors;
using Sorteo360.Events.Infrastructure.Directories;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Infrastructure.Crm;

/// <summary>
/// El directorio de empresas respaldado por el CRM, con una copia en caché para mostrar y consultas
/// en vivo para escribir. Es la misma jugada que <see cref="CachedOptDirectory"/> y el razonamiento
/// se traslada intacto: dos directorios sobre un mismo CRM, un solo patrón que aprender y mantener
/// (ADR-0015).
/// <para>
/// Leer, escribir, medir la vigencia y tomar el turno para renovar lo pone
/// <see cref="StaleTolerantCache"/>, y con él la regla de que una copia vencida sigue existiendo
/// para servirse si la renovación falla. Lo que queda acá es lo propio: sus claves, sus dos
/// retenciones, y cómo se traduce el resultado de una lectura a <see cref="CrmDirectoryStatus"/>.
/// </para>
/// </summary>
public sealed class CachedCompanyDirectory : ICompanyDirectory
{
    /// <summary>Clave de la copia completa del directorio.</summary>
    internal const string SnapshotCacheKey = "company-directory:companies";

    /// <summary>Prefijo de las claves de cada empresa resuelta por id.</summary>
    internal const string CompanyCacheKeyPrefix = "company-directory:company:";

    /// <summary>
    /// Cuánto sobrevive la copia completa, que no tiene nada que ver con su vigencia: es solo el
    /// techo que impide que una copia obtenida una vez ocupe memoria para siempre.
    /// </summary>
    private static readonly TimeSpan SnapshotRetention = TimeSpan.FromDays(30);

    /// <summary>
    /// El techo de una empresa resuelta por id. Es más corto que el de la copia completa a
    /// propósito: la cola de empresas cerradas es larga —nada que ver con las dieciocho oficinas de
    /// OPT—, así que cada entrada suelta tiene que poder soltarse, mientras que la copia completa es
    /// una sola y es la que sostiene una lectura con el CRM caído.
    /// </summary>
    private static readonly TimeSpan CompanyRetention = TimeSpan.FromDays(7);

    private readonly ICrmCompanyClient _client;
    private readonly CompanyDirectorySettings _settings;
    private readonly CrmDirectoryStatusContext _statusContext;
    private readonly CompanyDirectoryRefreshLock _refreshLock;
    private readonly StaleTolerantCache _copies;
    private readonly ILogger<CachedCompanyDirectory> _logger;

    public CachedCompanyDirectory(
        ICrmCompanyClient client,
        IDistributedCache cache,
        IOptions<CompanyDirectorySettings> settings,
        CrmDirectoryStatusContext statusContext,
        CompanyDirectoryRefreshLock refreshLock,
        TimeProvider timeProvider,
        ILogger<CachedCompanyDirectory> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(statusContext);
        ArgumentNullException.ThrowIfNull(refreshLock);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _client = client;
        _settings = settings.Value;
        _statusContext = statusContext;
        _refreshLock = refreshLock;
        _copies = new StaleTolerantCache(cache, timeProvider, logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CompanyLookup> ResolveAsync(
        IReadOnlyCollection<Guid> companyIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(companyIds);

        var (companies, status) = await GetSnapshotAsync(cancellationToken);

        var byId = new Dictionary<Guid, CrmCompanyDirectoryEntryResponse>();
        foreach (var company in companies)
            byId[company.Id] = company;

        var degraded = false;

        foreach (var companyId in companyIds.Distinct())
        {
            if (byId.ContainsKey(companyId))
                continue;

            var (filled, fresh) = await GetCompanyByIdAsync(companyId, cancellationToken);

            if (filled is not null)
                byId[companyId] = filled;

            if (!fresh)
                degraded = true;
        }

        // Un relleno que no se pudo hacer degrada la respuesta entera: los nombres que hay son
        // reales, pero no todos están, y el cliente merece enterarse por la cabecera.
        if (degraded && status is CrmDirectoryStatus.Ok)
            status = CrmDirectoryStatus.Stale;

        _statusContext.Report(status);

        return new CompanyLookup(status, BuildSnapshots(companyIds, byId));
    }

    /// <inheritdoc />
    public async Task<Result<Maybe<CompanyInfo>, BaseError>> GetCompanyAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        try
        {
            var company = await _client.GetCompanyAsync(companyId, cancellationToken);

            // No se informa estado, y acá es donde esto se aparta de CachedOptDirectory a
            // propósito: la cabecera dice qué tan vigentes son los nombres que *esta respuesta*
            // sirve, y una escritura de proveedor no sirve ninguno —crear devuelve un id pelado y
            // editar un 204—. En eventos sí lo informa porque su escritura devuelve el detalle con
            // los nombres ya resueltos. Decir "ok" acá sería hablar de una copia que esta respuesta
            // no tocó.
            return company is null
                ? Result.Success<Maybe<CompanyInfo>, BaseError>(Maybe<CompanyInfo>.None)
                : Result.Success<Maybe<CompanyInfo>, BaseError>(
                    Maybe.From(new CompanyInfo(company.Id, company.IsActive, company.IsDeleted)));
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                exception,
                "No se pudo consultar la empresa {CompanyId} en el CRM; se rechaza la escritura.",
                companyId);

            // Tampoco acá, y por partida doble: esta respuesta es un 503 que no sirve ningún
            // nombre, y decir "unavailable" haría creer que las lecturas están sin nombres cuando
            // la copia puede estar perfecta.
            return Result.Failure<Maybe<CompanyInfo>, BaseError>(CompanyErrors.CrmUnavailable());
        }
    }

    /// <summary>
    /// Proyecta las empresas pedidas. Lo que no se pudo resolver simplemente no entra: quien arma la
    /// respuesta emite <c>null</c>, que es un dato honesto, en lugar de un texto de relleno
    /// indistinguible de una empresa que de verdad se llamara así.
    /// </summary>
    private static Dictionary<Guid, CompanySnapshot> BuildSnapshots(
        IReadOnlyCollection<Guid> companyIds,
        Dictionary<Guid, CrmCompanyDirectoryEntryResponse> byId)
    {
        var snapshots = new Dictionary<Guid, CompanySnapshot>();

        foreach (var companyId in companyIds)
        {
            if (byId.TryGetValue(companyId, out var company))
                snapshots[companyId] = ToSnapshot(company);
        }

        return snapshots;
    }

    /// <summary>
    /// Devuelve la copia completa del directorio, renovándola si está vencida. Una renovación
    /// fallida no es un error: se sirve la copia anterior por vieja que sea, y solo se informa
    /// <see cref="CrmDirectoryStatus.Unavailable"/> cuando nunca hubo ninguna.
    /// </summary>
    private async Task<(List<CrmCompanyDirectoryEntryResponse> Companies, CrmDirectoryStatus Status)> GetSnapshotAsync(
        CancellationToken cancellationToken)
    {
        var read = await _copies.ReadOrRefreshAsync<List<CrmCompanyDirectoryEntryResponse>>(
            SnapshotCacheKey,
            new CacheCopyPolicy(_settings.SnapshotTtl, SnapshotRetention, _refreshLock),
            async token => await _client.ListCompaniesAsync(token),
            exception => _logger.LogWarning(
                exception,
                "No se pudo renovar el directorio de empresas desde el CRM; se sirve la última copia disponible."),
            cancellationToken);

        return (read.Value ?? [], read.Outcome.ToCrmDirectoryStatus());
    }

    /// <summary>
    /// Resuelve una empresa que la copia completa no trae y la guarda aparte. Es la cola larga del
    /// directorio —empresas cerradas— y no un síntoma de que el CRM esté mal. El segundo valor dice
    /// si el dato salió de una consulta o de una copia vencida.
    /// <para>
    /// No toma cerrojo: lo que se serializa es la renovación de la copia completa, que veinticinco
    /// lecturas simultáneas dispararían a la vez, y no el relleno de una empresa suelta. Tampoco
    /// guarda la ausencia: una empresa que hoy no está puede aparecer mañana.
    /// </para>
    /// </summary>
    private async Task<(CrmCompanyDirectoryEntryResponse? Company, bool Fresh)> GetCompanyByIdAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var read = await _copies.ReadOrRefreshAsync<CrmCompanyDirectoryEntryResponse>(
            CompanyCacheKeyPrefix + companyId,
            new CacheCopyPolicy(_settings.SnapshotTtl, CompanyRetention),
            async token => ToDirectoryEntry(await _client.GetCompanyAsync(companyId, token)),
            exception => _logger.LogWarning(
                exception,
                "No se pudo resolver la empresa {CompanyId} en el CRM; se sirve lo que haya en caché.",
                companyId),
            cancellationToken);

        return (read.Value, read.Outcome is CacheOutcome.Fresh);
    }

    /// <summary>
    /// La ficha entera reducida a lo que la copia sirve.
    /// <para>
    /// La lectura por id trae los dos ejes por separado; acá se funden en el mismo bit que emite el
    /// volcado: una empresa dormida y una dada de baja son igual de "no elegible" para quien dibuja
    /// una lista.
    /// </para>
    /// </summary>
    private static CrmCompanyDirectoryEntryResponse? ToDirectoryEntry(CrmCompanyResponse? company) =>
        company is null
            ? null
            : new CrmCompanyDirectoryEntryResponse(
                company.Id,
                company.Name,
                company.Country,
                company.City,
                company.IsActive && !company.IsDeleted);

    /// <summary>Traduce la empresa que llegó por la red al tipo que conoce el dominio.</summary>
    private static CompanySnapshot ToSnapshot(CrmCompanyDirectoryEntryResponse company) =>
        new(company.Id, company.Name, company.Country, company.City, company.IsActive);
}
