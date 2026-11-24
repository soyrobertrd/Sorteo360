namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Por qué se anuló un sorteo de premio (spec 02 §3): un código cerrado y no prosa, porque el cliente
/// arma su español desde el código y porque el acta lo imprime. El bombo no lo mira.
/// </summary>
public static class PrizeDrawVoidCode
{
    /// <summary>El ganador no está en la sala.</summary>
    public const string Absent = "absent";

    /// <summary>Está, y no quiere el premio.</summary>
    public const string Declined = "declined";

    /// <summary>
    /// No podía ganar: personal, un duplicado, algo que la calificación no vio. Como cualquier otro
    /// motivo, lo saca de todos los premios que quedan en el sorteo (spec 02 §3.1).
    /// </summary>
    public const string Ineligible = "ineligible";

    /// <summary>Ninguno de los anteriores; la nota es obligatoria.</summary>
    public const string Other = "other";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Absent, Declined, Ineligible, Other
    };
}
