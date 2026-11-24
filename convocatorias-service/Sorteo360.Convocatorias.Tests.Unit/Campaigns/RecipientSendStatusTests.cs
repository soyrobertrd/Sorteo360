using Sorteo360.Convocatorias.Core.Campaigns;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Campaigns;

/// <summary>
/// El eje del destinatario (spec <c>03</c> §3): la entrega y nada más. Lo que más valor tiene
/// acá es lo que <b>no</b> está, porque es lo que el modelo del mock confundía.
/// </summary>
public sealed class RecipientSendStatusTests
{
    [Fact]
    public void All_ReturnsExactlyFourStatuses_WhenCalled() =>
        RecipientSendStatus.All.ShouldBe(["pending", "handed_off", "failed", "suppressed"], ignoreOrder: true);

    /// <summary>
    /// <c>registered</c> se deriva de la existencia de una fila de registro; <c>opened</c>,
    /// <c>clicked</c> y <c>delivered</c> necesitan el webhook del proveedor. Plegar cualquiera
    /// de ellos acá perdería "le escribimos y contestó", que es de donde salieron el
    /// <c>WAITLIST</c> del mock y su sistema de puntuación.
    /// </summary>
    [Theory]
    [InlineData("registered")]
    [InlineData("opened")]
    [InlineData("clicked")]
    [InlineData("delivered")]
    [InlineData("bounced")]
    public void All_ReturnsNoSuchStatus_WhenItBelongsToAnotherAxis(string other) =>
        RecipientSendStatus.All.ShouldNotContain(other);

    [Theory]
    [InlineData(RecipientSendStatus.HandedOff)]
    [InlineData(RecipientSendStatus.Failed)]
    [InlineData(RecipientSendStatus.Suppressed)]
    public void IsTerminal_ReturnsTrue_WhenTheStatusIsHandedOffFailedOrSuppressed(string status) =>
        RecipientSendStatus.IsTerminal(status).ShouldBeTrue();

    /// <summary>
    /// Que ninguna fila siga pendiente es exactamente lo que lleva la campaña a <c>sent</c>, así
    /// que <c>pending</c> tiene que quedar fuera de terminal o el worker la daría por acabada
    /// sin haber mandado nada.
    /// </summary>
    [Fact]
    public void IsTerminal_ReturnsFalse_WhenTheStatusIsPending() =>
        RecipientSendStatus.IsTerminal(RecipientSendStatus.Pending).ShouldBeFalse();
}
