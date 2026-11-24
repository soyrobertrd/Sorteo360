using Shouldly;

namespace Sorteo360.Notifications.Tests.Integration.Infrastructure;

/// <summary>
/// La pata SMTP simulada hace lo que promete: acepta, guarda, y falla con el código que se
/// le pide. Todavía no hay worker que la use —llega con la especificación 03—, así que acá
/// se la ejercita directamente: el día que exista, un fallo señala al worker y no al doble.
/// </summary>
public sealed class FakeSmtpTransportSmokeTests
{
    [Fact]
    public void Send_ReturnsTheQueuedAsTokenAndRecordsTheMessage_WhenAccepted()
    {
        var transport = new FakeSmtpTransport();

        var response = transport.Send(Message("maria@sorteo360.local"));

        response.ShouldStartWith("250 Ok: queued as ");
        transport.Sent.ShouldHaveSingleItem().ToAddress.ShouldBe("maria@sorteo360.local");
        transport.AttemptCount.ShouldBe(1);
    }

    [Fact]
    public void Send_ThrowsFakeSmtpExceptionAndRecordsNothing_WhenAFailureIsProgrammed()
    {
        var transport = new FakeSmtpTransport();
        transport.FailAlwaysWith(535, "Authentication failed");

        var failure = Should.Throw<FakeSmtpException>(() => transport.Send(Message("jose@sorteo360.local")));

        failure.StatusCode.ShouldBe(535);
        transport.Sent.ShouldBeEmpty();
        transport.AttemptCount.ShouldBe(1);
    }

    [Fact]
    public void Send_ReturnsSuccessOnTheRetry_WhenATransientFailureIsProgrammedToClear()
    {
        var transport = new FakeSmtpTransport();
        transport.FailNextWith(451, "Try again later");

        Should.Throw<FakeSmtpException>(() => transport.Send(Message("juan@expedia.com")));
        transport.Send(Message("juan@expedia.com"));

        transport.Sent.ShouldHaveSingleItem();
        transport.AttemptCount.ShouldBe(2);
    }

    private static FakeSmtpMessage Message(string to) => new(
        FromAddress: "no-reply@sorteo360.local",
        FromName: "MITUR",
        ToAddress: to,
        Subject: "Cotización pendiente de aprobación",
        HtmlBody: "<html><body>Hola</body></html>",
        TextBody: "Hola",
        Headers: new Dictionary<string, string>
        {
            ["X-SMTPAPI"] = """{"unique_args":{"recipient_id":"11111111-1111-1111-1111-111111111111"}}""",
        });
}
