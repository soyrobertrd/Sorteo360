using CSharpFunctionalExtensions;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Accounts;

/// <summary>
/// Orquesta las solicitudes de plan por transferencia bancaria y el vencimiento de períodos pagados (§9.5).
/// </summary>
public sealed class PlanRequestService : IPlanRequestService
{
    public static readonly TimeSpan DefaultPaidPeriod = TimeSpan.FromDays(30);
    public static readonly TimeSpan ExpiryWarningLeadTime = TimeSpan.FromDays(7);

    private readonly IPlanRequestRepository _repository;
    private readonly IPlanRequestNotifier _notifier;
    private readonly TimeProvider _clock;

    public PlanRequestService(
        IPlanRequestRepository repository,
        IPlanRequestNotifier notifier,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(clock);
        _repository = repository;
        _notifier = notifier;
        _clock = clock;
    }

    public async Task<Result<(Account Account, IReadOnlyList<PlanRequest> Requests), BaseError>> ListForAccountAsync(
        Guid accountId, CancellationToken cancellationToken)
    {
        var accountResult = await ResolveAccountAsync(accountId, cancellationToken);
        if (accountResult.IsFailure)
            return Result.Failure<(Account, IReadOnlyList<PlanRequest>), BaseError>(accountResult.Error);

        var requestsResult = await _repository.ListForAccountAsync(accountResult.Value.Id, cancellationToken);
        if (requestsResult.IsFailure)
            return Result.Failure<(Account, IReadOnlyList<PlanRequest>), BaseError>(requestsResult.Error);

        return Result.Success<(Account, IReadOnlyList<PlanRequest>), BaseError>(
            (accountResult.Value, requestsResult.Value));
    }

    public async Task<Result<PlanRequest, BaseError>> CreateAsync(
        Guid accountId,
        string? requestedPlan,
        string? transferReference,
        Guid requestedById,
        string? requesterEmail,
        CancellationToken cancellationToken)
    {
        if (!PlanCatalog.IsPaidPlan(requestedPlan))
            return Result.Failure<PlanRequest, BaseError>(PlanRequestErrors.InvalidRequestedPlan());

        var accountResult = await ResolveAccountAsync(accountId, cancellationToken);
        if (accountResult.IsFailure)
            return Result.Failure<PlanRequest, BaseError>(accountResult.Error);

        var normalizedPlan = PlanCatalog.ByKey[requestedPlan!.Trim()].Key;
        var trimmedRef = string.IsNullOrWhiteSpace(transferReference) ? null : transferReference.Trim();
        var trimmedEmail = string.IsNullOrWhiteSpace(requesterEmail) ? accountResult.Value.OwnerEmail : requesterEmail.Trim();

        var created = await _repository.CreatePendingAsync(
            accountResult.Value.Id,
            normalizedPlan,
            trimmedRef,
            requestedById,
            trimmedEmail,
            _clock.GetUtcNow(),
            cancellationToken);

        if (created.IsFailure)
            return created;

        await _notifier.NotifyRequestedAsync(created.Value, cancellationToken);
        return created;
    }

    public async Task<Result<PlanRequest, BaseError>> CancelAsync(
        Guid accountId,
        Guid requestId,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var accountResult = await ResolveAccountAsync(accountId, cancellationToken);
        if (accountResult.IsFailure)
            return Result.Failure<PlanRequest, BaseError>(accountResult.Error);

        return await _repository.CancelPendingAsync(
            accountResult.Value.Id,
            requestId,
            actorId,
            _clock.GetUtcNow(),
            cancellationToken);
    }

    public Task<Result<IReadOnlyList<PlanRequest>, BaseError>> ListAllAsync(
        string? status, CancellationToken cancellationToken)
    {
        var normalized = string.IsNullOrWhiteSpace(status) ? null : status.Trim();
        return _repository.ListAllAsync(normalized, cancellationToken);
    }

    public async Task<Result<PlanRequest, BaseError>> ApproveAsync(
        Guid requestId,
        Guid decidedById,
        DateTimeOffset? paidPeriodStart,
        DateTimeOffset? paidPeriodEnd,
        CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        var start = paidPeriodStart ?? now;
        var end = paidPeriodEnd ?? start.Add(DefaultPaidPeriod);

        if (end <= start)
            return Result.Failure<PlanRequest, BaseError>(PlanRequestErrors.InvalidPaidPeriod());

        var approved = await _repository.ApprovePendingAsync(
            requestId,
            decidedById,
            start,
            end,
            now,
            cancellationToken);

        if (approved.IsFailure)
            return Result.Failure<PlanRequest, BaseError>(approved.Error);

        await _notifier.NotifyApprovedAsync(approved.Value.Request, approved.Value.Account, cancellationToken);
        return Result.Success<PlanRequest, BaseError>(approved.Value.Request);
    }

    public async Task<Result<PlanRequest, BaseError>> RejectAsync(
        Guid requestId,
        Guid decidedById,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure<PlanRequest, BaseError>(PlanRequestErrors.RejectionReasonRequired());

        var rejected = await _repository.RejectPendingAsync(
            requestId,
            decidedById,
            reason.Trim(),
            _clock.GetUtcNow(),
            cancellationToken);

        if (rejected.IsFailure)
            return rejected;

        await _notifier.NotifyRejectedAsync(rejected.Value, cancellationToken);
        return rejected;
    }

    public async Task<Result<PlanExpirationRunResult, BaseError>> ProcessExpirationsAsync(CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();

        var downgraded = await _repository.DowngradeExpiredAccountsAsync(now, cancellationToken);
        if (downgraded.IsFailure)
            return Result.Failure<PlanExpirationRunResult, BaseError>(downgraded.Error);

        var warned = await _repository.MarkExpiringAccountsWarnedAsync(
            now, now.Add(ExpiryWarningLeadTime), cancellationToken);
        if (warned.IsFailure)
            return Result.Failure<PlanExpirationRunResult, BaseError>(warned.Error);

        foreach (var account in warned.Value)
            await _notifier.NotifyExpiringSoonAsync(account, cancellationToken);

        return Result.Success<PlanExpirationRunResult, BaseError>(
            new PlanExpirationRunResult(downgraded.Value.Count, warned.Value.Count));
    }

    private async Task<Result<Account, BaseError>> ResolveAccountAsync(Guid accountId, CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty)
            return Result.Failure<Account, BaseError>(AccountErrors.AccountNotActive());

        if (accountId == Account.DefaultBanreservasAccountId)
            return await _repository.EnsureDefaultAccountAsync(cancellationToken);

        var found = await _repository.GetAccountAsync(accountId, cancellationToken);
        if (found.IsFailure)
            return Result.Failure<Account, BaseError>(found.Error);

        return found.Value.HasValue
            ? Result.Success<Account, BaseError>(found.Value.Value)
            : Result.Failure<Account, BaseError>(PlanRequestErrors.AccountNotFound());
    }
}

