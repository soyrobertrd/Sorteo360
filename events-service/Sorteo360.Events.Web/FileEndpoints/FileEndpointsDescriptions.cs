namespace Sorteo360.Events.Web.FileEndpoints;

/// <summary>
/// Textos en español de las operaciones de archivos, fuera de la tabla de rutas.
/// </summary>
internal static class FileEndpointsDescriptions
{
    public const string ListMissingFilesSummary = "Lista los archivos que el almacenamiento perdió";

    public const string ListMissingFilesDescription =
        """
        Devuelve los archivos cuya fila sigue estando pero cuyos bytes el almacenamiento ya no
        tiene, del más reciente al más antiguo. Un archivo entra en esta lista la primera vez que
        alguien intenta descargarlo y el almacenamiento contesta que no está.

        Es la pantalla del operador: el nombre original, quién lo subió, cuándo, y desde cuándo
        falta son lo que permite ir a buscarlo en la papelera del almacenamiento y restaurarlo.
        Restaurado el archivo, la primera descarga que vuelve a encontrarlo lo saca de esta lista.

        Un almacenamiento caído no agrega nada acá: no contestar no es lo mismo que contestar que
        el archivo no está.
        """;

    public const string GetFileByIdSummary = "Obtiene la ficha de un archivo";

    public const string GetFileByIdDescription =
        """
        Devuelve el nombre original, el tipo, el tamaño y la fecha de carga del archivo. La ficha
        se devuelve aunque el almacenamiento ya no tenga los bytes: en ese caso `missing` viene en
        `true`, porque el nombre y la fecha son lo que permite saber qué falta.

        Sirve a cualquier consumidor que tenga el id de un archivo, venga de donde venga.
        """;

    public const string GetFileContentSummary = "Descarga el contenido de un archivo";

    public const string GetFileContentDescription =
        """
        Entrega los bytes en streaming, con `ETag` y `Cache-Control`, de modo que una segunda
        visita se resuelve con un `304` si el archivo no cambió. Los archivos son inmutables:
        reemplazar uno crea otro con id propio.

        Un archivo que el almacenamiento ya no tiene responde `404` con el código `FILE_MISSING`,
        y un almacenamiento que no contesta responde `503` con `FILE_STORE_UNAVAILABLE`. Son dos
        situaciones distintas: la primera es permanente y la segunda se reintenta.
        """;
}
