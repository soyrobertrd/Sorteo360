using Sorteo360.Convocatorias.Core.Crm;

namespace Sorteo360.Convocatorias.Core.Registrations;

/// <summary>
/// En qué acabó llevar una inscripción al CRM, en la forma exacta en que se guarda
/// (spec <c>06</c> §4.2 y §4.3). Tres campos y ningún estado: los dos estados que hay ya salen
/// de cuáles de los dos primeros están puestos.
/// <para>
/// <b>El CRM se llama antes de escribir la fila, no después.</b> §4.3 se lee como un orden
/// —"guarda y luego marca"— pero habla de la respuesta, no del SQL. Escribir primero y actualizar
/// después abre una ventana en la que una caída deja la fila sin contacto <b>y</b> sin sello: un
/// tercer estado que el modelo de dos columnas no sabe nombrar y que el recuento de pendientes de
/// <c>07</c> no ve. Llamando antes, toda fila nace o sincronizada o pendiente.
/// </para>
/// <para>
/// Ese orden tiene dos costos, y se dicen en vez de esconderse. <b>Uno</b>: si la escritura falla
/// por algo que no sea la carrera de unicidad —la base caída— el contacto ya quedó creado en el
/// CRM para una inscripción que no existe. Es una persona que sí dio su consentimiento, así que la
/// ficha no es basura, solo llegó sola. <b>Dos</b>: en la carrera de unicidad los dos envíos
/// llaman al CRM, y el que pierde recibe un <c>409</c> y enlaza la misma ficha, así que el CRM
/// nunca duplica; lo que se pierde es su desenlace, porque la fila que vuelve es la del que ganó.
/// Si la del ganador quedó pendiente, sigue pendiente aunque el perdedor acabe de crear el
/// contacto, y quien lo resuelve es el reintento humano de <c>07</c>.
/// </para>
/// </summary>
/// <param name="ContactId">El contacto del CRM, o nulo si no se pudo llegar a él.</param>
/// <param name="FailedAt">Cuándo el CRM no contestó, o nulo si contestó.</param>
/// <param name="ContactDetailsDiffer">
/// Si lo que escribió no coincide con la ficha que ya estaba. <b>Solo puede ser cierto en el
/// camino del enlace</b>: una ficha que acabamos de crear se hizo con lo que escribió, así que
/// no puede discrepar de ello.
/// </param>
public sealed record CrmSyncOutcome(Guid? ContactId, DateTimeOffset? FailedAt, bool ContactDetailsDiffer)
{
    /// <summary>
    /// El <c>canBeEmailed</c> de la ficha que devolvió el CRM, o nulo si no contestó. <b>No se
    /// guarda</b>: una persona suprimida puede inscribirse igual (spec <c>06</c> §6), así que no
    /// cambia nada de la fila. Lo lee el reintento de <c>07</c> §6.1 para enseñárselo a quien lo
    /// pulsa, que es lo que la spec llama volver a mirarlo sobre el cuerpo del <c>409</c>.
    /// </summary>
    public bool? CanBeEmailed { get; init; }

    /// <summary>
    /// El CRM contestó: hay contacto. Lo mismo si lo creó esta inscripción que si ya estaba, que
    /// es por lo que la discrepancia viaja como parámetro y no se deduce acá.
    /// </summary>
    public static CrmSyncOutcome Synced(CrmContact contact, bool contactDetailsDiffer) =>
        new(contact.Id, null, contactDetailsDiffer) { CanBeEmailed = contact.CanBeEmailed };

    /// <summary>
    /// El CRM no contestó. <b>La inscripción se guarda igual</b>: la regla del
    /// <c>503 CRM_UNAVAILABLE</c> se escribió para nuestro propio personal en una pantalla de
    /// administración, y aplicada a un desconocido en un aeropuerto significa un portal del
    /// gobierno caído por un servicio del que nunca oyó hablar, y una persona que se va y no
    /// vuelve (§4.3).
    /// </summary>
    public static CrmSyncOutcome Pending(DateTimeOffset failedAt) =>
        new(null, failedAt, ContactDetailsDiffer: false);
}
