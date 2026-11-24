using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sorteo360.Events.Core.Accounts;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Platform.Database;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Infrastructure.Database;

public sealed class AccountOnboardingRepository : IAccountOnboardingRepository
{
    private readonly EventsDbContext _context;
    private readonly ILogger<AccountOnboardingRepository> _logger;

    public AccountOnboardingRepository(EventsDbContext context, ILogger<AccountOnboardingRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        _context = context;
        _logger = logger;
    }

    public Task<Result<bool, BaseError>> HasActiveAccountWithEmailAsync(
        string emailNormalized, CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "comprobar cuenta activa por correo", () =>
            _context.Accounts
                .AsNoTracking()
                .AnyAsync(a => a.Status == AccountStatus.Active && a.OwnerEmail == emailNormalized, cancellationToken));

    public Task<Result<AccountActivation, BaseError>> CreatePendingSignupAsync(
        string companyName,
        string fullName,
        string emailNormalized,
        string? requestedPaidPlan,
        string billingPeriod,
        byte[] tokenHash,
        DateTimeOffset now,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "registrar cuenta pendiente de activación", async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            var account = new AccountData
            {
                Id = Guid.NewGuid(),
                Name = companyName,
                Plan = PlanCatalog.Free,
                Status = AccountStatus.PendingActivation,
                OwnerEmail = emailNormalized,
                CreatedAt = now,
                UpdatedAt = now,
            };

            var activation = new AccountActivationData
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                CompanyName = companyName,
                FullName = fullName,
                EmailNormalized = emailNormalized,
                RequestedPlan = requestedPaidPlan,
                BillingPeriod = billingPeriod,
                TokenHash = tokenHash,
                ExpiresAt = expiresAt,
                CreatedAt = now,
                LastSentAt = now,
                ResendCountToday = 1,
                ResendDay = DateOnly.FromDateTime(now.UtcDateTime),
            };

            _context.Accounts.Add(account);
            _context.AccountActivations.Add(activation);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return MapToActivation(activation);
        });

    public Task<Result<Maybe<AccountActivation>, BaseError>> FindLatestPendingByEmailAsync(
        string emailNormalized, CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "buscar activación pendiente por correo", async () =>
        {
            var data = await _context.AccountActivations
                .AsNoTracking()
                .Where(a => a.EmailNormalized == emailNormalized && a.ConsumedAt == null)
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            return data is null ? Maybe<AccountActivation>.None : Maybe.From(MapToActivation(data));
        });

    public Task<Result<AccountActivation, BaseError>> RotateActivationTokenAsync(
        Guid activationId,
        byte[] newTokenHash,
        DateTimeOffset now,
        DateTimeOffset expiresAt,
        int nextResendCountToday,
        DateOnly resendDay,
        CancellationToken cancellationToken) =>
        DbResult.TryResultAsync(_logger, "rotar token de activación", async () =>
        {
            var data = await _context.AccountActivations
                .FirstOrDefaultAsync(a => a.Id == activationId, cancellationToken);
            if (data is null || data.ConsumedAt is not null)
                return Result.Failure<AccountActivation, BaseError>(AccountErrors.ActivationTokenInvalid());

            data.TokenHash = newTokenHash;
            data.ExpiresAt = expiresAt;
            data.LastSentAt = now;
            data.ResendCountToday = nextResendCountToday;
            data.ResendDay = resendDay;

            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success<AccountActivation, BaseError>(MapToActivation(data));
        });

    public Task<Result<Maybe<AccountActivation>, BaseError>> FindByTokenHashAsync(
        byte[] tokenHash, CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "buscar activación por token", async () =>
        {
            var data = await _context.AccountActivations
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.TokenHash == tokenHash, cancellationToken);

            return data is null ? Maybe<AccountActivation>.None : Maybe.From(MapToActivation(data));
        });

    public Task<Result<ActivateAccountResult, BaseError>> ActivateLockedAsync(
        byte[] tokenHash,
        DateTimeOffset now,
        Func<AccountActivation, Task<Result<Guid, BaseError>>> provisionUser,
        CancellationToken cancellationToken) =>
        DbResult.TryResultAsync(_logger, "activar cuenta bajo bloqueo exclusivo", async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            // R8: bloquear la fila de activación para serializar dobles clics concurrentes.
            var lockedIds = await _context.Database
                .SqlQuery<Guid>($"SELECT id AS \"Value\" FROM events.account_activation WHERE token_hash = {tokenHash} FOR UPDATE")
                .ToListAsync(cancellationToken);

            if (lockedIds.Count == 0)
                return Result.Failure<ActivateAccountResult, BaseError>(AccountErrors.ActivationTokenInvalid());

            var activation = await _context.AccountActivations
                .FirstOrDefaultAsync(a => a.Id == lockedIds[0], cancellationToken);
            if (activation is null || activation.ConsumedAt is not null || activation.ExpiresAt <= now)
                return Result.Failure<ActivateAccountResult, BaseError>(AccountErrors.ActivationTokenInvalid());

            var emailTaken = await _context.Accounts
                .AsNoTracking()
                .AnyAsync(a => a.OwnerEmail == activation.EmailNormalized && a.Status == AccountStatus.Active, cancellationToken);
            if (emailTaken)
                return Result.Failure<ActivateAccountResult, BaseError>(AccountErrors.EmailTaken());

            var domainActivation = MapToActivation(activation);
            var provisioned = await provisionUser(domainActivation);
            if (provisioned.IsFailure)
                return Result.Failure<ActivateAccountResult, BaseError>(provisioned.Error);

            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Id == activation.AccountId, cancellationToken);
            if (account is null)
                return Result.Failure<ActivateAccountResult, BaseError>(PlanRequestErrors.AccountNotFound());

            activation.ConsumedAt = now;
            account.Status = AccountStatus.Active;
            account.Plan = PlanCatalog.Free;
            account.PlanStartedAt = now;
            account.UpdatedAt = now;

            if (PlanCatalog.IsPaidPlan(activation.RequestedPlan))
            {
                _context.PlanRequests.Add(new PlanRequestData
                {
                    Id = Guid.NewGuid(),
                    AccountId = account.Id,
                    RequestedPlan = activation.RequestedPlan!,
                    Status = PlanRequestStatus.Pending,
                    TransferReference = $"Registro ({activation.BillingPeriod})",
                    RequestedAt = now,
                    RequestedById = provisioned.Value,
                    RequesterEmail = activation.EmailNormalized,
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result.Success<ActivateAccountResult, BaseError>(new ActivateAccountResult(
                activation.EmailNormalized,
                account.Name,
                activation.RequestedPlan));
        });

    public Task<Result<ActivateAccountResult, BaseError>> CompleteActivationAsync(
        Guid activationId,
        Guid ownerUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        DbResult.TryResultAsync(_logger, "completar activación de cuenta", async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            await _context.Database
                .SqlQuery<Guid>($"SELECT id AS \"Value\" FROM events.account_activation WHERE id = {activationId} FOR UPDATE")
                .ToListAsync(cancellationToken);

            var activation = await _context.AccountActivations
                .FirstOrDefaultAsync(a => a.Id == activationId, cancellationToken);
            if (activation is null || activation.ConsumedAt is not null)
                return Result.Failure<ActivateAccountResult, BaseError>(AccountErrors.ActivationTokenInvalid());

            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Id == activation.AccountId, cancellationToken);
            if (account is null)
                return Result.Failure<ActivateAccountResult, BaseError>(PlanRequestErrors.AccountNotFound());

            activation.ConsumedAt = now;
            account.Status = AccountStatus.Active;
            account.Plan = PlanCatalog.Free;
            account.PlanStartedAt = now;
            account.UpdatedAt = now;

            if (PlanCatalog.IsPaidPlan(activation.RequestedPlan))
            {
                _context.PlanRequests.Add(new PlanRequestData
                {
                    Id = Guid.NewGuid(),
                    AccountId = account.Id,
                    RequestedPlan = activation.RequestedPlan!,
                    Status = PlanRequestStatus.Pending,
                    TransferReference = $"Registro ({activation.BillingPeriod})",
                    RequestedAt = now,
                    RequestedById = ownerUserId,
                    RequesterEmail = activation.EmailNormalized,
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result.Success<ActivateAccountResult, BaseError>(new ActivateAccountResult(
                activation.EmailNormalized,
                account.Name,
                activation.RequestedPlan));
        });

    public Task<Result<AccountUsageSummary, BaseError>> GetAccountUsageSummaryAsync(
        Guid accountId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        DbResult.TryResultAsync(_logger, "leer resumen de cuenta y uso del plan", async () =>
        {
            var resolvedId = accountId == Guid.Empty ? Account.DefaultBanreservasAccountId : accountId;
            var data = await _context.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == resolvedId, cancellationToken);

            if (data is null && resolvedId == Account.DefaultBanreservasAccountId)
            {
                data = new AccountData
                {
                    Id = Account.DefaultBanreservasAccountId,
                    Name = "Banreservas",
                    Plan = PlanCatalog.Advanced,
                    Status = AccountStatus.Active,
                    PlanStartedAt = now,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                _context.Accounts.Add(data);
                await _context.SaveChangesAsync(cancellationToken);
            }

            if (data is null)
                return Result.Failure<AccountUsageSummary, BaseError>(PlanRequestErrors.AccountNotFound());

            var activeRaffles = await (
                from r in _context.Raffles.AsNoTracking()
                join e in _context.Events.IgnoreQueryFilters().AsNoTracking() on r.EventId equals e.Id
                where e.DeletedAt == null && e.AccountId == data.Id && r.State != RaffleState.Completed
                select r.Id
            ).CountAsync(cancellationToken);

            var yearMonth = PlanCatalog.CurrentYearMonth(now);
            var monthlyCreated = await _context.AccountMonthlyUsages
                .AsNoTracking()
                .Where(u => u.AccountId == data.Id && u.YearMonth == yearMonth)
                .Select(u => u.RafflesCreated)
                .FirstOrDefaultAsync(cancellationToken);

            var account = new Account(
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

            var plan = PlanCatalog.GetOrFree(account.Plan);
            return Result.Success<AccountUsageSummary, BaseError>(new AccountUsageSummary(
                account,
                plan,
                activeRaffles,
                monthlyCreated,
                UsersCount: 1,
                ResetsAt: PlanCatalog.NextMonthlyResetUtc(now)));
        });

    private static AccountActivation MapToActivation(AccountActivationData d) => new(
        d.Id,
        d.AccountId,
        d.CompanyName,
        d.FullName,
        d.EmailNormalized,
        d.RequestedPlan,
        d.BillingPeriod,
        d.TokenHash,
        d.ExpiresAt,
        d.ConsumedAt,
        d.CreatedAt,
        d.LastSentAt,
        d.ResendCountToday,
        d.ResendDay);
}

