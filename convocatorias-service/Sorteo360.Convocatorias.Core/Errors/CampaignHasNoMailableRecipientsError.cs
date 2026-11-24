using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Errors;

/// <summary>
/// La campaña sí tiene destinatarios y ninguno se puede contactar. Es una cosa real y alarmante
/// que decirle a quien lanza —los 240 que seleccionaste están todos dados de baja— y por eso no
/// comparte código con "no seleccionaste a nadie" (spec <c>03</c> §4).
/// <para>
/// <b>Una campaña que no escribiría a nadie se rechaza en vez de registrarse.</b> Esto invierte
/// lo que decía la spec de la época de la aprobación, que lo dejaba completar porque la gente
/// se había dado de baja durante la ventana de aprobación. Ya no hay ventana de aprobación: la
/// misma condición ahora significa que alguien está por publicar una campaña a una sala vacía.
/// </para>
/// </summary>
public sealed record CampaignHasNoMailableRecipientsError()
    : BaseError(
        "Ningún destinatario de la campaña se puede contactar: revisa las bajas antes de lanzarla.",
        ErrorCode,
        ErrorKind.Invalid)
{
    public const string ErrorCode = "CAMPAIGN_HAS_NO_MAILABLE_RECIPIENTS";
}
