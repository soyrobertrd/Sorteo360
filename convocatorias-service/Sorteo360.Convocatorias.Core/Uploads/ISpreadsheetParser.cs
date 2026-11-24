using CSharpFunctionalExtensions;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Uploads;

/// <summary>
/// Saca las filas de datos de una hoja ya admitida por <see cref="ISpreadsheetUploadPolicy"/>.
/// Solo lee: no valida nada del contenido de una fila, que es de
/// <see cref="UploadRowClassifier"/>.
/// <para>
/// Los dos únicos rechazos que puede devolver son de archivo: no se reconoció ningún encabezado,
/// y el libro se descomprime por encima del presupuesto. <b>Ese presupuesto lo fija la política
/// y lo cuenta quien lee</b>, porque el tamaño expandido no se sabe de antemano (spec <c>05</c>
/// §7).
/// </para>
/// </summary>
public interface ISpreadsheetParser
{
    /// <summary>
    /// Lee las filas de datos. El contenido se consume desde donde esté y debe poder rebobinarse.
    /// </summary>
    /// <param name="content">El archivo, tal como llegó.</param>
    /// <param name="format">Lo que averiguó la inspección: formato, codificación y separador.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<Result<IReadOnlyList<ParsedUploadRow>, BaseError>> ParseAsync(
        Stream content,
        SpreadsheetFormat format,
        CancellationToken cancellationToken);
}
