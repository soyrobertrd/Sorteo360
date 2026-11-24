namespace Sorteo360.Notifications.Core.Notifications;

/// <summary>
/// Lo que este servicio sabe del webhook de eventos del proveedor. Hoy solo si existe, que es lo
/// que se estampa en cada envío al aceptarlo.
/// </summary>
public sealed class SendGridEventWebhookSettings
{
    public static string SectionName => "SendGrid:EventWebhook";

    /// <summary>
    /// Si hay webhook configurado. Un envío aceptado mientras esto es <c>false</c> no va a recibir
    /// noticias de entrega nunca, y su fila lo dice para siempre.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// La clave pública ECDSA del proveedor, en base64, con la que se verifica cada lote del
    /// webhook. Es toda la autenticación que ese endpoint tiene.
    /// <para>
    /// Vacía significa que no se verifica nada, y por eso un lote con la clave sin configurar se
    /// rechaza con 403 en vez de aceptarse: fallar cerrado es lo único que evita que una
    /// configuración a medias abra el único endpoint anónimo de terceros de la plataforma.
    /// </para>
    /// </summary>
    public string? PublicKey { get; set; }
}
