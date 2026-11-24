using System.Text.RegularExpressions;

namespace Sorteo360.Notifications.Core.Rendering;

/// <summary>
/// Encuentra los <c>{{key}}</c> que menciona un texto. Es la mitad de lectura del motor de
/// sustitución: con la lista de claves se comprueba, antes del 202, que quien llama mandó un
/// valor para cada una.
/// <para>
/// La sintaxis es plana y literal a propósito: no hay condicionales, ni bucles, ni helpers, ni
/// un escape <c>{{{crudo}}}</c>. Este servicio no conoce ninguna clave: solo pregunta si la que
/// el cuerpo nombra tiene valor.
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
    /// <paramref name="transform"/>. Una clave sin valor se deja tal cual en vez de romper: la
    /// petición con marcadores sin resolver ya se rechazó con un 400, así que acá no hay nada
    /// que decidir.
    /// </summary>
    /// <param name="text">Texto con marcadores.</param>
    /// <param name="values">Valores por clave, planos y sin cascada.</param>
    /// <param name="transform">
    /// Se aplica a cada valor sustituido (el escape HTML en el cuerpo; la identidad en el asunto).
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
