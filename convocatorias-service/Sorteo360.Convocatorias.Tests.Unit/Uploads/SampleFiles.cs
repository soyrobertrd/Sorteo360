using System.IO.Compression;
using System.Text;
using MiniExcelLibs;

namespace Sorteo360.Convocatorias.Tests.Unit.Uploads;

/// <summary>
/// Archivos de verdad para los tests de subida: un libro que MiniExcel escribe, csv en las dos
/// codificaciones, y los dos impostores que la política tiene que rechazar.
/// <para>
/// Se generan en vez de guardarse como recursos para que lo que se prueba sea lo que un Excel
/// escribe hoy y no una copia de hace un año, y para que la bomba zip no viaje en el repositorio.
/// </para>
/// </summary>
internal static class SampleFiles
{
    /// <summary>Los cinco encabezados del mapeo provisional, escritos como los escribe una persona.</summary>
    public static readonly string[] Headers = ["Nombre", "Apellido", "Correo", "Teléfono", "Empresa"];

    /// <summary>Un libro con esos encabezados y las filas que se le pasen.</summary>
    public static MemoryStream Xlsx(params string?[][] rows)
    {
        var records = rows
            .Select(row => Headers
                .Select((header, index) => (header, value: (object?)row.ElementAtOrDefault(index)))
                .ToDictionary(pair => pair.header, pair => pair.value))
            .ToList();

        var stream = new MemoryStream();
        MiniExcel.SaveAs(stream, records, printHeader: true, excelType: ExcelType.XLSX);
        stream.Position = 0;

        return stream;
    }

    /// <summary>
    /// El libro de la auditoría run-1: el de <see cref="Xlsx"/> con una celda más en
    /// <paramref name="lastColumn"/> de la primera fila de datos, y la dimensión declarada hasta ahí.
    /// </summary>
    public static MemoryStream WideXlsx(string lastColumn, params string?[][] rows)
    {
        var stream = Xlsx(rows);

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("xl/worksheets/sheet1.xml")!;
            string xml;

            using (var reader = new StreamReader(entry.Open()))
                xml = reader.ReadToEnd();

            // MiniExcel escribe los elementos con prefijo ("x:row"), así que se respeta el que traiga.
            xml = System.Text.RegularExpressions.Regex.Replace(xml, "dimension ref=\"[^\"]*\"", $"dimension ref=\"A1:{lastColumn}2\"");
            var secondRowEnd = System.Text.RegularExpressions.Regex.Matches(xml, @"</(\w+:)?row>")[1];
            var p = secondRowEnd.Groups[1].Value;
            xml = xml.Insert(secondRowEnd.Index, $"<{p}c r=\"{lastColumn}2\" t=\"inlineStr\"><{p}is><{p}t>x</{p}t></{p}is></{p}c>");

            entry.Delete();
            using var writer = new StreamWriter(archive.CreateEntry("xl/worksheets/sheet1.xml").Open());
            writer.Write(xml);
        }

        stream.Position = 0;
        return stream;
    }

    /// <summary>Un csv con esos encabezados, en la codificación que se pida.</summary>
    public static MemoryStream Csv(Encoding encoding, char delimiter = ',', params string?[][] rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(delimiter, Headers));

        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(
                delimiter,
                Headers.Select((_, index) => Quote(row.ElementAtOrDefault(index), delimiter))));
        }

        return new MemoryStream(encoding.GetBytes(builder.ToString()));
    }

    /// <summary>El mismo csv precedido de la marca de orden de bytes que escribe un Excel.</summary>
    public static MemoryStream CsvWithBom(params string?[][] rows)
    {
        var body = Csv(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), ',', rows).ToArray();
        var bom = Encoding.UTF8.GetPreamble();

        return new MemoryStream([.. bom, .. body]);
    }

    /// <summary>
    /// Un zip que empieza por los mismos cuatro bytes que un xlsx y no lo es. Es el caso que la
    /// firma sola no distingue: docx, pptx, jar y apk empiezan todos igual.
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

    /// <summary>
    /// Un libro que se abre, trae su <c>xl/workbook.xml</c> y se descompone en mucho más de lo
    /// que ocupa. La expansión viene de una entrada de ceros, que el deflate reduce a casi nada.
    /// </summary>
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

    private static void Write(ZipArchive archive, string name, string content)
    {
        using var entryStream = archive.CreateEntry(name).Open();
        entryStream.Write(Encoding.UTF8.GetBytes(content));
    }

    /// <summary>Entrecomilla lo que lleve el separador dentro, como hace cualquier hoja.</summary>
    private static string Quote(string? value, char delimiter)
    {
        if (value is null)
            return string.Empty;

        return value.Contains(delimiter) || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
