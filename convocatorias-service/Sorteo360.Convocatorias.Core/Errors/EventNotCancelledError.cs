using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Errors;

/// <summary>
/// Se pidió el aviso de cancelación de un evento que no está cancelado (spec <c>06</c> §4.7).
/// <b>Es la guarda que impide que quien llama mal le escriba a 190 personas por un evento que
/// sigue en pie</b>: el aviso lo dispara el frontend como segunda llamada, y una segunda llamada
/// puede llegar sola.
/// <para>
/// Un evento cerrado también se niega acá, a propósito: cerrar significa que el evento ocurrió, y
/// avisar entonces le diría a quien asistió que se canceló. Un id que events-service no conoce se
/// niega igual: tampoco es un evento cancelado.
/// </para>
/// <para>
/// <see cref="ErrorKind.Conflict"/>: la petición está bien formada y lo que la rechaza es el estado
/// de otro recurso. No escribe nada y no le manda nada a nadie.
/// </para>
/// </summary>
public sealed record EventNotCancelledError : BaseError
{
    public const string ErrorCode = "EVENT_NOT_CANCELLED";

    /// <summary>Clave de la extensión ProblemDetails que lleva el evento.</summary>
    private const string EventIdExtension = "eventId";

    /// <summary>El evento que no está cancelado.</summary>
    public Guid EventId { get; init; }

    public EventNotCancelledError(Guid eventId)
        : base(
            "El evento no está cancelado: no se avisa a nadie.",
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
