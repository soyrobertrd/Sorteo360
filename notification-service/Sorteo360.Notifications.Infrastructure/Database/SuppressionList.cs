using Microsoft.EntityFrameworkCore;
using Sorteo360.Notifications.Core.Sending;

namespace Sorteo360.Notifications.Infrastructure.Database;

/// <inheritdoc cref="ISuppressionList" />
public sealed class SuppressionList : ISuppressionList
{
    private readonly NotificationsDbContext _context;

    public SuppressionList(NotificationsDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    /// <inheritdoc />
    public async Task<string?> ReasonForAsync(string email, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        // Se compara en minúsculas por las dos puntas: la tabla las guarda así y una supresión que
        // se pudiera esquivar cambiando una letra no sería una supresión.
        var normalized = email.Trim().ToLowerInvariant();

        return await _context.NotificationSuppressions
            .AsNoTracking()
            .Where(suppression => suppression.Email == normalized)
            .Select(suppression => suppression.Reason)
            .SingleOrDefaultAsync(cancellationToken);
    }
}
