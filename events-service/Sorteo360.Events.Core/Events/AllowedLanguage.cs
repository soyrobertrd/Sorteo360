namespace Sorteo360.Events.Core.Events;

public static class AllowedLanguage
{
    public const string Espanol   = "es";
    public const string Ingles    = "en";
    public const string Frances   = "fr";
    public const string Aleman    = "de";
    public const string Portugues = "pt";
    public const string Italiano  = "it";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Espanol, Ingles, Frances, Aleman, Portugues, Italiano
    };
}
