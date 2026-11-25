import type {
  ApiErrorCode,
  EventoStatus,
  EventoVisibility,
  MontajeEvento,
  EventoType,
  Idioma,
  ParticipantSendStatus,
  PrizeDrawStatus,
  PrizeDrawVoidCode,
  PrizeState,
  WinnerNotificationStatus,
  AttendanceStatus,
  AttendeeOverrideDecision,
  RaffleBlockingReason,
  RaffleExclusionReason,
  RaffleState,
  RaffleWarning,
  SuppressionReason,
} from '@/api/types'

/** Códigos máquina (inglés) → etiquetas de UI (español). */

/**
 * Un 403 que llega sin código: lo pone la autorización del servicio antes de llegar al
 * endpoint, así que no trae cuerpo. El mock lo usa tal cual para decir lo mismo.
 */
export const FORBIDDEN_MESSAGE = 'No tiene permiso para realizar esta acción.'

export const suppressionReasonLabel: Record<SuppressionReason, string> = {
  no_email: 'Sin dirección de correo',
  opted_out: 'Dado de baja por el usuario',
  suspended: 'Contacto suspendido',
  do_not_invite: 'Marcado como No-Invitar',
}

/** Por qué la selección dejó a alguien fuera de una campaña (spec convocatorias 07 §6.4). */
export const campaignExclusionReasonLabel: Record<string, string> = {
  ...suppressionReasonLabel,
  contact_unavailable: 'Contacto no disponible en el CRM',
}

/**
 * Las plantillas de campaña, con el nombre de tipo que ya usa la pantalla. `direct_message` no se
 * elige: es la de una campaña sin evento.
 */
export const campaignTemplateLabel: Record<string, string> = {
  save_the_date: 'Save the Date',
  registration_invite: 'Apertura de Registro',
  direct_message: 'Mensaje directo',
}

export const participantSendStatusLabel: Record<ParticipantSendStatus, string> = {
  handed_off: 'Enviado',
  pending: 'Pendiente',
  failed: 'Fallido',
  suppressed: 'Suprimido',
  none: 'Sin envío',
}

export const eventoStatusLabel: Record<EventoStatus, string> = {
  planning: 'Planificación',
  in_progress: 'En progreso',
  closed: 'Cerrado',
  cancelled: 'Cancelado',
}

export const raffleStateLabel: Record<RaffleState, string> = {
  draft: 'Borrador',
  in_progress: 'En progreso',
  completed: 'Completado',
}

export const raffleBlockingReasonLabel: Record<RaffleBlockingReason, string> = {
  no_qualified_attendees: 'Nadie registrado en la puerta cumple los requisitos del sorteo.',
  attendance_unavailable: 'No se pudo obtener la lista de asistentes registrados en puerta.',
  attendance_too_many_rows:
    'El evento tiene más asistentes registrados en puerta de los que admite un sorteo.',
  crm_unavailable:
    'No se pudo consultar el CRM: la lista puede incluir a alguien que no se debe invitar.',
}

export const raffleWarningLabel: Record<RaffleWarning, string> = {
  awards_list_not_in_force:
    'No hay una lista de DO Travel Awards en vigor, así que ese paso de la calificación se omite.',
  attendance_copy_stale:
    'La lista de asistentes es una copia anterior: la última lectura de la puerta falló.',
  no_prizes: 'El sorteo todavía no tiene premios.',
}

export const raffleExclusionReasonLabel: Record<RaffleExclusionReason, string> = {
  walk_in: 'Se registró sin invitación.',
  contact_unresolved: 'No tiene contacto en el CRM.',
  suspended: 'Está suspendido en el CRM.',
  do_not_invite: 'El CRM lo tiene marcado para no invitar.',
  manually_excluded: 'Se excluyó a mano.',
  not_awards_member: 'No está en la lista de DO Travel Awards.',
  dominican_domain: 'Su correo es de un dominio .do.',
  won_in_linked_raffle: 'Ganó en un sorteo vinculado.',
}

export const attendeeOverrideDecisionLabel: Record<AttendeeOverrideDecision, string> = {
  included: 'Incluido a mano',
  excluded: 'Excluido a mano',
}

export const attendanceStatusLabel: Record<AttendanceStatus, string> = {
  ok: 'Lista de la puerta al día',
  stale: 'Copia anterior de la lista de la puerta',
  unavailable: 'Lista de la puerta no disponible',
}

export const prizeStateLabel: Record<PrizeState, string> = {
  undrawn: 'Por sortear',
  drawn: 'Pendiente de entrega',
  redeemed: 'Entregado',
}

/** En qué está el código de un sorteo de premio (spec raffles/03 §2.4). */
export const prizeDrawStatusLabel: Record<PrizeDrawStatus, string> = {
  drawn: 'Pendiente de entrega',
  redeemed: 'Entregado',
  voided: 'Anulado',
}

/** Por qué se anuló un ganador (spec raffles/02 §3). */
export const prizeDrawVoidCodeLabel: Record<PrizeDrawVoidCode, string> = {
  absent: 'No está en la sala',
  declined: 'Rechazó el premio',
  ineligible: 'No podía ganar',
  other: 'Otro motivo',
}

/**
 * El título de la tarjeta del escáner ante un rechazo con código. El cuerpo es el mensaje del
 * código (`apiErrorCodeLabel`); el título solo dice de un vistazo qué pasó (spec convocatorias/10).
 */
export const scannerRefusalTitle: Partial<Record<ApiErrorCode, string>> = {
  CHECK_IN_WRONG_EVENT: 'Entrada de otro evento',
  CHECK_IN_CODE_NOT_FOUND: 'Entrada no encontrada',
  REDEMPTION_CODE_NOT_FOUND: 'Código de premio inexistente',
}

/** El título del escáner para el código de un rechazo, o `undefined` si no tiene uno propio. */
export function scannerRefusalTitleFor(code: string | undefined): string | undefined {
  if (!code) return undefined
  return Object.hasOwn(scannerRefusalTitle, code)
    ? scannerRefusalTitle[code as ApiErrorCode]
    : undefined
}

/** Cómo terminó el correo al ganador, que sale al completar el sorteo (spec raffles/03 §5). */
export const winnerNotificationStatusLabel: Record<WinnerNotificationStatus, string> = {
  sent: 'Correo enviado',
  failed: 'El correo falló',
  no_recipient: 'Sin correo',
}

export const eventoTypeLabel: Record<EventoType, string> = {
  Corporate: 'Fiesta corporativa',
  Christmas: 'Fiesta de Navidad',
  Wedding: 'Boda',
  Birthday: 'Cumpleaños',
  Concert: 'Concierto',
  Graduation: 'Graduación',
  Conference: 'Conferencia',
  Other: 'Otro',
}

/** Los tipos en el orden en que se ofrecen en los selectores. */
export const EVENTO_TYPES = Object.keys(eventoTypeLabel) as EventoType[]

/** Lo que hay que hacer para que una categoría deje de bloquear el cierre del evento. */
export const idiomaLabel: Record<Idioma, string> = {
  es: 'Español',
  en: 'Inglés',
  fr: 'Francés',
  de: 'Alemán',
  pt: 'Portugués',
  it: 'Italiano',
}

/** El montaje del salón, como lo elige quien crea el evento. */
export const montajeLabel: Record<MontajeEvento, string> = {
  Banquet: 'Banquete / Cena (mesas redondas)',
  Auditorium: 'Auditorio / Teatro (filas)',
  Conference: 'Conferencia (mesa principal)',
  Cocktail: 'Cóctel (de pie)',
  UShape: 'Herradura (forma U)',
  Classroom: 'Tipo escuela (escritorios)',
}

export const eventoVisibilityLabel: Record<EventoVisibility, string> = {
  public: 'Público',
  private: 'Privado',
}

/**
 * Los tipos de envío que hoy declaran events-service y convocatorias-service. La lista es
 * abierta: un tipo nuevo se muestra tal cual llega (`notificationTypeLabelFor`) hasta que se
 * le ponga nombre aquí.
 */
export const notificationTypeLabel: Record<string, string> = {
  'events.quote.submitted': 'Cotización por aprobar',
  'events.quote.approved': 'Cotización aprobada',
  'events.quote.rejected': 'Cotización rechazada',
  'events.quote.withdrawn': 'Cotización retirada',
  'convocatorias.campaign.invite': 'Invitación de campaña',
  'convocatorias.registration.confirmation': 'Confirmación de registro',
  'convocatorias.event.cancelled': 'Aviso de cancelación',
}

export function notificationTypeLabelFor(type: string): string {
  return Object.hasOwn(notificationTypeLabel, type) ? notificationTypeLabel[type] : type
}

/**
 * Rechazos del servicio, por su código máquina. El servicio ya describe el fallo en
 * prosa, pero esa prosa es suya y puede cambiar; aquí la aplicación decide qué le dice
 * a quien está frente a la pantalla, y sobre todo qué hacer a continuación.
 *
 * Los `TOTAL_COST_*` no nombran la categoría que bloquea —el servicio sí lo hace en su
 * mensaje— porque la pantalla de cierre ya la señala categoría por categoría con
 * `motivoBloqueoLabel`.
 */
export const apiErrorCodeLabel: Record<ApiErrorCode, string> = {
  PLAN_PARTICIPANT_LIMIT:
    'El padrón pasaría el tope de participantes de tu plan. Quita filas o sube de plan.',
  PLAN_ACTIVE_RAFFLE_LIMIT:
    'Ya tienes todos los sorteos activos que permite tu plan. Completa uno o sube de plan.',
  PLAN_MONTHLY_RAFFLE_LIMIT:
    'Ya creaste el sorteo de este mes del plan Gratis. El cupo vuelve el día 1, o sube de plan.',
  PLAN_ROUND_LIMIT: 'Tu plan no permite más rondas en este sorteo. Sube de plan para agregar más.',
  PLAN_PRIZE_LIMIT: 'Tu plan no permite más premios en este sorteo. Sube de plan para agregar más.',
  PLAN_USER_LIMIT: 'Tu plan no permite más usuarios. Sube de plan para invitar a alguien más.',
  PLAN_FEATURE_NOT_INCLUDED: 'Esta función no está incluida en tu plan.',
  PLAN_REQUEST_PENDING:
    'Ya tienes una solicitud de plan esperando la confirmación de la transferencia.',
  ACCOUNT_NOT_ACTIVE: 'La cuenta no está activa. Revisa el correo de activación o escríbenos.',
  ACCOUNT_EMAIL_TAKEN: 'Ese correo ya tiene una cuenta. Entra con él o recupera la contraseña.',
  ACTIVATION_TOKEN_INVALID:
    'El enlace de activación venció o ya se usó. Pide uno nuevo con tu correo.',
  OPT_OFFICE_NOT_AVAILABLE:
    'La Oficina de Promoción Turística (OPT) seleccionada ya no está disponible o fue deshabilitada. Elija otra.',
  OPT_CONTACT_NOT_AVAILABLE:
    'El contacto asignado no pertenece a esa OPT o fue desactivado. Elija otro contacto.',
  CRM_UNAVAILABLE:
    'El directorio de OPTs (CRM) no está disponible en este momento. Intente nuevamente en unos minutos.',
  EVENT_NOT_STARTABLE: 'Solo un evento en planificación puede iniciarse.',
  EVENT_NOT_CANCELLABLE: 'El evento ya está cerrado o cancelado; no puede cancelarse.',
  EVENT_NOT_EDITABLE: 'El evento ya está cerrado o cancelado y no admite cambios.',
  EVENT_LANGUAGE_REQUIRED: 'El evento requiere un idioma.',
  EVENT_NOT_CLOSABLE: 'Solo un evento en progreso puede cerrarse.',
  TIME_ZONE_UNKNOWN: 'La zona horaria no es válida. Elija una de la lista.',
  TOTAL_COST_NO_QUOTES:
    'No se puede cerrar el evento: no tiene ninguna cotización aprobada de la cual salga el costo total.',
  TOTAL_COST_QUOTE_DRAFT:
    'Hay una categoría con su cotización en borrador: sométala a aprobación o descártela antes de cerrar.',
  TOTAL_COST_QUOTE_UNDECIDED:
    'Hay una categoría sometida que todavía espera la decisión de Dirección.',
  TOTAL_COST_WINNER_UNKNOWN:
    'No se pudo identificar el candidato ganador de una categoría aprobada.',
  TOTAL_COST_FINAL_AMOUNT_AMBIGUOUS:
    'Hay más de un monto contratado registrado en una misma categoría.',
  TOTAL_COST_MISSING_FINAL_AMOUNT: 'Falta registrar el monto contratado de una categoría aprobada.',
  TOTAL_COST_FINAL_AMOUNT_PENDING:
    'Una categoría tiene un monto contratado pendiente de aprobación.',
  TOTAL_COST_MIXED_CURRENCIES:
    'Las cotizaciones aprobadas mezclan monedas (USD y EUR) y no pueden sumarse sin conversión.',
  MAX_CANDIDATES:
    'La categoría ya tiene el máximo de 3 proveedores. Elimine uno para agregar otro; los presupuestos de un mismo proveedor no cuentan aparte.',
  CANDIDATES_OUT_OF_RANGE:
    'Para someter hacen falta entre 1 y 3 candidatos. Agregue al menos uno antes de someter.',
  NO_RECOMMENDED: 'Marque exactamente un candidato como recomendado antes de someter.',
  EXCEPTION_REQUIRED:
    'Con menos de 3 candidatos hay que indicar la razón de excepción y justificarla.',
  // El servicio nombra el verbo (aprobar, rechazar) en su mensaje; acá no hace falta,
  // porque quien lo lee acaba de pulsar ese botón.
  SELF_DECISION:
    'No puede decidir una cotización que usted mismo sometió. La decisión le toca a otra persona de Dirección.',
  REASON_REQUIRED: 'Rechazar exige una razón: es lo único que le dice a la OPT qué corregir.',
  WINNER_NOT_IN_QUOTE:
    'El candidato ganador ya no pertenece a esta cotización. Recargue y vuelva a elegir.',
  QUOTE_NOT_DRAFT:
    'La cotización ya fue sometida y no admite cambios. Retírela para volver a editarla.',
  QUOTE_NOT_SUBMITTED:
    'La cotización no está sometida; no hay ninguna decisión pendiente. Recargue para ver su estado actual.',
  QUOTE_APPROVED: 'Una cotización aprobada no puede descartarse.',
  FINAL_AMOUNT_NOT_APPROVED: 'El monto contratado solo se registra en una cotización aprobada.',
  FINAL_AMOUNT_NOT_WINNER: 'El monto contratado solo se registra en el candidato ganador.',
  FINAL_AMOUNT_CURRENCY_MISMATCH:
    'El evento ya registra montos contratados en otra moneda y no es posible mezclarlas.',
  FINAL_AMOUNT_NOT_PENDING: 'Este monto contratado ya no está pendiente de aprobación.',
  FINAL_AMOUNT_PENDING_CHANGED:
    'El monto pendiente cambió mientras lo revisaba. Revise el monto actual antes de decidir.',
  FINAL_AMOUNT_SELF_DECISION: 'No puede aprobar ni rechazar un monto que usted mismo registró.',
  FINAL_QUOTE_NOT_A_QUOTE:
    'El documento que respalda el monto contratado tiene que ser una cotización, no una imagen.',
  SUPPLIER_CAPACITY_NOT_APPLICABLE: 'El aforo solo aplica a proveedores de categoría Venue.',
  SUPPLIER_SECONDARY_CATEGORY_IS_MAIN:
    'La categoría principal del proveedor no puede agregarse también como secundaria.',
  SUPPLIER_EVALUATION_EVENT_NOT_CLOSED: 'Solo se evalúa a un proveedor en un evento cerrado.',
  SUPPLIER_EVALUATION_SUPPLIER_NOT_WINNER:
    'Solo se evalúa a un proveedor al que se le adjudicó una cotización del evento.',
  SUPPLIER_EVALUATION_RESET_REASON_REQUIRED:
    'Indique el motivo para reiniciar las evaluaciones del proveedor.',
  CONCURRENCY_CONFLICT:
    'Otra persona modificó este registro mientras usted trabajaba. Recargue y vuelva a intentarlo.',
  // El servicio estampa la OPT dueña del registro a partir del token, así que esto no se
  // arregla desde la pantalla: no hay campo que corregir ni reintento que sirva.
  OPT_NOT_ASSIGNED:
    'Su usuario no tiene una OPT asignada, así que no puede registrar empresas ni contactos. Solicite la asignación al administrador.',
  // Una lista de Mis listas que se eliminó después de elegirla, o que no es de la OPT del usuario.
  CONTACT_LIST_NOT_FOUND:
    'La lista de contactos elegida ya no existe o no es de su OPT. Elija otra lista y vuelva a intentarlo.',
  DB_CONFLICT:
    'El registro choca con otro que ya existe o está en uso. Verifique los datos y vuelva a intentarlo.',
  INVALID_AFFILIATION_END_DATE:
    'La fecha de fin de la afiliación no puede ser anterior a su fecha de inicio.',
  FILE_TOO_LARGE: 'El archivo supera el tamaño máximo permitido de 25 MB.',
  FILE_TYPE_NOT_ALLOWED:
    'Formato no permitido. Se aceptan imágenes JPEG, PNG, WEBP y documentos PDF; en los archivos de una empresa, también libros de Excel (.xlsx).',
  FILE_TYPE_MISMATCH: 'La extensión del archivo no coincide con su contenido real.',
  FILE_TYPE_HEIC_NOT_SUPPORTED:
    'Las fotos en formato HEIC de iPhone no son compatibles directamente. Cambie la cámara a "Más compatible" o conviértala a JPG.',
  SUPPLIER_PHOTO_LIMIT_REACHED:
    'Alcanzado el límite máximo de 20 fotos por proveedor. Elimine una foto para subir otra.',
  PROPOSAL_ATTACHMENT_LIMIT_REACHED:
    'Alcanzado el límite máximo de 10 adjuntos por presupuesto. Elimine uno para subir otro.',
  CANDIDATE_NOT_FOUND: 'El candidato ya no está disponible en la cotización.',
  CANDIDATE_PROPOSAL_NOT_FOUND: 'El presupuesto ya no está disponible en la cotización.',
  CANDIDATE_LAST_PROPOSAL:
    'Un candidato no puede quedarse sin presupuestos. Elimine el candidato completo si ya no participa.',
  PROPOSAL_ATTACHMENT_NOT_FOUND: 'El adjunto solicitado fue eliminado previamente.',
  PROPOSAL_ATTACHMENT_KIND_NOT_SUPPORTED:
    'Ese tipo de adjunto no se admite. Solo la hoja de cotización y las fotos de la propuesta.',
  SUPPLIER_PHOTO_NOT_FOUND: 'La foto solicitada fue eliminada previamente.',
  SUPPLIER_SERVICE_NOT_FOUND:
    'Un servicio del catálogo ya no está en este proveedor. Recargue la ficha y vuelva a intentarlo.',
  SUPPLIER_SERVICE_DUPLICATED: 'Ya hay un servicio con ese nombre y esa unidad en el catálogo.',
  SUPPLIER_SERVICE_LIMIT_REACHED: 'Un proveedor admite hasta 100 servicios en su catálogo.',
  FILE_MISSING: 'El archivo ya no se encuentra en el almacenamiento. El fallo es permanente.',
  FILE_STORE_UNAVAILABLE:
    'El servicio de almacenamiento no responde. Intente nuevamente en unos minutos.',
  COMPANY_FILE_LIMIT_REACHED:
    'La empresa ya tiene 20 archivos, el máximo permitido. Elimine uno para subir otro.',
  EVENT_GALLERY_IMAGE_LIMIT_REACHED: 'Límite máximo de imágenes de galería del evento alcanzado.',
  QUOTE_STATUS_NOT_SUPPORTED: 'El estado de la cotización no es compatible con esta operación.',
  QUOTE_CATEGORY_NOT_SUPPORTED: 'La categoría de cotización seleccionada no es compatible.',
  CANDIDATE_TEARDOWN_BEFORE_SETUP:
    'La fecha/hora de desmontaje debe ser posterior a la de montaje.',
  CANDIDATE_PICKED_SERVICE_CURRENCY_MISMATCH:
    'Los servicios elegidos deben ir en la misma moneda que el monto estimado del presupuesto.',
  COMPANY_NOT_AVAILABLE:
    'La empresa seleccionada ya no está disponible o fue dada de baja. Elija otra.',
  INVALID_COMPANY_CATEGORY:
    'Una o más de las categorías elegidas ya no existen o fueron descontinuadas. Recarga el formulario y vuelve a elegirlas.',
  INVALID_CONTACT_CLASSIFICATION:
    'Una o más de las clasificaciones elegidas ya no existen o fueron descontinuadas. Recarga el formulario y vuelve a elegirlas.',
  DUPLICATE_CONTACT_EMAIL:
    'Ese correo ya pertenece a otro contacto. Ábrelo en vez de registrar el mismo correo dos veces.',
  SUPPLIER_COMPANY_ALREADY_REGISTERED:
    'Esa empresa ya tiene una ficha de proveedor viva en esta categoría.',
  'company.suspension_not_editable':
    'La suspensión de una empresa no se pone ni se quita desde aquí. Recarga la ficha para ver su estado actual.',
  'company.suspension_reason_required': 'Para suspender una empresa hay que escribir el motivo.',
  'company.already_suspended':
    'La empresa ya estaba suspendida. Recarga la ficha para ver el motivo vigente.',
  'company.not_suspended':
    'La empresa ya no estaba suspendida. Recarga la ficha para ver su estado actual.',
  'company.duplicate_domain':
    'Ya existe una empresa registrada con ese sitio web. Ábrela en vez de registrarla dos veces.',
  'company.duplicate_name':
    'Ya existe una empresa registrada con ese nombre. Revisa si es la misma antes de crearla.',
  'vip.reason_required':
    'Para marcar una cuenta estratégica (VIP) hay que escribir la justificación.',
  'contact.suspension_reason_required': 'Para suspender un contacto hay que escribir el motivo.',
  'contact.already_suspended':
    'El contacto ya estaba suspendido. Recarga la ficha para ver el motivo vigente.',
  'contact.unknown_status': 'El filtro pide un estado de contacto que no existe.',
  'contact.not_suspended':
    'El contacto ya no estaba suspendido. Recarga la ficha para ver su estado actual.',
  'contact.company_suspended':
    'El contacto trabaja en una empresa suspendida. Su suspensión no se puede levantar mientras la empresa siga suspendida.',
  'interaction.meeting_outside_contact_opt':
    'Solo un usuario de la OPT del contacto puede agendarle una reunión. La OPT del contacto es la de su empresa actual.',
  'contact_list.contacts_not_found':
    'Algunos contactos elegidos ya no existen o fueron dados de baja, así que no se agregó ninguno a la lista. Recarga el directorio y vuelve a elegirlos.',
  'task.contact_not_found':
    'El contacto de la tarea ya no existe en el CRM o fue dado de baja. Recarga la ficha.',
  'responsible_officer.not_found':
    'El oficial responsable elegido ya no es personal vigente de ninguna OPT. Elige otro.',
  'origin_source.not_found': 'La fuente de origen elegida ya no está en el catálogo. Elige otra.',
  'bank_account.invalid_currency': 'La moneda de la cuenta debe ser USD, EUR o DOP.',
  'bank_account.invalid_bank_country': 'Elige el país del banco de la lista.',
  'bank_account.invalid_swift_bic': 'El SWIFT/BIC tiene 8 u 11 caracteres, letras y números.',
  'bank_account.aba_required': 'Un banco de Estados Unidos exige el número ABA.',
  'bank_account.invalid_aba': 'El ABA tiene 9 dígitos.',
  'bank_account.invalid_iban':
    'El IBAN no es válido: su dígito de control no cuadra. Revisa el número de cuenta.',
  'bank_account.invalid_intermediary_swift_bic':
    'El SWIFT/BIC del banco intermediario tiene 8 u 11 caracteres, letras y números.',
  'bank_account.invalid_intermediary_aba': 'El ABA del banco intermediario tiene 9 dígitos.',
  'bank.invalid_country': 'Elige el país del banco de la lista.',
  'bank.invalid_swift_bic': 'El SWIFT/BIC tiene 8 u 11 caracteres, letras y números.',
  'bank.duplicate_swift_bic': 'Ya hay un banco con ese código SWIFT/BIC en el catálogo.',
  'feature_switch.invalid_key':
    'La clave del interruptor debe ser un módulo, o un módulo y una función separados por un punto, en minúsculas y con guiones entre palabras.',
  CANDIDATE_BANK_ACCOUNT_NOT_AVAILABLE:
    'La cuenta bancaria elegida ya no está disponible para este proveedor. Vuelve a elegir una cuenta.',
  CANDIDATE_SUPPLIER_ALREADY_QUOTED:
    'Ese proveedor ya es candidato en esta cotización. Edite el candidato existente en vez de agregarlo de nuevo.',
  PUBLIC_REGISTRATION_NOT_FOUND:
    'Este enlace no es válido o ya no está vigente. Si recibiste varias invitaciones, usa la más reciente.',
  PUBLIC_UNSUBSCRIBE_NOT_FOUND:
    'Este enlace de baja no es válido o ya no está vigente. Si sigues recibiendo invitaciones, escríbenos.',
  REGISTRATIONS_CLOSED: 'Las inscripciones para este evento están cerradas.',
  REGISTRATION_NOT_INVITED:
    'Este evento es privado: solo pueden registrarse las personas invitadas. Usa el correo con el que recibiste la invitación.',
  RATE_LIMITED: 'Demasiados intentos. Espera un minuto y vuelve a intentarlo.',
  SUPPLIER_INTAKE_LINK_NOT_FOUND:
    'Este enlace no es válido o ya no está activo. Pide el enlace vigente a quien te lo envió.',
  SUPPLIER_INTAKE_DAILY_CAP_REACHED:
    'Este enlace ya llegó al máximo de envíos de hoy. Puedes seguir mañana.',
  SUPPLIER_INTAKE_SENDER_NOT_ALLOWED:
    'Tu nombre ya no está en el personal de esta oficina. Vuelve a abrir el enlace y elige tu nombre de nuevo.',
  SUPPLIER_INTAKE_COMPANY_EXISTS:
    'Esta empresa ya está en el directorio, con ese nombre o ese sitio web. No hace falta enviarla otra vez.',
  SUPPLIER_INTAKE_NOT_READY:
    'La carga de proveedores todavía no está lista. Intenta de nuevo más tarde; no se guardó nada.',
  SUPPLIER_INTAKE_LINK_ALREADY_ACTIVE:
    'Esta oficina ya tiene un enlace activo. Si se perdió, reemplázalo por uno nuevo.',
  OPT_OFFICE_NOT_FOUND: 'La oficina no está en el directorio de OPT o está cerrada.',
  CAMPAIGN_NOT_DRAFT:
    'La campaña ya no admite cambios: solo un borrador acepta editar plantilla, idioma, destinatarios o cargas.',
  CAMPAIGN_TEMPLATE_NOT_FOUND:
    'No hay plantilla para ese tipo de campaña en el idioma elegido. Elija otro idioma o tipo.',
  CAMPAIGN_DIRECT_MESSAGE_MISMATCH:
    'Una campaña sin evento es un mensaje directo con asunto y texto; una de evento no lleva texto propio.',
  CAMPAIGN_HAS_NO_RECIPIENTS: 'La campaña no tiene destinatarios: selecciónelos antes de lanzarla.',
  CAMPAIGN_HAS_NO_MAILABLE_RECIPIENTS:
    'Ningún destinatario de la campaña se puede contactar: revise las bajas antes de lanzarla.',
  CAMPAIGN_RECIPIENTS_CHANGED:
    'La lista de destinatarios cambió desde que la abrió. Recargue la campaña y vuelva a lanzarla.',
  CAMPAIGN_TRANSITION_NOT_ALLOWED: 'La campaña no puede pasar a ese estado desde el actual.',
  UPLOAD_TOO_MANY_ROWS:
    'El archivo tiene más filas de datos de las que se pueden procesar en una sola carga. Divídalo y suba una parte a la vez.',
  UPLOAD_EXPANDED_TOO_LARGE:
    'El archivo se descomprime muy por encima del límite y no se puede procesar.',
  UPLOAD_TOO_MANY_COLUMNS:
    'Una hoja del archivo tiene demasiadas columnas. Deje solo las columnas de la lista y vuelva a subirlo.',
  UPLOAD_NO_HEADER_ROW:
    "No se reconoció ninguna columna en la primera fila. Se esperan encabezados como 'nombre', 'apellido', 'correo', 'teléfono' o 'empresa'.",
  UPLOAD_NOT_FOUND: 'Esta campaña no tiene ninguna carga pendiente de confirmar.',
  EVENT_NOT_CANCELLED: 'El evento no está cancelado: no se puede avisar a sus inscritos.',
  CANCELLATION_NOTICE_UNAVAILABLE:
    'Falta la plantilla del aviso de cancelación en el idioma del evento.',
  CAMPAIGN_EVENT_CANCELLED:
    'El evento de esta campaña fue cancelado, así que ya no se puede lanzar. La campaña sigue en borrador.',
  CAMPAIGN_REGISTRATIONS_CLOSED:
    'El registro del evento está cerrado. Reábralo antes de lanzar la campaña; mientras tanto sigue en borrador.',
  TEMPLATE_EVENT_VALUES_MISSING:
    'Al evento le faltan datos que la plantilla necesita (nombre, lugar o fecha). Complételos en el evento y vuelva a lanzar.',
  EVENTS_UNAVAILABLE:
    'El servicio de eventos no responde. Intente nuevamente en unos minutos; no se envió nada.',
  NOTIFICATIONS_UNAVAILABLE:
    'El servicio de notificaciones no responde. La campaña quedó en envío y puede reanudarse más tarde.',
  CAMPAIGN_NOT_SENDING: 'La campaña no está en envío, así que no hay nada pendiente que reanudar.',
  RAFFLE_ALREADY_EXISTS: 'El evento ya tiene un sorteo.',
  RAFFLE_NOT_FOUND: 'El evento no tiene sorteo.',
  RAFFLE_NOT_DRAFT: 'El sorteo ya arrancó y esta acción solo vale para un borrador.',
  RAFFLE_BULK_EXCLUDE_SOURCE_REQUIRED:
    'Indique una sola fuente: el sorteo de otro evento o una lista de correos e IDs.',
  RAFFLE_BULK_EXCLUDE_TOO_MANY: 'La exclusión en lote admite como máximo 5000 identificadores.',
  RAFFLE_BULK_EXCLUDE_SAME_EVENT: 'El sorteo de origen tiene que ser de otro evento.',
  RAFFLE_LINK_INVALID: 'No se pudo vincular el sorteo a ese evento.',
  RAFFLE_DISPLAY_FIELDS_INVALID: 'Elige una opción válida para cada dato que se muestra.',
  RAFFLE_NOT_IN_PROGRESS: 'El sorteo no está en curso.',
  RAFFLE_ATTENDEE_NOT_VOIDED: 'Esa persona no tiene un premio anulado: ya puede ganar.',
  RAFFLE_COMPLETED: 'El sorteo ya está completado y no admite cambios.',
  RAFFLE_NOT_COMPLETED:
    'El sorteo todavía no está completado: el acta solo se puede reemitir después de completarlo.',
  RAFFLE_HAS_DRAWS: 'El sorteo ya tiene premios sorteados y no se puede reabrir.',
  RAFFLE_AUTHORITIES_REQUIRED:
    'Para completar el sorteo debe registrar el nombre y el documento de identidad del notario y de los dos testigos.',
  RAFFLE_PICKUP_INSTRUCTIONS_REQUIRED:
    'Para completar el sorteo debe indicar dónde y hasta cuándo se retiran los premios.',
  RAFFLE_NO_PARTICIPANTS:
    'Nadie en la puerta cumple los requisitos del sorteo, así que no hay a quién sortear.',
  RAFFLE_POOL_EXHAUSTED: 'No quedan participantes elegibles para este premio.',
  ROUND_NOT_FOUND: 'La ronda no existe en este sorteo.',
  ROUND_NOT_EMPTY: 'La ronda tiene premios. Muévalos o elimínelos antes de eliminar la ronda.',
  PRIZE_NOT_FOUND: 'El premio no existe en este sorteo.',
  PRIZE_HAS_DRAWS: 'Este premio ya se sorteó y no se puede modificar ni eliminar.',
  PRIZE_ALREADY_DRAWN: 'Este premio ya tiene un ganador.',
  PRIZE_NOT_DRAWN: 'Este premio no tiene un ganador que anular.',
  PRIZE_ALREADY_REDEEMED: 'Este premio ya fue entregado.',
  PRIZE_NOT_REDEEMED: 'Este premio no tiene una entrega que revertir.',
  REDEMPTION_CODE_NOT_FOUND: 'No existe ningún premio con ese código de canje.',
  CHECK_IN_CODE_NOT_FOUND: 'Esta entrada no existe. Busque a la persona en la lista de la puerta.',
  CHECK_IN_WRONG_EVENT: 'Esta entrada no es para este evento.',
  REGISTRATION_NOT_FOUND:
    'La inscripción ya no está en este evento. Recargue la lista de la puerta.',
  ATTENDANCE_UNAVAILABLE:
    'No se pudo obtener la lista de asistentes registrados en puerta. Intente de nuevo en unos minutos.',
  ATTENDANCE_TOO_MANY_ROWS:
    'El evento tiene más de 5000 asistentes registrados en puerta, el máximo que admite un sorteo.',
  PARTICIPANT_WALK_IN: 'Esta persona se registró sin invitación y no puede entrar al sorteo.',
  PARTICIPANT_CONTACT_UNRESOLVED:
    'Esta persona no tiene contacto en el CRM, así que no se puede comprobar si se le puede invitar.',
  PARTICIPANT_SUSPENDED: 'Esta persona está suspendida en el CRM y no puede entrar al sorteo.',
  PARTICIPANT_DO_NOT_INVITE: 'El CRM tiene marcada a esta persona para no invitarla.',
  PARTICIPANT_NOT_CHECKED_IN: 'Esta persona no está registrada en la puerta del evento.',
}

/**
 * Traduce el código que trae la respuesta, o `undefined` si esta versión no lo conoce:
 * el backend publica más códigos de los que la UI necesita distinguir, y quien llama
 * decide entonces con qué se queda.
 */
export function apiErrorCodeLabelFor(code: string | undefined): string | undefined {
  if (!code) return undefined
  return Object.hasOwn(apiErrorCodeLabel, code)
    ? apiErrorCodeLabel[code as ApiErrorCode]
    : undefined
}
