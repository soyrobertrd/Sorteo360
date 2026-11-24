using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Accounts;

/// <summary>Errores de dominio con <c>code</c> de plataforma para cuentas, activación y topes de plan (§9.4).</summary>
public static class AccountErrors
{
    private sealed record ExtendedError : BaseError
    {
        private readonly IReadOnlyDictionary<string, object?> _extensions;

        public ExtendedError(string message, string code, ErrorKind kind, IReadOnlyDictionary<string, object?> extensions)
            : base(message, code, kind)
        {
            _extensions = extensions;
        }

        public override IReadOnlyDictionary<string, object?> ProblemExtensions => _extensions;
    }

    public static BaseError PlanParticipantLimit(int limit, int remaining) => new ExtendedError(
        $"Tu plan permite hasta {limit} participantes por sorteo. Te quedan {Math.Max(0, remaining)} espacios disponibles.",
        "PLAN_PARTICIPANT_LIMIT",
        ErrorKind.Conflict,
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["limit"] = limit,
            ["remaining"] = Math.Max(0, remaining),
        });

    public static BaseError PlanActiveRaffleLimit(int limit) => new ExtendedError(
        $"Tu plan permite hasta {limit} sorteos activos a la vez.",
        "PLAN_ACTIVE_RAFFLE_LIMIT",
        ErrorKind.Conflict,
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["limit"] = limit,
        });

    public static BaseError PlanMonthlyRaffleLimit(int limit, DateTimeOffset resetsAt) => new ExtendedError(
        $"Tu plan permite hasta {limit} sorteo creado al mes. El cupo se renueva el {resetsAt:yyyy-MM-dd}.",
        "PLAN_MONTHLY_RAFFLE_LIMIT",
        ErrorKind.Conflict,
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["limit"] = limit,
            ["resetsAt"] = resetsAt,
        });

    public static BaseError PlanRoundLimit(int limit, int remaining = 0) => new ExtendedError(
        $"Tu plan permite hasta {limit} rondas por sorteo.",
        "PLAN_ROUND_LIMIT",
        ErrorKind.Conflict,
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["limit"] = limit,
            ["remaining"] = Math.Max(0, remaining),
        });

    public static BaseError PlanPrizeLimit(int limit, int remaining = 0) => new ExtendedError(
        $"Tu plan permite hasta {limit} premios por sorteo.",
        "PLAN_PRIZE_LIMIT",
        ErrorKind.Conflict,
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["limit"] = limit,
            ["remaining"] = Math.Max(0, remaining),
        });

    public static BaseError PlanUserLimit(int limit) => new(
        $"Tu plan permite hasta {limit} usuarios.",
        "PLAN_USER_LIMIT",
        ErrorKind.Conflict);

    public static BaseError FeatureNotIncluded(string feature) => new(
        $"Tu plan actual no incluye la función '{feature}'.",
        "PLAN_FEATURE_NOT_INCLUDED",
        ErrorKind.Forbidden);

    public static BaseError AccountNotActive() => new(
        "La cuenta no está activa o se encuentra suspendida.",
        "ACCOUNT_NOT_ACTIVE",
        ErrorKind.Forbidden);

    public static BaseError EmailTaken() => new(
        "Ese correo ya pertenece a una cuenta activa.",
        "ACCOUNT_EMAIL_TAKEN",
        ErrorKind.Conflict);

    public static BaseError ActivationTokenInvalid() => new(
        "El enlace de activación es inválido, ya fue usado o expiró.",
        "ACTIVATION_TOKEN_INVALID",
        ErrorKind.Conflict);

    public static BaseError PasswordPolicyRejected(string message) => new(
        string.IsNullOrWhiteSpace(message) ? "La contraseña no cumple la política de seguridad." : message,
        "PASSWORD_POLICY_REJECTED",
        ErrorKind.Invalid);

    public static BaseError ResendRateLimited() => new(
        "Debes esperar antes de volver a solicitar el correo de activación.",
        "ACTIVATION_RESEND_RATE_LIMITED",
        ErrorKind.TooManyRequests);

    public static BaseError TermsMustBeAccepted() => new(
        "Debe aceptar los términos y la política de privacidad para crear una cuenta.",
        "SIGNUP_TERMS_REQUIRED",
        ErrorKind.Invalid);

    public static BaseError InvalidSignupData(string message) => new(
        message,
        "SIGNUP_INVALID",
        ErrorKind.Invalid);
}
