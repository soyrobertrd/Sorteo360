using Microsoft.EntityFrameworkCore;
using Sorteo360.Events.Core.Accounts;

namespace Sorteo360.Events.Infrastructure.Database;

public sealed class DatabaseAccountAccessChecker : IAccountAccessChecker
{
    private readonly EventsDbContext _context;

    public DatabaseAccountAccessChecker(EventsDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task<bool> IsActiveAccountAsync(Guid accountId, CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty)
            return false;

        return await _context.Accounts
            .AsNoTracking()
            .AnyAsync(a => a.Id == accountId && a.Status == AccountStatus.Active, cancellationToken);
    }
}

