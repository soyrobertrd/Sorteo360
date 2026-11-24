using CSharpFunctionalExtensions;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Accounts;

public interface IPlanRequestRepository
{
    Task<Result<Maybe<Account>, BaseError>> GetAccountAsync(Guid accountId, CancellationToken cancellationToken);

    Task<Result<Account, BaseError>> EnsureDefaultAccountAsync(CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<PlanRequest>, BaseError>> ListForAccountAsync(
        Guid accountId, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<PlanRequest>, BaseError>> ListAllAsync(
        string? status, CancellationToken cancellationToken);

    Task<Result<PlanRequest, BaseError>> CreatePendingAsync(
        Guid accountId,
        string requestedPlan,
        string? transferReference,
        Guid requestedById,
        string? requesterEmail,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<Result<PlanRequest, BaseError>> CancelPendingAsync(
        Guid accountId,
        Guid requestId,
        Guid actorId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<Result<(PlanRequest Request, Account Account), BaseError>> ApprovePendingAsync(
        Guid requestId,
        Guid decidedById,
        DateTimeOffset paidPeriodStart,
        DateTimeOffset paidPeriodEnd,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<Result<PlanRequest, BaseError>> RejectPendingAsync(
        Guid requestId,
        Guid decidedById,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<Account>, BaseError>> DowngradeExpiredAccountsAsync(
        DateTimeOffset now, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<Account>, BaseError>> MarkExpiringAccountsWarnedAsync(
        DateTimeOffset now, DateTimeOffset warnBefore, CancellationToken cancellationToken);
}

public interface IPlanRequestNotifier
{
    Task NotifyRequestedAsync(PlanRequest request, CancellationToken cancellationToken);
    Task NotifyApprovedAsync(PlanRequest request, Account account, CancellationToken cancellationToken);
    Task NotifyRejectedAsync(PlanRequest request, CancellationToken cancellationToken);
    Task NotifyExpiringSoonAsync(Account account, CancellationToken cancellationToken);
}

public interface IPlanRequestService
{
    Task<Result<(Account Account, IReadOnlyList<PlanRequest> Requests), BaseError>> ListForAccountAsync(
        Guid accountId, CancellationToken cancellationToken);

    Task<Result<PlanRequest, BaseError>> CreateAsync(
        Guid accountId,
        string? requestedPlan,
        string? transferReference,
        Guid requestedById,
        string? requesterEmail,
        CancellationToken cancellationToken);

    Task<Result<PlanRequest, BaseError>> CancelAsync(
        Guid accountId,
        Guid requestId,
        Guid actorId,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<PlanRequest>, BaseError>> ListAllAsync(
        string? status, CancellationToken cancellationToken);

    Task<Result<PlanRequest, BaseError>> ApproveAsync(
        Guid requestId,
        Guid decidedById,
        DateTimeOffset? paidPeriodStart,
        DateTimeOffset? paidPeriodEnd,
        CancellationToken cancellationToken);

    Task<Result<PlanRequest, BaseError>> RejectAsync(
        Guid requestId,
        Guid decidedById,
        string? reason,
        CancellationToken cancellationToken);

    Task<Result<PlanExpirationRunResult, BaseError>> ProcessExpirationsAsync(CancellationToken cancellationToken);
}

public sealed record PlanExpirationRunResult(int DowngradedCount, int WarnedCount);

