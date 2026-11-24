namespace Sorteo360.Events.Infrastructure.Crm;

/// <summary>La pregunta de contactabilidad: a lo sumo mil ids por llamada, el tope del CRM.</summary>
public sealed record CrmMailabilityRequest(IReadOnlyList<Guid> Ids);

/// <summary>
/// El veredicto del CRM sobre un contacto. El par del correo viaja pero el sorteo no lo lee: quien
/// decide es <c>CanBeInvited</c>, y <c>InviteSuppressionReason</c> solo le pone nombre (spec 03 §1b).
/// <c>CanBeInvited</c> es anulable a propósito: una fila sin él es una respuesta ilegible, y como
/// <c>bool</c> llegaría <c>false</c> y vaciaría el bombo sin que nadie se entere.
/// </summary>
public sealed record CrmMailabilityResponse(
    Guid Id,
    bool CanBeEmailed,
    string? SuppressionReason,
    bool? CanBeInvited,
    string? InviteSuppressionReason);

/// <summary>
/// Una página de <c>GET v1/contacts</c>, de la que solo se lee lo que nombra a cada contacto. El resto
/// de la ficha viaja y se ignora.
/// </summary>
public sealed record CrmContactNamePageResponse(IReadOnlyList<CrmContactNameResponse>? Items);

/// <summary>Un contacto de esa página: su id y su nombre completo, tal como lo arma el CRM.</summary>
public sealed record CrmContactNameResponse(Guid Id, string Name);
