using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sorteo360.Events.Core.Database;
using Sorteo360.Events.Core.FeatureSwitches;
using Sorteo360.Platform.Database;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Infrastructure.Database;

/// <summary>
/// Implementación EF Core de <see cref="IFeatureSwitchRepository"/> sobre <c>events.feature_switch</c>.
/// </summary>
public sealed class FeatureSwitchRepository : IFeatureSwitchRepository
{
    private readonly EventsDbContext _context;
    private readonly ILogger<FeatureSwitchRepository> _logger;

    public FeatureSwitchRepository(
        EventsDbContext context,
        ILogger<FeatureSwitchRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        _context = context;
        _logger = logger;
    }

    public Task<Result<IReadOnlyList<FeatureSwitchSetting>, BaseError>> ListAsync(CancellationToken cancellationToken)
    {
        return DbResult.TryAsync<IReadOnlyList<FeatureSwitchSetting>>(
            _logger,
            "listar los interruptores de funciones",
            async () =>
            {
                var rows = await _context.FeatureSwitches
                    .AsNoTracking()
                    .OrderBy(x => x.Key)
                    .ToListAsync(cancellationToken);

                return rows
                    .Select(MapToDomain)
                    .ToList();
            });
    }

    public Task<Result<FeatureSwitchSetting, BaseError>> UpsertAsync(
        FeatureSwitchSetting setting,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(setting);

        return DbResult.TryAsync(
            _logger,
            "guardar el interruptor de función",
            async () =>
            {
                var row = await _context.FeatureSwitches
                    .FirstOrDefaultAsync(x => x.Key == setting.Key, cancellationToken);

                if (row is null)
                {
                    row = new FeatureSwitchData { Key = setting.Key };
                    _context.FeatureSwitches.Add(row);
                }

                row.Enabled = setting.Enabled;
                row.ChangedById = setting.ChangedById;
                row.ChangedByName = setting.ChangedByName;
                row.ChangedAt = setting.ChangedAt;

                await _context.SaveChangesAsync(cancellationToken);
                return MapToDomain(row);
            });
    }

    private static FeatureSwitchSetting MapToDomain(FeatureSwitchData data) =>
        new(data.Key, data.Enabled, data.ChangedById, data.ChangedByName, data.ChangedAt);
}

