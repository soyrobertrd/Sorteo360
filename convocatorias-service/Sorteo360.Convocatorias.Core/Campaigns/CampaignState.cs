namespace Sorteo360.Convocatorias.Core.Campaigns;

/// <summary>
/// Los cuatro estados de una campaña. <c>draft</c> es donde se arma, <c>sending</c> donde se
/// está entregando, y los dos terminales dicen cómo terminó.
/// <para>
/// <b>No hay estado entre <c>draft</c> y <c>sending</c>.</b> <c>pending_approval</c> ya no
/// existe, y con él se fueron los actos de someter y retirar. Una campaña o se está armando o
/// va de salida: no queda ningún estado de reposo en el que espere a una persona (spec
/// <c>03</c> §2, ADR-0017).
/// </para>
/// <para>
/// Es la misma forma que <c>QuoteStatus</c> en events-service, de donde sale el patrón: un
/// catálogo cerrado de textos, una canonicalización que perdona las mayúsculas, y la tabla de
/// transiciones al lado del catálogo en vez de repartida por los servicios que la consultan.
/// </para>
/// </summary>
public static class CampaignState
{
    /// <summary>Se está armando: admite cambios de plantilla, de idioma y de destinatarios.</summary>
    public const string Draft = "draft";

    /// <summary>Se está entregando. Es de solo lectura: ni la copia ni la lista cambian ya.</summary>
    public const string Sending = "sending";

    /// <summary>Terminal. Ninguna fila de destinatario quedó en <c>pending</c>.</summary>
    public const string Sent = "sent";

    /// <summary>Terminal. Se abandonó antes de salir, o se detuvo en pleno envío.</summary>
    public const string Cancelled = "cancelled";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Draft, Sending, Sent, Cancelled
    };

    /// <summary>
    /// La tabla de la spec <c>03</c> §2 y nada más. Lo que no está acá se rechaza: es el único
    /// sitio donde vive "qué puede pasar después de qué", así que agregar una transición es
    /// editar esta tabla y no buscar guardas repartidas por el servicio.
    /// </summary>
    private static readonly Dictionary<string, IReadOnlySet<string>> AllowedTransitions =
        new(StringComparer.Ordinal)
        {
            [Draft] = new HashSet<string>(StringComparer.Ordinal) { Sending, Cancelled },

            // Cancelar en pleno envío sí se permite. Negarlo dejaría como única salida de una
            // campaña que no debía salir terminar de mandarla a todo el mundo. Deja a unos
            // mandados y a otros no, que es feo y cierto, y las filas dicen exactamente quién
            // está en cada grupo.
            [Sending] = new HashSet<string>(StringComparer.Ordinal) { Sent, Cancelled },

            [Sent] = new HashSet<string>(StringComparer.Ordinal),
            [Cancelled] = new HashSet<string>(StringComparer.Ordinal),
        };

    /// <summary>Un estado del que ya no se sale.</summary>
    public static bool IsTerminal(string state) => state is Sent or Cancelled;

    /// <summary>Si la tabla de §2 admite ese salto. Todo lo demás se rechaza con código.</summary>
    public static bool CanTransition(string from, string to) =>
        AllowedTransitions.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>
    /// Devuelve el estado con la grafía del catálogo (la que exigen las consultas y el CHECK de
    /// la base), o <c>null</c> si el valor no pertenece al catálogo.
    /// </summary>
    public static string? Canonicalize(string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
            return null;

        return All.FirstOrDefault(s => s.Equals(state, StringComparison.OrdinalIgnoreCase));
    }
}
