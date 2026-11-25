/**
 * Contratos HTTP de eventos-service (v1). Los nombres de propiedad reflejan
 * exactamente el JSON del backend (camelCase, dominio en inglés — ADR-0008).
 *
 * Algunos campos están marcados "(frontend)" porque no existen todavía en el
 * backend: se conservan como opcionales para no perder la funcionalidad de la
 * demo, pero no se persisten contra el servicio real hasta que se agreguen
 * allá. No los envíes como obligatorios en ningún formulario.
 */

/** El tipo de celebración. Códigos en inglés (ADR-0008); la etiqueta en español vive en labels.ts. */
export type EventoType =
  | 'Corporate'
  | 'Christmas'
  | 'Wedding'
  | 'Birthday'
  | 'Concert'
  | 'Graduation'
  | 'Conference'
  | 'Other'
export type EventoStatus = 'planning' | 'in_progress' | 'closed' | 'cancelled'
export type Idioma = 'es' | 'en' | 'fr' | 'de' | 'pt' | 'it'
/** Si el evento sale en la cartelera pública de convocatorias-service. */
export type EventoVisibility = 'public' | 'private'
export type Currency = 'USD' | 'EUR'

/**
 * Códigos máquina que el backend publica en la extensión `code` del ProblemDetails.
 * Distinguen dos fallos que comparten estado HTTP (dos 409 por motivos distintos) sin
 * leer el mensaje, que es prosa para una persona y no un contrato. Su traducción vive
 * en `apiErrorCodeLabel` (`src/lib/labels.ts`); un código ausente de esta unión cae al
 * mensaje del servicio.
 */
export type ApiErrorCode =
  | 'PLAN_PARTICIPANT_LIMIT'
  | 'PLAN_ACTIVE_RAFFLE_LIMIT'
  | 'PLAN_MONTHLY_RAFFLE_LIMIT'
  | 'PLAN_ROUND_LIMIT'
  | 'PLAN_PRIZE_LIMIT'
  | 'PLAN_USER_LIMIT'
  | 'PLAN_FEATURE_NOT_INCLUDED'
  | 'PLAN_REQUEST_PENDING'
  | 'ACCOUNT_NOT_ACTIVE'
  | 'ACCOUNT_EMAIL_TAKEN'
  | 'ACTIVATION_TOKEN_INVALID'
  | 'OPT_OFFICE_NOT_AVAILABLE'
  | 'OPT_CONTACT_NOT_AVAILABLE'
  | 'CRM_UNAVAILABLE'
  | 'EVENT_NOT_STARTABLE'
  | 'EVENT_NOT_CANCELLABLE'
  | 'EVENT_NOT_EDITABLE'
  | 'EVENT_LANGUAGE_REQUIRED'
  | 'EVENT_NOT_CLOSABLE'
  | 'TIME_ZONE_UNKNOWN'
  | 'TOTAL_COST_NO_QUOTES'
  | 'TOTAL_COST_QUOTE_DRAFT'
  | 'TOTAL_COST_QUOTE_UNDECIDED'
  | 'TOTAL_COST_WINNER_UNKNOWN'
  | 'TOTAL_COST_FINAL_AMOUNT_AMBIGUOUS'
  | 'TOTAL_COST_MISSING_FINAL_AMOUNT'
  | 'TOTAL_COST_FINAL_AMOUNT_PENDING'
  | 'TOTAL_COST_MIXED_CURRENCIES'
  | 'MAX_CANDIDATES'
  | 'CANDIDATES_OUT_OF_RANGE'
  | 'NO_RECOMMENDED'
  | 'EXCEPTION_REQUIRED'
  | 'SELF_DECISION'
  | 'REASON_REQUIRED'
  | 'WINNER_NOT_IN_QUOTE'
  | 'QUOTE_NOT_DRAFT'
  | 'QUOTE_NOT_SUBMITTED'
  | 'QUOTE_APPROVED'
  | 'FINAL_AMOUNT_NOT_APPROVED'
  | 'FINAL_AMOUNT_NOT_WINNER'
  | 'FINAL_AMOUNT_CURRENCY_MISMATCH'
  | 'FINAL_AMOUNT_NOT_PENDING'
  | 'FINAL_AMOUNT_PENDING_CHANGED'
  | 'FINAL_AMOUNT_SELF_DECISION'
  | 'FINAL_QUOTE_NOT_A_QUOTE'
  | 'SUPPLIER_CAPACITY_NOT_APPLICABLE'
  | 'SUPPLIER_SECONDARY_CATEGORY_IS_MAIN'
  | 'SUPPLIER_EVALUATION_EVENT_NOT_CLOSED'
  | 'SUPPLIER_EVALUATION_SUPPLIER_NOT_WINNER'
  | 'SUPPLIER_EVALUATION_RESET_REASON_REQUIRED'
  | 'CONCURRENCY_CONFLICT'
  | 'OPT_NOT_ASSIGNED'
  | 'CONTACT_LIST_NOT_FOUND'
  | 'DB_CONFLICT'
  | 'INVALID_AFFILIATION_END_DATE'
  | 'FILE_TOO_LARGE'
  | 'FILE_TYPE_NOT_ALLOWED'
  | 'FILE_TYPE_MISMATCH'
  | 'FILE_TYPE_HEIC_NOT_SUPPORTED'
  | 'SUPPLIER_PHOTO_LIMIT_REACHED'
  | 'PROPOSAL_ATTACHMENT_LIMIT_REACHED'
  | 'CANDIDATE_NOT_FOUND'
  | 'CANDIDATE_PROPOSAL_NOT_FOUND'
  | 'CANDIDATE_LAST_PROPOSAL'
  | 'PROPOSAL_ATTACHMENT_NOT_FOUND'
  | 'PROPOSAL_ATTACHMENT_KIND_NOT_SUPPORTED'
  | 'SUPPLIER_PHOTO_NOT_FOUND'
  | 'SUPPLIER_SERVICE_NOT_FOUND'
  | 'SUPPLIER_SERVICE_DUPLICATED'
  | 'SUPPLIER_SERVICE_LIMIT_REACHED'
  | 'FILE_MISSING'
  | 'FILE_STORE_UNAVAILABLE'
  | 'COMPANY_FILE_LIMIT_REACHED'
  | 'EVENT_GALLERY_IMAGE_LIMIT_REACHED'
  | 'QUOTE_STATUS_NOT_SUPPORTED'
  | 'QUOTE_CATEGORY_NOT_SUPPORTED'
  | 'CANDIDATE_TEARDOWN_BEFORE_SETUP'
  | 'CANDIDATE_PICKED_SERVICE_CURRENCY_MISMATCH'
  | 'COMPANY_NOT_AVAILABLE'
  | 'INVALID_COMPANY_CATEGORY'
  | 'INVALID_CONTACT_CLASSIFICATION'
  | 'DUPLICATE_CONTACT_EMAIL'
  | 'SUPPLIER_COMPANY_ALREADY_REGISTERED'
  | 'company.suspension_not_editable'
  | 'company.suspension_reason_required'
  | 'company.already_suspended'
  | 'company.not_suspended'
  | 'company.duplicate_domain'
  | 'company.duplicate_name'
  | 'vip.reason_required'
  | 'contact.suspension_reason_required'
  | 'contact.already_suspended'
  | 'contact.not_suspended'
  | 'contact.company_suspended'
  | 'contact.unknown_status'
  | 'task.contact_not_found'
  | 'contact_list.contacts_not_found'
  | 'interaction.meeting_outside_contact_opt'
  | 'responsible_officer.not_found'
  | 'bank_account.invalid_currency'
  | 'bank_account.invalid_bank_country'
  | 'bank_account.invalid_swift_bic'
  | 'bank_account.aba_required'
  | 'bank_account.invalid_aba'
  | 'bank_account.invalid_iban'
  | 'bank_account.invalid_intermediary_swift_bic'
  | 'bank_account.invalid_intermediary_aba'
  | 'bank.invalid_country'
  | 'bank.invalid_swift_bic'
  | 'bank.duplicate_swift_bic'
  | 'feature_switch.invalid_key'
  | 'origin_source.not_found'
  | 'CANDIDATE_BANK_ACCOUNT_NOT_AVAILABLE'
  | 'CANDIDATE_SUPPLIER_ALREADY_QUOTED'
  // Portal de registro (convocatorias-service, anónimo)
  | 'PUBLIC_REGISTRATION_NOT_FOUND'
  | 'PUBLIC_UNSUBSCRIBE_NOT_FOUND'
  | 'REGISTRATIONS_CLOSED'
  | 'REGISTRATION_NOT_INVITED'
  | 'RATE_LIMITED'
  // Carga de proveedores (convocatorias-service, anónima)
  | 'SUPPLIER_INTAKE_LINK_NOT_FOUND'
  | 'SUPPLIER_INTAKE_DAILY_CAP_REACHED'
  | 'SUPPLIER_INTAKE_SENDER_NOT_ALLOWED'
  | 'SUPPLIER_INTAKE_COMPANY_EXISTS'
  | 'SUPPLIER_INTAKE_NOT_READY'
  // Enlaces de la carga de proveedores (convocatorias-service, administración central)
  | 'SUPPLIER_INTAKE_LINK_ALREADY_ACTIVE'
  | 'OPT_OFFICE_NOT_FOUND'
  | 'CAMPAIGN_NOT_DRAFT'
  | 'CAMPAIGN_TEMPLATE_NOT_FOUND'
  | 'CAMPAIGN_DIRECT_MESSAGE_MISMATCH'
  | 'CAMPAIGN_HAS_NO_RECIPIENTS'
  | 'CAMPAIGN_HAS_NO_MAILABLE_RECIPIENTS'
  | 'CAMPAIGN_RECIPIENTS_CHANGED'
  | 'CAMPAIGN_TRANSITION_NOT_ALLOWED'
  | 'UPLOAD_TOO_MANY_ROWS'
  | 'UPLOAD_EXPANDED_TOO_LARGE'
  | 'UPLOAD_TOO_MANY_COLUMNS'
  | 'UPLOAD_NO_HEADER_ROW'
  | 'EVENT_NOT_CANCELLED'
  | 'CANCELLATION_NOTICE_UNAVAILABLE'
  | 'UPLOAD_NOT_FOUND'
  | 'CAMPAIGN_EVENT_CANCELLED'
  | 'CAMPAIGN_REGISTRATIONS_CLOSED'
  | 'TEMPLATE_EVENT_VALUES_MISSING'
  | 'EVENTS_UNAVAILABLE'
  | 'NOTIFICATIONS_UNAVAILABLE'
  | 'CAMPAIGN_NOT_SENDING'
  // Sorteos (events-service)
  | 'RAFFLE_ALREADY_EXISTS'
  | 'RAFFLE_NOT_FOUND'
  | 'RAFFLE_NOT_DRAFT'
  | 'RAFFLE_BULK_EXCLUDE_SOURCE_REQUIRED'
  | 'RAFFLE_BULK_EXCLUDE_TOO_MANY'
  | 'RAFFLE_BULK_EXCLUDE_SAME_EVENT'
  | 'RAFFLE_DISPLAY_FIELDS_INVALID'
  | 'RAFFLE_NOT_IN_PROGRESS'
  | 'RAFFLE_ATTENDEE_NOT_VOIDED'
  | 'RAFFLE_COMPLETED'
  | 'RAFFLE_NOT_COMPLETED'
  | 'RAFFLE_HAS_DRAWS'
  | 'RAFFLE_AUTHORITIES_REQUIRED'
  | 'RAFFLE_PICKUP_INSTRUCTIONS_REQUIRED'
  | 'RAFFLE_NO_PARTICIPANTS'
  | 'RAFFLE_POOL_EXHAUSTED'
  | 'ROUND_NOT_FOUND'
  | 'ROUND_NOT_EMPTY'
  | 'PRIZE_NOT_FOUND'
  | 'PRIZE_HAS_DRAWS'
  | 'PRIZE_ALREADY_DRAWN'
  | 'PRIZE_NOT_DRAWN'
  | 'PRIZE_ALREADY_REDEEMED'
  | 'PRIZE_NOT_REDEEMED'
  | 'REDEMPTION_CODE_NOT_FOUND'
  // Check-in en la puerta (convocatorias-service)
  | 'CHECK_IN_CODE_NOT_FOUND'
  | 'CHECK_IN_WRONG_EVENT'
  | 'REGISTRATION_NOT_FOUND'
  | 'ATTENDANCE_UNAVAILABLE'
  | 'ATTENDANCE_TOO_MANY_ROWS'
  | 'PARTICIPANT_WALK_IN'
  | 'PARTICIPANT_CONTACT_UNRESOLVED'
  | 'PARTICIPANT_SUSPENDED'
  | 'PARTICIPANT_DO_NOT_INVITE'
  | 'PARTICIPANT_NOT_CHECKED_IN'

export const CATEGORIAS_PROVEEDOR = [
  'Venue',
  'Ambientacion',
  'Audiovisuales',
  'Seguridad',
  'Traductores',
  'Edecanes',
  'Alimentacion',
  'Transporte',
  'Fotografia',
  'Produccion',
  'Tecnologia',
  'Mobiliario',
  'Entretenimiento',
  'Limpieza',
  'Logistica',
  'OrganizadoresDeFerias',
] as const
export type CategoriaProveedor = (typeof CATEGORIAS_PROVEEDOR)[number]

/**
 * Un servicio del catálogo del proveedor, como lo devuelve `GET /v1/service-suppliers/{id}`. El
 * `id` lo pone events-service y no cambia; el precio lleva su moneda, USD o EUR.
 */
export interface ProveedorServiceItem {
  id: string
  name: string
  /** Texto libre, como "Día" o "Pax". */
  unit: string
  referencePrice: Money
  /** (frontend) El servicio no guarda descripción: solo la demo de Ferias la trae. */
  description?: string | null
}

/**
 * Un servicio en el alta o la edición de la ficha. Sin `id` es nuevo y el servidor le pone uno;
 * con `id` se conserva. Un id que el servidor no dio es 400 `SUPPLIER_SERVICE_NOT_FOUND`, así que
 * los ids que la pantalla inventa para sus filas nunca viajan.
 */
export interface ProveedorServiceRequest {
  id?: string | null
  name: string
  unit: string
  referencePrice: Money
}

export interface Money {
  amount: number
  currency: Currency
}

// ---------- Empresas (crm-service API) ----------

/**
 * La página del CRM, que **no** es la de eventos: cuenta en `totalCount` y no en
 * `total`. Son dos servicios y dos formas; mezclarlas deja el total en `undefined`
 * y la paginación mostrando "NaN".
 */
export interface CrmPagedResponse<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

/**
 * Una categoría del catálogo administrable de crm-service (`GET /v1/company-categories`).
 * El `code` es estable y es lo que filtra el listado; el `name` es lo que se lee y un
 * `central_admin` lo puede cambiar.
 */
export interface CompanyCategoryResponse {
  id: string
  code: string
  name: string
  /** Una empresa con alguna categoría de persona es una persona (`docs/adr/0032`). */
  isPerson: boolean
}

/** Organización o persona: lo calcula crm-service de las categorías y no se escribe. */
export type CompanyKind = 'Organization' | 'Individual'

/**
 * Una fuente de origen del catálogo de crm-service (`GET /v1/origin-sources`): dónde
 * conoció la organización a la empresa o al contacto. Solo llegan las vigentes; el `code` no cambia y
 * el `name` lo corrige un `central_admin`.
 */
export interface OriginSourceResponse {
  id: string
  code: string
  name: string
}

/**
 * La relación de la organización con la empresa. `Inactive`: la empresa ya no existe (quebró,
 * cerró) y su ficha se conserva. `Suspended`: existe, pero la organización la suspendió de hacer
 * negocios con la organización; ni el PATCH la pone ni la quita. Viaja como texto.
 */
export type CompanyStatus = 'Active' | 'Inactive' | 'Suspended'

/**
 * Hasta dónde llegó la empresa con la organización (*relación con la organización*, ). No es `status`:
 * no tiene inactiva. Opcional; nulo es "Sin definir".
 */
export type RelationshipStage = 'Prospect' | 'Contacted' | 'Active' | 'KeyAlly'

/** Cuánta atención le da la organización a la empresa. No es la marca VIP. Opcional. */
export type StrategicPriority = 'Strategic' | 'Key' | 'Regular'

/** Hasta dónde llegan las operaciones de la empresa. Opcional. */
export type GeographicScope = 'Local' | 'Regional' | 'National' | 'International' | 'Multinational'

/** Por qué viajan los viajeros a los que vende la empresa. No es un país. */
export type Market = 'Leisure' | 'Mice' | 'Golf' | 'Weddings' | 'Cruises'

/** La clase de producto que vende la empresa. Reuniones y eventos son el mercado MICE. */
export type TourismSegment =
  'SunAndBeach' | 'Luxury' | 'Food' | 'Culture' | 'Ecotourism' | 'Adventure' | 'Sports' | 'Wellness'

/** Un polo turístico dominicano que la empresa promueve. No es una provincia. */
export type Destination =
  | 'PuntaCana'
  | 'SantoDomingo'
  | 'PuertoPlata'
  | 'Samana'
  | 'LaRomana'
  | 'Miches'
  | 'Santiago'
  | 'Pedernales'

/** La relación de la organización con la persona: un contacto nunca está inactivo. */
export type ContactStatus = 'Active' | 'Suspended'

/**
 * Cuánto pesa el contacto en un acuerdo con su empresa. No es el cargo, que es texto
 * libre en la afiliación. Viaja como texto; sin valor, la pantalla dice "Sin definir".
 */
export type DecisionLevel = 'CLevel' | 'Director' | 'Manager' | 'Operational'

/** Una empresa tal como la devuelve crm-service. */
export interface CompanyResponse {
  id: string
  name: string
  /** Nombre comercial, cuando difiere del registrado. */
  commercialName?: string | null
  /** Razón social: la que consta en el registro mercantil (Marriott International, Inc.). */
  legalName?: string | null
  /** RNC de una organización, cédula de una persona o su equivalente en otro país. */
  taxId?: string | null
  /** Perfiles en redes sociales (usuarios o enlaces), como los escribió el usuario. */
  socialNetworks?: string | null
  description?: string | null
  /** El host ya normalizado. */
  domain?: string | null
  /** Exactamente lo que el usuario escribió; `null` siempre que `domain` lo sea. */
  domainRaw?: string | null
  country: string
  city?: string | null
  streetAddress1?: string | null
  streetAddress2?: string | null
  phone?: string | null
  email?: string | null
  /**
   * La OPT dueña del registro. La estampa el servicio a partir del `opt_code` del
   * token: no viaja en el request y no se puede elegir desde la pantalla.
   */
  optOfficeId: string
  /** La relación comercial. Sin filtro de `status`, el listado solo trae `Active`. */
  status: CompanyStatus
  /** Derivado de `status`: `true` solo cuando es `Active`. */
  isActive: boolean
  /** El motivo de la suspensión. Solo tiene valor mientras `status` es `Suspended`. */
  suspensionReason?: string | null
  /**
   * Quién cambió `status` por última vez y cuándo. Lo estampan suspender, levantar la
   * suspensión y un PATCH que cambia `status`; nulos en una empresa que nunca cambió.
   */
  statusChangedAt?: string | null
  statusChangedById?: string | null
  /** La baja del registro — eje distinto de `isActive`. En el listado siempre `false`. */
  isDeleted: boolean
  createdAt: string
  updatedAt: string
  /**
   * Sus categorías, ordenadas por nombre. No viajan en el POST ni en el PATCH: se
   * reemplazan aparte con `PUT /v1/companies/{id}/categories`.
   */
  categories: CompanyCategoryResponse[]
  kind: CompanyKind
  /**
   * El oficial responsable: un `OptStaff` de cualquier OPT, por id. `responsibleOfficerName`
   * lo resuelve crm-service al leer, también si esa persona ya dejó su oficina.
   */
  responsibleOfficerId?: string | null
  responsibleOfficerName?: string | null
  /**
   * La fuente de origen, por id. `originSourceName` lo resuelve crm-service al leer,
   * también si la fuente ya se retiró del catálogo.
   */
  originSourceId?: string | null
  originSourceName?: string | null
  /**
   * La marca de cuenta estratégica, o `null` si no lo es. No viaja en el POST ni en el
   * PATCH: se pone con `PUT /v1/companies/{id}/vip` y se quita con `DELETE`.
   */
  vip?: VipResponse | null
  /** Etapa de relación, prioridad estratégica y alcance geográfico; nulos son "Sin definir". */
  relationshipStage?: RelationshipStage | null
  strategicPriority?: StrategicPriority | null
  geographicScope?: GeographicScope | null
  /**
   * Mercados, segmentos turísticos y destinos: cada valor una vez y en el orden del conjunto.
   * El servicio siempre los manda, vacíos si la empresa no tiene ninguno.
   */
  markets?: Market[]
  tourismSegments?: TourismSegment[]
  destinations?: Destination[]
}

/** La marca VIP de una empresa o de un contacto: por qué, cuándo y quién. */
export interface VipResponse {
  reason: string
  markedAt: string
  markedById: string
}

// ---------- Contactos (crm-service API) ----------

export type SuppressionReason = 'no_email' | 'opted_out' | 'suspended' | 'do_not_invite'

export interface ContactCompanyRef {
  id: string
  name: string
}

/**
 * Una clasificación del catálogo administrable de crm-service
 * (`GET /v1/contact-classifications`). Como las categorías de empresa: el `code` es estable
 * y es lo que filtra el listado; el `name` es lo que se lee. El catálogo nace vacío y lo
 * llena un `central_admin`.
 */
export interface ContactClassificationResponse {
  id: string
  code: string
  name: string
}

/**
 * Un contacto tal como lo devuelve crm-service: el nombre y el apellido por separado, y
 * `name` ya juntado para mostrar.
 *
 * El consentimiento y las marcas de supresión no están y no deben estar: el servicio
 * nunca los devuelve (docs/specs/crm/03-contacts-gdpr.md). De ellos solo publica la
 * respuesta, `canBeEmailed` y `suppressionReason`; lo que la ficha muestra de más vive
 * en el store local.
 */
export interface ContactResponse {
  id: string
  name: string
  firstName: string
  lastName: string
  email?: string | null
  phone?: string | null
  /** País y ciudad donde trabaja, no donde vive. Empiezan siendo los de su empresa. */
  country?: string | null
  city?: string | null
  /** Número de WhatsApp, aparte del teléfono. */
  whatsApp?: string | null
  preferredLanguage?: Idioma | null
  decisionLevel?: DecisionLevel | null
  createdAt: string
  updatedAt: string

  // Mailability y supresión (calculados en crm-service)
  canBeEmailed: boolean
  suppressionReason: SuppressionReason | null

  // Afiliaciones activas a empresas (display)
  companies?: ContactCompanyRef[]

  /**
   * La OPT de su empresa actual, que crm-service deduce en cada lectura: el contacto no
   * tiene OPT propia y ninguna escritura la acepta. Nula si no tiene empresa vigente.
   */
  optOfficeId?: string | null

  /**
   * El "Agente Responsable": un `OptStaff` de cualquier OPT, por id, que no cambia porque el
   * contacto cambie de empresa. `responsibleOfficerName` lo resuelve crm-service al leer.
   */
  responsibleOfficerId?: string | null
  responsibleOfficerName?: string | null

  /**
   * La fuente de origen, del mismo catálogo que la de la empresa, por id.
   * `originSourceName` lo resuelve crm-service al leer, también si la fuente ya se retiró.
   */
  originSourceId?: string | null
  originSourceName?: string | null

  /**
   * La marca de contacto VIP, o `null` si no lo es. No viaja en el POST ni en el PATCH: se
   * pone con `PUT /v1/contacts/{id}/vip` y se quita con `DELETE`.
   */
  vip?: VipResponse | null

  /**
   * Activo o suspendido, nunca inactivo. Se suspende con `POST /v1/contacts/{id}/suspension`
   * y se levanta con `DELETE`. `suspensionReason` existe solo mientras está
   * suspendido; quién y cuándo dicen el último cambio de estado.
   */
  status?: ContactStatus
  suspensionReason?: string | null
  statusChangedAt?: string | null
  statusChangedById?: string | null
  /**
   * La empresa cuya suspensión se copió al contacto: suspendido porque su empresa lo
   * fue. Nulo en una suspensión propia o en un contacto activo.
   */
  suspendedByCompanyId?: string | null

  /**
   * Sus clasificaciones vigentes, ordenadas por nombre. No viajan en el POST ni en el PATCH:
   * se reemplazan aparte con `PUT /v1/contacts/{id}/classifications`.
   */
  classifications: ContactClassificationResponse[]
}

/**
 * Lo que comparten POST /v1/contacts y PATCH /v1/contacts/{id}. El PATCH reemplaza la
 * representación completa, así que omitir un campo lo borra: quien cambie una sola cosa
 * manda igual el resto tal como vino.
 *
 * La empresa a la que pertenece el contacto **no** está acá: es un recurso aparte, la
 * afiliación (`CreateAffiliationRequest`), con su cargo y su departamento. Si `empresaId`
 * aparece en este tipo, deja de compilar.
 */
export interface ContactRequest {
  firstName: string
  lastName: string
  email?: string | null
  phone?: string | null
  /** Dónde trabaja: el formulario los toma de la empresa elegida y se pueden cambiar. */
  country?: string | null
  city?: string | null
  whatsApp?: string | null
  /** Uno de los códigos de `Idioma`; cualquier otro valor es un 400 de campo. */
  preferredLanguage?: Idioma | null
  /** Uno de los cuatro de `DecisionLevel`; nulo lo quita y cualquier otro valor es un 400. */
  decisionLevel?: DecisionLevel | null
  /**
   * El oficial responsable: el id de un `OptStaff` vigente de cualquier OPT (`GET /v1/opts`).
   * Nulo lo quita. Un id que no es personal vigente es 400 `responsible_officer.not_found`,
   * salvo que sea el que el registro ya tenía.
   */
  responsibleOfficerId?: string | null
  /**
   * La fuente de origen: el id de una vigente del catálogo. Nulo la quita. Una que no existe
   * o está retirada es 400 `origin_source.not_found`, salvo que sea la que ya tenía.
   */
  originSourceId?: string | null
}

export interface ListContactsParams {
  page?: number
  pageSize?: number
  /** Búsqueda en nombre y correo (substring). */
  search?: string
  /** Búsqueda por correo exacto (contra email_normalized). */
  email?: string
  /** Filtro por categoría de empresa (código). Repetible/Array. */
  companyCategory?: string | string[]
  /**
   * El `code` de una clasificación, no su id: coincidencia exacta, distinguiendo mayúsculas.
   * Varios códigos se repiten como `classification=A&classification=B` y se combinan con OR.
   */
  classification?: string | string[]
  /** Ids exactos, hasta 100 por llamada (400 si son más); uno inexistente o dado de baja no vuelve. */
  id?: string[]
  /** Sin él vienen todos los estados, suspendidos incluidos. Repetible/Array. */
  status?: ContactStatus | ContactStatus[]
}

// ---------- Afiliaciones empresa↔contacto (crm-service) ----------

/**
 * POST /v1/companies/{companyId}/contacts. Cargo y departamento son opcionales (hasta
 * 120 caracteres; vacío se guarda como null) y `startDate` es una fecha sin hora, `YYYY-MM-DD`. Un contacto tiene
 * una sola afiliación vigente por empresa: la segunda responde 409 `DB_CONFLICT`.
 */
export interface CreateAffiliationRequest {
  contactId: string
  jobTitle: string | null
  department: string | null
  startDate: string
}

// ---------- Listas de contactos (crm-service) ----------

/**
 * Una lista de `GET /v1/contact-lists`: un conjunto fijo de contactos que una OPT arma a mano,
 * no una búsqueda guardada. Solo se ven las de la OPT de quien pregunta; la de otra OPT es un
 * 404, y un usuario sin OPT recibe 403 `OPT_NOT_ASSIGNED` en todas las rutas. `memberCount`
 * cuenta solo contactos vivos: uno dado de baja sale de toda lista sin que nadie la toque.
 */
export interface ContactListResponse {
  id: string
  name: string
  memberCount: number
  createdAt: string
  createdById: string
  updatedAt: string
  updatedById: string
}

/** `GET /v1/contact-lists/{id}`: además, la OPT dueña y los ids de sus contactos vivos. */
export interface ContactListDetailResponse extends ContactListResponse {
  optOfficeId: string
  contactIds: string[]
}

// ---------- Interruptores de funciones (crm-service, docs/adr/0039) ----------

/**
 * Una fila guardada de `GET /v1/feature-switches`. Solo llegan las filas que existen: una clave
 * sin fila está encendida. `changedByName` es el nombre que traía el token al escribir, y los tres
 * campos del cambio vienen en null en una fila que sembró una migración.
 */
export interface FeatureSwitchResponse {
  key: string
  enabled: boolean
  changedById: string | null
  changedByName: string | null
  changedAt: string | null
}

/** El cuerpo de `PUT /v1/feature-switches/{key}`, que pide `feature-switches:write`. */
export interface SetFeatureSwitchRequest {
  enabled: boolean
}

// ---------- Eventos ----------

export interface CiudadResponse {
  id: string
  name: string
  country: string
  date?: string | null
  displayOrder?: number | null
  /** Id IANA. events-service lo devuelve desde ; hasta entonces falta. */
  timeZone?: string | null
}

/**
 * Un evento que el terminal de la puerta puede elegir (`GET /v1/events/door`, `check-in:read`):
 * los que pasan entre un día antes de empezar y un día después de terminar, sin los cancelados.
 * Solo estos campos, a propósito (spec convocatorias/10 §4).
 */
export interface DoorEventResponse {
  id: string
  name: string
  startDate: string
  endDate: string
  cities: CiudadResponse[]
}

export interface EventoImage {
  id: string
  storedFileId: string
  fileName: string
  contentType: string
  sizeBytes: number
  createdAt: string
  kind: 'Cover' | 'Gallery'
  missing?: boolean
}

/**
 * El montaje del salón (`seatingLayout` en events-service): decide si hay asientos y cómo se
 * reparten. Es opcional en todo tipo de evento; sin valor quiere decir que nadie lo eligió
 * todavía, que no es lo mismo que `Cocktail`. Los textos en español están en `montajeLabel`.
 */
export const MONTAJES_EVENTO = [
  'Banquet',
  'Auditorium',
  'Conference',
  'Cocktail',
  'UShape',
  'Classroom',
] as const
export type MontajeEvento = (typeof MONTAJES_EVENTO)[number]

export interface EventoDetail {
  id: string
  type: EventoType
  name: string
  startDate: string
  endDate: string
  status: EventoStatus
  visibility: EventoVisibility
  language?: Idioma | null
  externalUrl?: string | null
  invitedCount?: number | null
  description?: string | null
  estimatedBudget?: Money | null
  totalCost?: Money | null
  cancellationReason?: string | null
  cities: CiudadResponse[]
  images?: EventoImage[] | null
  /** (frontend) Código iframe o URL de Google Maps para la ubicación del venue */
  mapEmbedUrl?: string | null
  /** (frontend) Tipo de registro público: abierto o con whitelist previo */
  registrationType?: 'abierto' | 'whitelist' | null
  /** El montaje del salón; nulo cuando nadie lo eligió. */
  seatingLayout?: MontajeEvento | null
  createdAt: string
  updatedAt: string
}

export interface EventoListItem {
  id: string
  type: EventoType
  name: string
  status: EventoStatus
  visibility: EventoVisibility
  startDate: string
  endDate: string
  language?: Idioma | null
  invitedCount?: number | null
  /** (frontend) sin respaldo en events-service todavía. */
  estimatedBudget?: Money | null
  externalUrl?: string | null
  cover?: EventoImage | null
  totalCost?: Money | null
  /** El montaje del salón; nulo cuando nadie lo eligió. */
  seatingLayout?: MontajeEvento | null
  cities?: CiudadResponse[]
  createdAt: string
  updatedAt: string
}

export interface PagedResponse<T> {
  total: number
  page: number
  pageSize: number
  items: T[]
}

export interface ListEventosParams {
  type?: EventoType
  status?: EventoStatus
  active?: boolean
  visibility?: EventoVisibility
  startDateFrom?: string
  startDateTo?: string
  page?: number
  pageSize?: number
  searchTerm?: string
  sortField?: string
  sortOrder?: string
}

/**
 * Exactamente lo que acepta POST /v1/events. Los campos que la pantalla muestra pero el
 * servicio no conoce (portada, galería, mapa, tipo de registro) viven en el
 * estado del formulario y no viajan: si alguno reaparece acá, deja de compilar.
 */
export interface CreateEventoRequest {
  type: EventoType
  name: string
  startDate: string
  endDate: string
  language?: Idioma | null
  /** Sin valor el evento nace privado: nada sale a la cartelera sin que alguien lo decida. */
  visibility?: EventoVisibility | null
  externalUrl?: string | null
  invitedCount?: number | null
  city: string
  country: string
  /** La zona horaria de esa primera ciudad, id IANA. El servicio la ignora hasta . */
  timeZone: string
  estimatedBudget?: Money | null
  description?: string | null
  /** Sin valor el evento nace sin montaje del salón. Nunca `""`: el servicio lo rechaza. */
  seatingLayout?: MontajeEvento | null
}

/**
 * Exactamente lo que acepta PATCH /v1/events/{id}. Reemplaza la representación editable
 * completa, así que omitir un campo lo borra (ADR-0009). Por eso acá ningún campo es opcional,
 * aunque el servicio acepte varios ausentes: quien edite una sola cosa manda igual el resto tal
 * como vino, nulo solo para quitarlo, y el compilador encuentra a cualquier emisor que se olvide
 * uno. El tipo, la ciudad y el país no se editan por esta vía — las ciudades tienen sus propios
 * endpoints.
 */
export interface UpdateEventoRequest {
  name: string
  startDate: string
  endDate: string
  /**
   * Obligatorio. El PATCH reemplaza la representación editable completa, así que mandarlo nulo
   * (o no mandarlo) borraría el idioma guardado, y events-service lo rechaza con
   * `EVENT_LANGUAGE_REQUIRED`. Un evento sin idioma no tiene plantilla con la que convocar.
   */
  language: Idioma
  /**
   * Obligatorio acá aunque el servicio lo acepte ausente. Es el único campo del PATCH que omitir
   * no borra (lo deja como estaba), pero una pantalla que lo omite no puede despublicar nada:
   * quien edita manda siempre el valor, y el compilador encuentra a cualquier emisor nuevo.
   */
  visibility: EventoVisibility
  externalUrl: string | null
  invitedCount: number | null
  estimatedBudget: Money | null
  description: string | null
  seatingLayout: MontajeEvento | null
}

// ---------- Cartelera pública (convocatorias-service) ----------

/**
 * Una ciudad del recorrido tal como la ve un desconocido: sin id.
 */
export interface PublicCiudad {
  name: string
  country: string
  date?: string | null
  displayOrder?: number | null
}

/**
 * Un evento de la cartelera pública, `GET /v1/public/events` en convocatorias-service. Es una
 * vista recortada, no el evento: no trae estado, presupuesto, OPT, `invitedCount` ni imágenes.
 * La lista ya viene filtrada a los eventos públicos y activos.
 */
export interface PublicEvento {
  id: string
  name: string
  type: EventoType
  language: Idioma
  startDate: string
  endDate: string
  externalUrl?: string | null
  cities: PublicCiudad[]
  /** La ventana de inscripción de convocatorias. Una pista para la pantalla, no la guarda. */
  registrationOpen: boolean
}

/**
 * El detalle público, `GET /v1/public/events/{id}`. También sirve eventos cerrados, para que un
 * enlace compartido siga resolviendo; uno privado, cancelado o desconocido es un 404
 * `PUBLIC_EVENT_NOT_FOUND`, sin decir cuál.
 */
export interface PublicEventoDetail extends PublicEvento {
  description?: string | null
  /** Solo el nombre de la OPT. Nulo con el CRM caído, y entonces la página omite el bloque. */
  organizer?: string | null
  /** (frontend) Ver `EventoDetail.registrationType`: el mock lo conserva, el servicio no lo tiene. */
  registrationType?: 'abierto' | 'whitelist' | null
}

// ---------- Portal de registro (convocatorias-service, anónimo) ----------
// Las páginas a las que llevan los correos: `/publico/registro#t=`, `/publico/baja#u=` y
// `/publico/entrada/{qrCode}` (spec convocatorias/06).

/** `POST /v1/public/registrations/resolve`: el evento del enlace, y quién es solo si es privado. */
export interface ResolvedInvitationResponse {
  event: PublicEventoDetail
  /** Solo en un evento privado: un token público se reenvía y no puede enseñar al invitado. */
  prefill?: InvitationPrefill | null
}

export interface InvitationPrefill {
  firstName: string
  lastName: string
  email: string
}

/** `POST /v1/public/registrations`. Sin token solo se acepta en un evento público. */
export interface PublicRegisterRequest {
  eventId: string
  token?: string | null
  firstName: string
  lastName: string
  email: string
  phone?: string | null
  company?: string | null
  jobTitle?: string | null
  country?: string | null
}

/** 201 en una inscripción nueva, 200 con los mismos códigos en un reenvío. */
export interface PublicRegistrationResponse {
  /** `REF-<año>-<secuencia>`: legible, sin autoridad. */
  reference: string
  /** Lo que codifica el QR y la llave de la entrada. Nunca se muestra como texto. */
  qrCode: string
  event: PublicEventoDetail
}

/** `GET /v1/public/registrations/{qrCode}`: la entrada. */
export interface RegistrationTicketResponse {
  reference: string
  name: string
  company?: string | null
  event: PublicEventoDetail
}

/** `POST /v1/public/unsubscribe`: la dirección dada de baja, enmascarada (`a***@acme.com`). */
export interface UnsubscribeResponse {
  maskedEmail: string
}

export interface CiudadRequest {
  name: string
  country: string
  date?: string | null
  displayOrder?: number | null
  /**
   * Id IANA, como `America/Chicago`. Obligatorio: el PATCH lo exige desde y así el
   * compilador encuentra a quien no lo mande.
   */
  timeZone: string
}

// ---------- Proveedores ----------

export interface Contacto {
  name: string
  lastName?: string | null
  email: string
  phone?: string | null
}

export interface ProveedorFoto {
  id: string
  storedFileId: string
  fileName: string
  contentType: string
  sizeBytes: number
  createdAt: string
  missing?: boolean
}

/**
 * La empresa del CRM que un proveedor referencia, tal como events-service la resuelve
 * (ADR-0015). `name`/`country`/`city`/`isActive` viajan en `null` juntos cuando el
 * directorio no estaba disponible al armar la respuesta — nunca una ficha rota, una
 * fila que sigue ahí sin nombre. `isActive` es la relación comercial que el CRM lleva
 * con la empresa; no tiene nada que ver con `isHireable` del proveedor.
 */
export interface CompanyRefResponse {
  id: string
  name: string | null
  country: string | null
  city: string | null
  isActive: boolean | null
}

/** Un contacto de la empresa contratada, resuelto en vivo — solo en la ficha del proveedor. */
export interface CompanyContactResponse {
  id: string
  name: string
  email?: string | null
  phone?: string | null
  jobTitle: string | null
}

export interface ProveedorDocument {
  id: string
  name: string
  size?: string
  type: string
  url?: string | null
  uploadedAt: string
}

export interface ProveedorDetail {
  id: string
  category: CategoriaProveedor
  /**
   * Las otras categorías del proveedor, sin la principal (ADR-0034). Lo encuentran el listado y
   * el pool de cada una. Vacía cuando no tiene.
   */
  secondaryCategories?: CategoriaProveedor[]
  /**
   * `true` salvo que se apague explícitamente ("no volver a contratar"): saca al
   * proveedor del pool de candidatos futuros sin borrar su ficha ni su historial.
   */
  isHireable: boolean
  /**
   * Por qué se dejó de contratar, si se dio motivo. Siempre `null` con `isHireable` en `true`:
   * el servicio lo borra al volver a habilitar el proveedor. Solo lo trae la ficha, no el listado.
   */
  unhireableReason?: string | null
  /**
   * El promedio de sus evaluaciones, de 1 a 5 con un decimal, que calcula y redondea
   * events-service: cada evaluación aporta el promedio de sus cuatro notas, una por evento.
   * `null` sin evaluaciones, nunca cero. Lo traen también el listado y el pool.
   */
  averageScore?: number | null
  /** Cuántas evaluaciones cubre `averageScore`; `0` sin evaluaciones. */
  evaluationCount?: number
  /**
   * La empresa del CRM que se contrata, resuelta por events-service (ADR-0015): id, nombre,
   * país, ciudad y su relación comercial. `name`/`country`/`city`/`isActive` en `null`
   * significa que el directorio no resolvió, no una ficha incompleta. El proveedor apunta a
   * la empresa por `company.id`: el servicio no manda un `companyId` suelto.
   */
  company: CompanyRefResponse
  /**
   * (frontend) Nombre comercial, RNC, sitio web, dirección y mapa: en modo mock los guarda la
   * presentación. En http no llegan nunca: la ficha los lee de la empresa del CRM
   * (`GET /v1/companies/{id}`), que es su dueña (ADR-0015). La descripción no se muestra.
   */
  commercialName?: string | null
  rnc?: string | null
  websiteUrl?: string | null
  description?: string | null
  /** (frontend) El contacto principal de la demo: solo lo trae el mock, el servicio no. */
  contact?: Contacto | null
  /**
   * Los contactos de la empresa en el CRM (ADR-0015), que events-service resuelve en vivo solo
   * en esta lectura. `null` es "el CRM no respondió" y `[]` es "nadie registrado": la ficha los
   * muestra distinto. En mock son los que la presentación fue guardando.
   */
  contacts?: CompanyContactResponse[] | null
  address?: string | null
  locationMapUrl?: string | null
  comments?: string | null
  /**
   * Aforo máximo en montaje de auditorio; solo lo lleva un Venue. La ficha lo muestra en el
   * atributo "Aforo máximo" pero no lo edita: se reenvía intacto al editar (ver
   * `UpdateProveedorRequest`).
   */
  auditoriumCapacity?: number | null
  /** Aforo máximo en montaje de cóctel. Mismas reglas que `auditoriumCapacity`. */
  cocktailCapacity?: number | null
  photos: ProveedorFoto[]
  /**
   * (frontend) Los enlaces de "Documentos (URLs)" del formulario: solo los guarda el mock. Los
   * documentos que lista la ficha son los archivos de la empresa en el CRM (ADR-0033).
   */
  documents?: ProveedorDocument[]

  /** (frontend) Especificaciones técnicas dinámicas según la categoría del proveedor */
  technicalSpecs?: Record<string, unknown>
  /**
   * El catálogo de servicios, en el orden en que se guardó. Solo la ficha lo trae: el listado y el
   * pool no, así que quien lo necesita lee este detalle (`useServiciosDelProveedor`).
   */
  services?: ProveedorServiceItem[]

  createdAt: string
  updatedAt: string
}

/**
 * Los campos del contrato que la ficha llena, comunes al alta y a la edición. El proveedor
 * ya no lleva identidad propia: `companyId` apunta a la empresa del CRM que la tiene, y
 * events-service la valida en vivo contra crm-service antes de escribir.
 */
export interface ProveedorFichaRequest {
  companyId: string
  comments?: string | null
  /**
   * Las otras categorías, sin la principal (ADR-0034): repetirla es 400
   * `SUPPLIER_SECONDARY_CATEGORY_IS_MAIN`, y una que otra ficha de la misma empresa ya cubre es
   * 409 `SUPPLIER_COMPANY_ALREADY_REGISTERED`. Obligatoria en la edición: el PATCH reemplaza, así
   * que omitirla las borra.
   */
  secondaryCategories: CategoriaProveedor[]
  /**
   * El catálogo completo. El PATCH lo reemplaza y omitirlo lo vacía, igual que las secundarias:
   * toda edición lo reenvía (`toUpdateRequest`).
   */
  services: ProveedorServiceRequest[]
}

/**
 * Exactamente lo que acepta PATCH /v1/service-suppliers/{id}. Reemplaza la representación
 * editable completa, así que omitir un campo lo borra: quien cambie una sola cosa manda
 * igual el resto tal como vino.
 */
export interface UpdateProveedorRequest extends ProveedorFichaRequest {
  /**
   * Los aforos se fijan por API (Bruno, una herramienta futura) porque la ficha no los
   * muestra. Van en el payload justamente por eso: como el PATCH reemplaza todo, omitirlos
   * haría que una edición desde la pantalla borre una cifra que la pantalla no conoce.
   */
  auditoriumCapacity?: number | null
  cocktailCapacity?: number | null
  /**
   * Obligatorio: omitirlo es 400. `false` significa "no volver a contratar" — nunca se
   * adivina, así que toda edición manda el valor actual salvo que el toggle lo cambie.
   */
  isHireable: boolean
  /**
   * El motivo de "No usar en el futuro". Solo se guarda con `isHireable` en `false`; con `true`
   * el servicio lo descarta. Como todo el PATCH reemplaza, omitirlo borra el que hubiera.
   */
  unhireableReason?: string | null
}

// ---------- Convocatorias (convocatorias-service) ----------

/** Una fila del catálogo de plantillas. Fase 1: solo lectura, nadie la edita. */
export interface TemplateListItem {
  templateKey: string
  language: string
  /** `campaign` o `transactional`. */
  purpose: string
  /** Si una campaña la puede elegir como `templateKey`. */
  isCampaignSelectable: boolean
  subject: string
}

/** `draft → sending → sent | cancelled`. No hay `pending_approval`: ADR-0017 lo retiró. */
export type CampaignState = 'draft' | 'sending' | 'sent' | 'cancelled'

export interface CampaignResponse {
  id: string
  /** Nulo en un mensaje directo, que no convoca a ningún evento. */
  eventId: string | null
  /** Asunto y texto de un mensaje directo; nulos en una campaña de evento. */
  subject?: string | null
  message?: string | null
  templateKey: string
  language: string
  state: CampaignState
  /** Cuántos destinatarios lleva. Es el número que se manda al lanzar. */
  recipientCount: number
  /** Cuántas personas quedaron fuera al seleccionar. */
  exclusionCount: number
  launchedById?: string | null
  launchedAt?: string | null
  /** Lo estampa el worker de la entrega; sigue nulo justo después de lanzar. */
  sentAt?: string | null
  createdAt: string
  updatedAt: string
}

/**
 * Una pasada del aviso de cancelación (spec `06` §4.7). Volver a pedirlo es seguro: solo les
 * escribe a los inscritos que siguen sin aviso, así que `failed` es lo que otra pasada reintenta.
 */
export interface CancellationNoticeResponse {
  eventId: string
  notified: number
  failed: number
}

/**
 * Sin evento es un mensaje directo: plantilla `direct_message`, con asunto (máx. 200) y texto
 * (máx. 10.000). Con evento no lleva ninguno de los dos. Romperlo es 400
 * `CAMPAIGN_DIRECT_MESSAGE_MISMATCH`.
 */
export interface CreateCampaignRequest {
  eventId: string | null
  templateKey: string
  language: string
  subject?: string
  message?: string
}

/** PATCH de un borrador: plantilla e idioma, y el asunto y el texto solo de un mensaje directo. */
export interface UpdateCampaignRequest {
  templateKey?: string
  language?: string
  subject?: string
  message?: string
}

export interface ListCampaignsParams {
  page?: number
  pageSize?: number
  eventId?: string
  state?: CampaignState
}

/**
 * El filtro con el que se construye la lista desde crm-service. Repetible dentro de
 * cada dimensión (O), y las dos dimensiones se combinan con Y. Ninguna de las dos
 * trae el directorio entero — una convocatoria masiva legítima.
 */
export interface SelectRecipientsRequest {
  classifications?: string[]
  companyCategories?: string[]
  /**
   * Contactos elegidos a mano, de 1 a 1.000. Va sin los dos filtros. Un id que el CRM no
   * devuelve no entra y no cuenta en `matched`.
   */
  contactIds?: string[]
  /**
   * Una lista de contactos del CRM de la OPT de quien lanza; entran sus miembros vivos. Va sola.
   * Sin OPT es 403 `OPT_NOT_ASSIGNED`, y una lista de otra OPT o inexistente es 404
   * `CONTACT_LIST_NOT_FOUND`.
   */
  contactListId?: string
}

export interface RecipientSelectionResponse {
  /** Contactos que devolvió el CRM para ese filtro. */
  matched: number
  /** Destinatarios nuevos que escribió esta selección. */
  added: number
  /** Exclusiones nuevas que escribió esta selección. */
  excluded: number
  /** Contactos que ya estaban en la campaña y no se volvieron a escribir. */
  skipped: number
  /** Por código: `no_email`, `opted_out`, `do_not_invite`, `contact_unavailable`. */
  excludedByReason: Record<string, number>
  campaign: CampaignResponse
}

export interface LaunchCampaignRequest {
  /** Los destinatarios que quien lanza tenía en pantalla. Obligatorio: omitirlo es 400. */
  expectedRecipientCount: number
}

export type UploadRowBucket =
  | 'matched'
  | 'unmatched'
  | 'suppressed'
  | 'already_a_recipient'
  | 'duplicate_in_file'
  | 'malformed'
  | 'collapsed_exact_duplicates'

export interface RecipientUploadRow {
  sourceRowNumber: number
  firstName?: string | null
  lastName?: string | null
  email?: string | null
  phone?: string | null
  company?: string | null
  bucket: UploadRowBucket
  reasonCode?: string | null
  contactId?: string | null
  duplicateRowNumbers?: number[] | null
  collapsedDuplicates?: number | null
}

export interface UploadCounts {
  matched: number
  unmatched: number
  suppressed: number
  alreadyARecipient: number
  duplicateInFile: number
  malformed: number
  collapsedExactDuplicates: number
}

export interface RecipientUploadResponse {
  id: string
  campaignId: string
  originalFileName: string
  detectedEncoding: string
  rowCount: number
  counts: UploadCounts
  confirmableCount: number
  uploadedAt: string
  uploadedById: string
  confirmedAt?: string | null
  confirmedById?: string | null
  /** Solo en la respuesta de `stage`: la lista fila por fila para la pantalla de revisión. */
  rows?: RecipientUploadRow[]
}

/**
 * El resumen de un evento (spec convocatorias `07` §2). Nunca responde 404: un evento sin
 * campañas lanzadas devuelve ceros. No trae aperturas, rebotes ni bajas: no se sirven.
 */
export interface EventTrackingResponse {
  /** Solo las lanzadas (`launchedAt` puesto); nunca un borrador. */
  campaigns: TrackingCampaign[]
  people: { invited: number; pendingSend: number; failed: number; suppressed: number }
  registrations: { total: number; fromInvitation: number; referred: number; walkIn: number }
  /** Invitados sin inscripción. Nunca cuenta entradas libres. */
  notRegistered: number
  needsAttention: number
  registrationWindow: { open: boolean; closedAt?: string | null; closedById?: string | null }
}

/**
 * La ventana de inscripción de un evento, tal como la devuelven `close-registrations` y
 * `reopen-registrations` (spec convocatorias `06` §5.2). Un evento sin fila está abierto.
 */
export interface RegistrationWindowResponse {
  eventId: string
  open: boolean
  closedAt?: string | null
  /** Quién la cerró la última vez. Se conserva al reabrir, como registro. */
  closedById?: string | null
  reopenedAt?: string | null
  reopenedById?: string | null
}

export interface TrackingCampaign {
  id: string
  templateKey: string
  /** `sending`, `sent` o `cancelled`. */
  state: CampaignState
  sentAt?: string | null
  recipientCount: number
  exclusionCount: number
}

// ---------- Participantes de un evento (convocatorias-service, spec 07 §3) ----------

export type ParticipantOrigin = 'invited' | 'referred' | 'walk_in'
export type ParticipantSendStatus = 'handed_off' | 'pending' | 'failed' | 'suppressed' | 'none'

/**
 * Qué pasó con un correo ya entregado al proveedor, leído de notification-service.
 * Sin eventos del proveedor (Mailpit, o notification-service caído) se queda en `sent`.
 */
export type CampaignDeliveryStatus =
  'sent' | 'delivered' | 'bounced' | 'spam_report' | 'unsubscribed'

/** Filtros de `GET /events/{eventId}/participants` (spec 07 §3.3). Tope de página: 100. */
export interface ListParticipantsParams {
  page?: number
  pageSize?: number
  origin?: ParticipantOrigin
  sendStatus?: ParticipantSendStatus
  registered?: boolean
  search?: string
  /** Solo los destinatarios de esa campaña, borrador incluido. */
  campaignId?: string
}

/**
 * Una persona por evento: los destinatarios de todas sus campañas más las inscripciones, unidos
 * por el correo. Tres ejes que no se derivan entre sí: `origin`, `sendStatus` y `registered`.
 * No trae entregado, abierto ni rebotado (spec 07 §5).
 */
export interface ParticipantResponse {
  email: string
  firstName: string
  lastName: string
  company?: string | null
  origin: ParticipantOrigin
  sendStatus: ParticipantSendStatus
  registered: boolean
  registeredAt?: string | null
  reference?: string | null
  /** Solo en una fila `referred`, y solo el nombre de quien reenvió. */
  referredBy?: { name: string } | null
  campaignIds: string[]
  crmSyncPending: boolean
  /** La inscripción trae datos distintos de los del CRM: la reconciliación manual. */
  detailsDiffer: boolean
  /** Solo en la lista de una campaña y en una fila entregada al proveedor; si no, no viaja. */
  deliveryStatus?: CampaignDeliveryStatus
}

/**
 * Un evento al que asistió un contacto del CRM (`GET /v1/convocatorias/check-ins?contactId=`):
 * alguna inscripción suya en ese evento tiene check-in. Estar invitado o inscrito no cuenta. El
 * nombre y el lugar del evento no viajan: se leen de events-service por `eventId`.
 */
export interface ContactCheckInResponse {
  contactId: string
  eventId: string
  /** Cuándo entró, ISO: el día del evento en que estuvo. */
  checkedInAt: string
}

/**
 * La respuesta de un check-in, por QrCode o por inscripción (spec convocatorias/09 §3.1): la misma
 * con 201 la primera vez y con 200 después, cuando `checkedInAt` es la hora del primero. No trae
 * correo a propósito.
 */
export interface CheckInResponse {
  registrationId: string
  firstName: string
  lastName: string
  company: string | null
  reference: string
  checkedInAt: string
  checkedInById: string
  alreadyCheckedIn: boolean
}

/** Una fila de la lista de la puerta (spec convocatorias/09 §5.1). Nunca trae el correo. */
export interface DoorListItem {
  registrationId: string
  firstName: string
  lastName: string
  company: string | null
  reference: string
  checkedInAt: string | null
}

/** Los totales del evento, sin filtros: el pie del escáner (*Entradas: 287/412*). */
export interface DoorListTotals {
  registered: number
  checkedIn: number
}

export interface DoorListResponse extends PagedResponse<DoorListItem> {
  totals: DoorListTotals
}

export interface ListDoorRegistrationsParams {
  /** Cada palabra en el nombre, el apellido o la Reference; o el correo exacto. */
  search?: string
  checkedIn?: boolean
  page?: number
  pageSize?: number
}

/**
 * Una campaña lanzada que tuvo a un contacto del CRM en su lista
 * (`GET /v1/convocatorias/campaign-recipients?contactId=`). El nombre del evento no viaja: se lee
 * de events-service por `eventId`.
 */
export interface ContactCampaignResponse {
  contactId: string
  campaignId: string
  /** Nulo en un mensaje directo. */
  eventId: string | null
  /** El asunto de un mensaje directo; nulo en una campaña de evento. */
  subject: string | null
  /** Cuándo se lanzó la campaña, ISO: la fecha del envío. */
  launchedAt: string
  sendStatus: Exclude<ParticipantSendStatus, 'none'>
  /** Si se inscribió en el evento; nulo sin evento, que no tiene en qué inscribirse. */
  registered: boolean | null
  /** Nulo si no se entregó al proveedor: no hay correo del que decir nada. */
  deliveryStatus: CampaignDeliveryStatus | null
}

/** El CSV que arma el servicio, con el nombre de su `Content-Disposition`. */
export interface ParticipantsCsvFile {
  blob: Blob
  fileName: string
}

/** Alguien que la selección dejó fuera de una campaña (spec 07 §6.4). */
export interface CampaignExclusionRow {
  id: string
  contactId: string
  firstName: string
  lastName: string
  email?: string | null
  /** `no_email`, `opted_out`, `do_not_invite` o `contact_unavailable`. */
  reason: string
  excludedAt: string
}

// ---------- Historial de envíos (notification-service, spec notificaciones 05) ----------

/** Los siete estados de entrega del servicio. `dropped` es el que la pantalla llama suprimido. */
export const NOTIFICATION_DELIVERY_STATUSES = [
  'queued',
  'sent',
  'delivered',
  'bounced',
  'dropped',
  'deferred',
  'failed',
] as const

export type NotificationDeliveryStatus = (typeof NOTIFICATION_DELIVERY_STATUSES)[number]

/** Lo que hizo el destinatario con el correo, aparte de si llegó. */
export type NotificationRecipientAction = 'none' | 'unsubscribed' | 'complained'

/** Una entidad de la plataforma de la que trata el envío. Ids, nunca nombres. */
export interface NotificationAbout {
  sourceService: string
  entityType: string
  entityId: string
}

/**
 * Una fila del historial: un destinatario de un envío, con los datos del envío repetidos. Con
 * `deliveryTrackingAvailable` en falso, el estado nunca pasa de `sent`.
 */
export interface NotificationHistoryItem {
  id: string
  batchId: string
  /** El tipo que declaró quien envió, p. ej. `events.quote.submitted`. Abierto: puede llegar uno nuevo. */
  type: string
  subject: string
  language: string
  fromAddress: string
  fromName?: string | null
  email: string
  /** El nombre con que se envió. Puede faltar: un envío sin nombre, o uno anterior al campo. */
  name?: string | null
  deliveryStatus: NotificationDeliveryStatus
  recipientAction: NotificationRecipientAction
  deliveryTrackingAvailable: boolean
  queuedAt: string
  sentAt?: string | null
  attemptCount: number
  lastError?: string | null
  sgMessageId?: string | null
  smtpQueuedAs?: string | null
  about: NotificationAbout[]
}

/** Un envío entero, con el cuerpo sin sustituir y todos sus destinatarios. */
export interface NotificationBatch {
  id: string
  type: string
  subject: string
  body: string
  language: string
  fromAddress: string
  fromName?: string | null
  deliveryTrackingAvailable: boolean
  bodySha256: string
  createdAt: string
  about: NotificationAbout[]
  recipients: NotificationHistoryItem[]
}

/**
 * Filtros de `GET /v1/notifications`. `email` es exacto; `sentFrom` y `sentTo` son instantes ISO
 * sobre la fecha en que la plataforma aceptó el envío; `entityType` y `entityId` van juntos.
 * Página de 25 por defecto, tope 100.
 */
export interface ListNotificationsParams {
  page?: number
  pageSize?: number
  type?: string
  deliveryStatus?: NotificationDeliveryStatus
  recipientAction?: NotificationRecipientAction
  email?: string
  entityType?: string
  entityId?: string
  sourceService?: string
  sentFrom?: string
  sentTo?: string
}

// ---------- Sorteos (events-service, specs raffles/00–03) ----------
// Un sorteo por evento: toda ruta cuelga de `/v1/events/{eventId}/raffle`, y no hay lectura por id
// de sorteo. Los nombres y fechas del evento no viven aquí: los listados los traen unidos al leer y
// el detalle los lee del evento (ADR-0013).

export type RaffleState = 'draft' | 'in_progress' | 'completed'

/** Cómo se muestra en pantalla. No existe `tombola`: el valor heredado se escribe `ball_tombola`. */
export type RaffleVisualStyle = 'roulette' | 'ball_tombola' | 'sequential_names'

/** Lo que impide arrancar. Solo en `draft`. */
export type RaffleBlockingReason =
  | 'no_qualified_attendees'
  | 'attendance_unavailable'
  | 'attendance_too_many_rows'
  | 'crm_unavailable'

/** Lo que conviene saber sin que frene. */
export type RaffleWarning = 'awards_list_not_in_force' | 'attendance_copy_stale' | 'no_prizes'

/** No se guarda: sale de los sorteos del premio y de sus entregas. */
export type PrizeState = 'undrawn' | 'drawn' | 'redeemed'

export type PrizeDrawStatus = 'drawn' | 'redeemed' | 'voided'

export type PrizeDrawVoidCode = 'absent' | 'declined' | 'ineligible' | 'other'

/** Cómo terminó el correo al ganador; nulo mientras no se intentó. */
export type WinnerNotificationStatus = 'sent' | 'no_recipient' | 'failed'

/** Una versión del acta notarial. Los bytes se leen de `GET /v1/files/{storedFileId}/content`. */
export interface NotarialRecordResponse {
  version: number
  storedFileId: string
  generatedAt: string
  generatedById: string
}

export interface PrizeDrawVoidSummary {
  code: PrizeDrawVoidCode
  note: string | null
  voidedAt: string
  /** Quién anuló, para la bitácora. Opcional hasta que el servicio lo sirva. */
  voidedByName?: string | null
}

/** Una entrega, viva o revertida. Viva es la que no tiene `reversedAt`. */
export interface PrizeHandoverSummary {
  id: string
  receiverName: string
  receiverIdentityDocument: string
  receiverIsWinner: boolean
  signatureStoredFileId: string
  redeemedAt: string
  redeemedById: string
  /** Quién registró la entrega, para la bitácora. Opcional hasta que el servicio lo sirva. */
  redeemedByName?: string | null
  reversedAt: string | null
  reversedById: string | null
  reversalReason: string | null
}

/** Un sorteo de premio. El ganador vivo es el que tiene `void` nulo. Sin correo. */
export interface PrizeDrawSummary {
  id: string
  winnerAttendeeId: string
  winnerName: string
  winnerCompany: string | null
  /**
   * Lo que el tablero de ganadores muestra junto al nombre (plans/agente-a §A33). Opcionales hasta
   * que el servicio los sirva; sin ellos el tablero cae al nombre y la empresa.
   */
  winnerTicketNumber?: string | null
  winnerEmployeeCode?: string | null
  winnerDepartment?: string | null
  /** Del padrón, para los campos visibles (§10). Opcionales hasta que el servicio los sirva. */
  winnerLocation?: string | null
  winnerFirstName?: string | null
  winnerLastName?: string | null
  drawnAt: string
  /** Quién sorteó, para la bitácora. Opcional hasta que el servicio lo sirva. */
  drawnByName?: string | null
  eligiblePoolSize: number
  /** Las dos reglas con que se sorteó: cambian a mitad del sorteo y aplican desde el próximo. */
  onePrizePerPerson: boolean
  onePrizePerCompany: boolean
  redemptionCode: string
  status: PrizeDrawStatus
  void: PrizeDrawVoidSummary | null
  handovers: PrizeHandoverSummary[]
  winnerNotificationStatus: WinnerNotificationStatus | null
  winnerNotificationId: string | null
}

/** Un premio. El valor es texto tal como lo escribió el operador, no un monto. */
export interface PrizeResponse {
  id: string
  roundId: string
  name: string
  description: string | null
  value: string | null
  /** La foto del premio (A34). Premios iguales comparten la misma. Opcional hasta que el servicio la sirva. */
  imageUrl?: string | null
  /**
   * El patrocinador, o `null` si no tiene. Sin CRM llega como texto: `companyId` nulo y el nombre
   * en `companyName`. Opcional hasta que el servicio lo sirva.
   */
  sponsor?: PrizeSponsorResponse | null
  createdAt: string
  state: PrizeState
  /** Vivos y anulados, del más nuevo al más viejo. */
  draws: PrizeDrawSummary[]
}

/** El patrocinador de un premio. Sin CRM, solo `companyName` (el texto que escribió el operador). */
export interface PrizeSponsorResponse {
  companyId: string | null
  companyName: string | null
  contactId: string | null
  contactName: string | null
}

export interface RaffleRoundResponse {
  id: string
  number: number
  name: string
  description: string | null
  prizes: PrizeResponse[]
}

export interface RaffleResponse {
  id: string
  eventId: string
  state: RaffleState
  visualStyle: RaffleVisualStyle
  showPrizeOnScreen: boolean
  animationSeconds: number
  requiresPresenceConfirmation: boolean
  /**
   * Si solo entran a la tómbola quienes hicieron check-in (A35). Ausente = sí, la regla de siempre.
   * Apagado, entra todo el padrón activo.
   */
  requiresCheckIn?: boolean
  onePrizePerPerson: boolean
  onePrizePerCompany: boolean
  notaryName: string | null
  notaryIdentityDocument: string | null
  witnessOneName: string | null
  witnessOneIdentityDocument: string | null
  witnessTwoName: string | null
  witnessTwoIdentityDocument: string | null
  startedAt: string | null
  startedById: string | null
  awardsBatchId: string | null
  pickupInstructions: string | null
  /** Los firmantes que el certificado de premio propone, en el orden de sus líneas; 0 a 2. */
  certificateSigners: CertificateSigner[]
  completedAt: string | null
  completedById: string | null
  /** De la más nueva a la más vieja; vacía hasta completar. */
  notarialRecords: NotarialRecordResponse[]
  rounds: RaffleRoundResponse[]
  /** Nulo fuera de `draft`. */
  isReadyToStart: boolean | null
  /** Nulo fuera de `draft`. */
  blockingReasons: RaffleBlockingReason[] | null
  warnings: RaffleWarning[]
  /**
   * Los anulados que un administrador volvió a dejar entrar al bombo. Un anulado (por cualquier
   * motivo, también por no presentarse) queda fuera de los premios siguientes hasta estar aquí;
   * volver a anularlo lo saca de nuevo. Opcional hasta que el servicio lo sirva.
   */
  reinstatedAttendeeIds?: string[]
  /** Qué datos del ganador se muestran en pantalla (§10). Opcional hasta que el servicio lo sirva. */
  displayFields?: RaffleDisplayFields
}

export type DisplayNameMode = 'full_name' | 'first_and_last' | 'first_name'
export type DisplayIdentifierMode = 'ticket_number' | 'employee_code' | 'none'
export type DisplayDetailMode = 'department' | 'location' | 'company' | 'none'

/** `PUT …/raffle/display-fields`: solo en borrador; después queda fija, como la tómbola. */
export interface RaffleDisplayFields {
  name: DisplayNameMode
  identifier: DisplayIdentifierMode
  detail: DisplayDetailMode
}

/** Un firmante del certificado de premio: el nombre y el cargo que se imprimen bajo su línea. */
export interface CertificateSigner {
  name: string
  title: string
}

/**
 * Los firmantes del certificado, enteros (ADR-0009): reemplazan a los guardados y una lista vacía
 * los borra. Se arma solo con `toSetCertificateSignersRequest`.
 */
export interface SetCertificateSignersRequest {
  signers: CertificateSigner[]
}

/**
 * La representación editable completa del sorteo (ADR-0009): lo opcional omitido se borra. Se
 * arma solo con `toUpdateRaffleRequest` (`features/sorteo/raffleRequest.ts`).
 */
export interface UpdateRaffleRequest {
  visualStyle: RaffleVisualStyle
  showPrizeOnScreen: boolean
  animationSeconds: number
  requiresPresenceConfirmation: boolean
  requiresCheckIn: boolean
  onePrizePerPerson: boolean
  onePrizePerCompany: boolean
  notaryName: string | null
  notaryIdentityDocument: string | null
  witnessOneName: string | null
  witnessOneIdentityDocument: string | null
  witnessTwoName: string | null
  witnessTwoIdentityDocument: string | null
  pickupInstructions: string | null
}

/** El número no viaja: lo asigna el servicio. La descripción omitida se borra al editar. */
export interface RaffleRoundRequest {
  name: string
  description: string | null
}

/** `quantity` es cuántas filas idénticas crear; el servicio las expande y devuelve todas. */
/**
 * Premios de un archivo en una sola petición y una sola transacción: el servicio crea las rondas
 * que falten por número (`Ronda N`) y rechaza todo si algo no pasa (hasta 10.000 premios).
 */
export interface ImportPrizesRequest {
  prizes: {
    roundNumber: number
    name: string
    value: string | null
    quantity: number
    /** Texto libre, opcional: en blanco o nulo es sin patrocinador. */
    sponsorName?: string | null
  }[]
}

export interface ImportPrizesResponse {
  created: number
  roundsCreated: number
}

export interface CreatePrizesRequest {
  roundId: string
  name: string
  description: string | null
  value: string | null
  quantity: number
  /** El patrocinador como texto libre, opcional. */
  sponsorName?: string | null
}

/** La ronda es obligatoria: cambiarla mueve el premio. */
export interface UpdatePrizeRequest {
  roundId: string
  name: string
  description: string | null
  value: string | null
  /** Reemplaza al guardado (ADR-0009): omitirlo o mandarlo nulo quita el patrocinador. */
  sponsorName?: string | null
}

/** Una fila de `GET /v1/raffles`. `drawnCount` cuenta los premios con ganador vivo. */
export interface RaffleListItem {
  raffleId: string
  eventId: string
  eventName: string
  /** `YYYY-MM-DD`. */
  eventStartDate: string
  state: RaffleState
  prizeCount: number
  drawnCount: number
  redeemedCount: number
  startedAt: string | null
  completedAt: string | null
}

export interface RaffleTotals {
  raffles: number
  /** Los sorteos `in_progress`. */
  active: number
  prizes: number
  /** Los sorteos de premio vivos. */
  winners: number
  redeemed: number
  /** Los calificados de los bombos congelados; un borrador no aporta. */
  qualifiedParticipants: number
}

/**
 * Por qué un asistente no califica, en el orden en que se evalúa (spec raffles/01 §6). Los cuatro
 * primeros no los vence ninguna decisión manual.
 */
export type RaffleExclusionReason =
  | 'walk_in'
  | 'contact_unresolved'
  | 'suspended'
  | 'do_not_invite'
  | 'manually_excluded'
  | 'not_awards_member'
  | 'dominican_domain'

export type AttendeeOverrideDecision = 'included' | 'excluded'

/** De dónde sale la lista de la puerta: en vivo, una copia anterior, o ninguna. */
export type AttendanceStatus = 'ok' | 'stale' | 'unavailable'

/** Un asistente, calificado o con el único motivo por el que no. */
export interface RaffleAttendeeResponse {
  /** Nulo si la persona entró solo con su ID. */
  email: string | null
  /** La clave interna de la persona en el evento (el correo, o `id:` + su ID). No se muestra. */
  identityKey: string
  firstName: string
  lastName: string
  company: string | null
  qualified: boolean
  exclusionReason: RaffleExclusionReason | null
  /** Haya ganado o no; nulo sin decisión manual. */
  manualDecision: AttendeeOverrideDecision | null
  /** Lo que la decisión manual venció, o lo que la venció a ella. */
  overruledReason: RaffleExclusionReason | null
  manualNote: string | null
  manualDecidedById: string | null
  manualDecidedAt: string | null
}

/**
 * `GET …/raffle/attendees`: las dos listas en una lectura. Sin ninguna copia de la puerta,
 * `checkedIn`, `qualified` y `attendees` son nulos: no se sabe quién está, que no es nadie.
 * Después de `start` es la lista congelada, con `attendanceFetchedAt` igual a `startedAt`.
 */
export interface RaffleAttendeesResponse {
  checkedIn: number | null
  qualified: number | null
  attendanceFetchedAt: string | null
  attendanceStatus: AttendanceStatus
  awardsBatchId: string | null
  attendees: RaffleAttendeeResponse[] | null
}

/** La clave va en el cuerpo, nunca en la ruta. `POST` reemplaza la decisión anterior. */
export interface SetAttendeeOverrideRequest {
  /** La `identityKey` del asistente: con ella se decide también sobre quien no tiene correo. */
  identityKey: string
  decision: AttendeeOverrideDecision
  note: string | null
}

/**
 * `POST …/attendees/overrides/bulk-exclude`: exactamente una fuente. `sourceEventId` excluye a los
 * ganadores vivos del sorteo de ese evento; `identifiers`, a una lista de correos o IDs (hasta 5000).
 */
export interface BulkExcludeAttendeesRequest {
  sourceEventId: string | null
  identifiers: string[] | null
  note: string | null
}

/** Lo que hizo la exclusión en lote. Quien no se encontró se informa; no es un error. */
export interface BulkExcludeAttendeesResponse {
  /** Exclusiones nuevas, o inclusiones que pasaron a exclusión. */
  excluded: number
  /** Ya estaban excluidas; no se tocaron. */
  alreadyExcluded: number
  /** No están en el padrón o en la puerta de este evento. */
  notInRoster: string[]
  /** No parecen correo y ningún participante tiene ese ID. */
  invalid: string[]
  /** Solo con `sourceEventId`: ganadores sin correo ni ID, por su nombre. */
  unidentifiable: string[]
}

/** Un participante del bombo de un premio: nombre y empresa, y nada más, porque va al proyector. */
export interface EligibleParticipantResponse {
  attendeeId: string
  displayName: string
  company: string | null
}

/**
 * `GET …/raffle/prizes/{id}/eligible`: quién puede ganar ese premio ahora, ordenados por id como los
 * numera el sorteo. Solo en curso. El sorteo no confía en esta lectura: recalcula al sortear.
 */
export interface EligibleResponse {
  count: number
  participants: EligibleParticipantResponse[]
}

/** La reemisión del acta: las seis autoridades, todas obligatorias, y nada más (spec raffles/03 §4.5). */
export interface ReissueRaffleRequest {
  notaryName: string
  notaryIdentityDocument: string
  witnessOneName: string
  witnessOneIdentityDocument: string
  witnessTwoName: string
  witnessTwoIdentityDocument: string
}

/** La anulación del ganador vivo. `note` es obligatoria con `other`; en blanco cuenta como nula. */
export interface VoidPrizeDrawRequest {
  code: PrizeDrawVoidCode
  note: string | null
}

/**
 * La entrega de un premio: quien retira, su documento, si es el ganador (lo declara el operador) y
 * la firma en PNG, en un solo multipart (spec raffles/03 §3.1).
 */
export interface HandOverPrizeRequest {
  receiverName: string
  receiverIdentityDocument: string
  receiverIsWinner: boolean
  signature: Blob
}

/** La reversión de la entrega viva: el motivo es obligatorio, de hasta 500 caracteres. */
export interface ReversePrizeHandoverRequest {
  reason: string
}

/**
 * `GET /v1/prize-draws/by-code/{code}`: de qué premio es un código y en qué está. Un código anulado
 * se contesta con su anulación, nunca como inexistente (spec raffles/03 §2.4).
 */
export interface PrizeDrawLookupResponse {
  drawId: string
  eventId: string
  raffleId: string
  prizeId: string
  prizeName: string
  winnerName: string
  winnerCompany: string | null
  status: PrizeDrawStatus
  /** Solo si está anulado. */
  void: { code: PrizeDrawVoidCode; voidedAt: string } | null
  /** La entrega viva, solo si se entregó. */
  handover: { receiverName: string; redeemedAt: string } | null
}

/**
 * Las extensiones de `RAFFLE_POOL_EXHAUSTED`: quién quedó fuera y por qué regla, en orden y la
 * primera que aplica, así que suman `participants` (spec raffles/02 §2.1).
 */
export interface PoolExhaustedCounts {
  participants: number
  excludedByVoid: number
  excludedByPerson: number
  excludedByCompany: number
}

/** Solo paginación: ni el listado de sorteos ni el de ganadores filtran. Tope 100. */
export interface PagingParams {
  page?: number
  pageSize?: number
}

/** Participante a cargar en un sorteo (vía Excel, CSV, TXT, formulario manual, API o Webhook). */
export interface RaffleParticipantInput {
  fullName: string
  /** Nombre y apellido por separado (§10), opcionales: `fullName` sigue mandando. */
  firstName?: string | null
  lastName?: string | null
  email?: string | null
  company?: string | null
  department?: string | null
  location?: string | null
  employeeCode?: string | null
  phone?: string | null
  qrCode?: string | null
  checkedIn?: boolean
}

export interface ImportRaffleParticipantsRequest {
  participants: RaffleParticipantInput[]
  replaceExisting?: boolean
  markCheckedIn?: boolean
  source?: 'excel' | 'csv' | 'txt' | 'manual' | 'api' | 'webhook'
}

export interface RaffleParticipantItem {
  id: string
  /** Número de boleto al azar y único en el sorteo (`482-917`). Opcional hasta que el servicio lo sirva. */
  ticketNumber?: string | null
  fullName: string
  firstName?: string | null
  lastName?: string | null
  /** Nulo si la persona entró solo con su ID. */
  email: string | null
  /** La clave interna de la persona en el evento (el correo, o `id:` + su ID). No se muestra. */
  identityKey: string
  company: string | null
  department?: string | null
  location?: string | null
  employeeCode?: string | null
  phone?: string | null
  qrCode: string
  checkedIn: boolean
  checkedInAt: string | null
  source: 'convocatoria' | 'excel' | 'csv' | 'txt' | 'manual' | 'api' | 'webhook'
  qualified: boolean
  override: AttendeeOverrideDecision | null
}

export interface ImportRaffleParticipantsResponse {
  imported: number
  updated: number
  total: number
  participants: RaffleParticipantItem[]
  /** Las filas que no se cargaron: número de fila (desde 1) y motivo (`missing_email_and_id`). */
  rejected?: RaffleParticipantImportRejection[]
}

export interface RaffleParticipantImportRejection {
  row: number
  reason: 'missing_email_and_id'
}

// ---------- Cuentas: registro y activación (events-service, plans/sorteo360.md §9.3) ----------

export type AccountPlanKey = 'free' | 'basic' | 'pro' | 'advanced'
export type AccountBillingPeriod = 'monthly' | 'annual'

export interface SignupRequest {
  companyName: string
  fullName: string
  email: string
  /** Un plan de pago abre una solicitud; la cuenta arranca en Gratis (§9.5). */
  plan: AccountPlanKey
  billingPeriod: AccountBillingPeriod
  acceptTerms: boolean
}

export interface SignupResponse {
  /**
   * Solo el modo demo lo trae: la ruta `/activar/{token}` que el servidor manda por correo. El
   * servicio real nunca la devuelve.
   */
  demoActivationPath?: string | null
}

export interface ActivateAccountRequest {
  token: string
  password: string
}

export interface ActivateAccountResponse {
  email: string
  companyName: string
  /** El plan de pago pedido en el registro, pendiente de la transferencia; `null` si es Gratis. */
  requestedPlan: AccountPlanKey | null
}
