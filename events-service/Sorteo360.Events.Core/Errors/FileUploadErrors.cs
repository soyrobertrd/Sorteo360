using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Errors;

/// <summary>
/// Rechazos de una subida. Todos son fallos de quien llama y ninguno llega a tocar el
/// almacenamiento, así que ninguno deja bytes detrás.
/// <para>
/// Cada uno lleva su propio código porque el cliente decide con él qué mostrar: el de HEIC, en
/// particular, es el que más gente va a ver, y lo que ese mensaje explique es lo que evita la
/// llamada al soporte.
/// </para>
/// </summary>
public static class FileUploadErrors
{
    private const long BytesPerMegabyte = 1024 * 1024;

    /// <summary>
    /// El archivo pesa más de lo admitido. El tamaño sale de contar los bytes que llegaron y no
    /// de lo que declare nadie.
    /// </summary>
    public static BaseError FileTooLarge(long maxSizeBytes) => new(
        $"El archivo supera el tamaño máximo permitido de {maxSizeBytes / BytesPerMegabyte} MB.",
        "FILE_TOO_LARGE",
        ErrorKind.Invalid);

    /// <summary>
    /// Los bytes no son de ninguna de las clases que se guardan. Es el rechazo genérico: si se
    /// sabe qué es y aun así no sirve, hay un código propio para decirlo.
    /// </summary>
    public static BaseError FileTypeNotAllowed() => new(
        "El archivo no es de un tipo admitido. Se aceptan imágenes JPEG, PNG o WEBP y documentos PDF.",
        "FILE_TYPE_NOT_ALLOWED",
        ErrorKind.Invalid);

    /// <summary>
    /// Los bytes son de una clase que se guarda, pero no de una que se muestre: un PDF de portada
    /// no se puede enseñar en una página. Lleva el mismo código que el rechazo genérico —el caso
    /// es el mismo, estos bytes no sirven acá— y solo cambia el mensaje, que nombra lo que este
    /// endpoint sí admite.
    /// </summary>
    public static BaseError ImageTypeNotAllowed() => new(
        "El archivo no es una imagen. Se aceptan JPEG, PNG o WEBP.",
        "FILE_TYPE_NOT_ALLOWED",
        ErrorKind.Invalid);

    /// <summary>
    /// La firma de una entrega no es un PNG. Mismo código que el rechazo genérico, por el mismo motivo
    /// que <see cref="ImageTypeNotAllowed"/>; el mensaje nombra lo único que se admite.
    /// </summary>
    public static BaseError SignatureTypeNotAllowed() => new(
        "La firma debe ser una imagen PNG tomada del panel de firma.",
        "FILE_TYPE_NOT_ALLOWED",
        ErrorKind.Invalid);

    /// <summary>
    /// Los bytes son de una clase admitida, pero el tipo declarado o la extensión dicen otra
    /// cosa. Se rechaza en vez de corregirse en silencio: un nombre que miente sobre lo que
    /// contiene es lo que después se sirve como si fuera lo que dice ser.
    /// </summary>
    public static BaseError FileTypeMismatch() => new(
        "El contenido del archivo no corresponde con el tipo ni la extensión declarados.",
        "FILE_TYPE_MISMATCH",
        ErrorKind.Invalid);

    /// <summary>
    /// Una foto HEIC, que es lo que un iPhone dispara por omisión. Lleva código propio y un
    /// mensaje que dice qué cambiar, porque es un rechazo que la gente va a encontrar a diario y
    /// "formato no admitido" no le dice a nadie qué hacer.
    /// </summary>
    public static BaseError HeicNotSupported() => new(
        "Las fotos HEIC no se pueden mostrar en todos los navegadores. En el iPhone, la opción " +
        "Ajustes > Cámara > Formatos debe quedar en 'Más compatible' para que las fotos se " +
        "guarden como JPEG; una foto ya tomada se puede exportar a JPEG antes de subirla.",
        "FILE_TYPE_HEIC_NOT_SUPPORTED",
        ErrorKind.Invalid);

    /// <summary>
    /// La ficha ya llegó a su cupo de fotos. Es conflicto y no petición inválida porque el
    /// archivo está bien: lo que no admite otra foto es el estado de la ficha.
    /// </summary>
    public static BaseError ServiceSupplierPhotoLimitReached(int maxPhotos) => new(
        $"La ficha del proveedor ya tiene el máximo de {maxPhotos} fotos. Quite alguna para agregar otra.",
        "SUPPLIER_PHOTO_LIMIT_REACHED",
        ErrorKind.Conflict);

    /// <summary>
    /// La propuesta ya llegó a su cupo de adjuntos. Conflicto y no petición inválida por el
    /// mismo motivo que el de las fotos: el archivo está bien, lo que no admite otro es el
    /// estado de la propuesta.
    /// </summary>
    public static BaseError ProposalAttachmentLimitReached(int maxAttachments) => new(
        $"La propuesta ya tiene el máximo de {maxAttachments} adjuntos. Quite alguno para agregar otro.",
        "PROPOSAL_ATTACHMENT_LIMIT_REACHED",
        ErrorKind.Conflict);

    /// <summary>
    /// El evento ya llegó a su cupo de fotos de galería. Conflicto y no petición inválida por el
    /// mismo motivo que los anteriores: el archivo está bien, lo que no admite otra foto es el
    /// estado del evento. La portada no llega nunca acá, porque reemplaza en vez de acumularse.
    /// </summary>
    public static BaseError EventGalleryImageLimitReached(int maxImages) => new(
        $"El evento ya tiene el máximo de {maxImages} fotos de galería. Quite alguna para agregar otra.",
        "EVENT_GALLERY_IMAGE_LIMIT_REACHED",
        ErrorKind.Conflict);
}
