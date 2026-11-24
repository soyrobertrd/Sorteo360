using System.Text;
using Sorteo360.Convocatorias.Core.Uploads;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Uploads;

/// <summary>
/// La validación de archivo de la spec <c>05</c> §3 y los tres topes de §7. Todo se comprueba
/// antes de escribir nada, así que un archivo rechazado no deja ninguna fila detrás.
/// </summary>
public sealed class SpreadsheetUploadPolicyTests
{
    private static readonly SpreadsheetUploadLimits Limits = new();
    private static readonly SpreadsheetUploadPolicy Policy = new(Limits);

    static SpreadsheetUploadPolicyTests()
    {
        // Lo mismo que hace Program.cs al arrancar (spec 00 §8): sin esto el respaldo a
        // Windows-1252 de §3.2 tira NotSupportedException en Linux.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [Fact]
    public async Task Inspect_ReturnsXlsx_WhenGivenARealWorkbook()
    {
        using var file = SampleFiles.Xlsx(["Ana", "Pérez", "ana@acme.com", "809", "Acme"]);

        var inspected = await Policy.InspectAsync(file, "lista.xlsx", XlsxContentType, CancellationToken.None);

        inspected.IsSuccess.ShouldBeTrue();
        inspected.Value.Kind.ShouldBe(SpreadsheetKind.Xlsx);
        inspected.Value.Encoding.ShouldBe(SpreadsheetFormat.Utf8);
    }

    /// <summary>
    /// La firma sola no alcanza: docx, pptx, jar y apk empiezan por los mismos cuatro bytes. Lo
    /// que separa a un libro es que traiga <c>xl/workbook.xml</c>.
    /// </summary>
    [Fact]
    public async Task Inspect_ReturnsFileTypeNotAllowed_WhenADocxIsRenamedXlsx()
    {
        using var file = SampleFiles.Docx();

        var inspected = await Policy.InspectAsync(file, "lista.xlsx", XlsxContentType, CancellationToken.None);

        inspected.IsFailure.ShouldBeTrue();
        inspected.Error.Code.ShouldBe("FILE_TYPE_NOT_ALLOWED");
    }

    [Fact]
    public async Task Inspect_ReturnsFileTooLarge_WhenTheFileExceedsTheSizeCap()
    {
        using var file = new MemoryStream(new byte[Limits.MaxSizeBytes + 1]);

        var inspected = await Policy.InspectAsync(file, "lista.csv", "text/csv", CancellationToken.None);

        inspected.IsFailure.ShouldBeTrue();
        inspected.Error.Code.ShouldBe("FILE_TOO_LARGE");
    }

    [Fact]
    public async Task Inspect_ReturnsCsvWithItsEncodingAndDelimiter_WhenGivenAUtf8Csv()
    {
        using var file = SampleFiles.Csv(Encoding.UTF8, ',', ["Ana", "Pérez", "ana@acme.com", "809", "Acme"]);

        var inspected = await Policy.InspectAsync(file, "lista.csv", "text/csv", CancellationToken.None);

        inspected.IsSuccess.ShouldBeTrue();
        inspected.Value.Kind.ShouldBe(SpreadsheetKind.Csv);
        inspected.Value.Encoding.ShouldBe(SpreadsheetFormat.Utf8);
        inspected.Value.Delimiter.ShouldBe(',');
    }

    /// <summary>
    /// La decodificación estricta en UTF-8 falla sobre la tilde de un archivo que exportó un
    /// Excel en español, y el respaldo se anuncia en vez de escribir mojibake en silencio.
    /// </summary>
    [Fact]
    public async Task Inspect_ReturnsTheWindows1252Fallback_WhenTheCsvIsNotUtf8()
    {
        using var file = SampleFiles.Csv(Encoding.GetEncoding(1252), ';', ["José", "Martínez", "jose@acme.com", "809", "Acme"]);

        var inspected = await Policy.InspectAsync(file, "lista.csv", "text/csv", CancellationToken.None);

        inspected.IsSuccess.ShouldBeTrue();
        inspected.Value.Encoding.ShouldBe(SpreadsheetFormat.Windows1252);
        inspected.Value.Delimiter.ShouldBe(';');
    }

    /// <summary>Una línea suelta sin separador no es una tabla, y un csv no tiene bytes mágicos.</summary>
    [Fact]
    public async Task Inspect_ReturnsFileTypeNotAllowed_WhenTheTextHasNoConsistentDelimiter()
    {
        using var file = new MemoryStream(Encoding.UTF8.GetBytes("esto es un texto cualquiera\nsin columnas\n"));

        var inspected = await Policy.InspectAsync(file, "lista.csv", "text/csv", CancellationToken.None);

        inspected.IsFailure.ShouldBeTrue();
        inspected.Error.Code.ShouldBe("FILE_TYPE_NOT_ALLOWED");
    }

    /// <summary>
    /// Lo declarado no decide nada, pero contradecir sí se rechaza: un nombre que miente sobre lo
    /// que hay dentro es el que después se procesa confiando en el nombre.
    /// </summary>
    [Fact]
    public async Task Inspect_ReturnsFileTypeNotAllowed_WhenAWorkbookIsSentAsCsv()
    {
        using var file = SampleFiles.Xlsx(["Ana", "Pérez", "ana@acme.com", null, null]);

        var inspected = await Policy.InspectAsync(file, "lista.csv", "text/csv", CancellationToken.None);

        inspected.IsFailure.ShouldBeTrue();
        inspected.Error.Code.ShouldBe("FILE_TYPE_NOT_ALLOWED");
    }

    /// <summary>No declarar nada no contradice: quien no afirma no se equivoca.</summary>
    [Fact]
    public async Task Inspect_ReturnsSuccess_WhenNothingIsDeclared()
    {
        using var file = SampleFiles.Xlsx(["Ana", "Pérez", "ana@acme.com", null, null]);

        var inspected = await Policy.InspectAsync(file, "lista", string.Empty, CancellationToken.None);

        inspected.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void CheckRowCount_ReturnsTooManyRowsNamingTheCap_WhenOneRowPastTheCap()
    {
        var refused = Policy.CheckRowCount(Limits.MaxDataRows + 1);

        refused.IsFailure.ShouldBeTrue();
        refused.Error.Code.ShouldBe("UPLOAD_TOO_MANY_ROWS");
        refused.Error.Message.ShouldContain(Limits.MaxDataRows.ToString());

        Policy.CheckRowCount(Limits.MaxDataRows).IsSuccess.ShouldBeTrue();
    }

    /// <summary>La inspección deja el contenido donde estaba, porque después se parsea.</summary>
    [Fact]
    public async Task Inspect_ReturnsTheContentRewound_WhenItHasRead()
    {
        using var file = SampleFiles.Csv(Encoding.UTF8, ',', ["Ana", "Pérez", "ana@acme.com", null, null]);

        await Policy.InspectAsync(file, "lista.csv", "text/csv", CancellationToken.None);

        file.Position.ShouldBe(0);
    }

    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}
