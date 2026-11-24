using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Rendering;
using Sorteo360.Convocatorias.Core.Templates;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Campaigns;

/// <summary>
/// La campaña sin evento: un mensaje directo a uno o varios contactos, sin enlace de registro y sin
/// nada a lo que inscribirse (ADR-0026, PGTI-174). Pasa por las mismas comprobaciones de
/// consentimiento y supresión que una campaña de evento, porque es una campaña.
/// <para>
/// <b>La regla es una sola y vive acá</b>: una campaña no tiene evento si y solo si sale con
/// <see cref="TemplateKey.DirectMessage"/>, y entonces lleva su asunto y su texto; una de evento no
/// lleva ninguno. Sin evento no hay nombre, lugar ni fecha con que rellenar una plantilla, así que
/// el texto lo pone quien la arma. La base la vuelve a cobrar con una restricción.
/// </para>
/// </summary>
public static class CampaignDirectMessage
{
    /// <summary>El tope del asunto, que es una cabecera y se lee en una bandeja de entrada.</summary>
    public const int SubjectMaxLength = 200;

    /// <summary>El tope del texto.</summary>
    public const int MessageMaxLength = 10_000;

    /// <summary>Si esa plantilla es la del mensaje directo.</summary>
    public static bool IsDirectMessage(string templateKey) =>
        string.Equals(templateKey, TemplateKey.DirectMessage, StringComparison.Ordinal);

    /// <summary>
    /// Lo que la campaña incumple de la regla, o <c>null</c> si la cumple. Se pregunta con los
    /// valores ya resueltos, lo que viene en la petición más lo que ya tiene la fila, porque un
    /// <c>PATCH</c> que solo cambia la plantilla puede romperla tanto como un alta.
    /// </summary>
    public static BaseError? Check(Guid? eventId, string templateKey, string? subject, string? message)
    {
        ArgumentNullException.ThrowIfNull(templateKey);

        var direct = IsDirectMessage(templateKey);

        if (eventId is null && !direct)
            return new CampaignDirectMessageMismatchError($"sin evento solo se admite la plantilla '{TemplateKey.DirectMessage}'.");

        if (eventId is not null && direct)
            return new CampaignDirectMessageMismatchError($"la plantilla '{TemplateKey.DirectMessage}' no admite evento.");

        if (direct && (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(message)))
            return new CampaignDirectMessageMismatchError("un mensaje directo lleva asunto y texto.");

        if (!direct && (subject is not null || message is not null))
            return new CampaignDirectMessageMismatchError("una campaña de evento no lleva asunto ni texto propios.");

        return null;
    }

    /// <summary>
    /// El asunto y el texto como bolsa compartida del lote (spec <c>04</c> §2), en el lugar que en
    /// una campaña de evento ocupan sus tres valores. Sustituirlos es trabajo del lado que recibe.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ToSharedData(string subject, string message) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SupportedPlaceholder.DirectSubject] = subject,
            [SupportedPlaceholder.DirectMessage] = message,
        };
}
