namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>
/// Textos en español que documentan los endpoints del sorteo en OpenAPI (los renderiza /scalar).
/// Ver <c>EventEndpointsDescriptions</c> para la convención completa.
/// </summary>
internal static class RaffleEndpointsDescriptions
{
    public const string CreateRaffleSummary =
        "Crea el sorteo del evento, en borrador y con los valores por omisión.";

    public const string CreateRaffleDescription =
        """
        Un evento tiene a lo sumo un sorteo. No lleva cuerpo: el sorteo nace en `draft` con ruleta,
        premio visible, seis segundos de animación, sin confirmación de presencia, un premio por
        persona sí y uno por empresa no, y las autoridades en blanco; todo eso se ajusta después
        con el `PATCH`.

        El sorteo no tiene nombre ni fecha propios: los dos son del evento. Un evento cerrado o
        cancelado ya no admite un sorteo nuevo.
        """;

    public const string DeleteRaffleSummary =
        "Elimina el sorteo del evento mientras siga en borrador.";

    public const string DeleteRaffleDescription =
        """
        Es la salida de un sorteo creado en el evento equivocado: como un evento admite un solo
        sorteo, sin ella ese evento quedaría bloqueado. Se van con él sus rondas, sus premios y sus
        decisiones manuales, y después el evento admite un sorteo nuevo.

        Solo en `draft`, donde todavía no hay nada congelado ni sorteado: en curso es
        `RAFFLE_NOT_DRAFT` y completado es `RAFFLE_COMPLETED`, porque a partir de ahí el sorteo es
        evidencia. Un evento cerrado o cancelado tampoco lo admite (`EVENT_NOT_EDITABLE`).
        """;

    public const string ListRafflesSummary =
        "Lista los sorteos de todos los eventos.";

    public const string ListRafflesDescription =
        """
        Del evento más reciente al más viejo. Cada sorteo trae el nombre y la fecha de inicio de su
        evento, leídos del evento en cada consulta, su `state`, cuántos premios tiene
        (`prizeCount`), cuántos tienen un ganador vivo, entregados incluidos (`drawnCount`), y
        cuántos se entregaron (`redeemedCount`). El sorteo de un evento eliminado no aparece.
        """;

    public const string GetRaffleTotalsSummary =
        "Devuelve las cifras de todos los sorteos juntos.";

    public const string GetRaffleTotalsDescription =
        """
        `active` son los sorteos en curso, `winners` los sorteos de premio vivos y `redeemed` los
        vivos con una entrega en pie. `qualifiedParticipants` suma los bombos congelados, así que un
        borrador no aporta nada: su puerta todavía cambia. Los eventos eliminados no cuentan.
        """;

    public const string GetRaffleSummary =
        "Devuelve el sorteo del evento con sus rondas y premios.";

    public const string GetRaffleDescription =
        """
        En `draft` trae además si puede arrancar: `isReadyToStart` es verdadero exactamente cuando
        `blockingReasons` está vacía. Los motivos que bloquean son `no_qualified_attendees`,
        `attendance_unavailable` (no se pudo leer la puerta y no hay copia; en ese caso no se dice
        además que el bombo esté vacío, porque no se sabe), `attendance_too_many_rows` (más de
        25.000 asistentes; la lectura del sorteo no se niega por eso) y `crm_unavailable` (el CRM no
        contestó y el bombo se calculó sin su lista negra).

        `warnings` no frena el arranque: `awards_list_not_in_force` (no hay lista de DO Travel
        Awards y ese paso se salta), `attendance_copy_stale` (la puerta se muestra de una copia
        vieja) y `no_prizes`.

        Fuera de `draft`, `isReadyToStart` y `blockingReasons` van nulos y la puerta no se lee;
        `warnings` sigue, con `no_prizes` y con `awards_list_not_in_force` si al arrancar no había
        lista en vigor, que es lo que dirá el acta. `startedAt` y `startedById` dicen quién arrancó
        el sorteo y cuándo, y `awardsBatchId` qué lista se aplicó; los tres son nulos en `draft`.

        Cada premio trae su `state` (`undrawn` o `drawn`, calculado de sus sorteos) y sus `draws`,
        vivos y anulados, del más nuevo al más viejo; el ganador es el que tiene `void` nulo. Cada
        sorteo trae `onePrizePerPerson` y `onePrizePerCompany`: las reglas bajo las que salió, que
        pueden no ser las que el sorteo tiene ahora.

        Cada premio trae `sponsor`, nulo si no tiene patrocinador: `companyId` y `contactId`, que son
        lo que se guarda, y `companyName` y `contactName`, que se resuelven contra el CRM en cada
        respuesta y van nulos si no se pudieron resolver; la respuesta es 200 igual. `logo` es el
        logo de la empresa patrocinadora, nulo si no se subió: `fileId`, con el que se piden los
        bytes a `GET /v1/files/{fileId}/content`, `fileName`, y `missing` si el almacenamiento los
        perdió.

        """ + CrmMailabilityStatus;

    public const string GetAttendeesSummary =
        "Devuelve la lista de la puerta del evento, clasificada para el sorteo.";

    public const string GetAttendeesDescription =
        """
        Todos los registrados en la puerta, calificados o no, cada uno no calificado con su único
        `exclusionReason`, el primero que aplica en este orden: `walk_in`, `contact_unresolved`
        (sin contacto en el CRM, o con uno que el CRM ya no devuelve), `suspended` (suspendido en
        el CRM), `do_not_invite` (el CRM no deja invitarlo), `manually_excluded`,
        `not_awards_member` y `dominican_domain`. `checkedIn` siempre es `qualified` más los no
        calificados. Van ordenados por apellido, nombre y correo.

        Una decisión manual se muestra en `manualDecision` (`included` o `excluded`), con
        `manualNote`, `manualDecidedById` y `manualDecidedAt`, haya ganado o no. Una inclusión
        nunca vence a los cuatro primeros motivos: `overruledReason` dice qué venció una inclusión,
        o qué la venció a ella, y es nulo en una exclusión y en una inclusión sobre alguien que
        calificaba igual.

        Se calcula en cada lectura y no escribe nada. La puerta se lee de una copia de hace a lo
        sumo unos segundos: `attendanceFetchedAt` dice de cuándo es y `attendanceStatus` qué tan
        vigente (`ok`, `stale` si la última lectura falló y se sirve la anterior, `unavailable` si
        nunca hubo copia). Con `unavailable`, `checkedIn`, `qualified` y `attendees` van nulos: no
        se sabe quién está, que no es lo mismo que nadie.

        `awardsBatchId` es la lista de DO Travel Awards que se aplicó, nulo si no hay ninguna en
        vigor.

        Más de 25.000 asistentes se rechaza con `ATTENDANCE_TOO_MANY_ROWS`.

        Después del arranque es la lista congelada, en el mismo orden, y no se consulta nada:
        `attendanceStatus` es `ok`, `attendanceFetchedAt` la hora del arranque (la puerta se leyó en
        vivo en ese instante) y `awardsBatchId` la lista que se aplicó. Tampoco sale cabecera del CRM.

        """ + CrmMailabilityStatus;

    public const string StartRaffleSummary =
        "Arranca el sorteo y congela la lista de la puerta.";

    public const string StartRaffleDescription =
        """
        Lee la puerta en vivo, no de la copia de las lecturas, le pregunta al CRM por todos sus
        contactos, aplica la lista de DO Travel Awards en vigor y las decisiones manuales, y escribe
        una fila por cada persona en la puerta, calificada o no. Desde ese momento la lista no
        cambia: quien llegue después no entra, y las decisiones manuales se rechazan con
        `RAFFLE_NOT_DRAFT`. Las rondas y los premios se pueden seguir agregando.

        No lleva cuerpo. Responde con el sorteo, igual que su lectura, ya en `in_progress` y con los
        `warnings` en vigor al congelar.

        Las comprobaciones van en este orden y responde la primera que falla: el evento cerrado o
        cancelado (`EVENT_NOT_EDITABLE`), el sorteo que ya no está en `draft` (`RAFFLE_NOT_DRAFT`,
        también para el segundo de dos arranques simultáneos; `RAFFLE_COMPLETED` si ya se
        completó), la puerta que no contesta (`ATTENDANCE_UNAVAILABLE`) o pasa de 25.000 asistentes (`ATTENDANCE_TOO_MANY_ROWS`), el CRM
        que no contesta (`CRM_UNAVAILABLE`; a diferencia de las lecturas, el arranque no sigue sin
        él) y el bombo vacío (`RAFFLE_NO_PARTICIPANTS`), sea porque nadie llegó, porque todos
        quedaron fuera o porque la lista de DO Travel Awards no coincide con nadie. Un arranque
        rechazado no escribe nada.

        Nombra a los patrocinadores de los premios como la lectura del sorteo, y lleva la misma
        cabecera `X-Crm-Directory-Status`.
        """;

    public const string CompleteRaffleSummary =
        "Completa el sorteo y genera su acta notarial.";

    public const string CompleteRaffleDescription =
        """
        Cierra la noche: pasa el sorteo de `in_progress` a `completed`, genera el acta en PDF con lo
        que ocurrió, la guarda como archivo y la enlaza como versión 1. Todo o nada: si algo falla, el
        sorteo sigue en curso y se puede volver a intentar. Los premios sin sortear no lo impiden; el
        acta dice cuántos quedaron. No lleva cuerpo. Responde con el sorteo, igual que su lectura, ya
        con `completedAt`, `completedById` y el acta en `notarialRecords`. Los bytes del acta se leen
        de `GET /v1/files/{id}/content`.

        Después de completar le escribe una vez a cada ganador vivo, en el idioma del evento o en
        español. Cada sorteo de premio de la respuesta ya dice cómo terminó su correo en
        `winnerNotificationStatus` (`sent`, `no_recipient`, `failed`) y `winnerNotificationId`. Un
        correo que falla nunca hace fallar el cierre.

        Las comprobaciones van en este orden y responde la primera que falla: el sorteo en `draft`
        (`RAFFLE_NOT_IN_PROGRESS`) o ya completado (`RAFFLE_COMPLETED`), alguna de las seis autoridades
        en blanco (`RAFFLE_AUTHORITIES_REQUIRED`), las instrucciones de retiro en blanco
        (`RAFFLE_PICKUP_INSTRUCTIONS_REQUIRED`), un patrocinador cuya empresa no se puede nombrar de
        ninguna forma, sin copia del directorio y con el CRM caído (`CRM_UNAVAILABLE`, 503), y el
        almacenamiento de archivos caído (`FILE_STORE_UNAVAILABLE`, 503). Un evento cerrado o
        cancelado no lo impide. El acta imprime solo el nombre de la empresa patrocinadora, nunca el
        contacto. La respuesta nombra a los patrocinadores como la lectura del sorteo, con la misma
        cabecera `X-Crm-Directory-Status`.
        """;

    public const string ReissueRaffleSummary =
        "Corrige las autoridades de un sorteo completado y reemite su acta.";

    public const string ReissueRaffleDescription =
        """
        Para un dato mal escrito del notario o de un testigo, visto antes de firmar. El cuerpo trae las
        seis autoridades, todas obligatorias y que se guardan recortadas; es lo único que cambia. Las
        guarda en el sorteo y genera, guarda y enlaza la versión siguiente del acta, que en su primera
        página dice a cuál reemplaza y cuándo se generó esa. Las versiones anteriores no se borran. Todo
        o nada: si algo falla, las autoridades no cambian y la versión vigente sigue siéndolo. Cada
        llamada escribe una versión nueva, aunque los datos no cambien. No envía ningún correo. Responde
        con el sorteo, igual que su lectura, con la nueva versión primera en `notarialRecords`.

        Las comprobaciones van en este orden y responde la primera que falla: el cuerpo (400), el
        evento y su sorteo (404), el sorteo sin completar (`RAFFLE_NOT_COMPLETED`), un patrocinador que
        no se puede nombrar (`CRM_UNAVAILABLE`, 503, como al completar) y el almacenamiento de archivos
        caído (`FILE_STORE_UNAVAILABLE`, 503). Un evento cerrado o cancelado no lo impide. La respuesta
        nombra a los patrocinadores como la lectura del sorteo, con la misma cabecera
        `X-Crm-Directory-Status`.
        """;

    public const string ReopenRaffleSummary =
        "Devuelve a borrador un sorteo arrancado antes de tiempo.";

    public const string ReopenRaffleDescription =
        """
        Para un `start` apretado demasiado temprano. Pasa el sorteo de `in_progress` a `draft`, borra
        la lista congelada de la puerta y olvida quién lo arrancó, cuándo y con qué lista de DO Travel
        Awards. Las decisiones manuales se conservan para el próximo arranque. No lleva cuerpo y no
        deja rastro. Responde con el sorteo, igual que su lectura, patrocinadores y cabecera
        `X-Crm-Directory-Status` incluidos.

        Las comprobaciones van en este orden y responde la primera que falla: el evento cerrado o
        cancelado (`EVENT_NOT_EDITABLE`), el sorteo en `draft` (`RAFFLE_NOT_IN_PROGRESS`) o
        completado (`RAFFLE_COMPLETED`), y el sorteo con cualquier premio sorteado, aunque se haya
        anulado (`RAFFLE_HAS_DRAWS`). Una reapertura que compite con el primer sorteo se ordena con
        él: entra antes, o espera y responde `RAFFLE_HAS_DRAWS`.
        """;

    public const string DrawPrizeSummary =
        "Sortea el premio y devuelve al ganador.";

    public const string DrawPrizeDescription =
        """
        El servidor elige al ganador entre quienes pueden ganar este premio en este momento —la misma
        lista que devuelve `eligible`, recalculada bajo un bloqueo del sorteo— y escribe el sorteo con
        su código de retiro. No lleva cuerpo, y nada lo elige el cliente.

        Devuelve el premio entero, igual que en la lectura del sorteo: `state` en `drawn` y sus
        `draws` del más nuevo al más viejo, con el ganador primero. `eligiblePoolSize` es de
        cuántos salió, `redemptionCode` es el código de retiro del ganador, y `onePrizePerPerson` y
        `onePrizePerCompany` son las reglas del sorteo en ese momento, que quedan guardadas con él.
        El correo no viaja en la respuesta.

        Las comprobaciones van en este orden y responde la primera que falla: el sorteo en `draft`
        (`RAFFLE_NOT_IN_PROGRESS`) o completado (`RAFFLE_COMPLETED`); el premio con un ganador vivo
        (`PRIZE_ALREADY_DRAWN`, que trae ese sorteo en `draw`, así que un reintento tras una respuesta
        perdida recupera al ganador sin otra llamada); y nadie que pueda ganarlo
        (`RAFFLE_POOL_EXHAUSTED`, con `participants`, `excludedByVoid`, `excludedByPerson` y
        `excludedByCompany`, que suman `participants`). Un sorteo rechazado no escribe nada.

        Dos sorteos simultáneos en el mismo sorteo se ordenan: el segundo espera y contesta como si
        hubiera llegado solo después del primero, así que nunca responde `CONCURRENCY_CONFLICT`: ante
        cualquier conflicto, el cliente vuelve a leer el premio. El estado del evento no lo frena. No
        se reintenta automáticamente.
        """;

    public const string VoidPrizeDrawSummary =
        "Anula el ganador vivo del premio.";

    public const string VoidPrizeDrawDescription =
        """
        `code` es uno de `absent`, `declined`, `ineligible` u `other`; `note` es opcional, de hasta
        500 caracteres, y obligatoria con `other`. Se recorta, y una nota en blanco cuenta como
        ausente. Un motivo fuera de los cuatro, o `other` sin nota, es un `400`.

        El sorteo anulado no se borra: guarda el motivo, la nota, cuándo y quién, y queda en `draws`
        con su `void`. El premio vuelve a `undrawn` y se puede sortear otra vez, que es otra llamada:
        anular nunca sortea. Quien fue anulado, con cualquier motivo, no vuelve a salir en ningún
        premio del sorteo. Devuelve el premio entero, igual que el sorteo.

        Las comprobaciones van en este orden y responde la primera que falla: el sorteo en `draft`
        (`RAFFLE_NOT_IN_PROGRESS`) o completado (`RAFFLE_COMPLETED`), el premio sin ganador vivo
        (`PRIZE_NOT_DRAWN`) y el premio ya entregado (`PRIZE_ALREADY_REDEEMED`: lo único que lo devuelve
        es revertir la entrega). Se ordena con los sorteos y las entregas bajo el mismo bloqueo, así que nunca responde
        `CONCURRENCY_CONFLICT`. El estado del evento no la frena. **No se reintenta
        automáticamente**: la ruta nombra el premio y no el sorteo, así que un reintento que llega
        después de un nuevo sorteo anularía al nuevo ganador. Ante una respuesta perdida, se vuelve a
        leer el premio.
        """;

    public const string HandOverPrizeSummary =
        "Registra la entrega del premio a quien lo retira, con su firma.";

    public const string HandOverPrizeDescription =
        """
        Un formulario `multipart/form-data` con `receiverName` (hasta 200 caracteres),
        `receiverIdentityDocument` (hasta 100, texto libre), `receiverIsWinner` (`true` o `false`, sin
        distinguir mayúsculas, lo declara el operador) y `signature`, un PNG de hasta 1 MB tomado del
        panel de firma. Los cuatro son obligatorios —el documento también cuando retira el ganador— y
        un campo que falta, sobra de largo o no se entiende es un `400`. La firma se reconoce por sus
        bytes: otra cosa que un PNG es `FILE_TYPE_NOT_ALLOWED`, un archivo que dice ser lo que no es
        `FILE_TYPE_MISMATCH` y uno de más de 1 MB `FILE_TOO_LARGE`.

        La entrega queda contra el sorteo vivo del premio, que pasa a `redeemed`. Devuelve el premio
        entero, igual que la lectura del sorteo: cada sorteo con su `redemptionCode`, su `status` y sus
        `handovers`, vivas y revertidas, de la más nueva a la más vieja.

        Después de la firma, las comprobaciones van en este orden bajo el bloqueo del sorteo y responde
        la primera que falla: el sorteo en `draft` (`RAFFLE_NOT_IN_PROGRESS`), el premio sin ganador vivo
        (`PRIZE_NOT_DRAWN`) y el premio ya entregado (`PRIZE_ALREADY_REDEEMED`). Una entrega rechazada
        no deja ni la fila ni la firma. Con el almacenamiento caído es `FILE_STORE_UNAVAILABLE` y no se
        escribe nada. Se admite con el sorteo completado y cualquiera sea el estado del evento: los
        ganadores retiran días después. No se reintenta automáticamente.
        """;

    public const string ReversePrizeHandoverSummary =
        "Revierte la entrega viva del premio.";

    public const string ReversePrizeHandoverDescription =
        """
        `reason` es obligatorio, de hasta 500 caracteres, y se recorta. La entrega no se borra: guarda
        cuándo, quién y por qué, y su firma sigue a mano por su id. El premio vuelve a `drawn` y se
        puede entregar otra vez, que es otra llamada. Devuelve el premio entero, igual que la entrega.

        Responde `RAFFLE_NOT_IN_PROGRESS` con el sorteo en `draft` y `PRIZE_NOT_REDEEMED` si el premio no
        tiene una entrega viva. Se admite con el sorteo completado y cualquiera sea el estado del evento.
        No se reintenta automáticamente: un reintento que llega después de una nueva entrega la
        revertiría.
        """;

    public const string FindPrizeDrawByCodeSummary =
        "Busca el sorteo de premio de un código de retiro.";

    public const string FindPrizeDrawByCodeDescription =
        """
        Busca en todos los sorteos, sin saber de qué evento viene el código. Lo escrito se normaliza
        antes de comparar: sin distinguir mayúsculas, sin guiones ni espacios, la `O` se lee como `0` y
        la `I` o la `L` como `1`, y el `RD` es optativo, así que `rd7k3mq9xa` y `7K3MQ9XA` encuentran
        `RD-7K3M-Q9XA`. Después la comparación es exacta: no hay búsqueda parcial ni por nombre.

        `status` es `drawn`, `redeemed` o `voided`. Un código anulado se responde, con `void` (motivo y
        cuándo), para que el mostrador pueda decir que ese sorteo se anuló; uno entregado trae
        `handover` con quién lo retiró y cuándo. Una entrega revertida no aparece, y el sorteo vuelve a
        leerse `drawn`. Lo que no llega a ser un código y el código que no existe son los dos
        `REDEMPTION_CODE_NOT_FOUND`.

        Solo lee: nunca entrega el premio. Entregarlo es `POST …/prizes/{prizeId}/handover`, con quien
        retira, su documento y su firma.
        """;

    public const string GetEligibleSummary =
        "Devuelve quién puede ganar el premio en este momento.";

    public const string GetEligibleDescription =
        """
        Los participantes del bombo que pueden ganar este premio con las reglas del sorteo: los
        calificados, menos quien tiene un sorteo anulado en cualquier premio del sorteo, quien ya
        tiene un premio vivo si rige uno por persona y quien
        comparte empresa con un premio vivo si rige uno por empresa. Es la misma función que usa el
        sorteo, así que `count` es el `eligiblePoolSize` del próximo sorteo si nada cambia entre
        medio.

        Solo nombre y empresa, ordenados por id: la lista es para un proyector. Solo con el sorteo en
        `in_progress` (`RAFFLE_NOT_IN_PROGRESS` en cualquier otro estado) y con el premio sin ganador
        vivo (`PRIZE_ALREADY_DRAWN`, con ese sorteo en `draw`).
        """;

    /// <summary>
    /// Qué dice la cabecera del CRM en las dos lecturas que calculan el bombo. No es la escalera de
    /// los directorios: acá no hay copia ni nombres, solo la lista negra preguntada en vivo.
    /// </summary>
    private const string CrmMailabilityStatus =
        """
        La lista negra del CRM se consulta en vivo en cada lectura, sin copia. La cabecera
        `X-Crm-Directory-Status` (y `X-Opt-Directory-Status`, con el mismo valor) dice `ok` si el
        CRM contestó y `unavailable` si no; en ese caso el bombo se calcula igual, sin ese paso, y
        arrancar queda bloqueado con `crm_unavailable`. Sin ningún contacto que consultar, o sin
        lista de la puerta, no sale cabecera.
        """;

    public const string SetOverrideSummary =
        "Mete a un asistente al bombo o lo saca, a mano.";

    public const string SetOverrideDescription =
        """
        Escribe la decisión manual sobre el asistente de ese `email`, o reemplaza la que hubiera:
        la decisión, la nota, quién y cuándo. El correo se compara sin espacios alrededor y en
        minúsculas. Una nota en blanco se guarda como nula.

        Solo mientras el sorteo está en `draft` (`RAFFLE_NOT_DRAFT` en curso, `RAFFLE_COMPLETED` si
        ya se completó) y el evento admite cambios.
        La puerta se lee en vivo, no de la copia de las lecturas: si no contesta,
        `ATTENDANCE_UNAVAILABLE`; si la persona no está, `PARTICIPANT_NOT_CHECKED_IN`.

        Una inclusión se rechaza, sin escribir nada, para quien llegó sin invitación
        (`PARTICIPANT_WALK_IN`), no tiene contacto en el CRM o el CRM ya no lo devuelve
        (`PARTICIPANT_CONTACT_UNRESOLVED`), está suspendido (`PARTICIPANT_SUSPENDED`) o vetado
        (`PARTICIPANT_DO_NOT_INVITE`). El CRM se consulta en vivo por ese contacto: si no contesta,
        `CRM_UNAVAILABLE`.

        Una exclusión no consulta al CRM: nunca mete a nadie. Excluir a quien ya está fuera por
        un motivo de más arriba se acepta y no cambia nada.
        """;

    public const string BulkExcludeSummary =
        "Saca del bombo a un grupo de personas de una vez.";

    public const string BulkExcludeDescription =
        """
        Exclusión en lote, con exactamente una fuente: `sourceEventId` excluye a los ganadores vivos
        del sorteo de otro evento de la cuenta (un ganador anulado no cuenta), e `identifiers` a una
        lista de hasta 5000 correos o IDs de empleado (con `@` es un correo; si no, un ID). Una
        persona se reconoce si coincide su correo o su ID. Sin `note`, el servicio pone de dónde
        salió la exclusión.

        Quien no está en el padrón o en la puerta va a `notInRoster`; un valor que no es correo ni
        ID de nadie, a `invalid`; un ganador sin correo ni ID, a `unidentifiable` con su nombre.
        Nada de eso falla. Todo se escribe en una transacción.

        Ninguna fuente o las dos: `RAFFLE_BULK_EXCLUDE_SOURCE_REQUIRED`; más de 5000:
        `RAFFLE_BULK_EXCLUDE_TOO_MANY`; el mismo evento: `RAFFLE_BULK_EXCLUDE_SAME_EVENT`. Una
        fuente sin sorteo o de otra cuenta: `RAFFLE_NOT_FOUND`. Solo mientras el sorteo está en
        `draft` (`RAFFLE_NOT_DRAFT`) y el evento admite cambios.
        """;

    public const string RemoveOverrideSummary =
        "Retira la decisión manual sobre un asistente.";

    public const string RemoveOverrideDescription =
        """
        Tiene éxito haya o no una decisión sobre ese `email`. No consulta la puerta, así que
        se puede retirar la de alguien cuyo registro se revirtió. Solo mientras el sorteo está en
        `draft` y el evento admite cambios.
        """;

    public const string UpdateRaffleSummary =
        "Reemplaza la configuración completa del sorteo.";

    public const string UpdateRaffleDescription =
        """
        El cuerpo es la representación editable **completa**: los tres ajustes de pantalla, la
        confirmación de presencia, las dos reglas, las seis autoridades y las instrucciones de retiro
        (`pickupInstructions`, hasta 1000 caracteres). Una autoridad o unas instrucciones omitidas se
        **borran**, no se conservan; los otros seis campos son obligatorios. Quien edite solo una
        parte tiene que mandar el resto tal como lo leyó.

        Las rondas y los premios no viajan acá: tienen sus propios endpoints.

        Un evento cerrado o cancelado ya no admite cambios, y un sorteo completado tampoco
        (`RAFFLE_COMPLETED`). Con el sorteo en curso, las dos reglas también se pueden cambiar: el
        cambio rige desde el próximo sorteo y no toca los que ya salieron, que guardan las reglas bajo
        las que se sortearon.
        """;

    public const string SetCertificateSignersSummary =
        "Reemplaza los firmantes que el certificado de premio propone por omisión.";

    public const string SetCertificateSignersDescription =
        """
        El cuerpo trae `signers`, la lista **completa** y en el orden de las líneas de firma: a lo sumo
        dos, cada uno con `name` y `title` obligatorios, de hasta 100 caracteres, que se guardan
        recortados. Una lista vacía los borra. El sorteo los devuelve en `certificateSigners`.

        Son el punto de partida del certificado, que se sigue editando antes de descargarlo; lo que se
        imprime no se guarda. Como no atestiguan nada, se cambian en cualquier estado del sorteo,
        completado incluido. Un evento cerrado o cancelado ya no admite cambios.
        """;

    public const string CreateRoundSummary =
        "Agrega una ronda al final del sorteo.";

    public const string CreateRoundDescription =
        """
        El número de la ronda no viaja en el cuerpo: lo asigna el servicio, y es el siguiente al
        de la última ronda. Las rondas corren en ese orden, de uno a tantas como haya y sin huecos,
        y no se reordenan.

        Un evento cerrado o cancelado ya no admite cambios, y un sorteo completado tampoco. Un
        sorteo en curso sí.
        """;

    public const string UpdateRoundSummary =
        "Reemplaza el nombre y la descripción de una ronda.";

    public const string UpdateRoundDescription =
        """
        El cuerpo es la representación editable **completa**: una descripción omitida se
        **borra**. El número no se edita. Una ronda con algún premio ya sorteado, aunque se haya
        anulado, no se edita (`PRIZE_HAS_DRAWS`): su nombre es un encabezado del acta.
        """;

    public const string DeleteRoundSummary =
        "Elimina una ronda vacía y renumera las siguientes.";

    public const string DeleteRoundDescription =
        """
        Una ronda con premios no se elimina (`ROUND_NOT_EMPTY`): primero hay que mover sus premios
        a otra ronda o eliminarlos. Al eliminarla, cada ronda que venía detrás baja un número, de
        modo que siguen siendo de uno a n sin huecos.
        """;

    public const string CreatePrizesSummary =
        "Agrega uno o más premios idénticos a una ronda.";

    public const string CreatePrizesDescription =
        """
        `quantity` (de 1 a 50) es cuántos premios crear, no una propiedad del premio: cinco bolsas
        son cinco premios, con cinco identificadores y cinco ganadores. No se guarda, y no existe
        al editar. La respuesta es la lista de premios creados.

        `value` es texto libre que se imprime tal como se escribió (`RD$8,000`), no un monto.

        `sponsorCompanyId` es la empresa patrocinadora, del CRM, y `sponsorContactId` uno de sus
        contactos; los dos son opcionales, el contacto solo con la empresa (400 si no), y todos los
        premios del alta los llevan. Se comprueban en vivo contra el CRM: una empresa que nunca existió
        es `COMPANY_NOT_AVAILABLE` (una cerrada pasa), un contacto que no existe o no tiene hoy una
        vinculación con esa empresa es `COMPANY_CONTACT_NOT_AVAILABLE`, ambos 409, y un CRM que no
        contesta es `CRM_UNAVAILABLE` (503). La respuesta trae `sponsor` con los ids y los nombres
        nulos: los nombra la lectura del sorteo.
        """;

    public const string UpdatePrizeSummary =
        "Reemplaza un premio completo, y puede moverlo de ronda.";

    public const string UpdatePrizeDescription =
        """
        El cuerpo es la representación editable **completa**: la descripción o el valor omitidos
        se **borran**. `roundId` es obligatorio; si es otro que el actual, el
        premio se mueve a esa ronda, que tiene que ser del mismo sorteo (`ROUND_NOT_FOUND` si no).
        Un premio ya sorteado, aunque se haya anulado, no se edita (`PRIZE_HAS_DRAWS`).

        `sponsorCompanyId` y `sponsorContactId` siguen la misma regla: omitirlos quita el
        patrocinador. Se comprueban como al crear (`COMPANY_NOT_AVAILABLE`,
        `COMPANY_CONTACT_NOT_AVAILABLE`, `CRM_UNAVAILABLE`). Quitar la empresa o cambiarla por otra
        desenlaza el logo del patrocinador; el archivo se conserva.
        """;

    public const string DeletePrizeSummary =
        "Elimina un premio.";

    public const string DeletePrizeDescription =
        """
        Un premio ya sorteado, aunque se haya anulado, no se elimina (`PRIZE_HAS_DRAWS`): el acta lo
        nombra.
        """;

    public const string UploadPrizeSponsorLogoSummary =
        "Sube el logo del patrocinador de un premio, y reemplaza el que hubiera.";

    public const string UploadPrizeSponsorLogoDescription =
        """
        Multipart, con el archivo en el campo `file`. Solo imágenes: el tipo sale de los bytes, no de
        lo que se declara, y un PDF es `FILE_TYPE_NOT_ALLOWED`. Un premio tiene a lo sumo un logo:
        subir otro reemplaza el anterior, que queda desenlazado con su archivo intacto.

        El premio tiene que tener patrocinador (`PRIZE_NOT_SPONSORED`) y no haberse sorteado, aunque
        se haya anulado (`PRIZE_HAS_DRAWS`). Todo se comprueba antes de guardar un byte, y un
        almacenamiento que no contesta es `FILE_STORE_UNAVAILABLE` sin escribir nada. La
        respuesta es el logo: `fileId`, con el que se piden los bytes a
        `GET /v1/files/{fileId}/content`, `fileName` y `missing`.
        """;

    public const string RemovePrizeSponsorLogoSummary =
        "Quita el logo del patrocinador de un premio.";

    public const string RemovePrizeSponsorLogoDescription =
        """
        Desenlaza el logo, tenga o no uno el premio; el archivo no se borra. Un premio ya sorteado,
        aunque se haya anulado, no se toca (`PRIZE_HAS_DRAWS`).
        """;

    public const string ReinstateAttendeeSummary =
        "Reactiva a un participante anulado para que vuelva al bombo del sorteo.";

    public const string ReinstateAttendeeDescription =
        """
        Devuelve al bombo de los premios siguientes a un participante que fue anulado durante este
        sorteo (por ejemplo, si regresó al salón tras haberse vencido el tiempo de confirmación).
        Solo está disponible mientras el sorteo está en curso (`in_progress`) y devuelve el sorteo
        actualizado con `reinstatedAttendeeIds`.

        Si el participante no pertenece al sorteo congelado o no tiene ninguna anulación registrada,
        responde `409 RAFFLE_ATTENDEE_NOT_VOIDED`. Si vuelve a ser sorteado y anulado en otro premio,
        vuelve a quedar excluido del bombo.
        """;
}
