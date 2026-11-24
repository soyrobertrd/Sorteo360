using System.Globalization;
using System.Text.RegularExpressions;

namespace Sorteo360.Convocatorias.Core.Shared;

/// <summary>Si un sitio web es uno que el CRM acepta en el alta de una empresa.</summary>
/// <remarks>
/// Es copia de <c>WebDomain.IsPlausible</c> de crm-service, que es el dueño de la regla; si allá
/// cambia, cambia aquí. Hace falta de este lado porque un 400 del CRM se lee como "el CRM no está":
/// la carga de proveedores le contestaría a quien envía que lo intente más tarde, y el envío fallaría
/// igual en cada intento. Lo escrito se reduce a su host (sin esquema, <c>www.</c>, ruta ni puerto,
/// en minúsculas y en punycode) y ese host tiene que parecer un nombre de dominio.
/// </remarks>
public static partial class WebsiteShape
{
    private static readonly IdnMapping AsciiMapping = new();

    /// <summary>Etiquetas alfanuméricas con al menos un punto y un TLD alfabético o punycode, el patrón del CRM.</summary>
    [GeneratedRegex(@"^(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+(?:[a-z]{2,}|xn--[a-z0-9-]{2,})$")]
    private static partial Regex HostPattern();

    /// <summary>En blanco, ausente, o un sitio web cuyo host el CRM acepta.</summary>
    public static bool IsAcceptedByCrm(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        var host = HostOf(raw);

        return host is { Length: > 0 and <= 253 } && HostPattern().IsMatch(host);
    }

    /// <summary>Los pasos de <c>WebDomain.Normalize</c> del CRM, en el mismo orden.</summary>
    private static string HostOf(string raw)
    {
        var host = raw.Trim();

        if (host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            host = host["https://".Length..];
        else if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            host = host["http://".Length..];

        if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            host = host["www.".Length..];

        var cut = host.IndexOfAny(['/', '?', '#']);
        if (cut >= 0)
            host = host[..cut];

        var port = host.IndexOf(':', StringComparison.Ordinal);
        if (port >= 0)
            host = host[..port];

        host = host.TrimEnd('.').ToLowerInvariant();

        if (host.Length == 0)
            return host;

        try
        {
            return AsciiMapping.GetAscii(host);
        }
        catch (ArgumentException)
        {
            // No es un nombre IDN válido: se devuelve tal cual y el patrón lo rechaza.
            return host;
        }
    }
}
