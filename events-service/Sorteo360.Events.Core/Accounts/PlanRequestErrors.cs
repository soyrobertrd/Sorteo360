using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Accounts;

public static class PlanRequestErrors
{
    public static BaseError PendingRequestExists() => new(
        "La cuenta ya tiene una solicitud de plan pendiente.",
        "PLAN_REQUEST_PENDING",
        ErrorKind.Conflict);

    public static BaseError InvalidRequestedPlan() => new(
        "El plan solicitado debe ser un plan de pago válido (basic, pro o advanced).",
        "PLAN_REQUEST_INVALID_PLAN",
        ErrorKind.Invalid);

    public static BaseError NotPending() => new(
        "Solo se puede operar sobre una solicitud en estado pendiente.",
        "PLAN_REQUEST_NOT_PENDING",
        ErrorKind.Conflict);

    public static BaseError RejectionReasonRequired() => new(
        "Debe indicar un motivo para rechazar la solicitud de plan.",
        "PLAN_REQUEST_REJECTION_REASON_REQUIRED",
        ErrorKind.Invalid);

    public static BaseError InvalidPaidPeriod() => new(
        "La fecha de fin del período pagado debe ser posterior a la fecha de inicio.",
        "PLAN_REQUEST_INVALID_PERIOD",
        ErrorKind.Invalid);

    public static BaseError NotFound() => new(
        "La solicitud de plan no existe.",
        "PLAN_REQUEST_NOT_FOUND",
        ErrorKind.NotFound);

    public static BaseError AccountNotFound() => new(
        "La cuenta no existe.",
        "ACCOUNT_NOT_FOUND",
        ErrorKind.NotFound);
}

