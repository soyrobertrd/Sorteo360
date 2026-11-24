using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Crm;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Portal;
using Sorteo360.Convocatorias.Core.Unsubscribe;
using Sorteo360.Platform.Errors;
using NSubstitute;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Unsubscribe;

/// <summary>
/// La baja del portal (spec <c>06</c> §6): un token trae una fila, se le empuja el opt-out al CRM
/// y se le devuelve a la persona su dirección enmascarada. Con el CRM caído la baja igual triunfa
/// y la fila queda pendiente; el segundo clic es idempotente; un token que no casa sale por 404.
/// </summary>
public sealed class UnsubscribeServiceTests
{
    private readonly ICampaignRepository _repository = Substitute.For<ICampaignRepository>();
    private readonly ICrmContactDirectory _crm = Substitute.For<ICrmContactDirectory>();
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero));
    private readonly UnsubscribeService _service;

    private const string Token = "una-credencial-de-baja-de-ejemplo";
    private const string Email = "ana@acme.com";
    private const string MaskedAddress = "a***@acme.com";

    private readonly Guid _recipientId = Guid.NewGuid();
    private readonly Guid _contactId = Guid.NewGuid();

    public UnsubscribeServiceTests()
    {
        _service = new UnsubscribeService(_repository, _crm, _clock);

        _repository.MarkUnsubscribedAsync(
                Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
            .Returns(UnitResult.Success<BaseError>());

        _crm.OptOutAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(UnitResult.Success<BaseError>());
    }

    [Fact]
    public async Task UnsubscribeAsync_ReturnsNotFound_WhenTokenMatchesNoRecipient()
    {
        _repository.FindRecipientByUnsubscribeTokenAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<UnsubscribeTarget>, BaseError>(Maybe<UnsubscribeTarget>.None));

        var result = await _service.UnsubscribeAsync(Token, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(PublicUnsubscribeNotFoundError.ErrorCode);
        await _crm.DidNotReceive().OptOutAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnsubscribeAsync_MasksAddressAndSyncsToCrm_WhenCrmIsUp()
    {
        GivenTarget(unsubscribedAt: null, unsubscribeSyncedAt: null);

        var result = await _service.UnsubscribeAsync(Token, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(MaskedAddress);
        await _crm.Received(1).OptOutAsync(_contactId, Arg.Any<CancellationToken>());
        await _repository.Received(1).MarkUnsubscribedAsync(
            _recipientId, _clock.Now, _clock.Now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnsubscribeAsync_MarksPendingButStillSucceeds_WhenCrmIsDown()
    {
        GivenTarget(unsubscribedAt: null, unsubscribeSyncedAt: null);
        _crm.OptOutAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(UnitResult.Failure<BaseError>(new CrmUnavailableError()));

        var result = await _service.UnsubscribeAsync(Token, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(MaskedAddress);
        // La fila queda pendiente: sin sello de sincronización, que el próximo lanzamiento vacía.
        await _repository.Received(1).MarkUnsubscribedAsync(
            _recipientId, _clock.Now, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnsubscribeAsync_DoesNothingButConfirm_WhenAlreadySynced()
    {
        GivenTarget(unsubscribedAt: _clock.Now.AddDays(-1), unsubscribeSyncedAt: _clock.Now.AddDays(-1));

        var result = await _service.UnsubscribeAsync(Token, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(MaskedAddress);
        // Nada limpia una baja en fase 1: un segundo clic no empuja ni vuelve a sellar.
        await _crm.DidNotReceive().OptOutAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().MarkUnsubscribedAsync(
            Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnsubscribeAsync_KeepsTheFirstClickTime_WhenSecondClickCompletesAPendingRow()
    {
        var firstClick = _clock.Now.AddHours(-2);
        GivenTarget(unsubscribedAt: firstClick, unsubscribeSyncedAt: null);

        var result = await _service.UnsubscribeAsync(Token, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        // El clic conserva su hora original; solo el sello de sincronización es de ahora.
        await _repository.Received(1).MarkUnsubscribedAsync(
            _recipientId, firstClick, _clock.Now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RetrySyncAsync_ReturnsTheRecipientStampedWithTheFirstClickKept_WhenCrmTakesThePendingPush()
    {
        var firstClick = _clock.Now.AddDays(-3);
        GivenRecipient(unsubscribedAt: firstClick, unsubscribeSyncedAt: null);

        var result = await _service.RetrySyncAsync(_recipientId, CancellationToken.None);

        result.Value.Value.UnsubscribedAt.ShouldBe(firstClick);
        result.Value.Value.UnsubscribeSyncedAt.ShouldBe(_clock.Now);
        await _crm.Received(1).OptOutAsync(_contactId, Arg.Any<CancellationToken>());
        await _repository.Received(1).MarkUnsubscribedAsync(
            _recipientId, firstClick, _clock.Now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RetrySyncAsync_ReturnsCrmUnavailableWithNoStamp_WhenCrmIsStillDown()
    {
        GivenRecipient(unsubscribedAt: _clock.Now.AddDays(-3), unsubscribeSyncedAt: null);
        _crm.OptOutAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(UnitResult.Failure<BaseError>(new CrmUnavailableError()));

        var result = await _service.RetrySyncAsync(_recipientId, CancellationToken.None);

        result.Error.Code.ShouldBe(CrmUnavailableError.ErrorCode);
        await _repository.DidNotReceive().MarkUnsubscribedAsync(
            Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Is<DateTimeOffset?>(stamp => stamp != null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RetrySyncAsync_ReturnsRecipientNotUnsubscribedWithoutPushing_WhenNobodyClicked()
    {
        GivenRecipient(unsubscribedAt: null, unsubscribeSyncedAt: null);

        var result = await _service.RetrySyncAsync(_recipientId, CancellationToken.None);

        result.Error.Code.ShouldBe(RecipientNotUnsubscribedError.ErrorCode);
        await _crm.DidNotReceive().OptOutAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RetrySyncAsync_ReturnsTheRecipientAsIsWithoutPushing_WhenAlreadySynced()
    {
        var syncedAt = _clock.Now.AddDays(-1);
        GivenRecipient(unsubscribedAt: _clock.Now.AddDays(-2), unsubscribeSyncedAt: syncedAt);

        var result = await _service.RetrySyncAsync(_recipientId, CancellationToken.None);

        result.Value.Value.UnsubscribeSyncedAt.ShouldBe(syncedAt);
        await _crm.DidNotReceive().OptOutAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    private void GivenRecipient(DateTimeOffset? unsubscribedAt, DateTimeOffset? unsubscribeSyncedAt) =>
        _repository.FindRecipientUnsubscribeAsync(_recipientId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<RecipientUnsubscribe>, BaseError>(Maybe.From(
                new RecipientUnsubscribe(_recipientId, Guid.NewGuid(), _contactId, unsubscribedAt, unsubscribeSyncedAt))));

    private void GivenTarget(DateTimeOffset? unsubscribedAt, DateTimeOffset? unsubscribeSyncedAt)
    {
        var target = new UnsubscribeTarget(
            _recipientId,
            _contactId,
            Email,
            PortalToken.HashOf(Token),
            unsubscribedAt,
            unsubscribeSyncedAt);

        _repository.FindRecipientByUnsubscribeTokenAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<UnsubscribeTarget>, BaseError>(Maybe.From(target)));
    }

    /// <summary>Un reloj clavado, para poder afirmar las horas que se sellan.</summary>
    private sealed class FixedClock : TimeProvider
    {
        public FixedClock(DateTimeOffset now) => Now = now;

        public DateTimeOffset Now { get; }

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
