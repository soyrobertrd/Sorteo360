using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Errors;

/// <summary>
/// La campaña nombra una plantilla que no existe en su idioma: la pareja
/// <c>(template_key, language)</c> no está sembrada. Lo rechazan crear, editar y lanzar, antes de
/// que la campaña llegue al worker (spec <c>03</c> §4.1).
/// <para>
/// Sin esta guarda el lanzamiento contestaba <c>202</c> y el worker se topaba después con
/// <c>CAMPAIGN_NOT_RENDERABLE</c>, dejando la campaña atascada en <c>sending</c> sin nadie a quien
/// avisar. No hay caída al español (spec <c>02</c> §3): falta la copia, y quien coordina tiene que
/// saber cuál.
/// </para>
/// <para>
/// <see cref="ErrorKind.Invalid"/> y no <see cref="ErrorKind.NotFound"/>: la campaña sí existe, lo
/// que no sirve es la plantilla que nombra, y un <c>404</c> se leería como "esa campaña no está".
/// Es la misma familia que <c>TEMPLATE_EVENT_VALUES_MISSING</c>: "todavía no, y esto es lo que hay
/// que arreglar".
/// </para>
/// </summary>
public sealed record CampaignTemplateNotFoundError : BaseError
{
    public const string ErrorCode = "CAMPAIGN_TEMPLATE_NOT_FOUND";

    /// <summary>Clave de la extensión ProblemDetails que lleva la clave de plantilla.</summary>
    private const string TemplateKeyExtension = "templateKey";

    /// <summary>Clave de la extensión ProblemDetails que lleva el idioma.</summary>
    private const string LanguageExtension = "language";

    /// <summary>La clave de plantilla que se pidió.</summary>
    public string TemplateKey { get; init; }

    /// <summary>El idioma en el que no hay variante de esa plantilla.</summary>
    public string Language { get; init; }

    public CampaignTemplateNotFoundError(string templateKey, string language)
        : base(
            $"No hay plantilla '{templateKey}' en el idioma '{language}'.",
            ErrorCode,
            ErrorKind.Invalid)
    {
        ArgumentNullException.ThrowIfNull(templateKey);
        ArgumentNullException.ThrowIfNull(language);

        TemplateKey = templateKey;
        Language = language;
    }

    public override IReadOnlyDictionary<string, object?> ProblemExtensions =>
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [TemplateKeyExtension] = TemplateKey,
            [LanguageExtension] = Language
        };
}
