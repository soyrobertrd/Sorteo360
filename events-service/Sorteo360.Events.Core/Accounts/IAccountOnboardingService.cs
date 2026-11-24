using CSharpFunctionalExtensions;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Accounts;

/// <summary>Registro de activación pendiente de una cuenta (§9.3).</summary>
public sealed record AccountActivation(
    Guid Id,
    Guid AccountId,
    string CompanyName,
    string FullName,
    string EmailNormalized,
    string? RequestedPlan,
    string BillingPeriod,
    byte[] TokenHash,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? ConsumedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSentAt,
    int ResendCountToday,
    DateOnly ResendDay);

public sealed record SignupInput(
    string CompanyName,
    string FullName,
    string Email,
    string Plan,
    string BillingPeriod,
    bool AcceptTerms);

public sealed record ActivateAccountResult(
    string Email,
    string CompanyName,
    string? RequestedPlan);

public sealed record AccountUsageSummary(
    Account Account,
    PlanDefinition Plan,
    int ActiveRaffles,
    int MonthlyRafflesCreated,
    int UsersCount,
    DateTimeOffset ResetsAt);

public interface IAccountOnboardingRepository
{
    Task<Result<bool, BaseError>> HasActiveAccountWithEmailAsync(string emailNormalized, CancellationToken cancellationToken);

    Task<Result<AccountActivation, BaseError>> CreatePendingSignupAsync(
        string companyName,
        string fullName,
        string emailNormalized,
        string? requestedPaidPlan,
        string billingPeriod,
        byte[] tokenHash,
        DateTimeOffset now,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken);

    Task<Result<Maybe<AccountActivation>, BaseError>> FindLatestPendingByEmailAsync(
        string emailNormalized, CancellationToken cancellationToken);

    Task<Result<AccountActivation, BaseError>> RotateActivationTokenAsync(
        Guid activationId,
        byte[] newTokenHash,
        DateTimeOffset now,
        DateTimeOffset expiresAt,
        int nextResendCountToday,
        DateOnly resendDay,
        CancellationToken cancellationToken);

    Task<Result<Maybe<AccountActivation>, BaseError>> FindByTokenHashAsync(
        byte[] tokenHash, CancellationToken cancellationToken);

    Task<Result<ActivateAccountResult, BaseError>> CompleteActivationAsync(
        Guid activationId,
        Guid ownerUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Ejecuta la activación bajo bloqueo exclusivo de fila (<c>SELECT ... FOR UPDATE</c>) para
    /// serializar dobles clics y permitir reintentos seguros si falla la escritura final (R8).
    /// </summary>
    async Task<Result<ActivateAccountResult, BaseError>> ActivateLockedAsync(
        byte[] tokenHash,
        DateTimeOffset now,
        Func<AccountActivation, Task<Result<Guid, BaseError>>> provisionUser,
        CancellationToken cancellationToken)
    {
        var foundResult = await FindByTokenHashAsync(tokenHash, cancellationToken);
        if (foundResult.IsFailure)
            return Result.Failure<ActivateAccountResult, BaseError>(foundResult.Error);

        if (foundResult.Value.HasNoValue)
            return Result.Failure<ActivateAccountResult, BaseError>(AccountErrors.ActivationTokenInvalid());

        var activation = foundResult.Value.Value;
        if (activation.ConsumedAt is not null || activation.ExpiresAt <= now)
            return Result.Failure<ActivateAccountResult, BaseError>(AccountErrors.ActivationTokenInvalid());

        var existsResult = await HasActiveAccountWithEmailAsync(activation.EmailNormalized, cancellationToken);
        if (existsResult.IsFailure)
            return Result.Failure<ActivateAccountResult, BaseError>(existsResult.Error);
        if (existsResult.Value)
            return Result.Failure<ActivateAccountResult, BaseError>(AccountErrors.EmailTaken());

        var provisioned = await provisionUser(activation);
        if (provisioned.IsFailure)
            return Result.Failure<ActivateAccountResult, BaseError>(provisioned.Error);

        return await CompleteActivationAsync(activation.Id, provisioned.Value, now, cancellationToken);
    }

    Task<Result<AccountUsageSummary, BaseError>> GetAccountUsageSummaryAsync(
        Guid accountId,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}

public interface IKeycloakAccountProvisioner
{
    /// <summary>
    /// Crea el usuario administrador de la cuenta en Keycloak con su contraseña definitiva (nunca
    /// se persiste ni registra), el atributo <c>account_id</c> y el rol compuesto
    /// <c>account_admin</c> (§9.3). Si Keycloak rechaza la contraseña por política del realm,
    /// devuelve <see cref="AccountErrors.PasswordPolicyRejected"/> y el token NO se quema.
    /// </summary>
    Task<Result<Guid, BaseError>> CreateAccountAdminAsync(
        string email,
        string fullName,
        string password,
        Guid accountId,
        CancellationToken cancellationToken);
}

public interface IAccountOnboardingNotifier
{
    Task SendActivationEmailAsync(
        string email,
        string fullName,
        string companyName,
        string rawToken,
        CancellationToken cancellationToken);
}

public interface IAccountOnboardingService
{
    Task<UnitResult<BaseError>> SignupAsync(SignupInput input, CancellationToken cancellationToken);
    Task<UnitResult<BaseError>> ResendActivationAsync(string? email, CancellationToken cancellationToken);
    Task<Result<ActivateAccountResult, BaseError>> ActivateAsync(string? token, string? password, CancellationToken cancellationToken);
    Task<Result<AccountUsageSummary, BaseError>> GetAccountSummaryAsync(Guid accountId, CancellationToken cancellationToken);
}

