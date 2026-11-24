namespace Sorteo360.Convocatorias.Core.CheckIns;

/// <summary>
/// Un evento al que asistió un contacto del CRM: alguna inscripción suya en ese evento tiene
/// check-in. Es la <i>participación</i> de una persona en el vocabulario del CRM: estar invitado no
/// cuenta, inscribirse tampoco, y quien llegó sin ser contacto del CRM no cuenta para nadie.
/// </summary>
/// <param name="ContactId">El <c>crm_contact_id</c> de la inscripción, nunca el del destinatario que la trajo.</param>
/// <param name="EventId">El evento, que es de events-service: acá solo se guarda el id.</param>
/// <param name="CheckedInAt">
/// Cuándo entró. Es la fecha del evento para quien la lee: el día en que la persona estuvo ahí, que
/// en un evento de varios días dice más que su fecha de inicio. Si un mismo contacto entró con dos
/// inscripciones del mismo evento, la primera.
/// </param>
public sealed record ContactCheckIn(Guid ContactId, Guid EventId, DateTimeOffset CheckedInAt);
