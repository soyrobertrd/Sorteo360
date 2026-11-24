using System.Security.Cryptography;
using Sorteo360.Events.Core.Raffles;

namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>
/// Quién puede ganar el premio ahora mismo: los nombres que gira la ruleta y la cuenta de
/// "N participantes elegibles" (spec 02 §5).
/// </summary>
public record EligibleResponse
{
    public const int MaxLimit = 100;

    public int Count { get; init; }

    /// <summary>Ordenados por id (o muestra aleatoria si se pidió <c>?limit=</c>, R14).</summary>
    public IReadOnlyList<EligibleParticipantResponse> Participants { get; init; } = [];

    public static EligibleResponse From(EligiblePool pool, int? limit = null)
    {
        ArgumentNullException.ThrowIfNull(pool);

        IReadOnlyList<RaffleAttendee> selected;
        if (limit is { } requested && requested > 0 && pool.Members.Count > Math.Min(requested, MaxLimit))
        {
            var sampleSize = Math.Min(requested, MaxLimit);
            var copy = pool.Members.ToArray();
            // Fisher-Yates parcial criptográfico: elige sampleSize elementos al azar en O(sampleSize).
            for (var i = 0; i < sampleSize; i++)
            {
                var j = RandomNumberGenerator.GetInt32(i, copy.Length);
                (copy[i], copy[j]) = (copy[j], copy[i]);
            }
            selected = copy[..sampleSize];
        }
        else
        {
            selected = pool.Members;
        }

        return new()
        {
            Count = pool.Count,
            Participants = selected.Select(EligibleParticipantResponse.From).ToList(),
        };
    }
}

/// <summary>Un participante del bombo. Nombre y empresa, y nada más: la lista es para un proyector.</summary>
public record EligibleParticipantResponse
{
    public Guid AttendeeId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? Company { get; init; }

    public static EligibleParticipantResponse From(RaffleAttendee attendee) => new()
    {
        AttendeeId = attendee.Id,
        DisplayName = attendee.DisplayName,
        Company = attendee.Company,
    };
}
