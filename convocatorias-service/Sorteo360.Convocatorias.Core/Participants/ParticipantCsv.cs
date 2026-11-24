using System.Globalization;
using System.Text;

namespace Sorteo360.Convocatorias.Core.Participants;

/// <summary>
/// La lista de participantes como CSV (spec <c>07</c> §4): las mismas columnas que la fila JSON,
/// con cabeceras en español, en UTF-8 con BOM y con las comillas de RFC 4180.
/// <para>
/// No filtra ni une nada: escribe lo que le pasan, que es lo que devuelve
/// <see cref="IParticipantService.ListAsync"/>. Así el CSV y la lista no pueden discrepar.
/// </para>
/// </summary>
public static class ParticipantCsv
{
    /// <summary>
    /// <b>Punto pendiente, no se decide acá</b> (§4): Excel usa el separador de listas del sistema,
    /// que en varias configuraciones en español es el punto y coma. Se resuelve probando en una
    /// laptop de MITUR, y cambiarlo es cambiar esta línea.
    /// </summary>
    private const char Delimiter = ',';

    /// <summary>República Dominicana no tiene horario de verano: siempre UTC-4.</summary>
    private static readonly TimeSpan SantoDomingoOffset = TimeSpan.FromHours(-4);

    /// <summary>Lo que hace que Excel en Windows lea <c>Peña</c> y no <c>PeÃ±a</c>.</summary>
    private static readonly byte[] Bom = [0xEF, 0xBB, 0xBF];

    /// <summary>
    /// Las columnas de la fila JSON de §3 en su orden, salvo la fecha de check-in, que va al final
    /// para no correr las columnas de las hojas que ya existen (enmienda del 2026-10-10). Cada
    /// cabecera junto a su valor para que una columna nueva no pueda quedar con la cabecera de otra. Los códigos y los booleanos salen como
    /// en el JSON; las fechas, en UTC y sin fracciones; quien reenvió, solo por su nombre, y las
    /// campañas separadas por espacios, porque una celda no lleva objetos ni listas.
    /// </summary>
    private static readonly (string Header, Func<Participant, string?> Value)[] Columns =
    [
        ("Correo", row => row.Email),
        ("Nombre", row => row.FirstName),
        ("Apellido", row => row.LastName),
        ("Empresa", row => row.Company),
        ("Origen", row => row.Origin),
        ("Estado de envío", row => row.SendStatus),
        ("Inscrito", row => TrueFalse(row.Registered)),
        ("Fecha de inscripción", row => UtcSeconds(row.RegisteredAt)),
        ("Referencia", row => row.Reference),
        ("Referido por", row => row.ReferredByName),
        ("Campañas", row => string.Join(' ', row.CampaignIds)),
        ("Sincronización CRM pendiente", row => TrueFalse(row.CrmSyncPending)),
        ("Datos distintos", row => TrueFalse(row.DetailsDiffer)),
        ("Fecha de check-in", row => UtcSeconds(row.CheckedInAt)),
    ];

    /// <summary>Lo que obliga a entrecomillar una celda (RFC 4180 §2.6).</summary>
    private static readonly char[] NeedsQuoting = [Delimiter, '"', '\r', '\n'];

    /// <summary>Con lo que empieza una fórmula de hoja de cálculo.</summary>
    private static readonly char[] FormulaStart = ['=', '+', '-', '@', '\t', '\r'];

    /// <summary>
    /// <c>convocatoria-&lt;evento&gt;-&lt;yyyy-MM-dd&gt;.csv</c>. El evento va por su id y no por su
    /// nombre: las lecturas de seguimiento no le preguntan nada a events-service (§7).
    /// </summary>
    public static string FileName(Guid eventId, DateTimeOffset now) =>
        $"convocatoria-{eventId}-{now.ToOffset(SantoDomingoOffset):yyyy-MM-dd}.csv";

    /// <summary>Escribe el BOM, la cabecera y una línea por fila, terminadas en CRLF.</summary>
    public static async Task WriteAsync(IEnumerable<Participant> rows, Stream output, CancellationToken cancellationToken)
    {
        await output.WriteAsync(Bom, cancellationToken);

        await using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);

        await WriteLineAsync(writer, Columns.Select(column => column.Header), cancellationToken);

        foreach (var row in rows)
            await WriteLineAsync(writer, Columns.Select(column => column.Value(row)), cancellationToken);

        await writer.FlushAsync(cancellationToken);
    }

    /// <summary>Una fecha en UTC y sin fracciones, o vacía.</summary>
    private static string? UtcSeconds(DateTimeOffset? value) =>
        value?.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    /// <summary>Un booleano escrito como lo escribe el JSON.</summary>
    private static string TrueFalse(bool value) => value ? "true" : "false";

    /// <summary>Una línea entera: las celdas escapadas, unidas por el separador y terminadas en CRLF.</summary>
    private static async Task WriteLineAsync(StreamWriter writer, IEnumerable<string?> cells, CancellationToken cancellationToken)
    {
        var line = string.Join(Delimiter, cells.Select(Escape)) + "\r\n";
        await writer.WriteAsync(line.AsMemory(), cancellationToken);
    }

    /// <summary>
    /// Una celda que empieza como fórmula lleva un apóstrofo delante, que Excel muestra como texto:
    /// el nombre lo escribe un desconocido en el portal. Después, las comillas de RFC 4180.
    /// </summary>
    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (FormulaStart.Contains(value[0]))
            value = "'" + value;

        return value.IndexOfAny(NeedsQuoting) < 0 ? value : $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
