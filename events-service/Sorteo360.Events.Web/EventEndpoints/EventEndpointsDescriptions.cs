namespace Sorteo360.Events.Web.EventEndpoints;

/// <summary>
/// Textos en español que documentan los endpoints de eventos en OpenAPI (los renderiza
/// /scalar). La prosa va en español y los identificadores en inglés, entre comillas
/// invertidas: el payload es inglés y traducirlo en la prosa sería mentir.
/// No mencionan el rol exigido ni los códigos de estado: el rol lo renderiza
/// <c>RequiredRoleOperationTransformer</c> desde la política del endpoint y los códigos
/// salen de las declaraciones <c>Produces</c>.
/// </summary>
internal static class EventEndpointsDescriptions
{
    public const string CreateEventSummary =
        "Crea un evento en estado `planning` junto con su primera ciudad.";

    public const string CreateEventDescription =
        """
        El tipo (`type`) queda fijo en la creación y no admite cambios posteriores. El
        evento nace en `planning` con la ciudad indicada en `city` y `country`, porque
        un evento nunca existe sin al menos una ciudad.

        `optOfficeId` y `optContactId` son identificadores del directorio de OPT, cuya
        autoridad es crm-service (`GET /v1/opts`). Se comprueban en vivo contra el CRM
        antes de escribir: la oficina tiene que existir y estar vigente, y el contacto
        —opcional— tiene que pertenecer a esa oficina y seguir vigente. Un identificador
        que no cumple se rechaza con el código `OPT_OFFICE_NOT_AVAILABLE` o
        `OPT_CONTACT_NOT_AVAILABLE`; si el CRM no responde se rechaza con
        `CRM_UNAVAILABLE`, en lugar de guardar un dato sin validar.

        El presupuesto estimado (`estimatedBudget`) es opcional y va como monto y moneda.
        Es lo que la OPT presupuestó, y convive con `totalCost`, que se calcula al cerrar
        con las cotizaciones aprobadas: ninguna regla ata uno al otro. Se rechaza con
        `EVENT_BUDGET_CURRENCY_NOT_SUPPORTED` si la moneda no es `USD` ni `EUR`, y con
        `EVENT_BUDGET_AMOUNT_INVALID` si el monto no es mayor que cero.

        La descripción (`description`) es texto libre y también opcional.

        `timeZone` es el identificador IANA de la zona horaria de esa primera ciudad, como
        `America/Chicago`. Es opcional: sin él la ciudad queda en `America/Santo_Domingo`.
        Un identificador que no es IANA, uno de Windows incluido, se rechaza con
        `TIME_ZONE_UNKNOWN`.

        Devuelve el identificador del evento creado.
        """;

    public const string ListEventsSummary =
        "Lista los eventos con filtros opcionales y paginación.";

    public const string ListEventsDescription =
        """
        Los filtros se combinan: `type`, `status`, rango de fecha de inicio
        (`startDateFrom`, `startDateTo`), `active`, `searchTerm` y `optOfficeId`.

        `active=true` devuelve los eventos que todavía no terminaron (`planning` o
        `in_progress`) y `active=false` los terminados (`closed` o `cancelled`). Cuando
        se envía `status`, este manda y `active` se ignora.

        `searchTerm` busca en el **nombre del evento** y en nada más, sin distinguir
        mayúsculas y sin exigir que el término esté al principio. No busca por nombre de
        OPT: ese nombre no está en esta base y sale de una copia del directorio del CRM
        que puede estar vencida, así que buscarlo haría que los resultados se angostaran
        solos durante una caída. Los comodines `%` y `_` se buscan como texto.

        `optOfficeId` filtra por la OPT dueña del evento, por identificador y no por
        nombre. Es el mismo id que devuelve `GET /v1/opts` en crm-service.

        `sortField` es uno de `name`, `startDate`, `status` o `totalCost`, y `sortOrder`
        uno de `asc` o `desc`, sin distinguir mayúsculas. Un valor fuera del conjunto se
        rechaza con `EVENT_SORT_FIELD_NOT_SUPPORTED` o `EVENT_SORT_ORDER_NOT_SUPPORTED`,
        en lugar de devolver la página en un orden distinto del pedido.

        Sin ninguno de los dos, el orden es el de siempre: fecha de inicio descendente.
        Con `sortField` y sin `sortOrder`, ascendente. Un `sortOrder` solo se aplica a la
        fecha de inicio, que es el campo por omisión.

        Ordenar por `status` sigue el ciclo de vida (`planning`, `in_progress`, `closed`,
        `cancelled`) y no el alfabeto.

        Ordenar por `totalCost` agrupa primero por moneda, con USD antes que EUR, y
        ordena por monto dentro de cada grupo: la plataforma no convierte, así que
        comparar 900 EUR con 1.000 USD por el número pelado sería mentir. El sentido
        invierte el monto y nada más: la moneda es el agrupamiento, así que USD queda
        primero en los dos sentidos, igual que un evento sin costo total queda último en
        los dos.

        Todos los filtros se combinan entre sí, y el `total` de la respuesta cuenta las
        coincidencias de la consulta, no la tabla entera.

        Sin `page` ni `pageSize` la respuesta trae la página 1 con 20 elementos.

        La URL pública del evento (`externalUrl`) viene en cada fila. Un evento sin URL la
        devuelve nula, nunca como cadena vacía.

        """ + CrmDirectoryStatusDescription;

    public const string ListDoorEventsSummary =
        "Lista los eventos de hoy, ayer y mañana, para que una terminal de la puerta elija uno.";

    public const string ListDoorEventsDescription =
        """
        Devuelve cada evento cuyo rango de fechas toca la ventana de un día antes a un
        día después de hoy: `startDate − 1 ≤ hoy ≤ endDate + 1`. Hoy es la fecha UTC; el
        día de margen a cada lado cubre montar la víspera, una noche que pasa de
        medianoche y la diferencia horaria con República Dominicana.

        Entran los eventos públicos y los privados por igual, en cualquier estado salvo
        `cancelled`. Dejar fuera los cancelados es una ayuda para elegir, no una regla de
        la puerta: un registro en un evento cancelado sigue funcionando.

        Cada elemento trae solo `id`, `name`, `startDate`, `endDate` y `cities`. El orden
        es por `startDate` y luego por `name`, y no hay paginación: la ventana deja unos
        pocos eventos.
        """;

    public const string GetEventByIdSummary =
        "Devuelve el detalle de un evento con sus ciudades.";

    /// <summary>
    /// Qué campos de OPT resuelve el CRM al leer, más la escalera de degradación compartida. La
    /// escalera vive en <c>CrmDirectoryStatusDescription</c> y no acá porque el mismo par de
    /// cabeceras lo emiten también las rutas de proveedor.
    /// </summary>
    public const string CrmDirectoryStatusDescription =
        """
        Los nombres de OPT (`opt.name`, `optContact.name`) los resuelve crm-service al
        leer y este servicio no los guarda, así que pueden venir en `null` sin que el
        evento esté incompleto.

        """ + Filters.CrmDirectoryStatusDescription.Ladder;

    public const string UpdateEventSummary =
        "Actualiza los campos editables de un evento.";

    public const string UpdateEventDescription =
        """
        Solo aplica sobre eventos en estado no terminal: uno cerrado o cancelado ya no
        admite cambios.

        El tipo (`type`) es inmutable y el costo total no se edita, porque se calcula al
        cerrar el evento.

        Los identificadores de OPT se comprueban contra el CRM igual que al crear, aunque
        no hayan cambiado: la oficina pudo cerrar desde que se creó el evento.

        El cuerpo lleva la representación editable completa, así que omitir un campo
        opcional lo borra: sin `estimatedBudget` el evento se queda sin presupuesto, y sin
        `description` se queda sin descripción. El presupuesto se valida igual que al
        crear, con `EVENT_BUDGET_CURRENCY_NOT_SUPPORTED` y `EVENT_BUDGET_AMOUNT_INVALID`.

        Devuelve el detalle actualizado.
        """;

    public const string StartEventSummary =
        "Inicia el evento y lo pone en ejecución.";

    public const string StartEventDescription =
        """
        Cambia el estado de `planning` a `in_progress`. Un evento que ya arrancó, o que
        terminó, no puede volver a iniciarse.

        Devuelve el detalle actualizado.
        """;

    public const string CancelEventSummary =
        "Cancela el evento con una razón obligatoria.";

    public const string CancelEventDescription =
        """
        La razón (`reason`) es obligatoria y queda registrada en el evento. Se cancela
        desde cualquier estado no terminal, y la cancelación arrastra las cotizaciones
        abiertas del evento.

        No es lo mismo que cerrar: cancelar termina el evento sin haberlo ejecutado, y
        no calcula ningún costo.
        """;

    public const string CloseEventSummary =
        "Cierra el evento y materializa su costo total.";

    public const string CloseEventDescription =
        """
        Solo se cierra un evento en `in_progress`: uno en `planning` nunca comenzó y uno
        terminal ya no admite transiciones.

        Al cerrar se calcula el costo total sumando el monto final de la propuesta aprobada
        de cada categoría, y queda fijado en el evento. El cierre no procede si el evento
        no tiene ninguna cotización, si alguna categoría sigue en borrador o esperando
        decisión, si a una categoría aprobada le falta el monto final, o si los montos
        mezclan monedas.

        Devuelve el detalle actualizado.
        """;
}
