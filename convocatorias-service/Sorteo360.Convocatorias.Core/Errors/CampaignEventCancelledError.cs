using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Errors;

/// <summary>
/// Se quiso lanzar una campaña de un evento que ya se canceló. <b>Es la única guarda que hay</b>:
/// events-service nunca llama a convocatorias, así que nada más dentro de este servicio puede
/// enterarse de que el evento se apagó, y por eso la lectura del evento es un paso del orden del
/// lanzamiento y no un adorno (spec <c>04</c> §1 paso 4, spec <c>06</c> §4.6).
/// <para>
/// Escribirle a varios cientos de personas por un evento cancelado hace una hora es exactamente
/// el fallo que esto existe para impedir. Quien registró su asistencia ya recibió el aviso de
/// cancelación por su lado (spec <c>06</c> §4.7); invitar a lo mismo después sería el mensaje
/// contrario.
/// </para>
/// <para>
/// <see cref="ErrorKind.Conflict"/> y no <see cref="ErrorKind.Invalid"/>: la petición está bien
/// formada y lo que la rechaza es el estado de otro recurso. Deja la campaña en <c>draft</c>, sin
/// <c>launched_by</c> y sin <c>launched_at</c>, y no escribe ninguna fila de destinatario.
/// </para>
/// </summary>
public sealed record CampaignEventCancelledError : BaseError
{
    public const string ErrorCode = "CAMPAIGN_EVENT_CANCELLED";

    /// <summary>Clave de la extensión ProblemDetails que lleva el evento cancelado.</summary>
    private const string EventIdExtension = "eventId";

    /// <summary>El evento que está cancelado.</summary>
    public Guid EventId { get; init; }

    public CampaignEventCancelledError(Guid eventId)
        : base(
            "El evento de esta campaña está cancelado: no se puede lanzar.",
            ErrorCode,
            ErrorKind.Conflict)
    {
        EventId = eventId;
    }

    public override IReadOnlyDictionary<string, object?> ProblemExtensions =>
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [EventIdExtension] = EventId
        };
}
