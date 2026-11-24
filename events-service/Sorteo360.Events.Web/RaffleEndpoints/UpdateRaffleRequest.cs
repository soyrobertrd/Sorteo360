namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>
/// La representación editable completa del sorteo (ADR-0009). Todo tipo de valor es anulable para
/// distinguir la ausencia del <c>false</c> o del cero; cuáles son obligatorios lo decide el
/// validador. Una autoridad omitida se borra, y las instrucciones de retiro también.
/// </summary>
public record UpdateRaffleRequest
{
    public string? VisualStyle { get; init; }
    public bool? ShowPrizeOnScreen { get; init; }
    public int? AnimationSeconds { get; init; }
    public bool? RequiresPresenceConfirmation { get; init; }
    public bool? RequiresCheckIn { get; init; }
    public bool? OnePrizePerPerson { get; init; }
    public bool? OnePrizePerCompany { get; init; }
    public bool? OnePrizePerDepartmentPerRound { get; init; }
    public string? BoardTitle { get; init; }
    public string? BoardSubtitle { get; init; }
    public string? BoardIdentifier { get; init; }
    public int? BoardBatchSize { get; init; }
    public string? NotaryName { get; init; }
    public string? NotaryIdentityDocument { get; init; }
    public string? WitnessOneName { get; init; }
    public string? WitnessOneIdentityDocument { get; init; }
    public string? WitnessTwoName { get; init; }
    public string? WitnessTwoIdentityDocument { get; init; }
    public string? PickupInstructions { get; init; }
}
