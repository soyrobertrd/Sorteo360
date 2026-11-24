namespace Sorteo360.Convocatorias.Core.Uploads;

/// <summary>
/// Una fila de datos tal como salió de la hoja: los cinco campos de
/// <see cref="UploadColumnMap"/>, ya recortados, y el número de fila del archivo original.
/// <para>
/// <b>El número de fila es el del archivo, no el del lote.</b> Es lo que quien subió va a mirar
/// cuando abra su hoja, así que cuenta la de encabezados: en un archivo normal la primera fila
/// de datos es la 2.
/// </para>
/// <para>
/// Nada de acá está validado todavía. El correo puede no ser un correo y el nombre puede faltar:
/// eso lo decide <see cref="UploadRowClassifier"/>, y decidirlo al leer mezclaría parsear con
/// juzgar.
/// </para>
/// </summary>
/// <param name="SourceRowNumber">La fila del archivo, 1-based y contando el encabezado.</param>
/// <param name="FirstName">Nombre, nulo si la columna faltaba o venía vacía.</param>
/// <param name="LastName">Apellidos.</param>
/// <param name="Email">La dirección, <b>cruda salvo el recorte</b>: normalizar es del CRM (§5).</param>
/// <param name="Phone">Teléfono.</param>
/// <param name="Company">
/// La empresa como texto, y se queda como texto. No se resuelve contra nada ni crea nada.
/// </param>
public sealed record ParsedUploadRow(
    int SourceRowNumber,
    string? FirstName,
    string? LastName,
    string? Email,
    string? Phone,
    string? Company);
