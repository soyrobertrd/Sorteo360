using Sorteo360.Convocatorias.Core.Portal;
using Net.Codecrete.QrCodeGenerator;

namespace Sorteo360.Convocatorias.Infrastructure.Portal;

/// <inheritdoc cref="IQrImageRenderer" />
/// <remarks>
/// <b>Net.Codecrete.QrCodeGenerator, todo en memoria y sin nada nativo</b> (research
/// <c>.scratch/convocatorias-map/research-qr-generation.md</c>): dos DLLs administradas, ninguna
/// dependencia de <c>System.Drawing</c> ni de una librería que instalar en el Dockerfile. Devuelve
/// los bytes del PNG y no toca el disco: la imagen se genera al vuelo y no pide volumen
/// (spec <c>06</c> §4.5).
/// </remarks>
public sealed class QrImageRenderer : IQrImageRenderer
{
    /// <summary>
    /// Corrección de errores <c>Quartile</c>, la que fija la spec <c>06</c> §4.5. No es una
    /// elección de tamaño —a escala 10 el PNG midió 392 bytes— sino de legibilidad: un QR sobre
    /// una escarapela impresa sobrevive a una mancha o un doblez con este nivel.
    /// </summary>
    private const QrCode.Ecc ErrorCorrection = QrCode.Ecc.Quartile;

    /// <summary>Diez píxeles por módulo, el valor medido en la spec <c>06</c> §4.5.</summary>
    private const int Scale = 10;

    /// <summary>
    /// La zona de silencio, en módulos. Cuatro es el mínimo del estándar; por debajo hay lectores
    /// que no encuentran el código pegado al borde de la imagen.
    /// </summary>
    private const int QuietZone = 4;

    /// <inheritdoc />
    public byte[] RenderPng(string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        // El orden de los parámetros es (border, scale): nombrarlos evita servir un QR diminuto
        // sin zona de silencio por haberlos cruzado.
        return QrCode.EncodeText(payload, ErrorCorrection).ToPngBitmap(border: QuietZone, scale: Scale);
    }
}
