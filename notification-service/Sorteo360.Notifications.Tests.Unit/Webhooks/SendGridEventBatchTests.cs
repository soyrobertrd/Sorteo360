using Sorteo360.Notifications.Core.Webhooks;
using Shouldly;

namespace Sorteo360.Notifications.Tests.Unit.Webhooks;

/// <summary>
/// El parseo del lote, que corre después de verificar la firma y nunca antes.
/// </summary>
public sealed class SendGridEventBatchTests
{
    private static readonly Guid Recipient = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void TryParse_ReturnsOneEventPerEntry_WhenTheBatchIsValid()
    {
        var events = SendGridEventBatch.TryParse(
            """
            [
              {"event":"processed","email":"maria@sorteo360.local","timestamp":1757800000,"sg_event_id":"a"},
              {"event":"delivered","email":"maria@sorteo360.local","timestamp":1757800060,"sg_event_id":"b"}
            ]
            """);

        events.ShouldNotBeNull();
        events.Count.ShouldBe(2);
        events[0].EventType.ShouldBe("processed");
        events[0].SgEventId.ShouldBe("a");
        events[1].EventType.ShouldBe("delivered");
    }

    [Fact]
    public void TryParse_ReturnsOurGuid_WhenItIsFlattenedAtTheTopLevel()
    {
        // El proveedor aplana los unique_args como campos del evento, así que ahí se busca primero.
        var events = SendGridEventBatch.TryParse(
            $$"""[{"event":"delivered","email":"m@sorteo360.local","timestamp":1,"recipient_id":"{{Recipient}}"}]""");

        events!.Single().CorrelationId.ShouldBe(Recipient);
    }

    [Fact]
    public void TryParse_ReturnsOurGuid_WhenItIsNestedInUniqueArgs()
    {
        var events = SendGridEventBatch.TryParse(
            """[{"event":"delivered","email":"m@sorteo360.local","timestamp":1,"unique_args":{"recipient_id":"ID"}}]"""
                .Replace("ID", Recipient.ToString(), StringComparison.Ordinal));

        events!.Single().CorrelationId.ShouldBe(Recipient);
    }

    [Fact]
    public void TryParse_ReturnsNoRecipientId_WhenTheIdIsNotAGuid()
    {
        // Cae al segundo peldaño de la escalera en vez de romper el lote entero.
        var events = SendGridEventBatch.TryParse(
            """[{"event":"bounce","email":"m@sorteo360.local","timestamp":1,"recipient_id":"no-soy-un-guid"}]""");

        events!.Single().CorrelationId.ShouldBeNull();
    }

    [Fact]
    public void TryParse_ReturnsTheEvent_WhenTheTypeIsUnrecognised() =>
        // Se guarda, y se ignora al derivar el estado. Es lo que evita una migración el día que
        // alguien encienda la apertura o el clic.
        SendGridEventBatch.TryParse("""[{"event":"open","email":"m@sorteo360.local","timestamp":1}]""")!
            .Single().EventType.ShouldBe("open");

    [Fact]
    public void TryParse_ReturnsTheRawJsonOfEachEntry_WhenParsed()
    {
        var events = SendGridEventBatch.TryParse(
            """[{"event":"bounce","email":"m@sorteo360.local","timestamp":1,"reason":"550 no such user"}]""");

        // Lo que hoy no se lee es lo que mañana explica un evento raro sin pedírselo otra vez al
        // proveedor.
        events!.Single().Payload.ShouldContain("550 no such user");
    }

    [Fact]
    public void TryParse_ReturnsADate_WhenTheEntryHasATimestamp() =>
        SendGridEventBatch.TryParse("""[{"event":"delivered","email":"m@sorteo360.local","timestamp":1757800000}]""")!
            .Single().OccurredAt.ShouldBe(DateTimeOffset.FromUnixTimeSeconds(1757800000));

    [Fact]
    public void TryParse_ReturnsAnEmptyAddress_WhenTheEmailIsMissing() =>
        // Un evento sin dirección no se puede correlacionar por dirección, pero se guarda igual.
        SendGridEventBatch.TryParse("""[{"event":"bounce","timestamp":1}]""")!
            .Single().Email.ShouldBe(string.Empty);

    [Theory]
    [InlineData("no es json")]
    [InlineData("{}")]
    [InlineData("""{"event":"delivered"}""")]
    [InlineData("""["cadena suelta"]""")]
    [InlineData("""[{"email":"m@sorteo360.local","timestamp":1}]""")]
    [InlineData("""[{"event":"delivered","email":"m@sorteo360.local"}]""")]
    [InlineData("""[{"event":"delivered","email":"m@sorteo360.local","timestamp":"ayer"}]""")]
    public void TryParse_ReturnsFalse_WhenTheBatchIsMalformed(string rawBody) =>
        // Entero y no a medias: el reintento de 24 horas del proveedor lo vuelve a traer, así que
        // un 400 visible es mejor que guardar la mitad y perder el resto en silencio.
        SendGridEventBatch.TryParse(rawBody).ShouldBeNull();

    [Fact]
    public void TryParse_ReturnsFalse_WhenOneEntryIsBad() =>
        SendGridEventBatch.TryParse(
            """
            [
              {"event":"delivered","email":"m@sorteo360.local","timestamp":1},
              {"email":"m@sorteo360.local","timestamp":2}
            ]
            """).ShouldBeNull();

    [Fact]
    public void TryParse_ReturnsNoEvents_WhenTheBatchIsEmpty() =>
        SendGridEventBatch.TryParse("[]").ShouldBeEmpty();
}
