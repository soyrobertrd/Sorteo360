namespace Sorteo360.Convocatorias.Core.Templates;

/// <summary>
/// Para qué sirve una plantilla. Es una etiqueta sobre una lista plana y no una entidad
/// catálogo: el día que haga falta un catálogo por tipo de evento llegará como una columna
/// más y una consulta, que es exactamente por qué inventar la entidad hoy saldría caro.
/// <para>
/// La etiqueta manda sobre el enlace de baja: una plantilla de campaña lo lleva siempre, y una
/// transaccional no lo lleva nunca. Ver <see cref="Rendering.TemplateBodyRules"/>.
/// </para>
/// </summary>
public static class TemplatePurpose
{
    /// <summary>Correo a una lista que una persona seleccionó. Lleva enlace de baja.</summary>
    public const string Campaign = "campaign";

    /// <summary>
    /// Correo que contesta a un acto de la propia persona, o la avisa de algo que ya la afecta.
    /// Su lista se deriva de filas que ya tenemos, así que no hay de qué darse de baja.
    /// </summary>
    public const string Transactional = "transactional";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Campaign, Transactional
    };
}
