using System.Diagnostics;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using NSubstitute;
using Shouldly;
using Sorteo360.Events.Core.CompanyDirectory;
using Sorteo360.Events.Core.CrmDirectory;
using Sorteo360.Events.Core.Database;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Files;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Infrastructure.Raffles;
using Sorteo360.Events.Tests.Unit.Shared;
using Sorteo360.Events.Web.RaffleEndpoints;
using Sorteo360.Platform.Auth;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.Files;

namespace Sorteo360.Events.Tests.Unit.Raffles;

/// <summary>
/// Pruebas de regresión de los hallazgos de la revisión de código (R1, R2, R3, R10, R12).
/// </summary>
public sealed class ReviewFindingsRegressionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task R1_SameCompanyDistinctDepartments_AllTenPrizesCanBeDrawnAndDtoMatchesPool()
    {
        var eventId = Guid.NewGuid();
        var participants = Enumerable.Range(1, 10)
            .Select(i => new RaffleParticipant(
                id: Guid.NewGuid(),
                eventId: eventId,
                fullName: $"Colaborador {i}",
                email: $"colab{i}@banreservas.com",
                company: "Banreservas",
                department: $"Departamento {i}",
                employeeCode: $"EMP-{i:D4}",
                phone: null,
                qrCode: $"QR-{i}",
                checkedIn: true,
                checkedInAt: Now,
                source: "excel",
                createdAt: Now,
                updatedAt: Now,
                ticketNumber: $"000-{i:D3}"))
            .ToList();

        var repo = Substitute.For<IRaffleParticipantRepository>();
        repo.ListByEventIdAsync(eventId, Arg.Any<CancellationToken>(), Arg.Any<bool>())
            .Returns(Result.Success<IReadOnlyList<RaffleParticipant>, BaseError>(participants));

        var source = new RosterAttendanceSource(repo);
        var attendeesResult = await source.GetAttendeesAsync(eventId, requiresCheckIn: true, CancellationToken.None);
        attendeesResult.IsSuccess.ShouldBeTrue();

        var frozen = attendeesResult.Value
            .Select((a, idx) => new RaffleAttendee(
                Id: participants[idx].Id,
                IdentityKey: a.IdentityKey,
                ContactId: null,
                FirstName: a.FirstName,
                LastName: a.LastName,
                Company: a.Company,
                CheckedInAt: a.CheckedInAt,
                Qualified: true,
                ExclusionReason: null,
                ManualDecision: null,
                ManualDecidedById: null,
                ManualDecidedAt: null,
                ManualNote: null,
                OverruledReason: null))
            .ToList();

        var draws = new List<PrizeDraw>();
        var raffle = Raffle.CreateDraft(eventId, Guid.NewGuid(), Now) with { OnePrizePerPerson = true, OnePrizePerCompany = true };

        for (var round = 0; round < 10; round++)
        {
            var pool = EligiblePool.For(frozen, draws, onePrizePerCompany: true);
            pool.Count.ShouldBe(10 - round);

            var winner = pool.Members[0];
            draws.Add(new PrizeDraw(
                Guid.NewGuid(), Guid.NewGuid(), winner.Id, winner.DisplayName, winner.Company, winner.IdentityKey,
                Now.AddSeconds(round), Guid.NewGuid(), pool.Count, true, true, $"CODE-{round}", null, null, null, null));

            // Verificar que el DTO del padrón coincide exactamente con la tómbola
            var liveWinnerIdentityKeys = draws.Select(d => d.WinnerIdentityKey).ToHashSet(StringComparer.Ordinal);
            var liveWinnerKeys = draws.Select(d => CompanyKey.Normalize(d.WinnerCompany)).Where(k => k.HasValue).Select(k => k.Value).ToHashSet(StringComparer.Ordinal);
            var ctx = new RaffleParticipantWebhookEndpoints.RaffleParticipantContext(
                new RaffleWithRounds(raffle, []) { Draws = draws },
                new Dictionary<string, string>(),
                liveWinnerIdentityKeys,
                liveWinnerKeys);

            var remainingEligibleDtos = participants
                .Select(p => RaffleParticipantWebhookEndpoints.MapToDto(p, ctx))
                .Count(dto => dto.Qualified && dto.ExclusionReason is null);

            remainingEligibleDtos.ShouldBe(10 - round - 1);
        }
    }

    [Fact]
    public async Task R2_RequiresCheckInFalse_AllowsUncheckedInParticipantInRosterAndDto()
    {
        var eventId = Guid.NewGuid();
        var uncheckedParticipant = new RaffleParticipant(
            id: Guid.NewGuid(),
            eventId: eventId,
            fullName: "María Gómez",
            email: "maria@empresa.com",
            company: "Empresa",
            department: "Finanzas",
            employeeCode: "E-100",
            phone: null,
            qrCode: "QR-100",
            checkedIn: false,
            checkedInAt: null,
            source: "excel",
            createdAt: Now,
            updatedAt: Now,
            ticketNumber: "100-200");

        var repo = Substitute.For<IRaffleParticipantRepository>();
        repo.ListByEventIdAsync(eventId, Arg.Any<CancellationToken>(), Arg.Any<bool>())
            .Returns(Result.Success<IReadOnlyList<RaffleParticipant>, BaseError>([uncheckedParticipant]));

        var source = new RosterAttendanceSource(repo);

        var withCheckIn = await source.GetAttendeesAsync(eventId, requiresCheckIn: true, CancellationToken.None);
        withCheckIn.IsSuccess.ShouldBeTrue();
        withCheckIn.Value.ShouldBeEmpty();

        var withoutCheckIn = await source.GetAttendeesAsync(eventId, requiresCheckIn: false, CancellationToken.None);
        withoutCheckIn.IsSuccess.ShouldBeTrue();
        withoutCheckIn.Value.Count.ShouldBe(1);

        var raffleNoCheckIn = Raffle.CreateDraft(eventId, Guid.NewGuid(), Now) with { RequiresCheckIn = false };
        var ctxNoCheckIn = new RaffleParticipantWebhookEndpoints.RaffleParticipantContext(
            new RaffleWithRounds(raffleNoCheckIn, []),
            new Dictionary<string, string>(),
            new HashSet<string>(),
            new HashSet<string>());
        var dtoNoCheckIn = RaffleParticipantWebhookEndpoints.MapToDto(uncheckedParticipant, ctxNoCheckIn);
        dtoNoCheckIn.Qualified.ShouldBeTrue();
        dtoNoCheckIn.ExclusionReason.ShouldBeNull();

        var raffleRequireCheckIn = Raffle.CreateDraft(eventId, Guid.NewGuid(), Now) with { RequiresCheckIn = true };
        var ctxRequireCheckIn = new RaffleParticipantWebhookEndpoints.RaffleParticipantContext(
            new RaffleWithRounds(raffleRequireCheckIn, []),
            new Dictionary<string, string>(),
            new HashSet<string>(),
            new HashSet<string>());
        var dtoRequireCheckIn = RaffleParticipantWebhookEndpoints.MapToDto(uncheckedParticipant, ctxRequireCheckIn);
        dtoRequireCheckIn.Qualified.ShouldBeFalse();
        dtoRequireCheckIn.ExclusionReason.ShouldBe("not_checked_in");
    }

    [Fact]
    public void R3_AddKeycloakAuthentication_ThrowsWithoutAuthorityAndWithoutLocalDevFlag()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();
        var devEnv = new FakeWebHostEnvironment("Development");

        Should.Throw<InvalidOperationException>(() => services.AddKeycloakAuthentication(config, devEnv));
    }

    [Fact]
    public void R3_AddKeycloakAuthentication_ThrowsWhenLocalDevRequestedInProduction()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:Mode"] = "LocalDev" })
            .Build();
        var prodEnv = new FakeWebHostEnvironment("Production");

        Should.Throw<InvalidOperationException>(() => services.AddKeycloakAuthentication(config, prodEnv));
    }

    [Fact]
    public async Task R10_ReinstateAttendeeAsync_RejectsCancelledEvent()
    {
        var eventId = Guid.NewGuid();
        var raffle = Raffle.CreateDraft(eventId, Guid.NewGuid(), Now) with { State = RaffleState.InProgress };

        var raffleRepo = Substitute.For<IRaffleRepository>();
        raffleRepo.GetByEventIdAsync(eventId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<Raffle>, BaseError>(Maybe.From(raffle)));

        var eventRepo = Substitute.For<IEventRepository>();
        eventRepo.GetStatusAsync(eventId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<string>, BaseError>(Maybe.From(EventStatus.Cancelled)));

        var service = new RaffleService(
            raffleRepo,
            eventRepo,
            Substitute.For<IRecentAttendance>(),
            Substitute.For<IAttendanceSource>(),
            Substitute.For<IAwardsMembership>(),
            Substitute.For<IContactMailability>(),
            new CrmDirectoryStatusContext(),
            Substitute.For<IUploadPolicy>(),
            Substitute.For<IFileStore>(),
            Substitute.For<INotarialRecordRenderer>(),
            Substitute.For<IRaffleOperatorDirectory>(),
            Substitute.For<IRaffleWinnerNotifier>(),
            Substitute.For<ICompanyDirectory>(),
            Substitute.For<ICompanyContactDirectory>(),
            new TestTimeProvider());

        var result = await service.ReinstateAttendeeAsync(eventId, Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("EVENT_NOT_EDITABLE");
        await raffleRepo.DidNotReceiveWithAnyArgs().ReinstateAttendeeAsync(default, default, default, default, default);
    }

    [Fact]
    public void R12_EligiblePool_For_25000AttendeesAnd10000Draws_RunsUnder200Ms()
    {
        var raffleId = Guid.NewGuid();
        var attendees = Enumerable.Range(0, 25_000)
            .Select(i => new RaffleAttendee(
                Id: Guid.NewGuid(),
                IdentityKey: $"u{i}@empresa.com",
                ContactId: null,
                FirstName: "Nombre",
                LastName: $"Apellido {i}",
                Company: $"Depto {i % 500}",
                CheckedInAt: Now,
                Qualified: true,
                ExclusionReason: null,
                ManualDecision: null,
                ManualDecidedById: null,
                ManualDecidedAt: null,
                ManualNote: null,
                OverruledReason: null))
            .ToList();

        var draws = new List<PrizeDraw>(10_000);
        for (var i = 0; i < 10_000; i++)
        {
            var isVoided = i < 500;
            draws.Add(new PrizeDraw(
                Guid.NewGuid(),
                Guid.NewGuid(),
                attendees[i].Id,
                attendees[i].DisplayName,
                attendees[i].Company,
                attendees[i].IdentityKey,
                Now.AddSeconds(i),
                Guid.NewGuid(),
                25_000 - i,
                true,
                false,
                $"CODE-{i:D5}",
                isVoided ? PrizeDrawVoidCode.Absent : null,
                null,
                isVoided ? Now.AddSeconds(i + 1) : null,
                isVoided ? Guid.NewGuid() : null));
        }

        // Calentar JIT
        _ = EligiblePool.For(attendees.Take(100).ToList(), draws.Take(100).ToList(), false);

        var sw = Stopwatch.StartNew();
        var pool = EligiblePool.For(attendees, draws, onePrizePerCompany: false);
        sw.Stop();

        pool.Participants.ShouldBe(25_000);
        pool.ExcludedByVoid.ShouldBe(500);
        pool.ExcludedByPerson.ShouldBe(9_500);
        pool.Count.ShouldBe(15_000);
        sw.ElapsedMilliseconds.ShouldBeLessThan(200);
    }

    private sealed class FakeWebHostEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Test";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
