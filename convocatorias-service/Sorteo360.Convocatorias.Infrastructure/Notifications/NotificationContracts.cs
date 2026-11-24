namespace Sorteo360.Convocatorias.Infrastructure.Notifications;

/// <summary>
/// El cuerpo de <c>POST /v1/notifications</c>, tal como lo fija la spec <c>04</c> §2.
/// <para>
/// <b>Un cuerpo y una bolsa por persona, separados y no mezclados.</b> Es lo que mantiene el
/// nombre del evento fuera del cable mil veces, y lo que hace que una clave repetida sea un
/// <c>400</c> y nunca una regla de precedencia: ver <c>NotificationBatchContract</c>.
/// </para>
/// <para>
/// <b>El registro de tipos no se construyó.</b> La decisión cerrada del mapa de notificaciones
/// descansaba en que "el servicio que rinde es el que declara lo que necesita", y una plantilla
/// que manda quien llama rompe esa premisa: por eso <paramref name="Type"/> es una etiqueta de
/// historial y nada más, y un solo endpoint sirve la campaña y lo transaccional.
/// </para>
/// </summary>
/// <param name="Type">La etiqueta del tipo, guardada tal cual. No valida, no enruta, no elige plantilla.</param>
/// <param name="Language">El idioma, que dicta el evento.</param>
/// <param name="FromAddress">Quién firma. El emisor no averigua qué oficina manda: se lo decimos.</param>
/// <param name="FromName">El nombre visible del remitente, si hay uno configurado.</param>
/// <param name="Subject">El asunto, con sus marcadores sin sustituir.</param>
/// <param name="Body">El correo completo en HTML, con sus marcadores sin sustituir.</param>
/// <param name="SharedData">Lo que vale para todo el lote.</param>
/// <param name="Recipients">De 1 a 1000 personas.</param>
/// <param name="About">Sobre qué fue este correo. Se indexa y nunca sale al proveedor.</param>
/// <param name="IdempotencyKey">
/// Qué envío es este, derivado de una identidad que ya existe y nunca aleatorio (spec <c>01</c>
/// §3). Ver <c>NotificationIdempotencyKey</c>.
/// </param>
public sealed record NotificationBatchRequest(
    string Type,
    string Language,
    string FromAddress,
    string? FromName,
    string Subject,
    string Body,
    IReadOnlyDictionary<string, string> SharedData,
    IReadOnlyList<NotificationRecipientRequest> Recipients,
    IReadOnlyList<NotificationAboutRequest> About,
    string IdempotencyKey);

/// <summary>
/// Una persona del lote: a dónde va su correo y qué cambia en él.
/// <para>
/// <b>La dirección es lo único que la identifica en el cable</b>, y no es nuestra identidad de
/// fila: ver el emparejado en <c>HttpCampaignSendGateway</c>.
/// </para>
/// </summary>
/// <param name="Email">La dirección.</param>
/// <param name="Name">El nombre al que va dirigido, o null. Va en la cabecera <c>To</c> y en el historial.</param>
/// <param name="Data">Solo lo suyo. Los dos enlaces llevan un token vivo.</param>
public sealed record NotificationRecipientRequest(
    string Email,
    string? Name,
    IReadOnlyDictionary<string, string> Data);

/// <summary>
/// Sobre qué fue este correo, que es la pregunta que el cliente le hará al historial: enseñame
/// todo lo que salió sobre la Feria de Madrid.
/// <para>
/// <b>Se llama <c>about</c> y no <c>subjects</c></b> a propósito: la petición ya lleva
/// <c>subject</c>, la línea de asunto, y dos campos a una letra de distancia que significan cosas
/// distintas son una trampa en la que alguien cae antes de un mes (spec <c>04</c> §2).
/// </para>
/// </summary>
/// <param name="SourceService">Quién lo mandó.</param>
/// <param name="EntityType">De qué clase es la cosa sobre la que fue.</param>
/// <param name="EntityId">Cuál.</param>
public sealed record NotificationAboutRequest(
    string SourceService,
    string EntityType,
    string EntityId);

/// <summary>
/// El <c>202</c> de notification-service: el id del lote y una entrada por destinatario (spec
/// <c>04</c> §2.1).
/// <para>
/// <b>El 202 es la línea.</b> Después de él existe una fila de historial del otro lado; antes, no
/// existe nada, así que un <c>400</c> no anota a nadie.
/// </para>
/// </summary>
/// <param name="BatchId">El lote, tal como quedó en el historial del emisor.</param>
/// <param name="Results">Una entrada por destinatario, en el orden en que se mandaron.</param>
public sealed record NotificationBatchResponse(
    Guid BatchId,
    IReadOnlyList<NotificationResultResponse> Results);

/// <summary>
/// Qué pasó con una persona del lote.
/// <para>
/// <c>accepted</c> es que el emisor persistió la fila y la va a intentar, <b>no</b> que el
/// proveedor la tomara: detrás hay un segundo worker drenando SMTP. <c>rejected</c> es terminal
/// para esa persona, y al aceptar solo existe un motivo posible, una dirección mal formada.
/// </para>
/// </summary>
/// <param name="Id">La fila de historial, nula en un rechazo.</param>
/// <param name="Recipient">La dirección por la que se contesta.</param>
/// <param name="Status">Ver <c>CampaignNotification</c>.</param>
/// <param name="Reason">El motivo del rechazo, nulo cuando se aceptó.</param>
public sealed record NotificationResultResponse(
    string? Id,
    string Recipient,
    string Status,
    string? Reason);

/// <summary>
/// La página de <c>GET /v1/notifications</c>, de la que la lectura de la entrega solo mira las
/// filas (PGTI-177). Lo demás de la página no le hace falta: se pide por id.
/// </summary>
/// <param name="Items">Las filas que el alcance de la cuenta de servicio deja ver.</param>
public sealed record NotificationDeliveryPage(IReadOnlyList<NotificationDeliveryItem> Items);

/// <summary>Una fila del historial, recortada a los dos ejes de la entrega.</summary>
/// <param name="Id">El id del mensaje que devolvió el 202.</param>
/// <param name="DeliveryStatus">Hasta dónde llegó el correo, ya topado por el seguimiento.</param>
/// <param name="RecipientAction">Lo que hizo la persona: <c>none</c>, <c>unsubscribed</c> o <c>complained</c>.</param>
public sealed record NotificationDeliveryItem(Guid Id, string DeliveryStatus, string RecipientAction);
