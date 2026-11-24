using System.Text.RegularExpressions;

namespace Sorteo360.Convocatorias.Core.Rendering;

/// <summary>
/// La mitad de lectura del motor de sustitución: encuentra los <c>{{key}}</c> que nombra un
/// texto. Con esa lista se comprueban las reglas de <see cref="TemplateBodyRules"/> sobre la
/// copia sembrada, antes de que nadie mande nada.
/// <para>
/// La sintaxis es plana y literal a propósito: no hay condicionales, ni bucles, ni helpers, ni
/// un escape <c>{{{crudo}}}</c>. Es contrato y no detalle de implementación, porque el cuerpo
/// cruza una frontera de servicio y lo sustituye notification-service (spec <c>04</c> §2). Por
/// eso el dialecto de acá y el de allá son el mismo a propósito, y esta clase es la copia de
/// este lado de la frontera.
/// </para>
/// </summary>
public static class PlaceholderScanner
{
    /// <summary>
    /// Un marcador es exactamente dos llaves, un nombre sin llaves dentro, y dos llaves de
    /// cierre. Al no admitir llaves en el nombre, <c>{{{x}}}</c> casa solo por dentro y las
    /// llaves sobrantes quedan como texto: no existe la variante cruda.
    /// </summary>
    private static readonly Regex Marker = new(
        @"\{\{([^{}]*)\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Claves distintas que aparecen en los textos, en el orden en que se encuentran. Un
    /// <c>{{</c> sin cierre no es un marcador y no aparece; tampoco <c>{{ }}</c>, que no nombra
    /// nada.
    /// </summary>
    public static IReadOnlyList<string> Scan(params string?[] texts)
    {
        ArgumentNullException.ThrowIfNull(texts);

        var keys = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var text in texts)
        {
            if (string.IsNullOrEmpty(text))
                continue;

            foreach (var match in Marker.Matches(text).Cast<Match>())
            {
                var key = match.Groups[1].Value.Trim();

                if (key.Length > 0 && seen.Add(key))
                    keys.Add(key);
            }
        }

        return keys;
    }

    /// <summary>
    /// Reemplaza cada marcador por el valor que le corresponda, pasándolo antes por
    /// <paramref name="transform"/>. Una clave sin valor se deja tal cual en vez de romper: lo
    /// que no se reconoce se entrega intacto, y las reglas que deciden si eso es aceptable
    /// corren antes, en <see cref="TemplateBodyRules"/>.
    /// </summary>
    /// <param name="text">Texto con marcadores.</param>
    /// <param name="values">Valores por clave, planos y sin cascada.</param>
    /// <param name="transform">
    /// Se aplica a cada valor sustituido (el escape HTML en el cuerpo; quitar saltos de línea
    /// en el asunto).
    /// </param>
    internal static string Substitute(
        string text,
        IReadOnlyDictionary<string, string> values,
        Func<string, string> transform)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(transform);

        return Marker.Replace(text, match =>
        {
            var key = match.Groups[1].Value.Trim();

            if (key.Length == 0)
                return match.Value;

            return values.TryGetValue(key, out var value)
                ? transform(value)
                : match.Value;
        });
    }
}
