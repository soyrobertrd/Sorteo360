using Sorteo360.Convocatorias.Core.Shared;

namespace Sorteo360.Convocatorias.Core.Templates;

/// <summary>
/// Un correo entero —asunto y cuerpo— en un idioma, con el arte dentro del cuerpo. La clave es
/// <c>(TemplateKey, Language)</c>.
/// <para>
/// Fase 1: la persona elige una, nadie edita ninguna, y el conjunto lo siembra una migración de
/// datos. Que agregar una plantilla cueste una migración más un despliegue es deliberado: un
/// editor autoservicio es el mecanismo que produce cincuenta plantillas.
/// </para>
/// <para>
/// Las reglas de marcadores (<see cref="Rendering.TemplateBodyRules"/>) no se comprueban acá a
/// propósito. Son reglas sobre la copia sembrada, las cobra un test, y meterlas en el
/// constructor dejaría sin forma de escribir ese test.
/// </para>
/// </summary>
public sealed class Template
{
    public string TemplateKey { get; }

    public string Language { get; }

    public string Subject { get; }

    /// <summary>El correo completo, arte incluido, como un <c>&lt;img src&gt;</c> <c>https://</c>.</summary>
    public string BodyHtml { get; }

    /// <summary>Etiqueta, no entidad: <c>campaign</c> o <c>transactional</c>.</summary>
    public string Purpose { get; }

    /// <summary>Si aparece en el selector de una campaña.</summary>
    public bool IsCampaignSelectable { get; }

    public Template(
        string templateKey,
        string language,
        string subject,
        string bodyHtml,
        string purpose,
        bool isCampaignSelectable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(bodyHtml);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        if (!AllowedLanguage.All.Contains(language))
            throw new ArgumentException($"Idioma no admitido: '{language}'.", nameof(language));

        if (!TemplatePurpose.All.Contains(purpose))
            throw new ArgumentException($"Propósito no admitido: '{purpose}'.", nameof(purpose));

        // Una transaccional no se puede elegir en una campaña: su lista se deriva de filas que ya
        // tenemos, nadie la selecciona. La combinación contraria sí existe (una campaña que se
        // retira del selector sin borrarla).
        if (isCampaignSelectable && purpose != TemplatePurpose.Campaign)
            throw new ArgumentException("Solo una plantilla de campaña puede ser seleccionable.", nameof(isCampaignSelectable));

        TemplateKey = templateKey;
        Language = language;
        Subject = subject;
        BodyHtml = bodyHtml;
        Purpose = purpose;
        IsCampaignSelectable = isCampaignSelectable;
    }
}
