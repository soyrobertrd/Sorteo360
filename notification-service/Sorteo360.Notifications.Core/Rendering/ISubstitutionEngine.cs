namespace Sorteo360.Notifications.Core.Rendering;

/// <summary>
/// Motor de sustitución: convierte el asunto y el cuerpo que mandó quien llama, todavía con sus
/// <c>{{placeholders}}</c>, en el mensaje de una persona.
/// <para>
/// No conoce ninguna clave, ningún <c>type</c> y ninguna plantilla: no hay tabla que consultar ni
/// lista de claves legales. Solo sustituye lo que el propio cuerpo nombra.
/// </para>
/// </summary>
public interface ISubstitutionEngine
{
    /// <summary>
    /// Sustituye los marcadores del asunto y del cuerpo con <paramref name="values"/> y deriva la
    /// parte de texto. Los valores son planos: la búsqueda es una sola, sin precedencia, porque
    /// una clave repetida entre los datos comunes y los de una persona ya se rechazó antes.
    /// </summary>
    /// <param name="subject">Asunto con marcadores.</param>
    /// <param name="htmlBody">Cuerpo HTML completo, con marcadores.</param>
    /// <param name="values">Valores por clave.</param>
    RenderedEmail Render(string subject, string htmlBody, IReadOnlyDictionary<string, string> values);
}
