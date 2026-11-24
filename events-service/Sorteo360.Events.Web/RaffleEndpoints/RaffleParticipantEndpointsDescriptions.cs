namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>Textos en español de OpenAPI para las rutas del padrón de participantes y webhooks QR.</summary>
internal static class RaffleParticipantEndpointsDescriptions
{
    public const string ListSummary = "Lista los participantes registrados en el padrón del sorteo";

    public const string ImportSummary = "Importa o actualiza participantes en el padrón del sorteo";
    public const string ImportDescription =
        """
        Inserta o actualiza los participantes enviados en JSON desde Excel, CSV, TXT, captura manual
        o sincronización API. Toma `employeeCode` como identidad principal y `email` cuando no viene código.
        Si `replaceExisting` es verdadero, reemplaza todo el padrón previo del evento.
        """;

    public const string CheckInSummary = "Registra o revierte el check-in de un participante";
    public const string CheckInDescription =
        """
        Busca al participante por ID, token QR, código de empleado o correo electrónico normalizado
        y actualiza su estado `checkedIn`. Devuelve 404 con `REGISTRATION_NOT_FOUND` si no existe.
        """;

    public const string BulkCheckInSummary = "Aplica el estado de check-in a todo el padrón del evento";
    public const string BulkCheckInDescription =
        """
        Marca o retira el check-in de todos los participantes actualmente guardados en el padrón del evento.
        """;

    public const string RemoveSummary = "Quita un participante del padrón del evento";
    public const string RemoveDescription =
        """
        Elimina del padrón del evento al participante que coincida con el correo o código recibido.
        """;

    public const string QrPngSummary = "Genera la imagen PNG del código QR de un participante";

    public const string BadgesPdfSummary = "Genera un PDF con gafetes QR para los participantes del sorteo";

    public const string AuditSummary = "Exporta el snapshot verificable del bombo y la bitácora hash de sorteos";
    public const string AuditDescription =
        """
        Devuelve el paquete de contingencia y verificación criptográfica del sorteo con `drumHash`
        (SHA-256 determinista de los participantes calificados) y `drawChainHash` (cadena SHA-256 de
        todos los sorteos registrados en orden cronológico).
        """;

    public const string WebhookImportSummary = "Recibe participantes externos vía webhook autenticado por clave y firma";
    public const string WebhookImportDescription =
        """
        Ruta anónima pensada para integraciones de RR. HH. o CRM externos; no usa token de usuario.
        Valida la clave en `X-Webhook-Key`, `API-key` o `X-Webhook-Secret` antes de leer el cuerpo,
        y verifica la firma HMAC-SHA256 `X-Webhook-Signature` (`t=<epoch>,v1=<hex>`, tolerancia 5 min)
        cuando el evento o servidor tiene secreto configurado.
        """;

    public const string WebhookCheckInSummary = "Registra check-in QR vía webhook autenticado por clave y firma";
    public const string WebhookCheckInDescription =
        """
        Ruta anónima para sistemas de acreditación en puerta; no usa token de usuario.
        Valida la clave y la firma HMAC con tolerancia de 5 minutos antes de procesar el check-in.
        """;
}

