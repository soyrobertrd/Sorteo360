using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Infrastructure.Database;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Npgsql;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.Database;

/// <summary>
/// Las restricciones de la tabla <c>raffle</c>, probadas con SQL directo y sin pasar por el
/// endpoint. "A lo sumo un sorteo por evento" lo dice el índice único y nada en el código: la
/// comprobación del servicio solo elige el código que lee el cliente, y dos altas simultáneas la
/// pasarían las dos. Los CHECK defienden los valores que el validador ya rechaza, para que una fila
/// escrita a mano tampoco pueda traerlos.
/// <para>
/// Lo mismo vale para las rondas: "de uno a n, sin huecos ni repetidos" lo dice el único diferido
/// sobre <c>(raffle_id, number)</c>, y "una ronda con premios no se elimina" lo respalda la FK.
/// </para>
/// </summary>
public sealed class RaffleConstraintTests : IClassFixture<EventsApiDbFactory>
{
    private readonly EventsApiDbFactory _factory;

    public RaffleConstraintTests(EventsApiDbFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Insert_IsRejected_WhenTheEventAlreadyHasARaffle()
    {
        var eventId = await InsertEventAsync();
        await InsertRaffleAsync(eventId);

        var exception = await Should.ThrowAsync<PostgresException>(() => InsertRaffleAsync(eventId));

        exception.ConstraintName.ShouldBe("ux_raffle_event_id");
    }

    [Fact]
    public async Task Insert_IsRejected_WhenTheVisualStyleIsTheLegacyTombola()
    {
        var eventId = await InsertEventAsync();

        var exception = await Should.ThrowAsync<PostgresException>(() =>
            InsertRaffleAsync(eventId, visualStyle: "tombola"));

        exception.ConstraintName.ShouldBe("ck_raffle_visual_style");
    }

    [Theory]
    [InlineData(4)]
    [InlineData(31)]
    public async Task Insert_IsRejected_WhenTheAnimationLengthIsOutOfRange(int seconds)
    {
        var eventId = await InsertEventAsync();

        var exception = await Should.ThrowAsync<PostgresException>(() =>
            InsertRaffleAsync(eventId, animationSeconds: seconds));

        exception.ConstraintName.ShouldBe("ck_raffle_animation_seconds");
    }

    [Fact]
    public async Task Insert_IsRejected_WhenTheStateIsNotOneOfTheThree()
    {
        var eventId = await InsertEventAsync();

        var exception = await Should.ThrowAsync<PostgresException>(() =>
            InsertRaffleAsync(eventId, state: "configured"));

        exception.ConstraintName.ShouldBe("ck_raffle_state");
    }

    [Theory]
    [InlineData("draft", "roulette", 5)]
    [InlineData("in_progress", "roulette", 30)]
    [InlineData("completed", "sequential_names", 6)]
    public async Task Insert_IsAccepted_WhenEveryValueIsInItsCatalogue(string state, string visualStyle, int seconds)
    {
        var eventId = await InsertEventAsync();

        await Should.NotThrowAsync(() => InsertRaffleAsync(eventId, state, visualStyle, seconds));
    }

    public static TheoryData<string, bool, bool> CompletionOutOfStep => new()
    {
        { "completed", false, false },
        { "completed", true, false },
        { "completed", false, true },
        { "in_progress", true, true },
        { "draft", true, true },
    };

    [Theory]
    [MemberData(nameof(CompletionOutOfStep))]
    public async Task Insert_IsRejected_WhenTheCompletionStampIsOutOfStepWithTheState(
        string state, bool withAt, bool withBy)
    {
        var eventId = await InsertEventAsync();

        var exception = await Should.ThrowAsync<PostgresException>(() => InsertRaffleAsync(
            eventId, state, "roulette", 6,
            completedAt: withAt ? DateTimeOffset.UtcNow : null,
            completedById: withBy ? Guid.NewGuid() : null));

        exception.ConstraintName.ShouldBe("ck_raffle_completed");
    }

    [Fact]
    public async Task Commit_IsRejected_WhenTwoRoundsOfARaffleShareANumber()
    {
        var raffleId = await InsertRaffleAsync(await InsertEventAsync());

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        await using var transaction = await context.Database.BeginTransactionAsync();

        // Diferido: las dos filas entran, y es la confirmación la que choca.
        await InsertRoundAsync(context, raffleId, 1);
        await InsertRoundAsync(context, raffleId, 1);

        var exception = await Should.ThrowAsync<PostgresException>(() => transaction.CommitAsync());

        exception.ConstraintName.ShouldBe("ux_raffle_round_raffle_id_number");
    }

    [Fact]
    public async Task Insert_IsRejected_WhenTheRaffleAlreadyHasAnOverrideForTheEmail()
    {
        var raffleId = await InsertRaffleAsync(await InsertEventAsync());
        await InsertOverrideAsync(raffleId, "ana@agencia.com", "included");

        var exception = await Should.ThrowAsync<PostgresException>(() =>
            InsertOverrideAsync(raffleId, "ana@agencia.com", "excluded"));

        exception.ConstraintName.ShouldBe("ux_raffle_attendee_override_raffle_email");
    }

    [Fact]
    public async Task Insert_Succeeds_WhenAnotherRaffleHasAnOverrideForTheSameEmail()
    {
        await InsertOverrideAsync(await InsertRaffleAsync(await InsertEventAsync()), "ana@agencia.com", "included");

        await InsertOverrideAsync(await InsertRaffleAsync(await InsertEventAsync()), "ana@agencia.com", "included");
    }

    [Fact]
    public async Task Insert_IsRejected_WhenTheOverrideDecisionIsNotOneOfTheTwo()
    {
        var raffleId = await InsertRaffleAsync(await InsertEventAsync());

        var exception = await Should.ThrowAsync<PostgresException>(() =>
            InsertOverrideAsync(raffleId, "ana@agencia.com", "maybe"));

        exception.ConstraintName.ShouldBe("ck_raffle_attendee_override_decision");
    }

    [Fact]
    public async Task Insert_IsRejected_WhenTheRoundNumberIsZero()
    {
        var raffleId = await InsertRaffleAsync(await InsertEventAsync());

        var exception = await Should.ThrowAsync<PostgresException>(() => InsertRoundAsync(raffleId, 0));

        exception.ConstraintName.ShouldBe("ck_raffle_round_number");
    }

    [Fact]
    public async Task Delete_IsRejected_WhenTheRoundHoldsAPrize()
    {
        var raffleId = await InsertRaffleAsync(await InsertEventAsync());
        var roundId = await InsertRoundAsync(raffleId, 1);
        await InsertPrizeAsync(roundId);

        var exception = await Should.ThrowAsync<PostgresException>(() =>
            ExecuteAsync(context => context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM events.raffle_round WHERE id = {roundId}")));

        exception.ConstraintName.ShouldBe("fk_prize_raffle_round");
    }

    [Fact]
    public async Task Renumber_IsAccepted_WhenOneStatementShiftsTheRoundsBehindADeletedOne()
    {
        var raffleId = await InsertRaffleAsync(await InsertEventAsync());
        var first = await InsertRoundAsync(raffleId, 1);
        for (var number = 2; number <= 4; number++)
            await InsertRoundAsync(raffleId, number);

        await ExecuteAsync(context => context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM events.raffle_round WHERE id = {first}"));

        // Con un único inmediato, que esto pase dependería del orden físico de las filas: se
        // comprueba fila por fila, y 3 → 2 choca si la fila 2 todavía no bajó.
        await Should.NotThrowAsync(() => ExecuteAsync(context => context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE events.raffle_round SET number = number - 1 WHERE raffle_id = {raffleId} AND number > 1")));

        await using var scope = _factory.Services.CreateAsyncScope();
        var numbers = await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Database
            .SqlQuery<int>($"SELECT number AS \"Value\" FROM events.raffle_round WHERE raffle_id = {raffleId} ORDER BY number")
            .ToListAsync();
        numbers.ShouldBe([1, 2, 3]);
    }

    /// <summary>
    /// Lo que el sorteo deliberadamente no tiene, afirmado como ausencia para que no vuelva por
    /// descuido: nombre y fecha propios (son del evento), cantidad en el premio (cada fila es un
    /// premio), tabla de anulaciones (llega con su propio paso) y el estilo heredado <c>tombola</c>.
    /// </summary>
    [Theory]
    [InlineData("raffle", "name")]
    [InlineData("raffle", "scheduled_at")]
    [InlineData("prize", "quantity")]
    public async Task Schema_HasNoColumn_WhenTheSpecRulesItOut(string table, string column)
    {
        var count = await CountAsync($"""
            SELECT count(*)::int AS "Value" FROM information_schema.columns
            WHERE table_schema = 'events' AND table_name = {table} AND column_name = {column}
            """);

        count.ShouldBe(0);
    }

    [Fact]
    public async Task Schema_HasNoPrizeVoidTable_WhenMigrated()
    {
        var count = await CountAsync($"""
            SELECT count(*)::int AS "Value" FROM information_schema.tables
            WHERE table_schema = 'events' AND table_name = 'prize_void'
            """);

        count.ShouldBe(0);
    }

    [Fact]
    public async Task Schema_HasNoCheckAllowingTombola_WhenMigrated()
    {
        // 'ball_tombola' sí existe; lo que no puede aparecer es el literal heredado solo.
        var count = await CountAsync($"""
            SELECT count(*)::int AS "Value" FROM pg_constraint c
            JOIN pg_namespace n ON n.oid = c.connamespace
            WHERE n.nspname = 'events' AND c.contype = 'c'
              AND pg_get_constraintdef(c.oid) LIKE '%''tombola''%'
            """);

        count.ShouldBe(0);
    }

    [Fact]
    public async Task Schema_HasRaffleParticipantTable_WhenMigrated()
    {
        var count = await CountAsync($"""
            SELECT count(*)::int AS "Value" FROM information_schema.tables
            WHERE table_name = 'raffle_participant'
            """);

        count.ShouldBe(1);
    }

    [Fact]
    public async Task Schema_HasOneFrozenRowPerPersonAndRaffle_WhenMigrated()
    {
        var count = await CountAsync($"""
            SELECT count(*)::int AS "Value" FROM pg_indexes
            WHERE schemaname = 'events' AND tablename = 'raffle_attendee'
              AND indexname = 'ux_raffle_attendee_raffle_email'
              AND indexdef LIKE 'CREATE UNIQUE INDEX%(raffle_id, email_normalized)'
            """);

        count.ShouldBe(1);
    }

    [Fact]
    public async Task Insert_IsRejected_WhenThePrizeAlreadyHasALiveDraw()
    {
        var (prizeId, attendeeId) = await InsertDrawablePrizeAsync();
        await InsertDrawAsync(prizeId, attendeeId);

        var exception = await Should.ThrowAsync<PostgresException>(() => InsertDrawAsync(prizeId, attendeeId));

        exception.ConstraintName.ShouldBe("ux_prize_draw_prize_id_live");
    }

    [Fact]
    public async Task Insert_IsAccepted_WhenThePrizesEarlierDrawIsVoided()
    {
        var (prizeId, attendeeId) = await InsertDrawablePrizeAsync();
        await InsertDrawAsync(prizeId, attendeeId, voidCode: "absent", voided: true);

        await Should.NotThrowAsync(() => InsertDrawAsync(prizeId, attendeeId));
    }

    [Fact]
    public async Task Insert_IsRejected_WhenAnotherRaffleAlreadyUsesTheRedemptionCode()
    {
        var first = await InsertDrawablePrizeAsync();
        var second = await InsertDrawablePrizeAsync();
        await InsertDrawAsync(first.PrizeId, first.AttendeeId, code: "RD-AAAA-0000");

        var exception = await Should.ThrowAsync<PostgresException>(() =>
            InsertDrawAsync(second.PrizeId, second.AttendeeId, code: "RD-AAAA-0000"));

        exception.ConstraintName.ShouldBe("ux_prize_draw_redemption_code");
    }

    [Fact]
    public async Task Schema_KeysTheRedemptionCodeIndexOnTheCodeAlone_WhenMigrated()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var columns = await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Database
            .SqlQuery<string>($"""
                SELECT a.attname AS "Value" FROM pg_index i
                JOIN pg_class c ON c.oid = i.indexrelid
                JOIN pg_attribute a ON a.attrelid = i.indrelid AND a.attnum = ANY (i.indkey)
                WHERE c.relname = 'ux_prize_draw_redemption_code' AND i.indisunique
                """)
            .ToListAsync();

        columns.ShouldBe(["redemption_code"]);
    }

    [Theory]
    [InlineData("absent", false)]
    [InlineData(null, true)]
    public async Task Insert_IsRejected_WhenTheVoidIsHalfSet(string? voidCode, bool voided)
    {
        var (prizeId, attendeeId) = await InsertDrawablePrizeAsync();

        var exception = await Should.ThrowAsync<PostgresException>(() =>
            InsertDrawAsync(prizeId, attendeeId, voidCode: voidCode, voided: voided));

        exception.ConstraintName.ShouldBe("ck_prize_draw_void_complete");
    }

    [Theory]
    [InlineData("rd-aaaa-0000", "ck_prize_draw_redemption_code")]
    [InlineData("RD-IIII-0000", "ck_prize_draw_redemption_code")]
    public async Task Insert_IsRejected_WhenTheRedemptionCodeBreaksThePattern(string code, string constraint)
    {
        var (prizeId, attendeeId) = await InsertDrawablePrizeAsync();

        var exception = await Should.ThrowAsync<PostgresException>(() => InsertDrawAsync(prizeId, attendeeId, code: code));

        exception.ConstraintName.ShouldBe(constraint);
    }

    [Fact]
    public async Task Update_IsRejected_WhenTheWinnerNotificationStatusIsNotOneOfTheThree()
    {
        var (prizeId, attendeeId) = await InsertDrawablePrizeAsync();
        await InsertDrawAsync(prizeId, attendeeId);

        var exception = await Should.ThrowAsync<PostgresException>(() =>
            ExecuteAsync(context => context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE events.prize_draw SET winner_notification_status = 'delivered' WHERE prize_id = {prizeId}")));

        exception.ConstraintName.ShouldBe("ck_prize_draw_winner_notification_status");
    }

    [Theory]
    [InlineData("sent")]
    [InlineData("no_recipient")]
    [InlineData("failed")]
    public async Task Update_IsAccepted_WhenTheWinnerNotificationStatusIsOneOfTheThree(string status)
    {
        var (prizeId, attendeeId) = await InsertDrawablePrizeAsync();
        await InsertDrawAsync(prizeId, attendeeId);

        await Should.NotThrowAsync(() =>
            ExecuteAsync(context => context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE events.prize_draw SET winner_notification_status = {status} WHERE prize_id = {prizeId}")));
    }

    [Fact]
    public async Task Delete_IsRejected_WhenThePrizeHasADraw()
    {
        var (prizeId, attendeeId) = await InsertDrawablePrizeAsync();
        await InsertDrawAsync(prizeId, attendeeId);

        var exception = await Should.ThrowAsync<PostgresException>(() =>
            ExecuteAsync(context => context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM events.prize WHERE id = {prizeId}")));

        exception.ConstraintName.ShouldBe("fk_prize_draw_prize");
    }

    private async Task<Guid> InsertEventAsync()
    {
        var eventId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO events.event
                (id, type, name, start_date, end_date, status, visibility, language,
                 created_at, updated_at, created_by_id, updated_by_id)
            VALUES
                ({eventId}, {EventType.Feria}, 'Feria del sorteo', {new DateOnly(2026, 10, 1)},
                 {new DateOnly(2026, 10, 5)}, {EventStatus.Planning}, 'private', 'es',
                 now(), now(), {actorId}, {actorId})
            """);

        return eventId;
    }

    private async Task<Guid> InsertRaffleAsync(
        Guid eventId, string state = "draft", string visualStyle = "roulette", int animationSeconds = 6)
    {
        var closed = state == "completed";
        return await InsertRaffleAsync(
            eventId, state, visualStyle, animationSeconds,
            completedAt: closed ? DateTimeOffset.UtcNow : null,
            completedById: closed ? Guid.NewGuid() : null);
    }

    private async Task<Guid> InsertRaffleAsync(
        Guid eventId, string state, string visualStyle, int animationSeconds,
        DateTimeOffset? completedAt, Guid? completedById)
    {
        var raffleId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO events.raffle
                (id, event_id, state, visual_style, show_prize_on_screen, animation_seconds,
                 requires_presence_confirmation, one_prize_per_person, one_prize_per_company,
                 created_at, updated_at, created_by_id, updated_by_id, completed_at, completed_by_id)
            VALUES
                ({raffleId}, {eventId}, {state}, {visualStyle}, true, {animationSeconds},
                 false, true, false, now(), now(), {actorId}, {actorId}, {completedAt}, {completedById})
            """);

        return raffleId;
    }

    private Task InsertOverrideAsync(Guid raffleId, string email, string decision) =>
        ExecuteAsync(context => context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO events.raffle_attendee_override
                (id, raffle_id, email_normalized, decision, decided_by_id, decided_at)
            VALUES
                ({Guid.NewGuid()}, {raffleId}, {email}, {decision}, {Guid.NewGuid()}, now())
            """));

    private async Task<Guid> InsertRoundAsync(Guid raffleId, int number)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await InsertRoundAsync(scope.ServiceProvider.GetRequiredService<EventsDbContext>(), raffleId, number);
    }

    private static async Task<Guid> InsertRoundAsync(EventsDbContext context, Guid raffleId, int number)
    {
        var roundId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO events.raffle_round
                (id, raffle_id, number, name, created_at, updated_at, created_by_id, updated_by_id)
            VALUES
                ({roundId}, {raffleId}, {number}, 'Ronda', now(), now(), {actorId}, {actorId})
            """);

        return roundId;
    }

    /// <summary>Un premio en un sorteo en curso con una persona congelada que lo puede ganar.</summary>
    private async Task<(Guid PrizeId, Guid AttendeeId)> InsertDrawablePrizeAsync()
    {
        var raffleId = await InsertRaffleAsync(await InsertEventAsync(), state: "in_progress");
        var roundId = await InsertRoundAsync(raffleId, 1);
        var prizeId = await InsertPrizeAsync(roundId);
        var attendeeId = Guid.NewGuid();

        await ExecuteAsync(context => context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO events.raffle_attendee
                (id, raffle_id, email_normalized, first_name, last_name, checked_in_at, qualified)
            VALUES
                ({attendeeId}, {raffleId}, 'maria@agencia.com', 'María', 'Pérez', now(), true)
            """));

        return (prizeId, attendeeId);
    }

    private Task InsertDrawAsync(Guid prizeId, Guid attendeeId, string? code = null, string? voidCode = null, bool voided = false)
    {
        var redemptionCode = code ?? RedemptionCode.Generate();
        DateTimeOffset? voidedAt = voided ? DateTimeOffset.UtcNow : null;
        Guid? voidedById = voided ? Guid.NewGuid() : null;

        return ExecuteAsync(context => context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO events.prize_draw
                (id, prize_id, winner_attendee_id, winner_name, winner_email, drawn_at, drawn_by_id,
                 eligible_pool_size, one_prize_per_person, one_prize_per_company, redemption_code, void_code,
                 voided_at, voided_by_id)
            VALUES
                ({Guid.NewGuid()}, {prizeId}, {attendeeId}, 'María Pérez', 'maria@agencia.com', now(), {Guid.NewGuid()},
                 1, true, false, {redemptionCode}, {voidCode}, {voidedAt}, {voidedById})
            """));
    }

    private async Task<Guid> InsertPrizeAsync(Guid roundId)
    {
        var prizeId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        await ExecuteAsync(context => context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO events.prize
                (id, round_id, name, created_at, updated_at, created_by_id, updated_by_id)
            VALUES
                ({prizeId}, {roundId}, 'Bolsa', now(), now(), {actorId}, {actorId})
            """));

        return prizeId;
    }

    private async Task ExecuteAsync(Func<EventsDbContext, Task> action)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<EventsDbContext>());
    }

    private async Task<int> CountAsync(FormattableString sql)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Database
            .SqlQuery<int>(sql)
            .SingleAsync();
    }
}
