using System.ComponentModel.DataAnnotations;
using Sorteo360.Platform.Files;

namespace Sorteo360.Events.Core.Files;

/// <summary>
/// Cuánto se admite subir (sección <c>FileStore</c>). Acompaña a la configuración del proveedor
/// porque se despliegan juntas, pero es una regla de este servicio y no del almacenamiento: los
/// valores por omisión son los acordados con el cliente y sirven sin configurar nada.
/// </summary>
public sealed record UploadLimits
{
    public static string SectionName => "FileStore";

    /// <summary>Tamaño máximo de un archivo. Por omisión, el de la plataforma: 25 MB.</summary>
    [Range(1, long.MaxValue)]
    public long MaxSizeBytes { get; init; } = FileContentInspector.DefaultMaxSizeBytes;

    /// <summary>
    /// Fotos que puede acumular una ficha de proveedor. Acota un clic equivocado y acota lo que
    /// cuesta cargar una galería.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MaxPhotosPerServiceSupplier { get; init; } = 20;

    /// <summary>
    /// Adjuntos que puede acumular una propuesta. Una cotización de verdad llega como un PDF más
    /// un par de anexos escaneados, así que el cupo es bastante más chico que el de la galería
    /// de un proveedor: pasarse de diez es señal de que se está subiendo otra cosa. El cupo es de
    /// la oferta y no del candidato: un local que cotiza tres salones manda tres cotizaciones
    /// escritas, y compartir el número entre ellas dejaría a la tercera sin sitio.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MaxAttachmentsPerProposal { get; init; } = 10;

    /// <summary>
    /// Fotos de galería que puede acumular un evento. La portada no cuenta acá: es única por
    /// evento y esa regla ya la acota sola, así que sumarla dejaría la galería en uno menos de lo
    /// que dice el número.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MaxGalleryImagesPerEvent { get; init; } = 20;
}
