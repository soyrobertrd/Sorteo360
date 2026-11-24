namespace Sorteo360.Convocatorias.Core.Rendering;

/// <summary>
/// Una plantilla ya sustituida: el asunto y el cuerpo, listos para mandarse.
/// <para>
/// No hay alternativa de texto plano acá. La deriva notification-service del HTML que le
/// llega, así que nadie escribe la copia dos veces (spec <c>02</c> §1).
/// </para>
/// </summary>
/// <param name="Subject">Asunto sustituido, sin escape HTML: es una cabecera, no marcado.</param>
/// <param name="BodyHtml">Cuerpo sustituido, con cada valor escapado.</param>
public sealed record RenderedTemplate(string Subject, string BodyHtml);
