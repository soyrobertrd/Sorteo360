using Sorteo360.Events.Core.Events;

namespace Sorteo360.Events.Core.Templates;

/// <summary>
/// Un correo entero —asunto y cuerpo— en un idioma. La clave es <c>(TemplateKey, Language)</c>.
///
/// <para>
/// <b>Fase 1: nadie edita ninguna.</b> El conjunto lo siembra una migración de datos, y arreglar
/// una palabra cuesta una migración más un despliegue de events-service. Es deliberado: mientras no
/// haya endpoint de edición, la copia sigue siendo un diff que alguien revisa en un PR, y el test
/// de archivo dorado es lo que lo mantiene así.
/// </para>
///
/// <para>
/// <b>Cuatro columnas y ninguna más.</b> No lleva <c>purpose</c> ni <c>is_campaign_selectable</c>
/// como la tabla gemela de convocatorias: allá existen porque una persona <i>elige</i> una
/// plantilla para su campaña. Acá ninguna se elige —cada momento del flujo tiene la suya y la
/// busca por clave—, así que una columna para clasificar no separaría nada.
/// </para>
///
/// <para>
/// Los marcadores <c>{{clave}}</c> no se sustituyen de este lado: viajan intactos hasta
/// notification-service junto con sus valores, que es quien los rellena (spec <c>02</c> §5). Acá
/// solo se busca la fila.
/// </para>
/// </summary>
public sealed class NotificationTemplate
{
    public string TemplateKey { get; }

    public string Language { get; }

    public string Subject { get; }

    /// <summary>El correo completo, con sus marcadores sin sustituir.</summary>
    public string BodyHtml { get; }

    public NotificationTemplate(string templateKey, string language, string subject, string bodyHtml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(bodyHtml);

        if (!AllowedLanguage.All.Contains(language))
            throw new ArgumentException($"Idioma no admitido: '{language}'.", nameof(language));

        TemplateKey = templateKey;
        Language = language;
        Subject = subject;
        BodyHtml = bodyHtml;
    }
}
