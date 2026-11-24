namespace Sorteo360.Events.Web.EventEndpoints;

/// <summary>
/// Textos en español de las operaciones de imágenes de un evento, fuera de la tabla de rutas.
/// </summary>
internal static class EventImageEndpointsDescriptions
{
    public const string UploadSummary = "Sube una imagen a un evento";

    public const string UploadDescription =
        """
        Sube un archivo (`multipart/form-data`, campo `file`) y lo enlaza al evento en una sola
        llamada, una imagen por llamada.

        La imagen se sube después de crear el evento, porque no se pueden subir bytes a una fila
        que todavía no existe.

        El campo `kind` dice qué papel cumple: `Cover` la portada que encabeza el evento,
        `Gallery` una foto más de la galería promocional. Es opcional y omitirlo vale `Gallery`;
        mandarlo vacío es lo mismo que no mandarlo. Distingue mayúsculas.

        **Un evento tiene una sola portada, y la que se sube reemplaza a la anterior.** No se
        rechaza: cambiar la portada es un clic y no dos, y exigir que primero se quite la vieja
        dejaría al evento sin ninguna si la segunda llamada falla. La portada anterior queda
        desenlazada y sus bytes se quedan, así que sigue respondiendo por su id.

        El cupo del evento cuenta solo las fotos de galería. La portada no entra: es única, así
        que su propia regla ya la acota.

        La respuesta trae dos identificadores distintos: `id` es la imagen dentro del evento y es
        con lo que se quita, y `storedFileId` es el archivo, con el que se piden los bytes a
        `/v1/files/{storedFileId}/content`.

        El tipo del archivo lo deciden sus bytes y no el `Content-Type` ni la extensión, que los
        elige quien sube. Acá solo se admiten imágenes —JPEG, PNG o WEBP—: un PDF pasa las reglas
        generales de la plataforma pero no se puede enseñar en una página. Un archivo demasiado
        grande, que no sea una imagen, o cuyo contenido contradiga lo declarado se rechaza con su
        propio código y sin guardar nada, lo mismo que un evento que ya llegó a su cupo de
        galería.
        """;

    public const string ListSummary = "Lista las imágenes de un evento";

    public const string ListDescription =
        """
        Devuelve el nombre, el tipo, el tamaño y la fecha de cada imagen, la portada primero y el
        resto de la más antigua a la más reciente. Los bytes no vienen acá: se piden a
        `/v1/files/{storedFileId}/content`.

        Cada fila trae su `kind` —`Cover` o `Gallery`—. El parámetro `kind` filtra por él; sin él,
        o vacío, vienen los dos tipos. Distingue mayúsculas, y un valor que no exista se rechaza
        con `EVENT_IMAGE_KIND_NOT_SUPPORTED`.

        `id` es la imagen y `storedFileId` es el archivo; no son intercambiables. Una imagen cuyo
        archivo ya no esté en el almacenamiento se sigue listando con `missing` en `true`, porque
        el nombre y la fecha son lo que permite saber qué falta.

        Un evento sin imágenes devuelve una lista vacía, que no es lo mismo que un evento que no
        existe.
        """;

    public const string RemoveSummary = "Quita una imagen de un evento";

    public const string RemoveDescription =
        """
        Desenlaza la imagen del evento, sea la portada o una foto de la galería. El archivo y sus
        bytes no se borran: siguen respondiendo por su id, que es lo que deja la purga de
        retención como una decisión posterior y no como un rescate.

        Quitar la portada deja al evento sin ninguna hasta que se suba otra; el listado devuelve
        `cover` en `null` mientras tanto.
        """;
}
