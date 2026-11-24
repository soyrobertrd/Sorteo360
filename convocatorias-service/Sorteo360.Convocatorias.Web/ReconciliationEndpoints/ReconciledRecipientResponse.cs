namespace Sorteo360.Convocatorias.Web.ReconciliationEndpoints;

/// <summary>
/// Un destinatario después de reintentar el empujón de su baja al CRM (spec <c>07</c> §6.2). Los
/// dos estados salen de la pareja: con sello está sincronizada, con clic y sin sello, pendiente.
/// </summary>
public sealed record ReconciledRecipientResponse
{
    public Guid Id { get; init; }

    public Guid CampaignId { get; init; }

    /// <summary>El contacto del CRM al que se le empujó la baja.</summary>
    public Guid ContactId { get; init; }

    public DateTimeOffset? UnsubscribedAt { get; init; }

    public DateTimeOffset? UnsubscribeSyncedAt { get; init; }
}
