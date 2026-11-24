using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Errors;

/// <summary>
/// Se quiso lanzar una campaña de un evento con la ventana de inscripción cerrada (spec <c>04</c>
/// §1 paso 5, spec <c>06</c> §5.5). Nadie quiere mandarle a varios cientos de personas un enlace
/// que contesta <c>409</c>; quien quiera invitar a más reabre primero.
/// <para>
/// <b>Código propio, distinto de <see cref="CampaignEventCancelledError"/></b>: lo cancelado no
/// tiene vuelta y esto se deshace con un clic, así que la pantalla tiene que poder decir cuál de
/// las dos cosas pasó. Deja la campaña en <c>draft</c> sin escribir nada.
/// </para>
/// </summary>
public sealed record CampaignRegistrationsClosedError : BaseError
{
    public const string ErrorCode = "CAMPAIGN_REGISTRATIONS_CLOSED";

    /// <summary>Clave de la extensión ProblemDetails que lleva el evento.</summary>
    private const string EventIdExtension = "eventId";

    /// <summary>El evento con la ventana cerrada.</summary>
    public Guid EventId { get; init; }

    public CampaignRegistrationsClosedError(Guid eventId)
        : base(
            "Las inscripciones de este evento están cerradas: reábrelas antes de lanzar la campaña.",
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
