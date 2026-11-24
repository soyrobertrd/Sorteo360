using Sorteo360.Events.Core.Raffles;

namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>
/// La reemisión del acta: las seis autoridades, todas obligatorias, y nada más (spec 03 §4.5). Se
/// guardan recortadas.
/// </summary>
public record ReissueRaffleRequest
{
    /// <summary>El nombre del notario, de hasta 200 caracteres.</summary>
    public string? NotaryName { get; init; }

    /// <summary>Su documento de identidad, texto libre de hasta 100 caracteres.</summary>
    public string? NotaryIdentityDocument { get; init; }

    public string? WitnessOneName { get; init; }

    public string? WitnessOneIdentityDocument { get; init; }

    public string? WitnessTwoName { get; init; }

    public string? WitnessTwoIdentityDocument { get; init; }

    /// <summary>Las seis, recortadas. Solo después de validar: ninguna llega en blanco.</summary>
    public RaffleAuthorities ToAuthorities() => new(
        NotaryName!.Trim(),
        NotaryIdentityDocument!.Trim(),
        WitnessOneName!.Trim(),
        WitnessOneIdentityDocument!.Trim(),
        WitnessTwoName!.Trim(),
        WitnessTwoIdentityDocument!.Trim());
}
