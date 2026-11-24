namespace Sorteo360.Convocatorias.Core.Campaigns;

/// <summary>
/// Un lote de correos de una campaña, tal como se le pide al transporte (spec <c>04</c> §2).
/// <b>Viene ya troceado</b>: partir en mil es trabajo de quien llama, no del transporte.
/// <para>
/// <b>Convocatorias arma, no sustituye.</b> El asunto y el cuerpo viajan con sus marcadores
/// intactos, los valores del evento en una sola bolsa compartida y lo de cada persona en la suya.
/// Separar en vez de mezclar es lo que mantiene el nombre del evento fuera del cable mil veces, y
/// sustituir es trabajo del lado que recibe.
/// </para>
/// <para>
/// Por eso una clave no puede estar en las dos bolsas: no hay motivo legítimo para mandarla dos
/// veces, y un ganador silencioso es un generador de fallos.
/// </para>
/// <para>
/// <b>Esto no es todavía el cuerpo del cable.</b> Al contrato de la spec <c>04</c> §2 le faltan
/// <c>about</c> y <c>idempotencyKey</c>, y llegan con el cliente tipado que los pone en el JSON:
/// <see cref="CampaignId"/> y <see cref="EventId"/> son exactamente las dos entradas con las que se
/// arma <c>about</c>, que es por qué el id del evento viaja desde ya. El <c>type</c> no viaja acá
/// porque no cambia entre lotes: lo pone el transporte desde
/// <see cref="CampaignNotification.Type"/>.
/// </para>
/// </summary>
/// <param name="CampaignId">De qué campaña es el lote.</param>
/// <param name="EventId">
/// Qué evento se convoca. Es la otra mitad de "sobre qué fue este correo", y falta en un mensaje
/// directo, que no convoca a nada.
/// </param>
/// <param name="Language">El idioma, que dicta el evento.</param>
/// <param name="Subject">El asunto, con sus marcadores sin sustituir.</param>
/// <param name="BodyHtml">El correo completo, arte incluido, con sus marcadores sin sustituir.</param>
/// <param name="SharedData">
/// Lo que vale para todo el lote: <c>evento</c>, <c>lugar</c> y <c>fecha</c>, o <c>asunto</c> y
/// <c>mensaje</c> en un mensaje directo.
/// </param>
/// <param name="Recipients">A quiénes va, de 1 a 1000.</param>
/// <param name="FromAddress">
/// La dirección que firma el lote: la de envío de la OPT que lanzó la campaña (PGTI-168). Nula
/// para que el transporte firme con la dirección general, <c>Notifications__FromAddress</c>.
/// </param>
public sealed record SendBatchRequest(
    Guid CampaignId,
    Guid? EventId,
    string Language,
    string Subject,
    string BodyHtml,
    IReadOnlyDictionary<string, string> SharedData,
    IReadOnlyList<SendBatchRecipient> Recipients,
    string? FromAddress = null);

/// <summary>
/// Una persona del lote: a dónde va su correo y qué cambia en él.
/// </summary>
/// <param name="RecipientId">
/// Por donde vuelve el desenlace. No la dirección: dos personas pueden compartirla.
/// </param>
/// <param name="Email">La dirección, tal como se copió al seleccionarla.</param>
/// <param name="Name">
/// Nombre y apellido, para la cabecera <c>To</c> y el historial del emisor; null si la fila no
/// trae ninguno. Ver <see cref="Notifications.RecipientDisplayName"/>.
/// </param>
/// <param name="Data">
/// Solo lo suyo: <c>nombre</c>, <c>enlace_registro</c> y <c>enlace_baja</c>. Los dos enlaces llevan
/// un token vivo, que es por qué nada de esta bolsa se guarda en ningún sitio.
/// </param>
public sealed record SendBatchRecipient(
    Guid RecipientId,
    string Email,
    string? Name,
    IReadOnlyDictionary<string, string> Data);
