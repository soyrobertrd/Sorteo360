namespace Sorteo360.Convocatorias.Web.CampaignEndpoints;

/// <summary>Textos del documento OpenAPI para las dos rutas de carga de destinatarios.</summary>
internal static class RecipientUploadEndpointsDescriptions
{
    public const string StageSummary = "Sube una lista de destinatarios para revisarla";

    public const string StageDescription =
        """
        Lee una hoja (`.xlsx` o `.csv`) y deja **revisada** la lista, sin crear nada todavía. Es
        una segunda fuente de destinatarios sobre una campaña en borrador, al lado de la
        selección del CRM, y las dos se pueden usar en la misma campaña.

        El archivo **no se guarda**: se parsea y se descarta. Lo que queda son las filas
        revisadas, que es mejor registro que los bytes porque son lo que alguien confirmó.

        Cada fila cae en exactamente uno de seis cubos, evaluados en este orden y gana el
        primero: `malformed`, `duplicate_in_file`, `already_a_recipient`, `suppressed`,
        `matched`, `unmatched`. Las siete cuentas —esos seis más las filas idénticas plegadas—
        suman las filas de datos del archivo.

        **Un fallo de fila nunca rechaza el archivo.** Un archivo donde todas las filas están mal
        se revisa igual, con cero filas confirmables: es así como alguien descubre que se
        equivocó de columnas, y un 400 no se lo diría.

        Rechazos de archivo: `FILE_TOO_LARGE` (más de 5 MB), `FILE_TYPE_NOT_ALLOWED` (ni xlsx ni
        csv), `UPLOAD_TOO_MANY_ROWS` (más de 500 filas de datos, el mensaje lleva el tope),
        `UPLOAD_EXPANDED_TOO_LARGE` (el libro se descomprime más allá de lo admitido),
        `UPLOAD_TOO_MANY_COLUMNS` (una hoja del xlsx pasa de 50 columnas) y
        `UPLOAD_NO_HEADER_ROW` (ninguna columna reconocida en la primera fila).

        **Una segunda subida reemplaza a la primera.** Solo hay un sitio de carga pendiente por
        campaña; confirmar es lo que lo libera.

        Solo sobre una campaña en `draft`: cualquier otro estado es `CAMPAIGN_NOT_DRAFT` (409).
        """;

    public const string ConfirmSummary = "Confirma la carga pendiente";

    public const string ConfirmDescription =
        """
        Aplica lo que la revisión propuso: crea en el CRM las filas `unmatched`, enlaza las
        `matched`, escribe una exclusión por cada fila `suppressed` y no toca las
        `already_a_recipient`. Las `malformed` y las `duplicate_in_file` no se confirman nunca.

        Es **sincrónica, reanudable e idempotente por fila**: una petición del navegador y N
        peticiones al CRM por dentro. Si el CRM se cae a mitad, responde `503 CRM_UNAVAILABLE`
        con `completed` —las filas ya aplicadas— en las extensiones del ProblemDetails, y volver
        a llamar continúa desde ahí sin crear a nadie dos veces.

        Un `409` del CRM **no es un error**: significa que esa dirección ya la ocupa alguien, así
        que se toma su id y se enlaza, y si ese contacto está suprimido la fila pasa a exclusión
        en vez de a destinatario.

        **Una confirmación parcial no se deshace.** Lo que se creó en el CRM se queda creado: el
        CRM es el sistema de registro.

        Al terminar, el lote se sella con quién confirmó y cuándo, y sus filas se borran. El
        resumen con las siete cuentas se queda para siempre.

        Solo sobre una campaña en `draft`: cualquier otro estado es `CAMPAIGN_NOT_DRAFT` (409).
        """;
}
