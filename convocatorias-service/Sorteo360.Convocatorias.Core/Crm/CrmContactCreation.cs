namespace Sorteo360.Convocatorias.Core.Crm;

/// <summary>
/// Lo que devuelve pedirle al CRM que cree un contacto. Los dos desenlaces son buenos.
/// <para>
/// <b>Un 409 es una coincidencia, no un error.</b> Significa que esa dirección ya la ocupa
/// alguien vivo, así que se toma su id, se enlaza y se sigue; la fila pasa de "creada" a
/// "encontrada" en el resumen. Y <b>hay que volver a mirar su <c>canBeEmailed</c></b>, porque el
/// contacto que hay detrás de un 409 puede estar suprimido y sin esa comprobación la carrera le
/// escribe en silencio a quien se dio de baja. La comprobación es gratis: el 409 ya trae el
/// contacto (spec <c>05</c> §6).
/// </para>
/// </summary>
/// <param name="Contact">
/// El contacto resultante. Recién creado, o el que ya ocupaba la dirección.
/// </param>
/// <param name="AlreadyExisted">Si el CRM contestó 409 en vez de crear nada.</param>
public sealed record CrmContactCreation(CrmContact Contact, bool AlreadyExisted);
