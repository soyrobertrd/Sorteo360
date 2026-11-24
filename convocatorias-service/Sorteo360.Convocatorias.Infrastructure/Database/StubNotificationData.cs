namespace Sorteo360.Convocatorias.Infrastructure.Database;

/// <summary>
/// Fila de <c>stub_notification</c>: un correo que habría salido, guardado tal como lo guardaría
/// notification-service (spec <c>04</c> §4.1). Una fila por destinatario, con el cuerpo ya
/// sustituido.
/// <para>
/// <b>Es temporal y se porta como tal.</b> No tiene clave ajena a <c>campaign_recipient</c> ni
/// guarda el id de la campaña: lo que hay acá es lo que tendría una fila del historial de
/// notificaciones, nada de nuestro modelo. Atarla a nuestras tablas es exactamente cómo una tabla
/// provisional se convierte en esquema sin que nadie lo decida, y la migración que traiga el
/// cliente real la borra (§4.2).
/// </para>
/// <para>
/// Se descartaron las dos alternativas. Solo log: un test de integración no puede afirmar sobre
/// una línea de log. Archivos <c>.eml</c>: necesitan un volumen con nombre, y los bytes sin
/// volumen en el archivo de Dokploy viven en la capa de escritura del contenedor y desaparecen en
/// el siguiente despliegue.
/// </para>
/// </summary>
internal sealed class StubNotificationData
{
    public Guid Id { get; set; }

    /// <summary>El lote del que salió. Es lo que devuelve la respuesta como <c>batchId</c>.</summary>
    public Guid BatchId { get; set; }

    public required string RecipientEmail { get; set; }

    /// <summary>La etiqueta del tipo, verbatim. Ver <c>CampaignNotification.Type</c>.</summary>
    public required string Type { get; set; }

    public required string Language { get; set; }

    /// <summary>El asunto ya sustituido, sin escape: es una cabecera, no marcado.</summary>
    public required string Subject { get; set; }

    /// <summary>
    /// El cuerpo ya sustituido, que es el punto entero de la tabla: sin él no hay nada que mirar
    /// ni enlace que pulsar.
    /// <para>
    /// <b>Acá vive el token de registro en claro</b>, porque el cuerpo lleva
    /// <c>{{enlace_registro}}</c> y la spec <c>06</c> solo guarda su hash. Aceptado con su salida
    /// fijada en §4.2: un cuerpo censurado no se puede pulsar, lo que se llevaría por delante la
    /// demo y la única prueba de punta a punta del enlace de registro; stage no tiene datos reales
    /// de invitados; y stage se pasa a <c>http</c> el día que notification-service exista.
    /// </para>
    /// </summary>
    public required string BodyHtml { get; set; }

    /// <summary>
    /// El desenlace de esa persona, del vocabulario del contrato y no del nuestro: ver
    /// <c>CampaignNotification.AllStatuses</c>. Lo que pasa con la fila de la campaña se decide
    /// mirando esto, en el otro lado de la costura.
    /// </summary>
    public required string Status { get; set; }

    /// <summary>El motivo del rechazo. Nulo cuando se aceptó.</summary>
    public string? FailureReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
