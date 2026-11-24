namespace Sorteo360.Convocatorias.Web.CampaignEndpoints;

/// <summary>Textos del documento OpenAPI para las rutas de campañas.</summary>
internal static class CampaignEndpointsDescriptions
{
    public const string ListSummary = "Lista las campañas";

    public const string ListDescription =
        """
        Devuelve las campañas paginadas, con sus dos cuentas: cuántos destinatarios y cuántas
        exclusiones lleva cada una.

        Filtra por `eventId` y por `state` (`draft`, `sending`, `sent`, `cancelled`). Un estado
        que no pertenece al catálogo devuelve una página vacía en lugar de devolverlo todo: colar
        un filtro que no se entiende es la forma de que alguien crea que no hay campañas cuando
        lo que hay es una errata.

        No es el resumen por evento, que llega con el seguimiento.
        """;

    public const string CreateSummary = "Crea un borrador de campaña";

    public const string CreateDescription =
        """
        Crea la campaña en `draft` para un evento, con su plantilla y su idioma. Nace sin
        destinatarios: la lista se construye después, con la selección.

        El idioma tiene que ser uno de los seis admitidos (`es`, `en`, `fr`, `de`, `pt`, `it`),
        y la plantilla tiene que existir en ese idioma: si no, responde
        `400 CAMPAIGN_TEMPLATE_NOT_FOUND` con la clave y el idioma. No hay caída al español.
        Tanto la plantilla como el idioma son editables mientras siga en borrador y se congelan
        al lanzar.

        **Sin `eventId` es un mensaje directo**: la plantilla es `direct_message` y el cuerpo trae
        `subject` y `message`, el asunto y el texto plano que salen en el correo, sin enlace de
        registro. Una campaña sin evento con otra plantilla, un mensaje directo con evento, o uno
        sin asunto o sin texto responden `400 CAMPAIGN_DIRECT_MESSAGE_MISMATCH`, y una campaña de
        evento no admite ni `subject` ni `message`.
        """;

    public const string GetSummary = "Obtiene una campaña";

    public const string GetDescription =
        """
        La campaña con sus dos cuentas. `recipientCount` es el número que hay que repetir en
        `expectedRecipientCount` al lanzar.

        Leer una campaña **no llama al CRM**: la fila de cada destinatario guarda el nombre y la
        dirección a los que se escribió, así que el registro de un envío no se reescribe solo
        cuando alguien edita su ficha meses después.
        """;

    public const string UpdateSummary = "Edita un borrador de campaña";

    public const string UpdateDescription =
        """
        Cambia la plantilla y el idioma. **Solo en borrador**: una campaña en `sending`, `sent` o
        `cancelled` responde `409 CAMPAIGN_NOT_DRAFT`, con el estado en el que sí está.

        La pareja que resulta del cambio tiene que tener plantilla: cambiar solo el idioma a uno
        en el que esa plantilla no existe responde `400 CAMPAIGN_TEMPLATE_NOT_FOUND`.

        No se cambia de evento, porque una campaña de otro evento es otra campaña. Por eso mismo
        una campaña de evento no pasa a `direct_message` ni al revés, y `subject` y `message` solo
        se le editan a un mensaje directo: lo demás responde `400 CAMPAIGN_DIRECT_MESSAGE_MISMATCH`.
        """;

    public const string SelectRecipientsSummary = "Construye la lista de destinatarios desde el CRM";

    public const string SelectRecipientsDescription =
        """
        Lee los contactos del CRM que casan con el filtro y los materializa en filas: destinatario
        para quien se puede contactar, exclusión con su motivo para quien no. Las dos dimensiones
        son repetibles, **O dentro de cada una y Y entre ellas**, y esa regla la aplica el CRM.

        En vez del filtro puede llegar `contactIds`, los contactos elegidos a mano, de 1 a 1.000 y
        sin ninguna de las dos dimensiones al lado. Sirve con evento o sin él. Un id que el CRM no
        devuelve (inexistente, borrado o suprimido) no entra ni deja fila: se nota en que `matched`
        es menor que los ids enviados.

        O puede llegar `contactListId`, una lista de contactos del CRM, también sola. Entran sus
        miembros vivos con la misma exclusión. La lista tiene que ser de la OPT de quien llama:
        sin OPT (un administrador central, por ejemplo) responde `403 OPT_NOT_ASSIGNED`, y una
        lista que no existe o es de otra OPT, `404 CONTACT_LIST_NOT_FOUND`.

        **El CRM no filtra nada de esa lectura.** Viene quien no tiene correo y viene quien se dio
        de baja, y este servicio los excluye a la vista: quien espera 240 destinatarios y recibe
        227 en silencio no tiene forma de encontrar a los 13 que faltan. Los motivos son
        `no_email`, `opted_out`, `do_not_invite` y `contact_unavailable`.

        Es aditiva e idempotente: un contacto que ya está en la campaña se cuenta en `skipped` y
        no se vuelve a escribir, así que dos selecciones con filtros distintos componen en vez de
        duplicar.

        **Solo en borrador.** Si el CRM no contesta responde `503 CRM_UNAVAILABLE` y no deja lista
        a medias: una lista no se puede construir de la nada.
        """;

    public const string RemoveRecipientSummary = "Quita un destinatario de la lista";

    public const string RemoveRecipientDescription =
        """
        Borra la fila. **Solo en borrador.**

        No escribe una exclusión: una exclusión dice "el filtro lo trajo y el CRM dijo que no", y
        esto es una persona que alguien quitó a mano. Pintarla como exclusión mezclaría las dos
        cosas en la única tabla que contesta "¿quién falta y por qué?".
        """;

    public const string LaunchSummary = "Lanza la campaña";

    public const string LaunchDescription =
        """
        De `draft` a `sending`, en una sola llamada. **Nadie aprueba una campaña**: quien tiene
        `campaigns:launch` la lanza y sale.

        El cuerpo lleva `expectedRecipientCount`, **obligatorio**: la cuenta que quien lanza tenía
        en pantalla. Si la lista cambió debajo, el rechazo trae la cuenta real, para que la
        pantalla pueda decir "esto eran 227 cuando lo abriste y ahora son 240".

        **El orden es la funcionalidad.** Antes de tocar el estado se le empujan al CRM las bajas
        que todavía no le llegaron, y recién entonces se le vuelve a preguntar si se le puede
        escribir a la lista entera: al revés, el CRM contestaría que sí por alguien que acaba de
        darse de baja. A quien conteste que no se le anota la fila como suprimida y la campaña
        sigue para el resto; suprimir a alguien no es un error. Después del CRM se lee el evento,
        que es lo último que puede negarse. Un mensaje directo no tiene evento y se salta esa
        lectura y la de la ventana de inscripción; las bajas y el CRM le aplican igual.

        **Todo lo que puede negarse ocurre antes de contestar**, y cada rechazo deja la campaña
        intacta, en `draft`, sin sellos y sin tocar ninguna fila: no haber seleccionado a nadie,
        haber seleccionado a gente a la que ya no se le puede escribir, traer una cuenta que dejó
        de ser la real, que la plantilla no exista en el idioma de la campaña, que el evento esté
        cancelado, que le falte el nombre, el lugar o la fecha
        que el correo nombra, o que el CRM o events-service no estén. Con un vecino caído no se
        entrega a nadie: media campaña mandada contra una lista de bajas sin verificar, o un correo
        con un hueco donde va el evento, son peores que ninguna.

        **El evento se lee acá porque no hay otro sitio donde enterarse.** events-service nunca
        llama a convocatorias, así que escribirle a varios cientos de personas por un evento
        cancelado hace una hora solo lo impide esta lectura.

        Un lanzamiento que sí procede estampa quién y cuándo, deja `sentAt` nulo, apunta el
        `Location` a la campaña y contesta que quedó aceptada: la entrega corre después de
        contestar, y `sentAt` lo estampa el proceso que la drena cuando no queda ninguna fila
        pendiente.
        """;

    public const string CancelSummary = "Cancela la campaña";

    public const string CancelDescription =
        """
        Desde `draft` o desde `sending`, y de ningún otro sitio: sobre una campaña ya terminal
        responde `409 CAMPAIGN_TRANSITION_NOT_ALLOWED`.

        Cancelar en pleno envío está permitido a propósito. Negarlo dejaría como única salida de
        una campaña que no debía salir terminar de mandarla a todo el mundo. Deja a unos mandados
        y a otros no, y las filas dicen exactamente quién está en cada grupo: **las que no se
        tocaron se quedan `pending`**, que se lee correctamente como "pretendíamos escribirle y
        paramos".
        """;

    public const string ResumeHandoffSummary = "Reanuda la entrega pendiente";

    public const string ResumeHandoffDescription =
        """
        Vuelve a mandar **la cola sin entregar** de una campaña que se quedó a medias: un lote que
        se cayó, o un redespliegue que se llevó el worker con el proceso. Solo sobre una campaña en
        `sending`, que es el único estado en el que esa cola existe.

        **Nada en este servicio reintenta solo, a propósito.** Reanudar es trabajo de una persona,
        y lo que recorre es lo que está en `pending`, así que apretarlo dos veces es inofensivo:
        quien ya recibió no vuelve a recibir.

        La entrega ocurre después de contestar, en el mismo worker que la del lanzamiento, así que
        el cuerpo es de este instante y `Location` apunta a donde se mira cómo va.
        """;

    public const string RetryFailedSummary = "Reintenta los destinatarios rechazados";

    public const string RetryFailedDescription =
        """
        Devuelve a la cola las filas que el emisor rechazó, para que salgan en la siguiente vuelta
        del worker.

        Sobre una campaña en `sending` **o en `sent`**, y ese segundo es el caso normal: lo que
        cierra una campaña es que no le quede ninguna fila pendiente, así que una con rechazos ya
        está cerrada. **No vuelve atrás**: sigue en `sent`, se le reabre una cola y el worker la
        cierra otra vez con `sentAt` al día. Desde `draft` o `cancelled` se rechaza con el código
        `CAMPAIGN_NOT_SENDING`.

        En el momento de aceptar, el único rechazo que existe es una dirección mal formada, así que
        esta ruta sirve cuando la dirección se corrigió en el CRM y la lista se volvió a armar. Sin
        ninguna fila rechazada no hace nada y contesta igual: no es un error pedirlo de más.
        """;
}
