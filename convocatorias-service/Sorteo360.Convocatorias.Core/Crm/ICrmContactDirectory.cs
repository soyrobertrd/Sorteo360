using CSharpFunctionalExtensions;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Crm;

/// <summary>
/// El directorio de contactos del CRM visto desde el dominio: las lecturas que construyen la
/// lista de una campaña y casan una fila de hoja subida, más el alta que una carga confirmada
/// necesita. <b>Directorio y no solo lectura</b>: el CRM es el sistema de registro de las
/// personas, y dar de alta a una es una de las cosas que se le piden.
/// <para>
/// <b>Acá no hay escalera de degradación.</b> Si el CRM no contesta, la selección falla con
/// <c>503 CRM_UNAVAILABLE</c>: una lista no se puede construir de la nada. Es una desviación
/// deliberada de la regla de resolver-al-leer del <c>CLAUDE.md</c> raíz, y ADR-0017 la anota
/// para que nadie reutilice acá la escalera de ADR-0013 por reflejo (spec <c>03</c> §6.2).
/// </para>
/// <para>
/// <b>Con una excepción, y es del llamador y no de acá</b> (spec <c>06</c> §4.3). El portal de
/// registro tapa el fallo en vez de devolverlo: guarda la inscripción, marca la fila como
/// pendiente de sincronizar y contesta <c>201</c>. El motivo es quién está del otro lado — un
/// desconocido en un aeropuerto, no nuestro propio personal en una pantalla de administración, y
/// un <c>503</c> ahí es un portal del gobierno caído por un servicio del que nunca oyó hablar.
/// Este directorio sigue devolviendo el mismo <c>Result</c> fallido a todo el mundo; lo que
/// cambia es qué hace cada llamador con él.
/// </para>
/// </summary>
public interface ICrmContactDirectory
{
    /// <summary>
    /// Todos los contactos que casan con el filtro, paginando por debajo hasta agotarlo. El
    /// <c>pageSize</c> del CRM está topado en 100, así que 800 contactos son ocho llamadas
    /// pagadas <b>una vez</b>, al seleccionar, y nunca más: una campaña es historia, no una
    /// consulta viva.
    /// </summary>
    /// <param name="classifications">Clasificaciones de contacto; O entre ellas.</param>
    /// <param name="companyCategories">Categorías de empresa; O entre ellas, Y con las anteriores.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<Result<IReadOnlyList<CrmContact>, BaseError>> ListContactsAsync(
        IReadOnlyList<string> classifications,
        IReadOnlyList<string> companyCategories,
        CancellationToken cancellationToken);

    /// <summary>
    /// Los contactos con esos ids, que es como se llena una campaña con los que alguien eligió a
    /// mano en el CRM (PGTI-179). Llegan con su <c>canBeEmailed</c> igual que los del filtro, así
    /// que la exclusión es la misma.
    /// <para>
    /// Los ids repetidos se piden una vez, y la lista se parte en llamadas de 100, el tope del
    /// CRM. <b>Puede volver corta</b>: un id que no existe, o de un contacto borrado o suprimido,
    /// el CRM no lo devuelve, y no es un fallo. Quien llama lo ve en <c>matched</c>.
    /// </para>
    /// </summary>
    /// <param name="contactIds">Los contactos elegidos.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<Result<IReadOnlyList<CrmContact>, BaseError>> ListContactsByIdAsync(
        IReadOnlyList<Guid> contactIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Una lista de contactos por id, con su OPT y sus miembros vivos (PGTI-173). Se lee con la
    /// identidad de este servicio (<c>crm:service-read</c>), que la ve sin acotar por OPT: decidir
    /// si es de quien llama le toca a <see cref="IContactListReader"/>.
    /// <see cref="Maybe{T}.None"/> cuando el CRM no la tiene.
    /// </summary>
    /// <param name="contactListId">La lista.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<Result<Maybe<CrmContactList>, BaseError>> FindContactListAsync(
        Guid contactListId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Busca por dirección exacta, que es como se casa una fila de una hoja subida con una ficha
    /// del CRM. <see cref="Maybe{T}.None"/> cuando esa dirección no está en el CRM.
    /// <para>
    /// <b>Normaliza el CRM y nosotros no</b> (spec <c>05</c> §5): acá solo se recortan los
    /// espacios y se manda la dirección cruda a <c>GET /v1/contacts?email=</c>, que es quien
    /// pasa a minúsculas y compara contra <c>email_normalized</c>. Implementar nuestra propia
    /// normalización pondría una regla en dos bases de código, y eso sale como una fila
    /// informada "no encontrada" que después da 409 al confirmar.
    /// </para>
    /// <para>
    /// Es un filtro aparte de <c>search</c> a propósito: aquel es un <c>ILIKE</c> sobre nombre y
    /// correo, así que <c>ana@acme.com</c> es subcadena de <c>mariana@acme.com</c> y casar por
    /// ahí ataría a alguien a la ficha de un desconocido.
    /// </para>
    /// </summary>
    Task<Result<Maybe<CrmContact>, BaseError>> FindByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Crea un contacto suelto. Los dos desenlaces posibles son buenos: creado, o ya existía y
    /// el CRM devolvió su ficha en un 409 (ver <see cref="CrmContactCreation"/>).
    /// <para>
    /// <b>Una confirmación parcial nunca se deshace.</b> Lo que se creó en el CRM se queda
    /// creado: el CRM es el sistema de registro y no tenemos autoridad para borrar a una persona
    /// de allí para dejar limpio un lote nuestro que falló.
    /// </para>
    /// </summary>
    Task<Result<CrmContactCreation, BaseError>> CreateContactAsync(
        NewCrmContact contact,
        CancellationToken cancellationToken);

    /// <summary>
    /// Empuja al CRM la baja de un contacto. <b>Idempotente del otro lado</b>: repetirla sobre
    /// quien ya está de baja contesta 200 y no 409 (spec <c>08</c> §7), que es lo que permite
    /// reintentar un vaciado que se cortó a medias sin llevar cuenta de por dónde iba.
    /// <para>
    /// El lanzamiento las vacía todas <b>antes</b> de recomprobar la contactabilidad (spec
    /// <c>04</c> §3.1). Una baja sin sincronizar no es inocua: si el empujón nunca llega, el CRM
    /// sigue contestando <c>canBeEmailed: true</c> por esa persona y la campaña siguiente le
    /// escribe. Vaciar primero es también lo que evita escribir acá una segunda regla de
    /// supresión: en vez de enseñarle a este servicio a razonar sobre su propia columna, se hace
    /// que la respuesta del CRM sea correcta antes de preguntarle.
    /// </para>
    /// </summary>
    /// <param name="contactId">El contacto que se dio de baja.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<UnitResult<BaseError>> OptOutAsync(Guid contactId, CancellationToken cancellationToken);

    /// <summary>
    /// Vuelve a preguntar por la lista entera de destinatarios, que es lo último que se comprueba
    /// antes de mandar nada. Seleccionar y lanzar no son el mismo instante, y escribirle a quien
    /// se dio de baja en medio es el fallo que este orden existe para impedir (spec <c>04</c> §3).
    /// <para>
    /// Lo pedido se parte en llamadas de <b>1.000 ids</b>, que es el tope del CRM, y la respuesta
    /// llega unida. <b>Puede volver corta</b>: un id que no vuelve no es un fallo, es un contacto
    /// que el CRM ya no devuelve, y quien llama lo suprime por
    /// <see cref="Campaigns.ExclusionReason.ContactUnavailable"/>.
    /// </para>
    /// <para>
    /// Si la comprobación no se puede completar, falla entera con <c>503 CRM_UNAVAILABLE</c> y no
    /// se entrega a nadie: media campaña mandada contra una lista de bajas sin verificar es peor
    /// que ninguna.
    /// </para>
    /// </summary>
    /// <param name="contactIds">Los contactos por los que se pregunta.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<Result<IReadOnlyList<CrmMailability>, BaseError>> CheckMailabilityAsync(
        IReadOnlyList<Guid> contactIds,
        CancellationToken cancellationToken);
}
