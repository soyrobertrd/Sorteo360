using CSharpFunctionalExtensions;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Accounts;

/// <summary>
/// Evalúa los topes del plan vigente de la cuenta antes de crear sorteos, rondas, premios,
/// importar participantes o usar funciones de pago (§9.4).
/// </summary>
public interface IPlanLimitGuard
{
    Task<UnitResult<BaseError>> EnsureCanCreateRaffleAsync(Guid accountId, CancellationToken cancellationToken);
    Task<UnitResult<BaseError>> RecordRaffleCreatedAsync(Guid accountId, CancellationToken cancellationToken);
    Task<UnitResult<BaseError>> EnsureCanAddRoundAsync(Guid eventId, int currentRounds, CancellationToken cancellationToken, int addingCount = 1);
    Task<UnitResult<BaseError>> EnsureCanAddPrizesAsync(Guid eventId, int currentPrizes, int addingCount, CancellationToken cancellationToken);
    Task<Result<PlanParticipantLimitCheck, BaseError>> CheckParticipantLimitAsync(
        Guid eventId, int resultingTotal, int currentCount, CancellationToken cancellationToken);
    Task<UnitResult<BaseError>> EnsureFeatureEnabledAsync(Guid accountId, string featureKey, CancellationToken cancellationToken);
}

public sealed record PlanParticipantLimitCheck(bool Allowed, int? Limit, int? Remaining);

