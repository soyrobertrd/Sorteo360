using System.Security.Cryptography;

namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// El código con que el ganador retira su premio: <c>RD-XXXX-XXXX</c>, ocho caracteres del alfabeto
/// Crockford de base 32, sin I, L, O ni U para que nada se lea mal en la pantalla de un teléfono
/// (spec 03 §2.2). Sale de <see cref="RandomNumberGenerator"/>, sin reloj: la mitad adivinable del
/// código del mock era justamente la hora.
/// </summary>
public static class RedemptionCode
{
    /// <summary>Lo que la base de datos exige a cada código, con la misma expresión.</summary>
    public const string Pattern = "^RD-[0-9A-HJKMNP-TV-Z]{4}-[0-9A-HJKMNP-TV-Z]{4}$";

    private const string Crockford = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>
    /// El código canónico que alguien quiso escribir, o <c>null</c> si lo escrito no es un código
    /// (spec 03 §2.4). Ignora mayúsculas, guiones y espacios; lee la O como 0 y la I y la L como 1,
    /// que es para lo que el alfabeto las dejó afuera; y el <c>RD</c> es optativo. No busca nada: lo
    /// que devuelve se compara exacto contra el índice único.
    /// </summary>
    public static string? TryNormalize(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var compact = string.Concat(input.Where(c => c is not ('-' or ' ')))
            .ToUpperInvariant();

        if (compact.Length == 10 && compact.StartsWith("RD", StringComparison.Ordinal))
            compact = compact[2..];

        if (compact.Length != 8)
            return null;

        var body = string.Concat(compact.Select(ReadAlias));
        if (!body.All(Crockford.Contains))
            return null;

        return $"RD-{body[..4]}-{body[4..]}";
    }

    private static char ReadAlias(char c) => c switch
    {
        'O' => '0',
        'I' or 'L' => '1',
        _ => c,
    };

    /// <summary>Un código nuevo. Que no esté usado lo comprueba quien lo guarda.</summary>
    public static string Generate()
    {
        var body = RandomNumberGenerator.GetString(Crockford, 8);
        return $"RD-{body[..4]}-{body[4..]}";
    }
}
