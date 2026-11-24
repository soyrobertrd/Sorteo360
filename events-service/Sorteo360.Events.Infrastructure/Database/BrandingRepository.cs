using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sorteo360.Events.Core.Branding;
using Sorteo360.Events.Core.Database;
using Sorteo360.Events.Core.Errors;
using Sorteo360.Platform.Database;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Infrastructure.Database;

/// <summary>
/// Repositorio EF Core para la configuración de marca global y por evento (<c>events.branding_setting</c>).
/// </summary>
public sealed class BrandingRepository : IBrandingRepository
{
    private const string GlobalScopeKey = "global";

    private readonly EventsDbContext _context;
    private readonly TimeProvider _clock;
    private readonly ILogger<BrandingRepository> _logger;

    public BrandingRepository(
        EventsDbContext context,
        TimeProvider clock,
        ILogger<BrandingRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        _context = context;
        _clock = clock;
        _logger = logger;
    }

    public Task<Result<GlobalBrandingSettings, BaseError>> GetGlobalAsync(CancellationToken cancellationToken)
    {
        return DbResult.TryAsync(_logger, "leer la configuración global de marca", async () =>
        {
            // R7: BrandingSettings ya está filtrado por AccountId == CurrentAccountId.
            var row = await _context.BrandingSettings
                .AsNoTracking()
                .Where(b => b.EventId == null)
                .OrderByDescending(b => b.UpdatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            return row is null ? GlobalBrandingSettings.Defaults(_clock.GetUtcNow()) : MapToGlobal(row);
        });
    }

    public Task<Result<GlobalBrandingSettings, BaseError>> UpsertGlobalAsync(
        GlobalBrandingSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return DbResult.TryAsync(_logger, "guardar la configuración global de marca", async () =>
        {
            var accountId = _context.CurrentAccountId;
            var row = await _context.BrandingSettings
                .FirstOrDefaultAsync(b => b.EventId == null, cancellationToken);

            if (row is null)
            {
                row = new BrandingSettingData
                {
                    ScopeKey = AccountScopeKey(accountId),
                    AccountId = accountId,
                };
                _context.BrandingSettings.Add(row);
            }

            row.AccountId = accountId;
            row.EventId = null;
            row.AppName = settings.AppName;
            row.AppSubtitle = settings.AppSubtitle;
            row.OrganizationName = settings.OrganizationName;
            row.LogoUrl = settings.LogoUrl;
            row.LogoDarkUrl = settings.LogoDarkUrl;
            row.PrimaryColor = settings.PrimaryColor;
            row.AccentColor = settings.AccentColor;
            row.UpdatedAt = settings.UpdatedAt;

            await _context.SaveChangesAsync(cancellationToken);
            return MapToGlobal(row);
        });
    }

    public Task<Result<IReadOnlyDictionary<Guid, EventBrandingSettings>, BaseError>> ListEventBrandingsAsync(
        CancellationToken cancellationToken)
    {
        return DbResult.TryAsync<IReadOnlyDictionary<Guid, EventBrandingSettings>>(
            _logger,
            "listar las marcas personalizadas por evento",
            async () =>
            {
                var rows = await _context.BrandingSettings
                    .AsNoTracking()
                    .Where(b => b.EventId != null)
                    .ToListAsync(cancellationToken);

                return rows
                    .Where(b => b.EventId.HasValue)
                    .ToDictionary(
                        b => b.EventId!.Value,
                        b => new EventBrandingSettings(b.EventId!.Value, b.OrganizationName, b.LogoUrl, b.UpdatedAt));
            });
    }

    public Task<Result<EventBrandingSettings?, BaseError>> GetEventBrandingAsync(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        return DbResult.TryAsync(_logger, "leer la marca personalizada del evento", async () =>
        {
            var scopeKey = EventScopeKey(eventId);
            var row = await _context.BrandingSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.ScopeKey == scopeKey, cancellationToken);

            return row is null
                ? null
                : new EventBrandingSettings(eventId, row.OrganizationName, row.LogoUrl, row.UpdatedAt);
        });
    }

    public async Task<UnitResult<BaseError>> UpsertEventBrandingAsync(
        EventBrandingSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var result = await DbResult.TryAsync(_logger, "guardar la marca personalizada del evento", async () =>
        {
            // La clave es "event:{id}" para todas las cuentas: sin mirar que el evento sea de esta
            // (el filtro de Events), otra cuenta podía ocuparla primero y dejar al dueño con un 409.
            if (!await _context.Events.AsNoTracking().AnyAsync(e => e.Id == settings.EventId, cancellationToken))
                return false;

            var scopeKey = EventScopeKey(settings.EventId);
            var row = await _context.BrandingSettings
                .FirstOrDefaultAsync(b => b.ScopeKey == scopeKey, cancellationToken);

            if (string.IsNullOrWhiteSpace(settings.CompanyName) && string.IsNullOrWhiteSpace(settings.LogoUrl))
            {
                if (row is not null)
                {
                    _context.BrandingSettings.Remove(row);
                    await _context.SaveChangesAsync(cancellationToken);
                }
                return true;
            }

            if (row is null)
            {
                row = new BrandingSettingData
                {
                    ScopeKey = scopeKey,
                    AccountId = _context.CurrentAccountId,
                    EventId = settings.EventId,
                };
                _context.BrandingSettings.Add(row);
            }

            row.AccountId = _context.CurrentAccountId;
            row.OrganizationName = string.IsNullOrWhiteSpace(settings.CompanyName) ? null : settings.CompanyName.Trim();
            row.LogoUrl = string.IsNullOrWhiteSpace(settings.LogoUrl) ? null : settings.LogoUrl.Trim();
            row.UpdatedAt = settings.UpdatedAt;

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        });

        if (result.IsFailure)
            return UnitResult.Failure(result.Error);

        return result.Value ? UnitResult.Success<BaseError>() : UnitResult.Failure(EventImageErrors.EventNotFound());
    }

    public Task<Result<(GlobalBrandingSettings Global, EventBrandingSettings? EventBranding), BaseError>> GetPublicForEventAsync(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        return DbResult.TryAsync(_logger, "leer la marca pública de un evento", async () =>
        {
            var accountId = await _context.Events
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(e => e.Id == eventId && e.DeletedAt == null)
                .Select(e => (Guid?)e.AccountId)
                .FirstOrDefaultAsync(cancellationToken);

            if (accountId is null)
                return (GlobalBrandingSettings.Defaults(_clock.GetUtcNow()), (EventBrandingSettings?)null);

            var rows = await _context.BrandingSettings
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(b => b.AccountId == accountId.Value && (b.EventId == null || b.EventId == eventId))
                .ToListAsync(cancellationToken);

            var accountRow = rows
                .Where(b => b.EventId == null)
                .OrderByDescending(b => b.UpdatedAt)
                .FirstOrDefault();
            var eventRow = rows.FirstOrDefault(b => b.EventId == eventId);

            var global = accountRow is null
                ? GlobalBrandingSettings.Defaults(_clock.GetUtcNow())
                : MapToGlobal(accountRow);
            var eventBranding = eventRow is null
                ? null
                : new EventBrandingSettings(eventId, eventRow.OrganizationName, eventRow.LogoUrl, eventRow.UpdatedAt);

            return (global, eventBranding);
        });
    }

    private static string AccountScopeKey(Guid accountId) => $"account:{accountId:D}";

    private static string EventScopeKey(Guid eventId) => $"event:{eventId:D}";

    private static GlobalBrandingSettings MapToGlobal(BrandingSettingData row) => new(
        AppName: GlobalBrandingSettings.NormalizeText(row.AppName, GlobalBrandingSettings.DefaultAppName, 120),
        AppSubtitle: GlobalBrandingSettings.NormalizeText(row.AppSubtitle, GlobalBrandingSettings.DefaultAppSubtitle, 200),
        OrganizationName: GlobalBrandingSettings.NormalizeText(row.OrganizationName, GlobalBrandingSettings.DefaultOrganizationName, 200),
        LogoUrl: GlobalBrandingSettings.NormalizeText(row.LogoUrl, GlobalBrandingSettings.DefaultLogoUrl, 2000),
        LogoDarkUrl: GlobalBrandingSettings.NormalizeText(row.LogoDarkUrl, GlobalBrandingSettings.DefaultLogoDarkUrl, 2000),
        PrimaryColor: GlobalBrandingSettings.NormalizeHexColor(row.PrimaryColor, GlobalBrandingSettings.DefaultPrimaryColor),
        AccentColor: GlobalBrandingSettings.NormalizeHexColor(row.AccentColor, GlobalBrandingSettings.DefaultAccentColor),
        UpdatedAt: row.UpdatedAt);
}

