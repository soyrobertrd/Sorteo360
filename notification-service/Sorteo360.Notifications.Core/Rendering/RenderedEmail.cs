namespace Sorteo360.Notifications.Core.Rendering;

/// <summary>
/// Lo que sale del motor de sustitución para una persona: el asunto y las dos partes del
/// cuerpo. La parte de texto se deriva del HTML ya sustituido, así que nadie escribe la copia
/// dos veces y un arreglo de redacción no puede olvidar la mitad.
/// </summary>
/// <param name="Subject">Asunto sustituido, sin escape HTML: es una cabecera, no marcado.</param>
/// <param name="HtmlBody">Cuerpo sustituido, con cada valor escapado.</param>
/// <param name="TextBody">Alternativa de texto derivada del <paramref name="HtmlBody"/>.</param>
public sealed record RenderedEmail(string Subject, string HtmlBody, string TextBody);
