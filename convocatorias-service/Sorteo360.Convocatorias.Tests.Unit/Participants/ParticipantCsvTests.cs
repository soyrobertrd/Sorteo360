using System.Text;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Participants;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Participants;

/// <summary>El CSV de participantes (spec <c>07</c> §4): lo que Excel abre en la máquina de una OPT.</summary>
public sealed class ParticipantCsvTests
{
    private static readonly Guid FirstCampaign = Guid.Parse("0199a000-0000-7000-8000-000000000001");
    private static readonly Guid SecondCampaign = Guid.Parse("0199a000-0000-7000-8000-000000000002");

    private static readonly Participant Referred = new(
        "colega@empresa.com", "Luis", "Peña", "Empresa SRL", ParticipantOrigin.Referred, ParticipantSendStatus.None,
        Registered: true, new DateTimeOffset(2026, 9, 2, 10, 14, 0, TimeSpan.Zero), "REF-2026-00427", "Ana Gómez",
        [FirstCampaign, SecondCampaign], CrmSyncPending: true, DetailsDiffer: false)
    {
        CheckedInAt = new DateTimeOffset(2026, 9, 5, 18, 42, 11, 123, TimeSpan.Zero),
    };

    private static readonly Participant Invitee = new(
        "juan@empresa.com", "Juan", "Pérez", null, ParticipantOrigin.Invited, RecipientSendStatus.HandedOff,
        Registered: false, null, null, null, [FirstCampaign], CrmSyncPending: false, DetailsDiffer: true);

    [Fact]
    public async Task WriteAsync_ReturnsBomSpanishHeaderAndOneLinePerRow_WhenGivenTwoRows()
    {
        var bytes = await WriteAsync(Referred, Invitee);

        bytes.Take(3).ShouldBe(new byte[] { 0xEF, 0xBB, 0xBF });
        Encoding.UTF8.GetString(bytes[3..]).ShouldBe(
            "Correo,Nombre,Apellido,Empresa,Origen,Estado de envío,Inscrito,Fecha de inscripción,Referencia," +
            "Referido por,Campañas,Sincronización CRM pendiente,Datos distintos,Fecha de check-in\r\n" +
            "colega@empresa.com,Luis,Peña,Empresa SRL,referred,none,true,2026-09-02T10:14:00Z,REF-2026-00427," +
            "Ana Gómez,0199a000-0000-7000-8000-000000000001 0199a000-0000-7000-8000-000000000002,true,false," +
            "2026-09-05T18:42:11Z\r\n" +
            "juan@empresa.com,Juan,Pérez,,invited,handed_off,false,,,,0199a000-0000-7000-8000-000000000001,false,true,\r\n");
    }

    [Theory]
    [InlineData("Pérez, Gómez", "\"Pérez, Gómez\"")]
    [InlineData("Juan \"Juancho\"", "\"Juan \"\"Juancho\"\"\"")]
    [InlineData("Línea\nNueva", "\"Línea\nNueva\"")]
    public async Task WriteAsync_ReturnsAQuotedCell_WhenTheValueHoldsADelimiterQuoteOrLineBreak(string lastName, string expected)
    {
        var line = DataLine(await WriteAsync(Invitee with { LastName = lastName }));

        line.ShouldStartWith($"juan@empresa.com,Juan,{expected},");
    }

    /// <summary>
    /// El nombre lo teclea un desconocido en el portal público. Una celda que empieza por <c>=</c>
    /// sería una fórmula que Excel ejecuta al abrir el archivo en la máquina de la OPT.
    /// </summary>
    [Theory]
    [InlineData("=HYPERLINK(\"http://x\")", "\"'=HYPERLINK(\"\"http://x\"\")\"")]
    [InlineData("+1", "'+1")]
    [InlineData("-1", "'-1")]
    [InlineData("@SUM", "'@SUM")]
    public async Task WriteAsync_ReturnsAnInertCell_WhenTheValueStartsLikeAFormula(string firstName, string expected)
    {
        var line = DataLine(await WriteAsync(Invitee with { FirstName = firstName }));

        line.ShouldStartWith($"juan@empresa.com,{expected},Pérez,");
    }

    [Fact]
    public async Task WriteAsync_ReturnsOnlyTheHeader_WhenThereAreNoRows()
    {
        var text = Encoding.UTF8.GetString((await WriteAsync())[3..]);

        text.ShouldStartWith("Correo,");
        text.Count(c => c == '\n').ShouldBe(1);
    }

    /// <summary>Una exportación a las nueve de la noche en Santo Domingo lleva la fecha de ese día, no la de mañana.</summary>
    [Fact]
    public void FileName_ReturnsTheEventIdAndTheSantoDomingoDate_WhenItIsAlreadyTomorrowInUtc()
    {
        var eventId = Guid.Parse("0199a000-0000-7000-8000-0000000000aa");

        var name = ParticipantCsv.FileName(eventId, new DateTimeOffset(2026, 9, 23, 1, 0, 0, TimeSpan.Zero));

        name.ShouldBe("convocatoria-0199a000-0000-7000-8000-0000000000aa-2026-09-22.csv");
    }

    private static async Task<byte[]> WriteAsync(params Participant[] rows)
    {
        using var output = new MemoryStream();
        await ParticipantCsv.WriteAsync(rows, output, CancellationToken.None);
        return output.ToArray();
    }

    /// <summary>Todo lo que sigue a la cabecera, que termina en el primer CRLF.</summary>
    private static string DataLine(byte[] bytes)
    {
        var text = Encoding.UTF8.GetString(bytes[3..]);
        return text[(text.IndexOf("\r\n", StringComparison.Ordinal) + 2)..];
    }
}
