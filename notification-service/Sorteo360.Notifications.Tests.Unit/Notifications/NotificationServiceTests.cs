using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using Sorteo360.Notifications.Core.Database;
using Sorteo360.Notifications.Core.DTOs;
using Sorteo360.Notifications.Core.Errors;
using Sorteo360.Notifications.Core.Notifications;
using Sorteo360.Platform.Errors;
using NSubstitute;
using Shouldly;

namespace Sorteo360.Notifications.Tests.Unit.Notifications;

/// <summary>
/// Aceptar un envío: lo que se comprueba antes de escribir, qué se escribe, y qué se contesta
/// cuando la clave de idempotencia se repite.
/// <para>
/// El 202 es la línea. Cada test que espera un fallo comprueba además que el repositorio no fue
/// llamado: que el código sea el correcto no vale nada si igual quedó una fila.
/// </para>
/// </summary>
public sealed class NotificationServiceTests
{
    private readonly INotificationBatchRepository _repository = Substitute.For<INotificationBatchRepository>();
    private readonly SendGridEventWebhookSettings _eventWebhook = new() { Enabled = true };
    private readonly NotificationService _service;

    public NotificationServiceTests()
    {
        _service = new NotificationService(_repository, Options.Create(_eventWebhook));
    }

    #region Lo que se guarda

    [Fact]
    public async Task AcceptAsync_ReturnsTheBatchItsEntitiesAndOneRowPerPerson_WhenTheRequestIsValid()
    {
        StoredAsAsked();

        var result = await _service.AcceptAsync(
            NotificationTestData.Send(
                body: "<p>Hola {{nombre}}</p>",
                sharedData: NotificationTestData.Data(("nombre", "gente")),
                recipients:
                [
                    NotificationTestData.Recipient("maria@sorteo360.local"),
                    NotificationTestData.Recipient("jose@sorteo360.local")
                ],
                about:
                [
                    new NotificationAboutDto("events", "event", "xyz-789"),
                    new NotificationAboutDto("events", "quote", "abc-123")
                ]),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Results.Count.ShouldBe(2);
        result.Value.Results.ShouldAllBe(outcome => outcome.Status == RecipientOutcome.Accepted);
        result.Value.Results.Select(outcome => outcome.Recipient)
            .ShouldBe(["maria@sorteo360.local", "jose@sorteo360.local"]);

        var write = LastWrite();
        write.Batch.Id.ShouldBe(result.Value.BatchId);
        write.About.Count.ShouldBe(2);
        write.Recipients.Count.ShouldBe(2);
        write.Recipients.Select(row => row.Id).ShouldBe(result.Value.Results.Select(outcome => outcome.Id!.Value));
    }

    [Fact]
    public async Task AcceptAsync_ReturnsTheBodyStoredUnsubstituted_WhenAccepted()
    {
        StoredAsAsked();

        await _service.AcceptAsync(
            NotificationTestData.Send(
                subject: "Aviso de {{evento}}",
                body: "<p>Hola {{nombre}}</p>",
                sharedData: NotificationTestData.Data(("evento", "Feria"), ("nombre", "María"))),
            CancellationToken.None);

        // El cuerpo guardado conserva sus marcadores, y por eso ningún valor de data puede terminar
        // dentro de una columna.
        var write = LastWrite();
        write.Batch.Body.ShouldBe("<p>Hola {{nombre}}</p>");
        write.Batch.Subject.ShouldBe("Aviso de {{evento}}");
        write.Batch.Body.ShouldNotContain("María");
        write.Batch.Subject.ShouldNotContain("Feria");
    }

    [Fact]
    public async Task AcceptAsync_ReturnsEveryCreatedRowQueued_WhenAccepted()
    {
        StoredAsAsked();

        await _service.AcceptAsync(NotificationTestData.Send(), CancellationToken.None);

        var row = LastWrite().Recipients.Single();
        row.DeliveryStatus.ShouldBe(DeliveryStatus.Queued);
        row.RecipientAction.ShouldBe(RecipientAction.None);
        row.AttemptCount.ShouldBe(0);
        row.SentAt.ShouldBeNull();
        row.SmtpQueuedAs.ShouldBeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AcceptAsync_ReturnsTheWebhookFlagStampedOnTheBatch_WhenAccepted(bool enabled)
    {
        _eventWebhook.Enabled = enabled;
        StoredAsAsked();

        await _service.AcceptAsync(NotificationTestData.Send(), CancellationToken.None);

        LastWrite().Batch.TrackingExpected.ShouldBe(enabled);
    }

    [Fact]
    public async Task AcceptAsync_ReturnsTheRequestFingerprintStored_WhenAccepted()
    {
        StoredAsAsked();
        var dto = NotificationTestData.Send();

        await _service.AcceptAsync(dto, CancellationToken.None);

        LastWrite().Batch.BodySha256.ShouldBe(NotificationFingerprint.Compute(dto));
    }

    #endregion

    #region El tope de personas

    [Fact]
    public async Task AcceptAsync_ReturnsSuccess_WhenThePeopleAreExactlyAtTheCap()
    {
        StoredAsAsked();

        var result = await _service.AcceptAsync(
            NotificationTestData.Send(recipients: People(NotificationErrors.RecipientCap)),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Results.Count.ShouldBe(NotificationErrors.RecipientCap);
    }

    [Fact]
    public async Task AcceptAsync_ReturnsAnErrorAndStoresNothing_WhenOnePersonIsOverTheCap()
    {
        var result = await _service.AcceptAsync(
            NotificationTestData.Send(recipients: People(NotificationErrors.RecipientCap + 1)),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("NOTIFICATION_RECIPIENT_CAP_EXCEEDED");
        result.Error.Kind.ShouldBe(ErrorKind.Invalid);
        await NothingWasStored();
    }

    #endregion

    #region Claves repetidas

    [Fact]
    public async Task AcceptAsync_ReturnsAnErrorNamingTheKey_WhenAKeyComesTwice()
    {
        var result = await _service.AcceptAsync(
            NotificationTestData.Send(
                body: "<p>{{evento}}</p>",
                sharedData: NotificationTestData.Data(("evento", "Feria")),
                recipients: [NotificationTestData.Recipient("maria@sorteo360.local", ("evento", "Otra"))]),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(DataKeyCollisionError.ErrorCode);
        result.Error.ShouldBeOfType<DataKeyCollisionError>().DataKey.ShouldBe("evento");
        result.Error.ProblemExtensions["dataKey"].ShouldBe("evento");
        await NothingWasStored();
    }

    [Fact]
    public async Task AcceptAsync_ReturnsSuccess_WhenTwoPeopleShareAKey()
    {
        StoredAsAsked();

        var result = await _service.AcceptAsync(
            NotificationTestData.Send(
                body: "<p>{{nombre}}</p>",
                recipients:
                [
                    NotificationTestData.Recipient("maria@sorteo360.local", ("nombre", "María")),
                    NotificationTestData.Recipient("jose@sorteo360.local", ("nombre", "José"))
                ]),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    #endregion

    #region Marcadores sin resolver

    [Fact]
    public async Task AcceptAsync_ReturnsAnErrorNamingThePlaceholder_WhenItHasNoValueAnywhere()
    {
        var result = await _service.AcceptAsync(
            NotificationTestData.Send(body: "<p>Hola {{nombre}}</p>"),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(PlaceholderUnresolvedError.ErrorCode);
        result.Error.ShouldBeOfType<PlaceholderUnresolvedError>().Placeholder.ShouldBe("nombre");
        result.Error.ProblemExtensions["placeholder"].ShouldBe("nombre");
        await NothingWasStored();
    }

    [Fact]
    public async Task AcceptAsync_ReturnsAnError_WhenAPlaceholderIsMissingForOnlyOnePerson()
    {
        var result = await _service.AcceptAsync(
            NotificationTestData.Send(
                body: "<p>Hola {{nombre}}</p>",
                recipients:
                [
                    NotificationTestData.Recipient("maria@sorteo360.local", ("nombre", "María")),
                    NotificationTestData.Recipient("jose@sorteo360.local")
                ]),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<PlaceholderUnresolvedError>();
        error.Placeholder.ShouldBe("nombre");
        error.Recipient.ShouldBe("jose@sorteo360.local");
        await NothingWasStored();
    }

    [Fact]
    public async Task AcceptAsync_ReturnsAnError_WhenAPlaceholderInTheSubjectHasNoValue()
    {
        var result = await _service.AcceptAsync(
            NotificationTestData.Send(subject: "Cotización de {{evento}}"),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<PlaceholderUnresolvedError>().Placeholder.ShouldBe("evento");
        await NothingWasStored();
    }

    [Fact]
    public async Task AcceptAsync_ReturnsSuccess_WhenAValueIsNeverMentioned()
    {
        StoredAsAsked();

        var result = await _service.AcceptAsync(
            NotificationTestData.Send(
                body: "<p>Sin marcadores</p>",
                sharedData: NotificationTestData.Data(("clave_que_sobra", "valor"))),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task AcceptAsync_ReturnsSuccess_WhenBracesAreNotAMarker()
    {
        StoredAsAsked();

        var result = await _service.AcceptAsync(
            NotificationTestData.Send(body: "<p>{{ sin cierre y una {{clave}}</p>",
                sharedData: NotificationTestData.Data(("clave", "valor"))),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    #endregion

    #region Direcciones

    [Theory]
    [InlineData("no-such@@bad")]
    [InlineData("sin-arroba")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("María <maria@sorteo360.local>")]
    [InlineData("maria@sorteo360.local, jose@sorteo360.local")]
    public async Task AcceptAsync_ReturnsARejectedRecipientAndNoRow_WhenAnAddressIsInvalid(string email)
    {
        StoredAsAsked();

        var result = await _service.AcceptAsync(
            NotificationTestData.Send(recipients: [NotificationTestData.Recipient(email)]),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        var outcome = result.Value.Results.Single();
        outcome.Status.ShouldBe(RecipientOutcome.Rejected);
        outcome.Reason.ShouldBe(RecipientOutcome.AddressInvalid);
        outcome.Id.ShouldBeNull();

        // El envío sí existe: el 202 ya se cruzó, y el historial tiene que poder contarlo.
        LastWrite().Recipients.ShouldBeEmpty();
    }

    [Fact]
    public async Task AcceptAsync_ReturnsRowsForTheGoodAddresses_WhenOneAddressIsInvalid()
    {
        StoredAsAsked();

        var result = await _service.AcceptAsync(
            NotificationTestData.Send(recipients:
            [
                NotificationTestData.Recipient("maria@sorteo360.local"),
                NotificationTestData.Recipient("no-such@@bad"),
                NotificationTestData.Recipient("jose@sorteo360.local")
            ]),
            CancellationToken.None);

        result.Value.Results.Select(outcome => outcome.Status)
            .ShouldBe([RecipientOutcome.Accepted, RecipientOutcome.Rejected, RecipientOutcome.Accepted]);
        LastWrite().Recipients.Count.ShouldBe(2);
    }

    #endregion

    #region Idempotencia

    [Fact]
    public async Task AcceptAsync_ReturnsTheSameIds_WhenTheKeyAndTheBodyRepeat()
    {
        var dto = NotificationTestData.Send(recipients:
        [
            NotificationTestData.Recipient("maria@sorteo360.local"),
            NotificationTestData.Recipient("jose@sorteo360.local")
        ]);

        var batchId = Guid.NewGuid();
        var first = new StoredRecipient(Guid.NewGuid(), "maria@sorteo360.local");
        var second = new StoredRecipient(Guid.NewGuid(), "jose@sorteo360.local");

        // El repositorio contesta lo que ya estaba guardado bajo esa clave.
        Stored(new StoredBatch(batchId, NotificationFingerprint.Compute(dto), [first, second]));

        var result = await _service.AcceptAsync(dto, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.BatchId.ShouldBe(batchId);
        result.Value.Results.Select(outcome => outcome.Id).ShouldBe([first.Id, second.Id]);
        result.Value.Results.ShouldAllBe(outcome => outcome.Status == RecipientOutcome.Accepted);
    }

    [Fact]
    public async Task AcceptAsync_ReturnsAConflict_WhenTheKeyRepeatsWithAnotherBody()
    {
        Stored(new StoredBatch(Guid.NewGuid(), "otra-huella", []));

        var result = await _service.AcceptAsync(NotificationTestData.Send(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("NOTIFICATION_IDEMPOTENCY_KEY_REUSED");
        result.Error.Kind.ShouldBe(ErrorKind.Conflict);
    }

    [Fact]
    public async Task AcceptAsync_ReturnsTheAddressRejectedAgain_WhenARejectedRequestIsReplayed()
    {
        var dto = NotificationTestData.Send(recipients:
        [
            NotificationTestData.Recipient("maria@sorteo360.local"),
            NotificationTestData.Recipient("no-such@@bad")
        ]);

        var accepted = new StoredRecipient(Guid.NewGuid(), "maria@sorteo360.local");
        Stored(new StoredBatch(Guid.NewGuid(), NotificationFingerprint.Compute(dto), [accepted]));

        var result = await _service.AcceptAsync(dto, CancellationToken.None);

        result.Value.Results.Select(outcome => outcome.Status)
            .ShouldBe([RecipientOutcome.Accepted, RecipientOutcome.Rejected]);
        result.Value.Results[0].Id.ShouldBe(accepted.Id);
        result.Value.Results[1].Id.ShouldBeNull();
    }

    #endregion

    #region Fallos del repositorio

    [Fact]
    public async Task AcceptAsync_ReturnsTheDatabaseFailure_WhenTheDatabaseFails()
    {
        var failure = new BaseError("Error al guardar el envío de notificación: fallo de base de datos.", "DB_ERROR");

        _repository
            .AddAsync(Arg.Any<AcceptedSend>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<StoredBatch, BaseError>(failure));

        var result = await _service.AcceptAsync(NotificationTestData.Send(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("DB_ERROR");
    }

    #endregion

    #region Helpers

    /// <summary>El repositorio guarda lo que se le pide y devuelve esos mismos ids.</summary>
    private void StoredAsAsked()
    {
        _repository
            .AddAsync(Arg.Any<AcceptedSend>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var send = call.Arg<AcceptedSend>();

                return Result.Success<StoredBatch, BaseError>(new StoredBatch(
                    send.Batch.Id,
                    send.Batch.BodySha256,
                    [.. send.Recipients.Select(row => new StoredRecipient(row.Id, row.Email))]));
            });
    }

    /// <summary>La clave ya estaba ocupada por este envío.</summary>
    private void Stored(StoredBatch existing)
    {
        _repository
            .AddAsync(Arg.Any<AcceptedSend>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<StoredBatch, BaseError>(existing));
    }

    /// <summary>Lo que se le pidió guardar al repositorio en la última llamada.</summary>
    private AcceptedSend LastWrite()
    {
        var call = _repository.ReceivedCalls().Last(received => received.GetMethodInfo().Name == nameof(INotificationBatchRepository.AddAsync));

        return (AcceptedSend)call.GetArguments()[0]!;
    }

    /// <summary>Nada llegó a la base: el fallo se contestó antes de escribir.</summary>
    private async Task NothingWasStored() =>
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);

    private static IReadOnlyList<NotificationRecipientDto> People(int count) =>
        [.. Enumerable.Range(1, count).Select(index => NotificationTestData.Recipient($"persona{index}@sorteo360.local"))];

    #endregion
}
