using Sorteo360.Events.Core.Raffles;

namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>
/// La lista de la puerta clasificada, con lo que el operador necesita para confiar en ella.
/// <para>
/// Sin ninguna copia de la puerta, <see cref="CheckedIn"/>, <see cref="Qualified"/> y
/// <see cref="Attendees"/> son nulos, nunca cero ni vacíos: no se sabe quién está, y eso no es lo
/// mismo que nadie.
/// </para>
/// </summary>
public record RaffleAttendeesResponse
{
    public int? CheckedIn { get; init; }
    public int? Qualified { get; init; }
    public DateTimeOffset? AttendanceFetchedAt { get; init; }

    /// <summary><c>ok</c>, <c>stale</c> o <c>unavailable</c>.</summary>
    public string AttendanceStatus { get; init; } = string.Empty;

    /// <summary>La lista de DO Travel Awards que se aplicó, o nulo si no hay ninguna en vigor.</summary>
    public Guid? AwardsBatchId { get; init; }

    public IReadOnlyList<RaffleAttendeeResponse>? Attendees { get; init; }

    public static RaffleAttendeesResponse From(RaffleAttendees attendees) => new()
    {
        CheckedIn = attendees.Attendees?.Count,
        Qualified = attendees.Attendees?.Count(a => a.Qualified),
        AttendanceFetchedAt = attendees.AttendanceFetchedAt,
        AttendanceStatus = attendees.AttendanceStatus,
        AwardsBatchId = attendees.AwardsBatchId,
        Attendees = attendees.Attendees?.Select(RaffleAttendeeResponse.From).ToList(),
    };
}

/// <summary>Un asistente, calificado o con el único motivo por el que no.</summary>
public record RaffleAttendeeResponse
{
    public string Email { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? Company { get; init; }
    public bool Qualified { get; init; }
    public string? ExclusionReason { get; init; }

    /// <summary><c>included</c> o <c>excluded</c>, haya ganado o no; nulo sin decisión manual.</summary>
    public string? ManualDecision { get; init; }

    /// <summary>
    /// Lo que la decisión manual venció, o lo que la venció a ella. Nulo en una exclusión y en una
    /// inclusión sobre alguien que calificaba igual.
    /// </summary>
    public string? OverruledReason { get; init; }

    /// <summary>La nota de quien decidió, para el próximo operador.</summary>
    public string? ManualNote { get; init; }

    /// <summary>Quién decidió, como id: resolver el nombre es del acta, no de esta pantalla.</summary>
    public Guid? ManualDecidedById { get; init; }

    public DateTimeOffset? ManualDecidedAt { get; init; }

    public static RaffleAttendeeResponse From(RaffleAttendee attendee) => new()
    {
        Email = attendee.EmailNormalized,
        FirstName = attendee.FirstName,
        LastName = attendee.LastName,
        Company = attendee.Company,
        Qualified = attendee.Qualified,
        ExclusionReason = attendee.ExclusionReason,
        ManualDecision = attendee.ManualDecision,
        OverruledReason = attendee.OverruledReason,
        ManualNote = attendee.ManualNote,
        ManualDecidedById = attendee.ManualDecidedById,
        ManualDecidedAt = attendee.ManualDecidedAt,
    };
}
