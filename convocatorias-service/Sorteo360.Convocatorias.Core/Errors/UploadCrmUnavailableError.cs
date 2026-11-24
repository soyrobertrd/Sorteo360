using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Errors;

/// <summary>
/// El CRM dejó de contestar en mitad de una confirmación. <b>Mismo código que
/// <see cref="CrmUnavailableError"/></b>, porque para quien consume es el mismo hecho —el CRM no
/// está— y ramificar por código no debería obligarle a conocer desde qué ruta se llegó.
/// <para>
/// Lo que añade es <see cref="Completed"/>, las filas que sí se escribieron antes del corte.
/// La confirmación es reanudable y el lote es su propio cursor, así que <b>reintentar la misma
/// llamada</b> salta todo lo ya hecho y sigue por donde iba. Sin este número quien reintenta no
/// sabe si va a crear a nadie dos veces (spec <c>05</c> §6).
/// </para>
/// <para>
/// <b>Un circuito abierto llega acá igual.</b> El chasis traduce <c>BrokenCircuitException</c> a
/// <c>HttpRequestException</c>, así que el fallo que más esperamos —el cortacircuitos abriéndose
/// sobre la fila diez— sale como este 503 y no como un 500.
/// </para>
/// </summary>
public sealed record UploadCrmUnavailableError : BaseError
{
    private const string CompletedExtension = "completed";

    /// <summary>Filas de la carga que quedaron aplicadas antes del corte.</summary>
    public int Completed { get; init; }

    public UploadCrmUnavailableError(int completed)
        : base(
            $"El CRM no está disponible: se completaron {completed} filas y la carga quedó a medias. Vuelva a confirmar para continuar desde donde se quedó.",
            CrmUnavailableError.ErrorCode,
            ErrorKind.Unavailable)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(completed);
        Completed = completed;
    }

    public override IReadOnlyDictionary<string, object?> ProblemExtensions =>
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [CompletedExtension] = Completed
        };
}
