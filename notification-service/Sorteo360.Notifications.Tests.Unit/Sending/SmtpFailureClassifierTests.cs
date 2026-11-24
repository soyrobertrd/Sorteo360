using Sorteo360.Notifications.Core.Sending;
using Shouldly;

namespace Sorteo360.Notifications.Tests.Unit.Sending;

/// <summary>
/// La tabla de clasificación de la especificación 03 §6, entera. La regla es de SMTP y no nuestra,
/// así que lo que se prueba acá es que se aplique tal cual.
/// </summary>
public sealed class SmtpFailureClassifierTests
{
    [Theory]
    [InlineData(421)]  // El servicio no está disponible ahora mismo.
    [InlineData(450)]  // Buzón no disponible temporalmente.
    [InlineData(451)]  // Error local en el proceso.
    [InlineData(452)]  // Sin espacio de almacenamiento.
    public void Classify_ReturnsRetry_WhenTheCodeIs4xx(int statusCode) =>
        SmtpFailureClassifier.Classify(statusCode).ShouldBe(SmtpFailureKind.Transient);

    [Theory]
    [InlineData(550)]  // No existe ese buzón.
    [InlineData(551)]  // El usuario no es local.
    [InlineData(552)]  // Se pasó del cupo.
    [InlineData(553)]  // Nombre de buzón inválido.
    [InlineData(554)]  // La transacción falló.
    [InlineData(500)]  // Comando no reconocido.
    public void Classify_ReturnsNeverRetry_WhenTheCodeIs5xx(int statusCode) =>
        SmtpFailureClassifier.Classify(statusCode).ShouldBe(SmtpFailureKind.Terminal);

    [Fact]
    public void Classify_ReturnsAuthenticationFailure_WhenTheCodeIs535() =>
        // Separado del resto de los 5xx para que el worker lo pueda registrar como error: es
        // nuestra API key, no el buzón de quien recibe.
        SmtpFailureClassifier.Classify(535).ShouldBe(SmtpFailureKind.AuthenticationFailure);

    [Fact]
    public void Classify_ReturnsRetry_WhenThereIsNoStatusCode() =>
        // Conexión rechazada, tiempo agotado, TLS caído: no llegó a haber conversación, así que el
        // servidor nunca dijo que no.
        SmtpFailureClassifier.Classify(null).ShouldBe(SmtpFailureKind.Transient);
}
