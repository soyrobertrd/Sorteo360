using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.DTOs;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Campaigns;

/// <summary>
/// El lanzamiento, con los dos pasos del CRM en su sitio. <b>El orden es la funcionalidad</b>
/// (spec <c>04</c> §1): todo lo que puede negarse a lanzar ocurre dentro de la petición, antes
/// del 202, y después del 202 la respuesta siempre es "estamos enviando".
/// <list type="number">
/// <item>Se vacían al CRM las bajas sin sincronizar (§3.1).</item>
/// <item>Se recomprueba la contactabilidad de la lista entera (§3).</item>
/// <item>Se lee el evento: un evento cancelado y un valor de grano de evento que falta rechazan
/// el lanzamiento, y events-service caído lo rechaza también (§1 paso 4).</item>
/// <item>Se lanza: las guardas de estado y de cuenta, las supresiones, y el salto a
/// <c>sending</c>, todo dentro de una transacción.</item>
/// </list>
/// <para>
/// Vaciar va <b>primero</b> porque una baja sin sincronizar no es inocua: si el empujón nunca
/// llega, el CRM sigue contestando <c>canBeEmailed: true</c> por esa persona y la campaña
/// siguiente le escribe. Y va dentro de la petición, por delante del salto, porque es donde
/// todavía puede negarse; moverlo al worker convertiría un 503 que alguien lee en una campaña ya
/// en <c>sending</c>.
/// </para>
/// <para>
/// <b>Un corte del CRM en cualquiera de los dos pasos no entrega a nadie.</b> Escribirle a quien
/// se dio de baja entre la selección y el lanzamiento es el fallo que este orden existe para
/// impedir, y media campaña mandada contra una lista de bajas sin verificar es peor que ninguna.
/// </para>
/// <para>
/// <b>El evento se lee después del CRM y no antes.</b> Es la única guarda que hay sobre él
/// —events-service nunca llama a convocatorias, así que nada más acá puede enterarse de que se
/// canceló— y por eso es un paso del orden. Delante de los dos del CRM gastaría al vecino por
/// campañas que el CRM ya iba a rechazar.
/// </para>
/// <para>
/// Lo que no está acá, a propósito: la lectura de la ventana de registro (§1 paso 5), que llega
/// con el portal, y el worker que de verdad manda los correos.
/// </para>
/// </summary>
public interface ICampaignLaunchService
{
    /// <summary>
    /// Ejecuta el orden completo. <see cref="Maybe{T}.None"/> si esa campaña no existe.
    /// </summary>
    /// <param name="dto">Qué campaña, con cuántos destinatarios y de parte de quién.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<Result<Maybe<CampaignSummary>, BaseError>> LaunchAsync(
        LaunchCampaignDto dto,
        CancellationToken cancellationToken);
}
