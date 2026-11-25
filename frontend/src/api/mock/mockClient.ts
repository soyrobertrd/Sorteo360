import { ApiError, type ApiClient } from '../client'
import type {
  ActivateAccountRequest,
  ActivateAccountResponse,
  SignupRequest,
  SignupResponse,
  ImportPrizesRequest,
  ImportPrizesResponse,
} from '../types'
import {
  type FeatureSwitchResponse,
  type SetFeatureSwitchRequest,
  type ApiErrorCode,
  type CampaignResponse,
  type CancellationNoticeResponse,
  CiudadRequest,
  CiudadResponse,
  CompanyResponse,
  ContactClassificationResponse,
  ContactRequest,
  ContactResponse,
  type CreateCampaignRequest,
  CreateEventoRequest,
  CrmPagedResponse,
  EventTrackingResponse,
  RegistrationWindowResponse,
  EventoDetail,
  MONTAJES_EVENTO,
  MontajeEvento,
  EventoListItem,
  EventoImage,
  type LaunchCampaignRequest,
  type ListCampaignsParams,
  ListContactsParams,
  ListEventosParams,
  type ListNotificationsParams,
  NOTIFICATION_DELIVERY_STATUSES,
  type NotificationBatch,
  type NotificationHistoryItem,
  type CreatePrizesRequest,
  type EligibleResponse,
  type PagedResponse as RafflePage,
  type PoolExhaustedCounts,
  type PagingParams,
  type HandOverPrizeRequest,
  type PrizeDrawLookupResponse,
  type PrizeDrawSummary,
  type PrizeDrawVoidCode,
  type PrizeResponse,
  type PrizeState,
  type RaffleAttendeeResponse,
  type RaffleAttendeesResponse,
  type RaffleExclusionReason,
  type RaffleListItem,
  type RaffleResponse,
  type RaffleRoundRequest,
  type RaffleTotals,
  type ReissueRaffleRequest,
  type ReversePrizeHandoverRequest,
  type SetAttendeeOverrideRequest,
  type SetCertificateSignersRequest,
  RaffleDisplayFields,
  type UpdatePrizeRequest,
  type UpdateRaffleRequest,
  type VoidPrizeDrawRequest,
  type ImportRaffleParticipantsRequest,
  type ImportRaffleParticipantsResponse,
  type BulkExcludeAttendeesRequest,
  type BulkExcludeAttendeesResponse,
  type RaffleParticipantImportRejection,
  type RaffleParticipantItem,
  PagedResponse,
  PublicEvento,
  PublicEventoDetail,
  type PublicRegisterRequest,
  type PublicRegistrationResponse,
  type RegistrationTicketResponse,
  type ResolvedInvitationResponse,
  type UnsubscribeResponse,
  EventoVisibility,
  type RecipientSelectionResponse,
  type ListParticipantsParams,
  type ParticipantResponse,
  type ContactCheckInResponse,
  type ContactCampaignResponse,
  type ParticipantsCsvFile,
  type RecipientUploadResponse,
  type RecipientUploadRow,
  type SelectRecipientsRequest,
  type TemplateListItem,
  type UpdateCampaignRequest,
  UpdateEventoRequest,
  type UploadRowBucket,
} from '../types'
import type { CampaignExclusionRow, ContactListDetailResponse, ContactListResponse } from '../types'
import { currentMockIdentity } from '@/auth/mockIdentity'
import { ROLES, type FunctionRole } from '@/auth/roles'
import type {
  CheckInResponse,
  DoorEventResponse,
  DoorListItem,
  DoorListResponse,
  ListDoorRegistrationsParams,
} from '../types'
import { apiErrorCodeLabel, FORBIDDEN_MESSAGE } from '@/lib/labels'
import { esTelefonoValido } from '@/lib/validation/phone'
import { isWellFormedSwitchKey } from '@/lib/validation/switchKey'
import {
  seedCompanies,
  seedContactClassifications,
  seedAffiliations,
  seedFeatureSwitches,
  seedContactLists,
  type MockContactList,
  seedCampaigns,
  seedContactCheckIns,
  type MockAffiliation,
  seedContacts,
  seedEventos,
  seedNotificationBatches,
  seedFrozenRaffleAttendees,
  seedRaffleAttendeeOverrides,
  seedRaffleDoorList,
  seedRaffleQualifiedCounts,
  seedRaffles,
  type MockAttendeeOverride,
  type MockDoorEntry,
  type MockRaffle,
} from './seed'
import { nuevoBoleto } from '@/features/sorteo/boleto'
import { claveDePersona, correoDeClave } from '@/lib/personIdentity'

const LATENCY_MS = 250
// v2: semilla de fiestas navideñas; la v1 guardada (eventos del sistema anterior) se descarta.
// v3: la demo trae 50 presentes y 39 premios; una versión nueva descarta los datos guardados.
const SORT_MOCK_PERSIST_KEY = 'sorteo360.mock.state.v4'
const isBrowserPersist = (() => {
  try {
    return (
      typeof window !== 'undefined' &&
      typeof window.localStorage !== 'undefined' &&
      !import.meta.env?.VITEST
    )
  } catch {
    return false
  }
})()

/**
 * Las cuatro filas que siembra `TemplateSeed.cs`. Fase 1 no tiene create/update/delete, así
 * que el mock no las gestiona — solo las lista, igual que el servicio real.
 */
const MOCK_TEMPLATES: TemplateListItem[] = [
  {
    templateKey: 'save_the_date',
    language: 'es',
    purpose: 'campaign',
    isCampaignSelectable: true,
    subject: 'Reserva la fecha: {{evento_nombre}}',
  },
  {
    templateKey: 'registration_invite',
    language: 'es',
    purpose: 'campaign',
    isCampaignSelectable: true,
    subject: 'Está invitado a {{evento_nombre}} — registro abierto',
  },
  {
    templateKey: 'direct_message',
    language: 'es',
    purpose: 'campaign',
    isCampaignSelectable: false,
    subject: '{{asunto}}',
  },
  {
    templateKey: 'registration_confirmation',
    language: 'es',
    purpose: 'transactional',
    isCampaignSelectable: false,
    subject: 'Confirmación de registro: {{evento_nombre}}',
  },
  {
    templateKey: 'event_cancelled',
    language: 'es',
    purpose: 'transactional',
    isCampaignSelectable: false,
    subject: 'Cancelado: {{evento_nombre}}',
  },
]

/**
 * Como el servicio (spec 03 §4.1): una campaña cuyo par plantilla-idioma no existe no se puede
 * renderizar, así que el alta, la edición y el lanzamiento la rechazan con 400.
 */
function assertCampaignTemplate(templateKey: string, language: string): void {
  if (!MOCK_TEMPLATES.some((t) => t.templateKey === templateKey && t.language === language))
    throw rechazo('CAMPAIGN_TEMPLATE_NOT_FOUND', 400)
}

/** Una página de participantes, con el tope de 100 del servicio. */
function participantsPage(
  rows: ParticipantResponse[],
  params: { page?: number; pageSize?: number },
): PagedResponse<ParticipantResponse> {
  const page = Math.max(params.page ?? 1, 1)
  const pageSize = Math.min(Math.max(params.pageSize ?? 20, 1), 100)
  return {
    items: structuredClone(rows.slice((page - 1) * pageSize, page * pageSize)),
    total: rows.length,
    page,
    pageSize,
  }
}

/**
 * La regla de `CampaignDirectMessage.Check`, con los valores ya resueltos: sin evento si y solo si
 * la plantilla es `direct_message`, y entonces con asunto y texto; una de evento no lleva ninguno.
 */
function assertDirectMessageRule(campaign: {
  eventId: string | null
  templateKey: string
  subject?: string | null
  message?: string | null
}): void {
  const direct = campaign.templateKey === 'direct_message'
  const conTexto = campaign.subject != null || campaign.message != null
  const completo = Boolean(campaign.subject?.trim()) && Boolean(campaign.message?.trim())
  if ((campaign.eventId == null) !== direct || (direct ? !completo : conTexto))
    throw rechazo('CAMPAIGN_DIRECT_MESSAGE_MISMATCH', 400)
}

/**
 * Un rechazo con el mismo código y estado que emite el servicio, y con el texto que la
 * UI le pone a ese código: así el modo mock enseña exactamente lo que se ve en http.
 */
function rechazo(code: ApiErrorCode, status = 409): ApiError {
  return new ApiError(apiErrorCodeLabel[code], status, undefined, code)
}

/**
 * events-service acepta solo un id IANA que resuelva; un id de Windows o inventado es
 * 400 `TIME_ZONE_UNKNOWN`. Intl conoce los mismos ids y rechaza los demás.
 */
function assertTimeZone(timeZone: string): void {
  if (timeZone) {
    try {
      new Intl.DateTimeFormat('en', { timeZone })
      return
    } catch {
      // Intl no la conoce: el mismo rechazo que el servicio.
    }
  }
  throw rechazo('TIME_ZONE_UNKNOWN', 400)
}

const VISIBILIDADES: readonly EventoVisibility[] = ['public', 'private']

/**
 * El validator de events-service rechaza una visibilidad fuera del conjunto con un 400 de campo,
 * sin código: el mensaje va sobre el control, no en el banner.
 */
function assertVisibility(value: string | null | undefined): void {
  if (value == null || VISIBILIDADES.includes(value as EventoVisibility)) return
  const mensaje = `La visibilidad debe ser una de: ${VISIBILIDADES.join(', ')}.`
  throw new ApiError(mensaje, 400, { Visibility: [mensaje] })
}

/**
 * El montaje del salón como lo guarda el servicio: lo reconoce sin importar mayúsculas y lo
 * devuelve en su forma canónica. Un valor fuera de los seis, vacío incluido, es un 400 de campo.
 */
function montajeDelSalon(value: string | null | undefined): MontajeEvento | null {
  if (value == null) return null
  const montaje = MONTAJES_EVENTO.find((m) => m.toLowerCase() === value.toLowerCase())
  if (montaje) return montaje
  const mensaje = `El montaje del salón debe ser uno de: ${MONTAJES_EVENTO.join(', ')}.`
  throw new ApiError(mensaje, 400, { SeatingLayout: [mensaje] })
}

/** Un 400 de campo sin código, con el texto del validator de crm-service. */

/** Como el servicio: el patrocinador en texto, recortado; en blanco es sin patrocinador. */
function sponsorDeTexto(nombre: string | null | undefined): PrizeResponse['sponsor'] {
  const n = nombre?.trim().slice(0, 200)
  return n ? { companyId: null, companyName: n, contactId: null, contactName: null } : null
}

function rechazoDeCampo(campo: string, mensaje: string): ApiError {
  return new ApiError(mensaje, 400, { [campo]: [mensaje] })
}

/** El teléfono de empresas y contactos, con la regla de `PhoneRules` de crm-service. */
function assertTelefono(phone: string | null | undefined): void {
  if (!esTelefonoValido(phone))
    throw rechazoDeCampo(
      'Phone',
      'El campo [Phone] debe tener entre 7 y 20 caracteres, solo dígitos, espacios y + - ( ).',
    )
}

/**
 * El id con que el mock nombra a un asistente congelado. La fila del mock no trae id, y el correo es
 * único en la lista.
 */
function mockAttendeeId(attendee: RaffleAttendeeResponse): string {
  return `att-${attendee.identityKey}`
}

/** Las seis autoridades de la reemisión, con el nombre y el tope del validator del servicio. */
const REISSUE_FIELDS: [keyof ReissueRaffleRequest, string, number][] = [
  ['notaryName', 'El nombre del notario', 200],
  ['notaryIdentityDocument', 'El documento del notario', 100],
  ['witnessOneName', 'El nombre del primer testigo', 200],
  ['witnessOneIdentityDocument', 'El documento del primer testigo', 100],
  ['witnessTwoName', 'El nombre del segundo testigo', 200],
  ['witnessTwoIdentityDocument', 'El documento del segundo testigo', 100],
]

/** El tope del nombre y del cargo de un firmante del certificado, el del validator del servicio. */
const CERTIFICATE_SIGNER_MAX_LENGTH = 100

/** La empresa para la regla por empresa: recortada y en minúsculas, sin la normalización del servicio. */
function companyKey(company: string | null): string | null {
  const key = company?.trim().toLowerCase()
  return key ? key : null
}

/** Base 32 de Crockford: sin I, L, O ni U, como el código del servicio (spec raffles/03 §2). */
const REDEMPTION_ALPHABET = '0123456789ABCDEFGHJKMNPQRSTVWXYZ'

function mockRedemptionCode(): string {
  const block = () =>
    Array.from(
      { length: 4 },
      () => REDEMPTION_ALPHABET[Math.floor(Math.random() * REDEMPTION_ALPHABET.length)],
    ).join('')
  return `RD-${block()}-${block()}`
}

/**
 * El código como lo compara el servicio: sin mayúsculas, guiones ni espacios, con O leída como 0 e
 * I o L como 1, y el prefijo `RD` opcional. Lo que no queda en ocho caracteres del alfabeto no
 * existe (spec raffles/03 §2.4).
 */
function normalizeRedemptionCode(code: string): string | null {
  let key = code.toUpperCase().replace(/[-\s]/g, '').replace(/O/g, '0').replace(/[IL]/g, '1')
  if (key.length === 10 && key.startsWith('RD')) key = key.slice(2)
  return key.length === 8 && [...key].every((c) => REDEMPTION_ALPHABET.includes(c)) ? key : null
}

/** Los dos campos de texto de la entrega, con el nombre y el tope del validator del servicio. */
const HANDOVER_FIELDS: [keyof HandOverPrizeRequest & string, string, string, number][] = [
  [
    'receiverName',
    'El nombre de quien retira el premio es obligatorio.',
    'El nombre de quien retira no puede superar 200 caracteres.',
    200,
  ],
  [
    'receiverIdentityDocument',
    'El documento de identidad de quien retira el premio es obligatorio.',
    'El documento de identidad no puede superar 100 caracteres.',
    100,
  ],
]

const VOID_CODES: readonly PrizeDrawVoidCode[] = ['absent', 'declined', 'ineligible', 'other']

/** Los motivos que ninguna decisión manual vence, con el código que rechaza meterlos a mano. */
const UNOVERRIDABLE_EXCLUSION_CODES: Partial<Record<RaffleExclusionReason, ApiErrorCode>> = {
  contact_unresolved: 'PARTICIPANT_CONTACT_UNRESOLVED',
  suspended: 'PARTICIPANT_SUSPENDED',
  do_not_invite: 'PARTICIPANT_DO_NOT_INVITE',
}

/** El ajuste a mano sobre el motivo de la puerta, con las mismas precedencias que el servicio. */
function classifyDoorEntry(
  entry: MockDoorEntry,
  manual: MockAttendeeOverride | undefined,
): RaffleAttendeeResponse {
  const base = entry.exclusionBeforeOverride
  const included = manual?.decision === 'included'
  const reasons = (): [RaffleExclusionReason | null, RaffleExclusionReason | null] => {
    if (base && UNOVERRIDABLE_EXCLUSION_CODES[base]) return [base, included ? base : null]
    if (manual?.decision === 'excluded') return ['manually_excluded', null]
    if (included) return [null, base]
    return [base, null]
  }
  const [exclusionReason, overruledReason] = reasons()
  return {
    email: correoDeClave(entry.identityKey),
    identityKey: entry.identityKey,
    firstName: entry.firstName,
    lastName: entry.lastName,
    company: entry.company,
    qualified: exclusionReason === null,
    exclusionReason,
    manualDecision: manual?.decision ?? null,
    overruledReason,
    manualNote: manual?.note ?? null,
    manualDecidedById: manual?.decidedById ?? null,
    manualDecidedAt: manual?.decidedAt ?? null,
  }
}

/** PRNG determinista (mulberry32), para generar filas de carga que no cambian entre corridas. */
function mockMulberry32(seed: number) {
  let state = seed
  return function next(): number {
    state |= 0
    state = (state + 0x6d2b79f5) | 0
    let t = Math.imul(state ^ (state >>> 15), 1 | state)
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296
  }
}

function mockHashString(str: string): number {
  let hash = 2166136261
  for (let i = 0; i < str.length; i++) {
    hash ^= str.charCodeAt(i)
    hash = Math.imul(hash, 16777619)
  }
  return hash >>> 0
}

/** Una entrada de demostración que existe sin haberse inscrito: `/publico/entrada/<este>`. */
const MOCK_DEMO_QR = 'demo-entrada-0000000000000000000000000000000'

/** Una inscripción vista desde la puerta, con lo que el servicio usa para buscarla y no devuelve. */
type MockDoorRegistration = Omit<DoorListItem, 'checkedInAt'> & {
  email: string
  qrCode: string | null
}

/** 32 bytes al azar en base64url, 43 caracteres, como el `qr_code` del servicio. */
function mockQrCode(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(32))
  return btoa(String.fromCharCode(...bytes))
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '')
}

/**
 * Una imagen con forma de QR, determinista por código. No es un QR legible: en modo mock no hay
 * servicio que genere el de verdad, y el código no sale del navegador.
 */
function mockQrImage(qrCode: string): string {
  const n = 25
  const next = mockMulberry32(mockHashString(qrCode))
  const esGuia = (x: number, y: number) =>
    [
      [0, 0],
      [n - 7, 0],
      [0, n - 7],
    ].some(([gx, gy]) => {
      const dx = x - gx
      const dy = y - gy
      if (dx < 0 || dy < 0 || dx > 6 || dy > 6) return false
      return (
        dx === 0 || dy === 0 || dx === 6 || dy === 6 || (dx >= 2 && dx <= 4 && dy >= 2 && dy <= 4)
      )
    })
  const enGuia = (x: number, y: number) =>
    (x < 8 && y < 8) || (x > n - 9 && y < 8) || (x < 8 && y > n - 9)
  let celdas = ''
  for (let y = 0; y < n; y++) {
    for (let x = 0; x < n; x++) {
      const negra = enGuia(x, y) ? esGuia(x, y) : next() < 0.5
      if (negra) celdas += `<rect x="${x}" y="${y}" width="1" height="1"/>`
    }
  }
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="-2 -2 ${n + 4} ${n + 4}" shape-rendering="crispEdges"><rect x="-2" y="-2" width="${n + 4}" height="${n + 4}" fill="#fff"/>${celdas}</svg>`
  return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`
}

/**
 * Implementación de demostración: estado en memoria (se reinicia al recargar)
 * que replica las reglas de negocio de eventos-service para que el mockup
 * se comporte igual que el servicio real.
 */
export class MockApiClient implements ApiClient {
  private companies: CompanyResponse[] = structuredClone(seedCompanies)
  private contactClassifications: ContactClassificationResponse[] = structuredClone(
    seedContactClassifications,
  )
  private contacts: ContactResponse[] = structuredClone(seedContacts)
  /** Quien se dio de baja: como en el servicio, editar el contacto no lo deshace. */
  private optedOutIds = new Set<string>()
  private eventos: EventoDetail[] = structuredClone(seedEventos)
  /** Las afiliaciones empresa↔contacto, con las terminadas como historial. */
  private affiliations: MockAffiliation[] = structuredClone(seedAffiliations)
  /** Una fila por clave que alguien cambió; la que no tiene fila está encendida. */
  private featureSwitches: FeatureSwitchResponse[] = structuredClone(seedFeatureSwitches)
  /** Registros sin activar, por token, y los correos ya activados (§9.3). */
  private pendingSignups: Record<string, SignupRequest & { expiresAt: number }> = {}
  private activatedEmails: string[] = []
  private contactLists: MockContactList[] = structuredClone(seedContactLists)
  private campaigns: CampaignResponse[] = structuredClone(seedCampaigns)
  /** Solo lectura, como el historial real: nada en la pantalla envía ni reenvía. */
  private notificationBatches: NotificationBatch[] = structuredClone(seedNotificationBatches)
  private raffles: MockRaffle[] = structuredClone(seedRaffles)
  /** Lo suman los totales. Lo escriben `startRaffle` y `reopenRaffle`. */
  private raffleQualifiedCounts: Record<string, number> = { ...seedRaffleQualifiedCounts }
  /** Los ajustes a mano de cada borrador, por evento. Sobreviven a `reopen`, como en el servicio. */
  private raffleOverrides: Record<string, MockAttendeeOverride[]> = structuredClone(
    seedRaffleAttendeeOverrides,
  )
  /** La lista que congeló cada sorteo al arrancar, por sorteo. */
  private frozenAttendees: Record<string, RaffleAttendeeResponse[]> = Object.fromEntries(
    Object.entries(seedFrozenRaffleAttendees).map(([id, door]) => [
      id,
      door.map((entry) => classifyDoorEntry(entry, undefined)),
    ]),
  )
  /** Cuándo se leyó la puerta por última vez: el servicio guarda la copia 30 segundos. */
  private doorListFetchedAt = new Date().toISOString()
  /** Eventos cuyo aviso de cancelación ya salió: la segunda pasada no le escribe a nadie. */
  private cancellationNoticeSent = new Set<string>()
  /** Solo los eventos que alguna vez se cerraron: sin fila, la ventana está abierta. */
  private registrationWindows = new Map<string, RegistrationWindowResponse>()
  /** campaignId → ids de contacto que son destinatarios. */
  private campaignRecipients = new Map<string, Set<string>>(
    seedCampaigns.map((c) => [
      c.id,
      new Set(seedContacts.filter((x) => x.canBeEmailed).map((x) => x.id)),
    ]),
  )
  /** campaignId → id de contacto excluido → código de razón y cuándo. */
  private campaignExclusions = new Map<string, Map<string, { reason: string; excludedAt: string }>>(
    seedCampaigns.map((c) => [
      c.id,
      new Map(
        seedContacts
          .filter((x) => !x.canBeEmailed)
          .map((x) => [
            x.id,
            {
              reason: x.suppressionReason ?? 'contact_unavailable',
              excludedAt: c.launchedAt ?? c.createdAt,
            },
          ]),
      ),
    ]),
  )
  /** campaignId → la carga sin confirmar de esa campaña, si hay una. */
  private pendingUploads = new Map<
    string,
    RecipientUploadResponse & { rows: RecipientUploadRow[] }
  >()
  /** Participantes por evento (cargados vía Excel/CSV/TXT, API/Webhook, manuales o demo). */
  private raffleParticipants: Record<string, RaffleParticipantItem[]> = {
    'ev-0001': seedRaffleDoorList.map((entry, idx) => ({
      id: `part-ev0001-${idx + 1}`,
      fullName: `${entry.firstName} ${entry.lastName}`.trim(),
      email: correoDeClave(entry.identityKey),
      identityKey: entry.identityKey,
      company: entry.company,
      department: entry.company,
      employeeCode: String(19000 + idx * 311),
      phone: null,
      qrCode: `QR-NAV-2026-${String(idx + 1).padStart(4, '0')}`,
      checkedIn: true,
      checkedInAt: '2026-12-18T18:30:00Z',
      source: 'excel',
      qualified: true,
      override: null,
    })),
  }
  private sequence = 1000

  constructor() {
    this.hydrateState()
  }

  private hydrateState(): void {
    // Sincroniza checkIns con los participantes pre-marcados del evento demo.
    for (const [eventId, list] of Object.entries(this.raffleParticipants)) {
      for (const p of list) {
        if (p.checkedIn) {
          this.checkIns.set(`reg-${eventId}-${p.id}`, {
            checkedInAt: p.checkedInAt ?? this.now(),
            checkedInById: 'seed',
          })
        }
      }
    }
    if (!isBrowserPersist) return
    try {
      const raw = window.localStorage.getItem(SORT_MOCK_PERSIST_KEY)
      if (!raw) return
      const saved = JSON.parse(raw) as Partial<{
        eventos: EventoDetail[]
        raffles: MockRaffle[]
        raffleQualifiedCounts: Record<string, number>
        raffleOverrides: Record<string, MockAttendeeOverride[]>
        frozenAttendees: Record<string, RaffleAttendeeResponse[]>
        featureSwitches: FeatureSwitchResponse[]
        pendingSignups: Record<string, SignupRequest & { expiresAt: number }>
        activatedEmails: string[]
        raffleParticipants: Record<string, RaffleParticipantItem[]>
        checkIns: Array<[string, { checkedInAt: string; checkedInById: string }]>
        sequence: number
      }>
      if (Array.isArray(saved.eventos)) this.eventos = saved.eventos
      if (Array.isArray(saved.raffles)) this.raffles = saved.raffles
      if (saved.raffleQualifiedCounts) this.raffleQualifiedCounts = saved.raffleQualifiedCounts
      if (saved.raffleOverrides) this.raffleOverrides = saved.raffleOverrides
      if (saved.frozenAttendees) this.frozenAttendees = saved.frozenAttendees
      // Las filas sin autor son la siembra vieja que apagaba Eventos y Convocatorias: el
      // administrador los ve (2026-11-19); lo que apagó una persona se respeta.
      if (Array.isArray(saved.featureSwitches))
        this.featureSwitches = saved.featureSwitches.filter((r) => r.changedAt !== null)
      if (saved.pendingSignups) this.pendingSignups = saved.pendingSignups
      if (Array.isArray(saved.activatedEmails)) this.activatedEmails = saved.activatedEmails
      if (saved.raffleParticipants) this.raffleParticipants = saved.raffleParticipants
      if (Array.isArray(saved.checkIns)) this.checkIns = new Map(saved.checkIns)
      if (typeof saved.sequence === 'number') this.sequence = saved.sequence
    } catch {
      // Ignora JSON inválido guardado en localStorage.
    }
  }

  private persistState(): void {
    if (!isBrowserPersist) return
    try {
      window.localStorage.setItem(
        SORT_MOCK_PERSIST_KEY,
        JSON.stringify({
          eventos: this.eventos,
          raffles: this.raffles,
          raffleQualifiedCounts: this.raffleQualifiedCounts,
          raffleOverrides: this.raffleOverrides,
          frozenAttendees: this.frozenAttendees,
          featureSwitches: this.featureSwitches,
          pendingSignups: this.pendingSignups,
          activatedEmails: this.activatedEmails,
          raffleParticipants: this.raffleParticipants,
          checkIns: [...this.checkIns.entries()],
          sequence: this.sequence,
        }),
      )
    } catch {
      // Ignora cuota excedida en almacenamiento local.
    }
  }

  // ---------- Empresas (crm-service) ----------

  // ---------- Contactos (crm-service) ----------

  /** Paginado y búsqueda del lado del servidor, tal como los hace `ContactReader`. */
  async listContacts(params: ListContactsParams): Promise<CrmPagedResponse<ContactResponse>> {
    await this.delay()
    const page = Math.max(params.page ?? 1, 1)
    const pageSize = Math.min(Math.max(params.pageSize ?? 20, 1), 100)
    const term = params.search?.trim().toLowerCase() ?? ''
    // Como `ContactReader`: códigos exactos, varios combinados con OR.
    if ((params.id?.length ?? 0) > 100)
      throw rechazoDeCampo('Id', 'El campo [Id] admite hasta 100 elementos.')
    const codes = [params.classification ?? []]
      .flat()
      .map((code) => code.trim())
      .filter(Boolean)
    // Como el servicio: sin `status` vienen todos, suspendidos incluidos.
    const statuses = [params.status ?? []].flat()
    if (statuses.some((status) => status !== 'Active' && status !== 'Suspended'))
      throw rechazo('contact.unknown_status', 400)

    const matches = this.contacts
      .filter(
        (contact) =>
          term === '' ||
          contact.name.toLowerCase().includes(term) ||
          (contact.email ?? '').toLowerCase().includes(term),
      )
      .filter(
        (contact) =>
          codes.length === 0 ||
          contact.classifications.some((candidate) => codes.includes(candidate.code)),
      )
      .filter((contact) => !params.id?.length || params.id.includes(contact.id))
      .filter((contact) => statuses.length === 0 || statuses.includes(contact.status ?? 'Active'))
      .sort((a, b) => a.createdAt.localeCompare(b.createdAt) || a.id.localeCompare(b.id))

    return {
      items: matches
        .slice((page - 1) * pageSize, page * pageSize)
        .map((contact) => this.withCompanies(contact)),
      totalCount: matches.length,
      page,
      pageSize,
    }
  }

  async listContactClassifications(): Promise<ContactClassificationResponse[]> {
    await this.delay()
    return structuredClone(
      this.contactClassifications.toSorted((a, b) => a.name.localeCompare(b.name)),
    )
  }

  /**
   * Lo editable de un contacto, normalizado como lo devuelve el servicio. El
   * consentimiento no aparece porque el servicio tampoco lo devuelve: solo su efecto.
   */
  private contactFields(
    body: ContactRequest,
    doNotInvite: boolean,
    optedOut = false,
    suspended = false,
  ): Omit<ContactResponse, 'id' | 'createdAt' | 'updatedAt' | 'classifications'> {
    return {
      name: `${body.firstName} ${body.lastName}`.trim(),
      firstName: body.firstName,
      lastName: body.lastName,
      email: body.email ?? null,
      phone: body.phone ?? null,
      country: body.country?.trim() || null,
      city: body.city?.trim() || null,
      whatsApp: body.whatsApp?.trim() || null,
      preferredLanguage: body.preferredLanguage ?? null,
      decisionLevel: body.decisionLevel ?? null,
      ...this.mailability(body.email, doNotInvite, optedOut, suspended),
    }
  }

  /** La misma precedencia que `ContactMailability.Evaluate`: la suspensión va antes que no invitar. */
  private mailability(
    email: string | null | undefined,
    doNotInvite: boolean,
    optedOut: boolean,
    suspended: boolean,
  ): Pick<ContactResponse, 'canBeEmailed' | 'suppressionReason'> {
    const suppressionReason = !email
      ? 'no_email'
      : optedOut
        ? 'opted_out'
        : suspended
          ? 'suspended'
          : doNotInvite
            ? 'do_not_invite'
            : null
    return { canBeEmailed: suppressionReason === null, suppressionReason }
  }

  // ---------- Afiliaciones empresa↔contacto (crm-service) ----------

  /**
   * Como `ContactReader`: las empresas vigentes del contacto, ordenadas por nombre. Vigente
   * es sin fin o con un fin que todavía no llegó. La OPT es la de la empresa actual, como
   * `ContactOptOffice`: el empleo que empezó más tarde y, el mismo día, el registrado después
   * (acá, el que está más adelante en la lista). Sin empresa vigente no tiene OPT.
   */
  private withCompanies(contact: ContactResponse): ContactResponse {
    const hoy = this.today()
    const vigentes = this.affiliations.flatMap((a) => {
      const company = this.companies.find((c) => c.id === a.companyId)
      return a.contactId === contact.id && (a.endDate === null || a.endDate >= hoy) && company
        ? [{ affiliation: a, company }]
        : []
    })
    const companies = vigentes
      .map(({ company }) => ({ id: company.id, name: company.name }))
      .sort((a, b) => a.name.localeCompare(b.name))
    const actual = vigentes.reduce<(typeof vigentes)[number] | null>(
      (elegida, candidata) =>
        !elegida || candidata.affiliation.startDate >= elegida.affiliation.startDate
          ? candidata
          : elegida,
      null,
    )
    return {
      ...structuredClone(contact),
      companies,
      optOfficeId: actual?.company.optOfficeId ?? null,
    }
  }

  private async findContactList(id: string): Promise<MockContactList> {
    const list = this.contactLists.find((candidate) => candidate.id === id && !candidate.deletedAt)
    if (!list) throw new ApiError('La lista de contactos no fue encontrada.', 404)
    return list
  }

  private liveMembers(list: MockContactList): string[] {
    return list.contactIds.filter((contactId) => this.contacts.some((c) => c.id === contactId))
  }

  private contactListResponse(list: MockContactList): ContactListResponse {
    const { id, name, createdAt, createdById, updatedAt, updatedById } = list
    const memberCount = this.liveMembers(list).length
    return { id, name, memberCount, createdAt, createdById, updatedAt, updatedById }
  }

  async listContactLists(): Promise<ContactListResponse[]> {
    await this.delay()
    return this.contactLists
      .filter((list) => !list.deletedAt)
      .sort((a, b) => a.name.localeCompare(b.name))
      .map((list) => this.contactListResponse(list))
  }

  async getContactList(id: string): Promise<ContactListDetailResponse> {
    await this.delay()
    const list = await this.findContactList(id)
    return {
      ...this.contactListResponse(list),
      optOfficeId: list.optOfficeId,
      contactIds: this.liveMembers(list),
    }
  }

  // ---------- Interruptores de funciones (crm-service) ----------

  /** La lectura anónima del portal público: las mismas filas, sin pedir rol. */
  async listPublicFeatureSwitches(): Promise<FeatureSwitchResponse[]> {
    await this.delay()
    return this.featureSwitches.map((row) => ({ ...row }))
  }

  // ---------- Cuentas (§9.3) ----------

  /**
   * Como el servicio: responde igual exista o no el correo. El enlace del correo vuelve en
   * `demoActivationPath` porque el modo demo no manda correos.
   */
  async signup(body: SignupRequest): Promise<SignupResponse> {
    await this.delay()
    const errors: Record<string, string[]> = {}
    if (!body.companyName.trim()) errors.companyName = ['Escribe el nombre de la empresa.']
    if (!body.fullName.trim()) errors.fullName = ['Escribe tu nombre.']
    if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(body.email.trim()))
      errors.email = ['Escribe un correo válido.']
    if (!body.acceptTerms) errors.acceptTerms = ['Tienes que aceptar los términos.']
    if (Object.keys(errors).length > 0)
      throw new ApiError('Revisa los campos marcados.', 400, errors)
    const email = body.email.trim().toLowerCase()
    if (this.activatedEmails.includes(email)) return {}
    for (const [t, p] of Object.entries(this.pendingSignups))
      if (p.email === email) delete this.pendingSignups[t]
    const token = crypto.randomUUID().replaceAll('-', '')
    this.pendingSignups[token] = { ...body, email, expiresAt: Date.now() + 24 * 3_600_000 }
    this.persistState()
    return { demoActivationPath: `/activar/${token}` }
  }

  async resendActivation(email: string): Promise<SignupResponse> {
    await this.delay()
    const entry = Object.entries(this.pendingSignups).find(
      ([, p]) => p.email === email.trim().toLowerCase(),
    )
    if (!entry) return {}
    const [, pending] = entry
    return this.signup(pending)
  }

  async activateAccount({
    token,
    password,
  }: ActivateAccountRequest): Promise<ActivateAccountResponse> {
    await this.delay()
    const pending = this.pendingSignups[token]
    if (!pending || pending.expiresAt < Date.now()) {
      throw new ApiError(
        apiErrorCodeLabel.ACTIVATION_TOKEN_INVALID,
        410,
        undefined,
        'ACTIVATION_TOKEN_INVALID',
      )
    }
    // La política del realm; el servicio la delega en Keycloak.
    if (password.length < 10 || !/\d/.test(password) || !/[A-Za-z]/.test(password)) {
      throw new ApiError('Revisa los campos marcados.', 400, {
        password: ['Mínimo 10 caracteres, con letras y números.'],
      })
    }
    if (this.activatedEmails.includes(pending.email)) {
      throw new ApiError(
        apiErrorCodeLabel.ACCOUNT_EMAIL_TAKEN,
        409,
        undefined,
        'ACCOUNT_EMAIL_TAKEN',
      )
    }
    delete this.pendingSignups[token]
    this.activatedEmails.push(pending.email)
    this.persistState()
    return {
      email: pending.email,
      companyName: pending.companyName,
      requestedPlan: pending.plan === 'free' ? null : pending.plan,
    }
  }

  /** Como `FeatureSwitchService.ListAsync`: las filas guardadas, solo para `platform:staff`. */
  async listFeatureSwitches(): Promise<FeatureSwitchResponse[]> {
    await this.delay()
    if (!currentMockIdentity().roles.includes(ROLES.platformStaff)) {
      throw new ApiError(FORBIDDEN_MESSAGE, 403)
    }
    return structuredClone(this.featureSwitches)
  }

  /**
   * Como `FeatureSwitchService.SetAsync`: `feature-switches:write`, la forma de la clave y nunca si
   * existe; la fila queda a nombre de quien llama.
   */
  async setFeatureSwitch(key: string, body: SetFeatureSwitchRequest): Promise<void> {
    await this.delay()
    const identity = currentMockIdentity()
    if (!identity.roles.includes(ROLES.featureSwitchesWrite)) {
      throw new ApiError(FORBIDDEN_MESSAGE, 403)
    }
    if (!isWellFormedSwitchKey(key)) {
      throw rechazo('feature_switch.invalid_key', 400)
    }
    const row: FeatureSwitchResponse = {
      key,
      enabled: body.enabled,
      changedById: identity.sub,
      changedByName: identity.nombre,
      changedAt: new Date().toISOString(),
    }
    this.featureSwitches = [...this.featureSwitches.filter((r) => r.key !== key), row]
    this.persistState()
  }

  /** La fecha de hoy como la compara el servicio: `YYYY-MM-DD`, en UTC. */
  private today(): string {
    return this.now().slice(0, 10)
  }

  private async delay(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve, LATENCY_MS))
  }

  private newId(prefix: string): string {
    this.sequence += 1
    return `${prefix}-${this.sequence}`
  }

  private now(): string {
    return new Date().toISOString()
  }

  private findEvento(id: string): EventoDetail {
    const evento = this.eventos.find((e) => e.id === id)
    if (!evento) throw new ApiError('Evento no encontrado.', 404)
    return evento
  }

  private assertEditable(evento: EventoDetail): void {
    if (evento.status === 'closed' || evento.status === 'cancelled') {
      throw new ApiError('El evento está cerrado o cancelado y no admite cambios.', 409)
    }
  }

  // ---------- Eventos ----------

  /**
   * Como el servicio (spec convocatorias/10 §4): de un día antes de empezar a un día después de
   * terminar, por fecha UTC, sin los cancelados, y solo los campos del selector.
   */
  async listDoorEvents(): Promise<DoorEventResponse[]> {
    await this.delay()
    const shift = (date: string, days: number) => {
      const d = new Date(`${date.slice(0, 10)}T00:00:00Z`)
      d.setUTCDate(d.getUTCDate() + days)
      return d.toISOString().slice(0, 10)
    }
    const today = this.now().slice(0, 10)
    return this.eventos
      .filter(
        (e) =>
          e.status !== 'cancelled' &&
          (e.status === 'in_progress' ||
            (shift(e.startDate, -1) <= today && today <= shift(e.endDate, 1))),
      )
      .sort((a, b) => a.startDate.localeCompare(b.startDate) || a.name.localeCompare(b.name))
      .map((e) => ({
        id: e.id,
        name: e.name,
        startDate: e.startDate,
        endDate: e.endDate,
        cities: structuredClone(e.cities),
      }))
  }

  async listEventos(params: ListEventosParams): Promise<PagedResponse<EventoListItem>> {
    await this.delay()
    if (params.active !== undefined && params.status) {
      throw new ApiError('Los filtros "status" y "active" son mutuamente excluyentes.', 400)
    }
    let items = [...this.eventos]
    if (params.type) items = items.filter((e) => e.type === params.type)
    if (params.status) items = items.filter((e) => e.status === params.status)
    assertVisibility(params.visibility)
    if (params.visibility) items = items.filter((e) => e.visibility === params.visibility)
    if (params.active !== undefined) {
      const activos = new Set(['planning', 'in_progress'])
      items = items.filter((e) => activos.has(e.status) === params.active)
    }
    if (params.startDateFrom) items = items.filter((e) => e.startDate >= params.startDateFrom!)
    if (params.startDateTo) items = items.filter((e) => e.startDate <= params.startDateTo!)

    if (params.searchTerm) {
      const term = params.searchTerm.toLowerCase()
      items = items.filter((e) => e.name.toLowerCase().includes(term))
    }

    const field = params.sortField || 'startDate'
    const order = params.sortOrder || (params.sortField ? 'asc' : 'desc')

    const STATUS_ORDER: Record<string, number> = {
      planning: 1,
      in_progress: 2,
      closed: 3,
      cancelled: 4,
    }

    items.sort((a, b) => {
      let cmp = 0
      if (field === 'totalCost') {
        const costA = a.totalCost
        const costB = b.totalCost
        if (!costA && !costB) cmp = 0
        else if (!costA) cmp = 1
        else if (!costB) cmp = -1
        else if (costA.currency !== costB.currency) {
          cmp = costA.currency === 'USD' ? -1 : 1
        } else {
          cmp = order === 'asc' ? costA.amount - costB.amount : costB.amount - costA.amount
        }
      } else {
        let valA: unknown
        let valB: unknown
        if (field === 'status') {
          valA = STATUS_ORDER[a.status] || 99
          valB = STATUS_ORDER[b.status] || 99
        } else {
          valA = a[field as keyof typeof a] ?? ''
          valB = b[field as keyof typeof b] ?? ''
        }

        if (typeof valA === 'string' && typeof valB === 'string') {
          cmp = valA.localeCompare(valB)
        } else if (typeof valA === 'number' && typeof valB === 'number') {
          cmp = valA - valB
        }
        if (order === 'desc') cmp = -cmp
      }

      if (cmp !== 0) return cmp
      const dateCmp = b.createdAt.localeCompare(a.createdAt)
      if (dateCmp !== 0) return dateCmp
      return a.id.localeCompare(b.id)
    })

    const page = params.page ?? 1
    const pageSize = params.pageSize ?? 20
    const slice = items.slice((page - 1) * pageSize, page * pageSize)
    return {
      total: items.length,
      page,
      pageSize,
      items: slice.map((e) => ({
        id: e.id,
        type: e.type,
        name: e.name,
        status: e.status,
        visibility: e.visibility,
        startDate: e.startDate,
        endDate: e.endDate,
        language: e.language,
        invitedCount: e.invitedCount,
        estimatedBudget: e.estimatedBudget,
        externalUrl: e.externalUrl,
        cover: e.images?.find((img) => img.kind === 'Cover') ?? null,
        totalCost: e.totalCost,
        seatingLayout: e.seatingLayout ?? null,
        cities: e.cities,
        createdAt: e.createdAt,
        updatedAt: e.updatedAt,
      })),
    }
  }

  async getEvento(id: string): Promise<EventoDetail> {
    await this.delay()
    return structuredClone(this.findEvento(id))
  }

  async createEvento(body: CreateEventoRequest): Promise<string> {
    await this.delay()
    assertVisibility(body.visibility)
    assertTimeZone(body.timeZone)
    const seatingLayout = montajeDelSalon(body.seatingLayout)
    const id = this.newId('ev')

    this.eventos.unshift({
      id,
      type: body.type,
      name: body.name,
      startDate: body.startDate,
      endDate: body.endDate,
      status: 'planning',
      // Sin visibilidad nace privado, como en el servicio.
      visibility: body.visibility ?? 'private',
      // Sin idioma se crea en español: es opcional en el cuerpo y obligatorio en la columna.
      language: body.language ?? 'es',
      externalUrl: body.externalUrl ?? null,
      invitedCount: body.invitedCount ?? null,
      estimatedBudget: body.estimatedBudget ?? null,
      description: body.description ?? null,
      totalCost: null,
      cancellationReason: null,
      seatingLayout,
      cities: [
        {
          id: this.newId('ciu'),
          name: body.city,
          country: body.country,
          date: body.startDate,
          displayOrder: 1,
          timeZone: body.timeZone,
        },
      ],
      createdAt: this.now(),
      updatedAt: this.now(),
    })
    this.persistState()
    return id
  }

  async updateEvento(id: string, body: UpdateEventoRequest): Promise<EventoDetail> {
    await this.delay()
    const evento = this.findEvento(id)
    this.assertEditable(evento)
    // El PATCH reemplaza la representación editable completa, así que un idioma nulo borraría
    // el guardado. Se rechaza con el mismo código y estado que el servicio.
    if (!body.language) throw rechazo('EVENT_LANGUAGE_REQUIRED', 400)
    assertVisibility(body.visibility)
    Object.assign(evento, {
      name: body.name,
      startDate: body.startDate,
      endDate: body.endDate,
      language: body.language,
      // El único campo que omitir no borra: sin valor, queda la visibilidad que tenía.
      visibility: body.visibility ?? evento.visibility,
      // Como en el servicio (ADR-0009), omitir cualquiera de estos lo borra.
      externalUrl: body.externalUrl ?? null,
      invitedCount: body.invitedCount ?? null,
      estimatedBudget: body.estimatedBudget ?? null,
      description: body.description ?? null,
      seatingLayout: montajeDelSalon(body.seatingLayout),
      updatedAt: this.now(),
    })
    this.persistState()
    return structuredClone(evento)
  }

  async iniciarEvento(id: string): Promise<EventoDetail> {
    await this.delay()
    const evento = this.findEvento(id)
    if (evento.status !== 'planning') {
      throw new ApiError('Solo un evento en planificación puede iniciarse.', 409)
    }
    evento.status = 'in_progress'
    evento.updatedAt = this.now()
    this.persistState()
    return structuredClone(evento)
  }

  async cerrarEvento(id: string): Promise<EventoDetail> {
    await this.delay()
    const evento = this.findEvento(id)
    if (evento.status !== 'in_progress') {
      throw new ApiError('Solo un evento en progreso puede cerrarse.', 409)
    }
    evento.totalCost = null
    evento.status = 'closed'
    evento.updatedAt = this.now()
    this.persistState()
    return structuredClone(evento)
  }

  async cancelarEvento(id: string, razon: string): Promise<EventoDetail> {
    await this.delay()
    const evento = this.findEvento(id)
    this.assertEditable(evento)
    evento.status = 'cancelled'
    evento.cancellationReason = razon
    evento.updatedAt = this.now()
    this.persistState()
    return structuredClone(evento)
  }

  async uploadEventoImage(
    eventId: string,
    file: File,
    kind: 'Cover' | 'Gallery',
  ): Promise<EventoImage> {
    await this.delay()
    const evento = this.findEvento(eventId)

    // Validar tipo de archivo
    const isAllowed =
      ['image/jpeg', 'image/png', 'image/webp'].includes(file.type || '') ||
      file.name.toLowerCase().endsWith('.jpg') ||
      file.name.toLowerCase().endsWith('.jpeg') ||
      file.name.toLowerCase().endsWith('.png') ||
      file.name.toLowerCase().endsWith('.webp')

    if (!isAllowed) {
      throw rechazo('FILE_TYPE_NOT_ALLOWED', 400)
    }

    if (!evento.images) evento.images = []

    if (kind === 'Gallery') {
      const galleryCount = evento.images.filter((img) => img.kind === 'Gallery').length
      if (galleryCount >= 20) {
        throw rechazo('EVENT_GALLERY_IMAGE_LIMIT_REACHED', 400)
      }
    }

    // Si es Cover, reemplazamos la anterior
    if (kind === 'Cover') {
      evento.images = evento.images.filter((img) => img.kind !== 'Cover')
    }

    const image: EventoImage = {
      id: this.newId('img'),
      storedFileId: this.newId('file'),
      fileName: file.name,
      contentType: file.type || 'image/jpeg',
      sizeBytes: file.size,
      createdAt: this.now(),
      kind,
      missing: false,
    }

    evento.images.push(image)
    evento.updatedAt = this.now()
    return image
  }

  async listEventoImages(eventId: string, kind?: 'Cover' | 'Gallery'): Promise<EventoImage[]> {
    await this.delay()
    const evento = this.findEvento(eventId)
    let list = evento.images ?? []
    if (kind) {
      list = list.filter((img) => img.kind === kind)
    }
    return structuredClone(list)
  }

  async deleteEventoImage(eventId: string, imageId: string): Promise<void> {
    await this.delay()
    const evento = this.findEvento(eventId)
    if (!evento.images) evento.images = []
    const origLen = evento.images.length
    evento.images = evento.images.filter((img) => img.id !== imageId)
    if (evento.images.length === origLen) {
      throw new ApiError('Imagen no encontrada.', 404)
    }
    evento.updatedAt = this.now()
  }

  // ---------- Cartelera pública (convocatorias-service) ----------

  /**
   * La vista recortada que republica convocatorias: el listado solo trae eventos públicos y
   * activos; el detalle también sirve los cerrados. Un privado, un cancelado y uno desconocido
   * son el mismo 404. El mock no modela la ventana de inscripción, así que siempre está abierta,
   * que es lo que el servicio responde cuando no hay fila.
   */
  async listPublicEventos(): Promise<PublicEvento[]> {
    await this.delay()
    return this.eventos
      .filter(
        (e) => e.visibility === 'public' && (e.status === 'planning' || e.status === 'in_progress'),
      )
      .map((e) => this.toPublicEvento(e))
  }

  async getPublicEvento(id: string): Promise<PublicEventoDetail> {
    await this.delay()
    const evento = this.eventos.find((e) => e.id === id)
    if (!evento || evento.visibility !== 'public' || evento.status === 'cancelled') {
      throw new ApiError('Evento no encontrado.', 404, undefined, 'PUBLIC_EVENT_NOT_FOUND')
    }
    return {
      ...this.toPublicEvento(evento),
      description: evento.description ?? null,
      organizer: null,
      registrationType: evento.registrationType ?? null,
    }
  }

  private toPublicEvento(e: EventoDetail): PublicEvento {
    return {
      id: e.id,
      name: e.name,
      type: e.type,
      language: e.language ?? 'es',
      startDate: e.startDate,
      endDate: e.endDate,
      externalUrl: e.externalUrl ?? null,
      // Sin reordenar, como el servicio: el orden es el que tiene el evento.
      cities: e.cities.map((c) => ({
        name: c.name,
        country: c.country,
        date: c.date ?? null,
        displayOrder: c.displayOrder ?? null,
      })),
      registrationOpen: this.registrationWindows.get(e.id)?.open ?? true,
    }
  }

  // ---------- Portal de registro (convocatorias-service) ----------
  // El mock no manda correos, así que no hay tokens reales. Estos enlaces abren cada estado:
  //   /publico/registro#t=demo-publico  evento público, sin datos precargados
  //   /publico/registro#t=demo-privado  evento privado, con los datos del invitado
  //   /publico/registro#t=demo-cerrado  409 REGISTRATIONS_CLOSED
  //   /publico/baja#u=demo-baja         da de baja al primer contacto con correo
  //   /publico/entrada/<MOCK_DEMO_QR>   una entrada ya emitida
  //   un token que termine en -limite   429 RATE_LIMITED
  // Cualquier otro token es el 404 del servicio, que no dice por qué.

  /** qrCode → la inscripción. Un reenvío del mismo correo al mismo evento devuelve la misma. */
  private portalRegistrations = new Map<
    string,
    {
      reference: string
      qrCode: string
      eventId: string
      email: string
      name: string
      company: string | null
    }
  >()

  async resolveRegistration(token: string): Promise<ResolvedInvitationResponse> {
    await this.delay()
    const { evento, privado } = this.portalTokenEvent(token)
    const invitado = privado ? this.contacts.find((c) => c.email) : undefined
    return {
      event: this.portalEventDetail(evento),
      // Solo en un evento privado: un token público está hecho para reenviarse.
      prefill: invitado
        ? { firstName: invitado.firstName, lastName: invitado.lastName, email: invitado.email! }
        : null,
    }
  }

  async register(body: PublicRegisterRequest): Promise<PublicRegistrationResponse> {
    await this.delay()
    for (const [campo, valor] of [
      ['FirstName', body.firstName],
      ['LastName', body.lastName],
      ['Email', body.email],
    ] as const) {
      if (!valor?.trim()) throw rechazoDeCampo(campo, `El campo [${campo}] es obligatorio.`)
    }
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(body.email.trim())) {
      throw rechazoDeCampo('Email', 'El correo no tiene un formato válido.')
    }
    // Como convocatorias: el teléfono tiene que ser uno que el CRM acepte.
    assertTelefono(body.phone)

    // Sin token solo se entra a un evento público, abierto y sin terminar.
    const evento = body.token
      ? this.portalTokenEvent(body.token).evento
      : this.eventos.find(
          (e) => e.id === body.eventId && e.visibility === 'public' && this.portalEventLive(e),
        )
    if (!evento || evento.id !== body.eventId) throw rechazo('PUBLIC_REGISTRATION_NOT_FOUND', 404)

    const email = body.email.trim().toLowerCase()
    if (evento.visibility === 'private') {
      const invitados = (await this.eventParticipants(evento.id, {}))
        .filter((p) => p.origin === 'invited')
        .map((p) => p.email)
      const { correoEnListaBlanca } = await import('@/features/convocatorias/listaBlanca')
      if (!correoEnListaBlanca(evento.id, email, invitados)) {
        throw rechazo('REGISTRATION_NOT_INVITED', 403)
      }
    }
    const existente = [...this.portalRegistrations.values()].find(
      (r) => r.eventId === evento.id && r.email === email,
    )
    const registro = existente ?? {
      reference: `REF-${new Date().getFullYear()}-${String(++this.sequence).padStart(5, '0')}`,
      qrCode: mockQrCode(),
      eventId: evento.id,
      email,
      name: `${body.firstName.trim()} ${body.lastName.trim()}`,
      company: body.company?.trim() || null,
    }
    this.portalRegistrations.set(registro.qrCode, registro)
    if (!existente) await this.portalRegistrationSideEffects(evento, body, registro.reference)
    return {
      reference: registro.reference,
      qrCode: registro.qrCode,
      event: this.portalEventDetail(evento),
    }
  }

  /**
   * Lo que el servicio hace tras una inscripción nueva: el contacto se busca por correo en el CRM
   * y se crea si no está (spec convocatorias/06 §4.2). Además la deja en el store local de
   * registros públicos, que es de donde leen Invitados, Check-in y el escáner de la demo.
   */
  private async portalRegistrationSideEffects(
    evento: EventoDetail,
    body: PublicRegisterRequest,
    reference: string,
  ): Promise<void> {
    const email = body.email.trim().toLowerCase()
    if (!this.contacts.some((c) => c.email?.toLowerCase() === email)) {
      const now = this.now()
      this.contacts.push({
        ...this.contactFields(
          {
            firstName: body.firstName.trim(),
            lastName: body.lastName.trim(),
            email,
            phone: body.phone?.trim() || null,
          },
          false,
        ),
        id: this.newId('cnt'),
        classifications: [],
        createdAt: now,
        updatedAt: now,
      })
    }

    const { addRegistro, registrosPublicosGuardados } =
      await import('@/features/convocatorias/store')
    const yaGuardado = registrosPublicosGuardados().some(
      (r) => r.eventoId === evento.id && r.email.trim().toLowerCase() === email,
    )
    if (yaGuardado) return
    addRegistro({
      id: `reg-${evento.id}-${reference}`,
      eventoId: evento.id,
      eventoNombre: evento.name,
      nombre: `${body.firstName.trim()} ${body.lastName.trim()}`,
      empresa: body.company?.trim() || '',
      email,
      tipo: 'Invitado General',
      status: 'REGISTERED',
      score: 60,
    })
    // Un evento con asignación automática le da su silla al inscribirse.
    const { sentarSiEsAutomatico } = await import('@/features/convocatorias/mesas')
    sentarSiEsAutomatico(evento.id, {
      email,
      empresa: body.company?.trim() || '',
      categoria: 'Invitado General',
      fechaRegistro: this.now(),
    })
  }

  async getTicket(qrCode: string): Promise<RegistrationTicketResponse> {
    await this.delay()
    const registro = this.portalRegistration(qrCode)
    const evento = registro && this.eventos.find((e) => e.id === registro.eventId)
    if (!registro || !evento) throw rechazo('PUBLIC_REGISTRATION_NOT_FOUND', 404)
    return {
      reference: registro.reference,
      name: registro.name,
      company: registro.company,
      event: this.portalEventDetail(evento),
    }
  }

  ticketQrUrl(qrCode: string): string {
    return mockQrImage(qrCode)
  }

  /**
   * La inscripción del portal con ese QrCode. La entrada de demo se emite la primera vez que se
   * pide, en el primer evento público abierto, y desde ahí es una inscripción más.
   */
  private portalRegistration(qrCode: string) {
    let registro = this.portalRegistrations.get(qrCode)
    if (!registro && qrCode === MOCK_DEMO_QR) {
      const evento = this.eventos.find((e) => e.visibility === 'public' && this.portalEventLive(e))
      if (evento) {
        registro = {
          reference: `REF-${new Date().getFullYear()}-00001`,
          qrCode,
          eventId: evento.id,
          email: 'invitado.demo@example.com',
          name: 'Invitado Demo',
          company: 'Agencia Demo',
        }
        this.portalRegistrations.set(qrCode, registro)
      }
    }
    return registro
  }

  async unsubscribe(token: string): Promise<UnsubscribeResponse> {
    await this.delay()
    if (token.endsWith('-limite')) throw rechazo('RATE_LIMITED', 429)
    const contacto = token === 'demo-baja' ? this.contacts.find((c) => c.email) : undefined
    if (!contacto) throw rechazo('PUBLIC_UNSUBSCRIBE_NOT_FOUND', 404)
    // La baja llega al CRM como un opt-out; repetirla no cambia nada.
    this.optedOutIds.add(contacto.id)
    contacto.canBeEmailed = false
    contacto.suppressionReason = 'opted_out'
    const email = contacto.email!
    return { maskedEmail: `${email[0]}***${email.slice(email.indexOf('@'))}` }
  }

  /** El evento de un token de demostración, con los mismos rechazos que el servicio. */
  private portalTokenEvent(token: string): { evento: EventoDetail; privado: boolean } {
    if (token.endsWith('-limite')) throw rechazo('RATE_LIMITED', 429)
    const privado = token === 'demo-privado'
    const evento =
      token === 'demo-publico' || token === 'demo-cerrado' || privado
        ? this.eventos.find(
            (e) => e.visibility === (privado ? 'private' : 'public') && this.portalEventLive(e),
          )
        : undefined
    if (!evento) throw rechazo('PUBLIC_REGISTRATION_NOT_FOUND', 404)
    if (token === 'demo-cerrado') throw rechazo('REGISTRATIONS_CLOSED', 409)
    return { evento, privado }
  }

  /** Ni cancelado ni cerrado: un token solo vale mientras el evento sigue en pie. */
  private portalEventLive(e: EventoDetail): boolean {
    return e.status === 'planning' || e.status === 'in_progress'
  }

  private portalEventDetail(e: EventoDetail): PublicEventoDetail {
    return {
      ...this.toPublicEvento(e),
      description: e.description ?? null,
      organizer: null,
    }
  }

  // ---------- Ciudades ----------

  async updateCiudad(
    eventoId: string,
    ciudadId: string,
    body: CiudadRequest,
  ): Promise<CiudadResponse> {
    await this.delay()
    const evento = this.findEvento(eventoId)
    this.assertEditable(evento)
    const ciudad = evento.cities.find((c) => c.id === ciudadId)
    if (!ciudad) throw new ApiError('Ciudad no encontrada.', 404)
    assertTimeZone(body.timeZone)
    Object.assign(ciudad, {
      name: body.name,
      country: body.country,
      date: body.date ?? null,
      displayOrder: body.displayOrder ?? ciudad.displayOrder,
      timeZone: body.timeZone,
    })
    evento.updatedAt = this.now()
    return structuredClone(ciudad)
  }

  // ---------- Convocatorias (convocatorias-service) ----------

  async listTemplates(selectable?: boolean): Promise<TemplateListItem[]> {
    await this.delay()
    return structuredClone(
      selectable ? MOCK_TEMPLATES.filter((t) => t.isCampaignSelectable) : MOCK_TEMPLATES,
    )
  }

  async listCampaigns(params: ListCampaignsParams): Promise<PagedResponse<CampaignResponse>> {
    await this.delay()
    const page = Math.max(params.page ?? 1, 1)
    const pageSize = Math.min(Math.max(params.pageSize ?? 20, 1), 100)

    const matches = this.campaigns
      .filter((c) => !params.eventId || c.eventId === params.eventId)
      .filter((c) => !params.state || c.state === params.state)
      .sort((a, b) => b.createdAt.localeCompare(a.createdAt))

    return {
      items: structuredClone(matches.slice((page - 1) * pageSize, page * pageSize)),
      total: matches.length,
      page,
      pageSize,
    }
  }

  async getCampaign(id: string): Promise<CampaignResponse> {
    await this.delay()
    return structuredClone(this.findCampaign(id))
  }

  async createCampaign(body: CreateCampaignRequest): Promise<CampaignResponse> {
    await this.delay()
    assertCampaignTemplate(body.templateKey, body.language)
    assertDirectMessageRule(body)
    const now = this.now()
    const campaign: CampaignResponse = {
      id: this.newId('camp'),
      eventId: body.eventId,
      subject: body.subject ?? null,
      message: body.message ?? null,
      templateKey: body.templateKey,
      language: body.language,
      state: 'draft',
      recipientCount: 0,
      exclusionCount: 0,
      launchedById: null,
      launchedAt: null,
      sentAt: null,
      createdAt: now,
      updatedAt: now,
    }
    this.campaigns.push(campaign)
    this.campaignRecipients.set(campaign.id, new Set())
    this.campaignExclusions.set(campaign.id, new Map())
    return structuredClone(campaign)
  }

  async updateCampaign(id: string, body: UpdateCampaignRequest): Promise<CampaignResponse> {
    await this.delay()
    const campaign = this.findCampaign(id)
    this.requireDraft(campaign)
    assertCampaignTemplate(
      body.templateKey ?? campaign.templateKey,
      body.language ?? campaign.language,
    )
    assertDirectMessageRule({
      eventId: campaign.eventId,
      templateKey: body.templateKey ?? campaign.templateKey,
      subject: body.subject ?? campaign.subject,
      message: body.message ?? campaign.message,
    })
    if (body.templateKey !== undefined) campaign.templateKey = body.templateKey
    if (body.language !== undefined) campaign.language = body.language
    if (body.subject !== undefined) campaign.subject = body.subject
    if (body.message !== undefined) campaign.message = body.message
    campaign.updatedAt = this.now()
    return structuredClone(campaign)
  }

  /**
   * Igual que `CampaignRepository.SelectRecipientsAsync`: lee el "CRM" (los contactos del
   * propio mock), aditivo e idempotente — un contacto ya destinatario cuenta como
   * `skipped` — y una exclusión se borra sola si el contacto ya es mailable ahora.
   * Las clasificaciones/categorías del filtro no se aplican: el mock de contactos todavía
   * no las modela, así que el filtro siempre trae el directorio entero (comportamiento por
   * omisión válido según la spec 03 §6). `contactIds` sí se aplica: trae solo esos, y un id
   * que no existe no cuenta en `matched`. `contactListId` trae los miembros vivos de una lista
   * de la OPT de quien llama, con el mismo 403 y 404 que las rutas de listas.
   */
  async selectCampaignRecipients(
    id: string,
    body: SelectRecipientsRequest,
  ): Promise<RecipientSelectionResponse> {
    await this.delay()
    const campaign = this.findCampaign(id)
    this.requireDraft(campaign)

    const recipients = this.campaignRecipients.get(id)!
    const exclusions = this.campaignExclusions.get(id)!

    let added = 0
    let excluded = 0
    let skipped = 0
    const excludedByReason: Record<string, number> = {}

    // Las mismas reglas que el validador del servicio: una lista vacía no es "sin filtro".
    if (body.contactIds) {
      if (body.contactIds.length === 0 || body.contactIds.length > 1000) {
        throw rechazoDeCampo('contactIds', 'Elige entre 1 y 1.000 contactos.')
      }
      if (body.classifications?.length || body.companyCategories?.length) {
        throw rechazoDeCampo('contactIds', 'Los contactos elegidos no van junto con un filtro.')
      }
    }
    if (
      body.contactListId &&
      (body.contactIds || body.classifications?.length || body.companyCategories?.length)
    ) {
      throw rechazoDeCampo('contactListId', 'La lista no va junto con contactos ni con un filtro.')
    }
    const deLaLista = body.contactListId
      ? this.liveMembers(await this.findContactList(body.contactListId))
      : null
    const elegidos = body.contactIds ?? deLaLista
    const chosen = elegidos ? new Set(elegidos) : null
    const pool = chosen ? this.contacts.filter((c) => chosen.has(c.id)) : this.contacts

    for (const contact of pool) {
      if (recipients.has(contact.id)) {
        skipped += 1
        continue
      }
      if (contact.canBeEmailed) {
        if (exclusions.delete(contact.id)) {
          // Ya no está excluido: pasa a materializarse como destinatario.
        }
        recipients.add(contact.id)
        added += 1
      } else {
        const reason = contact.suppressionReason ?? 'contact_unavailable'
        if (!exclusions.has(contact.id)) {
          exclusions.set(contact.id, { reason, excludedAt: this.now() })
          excluded += 1
          excludedByReason[reason] = (excludedByReason[reason] ?? 0) + 1
        } else {
          skipped += 1
        }
      }
    }

    campaign.recipientCount = recipients.size
    campaign.exclusionCount = exclusions.size
    campaign.updatedAt = this.now()

    return {
      matched: pool.length,
      added,
      excluded,
      skipped,
      excludedByReason,
      campaign: structuredClone(campaign),
    }
  }

  async launchCampaign(id: string, body: LaunchCampaignRequest): Promise<CampaignResponse> {
    await this.delay()
    const campaign = this.findCampaign(id)
    this.requireDraft(campaign)
    assertCampaignTemplate(campaign.templateKey, campaign.language)

    const recipients = this.campaignRecipients.get(id)!
    if (recipients.size === 0) throw rechazo('CAMPAIGN_HAS_NO_RECIPIENTS', 400)

    const mailable = [...recipients].filter(
      (contactId) => this.contacts.find((c) => c.id === contactId)?.canBeEmailed,
    )
    if (mailable.length === 0) throw rechazo('CAMPAIGN_HAS_NO_MAILABLE_RECIPIENTS', 400)

    if (body.expectedRecipientCount !== recipients.size) {
      throw rechazo('CAMPAIGN_RECIPIENTS_CHANGED', 409)
    }

    campaign.state = 'sending'
    campaign.launchedById = currentMockIdentity().sub
    campaign.launchedAt = this.now()
    campaign.updatedAt = campaign.launchedAt
    // El 202 del servicio: la entrega la termina un worker después, y recién ahí la campaña
    // pasa a `sent` con su `sentAt`. Unos segundos bastan para que la pantalla lo vea cambiar.
    setTimeout(() => {
      if (campaign.state !== 'sending') return
      campaign.state = 'sent'
      campaign.sentAt = this.now()
      campaign.updatedAt = campaign.sentAt
    }, 4000)
    return structuredClone(campaign)
  }

  async cancelCampaign(id: string): Promise<CampaignResponse> {
    await this.delay()
    const campaign = this.findCampaign(id)
    if (campaign.state !== 'draft' && campaign.state !== 'sending') {
      throw rechazo('CAMPAIGN_TRANSITION_NOT_ALLOWED', 409)
    }
    campaign.state = 'cancelled'
    campaign.updatedAt = this.now()
    return structuredClone(campaign)
  }

  /**
   * Como el servicio: solo sobre `sending` o `sent`. El mock no guarda destinatarios
   * rechazados, así que es el caso "sin ninguna fila rechazada": no hace nada y contesta igual.
   */
  async retryCampaignFailed(id: string): Promise<CampaignResponse> {
    await this.delay()
    const campaign = this.findCampaign(id)
    if (campaign.state !== 'sending' && campaign.state !== 'sent') {
      throw rechazo('CAMPAIGN_NOT_SENDING', 409)
    }
    return structuredClone(campaign)
  }

  /**
   * No lee el archivo de verdad — sería reimplementar un parser de xlsx/csv en el
   * navegador para una demostración. Genera filas deterministas por nombre de archivo,
   * en las mismas seis categorías que describe la spec 05 §4, para que la pantalla de
   * revisión tenga algo real que mostrar antes de confirmar.
   */
  async stageCampaignRecipientUpload(id: string, file: File): Promise<RecipientUploadResponse> {
    await this.delay()
    const campaign = this.findCampaign(id)
    this.requireDraft(campaign)

    const rng = mockMulberry32(mockHashString(`${id}:${file.name}:${file.size}`))
    const rowCount = 6 + Math.floor(rng() * 10)
    const rows: RecipientUploadRow[] = []
    const buckets: UploadRowBucket[] = [
      'matched',
      'matched',
      'matched',
      'unmatched',
      'unmatched',
      'suppressed',
      'already_a_recipient',
      'malformed',
    ]

    for (let i = 0; i < rowCount; i++) {
      const bucket = buckets[Math.floor(rng() * buckets.length)]
      const firstName = bucket === 'malformed' ? null : `Invitado${i + 1}`
      const lastName = bucket === 'malformed' ? null : 'Demo'
      rows.push({
        sourceRowNumber: i + 2,
        firstName,
        lastName,
        email:
          bucket === 'malformed' ? null : `invitado${i + 1}.${file.name.length}@demo.example.com`,
        phone: null,
        company: 'Empresa Demo',
        bucket,
        reasonCode:
          bucket === 'suppressed' ? 'opted_out' : bucket === 'malformed' ? 'missing_name' : null,
        contactId:
          bucket === 'matched' || bucket === 'already_a_recipient' ? this.newId('cnt') : null,
        duplicateRowNumbers: null,
        collapsedDuplicates: null,
      })
    }

    const counts = {
      matched: rows.filter((r) => r.bucket === 'matched').length,
      unmatched: rows.filter((r) => r.bucket === 'unmatched').length,
      suppressed: rows.filter((r) => r.bucket === 'suppressed').length,
      alreadyARecipient: rows.filter((r) => r.bucket === 'already_a_recipient').length,
      duplicateInFile: rows.filter((r) => r.bucket === 'duplicate_in_file').length,
      malformed: rows.filter((r) => r.bucket === 'malformed').length,
      collapsedExactDuplicates: 0,
    }

    const pending: RecipientUploadResponse & { rows: RecipientUploadRow[] } = {
      id: this.newId('upl'),
      campaignId: id,
      originalFileName: file.name,
      detectedEncoding: 'utf-8',
      rowCount: rows.length,
      counts,
      confirmableCount: counts.matched + counts.unmatched,
      uploadedAt: this.now(),
      uploadedById: currentMockIdentity().sub,
      confirmedAt: null,
      confirmedById: null,
      rows,
    }
    this.pendingUploads.set(id, pending)
    return structuredClone(pending)
  }

  async confirmCampaignRecipientUpload(id: string): Promise<RecipientUploadResponse> {
    await this.delay()
    this.findCampaign(id)
    const pending = this.pendingUploads.get(id)
    if (!pending) throw rechazo('UPLOAD_NOT_FOUND', 404)

    const recipients = this.campaignRecipients.get(id)!
    for (const row of pending.rows) {
      if (row.bucket === 'unmatched' && row.email) {
        const now = this.now()
        const contact: ContactResponse = {
          id: row.contactId ?? this.newId('cnt'),
          name: `${row.firstName ?? ''} ${row.lastName ?? ''}`.trim(),
          firstName: row.firstName ?? '',
          lastName: row.lastName ?? '',
          email: row.email,
          phone: row.phone,
          createdAt: now,
          updatedAt: now,
          canBeEmailed: true,
          suppressionReason: null,
          classifications: [],
        }
        this.contacts.push(contact)
        recipients.add(contact.id)
      } else if (row.bucket === 'matched' && row.contactId) {
        recipients.add(row.contactId)
      }
    }

    const campaign = this.findCampaign(id)
    campaign.recipientCount = recipients.size
    campaign.updatedAt = this.now()

    // Igual que el `Confirm` real: la respuesta es el lote sin las filas, que solo
    // viajan en la de `stage`.
    const response: RecipientUploadResponse = {
      id: pending.id,
      campaignId: pending.campaignId,
      originalFileName: pending.originalFileName,
      detectedEncoding: pending.detectedEncoding,
      rowCount: pending.rowCount,
      counts: pending.counts,
      confirmableCount: pending.confirmableCount,
      uploadedAt: pending.uploadedAt,
      uploadedById: pending.uploadedById,
      confirmedAt: this.now(),
      confirmedById: currentMockIdentity().sub,
    }
    this.pendingUploads.delete(id)
    return structuredClone(response)
  }

  /**
   * El mock no guarda inscripciones: cuenta como inscritos a los destinatarios de las campañas
   * lanzadas del evento. Como el servicio, exige el evento cancelado y avisa una sola vez.
   */
  async sendCancellationNotice(eventId: string): Promise<CancellationNoticeResponse> {
    await this.delay()
    if (this.findEvento(eventId).status !== 'cancelled') throw rechazo('EVENT_NOT_CANCELLED', 409)
    const notified = this.cancellationNoticeSent.has(eventId)
      ? 0
      : this.campaigns
          .filter((c) => c.eventId === eventId && (c.state === 'sending' || c.state === 'sent'))
          .reduce((total, c) => total + c.recipientCount, 0)
    this.cancellationNoticeSent.add(eventId)
    return { eventId, notified, failed: 0 }
  }

  /**
   * Como `TrackingService`: solo cuentan las campañas lanzadas, y cada persona una vez. Un
   * destinatario que se puede escribir cuenta como entregado; el resto, como suprimido. El
   * mock no guarda inscripciones, así que las de invitación salen de una proporción fija por
   * evento; referidos y entradas libres quedan en cero. La ventana siempre está abierta.
   */
  async getEventTracking(eventId: string): Promise<EventTrackingResponse> {
    await this.delay()
    const launched = this.campaigns.filter((c) => c.eventId === eventId && c.launchedAt)
    const people = new Set(launched.flatMap((c) => [...(this.campaignRecipients.get(c.id) ?? [])]))
    const reachable = [...people].filter(
      (id) => this.contacts.find((c) => c.id === id)?.canBeEmailed,
    ).length
    // Los registrados salen de la misma lista de participantes que muestran las pantallas.
    const participantes = await this.eventParticipants(eventId, {})
    const fromInvitation = participantes.filter(
      (p) => p.registered && p.origin === 'invited',
    ).length
    const referred = participantes.filter((p) => p.registered && p.origin === 'referred').length
    const walkIn = participantes.filter((p) => p.registered && p.origin === 'walk_in').length
    const invited = participantes.filter((p) => p.origin === 'invited').length
    const window = this.registrationWindows.get(eventId)

    return {
      campaigns: launched.map((c) => ({
        id: c.id,
        templateKey: c.templateKey,
        state: c.state,
        sentAt: c.sentAt ?? null,
        recipientCount: c.recipientCount,
        exclusionCount: c.exclusionCount,
      })),
      people: { invited, pendingSend: 0, failed: 0, suppressed: people.size - reachable },
      registrations: {
        total: fromInvitation + referred + walkIn,
        fromInvitation,
        referred,
        walkIn,
      },
      notRegistered: Math.max(0, invited - fromInvitation),
      needsAttention: 0,
      registrationWindow: {
        open: window?.open ?? true,
        closedAt: window?.closedAt ?? null,
        closedById: window?.closedById ?? null,
      },
    }
  }

  /** Como el servicio: cerrar lo ya cerrado conserva la hora y la persona del primer cierre. */
  async closeEventRegistrations(eventId: string): Promise<RegistrationWindowResponse> {
    await this.delay()
    const current = this.registrationWindows.get(eventId)
    if (current && !current.open) return current
    const closed: RegistrationWindowResponse = {
      eventId,
      open: false,
      closedAt: new Date().toISOString(),
      closedById: currentMockIdentity().sub,
      reopenedAt: current?.reopenedAt ?? null,
      reopenedById: current?.reopenedById ?? null,
    }
    this.registrationWindows.set(eventId, closed)
    return closed
  }

  async reopenEventRegistrations(eventId: string): Promise<RegistrationWindowResponse> {
    await this.delay()
    const current = this.registrationWindows.get(eventId)
    if (!current) return { eventId, open: true }
    if (current.open) return current
    const reopened: RegistrationWindowResponse = {
      ...current,
      open: true,
      reopenedAt: new Date().toISOString(),
      reopenedById: currentMockIdentity().sub,
    }
    this.registrationWindows.set(eventId, reopened)
    return reopened
  }

  async listEventParticipants(
    eventId: string,
    params: ListParticipantsParams,
  ): Promise<PagedResponse<ParticipantResponse>> {
    await this.delay()
    return participantsPage(await this.eventParticipants(eventId, params), params)
  }

  /** Por campaña, tenga evento o no: la misma unión que `listEventParticipants` con su id. */
  async listCampaignRecipients(
    id: string,
    params: { page?: number; pageSize?: number },
  ): Promise<PagedResponse<ParticipantResponse>> {
    await this.delay()
    const campaign = this.findCampaign(id)
    return participantsPage(
      await this.eventParticipants(campaign.eventId, { campaignId: id }),
      params,
    )
  }

  async listCampaignExclusions(
    id: string,
    params: { page?: number; pageSize?: number },
  ): Promise<PagedResponse<CampaignExclusionRow>> {
    await this.delay()
    this.findCampaign(id)
    const page = Math.max(params.page ?? 1, 1)
    const pageSize = Math.min(Math.max(params.pageSize ?? 20, 1), 100)
    const rows: CampaignExclusionRow[] = [...(this.campaignExclusions.get(id) ?? [])].map(
      ([contactId, { reason, excludedAt }]) => {
        const contact = this.contacts.find((c) => c.id === contactId)
        return {
          id: `exc-${id}-${contactId}`,
          contactId,
          firstName: contact?.firstName ?? '',
          lastName: contact?.lastName ?? '',
          email: contact?.email ?? null,
          reason,
          excludedAt,
        }
      },
    )
    return {
      items: structuredClone(rows.slice((page - 1) * pageSize, page * pageSize)),
      total: rows.length,
      page,
      pageSize,
    }
  }

  // ---------- Check-in en la puerta (spec convocatorias/09 §3-§5) ----------

  /** registrationId → su check-in, las dos columnas del servicio. Deshacer las borra. */
  private checkIns = new Map<string, { checkedInAt: string; checkedInById: string }>()

  /**
   * Las inscripciones de un evento, una por correo: los invitados inscritos de la demo, que solo
   * se encuentran en la lista porque no tienen entrada, y las del portal, que traen su QrCode.
   */
  private async doorRegistrations(eventId: string): Promise<MockDoorRegistration[]> {
    const byEmail = new Map<string, MockDoorRegistration>()
    for (const p of await this.eventParticipants(eventId, { registered: true })) {
      if (!p.reference) continue
      const email = p.email.trim().toLowerCase()
      byEmail.set(email, {
        registrationId: `reg-${eventId}-${p.reference}`,
        firstName: p.firstName,
        lastName: p.lastName,
        company: p.company ?? null,
        reference: p.reference,
        email,
        qrCode: null,
      })
    }
    for (const r of this.portalRegistrations.values()) {
      if (r.eventId !== eventId) continue
      const [firstName, ...rest] = r.name.split(' ')
      byEmail.set(r.email, {
        registrationId: `reg-${eventId}-${r.reference}`,
        firstName,
        lastName: rest.join(' '),
        company: r.company,
        reference: r.reference,
        email: r.email,
        qrCode: r.qrCode,
      })
    }
    for (const p of this.raffleParticipants[eventId] ?? []) {
      const [firstName, ...rest] = p.fullName.trim().split(/\s+/)
      byEmail.set(p.identityKey, {
        registrationId: `reg-${eventId}-${p.id}`,
        firstName: firstName || p.fullName,
        lastName: rest.join(' '),
        company: p.company,
        reference: p.employeeCode || p.qrCode,
        email: p.email ?? '',
        qrCode: p.qrCode,
      })
    }
    return [...byEmail.values()]
  }

  /** Como el `UPDATE … WHERE checked_in_at IS NULL` del servicio: el primero gana y se repite. */
  private stampCheckIn(row: MockDoorRegistration): CheckInResponse {
    const existing = this.checkIns.get(row.registrationId)
    const stamp = existing ?? { checkedInAt: this.now(), checkedInById: currentMockIdentity().sub }
    if (!existing) {
      this.checkIns.set(row.registrationId, stamp)
      this.persistState()
    }
    return {
      registrationId: row.registrationId,
      firstName: row.firstName,
      lastName: row.lastName,
      company: row.company,
      reference: row.reference,
      ...stamp,
      alreadyCheckedIn: Boolean(existing),
    }
  }

  async checkIn(eventId: string, qrCode: string): Promise<CheckInResponse> {
    await this.delay()
    const raw = qrCode?.trim()
    if (!raw) throw rechazoDeCampo('QrCode', 'El campo [QrCode] es obligatorio.')
    if (raw.length > 256) {
      throw rechazoDeCampo('QrCode', 'El campo [QrCode] no debe superar los 256 caracteres.')
    }
    const norm = raw.toLowerCase()
    const matchIn = (evId: string) =>
      (this.raffleParticipants[evId] ?? []).find(
        (p) =>
          p.qrCode.toLowerCase() === norm ||
          p.email?.toLowerCase() === norm ||
          (p.employeeCode && p.employeeCode.toLowerCase() === norm),
      )

    const participant = matchIn(eventId)
    if (participant) {
      const alreadyCheckedIn = participant.checkedIn
      const now = participant.checkedInAt ?? this.now()
      participant.checkedIn = true
      participant.checkedInAt = now
      const regId = `reg-${eventId}-${participant.id}`
      if (!this.checkIns.has(regId)) {
        this.checkIns.set(regId, { checkedInAt: now, checkedInById: currentMockIdentity().sub })
      }
      this.persistState()
      const [firstName, ...rest] = participant.fullName.trim().split(/\s+/)
      return {
        registrationId: regId,
        firstName: firstName || participant.fullName,
        lastName: rest.join(' '),
        company: participant.company,
        reference: participant.employeeCode || participant.qrCode,
        checkedInAt: now,
        checkedInById: this.checkIns.get(regId)?.checkedInById ?? currentMockIdentity().sub,
        alreadyCheckedIn,
      }
    }

    for (const [otherEvId] of Object.entries(this.raffleParticipants)) {
      if (otherEvId !== eventId && matchIn(otherEvId)) {
        throw rechazo('CHECK_IN_WRONG_EVENT')
      }
    }

    const registro = this.portalRegistration(raw)
    if (!registro) throw rechazo('CHECK_IN_CODE_NOT_FOUND', 404)
    if (registro.eventId !== eventId) throw rechazo('CHECK_IN_WRONG_EVENT')
    const row = (await this.doorRegistrations(eventId)).find((r) => r.qrCode === raw)!
    return this.stampCheckIn(row)
  }

  async checkInByRegistration(eventId: string, registrationId: string): Promise<CheckInResponse> {
    await this.delay()
    const row = (await this.doorRegistrations(eventId)).find(
      (r) => r.registrationId === registrationId,
    )
    if (row) {
      const p = (this.raffleParticipants[eventId] ?? []).find(
        (x) => `reg-${eventId}-${x.id}` === registrationId,
      )
      if (p) {
        p.checkedIn = true
        p.checkedInAt = p.checkedInAt ?? this.now()
      }
      return this.stampCheckIn(row)
    }
    const otro = this.eventos.find(
      (e) => e.id !== eventId && registrationId.startsWith(`reg-${e.id}-`),
    )
    const deOtroEvento =
      otro !== undefined &&
      (await this.doorRegistrations(otro.id)).some((r) => r.registrationId === registrationId)
    throw deOtroEvento ? rechazo('CHECK_IN_WRONG_EVENT') : rechazo('REGISTRATION_NOT_FOUND', 404)
  }

  /** 204 haya o no check-in que borrar; 404 si la inscripción no es de este evento. */
  async undoCheckIn(eventId: string, registrationId: string): Promise<void> {
    await this.delay()
    const rows = await this.doorRegistrations(eventId)
    if (!rows.some((r) => r.registrationId === registrationId)) {
      throw rechazo('REGISTRATION_NOT_FOUND', 404)
    }
    this.checkIns.delete(registrationId)
    const p = (this.raffleParticipants[eventId] ?? []).find(
      (x) => `reg-${eventId}-${x.id}` === registrationId,
    )
    if (p) {
      p.checkedIn = false
      p.checkedInAt = null
    }
    this.persistState()
  }

  /**
   * Como el servicio: cada palabra en el nombre, el apellido o la Reference, sin tildes ni
   * mayúsculas, o el correo exacto; los totales sin filtros, y nunca un correo en la respuesta.
   */
  async listDoorRegistrations(
    eventId: string,
    params: ListDoorRegistrationsParams,
  ): Promise<DoorListResponse> {
    await this.delay()
    const rows = await this.doorRegistrations(eventId)
    const fold = (text: string) => text.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase()
    const search = params.search?.trim() ?? ''
    const words = fold(search).split(/\s+/).filter(Boolean)
    const checkedIn = (id: string) => this.checkIns.has(id)
    const matching = rows
      .filter(
        (r) =>
          !search ||
          r.email === search.toLowerCase() ||
          words.every((w) =>
            [r.firstName, r.lastName, r.reference].some((field) => fold(field).includes(w)),
          ),
      )
      .filter(
        (r) => params.checkedIn === undefined || checkedIn(r.registrationId) === params.checkedIn,
      )
      .sort(
        (a, b) =>
          a.lastName.localeCompare(b.lastName) ||
          a.firstName.localeCompare(b.firstName) ||
          a.reference.localeCompare(b.reference),
      )
    const page = Math.max(params.page ?? 1, 1)
    const pageSize = Math.min(Math.max(params.pageSize ?? 50, 1), 100)
    return {
      totals: {
        registered: rows.length,
        checkedIn: rows.filter((r) => checkedIn(r.registrationId)).length,
      },
      items: matching.slice((page - 1) * pageSize, page * pageSize).map((r) => ({
        registrationId: r.registrationId,
        firstName: r.firstName,
        lastName: r.lastName,
        company: r.company,
        reference: r.reference,
        checkedInAt: this.checkIns.get(r.registrationId)?.checkedInAt ?? null,
      })),
      total: matching.length,
      page,
      pageSize,
    }
  }

  /** Como el servicio: solo los contactos pedidos, lo más reciente primero. */
  async listContactCheckIns(contactIds: string[]): Promise<ContactCheckInResponse[]> {
    await this.delay()
    const pedidos = new Set(contactIds)
    return structuredClone(
      seedContactCheckIns
        .filter((c) => pedidos.has(c.contactId))
        .sort((a, b) => b.checkedInAt.localeCompare(a.checkedInAt)),
    )
  }

  /**
   * Como el servicio: solo campañas lanzadas y los contactos pedidos, lo más reciente primero.
   * Inscrito es una inscripción del portal en el evento con la dirección del contacto.
   */
  async listContactCampaigns(contactIds: string[]): Promise<ContactCampaignResponse[]> {
    await this.delay()
    const { registrosPublicosGuardados } = await import('@/features/convocatorias/store')
    const portal = registrosPublicosGuardados()
    const filas: ContactCampaignResponse[] = []
    for (const campaign of this.campaigns) {
      if (!campaign.launchedAt) continue
      for (const contactId of contactIds) {
        if (!this.campaignRecipients.get(campaign.id)?.has(contactId)) continue
        const email = this.contacts
          .find((c) => c.id === contactId)
          ?.email?.trim()
          .toLowerCase()
        filas.push({
          contactId,
          campaignId: campaign.id,
          eventId: campaign.eventId,
          subject: campaign.subject ?? null,
          launchedAt: campaign.launchedAt,
          sendStatus: campaign.state === 'sending' ? 'pending' : 'handed_off',
          // Como si el proveedor ya hubiera confirmado: en local, sin eventos, sería `sent`.
          deliveryStatus: campaign.state === 'sending' ? null : 'delivered',
          registered: campaign.eventId
            ? portal.some(
                (r) => r.eventoId === campaign.eventId && r.email.trim().toLowerCase() === email,
              )
            : null,
        })
      }
    }
    return filas.sort((a, b) => b.launchedAt.localeCompare(a.launchedAt))
  }

  /** Mismas columnas, BOM, CRLF y comillas que `ParticipantCsv.cs`. */
  async exportEventParticipantsCsv(
    eventId: string,
    params: ListParticipantsParams,
  ): Promise<ParticipantsCsvFile> {
    await this.delay()
    const rows = await this.eventParticipants(eventId, params)
    const escape = (value: string | null | undefined): string => {
      if (!value) return ''
      const cell = /^[=+\-@\t\r]/.test(value) ? `'${value}` : value
      return /[",\r\n]/.test(cell) ? `"${cell.replace(/"/g, '""')}"` : cell
    }
    const columns: [string, (p: ParticipantResponse) => string | null | undefined][] = [
      ['Correo', (p) => p.email],
      ['Nombre', (p) => p.firstName],
      ['Apellido', (p) => p.lastName],
      ['Empresa', (p) => p.company],
      ['Origen', (p) => p.origin],
      ['Estado de envío', (p) => p.sendStatus],
      ['Inscrito', (p) => String(p.registered)],
      ['Fecha de inscripción', (p) => p.registeredAt?.replace(/\.\d+Z$/, 'Z')],
      ['Referencia', (p) => p.reference],
      ['Referido por', (p) => p.referredBy?.name],
      ['Campañas', (p) => p.campaignIds.join(' ')],
      ['Sincronización CRM pendiente', (p) => String(p.crmSyncPending)],
      ['Datos distintos', (p) => String(p.detailsDiffer)],
    ]
    const lines = [
      columns.map(([header]) => escape(header)),
      ...rows.map((row) => columns.map(([, value]) => escape(value(row)))),
    ].map((cells) => cells.join(',') + '\r\n')
    // República Dominicana es siempre UTC-4.
    const today = new Date(Date.now() - 4 * 3_600_000).toISOString().slice(0, 10)
    return {
      blob: new Blob(['﻿', ...lines], { type: 'text/csv;charset=utf-8' }),
      fileName: `convocatoria-${eventId}-${today}.csv`,
    }
  }

  /**
   * Igual que `ParticipantService`: una fila por correo (recortado y en minúsculas) que une los
   * destinatarios de las campañas del evento con las inscripciones, filtrada y ordenada por
   * apellido, nombre y correo. Un borrador solo entra con su `campaignId`. Además de las
   * campañas del mock, siembra los invitados simulados del evento y los registros del portal
   * público, para que la demo no arranque vacía; algunos inscritos traen `detailsDiffer`.
   */
  private async eventParticipants(
    eventId: string | null,
    params: ListParticipantsParams,
  ): Promise<ParticipantResponse[]> {
    const byEmail = new Map<string, ParticipantResponse>()
    const upsert = (row: ParticipantResponse) => {
      const key = row.email.trim().toLowerCase()
      const current = byEmail.get(key)
      if (!current) byEmail.set(key, row)
      else current.campaignIds = [...new Set([...current.campaignIds, ...row.campaignIds])]
    }

    for (const campaign of this.campaigns) {
      if (campaign.eventId !== eventId) continue
      if (campaign.state === 'draft' && params.campaignId !== campaign.id) continue
      if (params.campaignId && params.campaignId !== campaign.id) continue
      for (const contactId of this.campaignRecipients.get(campaign.id) ?? []) {
        const contact = this.contacts.find((c) => c.id === contactId)
        if (!contact?.email) continue
        upsert({
          email: contact.email,
          firstName: contact.firstName,
          lastName: contact.lastName,
          company: contact.companies?.[0]?.name ?? null,
          origin: 'invited',
          sendStatus: campaign.state === 'sent' ? 'handed_off' : 'pending',
          registered: false,
          campaignIds: [campaign.id],
          crmSyncPending: false,
          detailsDiffer: false,
        })
      }
    }

    // Un mensaje directo no tiene evento: ni invitados simulados ni inscritos del portal.
    const evento = eventId ? this.eventos.find((e) => e.id === eventId) : undefined
    if (evento) {
      const [{ invitadosFor, ESTADOS_INSCRITOS }, { registrosPublicosGuardados }] =
        await Promise.all([
          import('@/features/convocatorias/mockParticipants'),
          import('@/features/convocatorias/store'),
        ])
      const portal = registrosPublicosGuardados().filter((r) => r.eventoId === eventId)
      invitadosFor(evento).forEach((invitado, index) => {
        const [firstName, ...rest] = invitado.nombre.split(' ')
        const registered = ESTADOS_INSCRITOS.includes(invitado.status)
        upsert({
          email: invitado.email,
          firstName,
          lastName: rest.join(' '),
          company: invitado.empresa,
          origin: 'invited',
          sendStatus: 'handed_off',
          registered,
          registeredAt: registered ? evento.updatedAt : null,
          reference: registered ? `REF-2026-${String(index + 1).padStart(5, '0')}` : null,
          campaignIds: [],
          crmSyncPending: false,
          detailsDiffer: registered && index % 7 === 3,
        })
      })
      for (const registro of portal) {
        const [firstName, ...rest] = registro.nombre.split(' ')
        const key = registro.email.trim().toLowerCase()
        const invited = byEmail.get(key)
        if (invited) {
          invited.registered = true
          continue
        }
        upsert({
          email: registro.email,
          firstName,
          lastName: rest.join(' '),
          company: registro.empresa,
          origin: 'walk_in',
          sendStatus: 'none',
          registered: true,
          registeredAt: this.now(),
          campaignIds: [],
          crmSyncPending: false,
          detailsDiffer: false,
        })
      }
    }

    const fold = (text: string) => text.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase()
    const search = params.search?.trim() ? fold(params.search.trim()) : null
    return [...byEmail.values()]
      .filter((p) => !params.origin || p.origin === params.origin)
      .filter((p) => !params.sendStatus || p.sendStatus === params.sendStatus)
      .filter((p) => params.registered === undefined || p.registered === params.registered)
      .filter((p) => !params.campaignId || p.campaignIds.includes(params.campaignId))
      .filter(
        (p) =>
          !search ||
          fold(p.email).includes(search) ||
          fold(`${p.firstName} ${p.lastName}`).includes(search),
      )
      .sort(
        (a, b) =>
          a.lastName.localeCompare(b.lastName) ||
          a.firstName.localeCompare(b.firstName) ||
          a.email.localeCompare(b.email),
      )
  }

  private findCampaign(id: string): CampaignResponse {
    const campaign = this.campaigns.find((c) => c.id === id)
    if (!campaign) throw new ApiError('La campaña no fue encontrada.', 404)
    return campaign
  }

  private requireDraft(campaign: CampaignResponse): void {
    if (campaign.state !== 'draft') throw rechazo('CAMPAIGN_NOT_DRAFT', 409)
  }

  async getFileContentUrl(storedFileId: string): Promise<string> {
    await this.delay()
    return `https://placehold.co/600x400?text=File+${storedFileId}`
  }

  // ---------- Historial de envíos (notification-service) ----------

  /**
   * Mismas reglas que el validator y el lector del servicio: estados del conjunto cerrado, página
   * de 25 con tope 100 (más es un 400, no un recorte), `entityType` y `entityId` juntos, correo
   * exacto, fechas sobre `queuedAt`, y lo más reciente primero.
   */
  async listNotifications(
    params: ListNotificationsParams,
  ): Promise<PagedResponse<NotificationHistoryItem>> {
    await this.delay()
    const page = params.page ?? 1
    const pageSize = params.pageSize ?? 25
    if (page < 1) throw rechazoDeCampo('Page', "'Page' debe ser mayor o igual a 1.")
    if (pageSize < 1 || pageSize > 100)
      throw rechazoDeCampo('PageSize', "'PageSize' debe estar entre 1 y 100.")
    if (
      params.deliveryStatus &&
      !(NOTIFICATION_DELIVERY_STATUSES as readonly string[]).includes(params.deliveryStatus)
    )
      throw rechazoDeCampo(
        'DeliveryStatus',
        `El deliveryStatus debe ser uno de: ${[...NOTIFICATION_DELIVERY_STATUSES].sort().join(', ')}.`,
      )
    if (
      params.recipientAction &&
      !['none', 'unsubscribed', 'complained'].includes(params.recipientAction)
    )
      throw rechazoDeCampo(
        'RecipientAction',
        'El recipientAction debe ser uno de: complained, none, unsubscribed.',
      )
    const email = params.email?.trim()
    if (email && email.length > 320)
      throw rechazoDeCampo('Email', 'El email no puede pasar de 320 caracteres.')
    if (!params.entityType !== !params.entityId)
      throw rechazoDeCampo(
        '',
        "Los parámetros 'entityType' y 'entityId' van juntos: o los dos o ninguno.",
      )
    const desde = params.sentFrom ? Date.parse(params.sentFrom) : undefined
    const hasta = params.sentTo ? Date.parse(params.sentTo) : undefined
    if (desde !== undefined && hasta !== undefined && hasta < desde)
      throw rechazoDeCampo('SentTo', 'sentTo debe ser mayor o igual que sentFrom.')

    const matches = this.notificationBatches
      .flatMap((batch) => batch.recipients)
      .filter((r) => !params.type || r.type === params.type)
      .filter((r) => !params.deliveryStatus || r.deliveryStatus === params.deliveryStatus)
      .filter((r) => !params.recipientAction || r.recipientAction === params.recipientAction)
      .filter((r) => !email || r.email === email)
      .filter(
        (r) =>
          !params.entityType ||
          r.about.some((a) => a.entityType === params.entityType && a.entityId === params.entityId),
      )
      .filter(
        (r) =>
          !params.sourceService || r.about.some((a) => a.sourceService === params.sourceService),
      )
      .filter((r) => desde === undefined || Date.parse(r.queuedAt) >= desde)
      .filter((r) => hasta === undefined || Date.parse(r.queuedAt) <= hasta)
      .sort((a, b) => b.queuedAt.localeCompare(a.queuedAt) || a.id.localeCompare(b.id))

    return {
      items: structuredClone(matches.slice((page - 1) * pageSize, page * pageSize)),
      total: matches.length,
      page,
      pageSize,
    }
  }

  /** El servicio contesta 404 sin cuerpo cuando el envío no existe. */
  async getNotificationBatch(batchId: string): Promise<NotificationBatch> {
    await this.delay()
    const batch = this.notificationBatches.find((b) => b.id === batchId)
    if (!batch) throw new ApiError('Recurso no encontrado.', 404)
    return structuredClone(batch)
  }

  // ---------- Sorteos ----------
  // De forma, a propósito (Decisión 3): semillas, las transiciones del camino feliz y los
  // códigos sobre los que la pantalla ramifica. No hay calificación de asistentes: un borrador
  // siempre está listo para arrancar, porque una segunda copia de esa tubería divergiría de la del
  // servicio.

  private assertRaffleRole(role: FunctionRole): void {
    if (!currentMockIdentity().roles.includes(role)) throw new ApiError(FORBIDDEN_MESSAGE, 403)
  }

  private findRaffle(eventId: string): MockRaffle {
    const raffle = this.raffles.find((r) => r.eventId === eventId)
    if (!raffle) throw rechazo('RAFFLE_NOT_FOUND', 404)
    return raffle
  }

  /** Armar un sorteo: no uno completado, ni el de un evento cerrado o cancelado. */
  private findRaffleForSetup(eventId: string): MockRaffle {
    this.assertRaffleRole(ROLES.rafflesWrite)
    const raffle = this.findRaffle(eventId)
    if (raffle.state === 'completed') throw rechazo('RAFFLE_COMPLETED')
    this.assertRaffleEventEditable(eventId)
    return raffle
  }

  private assertRaffleEventEditable(eventId: string): void {
    const evento = this.findEvento(eventId)
    if (evento.status === 'closed' || evento.status === 'cancelled') {
      throw rechazo('EVENT_NOT_EDITABLE')
    }
  }

  private findPrize(raffle: MockRaffle, prizeId: string): PrizeResponse {
    const prize = raffle.rounds.flatMap((r) => r.prizes).find((p) => p.id === prizeId)
    if (!prize) throw rechazo('PRIZE_NOT_FOUND', 404)
    return prize
  }

  private findRound(raffle: MockRaffle, roundId: string) {
    const round = raffle.rounds.find((r) => r.id === roundId)
    if (!round) throw rechazo('ROUND_NOT_FOUND', 404)
    return round
  }

  private prizeStateOf(draws: PrizeDrawSummary[]): PrizeState {
    const live = draws.find((d) => d.void === null)
    if (!live) return 'undrawn'
    return live.handovers.some((h) => h.reversedAt === null) ? 'redeemed' : 'drawn'
  }

  private page<T>(items: T[], params: PagingParams): RafflePage<T> {
    const page = params.page ?? 1
    const pageSize = params.pageSize ?? 20
    return {
      items: structuredClone(items.slice((page - 1) * pageSize, page * pageSize)),
      total: items.length,
      page,
      pageSize,
    }
  }

  async getRaffle(eventId: string): Promise<RaffleResponse> {
    await this.delay()
    const raffle = structuredClone(this.findRaffle(eventId))
    for (const prize of raffle.rounds.flatMap((r) => r.prizes)) {
      prize.state = this.prizeStateOf(prize.draws)
    }
    const hasPrizes = raffle.rounds.some((r) => r.prizes.length > 0)
    const draft = raffle.state === 'draft'
    const blockingReasons = this.doorListOf(eventId).some((a) => a.qualified)
      ? []
      : ['no_qualified_attendees' as const]
    return {
      ...raffle,
      isReadyToStart: draft ? blockingReasons.length === 0 : null,
      blockingReasons: draft ? blockingReasons : null,
      // Sin lista de DO Travel Awards cargada, el paso siempre se salta.
      warnings: ['awards_list_not_in_force', ...(hasPrizes ? [] : ['no_prizes' as const])],
    }
  }

  async createRaffle(eventId: string): Promise<string> {
    await this.delay()
    this.assertRaffleRole(ROLES.rafflesWrite)
    const evento = this.findEvento(eventId)
    if (this.raffles.some((r) => r.eventId === eventId)) throw rechazo('RAFFLE_ALREADY_EXISTS')
    if (evento.status === 'closed' || evento.status === 'cancelled') {
      throw rechazo('EVENT_NOT_EDITABLE')
    }
    const id = this.newId('raffle')
    this.raffles.unshift({
      id,
      eventId,
      state: 'draft',
      // Los valores con los que nace en el servicio (`Raffle.CreateDraft`).
      visualStyle: 'roulette',
      showPrizeOnScreen: true,
      animationSeconds: 6,
      requiresPresenceConfirmation: false,
      onePrizePerPerson: true,
      onePrizePerCompany: false,
      notaryName: null,
      notaryIdentityDocument: null,
      witnessOneName: null,
      witnessOneIdentityDocument: null,
      witnessTwoName: null,
      witnessTwoIdentityDocument: null,
      startedAt: null,
      startedById: null,
      awardsBatchId: null,
      pickupInstructions: null,
      certificateSigners: [],
      completedAt: null,
      completedById: null,
      notarialRecords: [],
      rounds: [],
    })
    this.persistState()
    return id
  }

  async updateRaffle(eventId: string, body: UpdateRaffleRequest): Promise<void> {
    await this.delay()
    const raffle = this.findRaffleForSetup(eventId)
    if (body.animationSeconds < 5 || body.animationSeconds > 30) {
      throw rechazoDeCampo(
        'AnimationSeconds',
        'La duración de la animación debe estar entre 5 y 30 segundos.',
      )
    }
    const linkedEventId = this.resolveLink(raffle, body.linkedEventId ?? null)
    // Como el servicio: un premio por persona no se apaga.
    Object.assign(raffle, body, { onePrizePerPerson: true, linkedEventId })
    this.persistState()
  }

  /** Como ResolveLinkAsync del servicio: sin cadenas, ni el propio evento, ni un ancla que se vincula. */
  private resolveLink(raffle: MockRaffle, target: string | null): string | null {
    if (target === null || target === (raffle.linkedEventId ?? null)) return target
    if (target === raffle.eventId) throw rechazo('RAFFLE_LINK_INVALID')
    if (!this.eventos.some((e) => e.id === target)) throw rechazo('RAFFLE_LINK_INVALID')
    const grupo = this.raffles.find((r) => r.eventId === target)?.linkedEventId ?? target
    if (grupo === raffle.eventId) return null
    if (this.raffles.some((r) => r.linkedEventId === raffle.eventId))
      throw rechazo('RAFFLE_LINK_INVALID')
    return grupo
  }

  /** Las claves de persona con un premio vivo en los sorteos vinculados a `raffle`. */
  private linkedWinnerKeys(raffle: MockRaffle): Set<string> {
    const grupo = raffle.linkedEventId ?? raffle.eventId
    const keys = new Set<string>()
    for (const otro of this.raffles) {
      if (otro === raffle || (otro.linkedEventId ?? otro.eventId) !== grupo) continue
      const porId = new Map(
        (this.frozenAttendees[otro.id] ?? []).map((a) => [mockAttendeeId(a), a.identityKey]),
      )
      for (const d of otro.rounds.flatMap((r) => r.prizes).flatMap((p) => p.draws)) {
        const key = d.void === null ? porId.get(d.winnerAttendeeId) : undefined
        if (key) keys.add(key)
      }
    }
    return keys
  }

  async deleteRaffle(eventId: string): Promise<void> {
    await this.delay()
    this.assertRaffleRole(ROLES.rafflesWrite)
    const raffle = this.findRaffle(eventId)
    if (raffle.state === 'in_progress') throw rechazo('RAFFLE_NOT_DRAFT')
    if (raffle.state === 'completed') throw rechazo('RAFFLE_COMPLETED')
    this.assertRaffleEventEditable(eventId)
    this.raffles = this.raffles.filter((r) => r !== raffle)
    this.persistState()
  }

  private allDoorEntriesOf(eventId: string): MockDoorEntry[] {
    const byEmail = new Map<string, MockDoorEntry>()
    const overrides = this.raffleOverrides[eventId] ?? []
    // Sin la regla de presencia, todo el padrón entra a la tómbola (A35).
    const exigeLlegada = this.raffles.find((r) => r.eventId === eventId)?.requiresCheckIn !== false
    for (const p of this.raffleParticipants[eventId] ?? []) {
      const manual = overrides.find((o) => o.identityKey === p.identityKey)
      if (exigeLlegada && !p.checkedIn && manual?.decision !== 'included') continue
      const [firstName, ...rest] = p.fullName.trim().split(/\s+/)
      byEmail.set(p.identityKey, {
        identityKey: p.identityKey,
        firstName: firstName || p.fullName,
        lastName: rest.join(' '),
        // Como RuleGroup del servicio: la regla de un premio por grupo agrupa por departamento.
        company: p.department?.trim() || p.company?.trim() || null,
        exclusionBeforeOverride: null,
      })
    }
    for (const r of this.portalRegistrations.values()) {
      if (r.eventId !== eventId) continue
      const regId = `reg-${eventId}-${r.reference}`
      const email = r.email.trim().toLowerCase()
      if (!this.checkIns.has(regId) || byEmail.has(email)) continue
      const [firstName, ...rest] = r.name.trim().split(/\s+/)
      byEmail.set(email, {
        identityKey: email,
        firstName: firstName || r.name,
        lastName: rest.join(' '),
        company: r.company,
        exclusionBeforeOverride: null,
      })
    }
    return [...byEmail.values()]
  }

  /** La puerta del evento con sus ajustes a mano aplicados. */
  private doorListOf(eventId: string): RaffleAttendeeResponse[] {
    const overrides = this.raffleOverrides[eventId] ?? []
    return this.allDoorEntriesOf(eventId).map((entry) =>
      classifyDoorEntry(
        entry,
        overrides.find((o) => o.identityKey === entry.identityKey),
      ),
    )
  }

  /** Iniciar, reabrir y los ajustes a mano: el evento tiene que admitir cambios. */
  private findRaffleForLifecycle(eventId: string, role: FunctionRole): MockRaffle {
    this.assertRaffleRole(role)
    const raffle = this.findRaffle(eventId)
    this.assertRaffleEventEditable(eventId)
    return raffle
  }

  async startRaffle(eventId: string): Promise<RaffleResponse> {
    await this.delay()
    const raffle = this.findRaffleForLifecycle(eventId, ROLES.rafflesDraw)
    if (raffle.state !== 'draft') throw rechazo('RAFFLE_NOT_DRAFT')
    const frozen = this.doorListOf(eventId)
    if (!frozen.some((a) => a.qualified)) throw rechazo('RAFFLE_NO_PARTICIPANTS')
    Object.assign(raffle, {
      state: 'in_progress',
      startedAt: this.now(),
      startedById: currentMockIdentity().sub,
    })
    this.frozenAttendees[raffle.id] = frozen
    this.raffleQualifiedCounts[raffle.id] = frozen.filter((a) => a.qualified).length
    this.persistState()
    return this.getRaffle(eventId)
  }

  async reinstateRaffleAttendee(eventId: string, attendeeId: string): Promise<RaffleResponse> {
    await this.delay()
    this.assertRaffleRole(ROLES.rafflesWrite)
    const raffle = this.findRaffle(eventId)
    if (raffle.state !== 'in_progress') throw rechazo('RAFFLE_NOT_IN_PROGRESS')
    const draws = raffle.rounds.flatMap((r) => r.prizes).flatMap((p) => p.draws)
    if (!draws.some((d) => d.void !== null && d.winnerAttendeeId === attendeeId)) {
      throw rechazo('RAFFLE_ATTENDEE_NOT_VOIDED')
    }
    raffle.reinstatedAttendeeIds = [
      ...new Set([...(raffle.reinstatedAttendeeIds ?? []), attendeeId]),
    ]
    this.persistState()
    return this.getRaffle(eventId)
  }

  async reopenRaffle(eventId: string): Promise<RaffleResponse> {
    await this.delay()
    const raffle = this.findRaffleForLifecycle(eventId, ROLES.rafflesDraw)
    if (raffle.state !== 'in_progress') throw rechazo('RAFFLE_NOT_IN_PROGRESS')
    if (raffle.rounds.some((r) => r.prizes.some((p) => p.draws.length > 0))) {
      throw rechazo('RAFFLE_HAS_DRAWS')
    }
    Object.assign(raffle, {
      state: 'draft',
      startedAt: null,
      startedById: null,
      awardsBatchId: null,
    })
    delete this.frozenAttendees[raffle.id]
    delete this.raffleQualifiedCounts[raffle.id]
    this.persistState()
    return this.getRaffle(eventId)
  }

  /** Como el servicio, ignora el estado del evento: un sorteo que arrancó siempre puede cerrarse. */
  async completeRaffle(eventId: string): Promise<RaffleResponse> {
    await this.delay()
    this.assertRaffleRole(ROLES.rafflesDraw)
    const raffle = this.findRaffle(eventId)
    if (raffle.state === 'completed') throw rechazo('RAFFLE_COMPLETED')
    if (raffle.state !== 'in_progress') throw rechazo('RAFFLE_NOT_IN_PROGRESS')
    const authorities = [
      raffle.notaryName,
      raffle.notaryIdentityDocument,
      raffle.witnessOneName,
      raffle.witnessOneIdentityDocument,
      raffle.witnessTwoName,
      raffle.witnessTwoIdentityDocument,
    ]
    if (authorities.some((a) => !a?.trim())) throw rechazo('RAFFLE_AUTHORITIES_REQUIRED')
    if (!raffle.pickupInstructions?.trim()) throw rechazo('RAFFLE_PICKUP_INSTRUCTIONS_REQUIRED')
    const now = this.now()
    const sub = currentMockIdentity().sub
    Object.assign(raffle, { state: 'completed', completedAt: now, completedById: sub })
    this.writeNotarialRecord(raffle, now, sub)
    for (const draw of raffle.rounds.flatMap((r) => r.prizes).flatMap((p) => p.draws)) {
      if (draw.void !== null) continue
      draw.winnerNotificationStatus = 'sent'
      draw.winnerNotificationId = this.newId('notification')
    }
    this.persistState()
    return this.getRaffle(eventId)
  }

  /** Las seis autoridades, recortadas, y una versión más; nunca reenvía un correo. */
  async reissueRaffle(eventId: string, body: ReissueRaffleRequest): Promise<RaffleResponse> {
    await this.delay()
    this.assertRaffleRole(ROLES.rafflesDraw)
    const errors: Record<string, string[]> = {}
    for (const [campo, nombre, tope] of REISSUE_FIELDS) {
      const valor = body[campo]?.trim() ?? ''
      const clave = campo.charAt(0).toUpperCase() + campo.slice(1)
      if (!valor) errors[clave] = [`${nombre} es obligatorio.`]
      else if (valor.length > tope)
        errors[clave] = [`${nombre} no puede superar ${tope} caracteres.`]
    }
    const primero = Object.values(errors)[0]?.[0]
    if (primero) throw new ApiError(primero, 400, errors)
    const raffle = this.findRaffle(eventId)
    if (raffle.state !== 'completed') throw rechazo('RAFFLE_NOT_COMPLETED')
    for (const [campo] of REISSUE_FIELDS) raffle[campo] = body[campo].trim()
    this.writeNotarialRecord(raffle, this.now(), currentMockIdentity().sub)
    this.persistState()
    return this.getRaffle(eventId)
  }

  /**
   * Los firmantes del certificado, recortados y en orden. Vale en cualquier estado del sorteo, pero
   * no con el evento cerrado o cancelado; los rechazos de campo llevan las claves del servicio.
   */
  /** Como el servicio: solo en borrador y con valores conocidos; devuelve el sorteo. */
  async setRaffleDisplayFields(
    eventId: string,
    body: RaffleDisplayFields,
  ): Promise<RaffleResponse> {
    await this.delay()
    const raffle = this.findRaffleForSetup(eventId)
    if (raffle.state !== 'draft') throw rechazo('RAFFLE_NOT_DRAFT')
    const validos =
      ['full_name', 'first_and_last', 'first_name'].includes(body.name) &&
      ['ticket_number', 'employee_code', 'none'].includes(body.identifier) &&
      ['department', 'location', 'company', 'none'].includes(body.detail)
    if (!validos) throw rechazo('RAFFLE_DISPLAY_FIELDS_INVALID')
    raffle.displayFields = { name: body.name, identifier: body.identifier, detail: body.detail }
    this.persistState()
    return this.getRaffle(eventId)
  }

  async setCertificateSigners(eventId: string, body: SetCertificateSignersRequest): Promise<void> {
    await this.delay()
    this.assertRaffleRole(ROLES.rafflesWrite)
    const errors: Record<string, string[]> = {}
    if (body.signers.length > 2) {
      errors.Signers = ['El certificado tiene 2 líneas de firma: a lo sumo 2 firmantes.']
    }
    body.signers.forEach((signer, i) => {
      for (const [campo, nombre, valor] of [
        ['Name', 'El nombre del firmante', signer.name],
        ['Title', 'El cargo del firmante', signer.title],
      ] as const) {
        const recortado = valor?.trim() ?? ''
        if (!recortado) errors[`Signers[${i}].${campo}`] = [`${nombre} es obligatorio.`]
        else if (recortado.length > CERTIFICATE_SIGNER_MAX_LENGTH)
          errors[`Signers[${i}].${campo}`] = [
            `${nombre} no puede superar ${CERTIFICATE_SIGNER_MAX_LENGTH} caracteres.`,
          ]
      }
    })
    const primero = Object.values(errors)[0]?.[0]
    if (primero) throw new ApiError(primero, 400, errors)
    const raffle = this.findRaffle(eventId)
    this.assertRaffleEventEditable(eventId)
    raffle.certificateSigners = body.signers.map((s) => ({
      name: s.name.trim(),
      title: s.title.trim(),
    }))
    this.persistState()
  }

  /** Una versión más del acta; las anteriores quedan. */
  private writeNotarialRecord(raffle: MockRaffle, now: string, sub: string): void {
    raffle.notarialRecords.unshift({
      version: (raffle.notarialRecords[0]?.version ?? 0) + 1,
      storedFileId: this.newId('file'),
      generatedAt: now,
      generatedById: sub,
    })
  }

  async getRaffleAttendees(eventId: string): Promise<RaffleAttendeesResponse> {
    await this.delay()
    const raffle = this.findRaffle(eventId)
    let attendees: RaffleAttendeeResponse[]
    let attendanceFetchedAt: string | null
    if (raffle.state === 'draft') {
      if (Date.now() - Date.parse(this.doorListFetchedAt) > 30_000) {
        this.doorListFetchedAt = this.now()
      }
      attendees = this.doorListOf(eventId)
      attendanceFetchedAt = this.doorListFetchedAt
    } else {
      attendees = this.frozenAttendees[raffle.id] ?? []
      attendanceFetchedAt = raffle.startedAt
    }
    return structuredClone({
      checkedIn: attendees.length,
      qualified: attendees.filter((a) => a.qualified).length,
      attendanceFetchedAt,
      attendanceStatus: 'ok',
      awardsBatchId: raffle.awardsBatchId,
      attendees,
    })
  }

  async setAttendeeOverride(eventId: string, body: SetAttendeeOverrideRequest): Promise<void> {
    await this.delay()
    const clave = body.identityKey.trim().toLowerCase()
    if ((body.note?.length ?? 0) > 1000) {
      throw rechazoDeCampo('Note', 'La nota no puede superar los 1000 caracteres.')
    }
    const raffle = this.findRaffleForLifecycle(eventId, ROLES.rafflesWrite)
    if (raffle.state !== 'draft') throw rechazo('RAFFLE_NOT_DRAFT')
    const inParticipants = (this.raffleParticipants[eventId] ?? []).find(
      (p) => p.identityKey === clave,
    )
    const inDoor = this.allDoorEntriesOf(eventId).find((e) => e.identityKey === clave)
    if (!inParticipants && !inDoor) throw rechazo('PARTICIPANT_NOT_CHECKED_IN')
    const overrides = (this.raffleOverrides[eventId] ?? []).filter((o) => o.identityKey !== clave)
    overrides.push({
      identityKey: clave,
      decision: body.decision,
      note: body.note?.trim() || null,
      decidedById: currentMockIdentity().sub,
      decidedAt: this.now(),
    })
    this.raffleOverrides[eventId] = overrides
    if (inParticipants) {
      inParticipants.override = body.decision
      inParticipants.qualified =
        body.decision === 'included'
          ? true
          : body.decision === 'excluded'
            ? false
            : inParticipants.checkedIn
    }
    this.persistState()
  }

  /**
   * Como `BulkExcludeAsync` del servicio: una sola fuente, solo en borrador, y a cada persona se la
   * reconoce por su correo o, si no, por su ID. Quien no está en el padrón o en la puerta se
   * informa; nada se escribe a medias.
   */
  async bulkExcludeAttendees(
    eventId: string,
    body: BulkExcludeAttendeesRequest,
  ): Promise<BulkExcludeAttendeesResponse> {
    await this.delay()
    if ((body.sourceEventId == null) === (body.identifiers == null))
      throw rechazo('RAFFLE_BULK_EXCLUDE_SOURCE_REQUIRED', 400)
    if (body.identifiers && body.identifiers.length > 5000)
      throw rechazo('RAFFLE_BULK_EXCLUDE_TOO_MANY', 400)
    if (body.sourceEventId === eventId) throw rechazo('RAFFLE_BULK_EXCLUDE_SAME_EVENT', 400)
    const raffle = this.findRaffleForLifecycle(eventId, ROLES.rafflesWrite)
    if (raffle.state !== 'draft') throw rechazo('RAFFLE_NOT_DRAFT')

    type Candidato = { label: string; email: string | null; id: string | null; deArchivo: boolean }
    const candidatos: Candidato[] = []
    const invalid: string[] = []
    const unidentifiable: string[] = []
    let notaPorOmision = 'Importado en lote'
    if (body.sourceEventId) {
      const fuente = this.findRaffle(body.sourceEventId)
      notaPorOmision = `Ganador de «${this.eventos.find((e) => e.id === body.sourceEventId)?.name ?? 'otro sorteo'}»`
      const padronFuente = this.raffleParticipants[body.sourceEventId] ?? []
      const vistos = new Set<string>()
      for (const draw of fuente.rounds.flatMap((r) => r.prizes).flatMap((p) => p.draws)) {
        if (draw.void !== null) continue
        const clave = draw.winnerAttendeeId.replace(/^att-/, '')
        if (vistos.has(clave)) continue
        vistos.add(clave)
        const ficha = padronFuente.find((p) => p.identityKey === clave)
        const email = ficha?.email ?? correoDeClave(clave)
        const id =
          ficha?.employeeCode?.trim().toLowerCase() ||
          (clave.startsWith('id:') ? clave.slice(3) : null)
        if (!email && !id) unidentifiable.push(draw.winnerName)
        else
          candidatos.push({
            label: email ?? ficha?.employeeCode ?? id!,
            email,
            id,
            deArchivo: false,
          })
      }
    } else {
      const vistos = new Set<string>()
      for (const raw of body.identifiers ?? []) {
        const valor = raw.trim()
        if (!valor || vistos.has(valor.toLowerCase())) continue
        vistos.add(valor.toLowerCase())
        if (!valor.includes('@'))
          candidatos.push({ label: valor, email: null, id: valor.toLowerCase(), deArchivo: true })
        else if (/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(valor))
          candidatos.push({ label: valor, email: valor.toLowerCase(), id: null, deArchivo: true })
        else invalid.push(valor)
      }
    }

    const padron = this.raffleParticipants[eventId] ?? []
    const puerta = new Set(this.allDoorEntriesOf(eventId).map((e) => e.identityKey))
    const overrides = this.raffleOverrides[eventId] ?? []
    const notInRoster: string[] = []
    const tratados = new Set<string>()
    const porExcluir: string[] = []
    let alreadyExcluded = 0
    for (const c of candidatos) {
      const persona =
        (c.email ? padron.find((p) => p.email === c.email) : undefined) ??
        (c.id ? padron.find((p) => p.employeeCode?.trim().toLowerCase() === c.id) : undefined)
      if (!persona) {
        if (c.deArchivo && !c.email) invalid.push(c.label)
        else notInRoster.push(c.label)
        continue
      }
      if (tratados.has(persona.identityKey)) continue
      tratados.add(persona.identityKey)
      if (!puerta.has(persona.identityKey)) {
        notInRoster.push(c.label)
        continue
      }
      if (overrides.find((o) => o.identityKey === persona.identityKey)?.decision === 'excluded')
        alreadyExcluded++
      else porExcluir.push(persona.identityKey)
    }

    const nota = body.note?.trim() || notaPorOmision
    const ahora = this.now()
    const sub = currentMockIdentity().sub
    this.raffleOverrides[eventId] = [
      ...overrides.filter((o) => !porExcluir.includes(o.identityKey)),
      ...porExcluir.map((identityKey) => ({
        identityKey,
        decision: 'excluded' as const,
        note: nota,
        decidedById: sub,
        decidedAt: ahora,
      })),
    ]
    for (const p of padron) {
      if (!porExcluir.includes(p.identityKey)) continue
      p.override = 'excluded'
      p.qualified = false
    }
    this.persistState()
    return {
      excluded: porExcluir.length,
      alreadyExcluded,
      notInRoster,
      invalid,
      unidentifiable,
    }
  }

  async removeAttendeeOverride(eventId: string, identityKey: string): Promise<void> {
    await this.delay()
    const raffle = this.findRaffleForLifecycle(eventId, ROLES.rafflesWrite)
    if (raffle.state !== 'draft') throw rechazo('RAFFLE_NOT_DRAFT')
    const normalized = identityKey.trim().toLowerCase()
    this.raffleOverrides[eventId] = (this.raffleOverrides[eventId] ?? []).filter(
      (o) => o.identityKey !== normalized,
    )
    const inParticipants = (this.raffleParticipants[eventId] ?? []).find(
      (p) => p.identityKey === normalized,
    )
    if (inParticipants) {
      inParticipants.override = null
      inParticipants.qualified = inParticipants.checkedIn
    }
    this.persistState()
  }

  async createRaffleRound(eventId: string, body: RaffleRoundRequest): Promise<string> {
    await this.delay()
    const raffle = this.findRaffleForSetup(eventId)
    if (!body.name.trim()) throw rechazoDeCampo('Name', 'El nombre de la ronda es obligatorio.')
    const id = this.newId('round')
    raffle.rounds.push({
      id,
      number: Math.max(0, ...raffle.rounds.map((r) => r.number)) + 1,
      name: body.name.trim(),
      description: body.description,
      prizes: [],
    })
    this.persistState()
    return id
  }

  async updateRaffleRound(
    eventId: string,
    roundId: string,
    body: RaffleRoundRequest,
  ): Promise<void> {
    await this.delay()
    const raffle = this.findRaffleForSetup(eventId)
    const round = this.findRound(raffle, roundId)
    if (round.prizes.some((p) => p.draws.length > 0)) throw rechazo('PRIZE_HAS_DRAWS')
    if (!body.name.trim()) throw rechazoDeCampo('Name', 'El nombre de la ronda es obligatorio.')
    round.name = body.name.trim()
    round.description = body.description
    this.persistState()
  }

  async deleteRaffleRound(eventId: string, roundId: string): Promise<void> {
    await this.delay()
    const raffle = this.findRaffleForSetup(eventId)
    const round = this.findRound(raffle, roundId)
    if (round.prizes.length > 0) throw rechazo('ROUND_NOT_EMPTY')
    raffle.rounds = raffle.rounds
      .filter((r) => r !== round)
      .map((r, i) => ({ ...r, number: i + 1 }))
    this.persistState()
  }

  async createPrizes(eventId: string, body: CreatePrizesRequest): Promise<PrizeResponse[]> {
    await this.delay()
    const raffle = this.findRaffleForSetup(eventId)
    const round = this.findRound(raffle, body.roundId)
    if (!body.name.trim()) throw rechazoDeCampo('Name', 'El nombre del premio es obligatorio.')
    if (body.quantity < 1 || body.quantity > 50) {
      throw rechazoDeCampo('Quantity', 'La cantidad de premios debe estar entre 1 y 50.')
    }
    const createdAt = this.now()
    const prizes: PrizeResponse[] = Array.from({ length: body.quantity }, () => ({
      id: this.newId('prize'),
      roundId: round.id,
      name: body.name.trim(),
      description: body.description,
      value: body.value,
      sponsor: sponsorDeTexto(body.sponsorName),
      createdAt,
      state: 'undrawn',
      draws: [],
    }))
    round.prizes.push(...prizes)
    this.persistState()
    return structuredClone(prizes)
  }

  /**
   * El mock guarda la foto reducida a 640 px como data URL, para que quepa en `localStorage`.
   * El servicio la guarda como archivo, con los mismos tipos y tope (A34).
   */
  async setPrizeImage(
    eventId: string,
    prizeIds: string[],
    file: File,
  ): Promise<{ imageUrl: string }> {
    await this.delay()
    const raffle = this.findRaffle(eventId)
    this.assertRaffleRole(ROLES.rafflesWrite)
    if (raffle.state === 'completed') throw rechazo('RAFFLE_COMPLETED')
    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) {
      throw rechazoDeCampo('File', 'La imagen tiene que ser JPG, PNG o WebP.')
    }
    if (file.size > 5 * 1024 * 1024) throw rechazoDeCampo('File', 'La imagen pasa de 5 MB.')
    const imageUrl = await reducirImagen(file, 640)
    for (const id of prizeIds) this.findPrize(raffle, id).imageUrl = imageUrl
    this.persistState()
    return { imageUrl }
  }

  async removePrizeImage(eventId: string, prizeIds: string[]): Promise<void> {
    await this.delay()
    const raffle = this.findRaffle(eventId)
    this.assertRaffleRole(ROLES.rafflesWrite)
    if (raffle.state === 'completed') throw rechazo('RAFFLE_COMPLETED')
    for (const id of prizeIds) this.findPrize(raffle, id).imageUrl = null
    this.persistState()
  }

  /** Como el servicio: todo o nada, y crea las rondas que falten por número. */
  async importPrizes(eventId: string, body: ImportPrizesRequest): Promise<ImportPrizesResponse> {
    await this.delay()
    const raffle = this.findRaffleForSetup(eventId)
    const total = body.prizes.reduce((n, p) => n + p.quantity, 0)
    if (total < 1 || total > 10_000) {
      throw rechazoDeCampo('Prizes', 'Se importan entre 1 y 10.000 premios por vez.')
    }
    if (body.prizes.some((p) => !p.name.trim() || p.quantity < 1 || p.roundNumber < 1)) {
      throw rechazoDeCampo('Prizes', 'Cada premio necesita nombre, cantidad y ronda.')
    }
    const createdAt = this.now()
    let roundsCreated = 0
    for (const p of body.prizes) {
      for (
        let n = Math.max(0, ...raffle.rounds.map((r) => r.number)) + 1;
        n <= p.roundNumber;
        n++
      ) {
        raffle.rounds.push({
          id: this.newId('round'),
          number: n,
          name: `Ronda ${n}`,
          description: null,
          prizes: [],
        })
        roundsCreated++
      }
      // Un número menor que ya no existe (se borró la ronda) va a la primera.
      const round = raffle.rounds.find((r) => r.number === p.roundNumber) ?? raffle.rounds[0]
      for (let i = 0; i < p.quantity; i++) {
        round.prizes.push({
          id: this.newId('prize'),
          roundId: round.id,
          name: p.name.trim(),
          description: null,
          value: p.value,
          sponsor: sponsorDeTexto(p.sponsorName),
          createdAt,
          state: 'undrawn',
          draws: [],
        })
      }
    }
    this.persistState()
    return { created: total, roundsCreated }
  }

  async updatePrize(eventId: string, prizeId: string, body: UpdatePrizeRequest): Promise<void> {
    await this.delay()
    const raffle = this.findRaffleForSetup(eventId)
    const prize = this.findPrize(raffle, prizeId)
    if (prize.draws.length > 0) throw rechazo('PRIZE_HAS_DRAWS')
    const target = this.findRound(raffle, body.roundId)
    if (!body.name.trim()) throw rechazoDeCampo('Name', 'El nombre del premio es obligatorio.')
    Object.assign(prize, {
      name: body.name.trim(),
      description: body.description,
      value: body.value,
      sponsor: sponsorDeTexto(body.sponsorName),
    })
    if (prize.roundId !== target.id) {
      const source = this.findRound(raffle, prize.roundId)
      source.prizes = source.prizes.filter((p) => p !== prize)
      prize.roundId = target.id
      target.prizes.push(prize)
    }
    this.persistState()
  }

  async deletePrize(eventId: string, prizeId: string): Promise<void> {
    await this.delay()
    const raffle = this.findRaffleForSetup(eventId)
    const prize = this.findPrize(raffle, prizeId)
    if (prize.draws.length > 0) throw rechazo('PRIZE_HAS_DRAWS')
    const round = this.findRound(raffle, prize.roundId)
    round.prizes = round.prizes.filter((p) => p !== prize)
    this.persistState()
  }

  /**
   * El bombo del premio con las cuatro reglas de spec raffles/02 §1, en orden, contando cada
   * exclusión en la primera que aplica: un anulado en cualquier premio, con cualquier motivo, un
   * ganador vivo con la regla por persona, la empresa de un ganador vivo con la regla por empresa.
   */
  private eligiblePool(raffle: MockRaffle): {
    members: RaffleAttendeeResponse[]
    counts: PoolExhaustedCounts
  } {
    const qualified = (this.frozenAttendees[raffle.id] ?? []).filter((a) => a.qualified)
    const draws = raffle.rounds.flatMap((r) => r.prizes).flatMap((p) => p.draws)
    const live = draws.filter((d) => d.void === null)
    // Un anulado queda fuera de los premios siguientes, salvo que un administrador lo reactive.
    const reinstated = new Set(raffle.reinstatedAttendeeIds ?? [])
    const voided = new Set(
      draws
        .filter((d) => d.void !== null && !reinstated.has(d.winnerAttendeeId))
        .map((d) => d.winnerAttendeeId),
    )
    const winners = new Set(live.map((d) => d.winnerAttendeeId))
    const winningCompanies = new Set(live.map((d) => companyKey(d.winnerCompany)))
    const linkedWinners = this.linkedWinnerKeys(raffle)
    const counts: PoolExhaustedCounts = {
      participants: qualified.length,
      excludedByVoid: 0,
      excludedByPerson: 0,
      excludedByCompany: 0,
      excludedByLinkedRaffle: 0,
    }
    const members = qualified.filter((attendee) => {
      const id = mockAttendeeId(attendee)
      const company = companyKey(attendee.company)
      if (voided.has(id)) counts.excludedByVoid++
      // Quien ganó nunca vuelve al bombo, como EligiblePool en el servicio.
      else if (winners.has(id)) counts.excludedByPerson++
      else if (linkedWinners.has(attendee.identityKey)) counts.excludedByLinkedRaffle! += 1
      else if (raffle.onePrizePerCompany && company !== null && winningCompanies.has(company)) {
        counts.excludedByCompany++
      } else return true
      return false
    })
    members.sort((a, b) => mockAttendeeId(a).localeCompare(mockAttendeeId(b)))
    return { members, counts }
  }

  /** Sortear y anular: solo en curso, y un premio con ganador vivo no se vuelve a sortear. */
  private findPrizeForDrawing(raffle: MockRaffle, prizeId: string): PrizeResponse {
    if (raffle.state === 'draft') throw rechazo('RAFFLE_NOT_IN_PROGRESS')
    if (raffle.state === 'completed') throw rechazo('RAFFLE_COMPLETED')
    return this.findPrize(raffle, prizeId)
  }

  /** Lleva el sorteo vivo, como el servicio: un reintento recupera al ganador sin otra llamada. */
  private assertPrizeUndrawn(prize: PrizeResponse): void {
    const live = prize.draws.find((d) => d.void === null)
    if (!live) return
    throw new ApiError(
      apiErrorCodeLabel.PRIZE_ALREADY_DRAWN,
      409,
      undefined,
      'PRIZE_ALREADY_DRAWN',
      {
        draw: structuredClone(live),
      },
    )
  }

  private prizeResponse(prize: PrizeResponse): PrizeResponse {
    return { ...structuredClone(prize), state: this.prizeStateOf(prize.draws) }
  }

  async getPrizeEligible(
    eventId: string,
    prizeId: string,
    limit?: number,
  ): Promise<EligibleResponse> {
    await this.delay()
    const raffle = this.findRaffle(eventId)
    // Un completado también es `RAFFLE_NOT_IN_PROGRESS`: ya no hay sorteo que alimentar
    if (raffle.state !== 'in_progress') throw rechazo('RAFFLE_NOT_IN_PROGRESS')
    const prize = this.findPrize(raffle, prizeId)
    this.assertPrizeUndrawn(prize)
    const { members } = this.eligiblePool(raffle)
    // Como el servicio (R14): con `limit`, una muestra al azar de a lo sumo 100; el total no cambia.
    const tope = limit && limit > 0 ? Math.min(limit, 100) : null
    let muestra = members
    if (tope !== null && members.length > tope) {
      const pool = [...members]
      for (let i = 0; i < tope; i++) {
        const j = i + Math.floor(Math.random() * (pool.length - i))
        ;[pool[i], pool[j]] = [pool[j], pool[i]]
      }
      muestra = pool.slice(0, tope)
    }
    return {
      count: members.length,
      participants: muestra.map((a) => ({
        attendeeId: mockAttendeeId(a),
        displayName: `${a.firstName} ${a.lastName}`,
        company: a.company,
      })),
    }
  }

  /** Como el servicio: uno tras otro; si el bombo se agota, el 409 trae los ya sorteados. */
  async drawPrizeBatch(eventId: string, prizeIds: string[]): Promise<PrizeResponse[]> {
    if (prizeIds.length < 1 || prizeIds.length > 10 || new Set(prizeIds).size !== prizeIds.length) {
      throw rechazoDeCampo('PrizeIds', 'Se sortean entre 1 y 10 premios distintos por lote.')
    }
    const drawn: PrizeResponse[] = []
    for (const id of prizeIds) {
      try {
        drawn.push(await this.drawPrize(eventId, id))
      } catch (err) {
        if (err instanceof ApiError && err.code === 'RAFFLE_POOL_EXHAUSTED' && drawn.length > 0) {
          throw new ApiError(err.message, 409, undefined, 'RAFFLE_POOL_EXHAUSTED', {
            ...err.extensions,
            drawn,
          })
        }
        throw err
      }
    }
    return drawn
  }

  async drawPrize(eventId: string, prizeId: string): Promise<PrizeResponse> {
    await this.delay()
    this.assertRaffleRole(ROLES.rafflesDraw)
    const raffle = this.findRaffle(eventId)
    const prize = this.findPrizeForDrawing(raffle, prizeId)
    this.assertPrizeUndrawn(prize)
    const { members, counts } = this.eligiblePool(raffle)
    if (members.length === 0) {
      throw new ApiError(
        apiErrorCodeLabel.RAFFLE_POOL_EXHAUSTED,
        409,
        undefined,
        'RAFFLE_POOL_EXHAUSTED',
        { ...counts },
      )
    }
    const winner = members[Math.floor(Math.random() * members.length)]
    const ficha = (this.raffleParticipants[eventId] ?? []).find(
      (p) => p.identityKey === winner.identityKey,
    )
    prize.draws.unshift({
      id: this.newId('draw'),
      winnerAttendeeId: mockAttendeeId(winner),
      winnerName: `${winner.firstName} ${winner.lastName}`,
      winnerCompany: winner.company,
      winnerTicketNumber: ficha?.ticketNumber ?? null,
      winnerEmployeeCode: ficha?.employeeCode ?? null,
      winnerDepartment: ficha?.department ?? null,
      winnerLocation: ficha?.location ?? null,
      winnerFirstName: ficha?.firstName ?? null,
      winnerLastName: ficha?.lastName ?? null,
      drawnAt: this.now(),
      drawnByName: currentMockIdentity().nombre,
      eligiblePoolSize: members.length,
      onePrizePerPerson: true,
      onePrizePerCompany: raffle.onePrizePerCompany,
      redemptionCode: mockRedemptionCode(),
      status: 'drawn',
      void: null,
      handovers: [],
      // El correo sale al completar, no al sortear (spec raffles/03 §5.1)
      winnerNotificationStatus: null,
      winnerNotificationId: null,
    })
    this.persistState()
    return this.prizeResponse(prize)
  }

  async voidPrizeDraw(
    eventId: string,
    prizeId: string,
    body: VoidPrizeDrawRequest,
  ): Promise<PrizeResponse> {
    await this.delay()
    this.assertRaffleRole(ROLES.rafflesDraw)
    const note = body.note?.trim() || null
    if (!VOID_CODES.includes(body.code)) {
      throw rechazoDeCampo('Code', 'El motivo debe ser absent, declined, ineligible u other.')
    }
    if (body.code === 'other' && note === null) {
      throw rechazoDeCampo('Note', 'La nota es obligatoria con el motivo other.')
    }
    if (note !== null && note.length > 500) {
      throw rechazoDeCampo('Note', 'La nota no puede superar los 500 caracteres.')
    }
    const raffle = this.findRaffle(eventId)
    const prize = this.findPrizeForDrawing(raffle, prizeId)
    const live = prize.draws.find((d) => d.void === null)
    if (!live) throw rechazo('PRIZE_NOT_DRAWN')
    if (live.handovers.some((h) => h.reversedAt === null)) throw rechazo('PRIZE_ALREADY_REDEEMED')
    live.void = {
      code: body.code,
      note,
      voidedAt: this.now(),
      voidedByName: currentMockIdentity().nombre,
    }
    live.status = 'voided'
    // Anularlo otra vez lo saca del bombo aunque lo hubieran reactivado.
    raffle.reinstatedAttendeeIds = (raffle.reinstatedAttendeeIds ?? []).filter(
      (id) => id !== live.winnerAttendeeId,
    )
    this.persistState()
    return this.prizeResponse(prize)
  }

  /**
   * Como el servicio: en curso o completado, sin mirar el evento. El mock no inspecciona la firma
   * (tipo, tamaño): el lienzo siempre da un PNG chico.
   */
  async handOverPrize(
    eventId: string,
    prizeId: string,
    body: HandOverPrizeRequest,
  ): Promise<PrizeResponse> {
    await this.delay()
    this.assertRaffleRole(ROLES.rafflesDraw)
    const errors: Record<string, string[]> = {}
    for (const [campo, falta, largo, tope] of HANDOVER_FIELDS) {
      const valor = String(body[campo]).trim()
      const clave = campo.charAt(0).toUpperCase() + campo.slice(1)
      if (!valor) errors[clave] = [falta]
      else if (valor.length > tope) errors[clave] = [largo]
    }
    if (body.signature.size === 0) {
      errors.Signature = ['La firma es obligatoria y no puede estar vacía.']
    }
    const primero = Object.values(errors)[0]?.[0]
    if (primero) throw new ApiError(primero, 400, errors)
    const raffle = this.findRaffle(eventId)
    const prize = this.findPrize(raffle, prizeId)
    if (raffle.state === 'draft') throw rechazo('RAFFLE_NOT_IN_PROGRESS')
    const live = prize.draws.find((d) => d.void === null)
    if (!live) throw rechazo('PRIZE_NOT_DRAWN')
    if (live.handovers.some((h) => h.reversedAt === null)) throw rechazo('PRIZE_ALREADY_REDEEMED')
    live.handovers.unshift({
      id: this.newId('handover'),
      receiverName: body.receiverName.trim(),
      receiverIdentityDocument: body.receiverIdentityDocument.trim(),
      receiverIsWinner: body.receiverIsWinner,
      signatureStoredFileId: this.newId('file'),
      redeemedAt: this.now(),
      redeemedById: currentMockIdentity().sub,
      redeemedByName: currentMockIdentity().nombre,
      reversedAt: null,
      reversedById: null,
      reversalReason: null,
    })
    live.status = 'redeemed'
    this.persistState()
    return this.prizeResponse(prize)
  }

  /** La entrega queda, con quién, cuándo y por qué se revirtió; el premio vuelve a `drawn`. */
  async reversePrizeHandover(
    eventId: string,
    prizeId: string,
    body: ReversePrizeHandoverRequest,
  ): Promise<PrizeResponse> {
    await this.delay()
    this.assertRaffleRole(ROLES.rafflesDraw)
    const reason = body.reason?.trim() ?? ''
    if (!reason) throw rechazoDeCampo('Reason', 'El motivo de la reversión es obligatorio.')
    if (reason.length > 500) {
      throw rechazoDeCampo('Reason', 'El motivo de la reversión no puede superar 500 caracteres.')
    }
    const raffle = this.findRaffle(eventId)
    const prize = this.findPrize(raffle, prizeId)
    if (raffle.state === 'draft') throw rechazo('RAFFLE_NOT_IN_PROGRESS')
    const live = prize.draws.find((d) => d.void === null)
    const handover = live?.handovers.find((h) => h.reversedAt === null)
    if (!live || !handover) throw rechazo('PRIZE_NOT_REDEEMED')
    Object.assign(handover, {
      reversedAt: this.now(),
      reversedById: currentMockIdentity().sub,
      reversalReason: reason,
    })
    live.status = 'drawn'
    this.persistState()
    return this.prizeResponse(prize)
  }

  async findPrizeDrawByCode(code: string): Promise<PrizeDrawLookupResponse> {
    await this.delay()
    const key = normalizeRedemptionCode(code)
    for (const raffle of this.raffles) {
      for (const prize of raffle.rounds.flatMap((r) => r.prizes)) {
        const draw = prize.draws.find(
          (d) => key !== null && normalizeRedemptionCode(d.redemptionCode) === key,
        )
        if (!draw) continue
        const handover = draw.void ? undefined : draw.handovers.find((h) => h.reversedAt === null)
        return {
          drawId: draw.id,
          eventId: raffle.eventId,
          raffleId: raffle.id,
          prizeId: prize.id,
          prizeName: prize.name,
          winnerName: draw.winnerName,
          winnerCompany: draw.winnerCompany,
          status: draw.status,
          void: draw.void ? { code: draw.void.code, voidedAt: draw.void.voidedAt } : null,
          handover: handover
            ? { receiverName: handover.receiverName, redeemedAt: handover.redeemedAt }
            : null,
        }
      }
    }
    throw rechazo('REDEMPTION_CODE_NOT_FOUND', 404)
  }

  async listRaffles(params: PagingParams): Promise<RafflePage<RaffleListItem>> {
    await this.delay()
    const items = this.raffles
      .map((raffle): RaffleListItem => {
        const evento = this.findEvento(raffle.eventId)
        const states = raffle.rounds.flatMap((r) => r.prizes).map((p) => this.prizeStateOf(p.draws))
        return {
          raffleId: raffle.id,
          eventId: raffle.eventId,
          eventName: evento.name,
          eventStartDate: evento.startDate,
          state: raffle.state,
          prizeCount: states.length,
          drawnCount: states.filter((s) => s !== 'undrawn').length,
          redeemedCount: states.filter((s) => s === 'redeemed').length,
          startedAt: raffle.startedAt,
          completedAt: raffle.completedAt,
          linkedEventId: raffle.linkedEventId ?? null,
        }
      })
      .sort((a, b) => b.eventStartDate.localeCompare(a.eventStartDate))
    return this.page(items, params)
  }

  async getRaffleTotals(): Promise<RaffleTotals> {
    await this.delay()
    const states = this.raffles.flatMap((raffle) =>
      raffle.rounds.flatMap((r) => r.prizes).map((p) => this.prizeStateOf(p.draws)),
    )
    return {
      raffles: this.raffles.length,
      active: this.raffles.filter((r) => r.state === 'in_progress').length,
      prizes: states.length,
      winners: states.filter((s) => s !== 'undrawn').length,
      redeemed: states.filter((s) => s === 'redeemed').length,
      qualifiedParticipants: this.raffles.reduce(
        (sum, r) => sum + (this.raffleQualifiedCounts[r.id] ?? 0),
        0,
      ),
    }
  }

  async listRaffleParticipants(eventId: string): Promise<RaffleParticipantItem[]> {
    await this.delay()
    this.findRaffle(eventId)
    const overrides = this.raffleOverrides[eventId] ?? []
    // Los participantes de antes del número de boleto reciben uno la primera vez que se listan.
    const padron = this.raffleParticipants[eventId] ?? []
    if (padron.some((x) => !x.ticketNumber)) {
      const usados = new Set(padron.map((x) => x.ticketNumber ?? ''))
      for (const x of padron) {
        if (x.ticketNumber) continue
        x.ticketNumber = nuevoBoleto(usados)
        usados.add(x.ticketNumber)
      }
      this.persistState()
    }
    const base = [...padron]
    const seenEmails = new Set(base.map((p) => p.identityKey))

    // Incorpora asistentes con check-in del portal de convocatorias que no estén ya en el padrón.
    for (const r of this.portalRegistrations.values()) {
      if (r.eventId !== eventId) continue
      const email = r.email.trim().toLowerCase()
      if (seenEmails.has(email)) continue
      const regId = `reg-${eventId}-${r.reference}`
      const checkInInfo = this.checkIns.get(regId)
      const override = overrides.find((o) => o.identityKey === email)?.decision ?? null
      const checkedIn = Boolean(checkInInfo)
      seenEmails.add(email)
      base.push({
        id: regId,
        fullName: r.name,
        email,
        identityKey: email,
        company: r.company,
        department: r.company,
        employeeCode: r.reference,
        phone: null,
        qrCode: r.qrCode,
        checkedIn,
        checkedInAt: checkInInfo?.checkedInAt ?? null,
        source: 'convocatoria',
        qualified: override === 'included' ? true : override === 'excluded' ? false : checkedIn,
        override,
      })
    }

    const decisionDe = new Map(overrides.map((o) => [o.identityKey, o.decision]))
    const raffle = this.findRaffle(eventId)
    const exigeLlegada = raffle.requiresCheckIn !== false
    const ganaronEnVinculados = this.linkedWinnerKeys(raffle)
    return structuredClone(
      base.map((p) => {
        const override = decisionDe.get(p.identityKey) ?? null
        // Como el servicio: el excluido a mano manda; después, quien ganó en un sorteo vinculado.
        const vinculado = override !== 'excluded' && ganaronEnVinculados.has(p.identityKey)
        return {
          ...p,
          override,
          exclusionReason: vinculado ? 'won_in_linked_raffle' : null,
          qualified: vinculado
            ? false
            : override === 'included'
              ? true
              : override === 'excluded'
                ? false
                : p.checkedIn || !exigeLlegada,
        }
      }),
    )
  }

  async importRaffleParticipants(
    eventId: string,
    body: ImportRaffleParticipantsRequest,
  ): Promise<ImportRaffleParticipantsResponse> {
    await this.delay()
    this.assertRaffleRole(ROLES.rafflesWrite)
    const raffle = this.findRaffle(eventId)
    if (raffle.state === 'completed') throw rechazo('RAFFLE_COMPLETED')

    const source = body.source ?? 'excel'
    const current = body.replaceExisting ? [] : [...(this.raffleParticipants[eventId] ?? [])]
    const overrides = this.raffleOverrides[eventId] ?? []
    let imported = 0
    let updated = 0
    // Índices en vez de buscar en la lista por cada fila: con 25.000 filas eso son 600 millones
    // de comparaciones.
    const porEmail = new Map(current.filter((p) => p.email).map((p) => [p.email!.toLowerCase(), p]))
    const porCodigo = new Map(
      current.filter((p) => p.employeeCode).map((p) => [p.employeeCode!.toLowerCase(), p]),
    )
    const porQr = new Map(current.map((p) => [p.qrCode.toLowerCase(), p]))
    const boletos = new Set(current.map((x) => x.ticketNumber ?? ''))
    const decisionDe = new Map(overrides.map((o) => [o.identityKey, o.decision]))
    const rejected: RaffleParticipantImportRejection[] = []
    let fila = 0

    for (const raw of body.participants) {
      fila++
      const fullName = raw.fullName?.trim()
      if (!fullName) continue
      const cleanCode = raw.employeeCode?.trim() || null
      const correoDado = raw.email?.trim().toLowerCase() || null
      // Como events-service: sin correo ni ID no hay cómo reconocer a la persona.
      if (!correoDado && !cleanCode) {
        rejected.push({ row: fila, reason: 'missing_email_and_id' })
        continue
      }
      // Manda el código; el correo solo une si no contradice el código.
      const duenoCorreo = correoDado ? porEmail.get(correoDado) : undefined
      const correoDeOtro =
        !!duenoCorreo &&
        !!cleanCode &&
        !!duenoCorreo.employeeCode &&
        duenoCorreo.employeeCode.toLowerCase() !== cleanCode.toLowerCase()
      const existing =
        (cleanCode ? porCodigo.get(cleanCode.toLowerCase()) : undefined) ??
        (correoDeOtro ? undefined : duenoCorreo) ??
        (raw.qrCode ? porQr.get(raw.qrCode.trim().toLowerCase()) : undefined)
      const libre = (c: string) => !porEmail.has(c) || porEmail.get(c) === existing
      // Un correo compartido no tumba la carga: el ID manda y el segundo entra sin ese correo.
      const email = correoDado && libre(correoDado) ? correoDado : (existing?.email ?? null)
      const identityKey =
        claveDePersona(email, cleanCode ?? existing?.employeeCode) ?? existing?.identityKey ?? ''
      const checkedIn = body.markCheckedIn ?? raw.checkedIn ?? true
      const now = checkedIn ? this.now() : null
      if (existing) {
        existing.fullName = fullName
        existing.email = email
        existing.identityKey = identityKey
        if (raw.company !== undefined) existing.company = raw.company?.trim() || null
        if (raw.department !== undefined) existing.department = raw.department?.trim() || null
        if (raw.location !== undefined) existing.location = raw.location?.trim() || null
        if (raw.firstName !== undefined) existing.firstName = raw.firstName?.trim() || null
        if (raw.lastName !== undefined) existing.lastName = raw.lastName?.trim() || null
        if (cleanCode) existing.employeeCode = cleanCode
        if (raw.phone !== undefined) existing.phone = raw.phone?.trim() || null
        if (raw.qrCode?.trim()) existing.qrCode = raw.qrCode.trim()
        if (body.markCheckedIn !== undefined || raw.checkedIn !== undefined) {
          existing.checkedIn = checkedIn
          existing.checkedInAt = checkedIn ? (existing.checkedInAt ?? now) : null
        }
        existing.source = source
        if (email) porEmail.set(email, existing)
        if (cleanCode) porCodigo.set(cleanCode.toLowerCase(), existing)
        porQr.set(existing.qrCode.toLowerCase(), existing)
        const ov = decisionDe.get(identityKey) ?? null
        existing.override = ov
        existing.qualified =
          ov === 'included' ? true : ov === 'excluded' ? false : existing.checkedIn
        const regId = `reg-${eventId}-${existing.id}`
        if (existing.checkedIn) {
          if (!this.checkIns.has(regId)) {
            this.checkIns.set(regId, {
              checkedInAt: existing.checkedInAt ?? this.now(),
              checkedInById: currentMockIdentity().sub,
            })
          }
        } else {
          this.checkIns.delete(regId)
        }
        updated++
      } else {
        const id = this.newId('part')
        const qrCode =
          raw.qrCode?.trim() ||
          `QR-${eventId
            .replace(/[^a-zA-Z0-9]/g, '')
            .slice(-4)
            .toUpperCase()}-${this.sequence}`
        const ov = decisionDe.get(identityKey) ?? null
        const ticketNumber = nuevoBoleto(boletos)
        boletos.add(ticketNumber)
        const item: RaffleParticipantItem = {
          id,
          ticketNumber,
          fullName,
          email,
          identityKey,
          company: raw.company?.trim() || raw.department?.trim() || null,
          department: raw.department?.trim() || raw.company?.trim() || null,
          location: raw.location?.trim() || null,
          firstName: raw.firstName?.trim() || null,
          lastName: raw.lastName?.trim() || null,
          employeeCode: cleanCode,
          phone: raw.phone?.trim() || null,
          qrCode,
          checkedIn,
          checkedInAt: now,
          source,
          qualified: ov === 'included' ? true : ov === 'excluded' ? false : checkedIn,
          override: ov,
        }
        current.push(item)
        if (email) porEmail.set(email, item)
        if (cleanCode) porCodigo.set(cleanCode.toLowerCase(), item)
        porQr.set(qrCode.toLowerCase(), item)
        if (checkedIn) {
          this.checkIns.set(`reg-${eventId}-${id}`, {
            checkedInAt: now ?? this.now(),
            checkedInById: currentMockIdentity().sub,
          })
        }
        imported++
      }
    }

    this.raffleParticipants[eventId] = current
    this.persistState()
    const list = await this.listRaffleParticipants(eventId)
    return { imported, updated, total: list.length, participants: list, rejected }
  }

  async setRaffleParticipantCheckIn(
    eventId: string,
    registrationIdOrCode: string,
    checkedIn: boolean,
  ): Promise<RaffleParticipantItem> {
    await this.delay()
    this.findRaffle(eventId)
    const norm = registrationIdOrCode.trim().toLowerCase()
    const list = this.raffleParticipants[eventId] ?? []
    const item = list.find(
      (p) =>
        p.id.toLowerCase() === norm ||
        p.qrCode.toLowerCase() === norm ||
        p.email?.toLowerCase() === norm ||
        (p.employeeCode && p.employeeCode.toLowerCase() === norm),
    )
    if (!item) throw rechazo('REGISTRATION_NOT_FOUND', 404)
    item.checkedIn = checkedIn
    item.checkedInAt = checkedIn ? (item.checkedInAt ?? this.now()) : null
    const regId = `reg-${eventId}-${item.id}`
    if (checkedIn) {
      this.checkIns.set(regId, {
        checkedInAt: item.checkedInAt ?? this.now(),
        checkedInById: currentMockIdentity().sub,
      })
    } else {
      this.checkIns.delete(regId)
    }
    const ov =
      (this.raffleOverrides[eventId] ?? []).find((o) => o.identityKey === item.identityKey)
        ?.decision ?? null
    item.override = ov
    item.qualified = ov === 'included' ? true : ov === 'excluded' ? false : checkedIn
    this.persistState()
    return structuredClone(item)
  }

  async bulkRaffleCheckIn(eventId: string, checkedIn: boolean): Promise<void> {
    await this.delay()
    this.findRaffle(eventId)
    const list = this.raffleParticipants[eventId] ?? []
    const now = this.now()
    const sub = currentMockIdentity().sub
    const overrides = this.raffleOverrides[eventId] ?? []
    for (const p of list) {
      p.checkedIn = checkedIn
      p.checkedInAt = checkedIn ? (p.checkedInAt ?? now) : null
      const regId = `reg-${eventId}-${p.id}`
      if (checkedIn) {
        this.checkIns.set(regId, { checkedInAt: p.checkedInAt ?? now, checkedInById: sub })
      } else {
        this.checkIns.delete(regId)
      }
      const ov = overrides.find((o) => o.identityKey === p.identityKey)?.decision ?? null
      p.qualified = ov === 'included' ? true : ov === 'excluded' ? false : checkedIn
    }
    this.persistState()
  }

  async removeRaffleParticipant(eventId: string, emailOrCode: string): Promise<void> {
    await this.delay()
    this.assertRaffleRole(ROLES.rafflesWrite)
    const raffle = this.findRaffle(eventId)
    if (raffle.state !== 'draft') throw rechazo('RAFFLE_NOT_DRAFT')
    // Como el servicio: por id, correo o ID de empleado.
    const norm = emailOrCode.trim().toLowerCase()
    const existing = this.raffleParticipants[eventId] ?? []
    const target = existing.find(
      (p) =>
        p.id.toLowerCase() === norm ||
        p.email === norm ||
        p.employeeCode?.trim().toLowerCase() === norm,
    )
    if (target) {
      this.checkIns.delete(`reg-${eventId}-${target.id}`)
    }
    this.raffleParticipants[eventId] = existing.filter((p) => p !== target)
    this.persistState()
  }
}

/** La imagen achicada a `lado` px en su lado mayor. PNG y WebP conservan la transparencia. */
async function reducirImagen(file: File, lado: number): Promise<string> {
  const bitmap = await createImageBitmap(file)
  const escala = Math.min(1, lado / Math.max(bitmap.width, bitmap.height))
  const canvas = document.createElement('canvas')
  canvas.width = Math.round(bitmap.width * escala)
  canvas.height = Math.round(bitmap.height * escala)
  canvas.getContext('2d')?.drawImage(bitmap, 0, 0, canvas.width, canvas.height)
  bitmap.close()
  return file.type === 'image/jpeg'
    ? canvas.toDataURL('image/jpeg', 0.82)
    : canvas.toDataURL(file.type === 'image/webp' ? 'image/webp' : 'image/png')
}
