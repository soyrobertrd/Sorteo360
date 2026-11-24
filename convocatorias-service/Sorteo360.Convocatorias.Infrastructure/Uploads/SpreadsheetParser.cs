using System.Globalization;
using System.Text;
using CSharpFunctionalExtensions;
using MiniExcelLibs;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Uploads;
using Sorteo360.Platform.Errors;
using nietras.SeparatedValues;

namespace Sorteo360.Convocatorias.Infrastructure.Uploads;

/// <inheritdoc cref="ISpreadsheetParser" />
public sealed class SpreadsheetParser : ISpreadsheetParser
{
    /// <summary>
    /// La primera fila de datos de un archivo normal. El número que se guarda es el del archivo,
    /// porque es el que quien subió va a mirar en su hoja, y ahí la 1 es el encabezado.
    /// </summary>
    private const int FirstDataRowNumber = 2;

    private readonly ISpreadsheetUploadPolicy _policy;

    public SpreadsheetParser(ISpreadsheetUploadPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        _policy = policy;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<ParsedUploadRow>, BaseError>> ParseAsync(
        Stream content,
        SpreadsheetFormat format,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(format);

        return format.Kind == SpreadsheetKind.Xlsx
            ? await ParseXlsxAsync(content, cancellationToken)
            : ParseCsv(content, format);
    }

    private async Task<Result<IReadOnlyList<ParsedUploadRow>, BaseError>> ParseXlsxAsync(
        Stream content,
        CancellationToken cancellationToken)
    {
        var withinBudget = await DecompressionBudget.IsWithinAsync(
            content,
            _policy.Limits.MaxDecompressedBytes,
            cancellationToken);

        if (!withinBudget)
        {
            return Result.Failure<IReadOnlyList<ParsedUploadRow>, BaseError>(
                RecipientUploadErrors.ExpandedTooLarge(_policy.Limits.MaxDecompressedMegabytes));
        }

        if (!WorksheetWidth.IsWithin(content, _policy.Limits.MaxColumns))
        {
            return Result.Failure<IReadOnlyList<ParsedUploadRow>, BaseError>(
                RecipientUploadErrors.TooManyColumns(_policy.Limits.MaxColumns));
        }

        var start = content.Position;

        try
        {
            // useHeaderRow en false: el encabezado se resuelve acá contra UploadColumnMap, que es
            // quien perdona acentos y mayúsculas. Dejárselo a MiniExcel ataría los nombres de las
            // columnas a la grafía exacta del archivo.
            // MiniExcel entrega cada fila como un ExpandoObject, que es un
            // IDictionary<string, object> con claves A, B, C…
            var sheet = MiniExcel.Query(content, useHeaderRow: false, excelType: ExcelType.XLSX)
                .Cast<IDictionary<string, object>>();

            Dictionary<string, UploadColumn>? columns = null;
            var rows = new List<ParsedUploadRow>();
            var rowNumber = 0;

            foreach (var sheetRow in sheet)
            {
                cancellationToken.ThrowIfCancellationRequested();
                rowNumber++;

                if (columns is null)
                {
                    columns = MapColumns(sheetRow);
                    continue;
                }

                var row = ReadRow(rowNumber, key => Text(Cell(sheetRow, key)), columns);

                if (row is not null)
                    rows.Add(row);
            }

            return columns is null or { Count: 0 }
                ? Result.Failure<IReadOnlyList<ParsedUploadRow>, BaseError>(RecipientUploadErrors.NoHeaderRow())
                : Result.Success<IReadOnlyList<ParsedUploadRow>, BaseError>(rows);
        }
        finally
        {
            content.Position = start;
        }
    }

    private static Result<IReadOnlyList<ParsedUploadRow>, BaseError> ParseCsv(
        Stream content,
        SpreadsheetFormat format)
    {
        var start = content.Position;

        try
        {
            // Nunca un StringReader: el BOM de UTF-8 se pega al nombre de la primera columna y
            // deja esa columna sin reconocer (spec 05 §7.2). El StreamReader con detección de
            // marcas de orden de bytes se lo come.
            using var reader = new StreamReader(
                content,
                Encoding.GetEncoding(format.Encoding),
                detectEncodingFromByteOrderMarks: true,
                leaveOpen: true);

            // Unescape en true: la detección de comillas viene de fábrica, quitarlas no. Sin esto
            // un campo entrecomillado llega con sus comillas dentro (spec 05 §7.2).
            using var csv = Sep.New(format.Delimiter!.Value)
                .Reader(options => options with { Unescape = true, HasHeader = true })
                .From(reader);

            var columns = MapColumns(csv.Header.ColNames);

            if (columns.Count == 0)
                return Result.Failure<IReadOnlyList<ParsedUploadRow>, BaseError>(RecipientUploadErrors.NoHeaderRow());

            var rows = new List<ParsedUploadRow>();
            var rowNumber = 1;

            foreach (var csvRow in csv)
            {
                rowNumber++;

                var values = new string?[csvRow.ColCount];

                for (var index = 0; index < csvRow.ColCount; index++)
                {
                    values[index] = csvRow[index].ToString();
                }

                var row = ReadRow(rowNumber, index => Text((object?)values.ElementAtOrDefault(index)), columns);

                if (row is not null)
                    rows.Add(row);
            }

            return Result.Success<IReadOnlyList<ParsedUploadRow>, BaseError>(rows);
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException)
        {
            // Sep tira InvalidDataException cuando una línea no trae las mismas columnas que el
            // encabezado, y NotSupportedException cuando una fila no cabe en su búfer. Las dos
            // son fallos de archivo y no de fila: pasó la sonda de §3.2 sobre los primeros 8 KB y
            // se rompe más adelante, así que no hay resumen que producir. Sale con el mismo
            // FILE_TYPE_NOT_ALLOWED, que es lo que §3.2 ya dice de un texto sin un separador
            // consistente.
            return Result.Failure<IReadOnlyList<ParsedUploadRow>, BaseError>(RecipientUploadErrors.FileTypeNotAllowed());
        }
        finally
        {
            content.Position = start;
        }
    }

    /// <summary>
    /// Arma una fila con los campos que el encabezado sí reconoció, o <c>null</c> si la fila está
    /// entera vacía. Una línea en blanco al final de un csv no es una fila de datos mal formada.
    /// </summary>
    private static ParsedUploadRow? ReadRow<TKey>(
        int rowNumber,
        Func<TKey, string?> read,
        IReadOnlyDictionary<TKey, UploadColumn> columns)
        where TKey : notnull
    {
        var values = new Dictionary<UploadColumn, string?>();

        foreach (var (key, column) in columns)
        {
            var value = read(key);

            // Una columna repetida en el encabezado no se pisa con un vacío: gana el primer
            // valor que diga algo.
            if (!string.IsNullOrWhiteSpace(value) || !values.ContainsKey(column))
                values[column] = value;
        }

        if (values.Values.All(string.IsNullOrWhiteSpace))
            return null;

        return new ParsedUploadRow(
            rowNumber,
            values.GetValueOrDefault(UploadColumn.FirstName),
            values.GetValueOrDefault(UploadColumn.LastName),
            values.GetValueOrDefault(UploadColumn.Email),
            values.GetValueOrDefault(UploadColumn.Phone),
            values.GetValueOrDefault(UploadColumn.Company));
    }

    /// <summary>Los títulos de un xlsx, que MiniExcel entrega con claves A, B, C…</summary>
    private static Dictionary<string, UploadColumn> MapColumns(IDictionary<string, object> header)
    {
        var columns = new Dictionary<string, UploadColumn>(StringComparer.Ordinal);

        foreach (var (key, value) in header)
        {
            if (UploadColumnMap.Resolve(Text(value)) is { } column)
                columns.TryAdd(key, column);
        }

        return columns;
    }

    /// <summary>El valor de una celda por su clave, o nulo si esa fila no la trae.</summary>
    private static object? Cell(IDictionary<string, object> row, string key) =>
        row.TryGetValue(key, out var value) ? value : null;

    /// <summary>Los títulos de un csv, que llegan por posición.</summary>
    private static Dictionary<int, UploadColumn> MapColumns(IReadOnlyList<string> header)
    {
        var columns = new Dictionary<int, UploadColumn>();

        for (var index = 0; index < header.Count; index++)
        {
            if (UploadColumnMap.Resolve(header[index]) is { } column)
                columns.TryAdd(index, column);
        }

        return columns;
    }

    /// <summary>
    /// El texto de una celda. Un Excel devuelve números y fechas como tales, y un teléfono
    /// guardado como número tiene que llegar como el texto que una persona escribió.
    /// </summary>
    private static string? Text(object? value)
    {
        var text = value switch
        {
            null => null,
            string s => s,
            DateTime date => date.ToString("O"),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}
