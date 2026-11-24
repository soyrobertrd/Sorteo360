namespace Sorteo360.Notifications.Core.Notifications;

/// <summary>
/// Los seis idiomas en que la plataforma manda correo. El servicio no usa el idioma para nada:
/// lo guarda. Elegir la copia de cada idioma es trabajo de quien llama, con su propia tabla.
/// </summary>
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
