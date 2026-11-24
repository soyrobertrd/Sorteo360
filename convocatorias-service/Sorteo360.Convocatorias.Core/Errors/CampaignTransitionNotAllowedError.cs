using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Errors;

/// <summary>
/// El salto pedido no está en la tabla de transiciones de la spec <c>03</c> §2. Cancelar una
/// campaña ya enviada, relanzar una cancelada, y todo lo demás que la tabla no nombra.
/// <para>
/// Un solo código para todas las transiciones inválidas, y las dos puntas en extensiones: la
/// alternativa es un código por pareja, que multiplica el catálogo sin que ninguna pantalla
/// haga nada distinto con cada uno.
/// </para>
/// </summary>
public sealed record CampaignTransitionNotAllowedError : BaseError
{
    public const string ErrorCode = "CAMPAIGN_TRANSITION_NOT_ALLOWED";

    private const string FromExtension = "state";
    private const string ToExtension = "requestedState";

    /// <summary>El estado actual.</summary>
    public string From { get; init; }

    /// <summary>El estado al que se quiso llevar.</summary>
    public string To { get; init; }

    public CampaignTransitionNotAllowedError(string from, string to)
        : base(
            $"Una campaña en '{from}' no puede pasar a '{to}'.",
            ErrorCode,
            ErrorKind.Conflict)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(from);
        ArgumentException.ThrowIfNullOrWhiteSpace(to);

        From = from;
        To = to;
    }

    public override IReadOnlyDictionary<string, object?> ProblemExtensions =>
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [FromExtension] = From,
            [ToExtension] = To
        };
}
