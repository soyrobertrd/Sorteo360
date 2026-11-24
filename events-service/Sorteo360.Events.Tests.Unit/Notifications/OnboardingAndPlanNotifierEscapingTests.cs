using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Sorteo360.Events.Core.Accounts;
using Sorteo360.Events.Infrastructure.Notifications;

namespace Sorteo360.Events.Tests.Unit.Notifications;

/// <summary>
/// Regresión de la auditoría run-1 (confirmado 5): lo que escribe quien se registra o pide un plan
/// no puede ir dentro del cuerpo HTML. Viaja en <c>SharedData</c>, que notification-service escapa,
/// y cada marcador del texto tiene su valor (si faltara, notification-service rechaza el envío).
/// </summary>
public sealed class OnboardingAndPlanNotifierEscapingTests
{
    private const string Hostile = "<a href=\"https://evil.example\">Juan</a> & {{codigo}}";

    private readonly INotificationClient _client = Substitute.For<INotificationClient>();
    private readonly List<SendNotificationRequest> _sent = [];

    public OnboardingAndPlanNotifierEscapingTests()
    {
        _client.SendAsync(Arg.Do<SendNotificationRequest>(_sent.Add), Arg.Any<CancellationToken>())
            .Returns(new SendNotificationResponse(Guid.NewGuid()));
    }

    [Fact]
    public async Task ActivationEmail_CarriesNameAndCompanyAsValues_NotInsideTheHtml()
    {
        var sut = new AccountOnboardingNotifier(
            _client,
            Options.Create(new NotificationSettings { FromAddress = "no-reply@sorteo360.local" }),
            Options.Create(new AppSettings { BaseUrl = "https://sorteo360.local" }),
            NullLogger<AccountOnboardingNotifier>.Instance);

        await sut.SendActivationEmailAsync("a@b.com", Hostile, Hostile, "tok", CancellationToken.None);

        var request = _sent.ShouldHaveSingleItem();
        AssertValuesOnly(request);
        request.SharedData["nombre"].ShouldBe(Hostile);
        request.SharedData["empresa"].ShouldBe(Hostile);
    }

    [Fact]
    public async Task PlanRequestEmails_CarryAccountReferenceAndReasonAsValues_NotInsideTheHtml()
    {
        var sut = new PlanRequestNotifier(
            _client,
            Options.Create(new NotificationSettings { FromAddress = "no-reply@sorteo360.local" }),
            Options.Create(new PlanBillingSettings { PlatformAdminEmail = "admin@sorteo360.local" }),
            NullLogger<PlanRequestNotifier>.Instance);
        var request = new PlanRequest(
            Guid.NewGuid(), Guid.NewGuid(), Hostile, "Pro", PlanRequestStatus.Pending, Hostile,
            DateTimeOffset.UtcNow, Guid.NewGuid(), "a@b.com", null, null, Hostile, null, null);
        var account = new Account(
            Guid.NewGuid(), Hostile, "Pro", AccountStatus.Active, "owner@b.com",
            null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        await sut.NotifyRequestedAsync(request, CancellationToken.None);
        await sut.NotifyRejectedAsync(request, CancellationToken.None);
        await sut.NotifyApprovedAsync(request, account, CancellationToken.None);

        _sent.Count.ShouldBe(4);
        foreach (var sent in _sent)
            AssertValuesOnly(sent);
        _sent.ShouldAllBe(s => s.SharedData["cuenta"] == Hostile);
    }

    private static void AssertValuesOnly(SendNotificationRequest request)
    {
        request.Body.ShouldNotContain("<a", Case.Sensitive);
        request.Subject.ShouldNotContain("<a", Case.Sensitive);

        var markers = Regex.Matches(request.Subject + request.Body, @"\{\{([^{}]*)\}\}")
            .Select(m => m.Groups[1].Value.Trim());
        foreach (var marker in markers)
            request.SharedData.ShouldContainKey(marker);
    }
}
