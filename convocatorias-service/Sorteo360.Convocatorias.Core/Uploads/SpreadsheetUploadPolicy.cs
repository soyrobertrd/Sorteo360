using System.IO.Compression;
using System.Text;
using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Uploads;

/// <inheritdoc cref="ISpreadsheetUploadPolicy" />
public sealed class SpreadsheetUploadPolicy : ISpreadsheetUploadPolicy
{
    /// <summary>Los cuatro bytes con los que empieza todo zip, y por tanto todo xlsx.</summary>
    private static readonly byte[] ZipMagic = [0x50, 0x4B, 0x03, 0x04];

    /// <summary>
    /// La entrada que separa un xlsx de los demás zip que empiezan igual. Un docx, un pptx, un
    /// jar y un apk comparten la firma; solo un libro de Excel trae esto dentro, y comprobarlo
    /// es menos código que un override de <c>[Content_Types].xml</c> para el mismo resultado.
    /// </summary>
    private const string WorkbookEntry = "xl/workbook.xml";

    /// <summary>Cuánto se lee para probar si un texto es un csv (§3.2).</summary>
    private const int CsvProbeBytes = 8 * 1024;

    /// <summary>
    /// Separadores candidatos, en el orden en que se prueban. La coma primero porque es lo que
    /// escribe un Excel en inglés, y el punto y coma justo detrás porque es lo que escribe uno
    /// en español, que es la mitad de los archivos que va a recibir este servicio.
    /// </summary>
    private static readonly char[] CandidateDelimiters = [',', ';', '\t', '|'];

    private static readonly string[] XlsxContentTypes =
    [
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-excel",
        "application/octet-stream",
    ];

    private static readonly string[] CsvContentTypes =
    [
        "text/csv",
        "text/plain",
        "application/csv",
        "application/vnd.ms-excel",
        "application/octet-stream",
    ];

    public SpreadsheetUploadPolicy(SpreadsheetUploadLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        Limits = limits;
    }

    /// <inheritdoc />
    public SpreadsheetUploadLimits Limits { get; }

    /// <inheritdoc />
    public async Task<Result<SpreadsheetFormat, BaseError>> InspectAsync(
        Stream content,
        string fileName,
        string declaredContentType,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(fileName);

        if (!content.CanSeek)
        {
            throw new ArgumentException(
                "El contenido de una carga debe poder rebobinarse: se inspecciona y después se parsea.",
                nameof(content));
        }

        // El tamaño se cuenta, no se pregunta: una cabecera Content-Length la escribe quien
        // llama. Y se comprueba antes de leer nada, que es lo que hace que el tope sea barato.
        var start = content.Position;

        if (content.Length - start > Limits.MaxSizeBytes)
            return Result.Failure<SpreadsheetFormat, BaseError>(RecipientUploadErrors.FileTooLarge(Limits.MaxSizeMegabytes));

        var header = new byte[ZipMagic.Length];
        var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        content.Position = start;

        var format = read == ZipMagic.Length && header.AsSpan().SequenceEqual(ZipMagic)
            ? InspectXlsx(content, start)
            : await InspectCsvAsync(content, start, cancellationToken);

        if (format.IsFailure)
            return format;

        return Contradicts(format.Value.Kind, declaredContentType, fileName)
            ? Result.Failure<SpreadsheetFormat, BaseError>(RecipientUploadErrors.FileTypeNotAllowed())
            : format;
    }

    /// <inheritdoc />
    public UnitResult<BaseError> CheckRowCount(int dataRowCount)
    {
        return dataRowCount > Limits.MaxDataRows
            ? UnitResult.Failure<BaseError>(RecipientUploadErrors.TooManyRows(dataRowCount, Limits.MaxDataRows))
            : UnitResult.Success<BaseError>();
    }

    /// <summary>
    /// La firma más la comprobación estructural de §3.1. La firma sola no alcanza: la comparten
    /// todos los formatos Office modernos, más un jar y un apk.
    /// </summary>
    private static Result<SpreadsheetFormat, BaseError> InspectXlsx(Stream content, long start)
    {
        try
        {
            using var archive = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);

            var isWorkbook = archive.Entries.Any(entry =>
                string.Equals(entry.FullName, WorkbookEntry, StringComparison.OrdinalIgnoreCase));

            return isWorkbook
                ? Result.Success<SpreadsheetFormat, BaseError>(
                    new SpreadsheetFormat(SpreadsheetKind.Xlsx, SpreadsheetFormat.Utf8))
                : Result.Failure<SpreadsheetFormat, BaseError>(RecipientUploadErrors.FileTypeNotAllowed());
        }
        catch (InvalidDataException)
        {
            // Empieza como un zip y no se puede abrir como uno. No hay nada que parsear.
            return Result.Failure<SpreadsheetFormat, BaseError>(RecipientUploadErrors.FileTypeNotAllowed());
        }
        finally
        {
            content.Position = start;
        }
    }

    /// <summary>
    /// La prueba de §3.2: decodificar y parsear. No hay atajo por bytes mágicos, porque un csv
    /// es texto sin marca y un BOM no es marca de csv.
    /// </summary>
    private static async Task<Result<SpreadsheetFormat, BaseError>> InspectCsvAsync(
        Stream content,
        long start,
        CancellationToken cancellationToken)
    {
        var length = (int)Math.Min(CsvProbeBytes, content.Length - start);

        if (length == 0)
            return Result.Failure<SpreadsheetFormat, BaseError>(RecipientUploadErrors.FileTypeNotAllowed());

        var probe = new byte[length];
        var read = await content.ReadAtLeastAsync(probe, length, throwOnEndOfStream: false, cancellationToken);
        content.Position = start;

        var (text, encoding) = Decode(probe.AsSpan(0, read));

        if (text is null)
            return Result.Failure<SpreadsheetFormat, BaseError>(RecipientUploadErrors.FileTypeNotAllowed());

        var delimiter = DetectDelimiter(text, isWholeFile: content.Length - start <= read);

        return delimiter is null
            ? Result.Failure<SpreadsheetFormat, BaseError>(RecipientUploadErrors.FileTypeNotAllowed())
            : Result.Success<SpreadsheetFormat, BaseError>(
                new SpreadsheetFormat(SpreadsheetKind.Csv, encoding, delimiter));
    }

    /// <summary>
    /// UTF-8 estricto primero y Windows-1252 después, que es el orden de §3.2. El respaldo no es
    /// silencioso: la codificación que ganó viaja hasta el resumen.
    /// <para>
    /// Windows-1252 y no ISO-8859-1, aunque <c>Encoding.GetEncoding(28591)</c> funcione sin
    /// registrar nada y por eso parezca el arreglo correcto: las dos difieren justo donde un
    /// Excel en español pone sus comillas tipográficas.
    /// </para>
    /// </summary>
    private static (string? Text, string Encoding) Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            return (strictUtf8.GetString(bytes), SpreadsheetFormat.Utf8);
        }
        catch (DecoderFallbackException)
        {
            try
            {
                return (Encoding.GetEncoding(1252).GetString(bytes), SpreadsheetFormat.Windows1252);
            }
            catch (NotSupportedException)
            {
                // CodePagesEncodingProvider no se registró al arrancar (spec 00 §8). Es un error
                // de composición del host y no del archivo, así que sale como excepción.
                throw;
            }
        }
    }

    /// <summary>
    /// El separador con el que la primera línea se parte en dos campos o más, eligiendo aquel con
    /// el que <b>más</b> líneas coinciden en ese número. Una línea suelta sin separador no es una
    /// tabla, y por eso el mínimo de dos campos sí es innegociable.
    /// <para>
    /// Exigir que <b>todas</b> las líneas coincidan sería más estricto de lo que pide §3.2 —"al
    /// menos una línea que se parta en dos campos o más con un separador consistente"— y
    /// rechazaría un csv legítimo cuya columna de dirección traiga un salto de línea
    /// entrecomillado: esa fila se ve como dos líneas sueltas al partir por <c>\n</c>, ninguna con
    /// la cuenta correcta. Que no coincidan no las convierte en un veto.
    /// </para>
    /// </summary>
    /// <param name="text">El texto de la sonda, que puede acabar a mitad de una línea.</param>
    /// <param name="isWholeFile">
    /// Si la sonda alcanzó el final del archivo. Cuando no, la última línea puede estar cortada y
    /// se descarta: contar sus campos rechazaría un csv perfectamente válido.
    /// </param>
    private static char? DetectDelimiter(string text, bool isWholeFile)
    {
        var lines = text.Split('\n', StringSplitOptions.None)
            .Select(line => line.TrimEnd('\r'))
            .ToList();

        if (!isWholeFile && lines.Count > 1)
            lines.RemoveAt(lines.Count - 1);

        var complete = lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToList();

        if (complete.Count == 0)
            return null;

        char? best = null;
        var bestAgreement = 0;

        foreach (var candidate in CandidateDelimiters)
        {
            // La primera línea manda: es el encabezado, y es la que tiene que ser una tabla.
            var header = CountFields(complete[0], candidate);

            if (header < 2)
                continue;

            var agreement = complete.Count(line => CountFields(line, candidate) == header);

            // Estrictamente mayor, así que un empate se lo lleva el candidato más temprano: la
            // coma primero, que es lo que escribe un Excel en inglés, y el punto y coma detrás.
            if (agreement > bestAgreement)
            {
                bestAgreement = agreement;
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>
    /// Campos de una línea, respetando las comillas: un separador dentro de un campo entrecomillado
    /// no separa nada. Es la misma regla que aplica el parser, y contarla distinto acá haría que
    /// un archivo se admitiera con una forma y se leyera con otra.
    /// </summary>
    private static int CountFields(string line, char delimiter)
    {
        var fields = 1;
        var quoted = false;

        foreach (var character in line)
        {
            if (character == '"')
                quoted = !quoted;
            else if (character == delimiter && !quoted)
                fields++;
        }

        return fields;
    }

    /// <summary>
    /// Indica si lo declarado contradice a lo que dicen los bytes. No declarar nada no
    /// contradice: quien no afirma no se equivoca.
    /// <para>
    /// La contradicción sale con el mismo <c>FILE_TYPE_NOT_ALLOWED</c> que el rechazo genérico, y
    /// no con un código propio, porque §7.1 fija tres códigos de tamaño y uno solo de tipo: para
    /// quien sube el remedio es el mismo, mandar el archivo que de verdad quería mandar.
    /// </para>
    /// </summary>
    private static bool Contradicts(SpreadsheetKind kind, string? declaredContentType, string fileName)
    {
        var mediaType = declaredContentType?.Split(';')[0].Trim();
        var accepted = kind == SpreadsheetKind.Xlsx ? XlsxContentTypes : CsvContentTypes;

        if (!string.IsNullOrEmpty(mediaType)
            && !accepted.Contains(mediaType, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        var extension = Path.GetExtension(fileName);

        if (string.IsNullOrEmpty(extension))
            return false;

        return kind == SpreadsheetKind.Xlsx
            ? !extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
            : !extension.Equals(".csv", StringComparison.OrdinalIgnoreCase)
              && !extension.Equals(".txt", StringComparison.OrdinalIgnoreCase);
    }
}
