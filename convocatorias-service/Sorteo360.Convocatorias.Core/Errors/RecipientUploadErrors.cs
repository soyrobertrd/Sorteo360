using Sorteo360.Convocatorias.Core.Uploads;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Errors;

/// <summary>
/// Los rechazos de <b>archivo</b> de una carga de destinatarios. Un fallo de archivo tumba la
/// subida entera porque es no haber podido producir ningún resumen; todo lo demás es una fila y
/// vive en <see cref="UploadRowBucket"/> (spec <c>05</c> §3 y §4).
/// <para>
/// <b>Tres códigos de tamaño y no uno.</b> Quien sube puede actuar sobre "el archivo pesa
/// demasiado" y no puede actuar sobre "el archivo se expande de forma sospechosa", así que
/// juntarlos convertiría un aviso accionable en uno que no lo es.
/// </para>
/// </summary>
public static class RecipientUploadErrors
{
    /// <summary>Más de lo admitido. Se comprueba antes de leer un solo byte del contenido.</summary>
    public static BaseError FileTooLarge(long maxSizeMegabytes) => new(
        $"El archivo supera el tamaño máximo permitido de {maxSizeMegabytes} MB.",
        "FILE_TOO_LARGE",
        ErrorKind.Invalid);

    /// <summary>
    /// Ni un xlsx según §3.1 ni un csv según §3.2. Cubre el caso que la firma sola no distingue:
    /// un docx, un pptx, un jar y un apk empiezan todos por <c>50 4B 03 04</c>.
    /// </summary>
    public static BaseError FileTypeNotAllowed() => new(
        "El archivo no es una hoja de cálculo admitida. Se aceptan libros de Excel (.xlsx) y archivos de texto separado (.csv).",
        "FILE_TYPE_NOT_ALLOWED",
        ErrorKind.Invalid);

    /// <summary>
    /// Más filas de datos de las que admite la escritura de la confirmación. <b>El mensaje lleva
    /// el tope</b>: "su archivo tiene 800 filas, el máximo es 500" es recuperable y claro, que es
    /// justamente la asimetría por la que el número se eligió bajo.
    /// </summary>
    public static BaseError TooManyRows(int rowCount, int maxDataRows) => new(
        $"El archivo tiene {rowCount} filas de datos y el máximo admitido es {maxDataRows}. Divídalo y suba una parte por vez.",
        "UPLOAD_TOO_MANY_ROWS",
        ErrorKind.Invalid);

    /// <summary>
    /// El libro se expande muchísimo más de lo que ocupa. Es una bomba zip, no un archivo
    /// grande: lo grande ya lo acotan el tamaño y las filas.
    /// </summary>
    public static BaseError ExpandedTooLarge(long maxDecompressedMegabytes) => new(
        $"El archivo se descomprime por encima de {maxDecompressedMegabytes} MB y no se puede procesar.",
        "UPLOAD_EXPANDED_TOO_LARGE",
        ErrorKind.Invalid);

    /// <summary>Una hoja que llega más allá de las columnas admitidas. Se mide antes de parsear.</summary>
    public static BaseError TooManyColumns(int maxColumns) => new(
        $"Una hoja del archivo pasa de {maxColumns} columnas. Deje solo las columnas de la lista y vuelva a subirlo.",
        "UPLOAD_TOO_MANY_COLUMNS",
        ErrorKind.Invalid);

    /// <summary>
    /// Se pudo parsear y no se reconoció ninguna columna. No es lo mismo que un archivo sin
    /// filas de datos, que sí es una subida correcta.
    /// </summary>
    public static BaseError NoHeaderRow() => new(
        "No se reconoció ninguna columna en la primera fila. Se esperan encabezados como 'nombre', 'apellido', 'correo', 'teléfono' o 'empresa'.",
        "UPLOAD_NO_HEADER_ROW",
        ErrorKind.Invalid);

    /// <summary>
    /// Se pidió confirmar y no hay ninguna carga sin confirmar en esa campaña. Código propio y
    /// no un 404 vacío: desde esta ruta también se responde 404 cuando la campaña no existe, y
    /// quien consume tiene que poder distinguir las dos cosas.
    /// </summary>
    public static BaseError NoPendingUpload() => new(
        "Esta campaña no tiene ninguna carga pendiente de confirmar.",
        UploadNotFoundCode,
        ErrorKind.NotFound);

    /// <summary>
    /// La carga desapareció mientras se confirmaba. El caso real es que alguien cancelara la
    /// campaña en pleno vuelo, y la cascada se llevara el lote y sus filas. Mismo código que
    /// <see cref="NoPendingUpload"/>, porque para quien consume es el mismo hecho —ya no hay nada
    /// que confirmar— y solo cambia el mensaje, que sí nombra lo que pasó.
    /// </summary>
    public static BaseError UploadGoneWhileConfirming() => new(
        "La carga dejó de existir mientras se confirmaba. Vuelva a leer la campaña.",
        UploadNotFoundCode,
        ErrorKind.NotFound);

    private const string UploadNotFoundCode = "UPLOAD_NOT_FOUND";
}
