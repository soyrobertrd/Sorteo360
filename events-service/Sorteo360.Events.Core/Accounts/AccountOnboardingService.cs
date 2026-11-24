using System.Security.Cryptography;
using System.Text;
using CSharpFunctionalExtensions;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Accounts;

/// <summary>
/// Registro de cuentas nuevas, reenvío de activación con topes de frecuencia (1/min, 5/día),
/// activación con contraseña directa a Keycloak y consulta del resumen de cuenta (§9.3, §9.7).
/// </summary>
public sealed class AccountOnboardingService : IAccountOnboardingService
{
    public static readonly TimeSpan ActivationTtl = TimeSpan.FromHours(24);
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(1);
    public const int MaxResendsPerDay = 5;

    private readonly IAccountOnboardingRepository _repository;
    private readonly IKeycloakAccountProvisioner _keycloak;
    private readonly IAccountOnboardingNotifier _notifier;
    private readonly TimeProvider _clock;

    public AccountOnboardingService(
        IAccountOnboardingRepository repository,
        IKeycloakAccountProvisioner keycloak,
        IAccountOnboardingNotifier notifier,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(keycloak);
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(clock);
        _repository = repository;
        _keycloak = keycloak;
        _notifier = notifier;
        _clock = clock;
    }

    public async Task<UnitResult<BaseError>> SignupAsync(SignupInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!input.AcceptTerms)
            return UnitResult.Failure(AccountErrors.TermsMustBeAccepted());

        var companyName = input.CompanyName?.Trim();
        var fullName = input.FullName?.Trim();
        var emailNormalized = input.Email?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(companyName))
            return UnitResult.Failure(AccountErrors.InvalidSignupData("El nombre de la empresa es obligatorio."));
        if (string.IsNullOrWhiteSpace(fullName))
            return UnitResult.Failure(AccountErrors.InvalidSignupData("El nombre completo es obligatorio."));
        if (string.IsNullOrWhiteSpace(emailNormalized) || !emailNormalized.Contains('@'))
            return UnitResult.Failure(AccountErrors.InvalidSignupData("Debe indicar un correo electrónico válido."));

        var requestedPaidPlan = PlanCatalog.IsPaidPlan(input.Plan)
            ? PlanCatalog.ByKey[input.Plan.Trim()].Key
            : null;
        var billingPeriod = string.Equals(input.BillingPeriod?.Trim(), "annual", StringComparison.OrdinalIgnoreCase)
            ? "annual"
            : "monthly";

        // No revelar si el correo ya tiene cuenta activa (§9.3 paso 2).
        var existsResult = await _repository.HasActiveAccountWithEmailAsync(emailNormalized, cancellationToken);
        if (existsResult.IsFailure)
            return UnitResult.Failure(existsResult.Error);
        if (existsResult.Value)
            return UnitResult.Success<BaseError>();

        var now = _clock.GetUtcNow();

        // S1: si ya existe una activación pendiente para este correo, reutilizarla y aplicar
        // los mismos topes de frecuencia que el reenvío (en silencio, respondiendo 200 sin spam).
        var pendingResult = await _repository.FindLatestPendingByEmailAsync(emailNormalized, cancellationToken);
        if (pendingResult.IsFailure)
            return UnitResult.Failure(pendingResult.Error);

        if (pendingResult.Value.HasValue)
        {
            var existing = pendingResult.Value.Value;
            if (now - existing.LastSentAt < ResendCooldown)
                return UnitResult.Success<BaseError>();

            var today = DateOnly.FromDateTime(now.UtcDateTime);
            var nextCount = existing.ResendDay == today ? existing.ResendCountToday + 1 : 1;
            if (nextCount > MaxResendsPerDay)
                return UnitResult.Success<BaseError>();

            var rotatedToken = GenerateToken();
            var rotated = await _repository.RotateActivationTokenAsync(
                existing.Id,
                HashToken(rotatedToken),
                now,
                now.Add(ActivationTtl),
                nextCount,
                today,
                cancellationToken);
            if (rotated.IsFailure)
                return UnitResult.Failure(rotated.Error);

            await _notifier.SendActivationEmailAsync(
                existing.EmailNormalized,
                existing.FullName,
                existing.CompanyName,
                rotatedToken,
                cancellationToken);
            return UnitResult.Success<BaseError>();
        }

        var rawToken = GenerateToken();
        var tokenHash = HashToken(rawToken);

        var created = await _repository.CreatePendingSignupAsync(
            companyName,
            fullName,
            emailNormalized,
            requestedPaidPlan,
            billingPeriod,
            tokenHash,
            now,
            now.Add(ActivationTtl),
            cancellationToken);

        if (created.IsFailure)
            return UnitResult.Failure(created.Error);

        await _notifier.SendActivationEmailAsync(emailNormalized, fullName, companyName, rawToken, cancellationToken);
        return UnitResult.Success<BaseError>();
    }

    public async Task<UnitResult<BaseError>> ResendActivationAsync(string? email, CancellationToken cancellationToken)
    {
        var emailNormalized = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(emailNormalized) || !emailNormalized.Contains('@'))
            return UnitResult.Success<BaseError>();

        var pendingResult = await _repository.FindLatestPendingByEmailAsync(emailNormalized, cancellationToken);
        if (pendingResult.IsFailure)
            return UnitResult.Failure(pendingResult.Error);

        // No revelar si existe una activación pendiente para ese correo.
        if (pendingResult.Value.HasNoValue)
            return UnitResult.Success<BaseError>();

        var activation = pendingResult.Value.Value;
        var now = _clock.GetUtcNow();

        // S2: cuando aplica el límite de frecuencia, responder 200 en silencio sin reenviar,
        // idéntico a un correo inexistente para no revelar cuentas pendientes.
        if (now - activation.LastSentAt < ResendCooldown)
            return UnitResult.Success<BaseError>();

        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var nextCount = activation.ResendDay == today ? activation.ResendCountToday + 1 : 1;
        if (nextCount > MaxResendsPerDay)
            return UnitResult.Success<BaseError>();

        var rawToken = GenerateToken();
        var rotated = await _repository.RotateActivationTokenAsync(
            activation.Id,
            HashToken(rawToken),
            now,
            now.Add(ActivationTtl),
            nextCount,
            today,
            cancellationToken);

        if (rotated.IsFailure)
            return UnitResult.Failure(rotated.Error);

        await _notifier.SendActivationEmailAsync(
            activation.EmailNormalized,
            activation.FullName,
            activation.CompanyName,
            rawToken,
            cancellationToken);

        return UnitResult.Success<BaseError>();
    }

    public async Task<Result<ActivateAccountResult, BaseError>> ActivateAsync(
        string? token,
        string? password,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Result.Failure<ActivateAccountResult, BaseError>(AccountErrors.ActivationTokenInvalid());

        // S3: política de contraseña alineada con Keycloak y el frontend (mínimo 10 caracteres con letras y números).
        if (string.IsNullOrWhiteSpace(password)
            || password.Length < 10
            || !password.Any(char.IsLetter)
            || !password.Any(char.IsDigit))
        {
            return Result.Failure<ActivateAccountResult, BaseError>(
                AccountErrors.PasswordPolicyRejected("La contraseña debe tener al menos 10 caracteres, incluyendo letras y números."));
        }

        var tokenHash = HashToken(token.Trim());
        var now = _clock.GetUtcNow();

        // R8: bloquea la fila de activación antes de llamar a Keycloak para serializar dobles clics
        // y permitir reintentos seguros si falla la escritura final en la base de datos.
        // La contraseña va directo a Keycloak y nunca se guarda ni aparece en logs (§9.3 paso 3).
        return await _repository.ActivateLockedAsync(
            tokenHash,
            now,
            activation => _keycloak.CreateAccountAdminAsync(
                activation.EmailNormalized,
                activation.FullName,
                password,
                activation.AccountId,
                cancellationToken),
            cancellationToken);
    }

    public Task<Result<AccountUsageSummary, BaseError>> GetAccountSummaryAsync(
        Guid accountId,
        CancellationToken cancellationToken) =>
        _repository.GetAccountUsageSummaryAsync(accountId, _clock.GetUtcNow(), cancellationToken);

    public static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[16]; // 128 bits
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    public static byte[] HashToken(string rawToken) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
}

