using System.ComponentModel.DataAnnotations;

namespace Sorteo360.Events.Infrastructure.Notifications;

/// <summary>
/// Dónde vive el frontend que leen los destinatarios de un aviso. Sección <c>App</c>.
///
/// <para>
/// <b>Es una variable distinta de <c>Portal__BaseUrl</c> de convocatorias, a propósito.</b> Aquel
/// es el sitio público de registro y este es la aplicación interna; juntarlos metería al personal
/// del MITUR en el portal público el día que los dos nombres de host se separen.
/// </para>
///
/// <para>
/// El peligro de esta variable no es que falte sino que su valor termine igual en los dos compose.
/// El porqué se explica una sola vez, junto a las dos líneas que hay que mantener distintas: ver
/// <c>events-service/CLAUDE.md</c>, §"A quote's alerts go out through IQuoteNotifier".
/// </para>
/// </summary>
public sealed class AppSettings
{
    public static string SectionName => "App";

    /// <summary>
    /// Base absoluta del frontend. Los enlaces los arma
    /// <see cref="Core.Templates.EventLink"/>, que tolera la barra final.
    /// </summary>
    [Required]
    [Url]
    public string BaseUrl { get; init; } = string.Empty;
}
