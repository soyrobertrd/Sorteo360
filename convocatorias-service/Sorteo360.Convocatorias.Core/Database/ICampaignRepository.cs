using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Crm;
using Sorteo360.Convocatorias.Core.DTOs;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Convocatorias.Core.Unsubscribe;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Database;

/// <summary>
/// Las tres tablas de campañas. Es donde viven las guardas de estado, y viven acá y no en el
/// endpoint a propósito: se comprueban <b>dentro</b> de la misma transacción que escribe, así
/// que dos lanzamientos simultáneos no pueden pasar los dos por un borrador leído antes.
/// <para>
/// No hay <c>ICampaignApprovalGate</c> ni nada que se le parezca. El cliente retiró el paso de
/// aprobación (ADR-0017), y lo que quedó es una sola llamada: quien tiene
/// <c>campaigns:launch</c> lanza.
/// </para>
/// </summary>
public interface ICampaignRepository
{
    /// <summary>Las campañas, paginadas, con sus dos cuentas. Filtra por evento y por estado.</summary>
    Task<Result<PagedResult<CampaignSummary>, BaseError>> ListAsync(
        CampaignQuery query,
        CancellationToken cancellationToken);

    /// <summary>Una campaña con sus dos cuentas, o nada si ese id no existe.</summary>
    Task<Result<Maybe<CampaignSummary>, BaseError>> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Crea el borrador y devuelve su id. Falla con <c>CAMPAIGN_TEMPLATE_NOT_FOUND</c> si la
    /// plantilla no existe en ese idioma (spec <c>03</c> §4.1).
    /// </summary>
    Task<Result<Guid, BaseError>> CreateAsync(CreateCampaignDto dto, CancellationToken cancellationToken);

    /// <summary>
    /// Cambia plantilla e idioma. Falla con <c>CAMPAIGN_NOT_DRAFT</c> si la campaña ya no
    /// admite cambios, con <c>CAMPAIGN_TEMPLATE_NOT_FOUND</c> si la pareja que resulta del cambio
    /// no tiene plantilla (spec <c>03</c> §4.1), y devuelve nada si ese id no existe.
    /// </summary>
    Task<Result<Maybe<CampaignSummary>, BaseError>> UpdateAsync(
        UpdateCampaignDto dto,
        CancellationToken cancellationToken);

    /// <summary>
    /// Materializa en filas lo que devolvió el CRM: destinatario para quien se puede contactar,
    /// exclusión con su motivo para quien no. <b>Solo en borrador.</b>
    /// <para>
    /// Es aditiva e idempotente: un contacto que ya está en la campaña —como destinatario o
    /// como exclusión— se cuenta como omitido y no se vuelve a escribir. Así dos selecciones
    /// con filtros distintos componen en vez de duplicar, y una segunda selección nunca borra
    /// en silencio una lista que alguien depuró a mano con el <c>DELETE</c> de un destinatario.
    /// </para>
    /// </summary>
    Task<Result<Maybe<RecipientSelection>, BaseError>> SelectRecipientsAsync(
        Guid campaignId,
        IReadOnlyList<CrmContact> contacts,
        CancellationToken cancellationToken);

    /// <summary>
    /// Quita un destinatario de la lista. <b>Solo en borrador.</b>
    /// </summary>
    Task<Result<RemoveRecipientOutcome, BaseError>> RemoveRecipientAsync(
        Guid campaignId,
        Guid recipientId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Los contactos con una baja que todavía no llegó al CRM, <b>de todo el servicio</b> y no
    /// solo de una campaña: quien se dio de baja en la campaña de marzo tiene que quedar
    /// suprimido en la de abril, y esa fila vive bajo otra campaña (spec <c>04</c> §3.1).
    /// <para>
    /// Uno por contacto: la misma persona puede tener varias filas con la baja sin sincronizar,
    /// y el CRM solo necesita enterarse una vez.
    /// </para>
    /// </summary>
    Task<Result<IReadOnlyList<Guid>, BaseError>> ListUnsyncedUnsubscribesAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Sella como sincronizadas las bajas de esos contactos, que es lo que significa "el CRM ya
    /// lo sabe".
    /// <para>
    /// <b>Se llama una sola vez, cuando el vaciado entero llegó.</b> Un vaciado que se cortó a
    /// medias no sella nada: repetir un empujón que ya llegó es un 200 en el CRM, así que
    /// reintentarlo sale gratis, mientras que un sello sobre un empujón que nunca llegó es
    /// exactamente el fallo que este orden existe para impedir.
    /// </para>
    /// </summary>
    Task<UnitResult<BaseError>> MarkUnsubscribesSyncedAsync(
        IReadOnlyList<Guid> contactIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// La fila de destinatario cuyo token de baja tiene ese hash, o <see cref="Maybe{T}.None"/>
    /// si ninguna lo tiene. <b>Busca por hash</b>, que es lo único que la columna guarda: el
    /// texto plano no llega hasta acá (spec <c>06</c> §2.1). El índice encuentra; la comparación
    /// en tiempo fijo, del lado del servicio, acepta.
    /// </summary>
    Task<Result<Maybe<UnsubscribeTarget>, BaseError>> FindRecipientByUnsubscribeTokenAsync(
        byte[] tokenHash,
        CancellationToken cancellationToken);

    /// <summary>
    /// La baja de la fila de destinatario con ese id, o <see cref="Maybe{T}.None"/> si no existe.
    /// Es la lectura del reintento a mano de <c>07</c> §6.2, que llega por id y no por token.
    /// </summary>
    Task<Result<Maybe<RecipientUnsubscribe>, BaseError>> FindRecipientUnsubscribeAsync(
        Guid recipientId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Marca una fila como dada de baja, y opcionalmente sincronizada con el CRM.
    /// <para>
    /// <b>El sello de sincronización no viaja solo</b>: el CHECK de la tabla no admite
    /// <c>unsubscribe_synced_at</c> sin <c>unsubscribed_at</c> detrás, así que
    /// <paramref name="unsubscribedAt"/> siempre se escribe y <paramref name="syncedAt"/> solo si
    /// el empujón al CRM llegó. Un <paramref name="syncedAt"/> nulo deja la fila pendiente, que el
    /// próximo lanzamiento vacía (spec <c>06</c> §6).
    /// </para>
    /// </summary>
    Task<UnitResult<BaseError>> MarkUnsubscribedAsync(
        Guid recipientId,
        DateTimeOffset unsubscribedAt,
        DateTimeOffset? syncedAt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lo que el lanzamiento necesita saber <b>antes</b> de llamar al CRM: en qué estado está la
    /// campaña y a qué contactos pretende escribirle. <see cref="Maybe{T}.None"/> si ese id no
    /// existe.
    /// <para>
    /// Es una lectura de guarda, no la autoridad: las mismas comprobaciones vuelven a correr
    /// dentro de la transacción de <see cref="LaunchAsync"/>, que es donde tienen que correr para
    /// que dos lanzamientos simultáneos no pasen los dos por un borrador que dejó de serlo. Lo que
    /// añade acá es el orden de la spec <c>04</c> §1: validar es el paso 1 y el CRM son el 2 y el
    /// 3, así que una cuenta rancia se rechaza sin haberle empujado nada al CRM.
    /// </para>
    /// </summary>
    Task<Result<Maybe<CampaignLaunchPreflight>, BaseError>> ReadLaunchPreflightAsync(
        Guid campaignId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lanza: de <c>draft</c> a <c>sending</c>, estampando quién y cuándo. Rechaza, dejando la
    /// campaña intacta, si no hay destinatarios, si ninguno se puede contactar, o si la cuenta
    /// que trae quien lanza ya no es la real (spec <c>03</c> §4 y §5.1).
    /// <para>
    /// <paramref name="suppressions"/> es lo que el CRM acaba de contestar sobre la lista (spec
    /// <c>04</c> §3). Se aplica <b>dentro</b> de esta transacción, y solo si el lanzamiento
    /// procede: un rechazo no deja una fila suprimida bajo una campaña que sigue en borrador.
    /// El CRM no se llama desde acá, ni se vuelve a razonar sobre su respuesta.
    /// </para>
    /// <para>
    /// <paramref name="eventValues"/> son los tres valores del evento que el paso 4 acaba de
    /// leer, y se copian en la fila acá dentro. Es el único momento en que se pueden guardar: el
    /// worker rinde después del <c>202</c>, con la petición ya cerrada, y no vuelve a llamar a
    /// events-service (spec <c>04</c> §1 paso 7). Nulos en un mensaje directo, que no tiene evento:
    /// las columnas se quedan vacías y el worker arma con el asunto y el texto de la fila.
    /// </para>
    /// <para>
    /// <paramref name="senderEmail"/> es la dirección de envío de la OPT de quien lanza
    /// (PGTI-168), copiada por la misma razón que el evento: el worker no vuelve a llamar al CRM.
    /// Nula cuando la campaña sale desde la dirección general.
    /// </para>
    /// <para>
    /// <c>sent_at</c> se queda nulo: lo estampa el worker cuando la última fila llega a
    /// terminal (spec <c>04</c> §1), nunca esta llamada.
    /// </para>
    /// </summary>
    Task<Result<Maybe<CampaignSummary>, BaseError>> LaunchAsync(
        LaunchCampaignDto dto,
        IReadOnlyList<RecipientSuppression> suppressions,
        CampaignEventValues? eventValues,
        string? senderEmail,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lo que el worker necesita de una campaña que salió del canal: su estado y la cola de filas
    /// que siguen en <c>pending</c>. <see cref="Maybe{T}.None"/> si ese id no existe.
    /// <para>
    /// El estado viene con la lectura porque cancelar en pleno envío está permitido (spec
    /// <c>03</c> §2): entre el 202 y el momento en que el worker llega a esta campaña, alguien
    /// pudo haberla parado.
    /// </para>
    /// </summary>
    Task<Result<Maybe<CampaignHandoff>, BaseError>> ReadHandoffAsync(
        Guid campaignId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Solo el estado de una campaña. <see cref="Maybe{T}.None"/> si ese id no existe.
    /// <para>
    /// Existe para el worker, que lo vuelve a mirar entre lote y lote: cancelar en pleno envío
    /// está permitido (spec <c>03</c> §2) y una lista de tres mil son tres llamadas, así que leer
    /// el estado una sola vez al empezar dejaría salir dos mil correos de una campaña que alguien
    /// ya paró.
    /// </para>
    /// </summary>
    Task<Result<Maybe<string>, BaseError>> ReadStateAsync(Guid campaignId, CancellationToken cancellationToken);

    /// <summary>
    /// Anota lo que el transporte contestó por cada persona del lote: <c>handed_off</c> la
    /// aceptada, <c>failed</c> con su motivo la rechazada (spec <c>04</c> §2.1).
    /// <para>
    /// <b>Solo mueve filas que siguen en <c>pending</c>.</b> Una fila ya terminal es historia de
    /// una entrega, y reescribirla la borraría; es también lo que hace inofensivo que un lote se
    /// anote dos veces.
    /// </para>
    /// <para>
    /// <paramref name="tokenHashes"/> viaja en la misma escritura y no en una anterior: el porqué,
    /// en <see cref="RecipientTokenHashes"/>. Solo se guardan los de las filas que llegan a
    /// <c>handed_off</c>, y nunca se pisa un hash que ya esté puesto.
    /// </para>
    /// </summary>
    Task<UnitResult<BaseError>> RecordHandoffAsync(
        Guid campaignId,
        IReadOnlyList<RecipientHandoffOutcome> outcomes,
        IReadOnlyList<RecipientTokenHashes> tokenHashes,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lleva la campaña a <c>sent</c> y le estampa <c>sent_at</c>, <b>si y solo si no le queda
    /// ninguna fila en <c>pending</c></b> (spec <c>04</c> §1 paso 8).
    /// <para>
    /// Que no quede ninguna pendiente es la única condición, así que una campaña cuyo resto eran
    /// supresiones llega igual, sin que nadie tenga que contar aparte. Y el salto pasa por la
    /// tabla de transiciones: una campaña cancelada en pleno envío no acaba en <c>sent</c> por
    /// haber terminado sus lotes. Las dos condiciones viajan <b>dentro</b> del propio
    /// <c>UPDATE</c>, así que una cancelación que entre entre la lectura y la escritura gana en
    /// vez de quedar pisada: acá no hay nadie mirando un código de estado que lo delate.
    /// </para>
    /// </summary>
    Task<UnitResult<BaseError>> CompleteHandoffAsync(Guid campaignId, CancellationToken cancellationToken);

    /// <summary>
    /// Deja lista una campaña para que alguien le vuelva a mandar <b>la cola sin entregar</b>
    /// (spec <c>04</c> §5). No toca ninguna fila: la cola ya es lo que está en <c>pending</c>, y
    /// eso es exactamente lo que el worker vuelve a recorrer.
    /// <para>
    /// Lo único que hace es cobrar la guarda de estado y devolver la campaña: fuera de
    /// <c>sending</c> es <see cref="CampaignNotSendingError"/>, y un id que no existe es
    /// <see cref="Maybe{T}.None"/>.
    /// </para>
    /// <para>
    /// <b>Esa guarda es una lectura y no viaja dentro de una escritura</b>, a diferencia del resto
    /// de las de esta interfaz. Puede hacerlo porque no escribe nada: si alguien cancela entre esta
    /// lectura y el momento en que el worker llega a la campaña, quien lo caza es el propio worker,
    /// que vuelve a mirar el estado antes de cada lote (ver <see cref="CampaignHandoff"/>). Lo peor
    /// que puede pasar acá es contestar <c>202</c> a un id que el worker luego descarta.
    /// </para>
    /// </summary>
    Task<Result<Maybe<CampaignSummary>, BaseError>> ResumeHandoffAsync(
        Guid campaignId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Devuelve a <c>pending</c> las filas que el transporte rechazó, para que la siguiente vuelta
    /// del worker las vuelva a mandar (spec <c>04</c> §5).
    /// <para>
    /// <b>Se hace acá y no en el worker</b> a propósito: su cola es lo que está en <c>pending</c> y
    /// nada más, y enseñarle una segunda idea de "qué hay que mandar" es cómo el cursor de
    /// reanudación deja de ser una sola cosa.
    /// </para>
    /// <para>
    /// Una fila <c>failed</c> no guarda ningún hash —solo se guardan los de las que llegan a
    /// <c>handed_off</c>—, así que volver a <c>pending</c> la deja acuñable otra vez y no rompe
    /// ningún enlace que esté en la bandeja de entrada de nadie.
    /// </para>
    /// </summary>
    Task<Result<Maybe<CampaignSummary>, BaseError>> RetryFailedRecipientsAsync(
        Guid campaignId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Cancela, desde <c>draft</c> o desde <c>sending</c> y de ningún otro sitio. Las filas sin
    /// tocar se quedan <c>pending</c> en vez de reescribirse: una fila <c>pending</c> bajo una
    /// campaña cancelada se lee correctamente como "pretendíamos escribirle y paramos".
    /// </summary>
    Task<Result<Maybe<CampaignSummary>, BaseError>> CancelAsync(
        Guid id,
        Guid cancelledById,
        CancellationToken cancellationToken);
}
