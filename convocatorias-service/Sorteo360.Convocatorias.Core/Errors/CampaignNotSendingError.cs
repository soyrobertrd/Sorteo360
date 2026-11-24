using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Errors;

/// <summary>
/// Se pidió desatascar una campaña que no está en condiciones de mandar nada (spec <c>03</c> §7).
/// <para>
/// Es el espejo de <see cref="CampaignNotDraftError"/> un estado más adelante: <c>resume-handoff</c>
/// y <c>retry-failed</c> mandan la cola de una lista que alguien ya lanzó, así que desatascan en
/// vez de decidir. Un borrador todavía no salió y una cancelada la paró alguien a conciencia, así
/// que ninguna de las dos tiene cola que mandar.
/// </para>
/// <para>
/// <b>Las dos rutas no admiten lo mismo, y el código sí es el mismo.</b> <c>resume-handoff</c>
/// quiere <c>sending</c>, porque la cola sin entregar solo existe ahí; <c>retry-failed</c> admite
/// además <c>sent</c>, porque una campaña con filas rechazadas ya está cerrada. Un código por cada
/// una no le serviría a nadie: lo que un cliente hace con esto es leer la extensión
/// <c>state</c> y decir en qué estado está.
/// </para>
/// <para>
/// No es una transición inválida y por eso no lleva su código: ninguna de las dos rutas cambia el
/// estado de la campaña, así que nombrar un salto sería nombrar algo que nadie pidió.
/// </para>
/// </summary>
public sealed record CampaignNotSendingError : BaseError
{
    public const string ErrorCode = "CAMPAIGN_NOT_SENDING";

    private const string StateExtension = "state";

    /// <summary>El estado en el que está.</summary>
    public string State { get; init; }

    public CampaignNotSendingError(string state)
        : base(
            $"La campaña está en '{state}': no hay nada que desatascar.",
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
