using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using MiniExcelLibs;

namespace Sorteo360.Convocatorias.Tests.Integration.Infrastructure;

/// <summary>
/// Archivos de verdad para los tests de carga de destinatarios, y la forma multipart en la que
/// llegan al endpoint. Se generan en vez de guardarse como recursos para que lo que se prueba sea
/// lo que un Excel escribe hoy, y para que la bomba zip no viaje en el repositorio.
/// </summary>
internal static class UploadFiles
{
    public const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public const string CsvContentType = "text/csv";

    /// <summary>Los cinco encabezados del mapeo provisional (spec <c>05</c> §8).</summary>
    private static readonly string[] Headers = ["Nombre", "Apellido", "Correo", "Teléfono", "Empresa"];

    /// <summary>Una fila de hoja, con solo lo que hace falta nombrar en un test.</summary>
    public sealed record Row(string? FirstName, string? LastName, string? Email, string? Phone = null, string? Company = null);

    /// <summary>Una fila corriente: nombre, apellido y dirección.</summary>
    public static Row Person(string firstName, string lastName, string email) => new(firstName, lastName, email);

    public static MemoryStream Xlsx(params Row[] rows)
    {
        var records = rows
            .Select(row => new Dictionary<string, object?>
            {
                [Headers[0]] = row.FirstName,
                [Headers[1]] = row.LastName,
                [Headers[2]] = row.Email,
                [Headers[3]] = row.Phone,
                [Headers[4]] = row.Company,
            })
            .ToList();

        var stream = new MemoryStream();
        MiniExcel.SaveAs(stream, records, printHeader: true, excelType: ExcelType.XLSX);
        stream.Position = 0;

        return stream;
    }

    public static MemoryStream Csv(params Row[] rows) => Csv(Encoding.UTF8, ',', rows);

    public static MemoryStream Csv(Encoding encoding, char delimiter, params Row[] rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(delimiter, Headers));

        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(delimiter, new[]
            {
                Quote(row.FirstName, delimiter),
                Quote(row.LastName, delimiter),
                Quote(row.Email, delimiter),
                Quote(row.Phone, delimiter),
                Quote(row.Company, delimiter),
            }));
        }

        return new MemoryStream(encoding.GetBytes(builder.ToString()));
    }

    /// <summary>
    /// Un zip que empieza por los mismos cuatro bytes que un xlsx y no lo es: es el caso que la
    /// firma sola no distingue.
    /// </summary>
    public static MemoryStream Docx()
    {
        var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "[Content_Types].xml", "<Types />");
            Write(archive, "word/document.xml", "<w:document />");
        }

        stream.Position = 0;
        return stream;
    }

    /// <summary>Un libro que se abre, trae su <c>xl/workbook.xml</c> y se expande muchísimo.</summary>
    public static MemoryStream ZipBomb(long decompressedBytes)
    {
        var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "xl/workbook.xml", "<workbook />");

            var entry = archive.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.SmallestSize);
            using var entryStream = entry.Open();

            var chunk = new byte[1024 * 1024];
            var written = 0L;

            while (written < decompressedBytes)
            {
                var size = (int)Math.Min(chunk.Length, decompressedBytes - written);
                entryStream.Write(chunk, 0, size);
                written += size;
            }
        }

        stream.Position = 0;
        return stream;
    }

    /// <summary>El cuerpo multipart que espera el endpoint: un solo campo, <c>file</c>.</summary>
    public static MultipartFormDataContent AsForm(Stream content, string fileName, string contentType)
    {
        var part = new StreamContent(content);
        part.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        return new MultipartFormDataContent { { part, "file", fileName } };
    }

    private static void Write(ZipArchive archive, string name, string content)
    {
        using var entryStream = archive.CreateEntry(name).Open();
        entryStream.Write(Encoding.UTF8.GetBytes(content));
    }

    private static string Quote(string? value, char delimiter)
    {
        if (value is null)
            return string.Empty;

        return value.Contains(delimiter) || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
