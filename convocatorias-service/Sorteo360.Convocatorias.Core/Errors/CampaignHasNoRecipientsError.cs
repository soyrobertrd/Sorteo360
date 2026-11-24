using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Errors;

/// <summary>
/// Se quiso lanzar una campaña que no tiene ni una fila de destinatario. Significa que nunca se
/// seleccionó a nadie.
/// <para>
/// Es un código distinto del de "ninguno es contactable" a propósito (spec <c>03</c> §4): son
/// dos errores distintos y la pantalla tiene que decir dos cosas distintas. Juntarlos mandaría
/// a quien coordina a buscar una lista que está ahí mismo.
/// </para>
/// <para>
/// Deja la campaña en <c>draft</c>, sin <c>launched_by</c>, sin <c>launched_at</c> y sin tocar
/// ninguna fila: un lanzamiento fallido no escribe nada, porque una campaña todavía en borrador
/// ya es el registro.
/// </para>
/// </summary>
public sealed record CampaignHasNoRecipientsError()
    : BaseError(
        "La campaña no tiene destinatarios: selecciona a alguien antes de lanzarla.",
        ErrorCode,
        ErrorKind.Invalid)
{
    public const string ErrorCode = "CAMPAIGN_HAS_NO_RECIPIENTS";
}
