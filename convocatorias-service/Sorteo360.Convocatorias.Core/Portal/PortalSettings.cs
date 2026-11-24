using System.ComponentModel.DataAnnotations;

namespace Sorteo360.Convocatorias.Core.Portal;

/// <summary>
/// Desde dónde se arman los enlaces que van dentro de la invitación. Sección <c>Portal</c>.
/// <para>
/// <b>Es el origen que abre el invitado en su navegador</b>, no un nombre de la red de Compose:
/// el enlace lo abre una persona desde fuera, así que <c>http://convocatorias-service:8080</c> no
/// le sirve a nadie.
/// </para>
/// <para>
/// Se valida al arrancar porque un enlace mal armado no falla por su cuenta: el correo sale
/// igual y el síntoma es una persona haciendo clic en un enlace muerto, días después y sin
/// ninguna traza. Es también variable distinta de <c>App__BaseUrl</c> de events-service, que es
/// la aplicación interna: juntarlas metería al personal del MITUR en el portal público el día que
/// los dos nombres de host se separen.
/// </para>
/// </summary>
public sealed record PortalSettings
{
    public static string SectionName => "Portal";

    /// <summary>Base absoluta del portal público. <see cref="PortalLink"/> tolera la barra final.</summary>
    [Required]
    [Url]
    public string BaseUrl { get; init; } = string.Empty;
}
