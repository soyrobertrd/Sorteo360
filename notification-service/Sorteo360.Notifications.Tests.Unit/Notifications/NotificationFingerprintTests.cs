using Sorteo360.Notifications.Core.Notifications;
using Shouldly;

namespace Sorteo360.Notifications.Tests.Unit.Notifications;

/// <summary>
/// La huella de la petición: lo que permite contestar 409 cuando la misma clave de idempotencia
/// llega con otro contenido, en vez de dejar pasar un correo que nadie recibió.
/// </summary>
public sealed class NotificationFingerprintTests
{
    [Fact]
    public void Compute_ReturnsTheSameHash_WhenTheRequestIsTheSame()
    {
        var first = NotificationFingerprint.Compute(NotificationTestData.Send());
        var second = NotificationFingerprint.Compute(NotificationTestData.Send());

        second.ShouldBe(first);
    }

    [Fact]
    public void Compute_ReturnsTheSameHash_WhenDictionaryKeysAreReordered()
    {
        // El orden de las claves de un objeto JSON no significa nada, así que no puede cambiar la
        // huella: si cambiara, un cliente que serializa distinto recibiría un 409 por nada.
        var first = NotificationFingerprint.Compute(NotificationTestData.Send(
            sharedData: NotificationTestData.Data(("evento", "Feria"), ("categoria", "Ambientacion"))));

        var second = NotificationFingerprint.Compute(NotificationTestData.Send(
            sharedData: NotificationTestData.Data(("categoria", "Ambientacion"), ("evento", "Feria"))));

        second.ShouldBe(first);
    }

    [Fact]
    public void Compute_ReturnsAnotherHash_WhenTheBodyChanges()
    {
        var first = NotificationFingerprint.Compute(NotificationTestData.Send(body: "<p>Uno</p>"));
        var second = NotificationFingerprint.Compute(NotificationTestData.Send(body: "<p>Dos</p>"));

        second.ShouldNotBe(first);
    }

    [Fact]
    public void Compute_ReturnsAnotherHash_WhenAValueChanges()
    {
        var first = NotificationFingerprint.Compute(NotificationTestData.Send(
            sharedData: NotificationTestData.Data(("evento", "Feria"))));

        var second = NotificationFingerprint.Compute(NotificationTestData.Send(
            sharedData: NotificationTestData.Data(("evento", "Otra feria"))));

        second.ShouldNotBe(first);
    }

    [Fact]
    public void Compute_ReturnsAnotherHash_WhenThePeopleChange()
    {
        var first = NotificationFingerprint.Compute(NotificationTestData.Send(
            recipients: [NotificationTestData.Recipient("maria@sorteo360.local")]));

        var second = NotificationFingerprint.Compute(NotificationTestData.Send(
            recipients:
            [
                NotificationTestData.Recipient("maria@sorteo360.local"),
                NotificationTestData.Recipient("jose@sorteo360.local")
            ]));

        second.ShouldNotBe(first);
    }

    [Fact]
    public void Compute_ReturnsAnotherHash_WhenOnlyARecipientNameChanges()
    {
        var unnamed = NotificationFingerprint.Compute(NotificationTestData.Send(
            recipients: [NotificationTestData.Recipient("maria@sorteo360.local")]));

        var named = NotificationFingerprint.Compute(NotificationTestData.Send(
            recipients: [NotificationTestData.Named("maria@sorteo360.local", "María González")]));

        var renamed = NotificationFingerprint.Compute(NotificationTestData.Send(
            recipients: [NotificationTestData.Named("maria@sorteo360.local", "María Pérez")]));

        named.ShouldNotBe(unnamed);
        renamed.ShouldNotBe(named);
    }

    [Fact]
    public void Compute_ReturnsAnotherHash_WhenANameAndADataValueCouldReadAlike()
    {
        // Un nombre no puede pasar por un valor de data ni al revés: el nombre va antes del primer
        // separador de registro de la persona.
        var named = NotificationFingerprint.Compute(NotificationTestData.Send(
            recipients: [NotificationTestData.Named("maria@sorteo360.local", "nombre")]));

        var withData = NotificationFingerprint.Compute(NotificationTestData.Send(
            recipients: [NotificationTestData.Recipient("maria@sorteo360.local", ("nombre", ""))]));

        withData.ShouldNotBe(named);
    }

    [Fact]
    public void Compute_ReturnsAnotherHash_WhenAValueMovesBetweenFields()
    {
        // Sin separadores entre campos, "ab" + "c" y "a" + "bc" darían la misma huella.
        var first = NotificationFingerprint.Compute(NotificationTestData.Send(
            subject: "ab", body: "c"));

        var second = NotificationFingerprint.Compute(NotificationTestData.Send(
            subject: "a", body: "bc"));

        second.ShouldNotBe(first);
    }

    [Fact]
    public void Compute_ReturnsALowercaseHexSha256_WhenCalled()
    {
        var hash = NotificationFingerprint.Compute(NotificationTestData.Send());

        hash.Length.ShouldBe(64);
        hash.ShouldBe(hash.ToLowerInvariant());
    }
}
