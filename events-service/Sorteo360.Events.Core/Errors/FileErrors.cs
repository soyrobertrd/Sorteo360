using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Errors;

/// <summary>
/// Fallos del almacenamiento de archivos. Los dos que hay son distintos y no se tratan igual: uno
/// es permanente y el otro pasa. Confundirlos haría que un reinicio de cinco minutos pareciera la
/// pérdida de la biblioteca entera.
/// </summary>
public static class FileErrors
{
    /// <summary>
    /// El almacenamiento contestó y el archivo no está: alguien lo borró o el antivirus lo puso en
    /// cuarentena. La fila se conserva —el nombre y la fecha son lo que dice qué hay que
    /// restaurar—, así que esto es 404 del contenido y no del recurso entero.
    /// </summary>
    public static BaseError FileMissing() => new(
        "El archivo ya no está disponible en el almacenamiento.",
        "FILE_MISSING",
        ErrorKind.NotFound);

    /// <summary>
    /// El almacenamiento no contestó. Este servicio está sano —de ahí el 503 y no un 500—, y quien
    /// pidió el archivo puede volver a intentarlo.
    /// </summary>
    public static BaseError FileStoreUnavailable() => new(
        "El almacenamiento de archivos no está disponible en este momento; intente nuevamente.",
        "FILE_STORE_UNAVAILABLE",
        ErrorKind.Unavailable);
}
