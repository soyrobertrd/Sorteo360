namespace Sorteo360.Events.Infrastructure.Database;

/// <summary>
/// Entidad de persistencia para la tabla <c>events.raffle</c> (el sorteo de un evento).
/// </summary>
internal sealed class RaffleData
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string State { get; set; } = string.Empty;
    public string VisualStyle { get; set; } = string.Empty;
    public bool ShowPrizeOnScreen { get; set; }
    public int AnimationSeconds { get; set; }
    public bool RequiresPresenceConfirmation { get; set; }
    public bool RequiresCheckIn { get; set; } = true;
    public bool OnePrizePerPerson { get; set; }
    public bool OnePrizePerCompany { get; set; }
    public Guid? LinkedEventId { get; set; }
    public string? BoardTitle { get; set; }
    public string? BoardSubtitle { get; set; }
    public string? BoardIdentifier { get; set; }
    public int? BoardBatchSize { get; set; }
    public string? NotaryName { get; set; }
    public string? NotaryIdentityDocument { get; set; }
    public string? WitnessOneName { get; set; }
    public string? WitnessOneIdentityDocument { get; set; }
    public string? WitnessTwoName { get; set; }
    public string? WitnessTwoIdentityDocument { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid CreatedById { get; set; }
    public Guid UpdatedById { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public Guid? StartedById { get; set; }
    public Guid? AwardsBatchId { get; set; }
    public string? PickupInstructions { get; set; }
    public string? CertificateSignerOneName { get; set; }
    public string? CertificateSignerOneTitle { get; set; }
    public string? CertificateSignerTwoName { get; set; }
    public string? CertificateSignerTwoTitle { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public Guid? CompletedById { get; set; }
    public string DisplayNameMode { get; set; } = "full_name";
    public string DisplayIdentifierMode { get; set; } = "ticket_number";
    public string DisplayDetailMode { get; set; } = "department";
}
