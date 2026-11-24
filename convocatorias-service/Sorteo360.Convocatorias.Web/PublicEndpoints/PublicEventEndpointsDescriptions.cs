namespace Sorteo360.Convocatorias.Web.PublicEndpoints;

/// <summary>Textos del documento OpenAPI para las rutas públicas de eventos.</summary>
internal static class PublicEventEndpointsDescriptions
{
    public const string ListSummary = "Lista los eventos publicados";

    public const string ListDescription =
        """
        Anónima. Devuelve los eventos marcados como `public` en events-service que todavía no
        terminaron: `planning` e `in_progress`. Un evento cancelado no aparece.

        Es una vista recortada y no un proxy: el presupuesto, el costo total, los invitados
        previstos y el contacto de la OPT se quedan dentro de events-service, que nunca se alcanza
        desde internet.

        Sin caché, a propósito: una lista cacheada es una decisión de visibilidad cacheada. Si
        events-service no contesta, esto responde `503 EVENTS_UNAVAILABLE`.
        """;

    public const string GetSummary = "Devuelve un evento publicado";

    public const string GetDescription =
        """
        Anónima. Sirve además el evento ya `closed`, para que un enlace compartido la semana
        pasada siga resolviendo después de que el evento termine.

        Un id desconocido, un evento privado y uno cancelado responden todos `404` con el mismo
        código: decir cuál de los tres es le confirmaría a un desconocido que el evento existe.

        `organizer` es el nombre de la OPT resuelto contra el CRM y puede venir nulo si ese
        directorio estaba caído; la página omite el bloque y sigue sirviendo.
        """;
}
