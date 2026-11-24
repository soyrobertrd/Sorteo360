using System.Text;
using Sorteo360.Convocatorias.Core.Uploads;
using Sorteo360.Convocatorias.Infrastructure.Uploads;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Uploads;

/// <summary>
/// La lectura de una hoja ya admitida. El lector no juzga el contenido de una fila: eso es de
/// <see cref="UploadRowClassifier"/>. Sus dos únicos rechazos son de archivo.
/// </summary>
public sealed class SpreadsheetParserTests
{
    private static readonly SpreadsheetUploadLimits Limits = new();
    private static readonly SpreadsheetUploadPolicy Policy = new(Limits);
    private static readonly SpreadsheetParser Parser = new(Policy);

    static SpreadsheetParserTests() =>
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    [Fact]
    public async Task Parse_ReturnsTheFiveColumnsAndTheFileRowNumber_WhenGivenAWorkbook()
    {
        using var file = SampleFiles.Xlsx(["Ana", "Pérez", "ana@acme.com", "809-555-0100", "Acme"]);

        var rows = await ParseAsync(file, "lista.xlsx", XlsxContentType);

        rows.Count.ShouldBe(1);
        rows[0].SourceRowNumber.ShouldBe(2);
        rows[0].FirstName.ShouldBe("Ana");
        rows[0].LastName.ShouldBe("Pérez");
        rows[0].Email.ShouldBe("ana@acme.com");
        rows[0].Phone.ShouldBe("809-555-0100");
        rows[0].Company.ShouldBe("Acme");
    }

    [Fact]
    public async Task Parse_ReturnsTheSameShape_WhenGivenACsv()
    {
        using var file = SampleFiles.Csv(
            Encoding.UTF8,
            ',',
            ["Ana", "Pérez", "ana@acme.com", "809", "Acme, S. A."],
            ["Luis", "Gómez", "luis@acme.com", null, null]);

        var rows = await ParseAsync(file, "lista.csv", "text/csv");

        rows.Count.ShouldBe(2);
        rows[0].SourceRowNumber.ShouldBe(2);
        rows[1].SourceRowNumber.ShouldBe(3);

        // Unescape en true: sin eso el campo entrecomillado llega con sus comillas dentro.
        rows[0].Company.ShouldBe("Acme, S. A.");
    }

    /// <summary>
    /// La marca de orden de bytes se pega al nombre de la primera columna si se lee por un
    /// <c>StringReader</c>, y esa columna deja de reconocerse (spec <c>05</c> §7.2).
    /// </summary>
    [Fact]
    public async Task Parse_ReturnsTheFirstColumn_WhenTheCsvHasAByteOrderMark()
    {
        using var file = SampleFiles.CsvWithBom(["Ana", "Pérez", "ana@acme.com", null, null]);

        var rows = await ParseAsync(file, "lista.csv", "text/csv");

        rows.Count.ShouldBe(1);
        rows[0].FirstName.ShouldBe("Ana");
    }

    /// <summary>
    /// Los acentos sobreviven al respaldo. Es lo que separa "leído como Windows-1252" de escribir
    /// mojibake en el sistema de registro.
    /// </summary>
    [Fact]
    public async Task Parse_ReturnsAccentedNamesIntact_WhenTheCsvIsWindows1252()
    {
        using var file = SampleFiles.Csv(
            Encoding.GetEncoding(1252),
            ';',
            ["José", "Martínez", "jose@acme.com", null, "Añil"]);

        var rows = await ParseAsync(file, "lista.csv", "text/csv");

        rows[0].FirstName.ShouldBe("José");
        rows[0].LastName.ShouldBe("Martínez");
        rows[0].Company.ShouldBe("Añil");
    }

    /// <summary>Se parseó y no se reconoció ninguna columna. No es un archivo sin filas.</summary>
    [Fact]
    public async Task Parse_ReturnsNoHeaderRowFailure_WhenNoHeaderIsRecognised()
    {
        using var file = new MemoryStream(Encoding.UTF8.GetBytes("uno,dos,tres\na,b,c\n"));

        var format = await Policy.InspectAsync(file, "lista.csv", "text/csv", CancellationToken.None);
        format.IsSuccess.ShouldBeTrue();

        var parsed = await Parser.ParseAsync(file, format.Value, CancellationToken.None);

        parsed.IsFailure.ShouldBeTrue();
        parsed.Error.Code.ShouldBe("UPLOAD_NO_HEADER_ROW");
    }

    /// <summary>
    /// Un libro que se expande muchísimo más de lo que ocupa se corta mientras se lee, así que la
    /// bomba no llega a descomprimirse entera.
    /// </summary>
    [Fact]
    public async Task Parse_ReturnsExpandedTooLargeFailure_WhenTheWorkbookIsAZipBomb()
    {
        using var file = SampleFiles.ZipBomb(Limits.MaxDecompressedBytes + (8L * 1024 * 1024));

        var parsed = await Parser.ParseAsync(
            file,
            new SpreadsheetFormat(SpreadsheetKind.Xlsx, SpreadsheetFormat.Utf8),
            CancellationToken.None);

        parsed.IsFailure.ShouldBeTrue();
        parsed.Error.Code.ShouldBe("UPLOAD_EXPANDED_TOO_LARGE");
    }

    /// <summary>
    /// Regresión de la auditoría run-1 (confirmado 8): una celda en XFD tenía a MiniExcel más de
    /// 20 s por subida. Se rechaza midiendo la hoja, antes de parsearla.
    /// </summary>
    [Fact]
    public async Task Parse_ReturnsTooManyColumnsFast_WhenASheetReachesColumnXfd()
    {
        using var file = SampleFiles.WideXlsx("XFD", ["Ana", "Pérez", "ana@acme.com", null, null]);
        var started = System.Diagnostics.Stopwatch.StartNew();

        var parsed = await Parser.ParseAsync(
            file,
            new SpreadsheetFormat(SpreadsheetKind.Xlsx, SpreadsheetFormat.Utf8),
            CancellationToken.None);

        parsed.IsFailure.ShouldBeTrue();
        parsed.Error.Code.ShouldBe("UPLOAD_TOO_MANY_COLUMNS");
        started.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(1));
    }

    /// <summary>El tope deja pasar una hoja con columnas de sobra hasta el límite.</summary>
    [Fact]
    public async Task Parse_ReadsTheWorkbook_WhenItsWidestCellIsAtTheColumnLimit()
    {
        // AX es la columna 50.
        using var file = SampleFiles.WideXlsx("AX", ["Ana", "Pérez", "ana@acme.com", null, null]);

        var rows = await ParseAsync(file, "lista.xlsx", XlsxContentType);

        rows.ShouldHaveSingleItem().Email.ShouldBe("ana@acme.com");
    }

    /// <summary>
    /// Una hoja legítima y ancha pasa: el presupuesto caza una expansión de cien veces, no un
    /// archivo grande, que ya lo acotan el tamaño y las filas.
    /// </summary>
    [Fact]
    public async Task Parse_ReturnsEveryRow_WhenALegitimateWorkbookIsWithinTheBudget()
    {
        var rows = Enumerable.Range(0, 200)
            .Select(index => new string?[] { $"Nombre{index}", $"Apellido{index}", $"p{index}@acme.com", "809", "Acme" })
            .ToArray();

        using var file = SampleFiles.Xlsx(rows);

        var parsed = await ParseAsync(file, "lista.xlsx", XlsxContentType);

        parsed.Count.ShouldBe(200);
    }

    /// <summary>
    /// Una dirección con un salto de línea entrecomillado parte el archivo en dos líneas sueltas
    /// al mirarlo por <c>\n</c>, ninguna con la cuenta de campos del encabezado. §3.2 pide "al
    /// menos una línea" consistente, no todas, así que el archivo se admite y se lee entero.
    /// </summary>
    [Fact]
    public async Task Parse_ReturnsEveryRow_WhenAFieldHasAQuotedNewline()
    {
        using var file = new MemoryStream(Encoding.UTF8.GetBytes(
            "Nombre,Apellido,Correo,Empresa\n" +
            "Ana,Pérez,ana@acme.com,\"Acme\nCalle 1\"\n" +
            "Luis,Gómez,luis@acme.com,Beta\n"));

        var rows = await ParseAsync(file, "lista.csv", "text/csv");

        rows.Count.ShouldBe(2);
        rows[0].Company.ShouldBe("Acme\nCalle 1");
        rows[1].Email.ShouldBe("luis@acme.com");
    }

    /// <summary>Una línea en blanco al final de un csv no es una fila de datos mal formada.</summary>
    [Fact]
    public async Task Parse_ReturnsOnlyDataRows_WhenTheCsvHasBlankLines()
    {
        using var file = new MemoryStream(Encoding.UTF8.GetBytes(
            "Nombre,Apellido,Correo\nAna,Pérez,ana@acme.com\n,,\n"));

        var rows = await ParseAsync(file, "lista.csv", "text/csv");

        rows.Count.ShouldBe(1);
    }

    private static async Task<IReadOnlyList<ParsedUploadRow>> ParseAsync(
        Stream content,
        string fileName,
        string contentType)
    {
        var format = await Policy.InspectAsync(content, fileName, contentType, CancellationToken.None);
        format.IsSuccess.ShouldBeTrue(format.IsFailure ? format.Error.Message : null);

        var parsed = await Parser.ParseAsync(content, format.Value, CancellationToken.None);
        parsed.IsSuccess.ShouldBeTrue(parsed.IsFailure ? parsed.Error.Message : null);

        return parsed.Value;
    }

    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}
