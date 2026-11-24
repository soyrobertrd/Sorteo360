using System.Globalization;
using System.Text;

namespace Sorteo360.Convocatorias.Core.Uploads;

/// <summary>
/// Cómo se llaman en la hoja las cinco columnas que se leen. <b>Es el único sitio del servicio
/// donde vive esa correspondencia</b>, que es lo que pide la spec <c>05</c> §8: el listado real
/// del cliente todavía no llegó, así que esto se va a cambiar <b>una vez</b>, acá, y no se
/// diseña para que cambie muchas.
/// <para>
/// <b>Provisional a propósito.</b> Nombre, apellido, correo, teléfono y empresa, con los títulos
/// que escribe un Excel en español y sus equivalentes en inglés. Cuáles son obligatorias no se
/// decide acá: lo decide el catálogo de rechazos por fila (<see cref="MalformedRowReason"/>),
/// porque una fila a la que le falta algo no invalida el archivo.
/// </para>
/// <para>
/// El cotejo perdona lo que un título de columna trae de adorno: mayúsculas, acentos, espacios de
/// más y los separadores <c>_</c>, <c>-</c> y <c>.</c>. Así "Primer Nombre", "primer_nombre" y
/// "PRIMER NOMBRE" son la misma columna sin necesidad de listarlas tres veces.
/// </para>
/// </summary>
public static class UploadColumnMap
{
    /// <summary>
    /// Los títulos aceptados por columna, ya normalizados con <see cref="Normalize"/>. Se
    /// escriben en su forma normalizada para que la lista se lea igual que como se compara.
    /// </summary>
    private static readonly Dictionary<string, UploadColumn> Headers = new(StringComparer.Ordinal)
    {
        ["nombre"] = UploadColumn.FirstName,
        ["nombres"] = UploadColumn.FirstName,
        ["primernombre"] = UploadColumn.FirstName,
        ["firstname"] = UploadColumn.FirstName,

        ["apellido"] = UploadColumn.LastName,
        ["apellidos"] = UploadColumn.LastName,
        ["primerapellido"] = UploadColumn.LastName,
        ["lastname"] = UploadColumn.LastName,
        ["surname"] = UploadColumn.LastName,

        ["correo"] = UploadColumn.Email,
        ["correoelectronico"] = UploadColumn.Email,
        ["email"] = UploadColumn.Email,
        ["emailaddress"] = UploadColumn.Email,

        ["telefono"] = UploadColumn.Phone,
        ["celular"] = UploadColumn.Phone,
        ["phone"] = UploadColumn.Phone,
        ["phonenumber"] = UploadColumn.Phone,

        ["empresa"] = UploadColumn.Company,
        ["compania"] = UploadColumn.Company,
        ["institucion"] = UploadColumn.Company,
        ["organizacion"] = UploadColumn.Company,
        ["company"] = UploadColumn.Company,
        ["organization"] = UploadColumn.Company,
    };

    /// <summary>
    /// La columna que corresponde a ese título, o <c>null</c> si no es ninguna de las conocidas.
    /// Un título desconocido no es un error: la hoja del cliente puede traer columnas que a este
    /// servicio no le sirven, y se descartan sin ruido.
    /// </summary>
    public static UploadColumn? Resolve(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
            return null;

        return Headers.TryGetValue(Normalize(header), out var column) ? column : null;
    }

    /// <summary>
    /// Deja un título en la forma con la que se compara: sin acentos, sin mayúsculas y sin
    /// espacios ni separadores. Lo que queda son solo letras y dígitos.
    /// </summary>
    private static string Normalize(string header)
    {
        var decomposed = header.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            // La descomposición separa la tilde de su letra; se descarta la tilde y se queda la
            // letra, que es lo que hace que "teléfono" y "telefono" sean el mismo título.
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(character))
                builder.Append(character);
        }

        return builder.ToString();
    }
}
