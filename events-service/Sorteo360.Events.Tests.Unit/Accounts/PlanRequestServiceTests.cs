using CSharpFunctionalExtensions;
using NSubstitute;
using Shouldly;
using Sorteo360.Events.Core.Accounts;
using Sorteo360.Events.Tests.Unit.Shared;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Tests.Unit.Accounts;

public sealed class PlanRequestServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly Guid AdminId = Guid.NewGuid();

    private readonly IPlanRequestRepository _repository = Substitute.For<IPlanRequestRepository>();
    private readonly IPlanRequestNotifier _notifier = Substitute.For<IPlanRequestNotifier>();
    private readonly TestTimeProvider _clock = new();
    private readonly DateTimeOffset _now;
    private readonly PlanRequestService _sut;

    public PlanRequestServiceTests()
    {
        _now = _clock.GetUtcNow();
        _sut = new PlanRequestService(_repository, _notifier, _clock);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("free")]
    [InlineData("enterprise")]
    public async Task CreateAsync_ReturnsInvalid_WhenPlanIsNotPaid(string? requestedPlan)
    {
        var result = await _sut.CreateAsync(AccountId, requestedPlan, "REF-1", ActorId, "a@b.com", CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("PLAN_REQUEST_INVALID_PLAN");
        await _notifier.DidNotReceiveWithAnyArgs().NotifyRequestedAsync(default!, default);
    }

    [Fact]
    public async Task CreateAsync_CreatesPendingRequestAndNotifies_WhenValid()
    {
        var account = SampleAccount(PlanCatalog.Free);
        var created = SampleRequest(PlanCatalog.Pro, PlanRequestStatus.Pending);

        _repository.GetAccountAsync(AccountId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<Account>, BaseError>(Maybe.From(account)));
        _repository.CreatePendingAsync(
                AccountId, PlanCatalog.Pro, "TRF-900", ActorId, "owner@acme.com", Now, Arg.Any<CancellationToken>())
            .Returns(Result.Success<PlanRequest, BaseError>(created));

        var result = await _sut.CreateAsync(AccountId, "PRO", " TRF-900 ", ActorId, null, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(created);
        await _notifier.Received(1).NotifyRequestedAsync(created, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApproveAsync_UsesDefault30DayPeriodAndNotifies_WhenDatesAreOmitted()
    {
        var requestId = Guid.NewGuid();
        var approved = SampleRequest(PlanCatalog.Basic, PlanRequestStatus.Approved);
        var upgraded = SampleAccount(PlanCatalog.Basic, Now, Now.AddDays(30));

        _repository.ApprovePendingAsync(
                requestId, AdminId, Now, Now.AddDays(30), Now, Arg.Any<CancellationToken>())
            .Returns(Result.Success<(PlanRequest, Account), BaseError>((approved, upgraded)));

        var result = await _sut.ApproveAsync(requestId, AdminId, null, null, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _notifier.Received(1).NotifyApprovedAsync(approved, upgraded, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApproveAsync_ReturnsInvalid_WhenPeriodEndIsNotAfterStart()
    {
        var result = await _sut.ApproveAsync(Guid.NewGuid(), AdminId, Now, Now, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("PLAN_REQUEST_INVALID_PERIOD");
    }

    [Fact]
    public async Task RejectAsync_RequiresReason()
    {
        var result = await _sut.RejectAsync(Guid.NewGuid(), AdminId, "   ", CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("PLAN_REQUEST_REJECTION_REASON_REQUIRED");
    }

    [Fact]
    public async Task ProcessExpirationsAsync_DowngradesExpiredAndWarnsExpiringWithin7Days()
    {
        var downgraded = SampleAccount(PlanCatalog.Free);
        var expiring = SampleAccount(PlanCatalog.Pro, Now.AddDays(-25), Now.AddDays(5));

        _repository.DowngradeExpiredAccountsAsync(Now, Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<Account>, BaseError>([downgraded]));
        _repository.MarkExpiringAccountsWarnedAsync(Now, Now.AddDays(7), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<Account>, BaseError>([expiring]));

        var result = await _sut.ProcessExpirationsAsync(CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.DowngradedCount.ShouldBe(1);
        result.Value.WarnedCount.ShouldBe(1);
        await _notifier.Received(1).NotifyExpiringSoonAsync(expiring, Arg.Any<CancellationToken>());
    }

    private static Account SampleAccount(
        string plan,
        DateTimeOffset? startedAt = null,
        DateTimeOffset? expiresAt = null) => new(
        AccountId,
        "Acme RD",
        plan,
        AccountStatus.Active,
        "owner@acme.com",
        startedAt,
        expiresAt,
        null,
        Now.AddDays(-10),
        Now);

    private static PlanRequest SampleRequest(string plan, string status) => new(
        Guid.NewGuid(),
        AccountId,
        "Acme RD",
        plan,
        status,
        "TRF-900",
        Now,
        ActorId,
        "owner@acme.com",
        null,
        null,
        null,
        null,
        null);
}

