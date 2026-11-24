namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// El notario y los dos testigos que dan fe del sorteo, cada uno con nombre y documento de
/// identidad. Siempre tres personas, porque el proceso del cliente nombra tres: por eso son seis
/// columnas y no una tabla que invitaría a un cuarto sin línea en el acta (spec 00 §6).
/// <para>
/// Todos pueden quedar en blanco mientras el sorteo se arma y se corre: se exigen recién al
/// completarlo, que es cuando se genera el acta que los nombra. Un blanco nunca se rellena con un
/// nombre inventado. El documento es texto libre y opaco para este servicio.
/// </para>
/// </summary>
public sealed record RaffleAuthorities(
    string? NotaryName,
    string? NotaryIdentityDocument,
    string? WitnessOneName,
    string? WitnessOneIdentityDocument,
    string? WitnessTwoName,
    string? WitnessTwoIdentityDocument)
{
    public static RaffleAuthorities Blank { get; } = new(null, null, null, null, null, null);

    /// <summary>Si están los seis campos, ninguno en blanco: lo que completar exige (spec 00 §6).</summary>
    public bool IsComplete => new[]
    {
        NotaryName,
        NotaryIdentityDocument,
        WitnessOneName,
        WitnessOneIdentityDocument,
        WitnessTwoName,
        WitnessTwoIdentityDocument,
    }.All(value => !string.IsNullOrWhiteSpace(value));
}
