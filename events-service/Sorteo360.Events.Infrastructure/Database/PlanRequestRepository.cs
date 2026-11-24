using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sorteo360.Events.Core.Accounts;
using Sorteo360.Platform.Database;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Infrastructure.Database;

public sealed class PlanRequestRepository : IPlanRequestRepository
{
    private readonly EventsDbContext _context;
    private readonly ILogger<PlanRequestRepository> _logger;

    public PlanRequestRepository(EventsDbContext context, ILogger<PlanRequestRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        _context = context;
        _logger = logger;
    }

    public Task<Result<Maybe<Account>, BaseError>> GetAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "leer la cuenta", async () =>
        {
            var data = await _context.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);

            return data is null ? Maybe<Account>.None : Maybe.From(MapToAccount(data));
        });

    public Task<Result<Account, BaseError>> EnsureDefaultAccountAsync(CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "asegurar la cuenta por defecto", async () =>
        {
            var existing = await _context.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == Account.DefaultBanreservasAccountId, cancellationToken);

            if (existing is not null)
                return MapToAccount(existing);

            var now = DateTimeOffset.UtcNow;
            var created = new AccountData
            {
                Id = Account.DefaultBanreservasAccountId,
                Name = "Banreservas",
                Plan = PlanCatalog.Advanced,
                Status = AccountStatus.Active,
                OwnerEmail = null,
                PlanStartedAt = now,
                PlanExpiresAt = null,
                CreatedAt = now,
                UpdatedAt = now,
            };

            _context.Accounts.Add(created);
            await _context.SaveChangesAsync(cancellationToken);
            return MapToAccount(created);
        });

    public Task<Result<IReadOnlyList<PlanRequest>, BaseError>> ListForAccountAsync(
        Guid accountId, CancellationToken cancellationToken) =>
        DbResult.TryAsync<IReadOnlyList<PlanRequest>>(_logger, "listar solicitudes de plan de la cuenta", async () =>
        {
            var accountName = await _context.Accounts
                .AsNoTracking()
                .Where(a => a.Id == accountId)
                .Select(a => a.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

            var rows = await _context.PlanRequests
                .AsNoTracking()
                .Where(r => r.AccountId == accountId)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync(cancellationToken);

            return rows.Select(r => MapToPlanRequest(r, accountName)).ToList();
        });

    public Task<Result<IReadOnlyList<PlanRequest>, BaseError>> ListAllAsync(
        string? status, CancellationToken cancellationToken) =>
        DbResult.TryAsync<IReadOnlyList<PlanRequest>>(_logger, "listar solicitudes de plan", async () =>
        {
            var query = _context.PlanRequests.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(r => r.Status == status);

            var rows = await query
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync(cancellationToken);

            var accountIds = rows.Select(r => r.AccountId).Distinct().ToList();
            var accountNames = await _context.Accounts
                .AsNoTracking()
                .Where(a => accountIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, a => a.Name, cancellationToken);

            return rows.Select(r => MapToPlanRequest(r, accountNames.GetValueOrDefault(r.AccountId, string.Empty))).ToList();
        });

    public Task<Result<PlanRequest, BaseError>> CreatePendingAsync(
        Guid accountId,
        string requestedPlan,
        string? transferReference,
        Guid requestedById,
        string? requesterEmail,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        DbResult.TryResultAsync(_logger, "crear solicitud de plan", async () =>
        {
            var account = await _context.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);
            if (account is null)
                return Result.Failure<PlanRequest, BaseError>(PlanRequestErrors.AccountNotFound());

            var hasPending = await _context.PlanRequests
                .AsNoTracking()
                .AnyAsync(r => r.AccountId == accountId && r.Status == PlanRequestStatus.Pending, cancellationToken);
            if (hasPending)
                return Result.Failure<PlanRequest, BaseError>(PlanRequestErrors.PendingRequestExists());

            var data = new PlanRequestData
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                RequestedPlan = requestedPlan,
                Status = PlanRequestStatus.Pending,
                TransferReference = transferReference,
                RequestedAt = now,
                RequestedById = requestedById,
                RequesterEmail = requesterEmail,
            };

            _context.PlanRequests.Add(data);
            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success<PlanRequest, BaseError>(MapToPlanRequest(data, account.Name));
        });

    public Task<Result<PlanRequest, BaseError>> CancelPendingAsync(
        Guid accountId,
        Guid requestId,
        Guid actorId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        DbResult.TryResultAsync(_logger, "cancelar solicitud de plan", async () =>
        {
            var data = await _context.PlanRequests
                .FirstOrDefaultAsync(r => r.Id == requestId && r.AccountId == accountId, cancellationToken);
            if (data is null)
                return Result.Failure<PlanRequest, BaseError>(PlanRequestErrors.NotFound());

            if (data.Status != PlanRequestStatus.Pending)
                return Result.Failure<PlanRequest, BaseError>(PlanRequestErrors.NotPending());

            data.Status = PlanRequestStatus.Cancelled;
            data.DecidedAt = now;
            data.DecidedById = actorId;

            await _context.SaveChangesAsync(cancellationToken);

            var accountName = await _context.Accounts
                .AsNoTracking()
                .Where(a => a.Id == accountId)
                .Select(a => a.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

            return Result.Success<PlanRequest, BaseError>(MapToPlanRequest(data, accountName));
        });

    public Task<Result<(PlanRequest Request, Account Account), BaseError>> ApprovePendingAsync(
        Guid requestId,
        Guid decidedById,
        DateTimeOffset paidPeriodStart,
        DateTimeOffset paidPeriodEnd,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        DbResult.TryResultAsync(_logger, "aprobar solicitud de plan", async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            var data = await _context.PlanRequests
                .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
            if (data is null)
                return Result.Failure<(PlanRequest, Account), BaseError>(PlanRequestErrors.NotFound());

            if (data.Status != PlanRequestStatus.Pending)
                return Result.Failure<(PlanRequest, Account), BaseError>(PlanRequestErrors.NotPending());

            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Id == data.AccountId, cancellationToken);
            if (account is null)
                return Result.Failure<(PlanRequest, Account), BaseError>(PlanRequestErrors.AccountNotFound());

            data.Status = PlanRequestStatus.Approved;
            data.DecidedAt = now;
            data.DecidedById = decidedById;
            data.PaidPeriodStart = paidPeriodStart;
            data.PaidPeriodEnd = paidPeriodEnd;

            account.Plan = data.RequestedPlan;
            account.PlanStartedAt = paidPeriodStart;
            account.PlanExpiresAt = paidPeriodEnd;
            account.ExpiryWarningSentAt = null;
            account.UpdatedAt = now;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result.Success<(PlanRequest, Account), BaseError>(
                (MapToPlanRequest(data, account.Name), MapToAccount(account)));
        });

    public Task<Result<PlanRequest, BaseError>> RejectPendingAsync(
        Guid requestId,
        Guid decidedById,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        DbResult.TryResultAsync(_logger, "rechazar solicitud de plan", async () =>
        {
            var data = await _context.PlanRequests
                .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
            if (data is null)
                return Result.Failure<PlanRequest, BaseError>(PlanRequestErrors.NotFound());

            if (data.Status != PlanRequestStatus.Pending)
                return Result.Failure<PlanRequest, BaseError>(PlanRequestErrors.NotPending());

            data.Status = PlanRequestStatus.Rejected;
            data.DecidedAt = now;
            data.DecidedById = decidedById;
            data.RejectionReason = reason;

            await _context.SaveChangesAsync(cancellationToken);

            var accountName = await _context.Accounts
                .AsNoTracking()
                .Where(a => a.Id == data.AccountId)
                .Select(a => a.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

            return Result.Success<PlanRequest, BaseError>(MapToPlanRequest(data, accountName));
        });

    public Task<Result<IReadOnlyList<Account>, BaseError>> DowngradeExpiredAccountsAsync(
        DateTimeOffset now, CancellationToken cancellationToken) =>
        DbResult.TryAsync<IReadOnlyList<Account>>(_logger, "bajar cuentas con plan vencido", async () =>
        {
            var expired = await _context.Accounts
                .Where(a => a.Plan != PlanCatalog.Free && a.PlanExpiresAt != null && a.PlanExpiresAt <= now)
                .ToListAsync(cancellationToken);

            foreach (var account in expired)
            {
                account.Plan = PlanCatalog.Free;
                account.PlanExpiresAt = null;
                account.ExpiryWarningSentAt = null;
                account.UpdatedAt = now;
            }

            if (expired.Count > 0)
                await _context.SaveChangesAsync(cancellationToken);

            return expired.Select(MapToAccount).ToList();
        });

    public Task<Result<IReadOnlyList<Account>, BaseError>> MarkExpiringAccountsWarnedAsync(
        DateTimeOffset now, DateTimeOffset warnBefore, CancellationToken cancellationToken) =>
        DbResult.TryAsync<IReadOnlyList<Account>>(_logger, "marcar cuentas próximas a vencer", async () =>
        {
            var expiring = await _context.Accounts
                .Where(a => a.Plan != PlanCatalog.Free
                    && a.PlanExpiresAt != null
                    && a.PlanExpiresAt > now
                    && a.PlanExpiresAt <= warnBefore
                    && a.ExpiryWarningSentAt == null)
                .ToListAsync(cancellationToken);

            foreach (var account in expiring)
            {
                account.ExpiryWarningSentAt = now;
                account.UpdatedAt = now;
            }

            if (expiring.Count > 0)
                await _context.SaveChangesAsync(cancellationToken);

            return expiring.Select(MapToAccount).ToList();
        });

    private static Account MapToAccount(AccountData data) => new(
        data.Id,
        data.Name,
        data.Plan,
        data.Status,
        data.OwnerEmail,
        data.PlanStartedAt,
        data.PlanExpiresAt,
        data.ExpiryWarningSentAt,
        data.CreatedAt,
        data.UpdatedAt);

    private static PlanRequest MapToPlanRequest(PlanRequestData data, string accountName) => new(
        data.Id,
        data.AccountId,
        accountName,
        data.RequestedPlan,
        data.Status,
        data.TransferReference,
        data.RequestedAt,
        data.RequestedById,
        data.RequesterEmail,
        data.DecidedAt,
        data.DecidedById,
        data.RejectionReason,
        data.PaidPeriodStart,
        data.PaidPeriodEnd);
}

