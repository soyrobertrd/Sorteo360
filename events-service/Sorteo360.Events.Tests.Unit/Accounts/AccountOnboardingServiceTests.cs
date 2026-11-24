using System.Net;
using System.Text;
using System.Text.Json;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Sorteo360.Events.Core.Accounts;
using Sorteo360.Events.Infrastructure.Keycloak;
using Sorteo360.Events.Tests.Unit.Shared;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Tests.Unit.Accounts;

public sealed class AccountOnboardingServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly IAccountOnboardingRepository _repository = Substitute.For<IAccountOnboardingRepository>();
    private readonly IKeycloakAccountProvisioner _keycloak = Substitute.For<IKeycloakAccountProvisioner>();
    private readonly IAccountOnboardingNotifier _notifier = Substitute.For<IAccountOnboardingNotifier>();
    private readonly TestTimeProvider _clock = new();
    private readonly AccountOnboardingService _sut;

    public AccountOnboardingServiceTests()
    {
        _sut = new AccountOnboardingService(_repository, _keycloak, _notifier, _clock);
    }

    [Fact]
    public async Task SignupAsync_RequiresTermsAcceptance()
    {
        var result = await _sut.SignupAsync(
            new SignupInput("Acme", "Ana Pérez", "ana@acme.com", "free", "monthly", AcceptTerms: false),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("SIGNUP_TERMS_REQUIRED");
    }

    [Fact]
    public async Task SignupAsync_ReturnsSuccessWithoutLeaking_WhenEmailAlreadyHasActiveAccount()
    {
        _repository.HasActiveAccountWithEmailAsync("ana@acme.com", Arg.Any<CancellationToken>())
            .Returns(Result.Success<bool, BaseError>(true));

        var result = await _sut.SignupAsync(
            new SignupInput("Acme", "Ana Pérez", "ANA@ACME.COM", "free", "monthly", AcceptTerms: true),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _notifier.DidNotReceiveWithAnyArgs().SendActivationEmailAsync(default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task S1_SignupAsync_TenRepeatedSignupsSameEmail_SendsOnlyOneEmailAndCreatesOneActivation()
    {
        AccountActivation? pending = null;
        _repository.HasActiveAccountWithEmailAsync("ana@acme.com", Arg.Any<CancellationToken>())
            .Returns(Result.Success<bool, BaseError>(false));
        _repository.FindLatestPendingByEmailAsync("ana@acme.com", Arg.Any<CancellationToken>())
            .Returns(_ => Result.Success<Maybe<AccountActivation>, BaseError>(
                pending is null ? Maybe<AccountActivation>.None : Maybe.From(pending)));
        _repository.CreatePendingSignupAsync(
                Arg.Any<string>(), Arg.Any<string>(), "ana@acme.com", Arg.Any<string?>(),
                Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                pending = SampleActivation(lastSentAt: Now, resendCountToday: 1);
                return Result.Success<AccountActivation, BaseError>(pending);
            });

        for (var i = 0; i < 10; i++)
        {
            var res = await _sut.SignupAsync(
                new SignupInput("Acme", "Ana Pérez", "ana@acme.com", "free", "monthly", AcceptTerms: true),
                CancellationToken.None);
            res.IsSuccess.ShouldBeTrue();
        }

        await _repository.Received(1).CreatePendingSignupAsync(
            Arg.Any<string>(), Arg.Any<string>(), "ana@acme.com", Arg.Any<string?>(),
            Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _notifier.Received(1).SendActivationEmailAsync("ana@acme.com", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task S2_ResendActivationAsync_ReturnsSuccessSilently_WithinCooldownWindow_SameAsUnknownEmail()
    {
        var activation = SampleActivation(lastSentAt: Now.AddSeconds(-20), resendCountToday: 1);
        _repository.FindLatestPendingByEmailAsync("ana@acme.com", Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<AccountActivation>, BaseError>(Maybe.From(activation)));
        _repository.FindLatestPendingByEmailAsync("nadie@acme.com", Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<AccountActivation>, BaseError>(Maybe<AccountActivation>.None));

        var pendingResult = await _sut.ResendActivationAsync("ana@acme.com", CancellationToken.None);
        var unknownResult = await _sut.ResendActivationAsync("nadie@acme.com", CancellationToken.None);

        pendingResult.IsSuccess.ShouldBeTrue();
        unknownResult.IsSuccess.ShouldBeTrue();
        await _notifier.DidNotReceiveWithAnyArgs().SendActivationEmailAsync(default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task S2_ResendActivationAsync_ReturnsSuccessSilently_WhenDailyCapReached()
    {
        var activation = SampleActivation(lastSentAt: Now.AddMinutes(-10), resendCountToday: AccountOnboardingService.MaxResendsPerDay);
        _repository.FindLatestPendingByEmailAsync("ana@acme.com", Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<AccountActivation>, BaseError>(Maybe.From(activation)));

        var result = await _sut.ResendActivationAsync("ana@acme.com", CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _notifier.DidNotReceiveWithAnyArgs().SendActivationEmailAsync(default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task S3_ActivateAsync_RejectsWeakPassword_WithoutConsumingToken()
    {
        var token = AccountOnboardingService.GenerateToken();

        var result = await _sut.ActivateAsync(token, "abc", CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("PASSWORD_POLICY_REJECTED");
        await _repository.DidNotReceiveWithAnyArgs().ActivateLockedAsync(default!, default, default!, default);
    }

    [Fact]
    public async Task ActivateAsync_DoesNotConsumeToken_WhenKeycloakRejectsPasswordPolicy()
    {
        var token = AccountOnboardingService.GenerateToken();
        var activation = SampleActivation(lastSentAt: Now.AddMinutes(-5), resendCountToday: 1, tokenHash: AccountOnboardingService.HashToken(token));

        _repository.ActivateLockedAsync(Arg.Any<byte[]>(), Arg.Any<DateTimeOffset>(), Arg.Any<Func<AccountActivation, Task<Result<Guid, BaseError>>>>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                var provision = ci.ArgAt<Func<AccountActivation, Task<Result<Guid, BaseError>>>>(2);
                var provisioned = await provision(activation);
                return provisioned.IsFailure
                    ? Result.Failure<ActivateAccountResult, BaseError>(provisioned.Error)
                    : Result.Success<ActivateAccountResult, BaseError>(new ActivateAccountResult(activation.EmailNormalized, activation.CompanyName, activation.RequestedPlan));
            });
        _keycloak.CreateAccountAdminAsync("ana@acme.com", "Ana Pérez", "weakpass123", activation.AccountId, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<Guid, BaseError>(AccountErrors.PasswordPolicyRejected("Falta mayúscula")));

        var result = await _sut.ActivateAsync(token, "weakpass123", CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("PASSWORD_POLICY_REJECTED");
    }

    [Fact]
    public async Task R8_KeycloakAccountProvisioner_ReusesExistingUserOnConflict_WhenAccountIdMatches()
    {
        var accountId = Guid.NewGuid();
        var existingUserId = Guid.NewGuid();
        var handler = new FakeKeycloakHandler(req =>
        {
            if (req.Method == HttpMethod.Post && req.RequestUri!.PathAndQuery.EndsWith("users", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.Conflict);
            if (req.Method == HttpMethod.Get && req.RequestUri!.PathAndQuery.Contains("users?email=", StringComparison.Ordinal))
            {
                var payload = JsonSerializer.Serialize(new[]
                {
                    new { id = existingUserId.ToString("D"), attributes = new Dictionary<string, string[]> { ["account_id"] = [accountId.ToString("D")] } }
                });
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
            }
            if (req.Method == HttpMethod.Get && req.RequestUri!.PathAndQuery.Contains("roles/account_admin", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"id":"role-1","name":"account_admin"}""", Encoding.UTF8, "application/json") };
            if (req.Method == HttpMethod.Post && req.RequestUri!.PathAndQuery.Contains("role-mappings/realm", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.NoContent);

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://keycloak.local/admin/realms/sorteo360/") };
        var provisioner = new KeycloakAccountProvisioner(http, NullLogger<KeycloakAccountProvisioner>.Instance);

        var result = await provisioner.CreateAccountAdminAsync("ana@acme.com", "Ana Pérez", "StrongPass2026", accountId, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(existingUserId);
    }

    [Fact]
    public async Task R8_KeycloakAccountProvisioner_FailsActivation_WhenRoleAssignmentFails()
    {
        var accountId = Guid.NewGuid();
        var createdUserId = Guid.NewGuid();
        var handler = new FakeKeycloakHandler(req =>
        {
            if (req.Method == HttpMethod.Post && req.RequestUri!.PathAndQuery.EndsWith("users", StringComparison.Ordinal))
            {
                var resp = new HttpResponseMessage(HttpStatusCode.Created);
                resp.Headers.Location = new Uri($"http://keycloak.local/admin/realms/sorteo360/users/{createdUserId:D}");
                return resp;
            }
            if (req.Method == HttpMethod.Get && req.RequestUri!.PathAndQuery.Contains("roles/account_admin", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"id":"role-1","name":"account_admin"}""", Encoding.UTF8, "application/json") };
            if (req.Method == HttpMethod.Post && req.RequestUri!.PathAndQuery.Contains("role-mappings/realm", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://keycloak.local/admin/realms/sorteo360/") };
        var provisioner = new KeycloakAccountProvisioner(http, NullLogger<KeycloakAccountProvisioner>.Instance);

        var result = await provisioner.CreateAccountAdminAsync("ana@acme.com", "Ana Pérez", "StrongPass2026", accountId, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("IDENTITY_PROVIDER_UNAVAILABLE");
    }

    [Fact]
    public async Task R8_KeycloakAccountProvisioner_DoesNotInventGuid_WhenLocationMissingAndLookupFails()
    {
        var accountId = Guid.NewGuid();
        var handler = new FakeKeycloakHandler(req =>
        {
            if (req.Method == HttpMethod.Post && req.RequestUri!.PathAndQuery.EndsWith("users", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.Created); // Sin Location
            if (req.Method == HttpMethod.Get && req.RequestUri!.PathAndQuery.Contains("users?email=", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]", Encoding.UTF8, "application/json") };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://keycloak.local/admin/realms/sorteo360/") };
        var provisioner = new KeycloakAccountProvisioner(http, NullLogger<KeycloakAccountProvisioner>.Instance);

        var result = await provisioner.CreateAccountAdminAsync("ana@acme.com", "Ana Pérez", "StrongPass2026", accountId, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("IDENTITY_PROVIDER_UNAVAILABLE");
    }

    [Fact]
    public void PlanCatalog_NextMonthlyResetUtc_ReturnsFirstOfNextMonthInSantoDomingo()
    {
        var sample = new DateTimeOffset(2026, 11, 19, 18, 0, 0, TimeSpan.Zero);
        var reset = PlanCatalog.NextMonthlyResetUtc(sample);

        // 2026-12-01 00:00 AST (UTC-4) == 2026-12-01 04:00:00Z
        reset.ShouldBe(new DateTimeOffset(2026, 12, 1, 4, 0, 0, TimeSpan.Zero));
    }

    private static AccountActivation SampleActivation(
        DateTimeOffset lastSentAt,
        int resendCountToday,
        DateTimeOffset? expiresAt = null,
        byte[]? tokenHash = null) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "Acme RD",
        "Ana Pérez",
        "ana@acme.com",
        PlanCatalog.Pro,
        "monthly",
        tokenHash ?? [1, 2, 3],
        expiresAt ?? Now.AddHours(24),
        null,
        Now.AddHours(-1),
        lastSentAt,
        resendCountToday,
        DateOnly.FromDateTime(Now.UtcDateTime));

    private sealed class FakeKeycloakHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
