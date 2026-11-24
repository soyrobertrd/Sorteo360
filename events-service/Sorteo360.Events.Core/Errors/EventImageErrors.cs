using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Errors;

/// <summary>
/// Rechazos propios de las imágenes de un evento. Los del archivo en sí
/// (<see cref="FileUploadErrors"/>) y los del almacenamiento (<see cref="FileErrors"/>) están
/// aparte: acá solo vive lo que depende de a qué se está subiendo la imagen.
/// </summary>
public static class EventImageErrors
{
    /// <summary>
    /// No hay tal evento, o fue eliminado. Se responde antes de leer un solo byte, así que una
    /// subida contra un evento inexistente no deja nada en el almacenamiento.
    /// </summary>
    public static BaseError EventNotFound() => new(
        "El evento no existe.",
        "EVENT_NOT_FOUND",
        ErrorKind.NotFound);

    /// <summary>La imagen no está entre las de ese evento, o ya se quitó.</summary>
    public static BaseError ImageNotFound() => new(
        "La imagen no existe en ese evento.",
        "EVENT_IMAGE_NOT_FOUND",
        ErrorKind.NotFound);

    /// <summary>
    /// El <c>kind</c> que llegó no es ninguno de los que existen. Se rechaza en vez de caer en el
    /// valor por omisión: subir una foto de la galería y que reemplace la portada es justo lo que
    /// esta distinción vino a evitar. Distingue mayúsculas, igual que la moneda.
    /// </summary>
    public static BaseError KindNotSupported() => new(
        "El tipo de imagen no es válido. Se admiten 'Cover' y 'Gallery'.",
        "EVENT_IMAGE_KIND_NOT_SUPPORTED",
        ErrorKind.Invalid);
}
