namespace Sorteo360.Notifications.Web.WebhookEndpoints;

/// <summary>
/// Los textos del documento OpenAPI del webhook. Viven acá y no en línea para que
/// <c>DefineEndpoints</c> siga siendo una tabla de rutas que se lee de un golpe.
/// </summary>
internal static class SendGridEventWebhookEndpointDescriptions
{
    public const string ReceiveSummary = "Recibe un lote de eventos de entrega del proveedor de correo.";

    public const string ReceiveDescription =
        "Verifica la firma ECDSA del proveedor sobre el cuerpo crudo y, solo después, parsea y "
        + "guarda cada evento. **No lleva autenticación de la plataforma**: quien llama es el "
        + "proveedor, que no es un usuario del realm, y la firma es toda la autenticación que este "
        + "endpoint tiene. Responde 403 ante una firma inválida, una marca de tiempo fuera de la "
        + "ventana de diez minutos o una clave pública sin configurar; 413 sobre los 2 MB; 400 ante "
        + "un lote firmado pero mal formado.";
}
