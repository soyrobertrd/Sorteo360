namespace Sorteo360.Events.Core.Events;

public static class EventType
{
    public const string Corporate  = "Corporate";
    public const string Christmas  = "Christmas";
    public const string Wedding    = "Wedding";
    public const string Birthday   = "Birthday";
    public const string Concert    = "Concert";
    public const string Graduation = "Graduation";
    public const string Conference = "Conference";
    public const string Other      = "Other";

    // Alias legados conservados para compatibilidad binaria/fuente con pruebas existentes;
    // todos mapean a los nuevos tipos genéricos de Sorteo360.
    public const string Roadshow  = Corporate;
    public const string Feria     = Corporate;
    public const string Tradeshow = Corporate;
    public const string Webinar   = Conference;

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Corporate, Christmas, Wedding, Birthday, Concert, Graduation, Conference, Other
    };
}
