namespace Sorteo360.Convocatorias.Core.Rendering;

/// <summary>
/// Motor de sustitución: rellena el asunto y el cuerpo de una plantilla con los valores de un
/// evento y de una persona.
/// <para>
/// Sustitución plana y literal de <c>{{clave}}</c>, sin condicionales, bucles, helpers ni
/// escape crudo. Es contrato: el transporte es relay SMTP y no hay plantilla del lado del
/// proveedor, así que el cuerpo se renderiza siempre de este lado de la plataforma y el
/// dialecto es nuestro (spec <c>02</c> §5).
/// </para>
/// <para>
/// El costo aceptado es que no existe un <c>{{#if empresa}}</c>: un valor opcional que falta se
/// rellena con su caída neutra en vez de tirar la frase que lo rodea.
/// </para>
/// </summary>
public interface ISubstitutionEngine
{
    /// <summary>
    /// Sustituye los marcadores del asunto y del cuerpo con <paramref name="values"/>.
    /// <list type="bullet">
    /// <item>Los valores del cuerpo se escapan como HTML: llegan de una hoja de cálculo que
    /// sube una persona, así que un <c>Juan &lt;script&gt;</c> es un camino vivo hasta el correo.</item>
    /// <item>Un <c>{{...}}</c> que no se reconoce se deja tal cual, nunca se lanza.</item>
    /// <item>Un <c>{{nombre}}</c> sin valor se rellena con el saludo neutro
    /// (<see cref="SupportedPlaceholder.NeutralRecipientName"/>).</item>
    /// </list>
    /// </summary>
    /// <param name="subject">Asunto con marcadores.</param>
    /// <param name="bodyHtml">Cuerpo HTML completo, con marcadores.</param>
    /// <param name="values">Valores por clave, planos y sin precedencia.</param>
    RenderedTemplate Render(string subject, string bodyHtml, IReadOnlyDictionary<string, string> values);
}
