using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.Participants;
using Sorteo360.Platform.Database;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Infrastructure.Database;

/// <inheritdoc cref="IParticipantRepository" />
public sealed class ParticipantRepository : IParticipantRepository
{
    private readonly ConvocatoriasDbContext _context;
    private readonly ILogger<ParticipantRepository> _logger;

    public ParticipantRepository(ConvocatoriasDbContext context, ILogger<ParticipantRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<ParticipantSources, BaseError>> ReadSourcesAsync(
        Guid eventId,
        Guid? campaignId,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "leer los participantes del evento", async () =>
        {
            var campaigns = _context.Campaigns
                .AsNoTracking()
                .Where(c => c.EventId == eventId);

            // Una campaña sin lanzar (un borrador, o una cancelada desde borrador) tiene filas de
            // gente a la que nadie decidió escribirle, así que solo entra cuando alguien pide esa
            // campaña por su id, que es la lista que se revisa antes de lanzarla (spec 07 §2).
            campaigns = campaignId is { } id
                ? campaigns.Where(c => c.Id == id)
                : campaigns.Where(LaunchedCampaign.Is);

            return new ParticipantSources(
                await ReadRecipientsAsync(campaigns, cancellationToken),
                await ReadRegistrationsAsync(eventId, cancellationToken));
        });

    /// <inheritdoc />
    public Task<Result<Maybe<ParticipantSources>, BaseError>> ReadCampaignSourcesAsync(
        Guid campaignId,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "leer los destinatarios de la campaña", async () =>
        {
            var campaigns = _context.Campaigns
                .AsNoTracking()
                .Where(c => c.Id == campaignId);

            var found = await campaigns
                .Select(c => new { c.EventId })
                .SingleOrDefaultAsync(cancellationToken);

            if (found is null)
                return Maybe<ParticipantSources>.None;

            // Sin evento no hay portal en el que inscribirse, así que no hay inscripciones que unir.
            IReadOnlyList<RegistrationSource> registrations = found.EventId is { } eventId
                ? await ReadRegistrationsAsync(eventId, cancellationToken)
                : [];

            return Maybe.From(new ParticipantSources(
                await ReadRecipientsAsync(campaigns, cancellationToken),
                registrations));
        });

    /// <summary>Las filas de <c>campaign_recipient</c> de esas campañas, con su copia del nombre.</summary>
    private static async Task<IReadOnlyList<RecipientSource>> ReadRecipientsAsync(
        IQueryable<CampaignData> campaigns,
        CancellationToken cancellationToken) =>
        await campaigns
            .SelectMany(c => c.Recipients)
            .Select(r => new RecipientSource(
                r.CampaignId, r.FirstName, r.LastName, r.Email, r.SendStatus, r.CreatedAt, r.NotificationId))
            .ToListAsync(cancellationToken);

    /// <summary>Todas las inscripciones del evento.</summary>
    private async Task<IReadOnlyList<RegistrationSource>> ReadRegistrationsAsync(
        Guid eventId,
        CancellationToken cancellationToken) =>
        // Pendiente de sincronizar es la pareja de columnas y nada más (spec 06 §4.3): el
        // fallo sellado y ningún contacto todavía.
        await _context.Registrations
            .AsNoTracking()
            .Where(r => r.EventId == eventId)
            .Select(r => new RegistrationSource(
                r.FirstName,
                r.LastName,
                r.Email,
                r.Company,
                r.Reference,
                r.CreatedAt,
                r.CampaignRecipient == null
                    ? null
                    : r.CampaignRecipient.FirstName + " " + r.CampaignRecipient.LastName,
                r.CrmContactId == null && r.CrmSyncFailedAt != null,
                r.ContactDetailsDiffer,
                r.CheckedInAt))
            .ToListAsync(cancellationToken);
}
