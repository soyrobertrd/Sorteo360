using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Infrastructure.NotarialRecords;
using Net.Codecrete.QrCodeGenerator;

namespace Sorteo360.Events.Infrastructure.Raffles;

/// <summary>
/// Renderiza credenciales QR en PNG (sin dependencias nativas) y gafetes imprimibles en PDF con MigraDoc.
/// </summary>
public sealed class RaffleBadgeRenderer : IRaffleBadgeRenderer
{
    private const QrCode.Ecc ErrorCorrection = QrCode.Ecc.Quartile;
    private const int QuietZone = 4;
    private const int Scale = 10;

    public RaffleBadgeRenderer() => NotoSerifFontResolver.EnsureRegistered();

    public byte[] RenderQrPng(string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        return QrCode.EncodeText(payload, ErrorCorrection).ToPngBitmap(border: QuietZone, scale: Scale);
    }

    public byte[] RenderBadgesPdf(string eventName, IReadOnlyList<RaffleParticipant> participants)
    {
        ArgumentNullException.ThrowIfNull(participants);

        var title = string.IsNullOrWhiteSpace(eventName) ? "Sorteo Corporativo" : eventName.Trim();

        var document = new Document();
        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = NotoSerifFontResolver.FamilyName;
        normal.Font.Size = 9;

        var section = document.AddSection();
        section.PageSetup = document.DefaultPageSetup.Clone();
        section.PageSetup.PageFormat = PageFormat.Letter;
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.5);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.5);
        section.PageSetup.TopMargin = Unit.FromCentimeter(1.5);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1.5);

        var heading = section.AddParagraph($"Gafetes QR — {title}");
        heading.Format.Font.Bold = true;
        heading.Format.Font.Size = 13;
        heading.Format.SpaceAfter = Unit.FromPoint(8);

        if (participants.Count == 0)
        {
            section.AddParagraph("No hay participantes registrados en este sorteo.");
        }
        else
        {
            var table = section.AddTable();
            table.Borders.Width = 0.5;
            table.Borders.Color = Colors.LightGray;
            table.LeftPadding = Unit.FromPoint(8);
            table.RightPadding = Unit.FromPoint(8);
            table.AddColumn(Unit.FromCentimeter(9.1));
            table.AddColumn(Unit.FromCentimeter(9.1));

            for (var i = 0; i < participants.Count; i += 2)
            {
                var row = table.AddRow();
                row.TopPadding = Unit.FromPoint(8);
                row.BottomPadding = Unit.FromPoint(8);

                FillBadgeCell(row.Cells[0], title, participants[i]);
                if (i + 1 < participants.Count)
                {
                    FillBadgeCell(row.Cells[1], title, participants[i + 1]);
                }
            }
        }

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();

        using var output = new MemoryStream();
        renderer.PdfDocument.Save(output, closeStream: false);
        return output.ToArray();
    }

    private void FillBadgeCell(Cell cell, string eventTitle, RaffleParticipant participant)
    {
        var eventLine = cell.AddParagraph(eventTitle);
        eventLine.Format.Alignment = ParagraphAlignment.Center;
        eventLine.Format.Font.Size = 7.5;
        eventLine.Format.Font.Color = Colors.SlateGray;
        eventLine.Format.SpaceAfter = Unit.FromPoint(2);

        var name = cell.AddParagraph(participant.FullName);
        name.Format.Alignment = ParagraphAlignment.Center;
        name.Format.Font.Bold = true;
        name.Format.Font.Size = 11;
        name.Format.SpaceAfter = Unit.FromPoint(2);

        var detailParts = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(participant.Company))
            detailParts.Add(participant.Company);
        if (!string.IsNullOrWhiteSpace(participant.Department))
            detailParts.Add(participant.Department);

        // El correo interno de quien llegó sin correo no se imprime en el gafete.
        var orgText = detailParts.Count > 0 ? string.Join(" · ", detailParts)
            : RaffleParticipant.IsSyntheticEmail(participant.EmailNormalized) ? string.Empty
            : participant.EmailNormalized;
        var org = cell.AddParagraph(orgText);
        org.Format.Alignment = ParagraphAlignment.Center;
        org.Format.Font.Size = 8;
        org.Format.Font.Color = Colors.DimGray;
        org.Format.SpaceAfter = Unit.FromPoint(6);

        var qrPng = RenderQrPng(participant.QrCode);
        var imageParagraph = cell.AddParagraph();
        imageParagraph.Format.Alignment = ParagraphAlignment.Center;
        var image = imageParagraph.AddImage($"base64:{Convert.ToBase64String(qrPng)}");
        image.Width = Unit.FromCentimeter(3.4);
        image.LockAspectRatio = true;

        var codeText = string.IsNullOrWhiteSpace(participant.EmployeeCode)
            ? participant.QrCode
            : $"{participant.QrCode} · {participant.EmployeeCode}";
        var code = cell.AddParagraph(codeText);
        code.Format.Alignment = ParagraphAlignment.Center;
        code.Format.Font.Size = 7.5;
        code.Format.SpaceBefore = Unit.FromPoint(3);

        if (!string.IsNullOrWhiteSpace(participant.TicketNumber))
        {
            var ticket = cell.AddParagraph($"Boleto #{participant.TicketNumber}");
            ticket.Format.Alignment = ParagraphAlignment.Center;
            ticket.Format.Font.Bold = true;
            ticket.Format.Font.Size = 10;
            ticket.Format.SpaceBefore = Unit.FromPoint(3);
        }
    }
}

