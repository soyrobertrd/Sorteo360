using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sorteo360.Events.Core.Accounts;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Platform.Database;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Infrastructure.Database;

/// <summary>
/// Hace cumplir los topes y funciones del plan de cada cuenta sobre PostgreSQL (§9.4).
/// </summary>
public sealed class PlanLimitGuard : IPlanLimitGuard
{
    private readonly EventsDbContext _context;
    private readonly TimeProvider _clock;
    private readonly ILogger<PlanLimitGuard> _logger;

    public PlanLimitGuard(EventsDbContext context, TimeProvider clock, ILogger<PlanLimitGuard> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        _context = context;
        _clock = clock;
        _logger = logger;
    }

    public async Task<UnitResult<BaseError>> EnsureCanCreateRaffleAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var accountResult = await LoadAccountAsync(accountId, cancellationToken);
        if (accountResult.IsFailure)
            return UnitResult.Failure(accountResult.Error);

        var account = accountResult.Value;
        if (account.Status != AccountStatus.Active)
            return UnitResult.Failure(AccountErrors.AccountNotActive());

        var plan = PlanCatalog.GetOrFree(account.Plan);
        var now = _clock.GetUtcNow();

        if (plan.MaxMonthlyRaffles is { } monthlyCap)
        {
            var yearMonth = PlanCatalog.CurrentYearMonth(now);
            var used = await _context.AccountMonthlyUsages
                .AsNoTracking()
                .Where(u => u.AccountId == account.Id && u.YearMonth == yearMonth)
                .Select(u => u.RafflesCreated)
                .FirstOrDefaultAsync(cancellationToken);

            if (used >= monthlyCap)
                return UnitResult.Failure(AccountErrors.PlanMonthlyRaffleLimit(monthlyCap, PlanCatalog.NextMonthlyResetUtc(now)));
        }

        if (plan.MaxActiveRaffles is { } activeCap)
        {
            var activeCount = await (
                from r in _context.Raffles.AsNoTracking()
                join e in _context.Events.IgnoreQueryFilters().AsNoTracking() on r.EventId equals e.Id
                where e.DeletedAt == null && e.AccountId == account.Id && r.State != RaffleState.Completed
                select r.Id
            ).CountAsync(cancellationToken);

            if (activeCount >= activeCap)
                return UnitResult.Failure(AccountErrors.PlanActiveRaffleLimit(activeCap));
        }

        return UnitResult.Success<BaseError>();
    }

    public async Task<UnitResult<BaseError>> RecordRaffleCreatedAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var resolvedId = accountId == Guid.Empty ? Account.DefaultBanreservasAccountId : accountId;
        var now = _clock.GetUtcNow();
        var yearMonth = PlanCatalog.CurrentYearMonth(now);

        var result = await DbResult.TryAsync(_logger, "registrar sorteo creado en el mes", async () =>
        {
            var row = await _context.AccountMonthlyUsages
                .FirstOrDefaultAsync(u => u.AccountId == resolvedId && u.YearMonth == yearMonth, cancellationToken);

            if (row is null)
            {
                _context.AccountMonthlyUsages.Add(new AccountMonthlyUsageData
                {
                    AccountId = resolvedId,
                    YearMonth = yearMonth,
                    RafflesCreated = 1,
                    UpdatedAt = now,
                });
            }
            else
            {
                row.RafflesCreated += 1;
                row.UpdatedAt = now;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        });

        return result.IsSuccess ? UnitResult.Success<BaseError>() : UnitResult.Failure(result.Error);
    }

    public async Task<UnitResult<BaseError>> EnsureCanAddRoundAsync(
        Guid eventId, int currentRounds, CancellationToken cancellationToken, int addingCount = 1)
    {
        if (addingCount <= 0)
            return UnitResult.Success<BaseError>();

        var accountResult = await LoadAccountByEventAsync(eventId, cancellationToken);
        if (accountResult.IsFailure)
            return UnitResult.Failure(accountResult.Error);

        var plan = PlanCatalog.GetOrFree(accountResult.Value.Plan);
        if (plan.MaxRoundsPerRaffle is { } maxRounds && currentRounds + addingCount > maxRounds)
            return UnitResult.Failure(AccountErrors.PlanRoundLimit(maxRounds, Math.Max(0, maxRounds - currentRounds)));

        return UnitResult.Success<BaseError>();
    }

    public async Task<UnitResult<BaseError>> EnsureCanAddPrizesAsync(
        Guid eventId, int currentPrizes, int addingCount, CancellationToken cancellationToken)
    {
        var accountResult = await LoadAccountByEventAsync(eventId, cancellationToken);
        if (accountResult.IsFailure)
            return UnitResult.Failure(accountResult.Error);

        var plan = PlanCatalog.GetOrFree(accountResult.Value.Plan);
        if (plan.MaxPrizesPerRaffle is { } maxPrizes && currentPrizes + addingCount > maxPrizes)
            return UnitResult.Failure(AccountErrors.PlanPrizeLimit(maxPrizes, Math.Max(0, maxPrizes - currentPrizes)));

        return UnitResult.Success<BaseError>();
    }

    public async Task<Result<PlanParticipantLimitCheck, BaseError>> CheckParticipantLimitAsync(
        Guid eventId, int resultingTotal, int currentCount, CancellationToken cancellationToken)
    {
        var accountResult = await LoadAccountByEventAsync(eventId, cancellationToken);
        if (accountResult.IsFailure)
            return Result.Failure<PlanParticipantLimitCheck, BaseError>(accountResult.Error);

        var plan = PlanCatalog.GetOrFree(accountResult.Value.Plan);
        if (plan.MaxParticipantsPerRaffle is null)
            return Result.Success<PlanParticipantLimitCheck, BaseError>(new PlanParticipantLimitCheck(true, null, null));

        var limit = plan.MaxParticipantsPerRaffle.Value;
        var remaining = Math.Max(0, limit - currentCount);
        return Result.Success<PlanParticipantLimitCheck, BaseError>(
            new PlanParticipantLimitCheck(resultingTotal <= limit, limit, remaining));
    }

    public async Task<UnitResult<BaseError>> EnsureFeatureEnabledAsync(
        Guid accountId, string featureKey, CancellationToken cancellationToken)
    {
        var accountResult = await LoadAccountAsync(accountId, cancellationToken);
        if (accountResult.IsFailure)
            return UnitResult.Failure(accountResult.Error);

        var account = accountResult.Value;
        if (account.Status != AccountStatus.Active)
            return UnitResult.Failure(AccountErrors.AccountNotActive());

        var plan = PlanCatalog.GetOrFree(account.Plan);
        var allowed = featureKey switch
        {
            "api" => plan.ApiEnabled,
            "branding" => plan.CustomBrandingEnabled,
            "projector" => plan.ProjectorEnabled,
            _ => true,
        };

        return allowed
            ? UnitResult.Success<BaseError>()
            : UnitResult.Failure(AccountErrors.FeatureNotIncluded(featureKey));
    }

    private async Task<Result<AccountData, BaseError>> LoadAccountByEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var accountId = await _context.Events
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(e => e.Id == eventId)
            .Select(e => e.AccountId)
            .FirstOrDefaultAsync(cancellationToken);

        return await LoadAccountAsync(
            accountId == Guid.Empty ? Account.DefaultBanreservasAccountId : accountId,
            cancellationToken);
    }

    private async Task<Result<AccountData, BaseError>> LoadAccountAsync(Guid accountId, CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty)
            return Result.Failure<AccountData, BaseError>(AccountErrors.AccountNotActive());

        var account = await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);

        if (account is not null)
            return Result.Success<AccountData, BaseError>(account);

        // Si es la cuenta por defecto y aún no existe en una base en memoria/parcial, asumir Avanzado activa.
        if (accountId == Account.DefaultBanreservasAccountId)
        {
            var now = _clock.GetUtcNow();
            return Result.Success<AccountData, BaseError>(new AccountData
            {
                Id = Account.DefaultBanreservasAccountId,
                Name = "Banreservas",
                Plan = PlanCatalog.Advanced,
                Status = AccountStatus.Active,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        return Result.Failure<AccountData, BaseError>(PlanRequestErrors.AccountNotFound());
    }
}

