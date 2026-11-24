using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Errors;

/// <summary>
/// Se intentó cambiar una campaña que ya no admite cambios. Una campaña en <c>sending</c> es de
/// solo lectura —ni destinatarios, ni plantilla, ni idioma— y <c>sent</c> y <c>cancelled</c>
/// igual. Solo <c>draft</c> acepta una edición, que es la guarda entera en la que se apoyan la
/// selección y las cargas de la spec <c>05</c> (spec <c>03</c> §5).
/// <para>
/// Lleva el estado en el que sí está, porque una pantalla que dice "ya se está enviando" es
/// otra cosa que una que dice "se canceló", y quien consume no debería tener que releer la
/// campaña para saber cuál de las dos pintar.
/// </para>
/// </summary>
public sealed record CampaignNotDraftError : BaseError
{
    public const string ErrorCode = "CAMPAIGN_NOT_DRAFT";

    private const string StateExtension = "state";

    /// <summary>El estado en el que está la campaña.</summary>
    public string State { get; init; }

    public CampaignNotDraftError(string state)
        : base(
            $"La campaña ya no admite cambios: está en '{state}'.",
            ErrorCode,
            ErrorKind.Conflict)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        State = state;
    }

    public override IReadOnlyDictionary<string, object?> ProblemExtensions =>
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [StateExtension] = State
        };
}
