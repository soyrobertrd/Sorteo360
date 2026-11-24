namespace Sorteo360.Convocatorias.Core.Campaigns;

/// <summary>
/// El veredicto del CRM sobre un destinatario al que <b>no</b> se le va a escribir, tal como
/// entra en la transacción que lanza. Es lo que el lanzamiento lleva consigo desde la
/// recomprobación hasta la escritura: dentro de esa transacción no se llama al CRM.
/// <para>
/// <b>Suprimir a alguien no es un error y no lleva código.</b> La campaña sigue para el resto y
/// quien lanza recibe su 202; lo que cuenta la supresión es la fila, no la respuesta HTTP (spec
/// <c>04</c> §3).
/// </para>
/// </summary>
/// <param name="ContactId">La identidad, que vive en el CRM.</param>
/// <param name="Reason">
/// Uno de los cuatro de <see cref="ExclusionReason"/>. El CRM manda; lo que no reconocemos, y lo
/// que ni siquiera volvió, cae en <see cref="ExclusionReason.ContactUnavailable"/>.
/// </param>
public sealed record RecipientSuppression(Guid ContactId, string Reason);
