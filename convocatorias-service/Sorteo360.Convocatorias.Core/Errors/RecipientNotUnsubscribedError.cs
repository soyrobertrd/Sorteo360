using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Errors;

/// <summary>
/// Se pidió sincronizar la baja de alguien que nunca se dio de baja (spec <c>07</c> §6.2).
/// <b>Se niega en vez de empujar</b>: darle de baja en el CRM a quien no lo pidió es justo el fallo
/// de cumplimiento que la columna existe para impedir, y en fase 1 nada lo deshace (spec
/// <c>06</c> §6).
/// </summary>
public sealed record RecipientNotUnsubscribedError()
    : BaseError(
        "Ese destinatario no se dio de baja: no hay nada que sincronizar.",
        ErrorCode,
        ErrorKind.Conflict)
{
    public const string ErrorCode = "RECIPIENT_NOT_UNSUBSCRIBED";
}
