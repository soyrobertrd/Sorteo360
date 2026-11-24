namespace Sorteo360.Notifications.Web.NotificationEndpoints;

/// <summary>
/// Texto de la documentación OpenAPI de los endpoints de envío y de historial. Vive acá para que
/// <c>DefineEndpoints</c> siga siendo una tabla de rutas legible de un vistazo.
/// </summary>
internal static class NotificationEndpointsDescriptions
{
    public const string SendSummary = "Aceptar una notificación y registrarla";

    public const string SendDescription = """
        Recibe un correo completo —el remitente, el asunto, el cuerpo HTML con sus
        `{{placeholders}}`, los valores, las personas y las entidades de las que trata— y lo
        registra para que el worker lo mande. El 202 es la línea: después de él existe una fila de
        historial, antes de él no existe nada.

        El cuerpo se guarda **sin sustituir** y los valores de `data` no se guardan en ninguna
        columna. De ahí la obligación de quien llama, que ningún código puede comprobar: los datos
        personales y cualquier credencial van en `data`, nunca escritos dentro del `body`.

        Todos los valores son cadenas, incluidos los números y los importes ya formateados. Los
        campos que este servicio no conoce se ignoran en vez de rechazarse.

        `idempotencyKey` la acuña quien llama a partir de una clave natural, y nombra el envío, no
        la cosa de la que trata. Repetirla con el mismo contenido devuelve otro 202 con los mismos
        ids y no crea nada; repetirla con otro contenido es un conflicto.

        La respuesta lleva ids y nada más: no hay estado agregado en el que ramificar, porque el
        correo sale después de contestar. Una dirección que no es una dirección es lo único que se
        puede rechazar por persona, y una dirección suprimida se acepta acá y la marca el worker.
        """;

    public const string ListHistorySummary = "Listar el historial de envíos";

    public const string ListHistoryDescription = """
        Una fila por correo mandado, de la más reciente a la más vieja. Es la pantalla del
        cliente: *"déjame ver todos los correos que mandamos"*.

        Filtros: `type`, `deliveryStatus`, `recipientAction`, `email`, `entityType` + `entityId`,
        `sourceService`, `sentFrom` / `sentTo`, e `id` repetido (`?id=a&id=b`, hasta 100), que es el
        id por persona del `202`. La paginación es `page` y `pageSize`, que por
        omisión trae 25 filas y admite 100 como máximo.

        `entityType` + `entityId` van siempre juntos y contestan *"todo lo que se mandó sobre este
        evento"*, incluidas las filas de envíos distintos que lo mencionan.

        `id` es como quien mandó lee el estado de sus propios envíos en una sola llamada.

        `email` es coincidencia **exacta**: no busca por parecido ni pliega mayúsculas. No hay
        búsqueda de texto libre sobre el asunto.

        `sentFrom` / `sentTo` acotan por la fecha en que la plataforma aceptó el envío. Una fila
        que nunca salió no tiene fecha de salida, y son justo las que se buscan cuando algo falló.

        Cada fila lleva `deliveryTrackingAvailable`. Cuando es falso el estado no pasa de `sent`
        nunca, y la pantalla tiene que decir "seguimiento de entrega no disponible en este
        entorno" en vez de leer ese `sent` como un final.

        Una persona ve las filas de todos los servicios; una cuenta de servicio ve solo las de los
        envíos que tratan del suyo, y solo las entidades suyas dentro de cada envío.
        """;

    public const string ListOrphanEventsSummary = "Listar los eventos sin correlacionar";

    public const string ListOrphanEventsDescription = """
        Los eventos que llegaron del proveedor sin poder atarse a ninguna fila de envío, con su
        JSON crudo.

        Tienen endpoint propio y no un filtro por id nulo: un huérfano no nombra ninguna fila, así
        que no puede aparecer en el listado de personas ni queriendo, y muchos son rebotes, que es
        el evento que más importa. Detrás de un filtro sería un cajón que nadie abre.

        Una cuenta de servicio ve una lista vacía: un huérfano no tiene envío, así que no hay
        servicio de origen contra el que acotarlo.

        Está vacío mientras no exista el webhook.
        """;

    public const string GetBatchSummary = "Obtener un envío con su cuerpo y sus personas";

    public const string GetBatchDescription = """
        El envío entero: el asunto, el cuerpo, de qué trataba y a quién se le mandó. Es lo que
        resuelve la cabecera `Location` con la que se contesta un envío aceptado, así que existe
        con o sin pantalla de historial.

        El cuerpo sale **sin sustituir**, con sus `{{placeholders}}` tal como se guardaron. Es lo
        único que hay: los valores no se guardan, y por eso no existe el reenvío. Un botón que
        mandara un correo distinto del que la pantalla muestra sería peor que no tener botón.

        Un envío que quien pregunta no alcanza se contesta igual que uno que no existe: decir que
        existe ya sería decir algo que no le toca.
        """;
}
