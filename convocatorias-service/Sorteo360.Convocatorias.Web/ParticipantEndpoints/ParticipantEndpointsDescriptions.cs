namespace Sorteo360.Convocatorias.Web.ParticipantEndpoints;

/// <summary>
/// Textos del documento OpenAPI para la lista de participantes. No se nombra ningún estado HTTP ni
/// ningún rol: salen de <c>.Produces</c> y de la política, y repetirlos acá los desincronizaría.
/// </summary>
internal static class ParticipantEndpointsDescriptions
{
    public const string ListSummary = "Lista quién está en un evento: invitados, inscritos y quién falta";

    public const string ListDescription =
        """
        Una fila por persona y por evento: cada destinatario de cada campaña, en cualquier estado,
        más cada inscripción, unidos por la dirección recortada y en minúsculas. Una persona
        invitada por dos campañas es una fila. Quien fue invitado en una dirección y se inscribió
        con otra son dos filas: la dirección es la persona.

        Los destinatarios de un borrador solo aparecen al filtrar por `campaignId`.

        Orden por apellido, nombre y dirección. Un evento que este servicio no conoce devuelve una
        página vacía: convocatorias no sabe qué eventos existen.
        """;

    public const string ExportCsvSummary = "Descarga la lista de participantes de un evento como CSV";

    public const string ExportCsvDescription =
        """
        Las mismas filas y columnas que la lista, con los mismos cinco filtros, sin paginar y sin
        tope. UTF-8 con BOM, cabeceras en español y comillas de RFC 4180. El archivo se llama
        `convocatoria-<id del evento>-<yyyy-MM-dd>.csv`: el nombre del evento no se consulta.
        """;

    public const string ListCampaignRecipientsSummary = "Lista los destinatarios de una campaña, tenga evento o no";

    public const string ListCampaignRecipientsDescription =
        """
        Las mismas filas que la lista de participantes del evento filtrada por `campaignId`, borrador
        incluido, pero buscadas por la campaña: un mensaje directo no tiene evento. En una campaña de
        evento la respuesta es idéntica a la de esa lista; en un mensaje directo no hay inscripciones
        que unir, así que cada fila es un destinatario. Una campaña que no existe es `404`.
        """;
}
