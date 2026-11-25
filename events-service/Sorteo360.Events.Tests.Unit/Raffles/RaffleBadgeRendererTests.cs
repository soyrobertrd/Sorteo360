using System.Text;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Infrastructure.Raffles;
using Shouldly;
using UglyToad.PdfPig;

namespace Sorteo360.Events.Tests.Unit.Raffles;

public sealed class RaffleBadgeRendererTests
{
    private static readonly DateTimeOffset Now = new(2026, 12, 18, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RenderQrPng_ReturnsValidPngImage_WhenGivenPayload()
    {
        var renderer = new RaffleBadgeRenderer();

        var png = renderer.RenderQrPng("QR-TEST-1234");

        png.Length.ShouldBeGreaterThan(8);
        png[..4].ShouldBe([(byte)0x89, (byte)'P', (byte)'N', (byte)'G']);
    }

    [Fact]
    public void RenderBadgesPdf_ContainsEventNameParticipantAndQrCode_WhenParticipantsProvided()
    {
        var renderer = new RaffleBadgeRenderer();
        var participant = new RaffleParticipant(
            id: Guid.NewGuid(),
            eventId: Guid.NewGuid(),
            fullName: "Carlos Rodríguez",
            email: "carlos@empresa.do",
            company: "Empresa Navideña",
            department: "Tecnología",
            employeeCode: "EMP-101",
            phone: null,
            qrCode: "QR-TEST-1234",
            checkedIn: false,
            checkedInAt: null,
            source: "excel",
            createdAt: Now,
            updatedAt: Now,
            ticketNumber: "482-917");

        var pdf = renderer.RenderBadgesPdf("Fiesta Navidad 2026", [participant]);

        Encoding.ASCII.GetString(pdf, 0, 5).ShouldBe("%PDF-");
        using var document = PdfDocument.Open(pdf);
        var text = string.Join("\n", document.GetPages().Select(p =>
            string.Join(" ", p.GetWords().Select(w => w.Text))));
        text.ShouldContain("Fiesta Navidad 2026");
        text.ShouldContain("Carlos Rodríguez");
        text.ShouldContain("QR-TEST-1234");
        text.ShouldContain("482-917");
    }
}

