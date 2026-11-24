namespace Sorteo360.Convocatorias.Core.Events;

/// <summary>
/// Los cuatro estados de un evento, tal como los declara events-service. Se repiten acá porque
/// el portal ramifica sobre ellos —un evento cancelado es 404 y uno cerrado sigue sirviendo— y
/// comparar contra cadenas sueltas esparcidas por los endpoints es como una de ellas termina
/// escrita distinto.
/// <para>
/// Es una copia de un conjunto ajeno, no una autoridad: events-service es quien lo decide, y si
/// algún día crece un quinto estado, acá hay que venir a ponerlo.
/// </para>
/// </summary>
public static class EventStatus
{
    public const string Planning   = "planning";
    public const string InProgress = "in_progress";
    public const string Closed     = "closed";
    public const string Cancelled  = "cancelled";

    /// <summary>El evento todavía no terminó. Es lo único que sirve el listado público.</summary>
    public static readonly IReadOnlySet<string> Active = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Planning, InProgress
    };
}
