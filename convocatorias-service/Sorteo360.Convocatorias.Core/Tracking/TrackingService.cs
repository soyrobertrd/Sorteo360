using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.DTOs;
using Sorteo360.Convocatorias.Core.Participants;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Tracking;

/// <inheritdoc />
/// <remarks>
/// Compone lecturas que ya existen en vez de escribir otras: la unión de participantes, las
/// definiciones de los cubos de reconciliación y la ventana de inscripción. Un número que se
/// calculara dos veces terminaría dando dos resultados.
/// </remarks>
public sealed class TrackingService : ITrackingService
{
    /// <summary>El tope de página del listado de campañas; se recorren todas las páginas.</summary>
    private const int CampaignPageSize = 100;

    private readonly IParticipantService _participants;
    private readonly ICampaignRepository _campaigns;
    private readonly IReconciliationRepository _reconciliation;
    private readonly IRegistrationWindowRepository _windows;

    public TrackingService(
        IParticipantService participants,
        ICampaignRepository campaigns,
        IReconciliationRepository reconciliation,
        IRegistrationWindowRepository windows)
    {
        ArgumentNullException.ThrowIfNull(participants);
        ArgumentNullException.ThrowIfNull(campaigns);
        ArgumentNullException.ThrowIfNull(reconciliation);
        ArgumentNullException.ThrowIfNull(windows);

        _participants = participants;
        _campaigns = campaigns;
        _reconciliation = reconciliation;
        _windows = windows;
    }

    /// <inheritdoc />
    public async Task<Result<EventTracking, BaseError>> GetAsync(Guid eventId, CancellationToken cancellationToken)
    {
        // Sin campaignId la unión ya deja fuera a los destinatarios de las campañas sin lanzar (§2).
        var participants = await _participants.ListAsync(eventId, ParticipantFilter.None, cancellationToken);
        if (participants.IsFailure)
            return participants.Error;

        var campaigns = await ListLaunchedCampaignsAsync(eventId, cancellationToken);
        if (campaigns.IsFailure)
            return campaigns.Error;

        var needsAttention = await _reconciliation.CountNeedsAttentionAsync(eventId, cancellationToken);
        if (needsAttention.IsFailure)
            return needsAttention.Error;

        var window = await _windows.GetAsync(eventId, cancellationToken);
        if (window.IsFailure)
            return window.Error;

        return new EventTracking(
            campaigns.Value,
            TrackingRollup.Count(participants.Value),
            needsAttention.Value,
            window.Value);
    }

    /// <summary>
    /// Las campañas lanzadas del evento, con la misma definición que la unión de participantes:
    /// una cancelada desde borrador no aparece, igual que un borrador. Un evento tiene un puñado,
    /// así que se recorren las páginas en vez de cortar en silencio en la primera.
    /// </summary>
    private async Task<Result<IReadOnlyList<CampaignSummary>, BaseError>> ListLaunchedCampaignsAsync(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var summaries = new List<CampaignSummary>();

        for (var page = 1; ; page++)
        {
            var result = await _campaigns.ListAsync(
                new CampaignQuery(page, CampaignPageSize, eventId, LaunchedOnly: true), cancellationToken);

            if (result.IsFailure)
                return result.Error;

            summaries.AddRange(result.Value.Items);

            if (page * CampaignPageSize >= result.Value.Total)
                return summaries;
        }
    }
}
