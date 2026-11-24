namespace Sorteo360.Convocatorias.Core.Shared;

/// <summary>
/// Los seis idiomas en que puede correr un evento, y por tanto los seis en que puede existir
/// una plantilla. El conjunto es el mismo que declara events-service, porque el idioma de una
/// convocatoria lo dicta el evento y no la campaña.
/// <para>
/// No hay caída al español: si falta la variante del idioma del evento, el lanzamiento se
/// bloquea nombrando la plantilla que falta. Un correo en español a la prensa de Madrid es peor
/// que un lanzamiento rechazado.
/// </para>
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
