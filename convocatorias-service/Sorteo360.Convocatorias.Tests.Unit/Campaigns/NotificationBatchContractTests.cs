using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Platform.Errors;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Campaigns;

/// <summary>
/// Las dos reglas que notification-service cobra sobre un lote (spec <c>04</c> §2 y §2.2).
/// <para>
/// Se cobran acá y no en cada transporte porque los dos tienen que contestar lo mismo: el stub es
/// el ensayo de lo que hará el servicio real, y un stub más permisivo haría que stage dejara pasar
/// lo que producción rechaza.
/// </para>
/// </summary>
public sealed class NotificationBatchContractTests
{
    [Fact]
    public void Validate_Succeeds_WhenTheBatchSitsExactlyOnTheCap()
    {
        var request = Batch(NotificationBatchContract.MaxRecipients);

        NotificationBatchContract.Validate(request).IsSuccess.ShouldBeTrue();
    }

    /// <summary>
    /// Mil uno en una sola llamada es un 400. Trocear es trabajo de quien llama, así que llegar
    /// acá significa que el emisor se equivocó, no el dato.
    /// </summary>
    [Fact]
    public void Validate_Fails_WhenTheBatchIsOverTheCap()
    {
        var request = Batch(NotificationBatchContract.MaxRecipients + 1);

        var result = NotificationBatchContract.Validate(request);

        result.IsFailure.ShouldBeTrue();

        var error = result.Error.ShouldBeOfType<NotificationBatchTooLargeError>();
        error.Code.ShouldBe(NotificationBatchTooLargeError.ErrorCode);
        error.Kind.ShouldBe(ErrorKind.Invalid);
        error.RecipientCount.ShouldBe(NotificationBatchContract.MaxRecipients + 1);
    }

    /// <summary>
    /// Nunca una regla de precedencia: el lote entero se cae y la clave se nombra, para que quien
    /// lea el log sepa cuál de las dos bolsas tocar.
    /// </summary>
    [Fact]
    public void Validate_Fails_WhenAKeyIsInBothBags()
    {
        var request = Batch(
            recipientCount: 1,
            sharedData: new Dictionary<string, string> { ["evento"] = "Feria de Madrid" },
            recipientData: new Dictionary<string, string> { ["evento"] = "otra cosa" });

        var result = NotificationBatchContract.Validate(request);

        result.IsFailure.ShouldBeTrue();

        var error = result.Error.ShouldBeOfType<NotificationDataKeyConflictError>();
        error.Code.ShouldBe(NotificationDataKeyConflictError.ErrorCode);
        error.Kind.ShouldBe(ErrorKind.Invalid);
        error.Key.ShouldBe("evento");
    }

    /// <summary>
    /// La colisión se busca en cada persona, no solo en la primera: la bolsa de cada una la arma
    /// una vuelta distinta del bucle que acuña los tokens.
    /// </summary>
    [Fact]
    public void Validate_Fails_WhenOnlyTheLastRecipientCollides()
    {
        var shared = new Dictionary<string, string> { ["evento"] = "Feria de Madrid" };

        var request = new SendBatchRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "es",
            "Te invitamos a {{evento}}",
            "<html>{{nombre}}</html>",
            shared,
            [
                new SendBatchRecipient(Guid.NewGuid(), "ana@acme.com", null, new Dictionary<string, string>
                {
                    ["nombre"] = "Ana",
                }),
                new SendBatchRecipient(Guid.NewGuid(), "luis@acme.com", null, new Dictionary<string, string>
                {
                    ["evento"] = "otra cosa",
                }),
            ]);

        NotificationBatchContract.Validate(request).Error
            .ShouldBeOfType<NotificationDataKeyConflictError>()
            .Key.ShouldBe("evento");
    }

    /// <summary>
    /// El lote que arma el servicio de verdad: los tres valores del evento compartidos y el saludo
    /// con los dos enlaces por persona. No colisiona, y este test es lo que lo mantiene así.
    /// </summary>
    [Fact]
    public void Validate_Succeeds_WhenTheTwoBagsAreTheRealOnes()
    {
        var request = Batch(
            recipientCount: 2,
            sharedData: new Dictionary<string, string>
            {
                ["evento"] = "Feria de Madrid",
                ["lugar"] = "Madrid, España",
                ["fecha"] = "12 de junio de 2026",
            },
            recipientData: new Dictionary<string, string>
            {
                ["nombre"] = "Ana",
                ["enlace_registro"] = "https://app.pgti360.test/publico/registro#t=abc",
                ["enlace_baja"] = "https://app.pgti360.test/publico/baja#t=def",
            });

        NotificationBatchContract.Validate(request).IsSuccess.ShouldBeTrue();
    }

    private static SendBatchRequest Batch(
        int recipientCount,
        IReadOnlyDictionary<string, string>? sharedData = null,
        IReadOnlyDictionary<string, string>? recipientData = null) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "es",
            "Te invitamos a {{evento}}",
            "<html>{{nombre}}</html>",
            sharedData ?? new Dictionary<string, string>(),
            [.. Enumerable.Range(0, recipientCount).Select(index => new SendBatchRecipient(
                Guid.NewGuid(),
                $"invitado{index}@acme.com",
                null,
                recipientData ?? new Dictionary<string, string>()))]);
}
