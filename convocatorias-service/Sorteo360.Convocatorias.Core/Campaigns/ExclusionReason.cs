namespace Sorteo360.Convocatorias.Core.Campaigns;

/// <summary>
/// Por qué una persona que entró en el filtro no llegó a ser destinataria. Los cuatro códigos
/// de la spec <c>03</c> §6.1.
/// <para>
/// Los tres primeros los calcula el CRM y llegan en <c>suppressionReason</c>, con la
/// precedencia fijada allá y no acá: la regla pertenece a su dueño, o el día que el CRM crezca
/// un cuarto motivo este servicio le sigue escribiendo a esa gente hasta que alguien se
/// acuerde de redesplegarnos. El cuarto lo calcula convocatorias, porque el CRM no puede
/// informar sobre una fila que no devuelve.
/// </para>
/// <para>
/// Convocatorias pinta su propio español por código; el <c>message</c> nunca es contrato.
/// </para>
/// </summary>
public static class ExclusionReason
{
    /// <summary>No tiene dirección de correo. Lo calcula el CRM.</summary>
    public const string NoEmail = "no_email";

    /// <summary>Se dio de baja. Lo calcula el CRM.</summary>
    public const string OptedOut = "opted_out";

    /// <summary>MITUR lo marcó como no invitable. Lo calcula el CRM.</summary>
    public const string DoNotInvite = "do_not_invite";

    /// <summary>
    /// El CRM no devolvió esa ficha al recomprobar. Lo calcula convocatorias (spec <c>04</c>
    /// §3): el caso real es un contacto borrado en blando entre la selección y el lanzamiento.
    /// </summary>
    public const string ContactUnavailable = "contact_unavailable";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        NoEmail, OptedOut, DoNotInvite, ContactUnavailable
    };

    /// <summary>
    /// El motivo con el que se excluye a un contacto que el CRM declaró no contactable. El CRM
    /// manda: si su código pertenece al catálogo se guarda tal cual.
    /// <para>
    /// Si no —un motivo nuevo del otro lado, o ninguno— cae en <see cref="ContactUnavailable"/>,
    /// que es el único de los cuatro que no afirma una causa concreta: significa "no pudimos leer
    /// una respuesta utilizable sobre esta persona". La alternativa sería copiar acá el catálogo
    /// del CRM y mantenerlo al día, que es exactamente cómo la regla acaba viviendo en dos bases
    /// de código (spec <c>08</c> §2.6). Inventar un quinto código sí sería un cambio de spec: el
    /// CHECK de la tabla solo admite estos cuatro.
    /// </para>
    /// </summary>
    public static string FromCrm(string? suppressionReason) =>
        All.FirstOrDefault(reason => reason.Equals(suppressionReason, StringComparison.OrdinalIgnoreCase))
        ?? ContactUnavailable;
}
