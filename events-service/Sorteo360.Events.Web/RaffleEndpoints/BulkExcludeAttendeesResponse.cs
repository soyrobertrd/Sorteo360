using Sorteo360.Events.Core.Raffles;

namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>Lo que hizo la exclusión en lote. Quien no se encontró se informa; no es un error.</summary>
public record BulkExcludeAttendeesResponse
{
    /// <summary>Exclusiones nuevas, o inclusiones que pasaron a exclusión.</summary>
    public int Excluded { get; init; }

    /// <summary>Ya estaban excluidas; no se tocaron.</summary>
    public int AlreadyExcluded { get; init; }

    /// <summary>No están en el padrón o en la puerta de este evento.</summary>
    public IReadOnlyList<string> NotInRoster { get; init; } = [];

    /// <summary>No parecen correo y ningún participante tiene ese ID.</summary>
    public IReadOnlyList<string> Invalid { get; init; } = [];

    /// <summary>Solo con <c>sourceEventId</c>: ganadores sin correo ni ID, por su nombre.</summary>
    public IReadOnlyList<string> Unidentifiable { get; init; } = [];

    public static BulkExcludeAttendeesResponse From(BulkExcludeOutcome outcome) => new()
    {
        Excluded = outcome.Excluded,
        AlreadyExcluded = outcome.AlreadyExcluded,
        NotInRoster = outcome.NotInRoster,
        Invalid = outcome.Invalid,
        Unidentifiable = outcome.Unidentifiable,
    };
}
