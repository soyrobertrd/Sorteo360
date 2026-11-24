namespace Sorteo360.Convocatorias.Core.Campaigns;

/// <summary>
/// Lo que el worker necesita saber de una campaña para entregarla (spec <c>04</c> §1.1): en qué
/// estado está, con qué plantilla y en qué idioma sale, qué decía el evento cuando se lanzó, y qué
/// filas le quedan pendientes.
/// <para>
/// El estado viene con la lectura y no se da por supuesto. Entre el lanzamiento y el momento en
/// que el worker llega a esta campaña alguien puede haberla cancelado, y cancelar en pleno envío
/// está permitido a propósito (spec <c>03</c> §2): una campaña que ya no está en <c>sending</c>
/// no se entrega.
/// </para>
/// <para>
/// <b>Los valores del evento salen de esta fila y de ningún otro sitio.</b> El worker no llama a
/// events-service: hacerlo convertiría una caída del vecino en una campaña atascada, y dejaría que
/// una reanudación de la semana siguiente rindiera un texto distinto del que ya salió en el primer
/// correo (ver <see cref="CampaignEventValues"/>).
/// </para>
/// </summary>
/// <param name="State">Ver <see cref="CampaignState"/>.</param>
/// <param name="EventId">
/// El evento que se convoca, para que el transporte sepa de qué es el lote. Nulo en un mensaje
/// directo (ver <see cref="CampaignDirectMessage"/>).
/// </param>
/// <param name="TemplateKey">Qué correo sale. Se congeló en el lanzamiento.</param>
/// <param name="Language">En qué idioma.</param>
/// <param name="EventValues">
/// Los tres valores de grano de evento, copiados en el lanzamiento. Nulos solo en una campaña que
/// nunca se lanzó, o en una lanzada antes de que existieran las columnas: sin ellos no hay correo
/// que armar y la entrega falla en vez de mandar un hueco.
/// </param>
/// <param name="Subject">El asunto de un mensaje directo; nulo en una campaña de evento.</param>
/// <param name="Message">El texto de un mensaje directo; nulo en una campaña de evento.</param>
/// <param name="SenderEmail">
/// Desde dónde sale, copiado en el lanzamiento (PGTI-168). Nulo si sale desde la dirección general.
/// </param>
/// <param name="Recipients">
/// Solo las filas que siguen en <c>pending</c>. Es lo que hace que reanudar recorra la cola sin
/// entregar dos veces a quien ya se entregó (spec <c>04</c> §5).
/// </param>
public sealed record CampaignHandoff(
    string State,
    Guid? EventId,
    string TemplateKey,
    string Language,
    CampaignEventValues? EventValues,
    IReadOnlyList<PendingHandoffRecipient> Recipients,
    string? Subject = null,
    string? Message = null,
    string? SenderEmail = null)
{
    /// <summary>
    /// De esta campaña todavía puede salir un lote.
    /// <para>
    /// Son dos casillas y no una. <c>sending</c> es la normal. <c>sent</c> entra porque
    /// <c>retry-failed</c> devuelve a <c>pending</c> las filas que el emisor rechazó (spec
    /// <c>04</c> §5) y una campaña con rechazos ya está cerrada: lo que las cierra es que no quede
    /// ninguna pendiente, y eso pasa con la primera vuelta. <b>La campaña no vuelve atrás</b> —
    /// <c>sent</c> sigue siendo terminal y la tabla de transiciones de la spec <c>03</c> §2 no
    /// gana ninguna arista—, lo que pasa es que se le vuelve a mandar una cola que alguien
    /// reabrió.
    /// </para>
    /// <para>
    /// <c>draft</c> no entra porque nunca salió, y <c>cancelled</c> no entra porque alguien la
    /// paró a conciencia: cancelar en pleno envío está permitido y sus filas sin tocar se quedan
    /// <c>pending</c> para siempre, que es como se lee "pretendíamos escribirle y paramos".
    /// </para>
    /// </summary>
    public bool IsDeliverable => State is CampaignState.Sending or CampaignState.Sent;
}

/// <summary>
/// Una fila que todavía espera su correo, con la copia del nombre y la dirección que se guardó al
/// seleccionarla. <b>La identidad de la fila es <paramref name="RecipientId"/></b>, no la
/// dirección: es por donde vuelve el resultado de la entrega, así que dos personas con el mismo
/// correo no se pisan.
/// </summary>
/// <param name="HasTokens">
/// Si esta fila ya guarda <b>alguno</b> de sus dos hashes. En el camino normal no puede: los dos se
/// escriben en la misma transacción que anota el desenlace, así que una fila pendiente no tiene
/// ninguno. Que tenga alguno significa que esa escritura aterrizó a medias, y entonces su correo ya
/// salió con un enlace que funciona: acuñarle un segundo token lo rompería.
/// <para>
/// <b>Es "alguno" y no "los dos" a propósito</b>, porque es el mismo predicado con el que la
/// escritura se niega a pisar un hash puesto. Con "los dos", una fila a medias se leería como
/// acuñable, recibiría un segundo correo, y la escritura le rechazaría el hash nuevo: un enlace
/// muerto.
/// </para>
/// </param>
public sealed record PendingHandoffRecipient(
    Guid RecipientId,
    Guid ContactId,
    string FirstName,
    string LastName,
    string Email,
    bool HasTokens);
