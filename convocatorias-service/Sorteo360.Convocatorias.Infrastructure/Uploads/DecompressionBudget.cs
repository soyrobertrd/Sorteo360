using System.Buffers;
using System.IO.Compression;

namespace Sorteo360.Convocatorias.Infrastructure.Uploads;

/// <summary>
/// Cuenta lo que un libro ocupa una vez descomprimido y corta en cuanto se pasa del presupuesto.
/// <para>
/// <b>Acá vive la cuenta y no el número.</b> El número lo fija <c>SpreadsheetUploadLimits</c>; la
/// cuenta tiene que estar donde se lee, porque el tamaño expandido no se sabe de antemano y la
/// cabecera del zip la escribe quien manda el archivo (spec <c>05</c> §7).
/// </para>
/// <para>
/// Se lee cada entrada a la basura, sin guardar nada: lo que interesa es cuántos bytes salen. El
/// coste tiene techo, que es el propio presupuesto, y por eso una bomba zip no llega a expandirse
/// entera.
/// </para>
/// <para>
/// <b>Es una pasada previa y no el envoltorio que dibuja §7</b>, que cuenta mientras el parser
/// lee. Quien abre el zip es MiniExcel, así que no hay ningún <c>ZipArchiveEntry</c> nuestro que
/// envolver sin reimplementar la lectura de xlsx. La pasada cuesta una descompresión de más sobre
/// un libro legítimo —acotada por el tope de 5 MB del archivo— y corta igual de pronto sobre uno
/// que no lo es, que es lo que el número protege. Si algún día se lee el zip a mano, el envoltorio
/// es la forma mejor.
/// </para>
/// </summary>
internal static class DecompressionBudget
{
    private const int BufferSize = 81920;

    /// <summary>
    /// Indica si el libro se descomprime dentro del presupuesto. Deja el contenido rebobinado
    /// donde estaba, porque después se parsea.
    /// </summary>
    public static async Task<bool> IsWithinAsync(
        Stream content,
        long maxDecompressedBytes,
        CancellationToken cancellationToken)
    {
        var start = content.Position;
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

        try
        {
            using var archive = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);

            var total = 0L;

            foreach (var entry in archive.Entries)
            {
                await using var entryStream = entry.Open();

                int read;

                while ((read = await entryStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    total += read;

                    if (total > maxDecompressedBytes)
                        return false;
                }
            }

            return true;
        }
        catch (InvalidDataException)
        {
            // Una entrada que no se puede inflar. La inspección ya dijo que el libro se abre y
            // trae su `xl/workbook.xml`, así que esto es un archivo roto: no cabe en el
            // presupuesto porque no se puede leer.
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            content.Position = start;
        }
    }
}
