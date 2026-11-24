using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sorteo360.Convocatorias.Core.CheckIns;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Platform.Database;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Infrastructure.Database;

/// <inheritdoc cref="ICheckInListRepository" />
public sealed class CheckInListRepository : ICheckInListRepository
{
    private readonly ConvocatoriasDbContext _context;
    private readonly ILogger<CheckInListRepository> _logger;

    public CheckInListRepository(ConvocatoriasDbContext context, ILogger<CheckInListRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<CheckInListRow>, BaseError>> ReadAsync(
        Guid eventId,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync<IReadOnlyList<CheckInListRow>>(_logger, "leer la lista de check-ins del evento", async () =>
        {
            // Las mismas campañas y la misma clave que la unión de participantes: las lanzadas del
            // evento, y la dirección normalizada por EmailAddressShape en memoria, no en SQL.
            var recipientEmails = await _context.Campaigns
                .AsNoTracking()
                .Where(campaign => campaign.EventId == eventId)
                .Where(LaunchedCampaign.Is)
                .SelectMany(campaign => campaign.Recipients)
                .Select(recipient => recipient.Email)
                .ToListAsync(cancellationToken);

            var recipients = recipientEmails.Select(EmailAddressShape.Normalize).ToHashSet();

            // La sirve ix_registration_event_checked_in, parcial sobre quien tiene check-in.
            var checkedIn = await _context.Registrations
                .AsNoTracking()
                .Where(registration => registration.EventId == eventId && registration.CheckedInAt != null)
                .OrderBy(registration => registration.CheckedInAt)
                .ThenBy(registration => registration.Id)
                .Select(registration => new
                {
                    registration.Id,
                    registration.EmailNormalized,
                    registration.FirstName,
                    registration.LastName,
                    registration.Company,
                    registration.CrmContactId,
                    CameThroughLink = registration.CampaignRecipientId != null,
                    CheckedInAt = registration.CheckedInAt!.Value,
                })
                .ToListAsync(cancellationToken);

            return [.. checkedIn.Select(row => new CheckInListRow(
                row.Id,
                row.EmailNormalized,
                row.FirstName,
                row.LastName,
                row.Company,
                row.CrmContactId,
                IsRecipient: recipients.Contains(row.EmailNormalized),
                row.CameThroughLink,
                row.CheckedInAt))];
        });
}
