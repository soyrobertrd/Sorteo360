using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Platform.Database;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Infrastructure.Database;

/// <inheritdoc cref="IContactCampaignRecipientRepository" />
public sealed class ContactCampaignRecipientRepository : IContactCampaignRecipientRepository
{
    private readonly ConvocatoriasDbContext _context;
    private readonly ILogger<ContactCampaignRecipientRepository> _logger;

    public ContactCampaignRecipientRepository(
        ConvocatoriasDbContext context,
        ILogger<ContactCampaignRecipientRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<ContactCampaignRecipient>, BaseError>> ListByContactsAsync(
        IReadOnlyCollection<Guid> contactIds,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "leer las campañas de los contactos", async () =>
        {
            var ids = contactIds.Distinct().ToList();

            // Solo lo lanzado, con la misma definición que la lista de participantes (spec 07 §2):
            // las filas de un borrador son gente a la que nadie decidió escribirle todavía.
            var rows = await _context.Campaigns
                .AsNoTracking()
                .Where(LaunchedCampaign.Is)
                .SelectMany(
                    c => c.Recipients.Where(r => ids.Contains(r.ContactId)),
                    (c, r) => new
                    {
                        r.ContactId,
                        CampaignId = c.Id,
                        c.EventId,
                        c.Subject,
                        LaunchedAt = c.LaunchedAt!.Value,
                        r.SendStatus,
                        r.Email,
                        r.NotificationId,
                    })
                .ToListAsync(cancellationToken);

            var registered = await ReadRegistrationsAsync(
                [.. rows.Where(r => r.EventId is not null).Select(r => r.EventId!.Value).Distinct()],
                [.. rows.Select(r => EmailAddressShape.Normalize(r.Email)).Distinct()],
                cancellationToken);

            IReadOnlyList<ContactCampaignRecipient> recipients =
            [
                .. rows
                    .OrderByDescending(r => r.LaunchedAt)
                    .ThenBy(r => r.CampaignId)
                    .ThenBy(r => r.ContactId)
                    .Select(r => new ContactCampaignRecipient(
                        r.ContactId,
                        r.CampaignId,
                        r.EventId,
                        r.Subject,
                        r.LaunchedAt,
                        r.SendStatus,
                        r.EventId is { } eventId
                            ? registered.Contains((eventId, EmailAddressShape.Normalize(r.Email)))
                            : null,
                        r.NotificationId)),
            ];

            return recipients;
        });

    /// <summary>
    /// Las parejas evento y dirección normalizada que tienen inscripción. La unión es por la dirección
    /// y nada más (spec 07 §3.1), así que inscrito dice lo mismo acá que en la lista del evento.
    /// </summary>
    private async Task<HashSet<(Guid EventId, string Email)>> ReadRegistrationsAsync(
        List<Guid> eventIds,
        List<string> emails,
        CancellationToken cancellationToken)
    {
        if (eventIds.Count == 0)
            return [];

        var pairs = await _context.Registrations
            .AsNoTracking()
            .Where(r => eventIds.Contains(r.EventId) && emails.Contains(r.EmailNormalized))
            .Select(r => new { r.EventId, r.EmailNormalized })
            .ToListAsync(cancellationToken);

        return [.. pairs.Select(p => (p.EventId, p.EmailNormalized))];
    }
}
