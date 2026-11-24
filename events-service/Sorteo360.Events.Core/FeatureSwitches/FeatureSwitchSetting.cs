using System.Text.RegularExpressions;

namespace Sorteo360.Events.Core.FeatureSwitches;

/// <summary>
/// Estado persistido de un interruptor de módulo o función (<c>events.feature_switch</c>).
/// Las claves ausentes en la tabla se interpretan como habilitadas por omisión.
/// </summary>
public sealed record FeatureSwitchSetting(
    string Key,
    bool Enabled,
    Guid? ChangedById,
    string? ChangedByName,
    DateTimeOffset? ChangedAt)
{
    public const int KeyMaxLength = 120;

    private static readonly Regex ValidKeyPattern = new(
        @"^[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*)?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Comprueba que la clave siga la forma <c>modulo</c> o <c>modulo.funcion</c>.</summary>
    public static bool IsValidKey(string? key) =>
        !string.IsNullOrWhiteSpace(key)
        && key.Length <= KeyMaxLength
        && ValidKeyPattern.IsMatch(key);
}

