using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Raffles;

namespace Sorteo360.Events.Tests.Unit.NotarialRecords;

/// <summary>Contenidos de acta armados a mano, para probar el renderizador sin pasar por el dominio.</summary>
internal static class NotarialRecordSamples
{
    /// <summary>21:40 en Santo Domingo, que en UTC ya es el día siguiente.</summary>
    public static readonly DateTimeOffset StartedAt = new(2026, 10, 7, 1, 40, 0, TimeSpan.Zero);

    public static NotarialRecordContent Content(int participants = 2) => new(
        RecordNumber: "3F9A1C02",
        Version: 1,
        Replaces: null,
        Event: new NotarialRecordEvent(
            "Roadshow Bávaro", new DateOnly(2026, 10, 6), new DateOnly(2026, 10, 6),
            [new NotarialRecordPlace("Punta Cana", "República Dominicana", CityTimeZone.Create("America/Santo_Domingo").Value)]),
        StartedAt: StartedAt,
        CompletedAt: StartedAt.AddHours(1),
        OnePrizePerPerson: true,
        OnePrizePerCompany: false,
        RulesChanged: false,
        AwardsListApplied: false,
        Arithmetic: new NotarialRecordArithmetic(participants + 1, participants, 1,
            [new NotarialRecordExclusion(ExclusionReason.WalkIn, 1)]),
        ManualDecisions: [],
        Rounds: [new NotarialRecordRound(1, "Primera ronda", [new NotarialRecordPrize("Bolsa", null, null, null, false)])],
        DrawLog: [],
        Authorities: new RaffleAuthorities("Ana Notaria", "001-1", "Luis Testigo", "002-2", "Eva Testigo", "003-3"),
        Annex: Enumerable.Range(1, participants)
            .Select(i => new NotarialRecordParticipant(i, $"Persona Número {i}", $"Agencia Ñandú {i}"))
            .ToList());
}
