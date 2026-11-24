namespace Sorteo360.Convocatorias.Infrastructure.Crm;

/// <summary>
/// El cuerpo de <c>POST /v1/contacts</c> del CRM, tal como lo acepta hoy.
/// <para>
/// <b>Sin <c>onBehalfOfUserId</c></b>: la spec <c>08</c> §4 lo aplaza, así que el CRM atribuye
/// todo lo que se cree desde acá a <c>service-account-convocatorias-service</c>. Es aceptable
/// porque el lote de la carga registra quién confirmó, y el cambio posterior se rellena desde ahí.
/// </para>
/// </summary>
/// <param name="FirstName">Nombre.</param>
/// <param name="LastName">Apellidos.</param>
/// <param name="Email">La dirección, cruda: el CRM la normaliza.</param>
/// <param name="Phone">Teléfono, si la hoja lo traía.</param>
/// <param name="HasConsent">Cierto desde una carga confirmada y desde el portal; falso desde la carga de proveedores.</param>
/// <param name="ConsentSource">De dónde sale ese consentimiento; nulo sin consentimiento, como exige el CRM.</param>
public sealed record CrmCreateContactRequest(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    bool HasConsent,
    string? ConsentSource);

/// <summary>
/// La respuesta del CRM a un alta: o el id de lo que creó, o el contacto que ya ocupaba la
/// dirección. <b>Las dos son respuestas buenas</b>, y por eso el 409 no viaja como excepción.
/// </summary>
/// <param name="CreatedId">El id del contacto nuevo, nulo si no se creó ninguno.</param>
/// <param name="ExistingContact">El contacto que ya ocupaba la dirección, nulo si no lo hubo.</param>
public sealed record CrmCreateContactResponse(Guid? CreatedId, CrmContactResponse? ExistingContact);

/// <summary>
/// El ProblemDetails del 409 del CRM. La extensión <c>existingContact</c> es la que trae la
/// ficha viva, y es lo que evita una segunda petición para averiguar su id (spec <c>08</c> §2.3).
/// </summary>
/// <param name="ExistingContact">El contacto que ocupa la dirección.</param>
public sealed record CrmDuplicateEmailProblem(CrmContactResponse? ExistingContact);

/// <summary>
/// El cuerpo de <c>POST /v1/contacts/{id}/opt-out</c>. Un solo campo, y un solo valor admitido
/// en fase 1: la baja que llega desde acá siempre sale de que alguien hizo clic en el enlace de
/// una campaña (spec <c>08</c> §7).
/// </summary>
/// <param name="Source">De dónde sale la baja.</param>
public sealed record CrmOptOutRequest(string Source);

/// <summary>
/// El cuerpo de <c>POST /v1/contacts/mailability</c>. Es un POST y no una consulta porque mil
/// ids no caben en una URL, y una llamada por contacto serían mil idas y vueltas dentro de la
/// petición de lanzamiento (spec <c>08</c> §10).
/// </summary>
/// <param name="Ids">Los contactos por los que se pregunta; el CRM topa la lista en 1.000.</param>
public sealed record CrmMailabilityRequest(IReadOnlyList<Guid> Ids);

/// <summary>
/// Un veredicto de <c>POST /v1/contacts/mailability</c>. El CRM contesta esto y nada más: la
/// recomprobación no quiere la ficha, quiere saber si se le escribe.
/// <para>
/// <b>La respuesta puede venir más corta que lo pedido.</b> El CRM no informa sobre una ficha
/// que no devuelve, así que el id que falta lo resuelve quien llama.
/// </para>
/// </summary>
/// <param name="Id">Identidad del contacto.</param>
/// <param name="CanBeEmailed">Si se le puede escribir, según el CRM.</param>
/// <param name="SuppressionReason">Su código de motivo, nulo cuando sí se le puede escribir.</param>
public sealed record CrmMailabilityResponse(Guid Id, bool CanBeEmailed, string? SuppressionReason);

/// <summary>
/// Los valores de <c>opt_out_source</c> que el CRM reconoce. Uno solo en fase 1, y este servicio
/// no elige: toda baja que sale de acá viene de un clic en el enlace de una campaña, así que la
/// constante vive con el contrato del CRM y no en el dominio (spec <c>08</c> §7).
/// </summary>
public static class CrmOptOutSources
{
    /// <summary>Alguien hizo clic en el enlace de baja de un correo de campaña.</summary>
    public const string CampaignUnsubscribe = "campaign_unsubscribe";
}

/// <summary>
/// En qué acabó empujarle una baja al CRM. Los dos desenlaces son buenos, como en el alta: lo que
/// se le pide al CRM es que sepa que esa persona se dio de baja, y si ya no tiene su ficha,
/// tampoco le va a escribir.
/// </summary>
public enum CrmOptOutOutcome
{
    /// <summary>El CRM la registró, o ya la tenía registrada.</summary>
    Pushed,

    /// <summary>
    /// El CRM contestó 404: esa ficha ya no está. El caso real es un contacto borrado en blando
    /// entre el clic en el enlace de baja y el lanzamiento siguiente.
    /// </summary>
    ContactGone,
}
