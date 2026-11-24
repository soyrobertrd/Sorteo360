namespace Sorteo360.Convocatorias.Core.Campaigns;

/// <summary>
/// Por dónde le llega al worker una campaña que alguien acaba de lanzar (spec <c>04</c> §1.1,
/// ADR-0018). Es un canal en memoria de este proceso, no una cola durable.
/// <para>
/// <b><see cref="Enqueue"/> no recibe token de cancelación, y es a propósito.</b> Quien encola es
/// un manejador de Minimal API, y ahí un parámetro <c>CancellationToken</c> se enlaza a
/// <c>HttpContext.RequestAborted</c>: pasarlo hacia adentro haría que el envío muriera en cuanto
/// alguien cierra la pestaña, que es justo el caso que ADR-0018 existe para arreglar. Sin
/// parámetro no hay forma de equivocarse.
/// </para>
/// <para>
/// <b>La durabilidad no mejora y el hueco es deliberado.</b> Un redespliegue en pleno envío se
/// lleva el canal con el proceso y deja la campaña en <c>sending</c> con filas pendientes. Eso ya
/// pasaba con el camino síncrono; quien lo cierra es una persona, por <c>resume-handoff</c>
/// (§5), y <c>strandedCampaigns</c> (<c>07</c> §6) es lo que se la pone delante.
/// </para>
/// </summary>
public interface ICampaignHandoffQueue
{
    /// <summary>
    /// Anota que esta campaña tiene lotes que entregar. Vuelve enseguida: lo que sigue ocurre
    /// después de la respuesta.
    /// </summary>
    void Enqueue(Guid campaignId);

    /// <summary>
    /// Las campañas encoladas, en orden de llegada. <b>Se queda esperando mientras el canal está
    /// vacío</b>, que es lo que hace que el worker no sea un temporizador ni un sondeo: no hay
    /// intervalo, ni barrido al arrancar.
    /// </summary>
    /// <param name="cancellationToken">
    /// El del ciclo de vida del host, nunca el de una petición.
    /// </param>
    IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken);
}
