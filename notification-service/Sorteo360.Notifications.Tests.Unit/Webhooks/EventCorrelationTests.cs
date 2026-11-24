using Sorteo360.Notifications.Core.Notifications;
using Sorteo360.Notifications.Core.Webhooks;
using Shouldly;

namespace Sorteo360.Notifications.Tests.Unit.Webhooks;

/// <summary>
/// La escalera de tres peldaños: el GUID, la dirección, y el huérfano. Nunca adivinar, nunca
/// descartar.
/// </summary>
public sealed class EventCorrelationTests
{
    private static readonly Guid Recipient = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Other = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void Resolve_ReturnsTheRowOfOurGuid_WhenTheRowExists() =>
        EventCorrelation.Resolve(Recipient, correlationIdExists: true, []).ShouldBe(Recipient);

    [Fact]
    public void Resolve_ReturnsAnOrphan_WhenTheGuidIsUnknown() =>
        // Stage y producción comparten cuenta del proveedor: un GUID que este entorno nunca emitió
        // se guarda sin enlazar en vez de corromper una fila ajena.
        EventCorrelation.Resolve(Recipient, correlationIdExists: false, []).ShouldBeNull();

    [Fact]
    public void Resolve_ReturnsAnOrphanIgnoringTheAddress_WhenTheGuidIsUnknown()
    {
        // El evento sí nombra a alguien, solo que a nadie de acá. Buscar por dirección entonces
        // sería inventar un enlace que el propio evento contradice.
        EventCorrelation.Resolve(Recipient, correlationIdExists: false, [Other]).ShouldBeNull();
    }

    [Fact]
    public void Resolve_ReturnsTheRowOfTheAddress_WhenThereIsNoGuidAndExactlyOneOpenSend() =>
        EventCorrelation.Resolve(correlationId: null, correlationIdExists: false, [Recipient])
            .ShouldBe(Recipient);

    [Fact]
    public void Resolve_ReturnsAnOrphan_WhenThereIsNoGuidAndTwoOpenSends() =>
        // Elegir uno sería adivinar, y adivinar escribe una entrega falsa en el sistema de registro.
        EventCorrelation.Resolve(correlationId: null, correlationIdExists: false, [Recipient, Other])
            .ShouldBeNull();

    [Fact]
    public void Resolve_ReturnsAnOrphan_WhenThereIsNoGuidAndNoOpenSend() =>
        EventCorrelation.Resolve(correlationId: null, correlationIdExists: false, []).ShouldBeNull();

    [Fact]
    public void OpenStatuses_ReturnsSentAndDeferred_WhenTheProviderStillOwesAnAnswer()
    {
        EventCorrelation.OpenStatuses.ShouldBe(
            new[] { DeliveryStatus.Sent, DeliveryStatus.Deferred },
            ignoreOrder: true);

        // 'queued' nunca salió de acá y los terminales ya tienen su historia contada; incluir
        // 'delivered' dejaría que un rebote tardío aterrizara sobre un envío anterior y exitoso.
        EventCorrelation.OpenStatuses.ShouldNotContain(DeliveryStatus.Queued);
        EventCorrelation.OpenStatuses.ShouldNotContain(DeliveryStatus.Delivered);
    }
}
