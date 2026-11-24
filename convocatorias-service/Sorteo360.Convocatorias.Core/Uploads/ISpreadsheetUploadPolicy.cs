using CSharpFunctionalExtensions;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Uploads;

/// <summary>
/// Las reglas de lo que se admite subir como lista de destinatarios, en un solo sitio.
/// <para>
/// <b>Copia la forma de <c>IUploadPolicy</c> de events-service, no su contenido</b> (spec
/// <c>05</c> §3.3). Lo que se copia es la forma: <c>Result&lt;T, BaseError&gt;</c>, todo
/// comprobado <b>antes</b> de escribir un byte, y el tipo declarado y la extensión usados solo
/// para detectar que contradicen al contenido, nunca para decidir. Lo que no se copia es su
/// lista de aceptados, su caso HEIC y sus cupos por dueño: aquella política es de imágenes y
/// PDF que se vuelven a servir a un navegador, y esta es de dos formatos que se parsean una vez
/// y se tiran.
/// </para>
/// </summary>
public interface ISpreadsheetUploadPolicy
{
    /// <summary>Los tres números de §7, para que quien lee cuente contra el mismo sitio que decide.</summary>
    SpreadsheetUploadLimits Limits { get; }

    /// <summary>
    /// Decide si el archivo se admite y de qué formato es realmente, con la codificación que hizo
    /// falta para leerlo. El contenido debe poder rebobinarse y se deja en la posición en que
    /// llegó, porque después se parsea.
    /// </summary>
    /// <param name="content">Contenido a inspeccionar; debe admitir búsqueda.</param>
    /// <param name="fileName">Nombre original, del que sale la extensión declarada.</param>
    /// <param name="declaredContentType">Tipo declarado por quien sube el archivo.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<Result<SpreadsheetFormat, BaseError>> InspectAsync(
        Stream content,
        string fileName,
        string declaredContentType,
        CancellationToken cancellationToken);

    /// <summary>
    /// Comprueba que el número de filas de datos cabe en la escritura de la confirmación. Se
    /// pregunta después de parsear, porque cuántas filas trae un archivo no se sabe antes.
    /// </summary>
    UnitResult<BaseError> CheckRowCount(int dataRowCount);
}
