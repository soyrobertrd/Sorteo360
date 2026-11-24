using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Templates;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Rendering;

/// <summary>
/// Lo que tiene que cumplir el texto de una plantilla. Las tres reglas se comprueban sobre lo
/// que el cuerpo <b>dice</b>, nunca sobre una lista que alguien escribió aparte: los marcadores
/// se derivan, no se declaran (spec <c>02</c> §2).
/// <para>
/// Las dos primeras corren sobre la copia sembrada, en un test, y rompen la compilación antes de
/// que un <c>{{evneto}}</c> llegue a mil bandejas de entrada. La tercera corre en caliente,
/// dentro del lanzamiento, antes del <c>202</c>: después ya no queda a quién avisar.
/// </para>
/// </summary>
public static class TemplateBodyRules
{
    /// <summary>
    /// Marcadores que el texto nombra y el conjunto soportado no conoce, en el orden en que
    /// aparecen. Vacío es cumplir.
    /// </summary>
    /// <param name="subject">Asunto de la plantilla.</param>
    /// <param name="bodyHtml">Cuerpo de la plantilla.</param>
    public static IReadOnlyList<string> UnsupportedPlaceholders(string subject, string bodyHtml)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(bodyHtml);

        return [.. PlaceholderScanner.Scan(subject, bodyHtml)
            .Where(key => !SupportedPlaceholder.IsSupported(key))];
    }

    /// <summary>
    /// Cómo incumple el cuerpo la regla del enlace de baja para su propósito. La regla se
    /// invierte: obligatorio en una campaña, prohibido en una transaccional.
    /// </summary>
    /// <param name="purpose">Propósito de la plantilla (<see cref="TemplatePurpose"/>).</param>
    /// <param name="bodyHtml">Cuerpo de la plantilla; el enlace vive en el cuerpo, no en el asunto.</param>
    public static UnsubscribeRuleViolation CheckUnsubscribeRule(string purpose, string bodyHtml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        ArgumentNullException.ThrowIfNull(bodyHtml);

        var carriesLink = PlaceholderScanner.Scan(bodyHtml)
            .Contains(SupportedPlaceholder.UnsubscribeLink, StringComparer.Ordinal);

        return (purpose, carriesLink) switch
        {
            (TemplatePurpose.Campaign, false) => UnsubscribeRuleViolation.MissingOnCampaign,
            (TemplatePurpose.Transactional, true) => UnsubscribeRuleViolation.PresentOnTransactional,
            _ => UnsubscribeRuleViolation.None
        };
    }

    /// <summary>
    /// Si el evento tiene valor para cada marcador de grano de evento que el texto nombra. El
    /// fallo nombra los campos que faltan, para que nadie mande "se celebrará en ." a mil
    /// personas.
    /// <para>
    /// Es la mitad de evento de la regla de grano. Los de grano de persona no salen por acá:
    /// <c>{{nombre}}</c> tiene su caída neutra y los otros tres los produce este servicio para
    /// cada destinatario.
    /// </para>
    /// <para>
    /// Lo pregunta el lanzamiento, <b>antes</b> del <c>202</c>: después ya no queda a quién
    /// avisar.
    /// </para>
    /// </summary>
    /// <param name="subject">Asunto de la plantilla.</param>
    /// <param name="bodyHtml">Cuerpo de la plantilla.</param>
    /// <param name="values">Valores disponibles por clave.</param>
    public static UnitResult<BaseError> CheckEventValues(
        string subject,
        string bodyHtml,
        IReadOnlyDictionary<string, string> values)
    {
        var missing = MissingEventValues(subject, bodyHtml, values);

        return missing.Count == 0
            ? UnitResult.Success<BaseError>()
            : UnitResult.Failure<BaseError>(new EventValuesMissingError(missing));
    }

    /// <summary>
    /// Lo mismo sin plantilla: si el evento tiene valor para <b>los tres</b> marcadores de grano
    /// de evento, los nombre alguna plantilla o no.
    /// <para>
    /// Es lo que pregunta el lanzamiento hoy, porque todavía no elige la plantilla por el idioma
    /// del evento (spec <c>02</c> §3) y por tanto no tiene un cuerpo que escanear. Las dos
    /// plantillas de campaña sembradas nombran los tres, así que hoy da la misma respuesta que
    /// <see cref="CheckEventValues"/>; el día que el cuerpo esté a mano, esta se retira.
    /// </para>
    /// </summary>
    /// <param name="values">Valores disponibles por clave.</param>
    public static UnitResult<BaseError> CheckEveryEventValue(IReadOnlyDictionary<string, string> values)
    {
        var missing = WithoutValue(SupportedPlaceholder.EventLevel, values);

        return missing.Count == 0
            ? UnitResult.Success<BaseError>()
            : UnitResult.Failure<BaseError>(new EventValuesMissingError(missing));
    }

    /// <summary>
    /// Marcadores de grano de evento que el texto nombra y para los que no llegó valor, en el
    /// orden en que aparecen.
    /// </summary>
    private static IReadOnlyList<string> MissingEventValues(
        string subject,
        string bodyHtml,
        IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(bodyHtml);

        return WithoutValue(
            PlaceholderScanner.Scan(subject, bodyHtml).Where(SupportedPlaceholder.EventLevel.Contains),
            values);
    }

    /// <summary>
    /// Cuáles de esas claves no traen valor, en el orden en que llegan. Una clave ausente y una
    /// con el valor en blanco son lo mismo acá: las dos dejarían un hueco en el correo.
    /// </summary>
    /// <param name="keys">Claves a comprobar.</param>
    /// <param name="values">Valores disponibles por clave.</param>
    private static IReadOnlyList<string> WithoutValue(
        IEnumerable<string> keys,
        IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return [.. keys.Where(key => !values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))];
    }
}
