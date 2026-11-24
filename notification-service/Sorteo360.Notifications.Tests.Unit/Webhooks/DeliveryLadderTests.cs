using Sorteo360.Notifications.Core.Notifications;
using Sorteo360.Notifications.Core.Webhooks;
using Shouldly;

namespace Sorteo360.Notifications.Tests.Unit.Webhooks;

/// <summary>
/// Cómo un evento mueve el estado de una fila, y cuándo no lo mueve. El desorden de los eventos es
/// estructural, así que todo esto es el caso corriente y no el borde.
/// </summary>
public sealed class DeliveryLadderTests
{
    [Theory]
    [InlineData(SendGridEventTypes.Processed, DeliveryStatus.Sent)]
    [InlineData(SendGridEventTypes.Deferred, DeliveryStatus.Deferred)]
    [InlineData(SendGridEventTypes.Delivered, DeliveryStatus.Delivered)]
    [InlineData(SendGridEventTypes.Bounce, DeliveryStatus.Bounced)]
    [InlineData(SendGridEventTypes.Dropped, DeliveryStatus.Dropped)]
    public void DeliveryStatusFor_ReturnsItsDeliveryStatus_WhenGivenAnEventType(string eventType, string expected) =>
        DeliveryLadder.DeliveryStatusFor(eventType).ShouldBe(expected);

    [Fact]
    public void DeliveryStatusFor_ReturnsNothing_WhenTheEventIsAnUnsubscribe() =>
        // Una baja habla de la persona, no del mensaje.
        DeliveryLadder.DeliveryStatusFor(SendGridEventTypes.Unsubscribe).ShouldBeNull();

    [Fact]
    public void DeliveryStatusFor_ReturnsNothing_WhenTheEventTypeIsUnrecognised() =>
        // Guardado e inerte: el proveedor agrega tipos cuando quiere.
        DeliveryLadder.DeliveryStatusFor("open").ShouldBeNull();

    [Fact]
    public void Advance_ReturnsDelivered_WhenALateProcessedArrives() =>
        DeliveryLadder.Advance(DeliveryStatus.Delivered, DeliveryLadder.DeliveryStatusFor(SendGridEventTypes.Processed))
            .ShouldBe(DeliveryStatus.Delivered);

    [Fact]
    public void Advance_ReturnsDelivered_WhenALateDeferredArrives() =>
        DeliveryLadder.Advance(DeliveryStatus.Delivered, DeliveryStatus.Deferred)
            .ShouldBe(DeliveryStatus.Delivered);

    [Fact]
    public void Advance_ReturnsBounced_WhenABounceFollowsADelivered() =>
        DeliveryLadder.Advance(DeliveryStatus.Delivered, DeliveryStatus.Bounced)
            .ShouldBe(DeliveryStatus.Bounced);

    [Fact]
    public void Advance_ReturnsDelivered_WhenADeliveredFollowsADeferred() =>
        DeliveryLadder.Advance(DeliveryStatus.Deferred, DeliveryStatus.Delivered)
            .ShouldBe(DeliveryStatus.Delivered);

    [Fact]
    public void Advance_ReturnsDeferred_WhenADeferredFollowsASent() =>
        DeliveryLadder.Advance(DeliveryStatus.Sent, DeliveryStatus.Deferred)
            .ShouldBe(DeliveryStatus.Deferred);

    [Fact]
    public void Advance_ReturnsTheTerminalStatus_WhenOurOwnSmtpLegKilledTheRow() =>
        // 'failed' es nuestra transacción SMTP muriendo: el mensaje nunca llegó al proveedor, así
        // que un evento suyo sobre esa fila solo puede venir de una correlación equivocada.
        DeliveryLadder.Advance(DeliveryStatus.Failed, DeliveryStatus.Delivered)
            .ShouldBe(DeliveryStatus.Failed);

    [Fact]
    public void Advance_ReturnsTheCurrentStatus_WhenTheEventAssertsNothing() =>
        DeliveryLadder.Advance(DeliveryStatus.Sent, incoming: null).ShouldBe(DeliveryStatus.Sent);

    [Fact]
    public void DeliveryStatusFor_ReturnsDelivered_WhenTheEventIsASpamreport() =>
        // Normativo: entrega y reacción son dos hechos distintos, y un reporte de spam prueba el
        // primero. No se puede reportar como spam algo que no llegó.
        DeliveryLadder.DeliveryStatusFor(SendGridEventTypes.SpamReport).ShouldBe(DeliveryStatus.Delivered);

    [Fact]
    public void Advance_ReturnsDelivered_WhenASpamreportFollowsADelivered() =>
        DeliveryLadder.Advance(DeliveryStatus.Delivered, DeliveryLadder.DeliveryStatusFor(SendGridEventTypes.SpamReport))
            .ShouldBe(DeliveryStatus.Delivered);

    [Fact]
    public void RecipientActionFor_ReturnsComplaint_WhenTheEventIsASpamreport() =>
        DeliveryLadder.RecipientActionFor(SendGridEventTypes.SpamReport).ShouldBe(RecipientAction.Complained);

    [Fact]
    public void RecipientActionFor_ReturnsUnsubscription_WhenTheEventIsAnUnsubscribe() =>
        DeliveryLadder.RecipientActionFor(SendGridEventTypes.Unsubscribe).ShouldBe(RecipientAction.Unsubscribed);

    [Fact]
    public void RecipientActionFor_ReturnsNothing_WhenTheEventIsADelivered() =>
        DeliveryLadder.RecipientActionFor(SendGridEventTypes.Delivered).ShouldBeNull();

    [Fact]
    public void AdvanceAction_ReturnsComplaint_WhenAComplaintFollowsAnUnsubscription() =>
        // Quien reporta spam además se dio de baja de hecho; la vuelta no es cierta.
        DeliveryLadder.AdvanceAction(RecipientAction.Unsubscribed, RecipientAction.Complained)
            .ShouldBe(RecipientAction.Complained);

    [Fact]
    public void AdvanceAction_ReturnsComplaint_WhenAnUnsubscriptionFollowsAComplaint() =>
        DeliveryLadder.AdvanceAction(RecipientAction.Complained, RecipientAction.Unsubscribed)
            .ShouldBe(RecipientAction.Complained);

    [Theory]
    [InlineData(SendGridEventTypes.Unsubscribe)]
    [InlineData(SendGridEventTypes.SpamReport)]
    public void SuppressionReasonFor_ReturnsTheEventType_WhenThePersonAskedForIt(string eventType) =>
        DeliveryLadder.SuppressionReasonFor(eventType).ShouldBe(eventType);

    [Theory]
    [InlineData(SendGridEventTypes.Bounce)]
    [InlineData(SendGridEventTypes.Dropped)]
    [InlineData(SendGridEventTypes.Delivered)]
    [InlineData("open")]
    public void SuppressionReasonFor_ReturnsNull_WhenTheEventIsAMailboxFact(string eventType) =>
        // Un rebote no entra en la lista de supresión desde acá: confundir el buzón con el
        // consentimiento haría que una dirección mal escrita se viera como una baja.
        DeliveryLadder.SuppressionReasonFor(eventType).ShouldBeNull();

    [Fact]
    public void Subscribed_ReturnsTheSevenConsoleEventTypes_WhenRead() =>
        // Los siete de la especificación. El test los fija acá porque la lista también es una
        // instrucción de configuración en la consola del proveedor (ver el runbook de despliegue):
        // si alguien agrega uno al código, hay que encenderlo allá, y al revés.
        SendGridEventTypes.Subscribed.ShouldBe(
            new[]
            {
                "processed", "dropped", "delivered", "deferred", "bounce", "spamreport", "unsubscribe"
            },
            ignoreOrder: true);

    [Fact]
    public void Subscribed_ReturnsOnlyTypesThatAssertSomething_WhenEachIsLadderChecked() =>
        // Lo que hace que un tipo desconocido quede guardado e inerte: los siete suscritos mueven
        // algo —la entrega, la reacción, o las dos— y nada fuera de esa lista mueve nada.
        SendGridEventTypes.Subscribed.ShouldAllBe(eventType =>
            DeliveryLadder.DeliveryStatusFor(eventType) != null
            || DeliveryLadder.RecipientActionFor(eventType) != null);
}
