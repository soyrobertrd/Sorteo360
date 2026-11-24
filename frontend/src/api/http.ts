import { config } from '@/lib/config'
import { apiErrorCodeLabelFor, FORBIDDEN_MESSAGE } from '@/lib/labels'
import { ApiError, type ApiClient } from './client'
import type {
  ContactListDetailResponse,
  ContactListResponse,
  CampaignResponse,
  CancellationNoticeResponse,
  CiudadRequest,
  CiudadResponse,
  ContactClassificationResponse,
  ContactResponse,
  CreateCampaignRequest,
  CreateEventoRequest,
  CrmPagedResponse,
  EventTrackingResponse,
  RegistrationWindowResponse,
  EventoDetail,
  EventoListItem,
  EventoImage,
  LaunchCampaignRequest,
  ListCampaignsParams,
  ListParticipantsParams,
  ParticipantResponse,
  CheckInResponse,
  ContactCheckInResponse,
  DoorEventResponse,
  DoorListResponse,
  ListDoorRegistrationsParams,
  ContactCampaignResponse,
  ParticipantsCsvFile,
  ListContactsParams,
  ListEventosParams,
  ListNotificationsParams,
  NotificationBatch,
  NotificationHistoryItem,
  CreatePrizesRequest,
  EligibleResponse,
  PagingParams,
  HandOverPrizeRequest,
  PrizeDrawLookupResponse,
  PrizeResponse,
  RaffleListItem,
  RaffleResponse,
  RaffleRoundRequest,
  RaffleAttendeesResponse,
  RaffleTotals,
  ReissueRaffleRequest,
  ReversePrizeHandoverRequest,
  SetAttendeeOverrideRequest,
  SetCertificateSignersRequest,
  RaffleDisplayFields,
  UpdatePrizeRequest,
  UpdateRaffleRequest,
  VoidPrizeDrawRequest,
  RaffleParticipantItem,
  ImportRaffleParticipantsRequest,
  ImportRaffleParticipantsResponse,
  PagedResponse,
  PublicEvento,
  PublicEventoDetail,
  PublicRegisterRequest,
  PublicRegistrationResponse,
  RegistrationTicketResponse,
  ResolvedInvitationResponse,
  UnsubscribeResponse,
  RecipientSelectionResponse,
  RecipientUploadResponse,
  SelectRecipientsRequest,
  TemplateListItem,
  UpdateCampaignRequest,
  UpdateEventoRequest,
} from './types'
import type { CampaignExclusionRow } from './types'
import type {
  ActivateAccountRequest,
  ActivateAccountResponse,
  SignupRequest,
  SignupResponse,
  ImportPrizesRequest,
  ImportPrizesResponse,
} from './types'
import type { FeatureSwitchResponse, SetFeatureSwitchRequest } from './types'

interface ProblemDetails {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
  /** Extensión `code`: el código máquina del fallo. Ausente en las validaciones de campo. */
  code?: string
  /** El resto de las extensiones: lo que cada rechazo adjunta además de su código. */
  [extension: string]: unknown
}

/** Los miembros de RFC 7807 y los que `ApiError` ya lleva aparte; lo demás es extensión. */
const MIEMBROS_PROPIOS = new Set([
  'type',
  'title',
  'status',
  'detail',
  'instance',
  'errors',
  'code',
])

function extensionsOf(problem: ProblemDetails): Record<string, unknown> {
  return Object.fromEntries(
    Object.entries(problem).filter(([member]) => !MIEMBROS_PROPIOS.has(member)),
  )
}

// Inyectados por AuthProvider en modo http: token portador y reacción ante 401
// (sesión vencida → volver al login de Keycloak).
type TokenProvider = () => Promise<string | null>
let tokenProvider: TokenProvider | null = null
let unauthorizedHandler: (() => void) | null = null

export function setTokenProvider(provider: TokenProvider): void {
  tokenProvider = provider
}

export function setUnauthorizedHandler(handler: () => void): void {
  unauthorizedHandler = handler
}

/**
 * La petición no llegó a tener respuesta: el servicio está apagado, el proxy no lo
 * alcanza o la red se cortó. No hay estado HTTP que reportar, así que se usa el cero
 * para que quien atrape el error lo distinga de cualquier respuesta real.
 */
export const NETWORK_ERROR_STATUS = 0

/** Último recurso: la respuesta no traía cuerpo, así que solo queda el estado. */
function statusFallback(status: number): string {
  if (status === 404) return 'Recurso no encontrado.'
  if (status === 403) return FORBIDDEN_MESSAGE
  // 502 y 504 los pone el proxy cuando el servicio detrás no está en pie: para quien
  // mira la pantalla es lo mismo que el 503 que el propio servicio devuelve.
  if (status === 502 || status === 503 || status === 504) {
    return 'El servicio no está disponible. Intente nuevamente en unos minutos.'
  }
  return `Error ${status}`
}

/**
 * La petición con la sesión puesta. `headers` reemplaza los del `init`, así que el token no se
 * pierde. Ni la red caída ni la sesión vencida llegan como respuesta: salen como `ApiError`.
 */
async function fetchWithSession(
  path: string,
  init: RequestInit | undefined,
  headers: Record<string, string>,
): Promise<Response> {
  if (tokenProvider) {
    const token = await tokenProvider()
    if (token) headers.Authorization = `Bearer ${token}`
  }

  let response: Response
  try {
    response = await fetch(`${config.eventosApiUrl}${path}`, {
      ...init,
      headers,
    })
  } catch {
    // `fetch` solo rechaza cuando no hubo respuesta. Se traduce acá para que ninguna
    // pantalla termine mostrando el "Failed to fetch" del navegador.
    throw new ApiError(
      'No se pudo contactar el servicio. Verifique su conexión e intente nuevamente.',
      NETWORK_ERROR_STATUS,
    )
  }

  if (response.status === 401 && unauthorizedHandler) {
    unauthorizedHandler()
    throw new ApiError('La sesión expiró; redirigiendo al inicio de sesión…', 401)
  }
  return response
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetchWithSession(path, init, { 'Content-Type': 'application/json' })

  if (!response.ok) throw await apiErrorFrom(response)

  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}

/** Una respuesta fallida, traducida: el código máquina manda sobre la prosa del servicio. */
async function apiErrorFrom(response: Response): Promise<ApiError> {
  let problem: ProblemDetails = {}
  try {
    problem = await response.json()
  } catch {
    // 404/204 sin cuerpo
  }

  // El código máquina manda: es lo único estable que trae la respuesta. El estado HTTP
  // no alcanza (dos 409 pueden ser cosas distintas) y el mensaje del servicio es prosa
  // que puede cambiar sin previo aviso. Un código que esta versión no conoce cae a esa
  // prosa, que ya viene en español.
  // Una validación de campo no trae código, y su `title` es la prosa en inglés que pone
  // ASP.NET. El detalle de cada campo ya viaja en `errors` y lo muestra el formulario
  // sobre el input; acá solo queda decir en español que algo no pasó la validación.
  const validacionDeCampo =
    problem.errors && Object.keys(problem.errors).length > 0
      ? `Algún dato no es válido: ${Object.entries(problem.errors)
          .map(([field, errs]) => `${field} (${Array.isArray(errs) ? errs.join(', ') : errs})`)
          .join('; ')}`
      : undefined

  const message =
    apiErrorCodeLabelFor(problem.code) ??
    validacionDeCampo ??
    problem.detail ??
    problem.title ??
    statusFallback(response.status)
  return new ApiError(message, response.status, problem.errors, problem.code, extensionsOf(problem))
}

function query(
  params: Record<string, string | number | boolean | (string | number | boolean)[] | undefined>,
): string {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== '') {
      if (Array.isArray(value)) {
        for (const item of value) {
          if (item !== undefined && item !== '') {
            search.append(key, String(item))
          }
        }
      } else {
        search.set(key, String(value))
      }
    }
  }
  const qs = search.toString()
  return qs ? `?${qs}` : ''
}

export class HttpApiClient implements ApiClient {
  // ---------- Contactos (crm-service) ----------

  listContacts(params: ListContactsParams): Promise<CrmPagedResponse<ContactResponse>> {
    return request(`/v1/contacts${query({ ...params })}`)
  }

  listContactClassifications(): Promise<ContactClassificationResponse[]> {
    return request(`/v1/contact-classifications`)
  }

  // ---------- Listas de contactos (crm-service) ----------

  listContactLists(): Promise<ContactListResponse[]> {
    return request(`/v1/contact-lists`)
  }

  getContactList(id: string): Promise<ContactListDetailResponse> {
    return request(`/v1/contact-lists/${id}`)
  }

  // ---------- Interruptores de funciones (events-service) ----------

  listFeatureSwitches(): Promise<FeatureSwitchResponse[]> {
    return request('/v1/feature-switches')
  }

  listPublicFeatureSwitches(): Promise<FeatureSwitchResponse[]> {
    return request('/v1/public/feature-switches')
  }

  // ---------- Cuentas (events-service, anónimas) ----------
  // Bajo /v1/accounts y no /v1/public: el proxy manda /v1/public/ a convocatorias-service.

  signup(body: SignupRequest): Promise<SignupResponse> {
    return request('/v1/accounts/signup', { method: 'POST', body: JSON.stringify(body) })
  }

  resendActivation(email: string): Promise<SignupResponse> {
    return request('/v1/accounts/signup/resend', {
      method: 'POST',
      body: JSON.stringify({ email }),
    })
  }

  activateAccount(body: ActivateAccountRequest): Promise<ActivateAccountResponse> {
    return request('/v1/accounts/activate', { method: 'POST', body: JSON.stringify(body) })
  }

  setFeatureSwitch(key: string, body: SetFeatureSwitchRequest): Promise<void> {
    return request(`/v1/feature-switches/${encodeURIComponent(key)}`, {
      method: 'PUT',
      body: JSON.stringify(body),
    })
  }

  // ---------- Eventos ----------

  listEventos(params: ListEventosParams): Promise<PagedResponse<EventoListItem>> {
    return request(`/v1/events${query({ ...params })}`)
  }

  listDoorEvents(): Promise<DoorEventResponse[]> {
    return request(`/v1/events/door`)
  }

  getEvento(id: string): Promise<EventoDetail> {
    return request(`/v1/events/${id}`)
  }

  async createEvento(body: CreateEventoRequest): Promise<string> {
    const res = await request<{ id?: string } | string>(`/v1/events`, {
      method: 'POST',
      body: JSON.stringify(body),
    })
    if (typeof res === 'object' && res !== null && 'id' in res) {
      return res.id as string
    }
    return String(res).replace(/^"|"$/g, '')
  }

  updateEvento(id: string, body: UpdateEventoRequest): Promise<EventoDetail> {
    return request(`/v1/events/${id}`, { method: 'PATCH', body: JSON.stringify(body) })
  }

  iniciarEvento(id: string): Promise<EventoDetail> {
    return request(`/v1/events/${id}/start`, { method: 'POST' })
  }

  // ---------- Cartelera pública (convocatorias-service) ----------
  // Anónima: el proxy manda `/v1/public/` a convocatorias, que republica una vista recortada
  // del evento. events-service no se alcanza desde internet.

  listPublicEventos(): Promise<PublicEvento[]> {
    return request(`/v1/public/events`)
  }

  getPublicEvento(id: string): Promise<PublicEventoDetail> {
    return request(`/v1/public/events/${id}`)
  }

  // ---------- Portal de registro (convocatorias-service) ----------
  // El token viaja en el cuerpo de un POST, nunca en la URL: así no queda en ningún registro de
  // acceso (spec convocatorias/06 §2.1).

  resolveRegistration(token: string): Promise<ResolvedInvitationResponse> {
    return request(`/v1/public/registrations/resolve`, {
      method: 'POST',
      body: JSON.stringify({ token }),
    })
  }

  register(body: PublicRegisterRequest): Promise<PublicRegistrationResponse> {
    return request(`/v1/public/registrations`, { method: 'POST', body: JSON.stringify(body) })
  }

  getTicket(qrCode: string): Promise<RegistrationTicketResponse> {
    return request(`/v1/public/registrations/${encodeURIComponent(qrCode)}`)
  }

  ticketQrUrl(qrCode: string): string {
    return `${config.eventosApiUrl}/v1/public/registrations/${encodeURIComponent(qrCode)}/qr`
  }

  unsubscribe(token: string): Promise<UnsubscribeResponse> {
    return request(`/v1/public/unsubscribe`, { method: 'POST', body: JSON.stringify({ token }) })
  }

  cerrarEvento(id: string): Promise<EventoDetail> {
    return request(`/v1/events/${id}/close`, { method: 'POST' })
  }

  cancelarEvento(id: string, razon: string): Promise<EventoDetail> {
    return request(`/v1/events/${id}/cancel`, {
      method: 'POST',
      body: JSON.stringify({ reason: razon }),
    })
  }

  async uploadEventoImage(
    eventId: string,
    file: File,
    kind: 'Cover' | 'Gallery',
  ): Promise<EventoImage> {
    const formData = new FormData()
    formData.append('file', file)
    formData.append('kind', kind)
    const path = `/v1/events/${eventId}/images`
    // Sin Content-Type: el navegador pone el multipart con su boundary.
    const res = await fetchWithSession(path, { method: 'POST', body: formData }, {})
    if (!res.ok) throw await apiErrorFrom(res)
    return res.json()
  }

  async listEventoImages(eventId: string, kind?: 'Cover' | 'Gallery'): Promise<EventoImage[]> {
    const query = kind ? `?kind=${kind}` : ''
    return request(`/v1/events/${eventId}/images${query}`)
  }

  async deleteEventoImage(eventId: string, imageId: string): Promise<void> {
    return request(`/v1/events/${eventId}/images/${imageId}`, { method: 'DELETE' })
  }

  // ---------- Ciudades ----------

  updateCiudad(eventoId: string, ciudadId: string, body: CiudadRequest): Promise<CiudadResponse> {
    return request(`/v1/events/${eventoId}/cities/${ciudadId}`, {
      method: 'PATCH',
      body: JSON.stringify(body),
    })
  }

  // ---------- Convocatorias (convocatorias-service) ----------

  listTemplates(selectable?: boolean): Promise<TemplateListItem[]> {
    return request(`/v1/convocatorias/templates${query({ selectable })}`)
  }

  listCampaigns(params: ListCampaignsParams): Promise<PagedResponse<CampaignResponse>> {
    return request(`/v1/convocatorias/campaigns${query({ ...params })}`)
  }

  listContactCheckIns(contactIds: string[]): Promise<ContactCheckInResponse[]> {
    return request(`/v1/convocatorias/check-ins${query({ contactId: contactIds })}`)
  }

  checkIn(eventId: string, qrCode: string): Promise<CheckInResponse> {
    return request(`/v1/convocatorias/events/${eventId}/check-ins`, {
      method: 'POST',
      body: JSON.stringify({ qrCode }),
    })
  }

  checkInByRegistration(eventId: string, registrationId: string): Promise<CheckInResponse> {
    return request(`/v1/convocatorias/events/${eventId}/check-ins/by-registration`, {
      method: 'POST',
      body: JSON.stringify({ registrationId }),
    })
  }

  undoCheckIn(eventId: string, registrationId: string): Promise<void> {
    return request(`/v1/convocatorias/events/${eventId}/check-ins/${registrationId}`, {
      method: 'DELETE',
    })
  }

  listDoorRegistrations(
    eventId: string,
    params: ListDoorRegistrationsParams,
  ): Promise<DoorListResponse> {
    return request(`/v1/convocatorias/events/${eventId}/registrations/door${query({ ...params })}`)
  }

  listContactCampaigns(contactIds: string[]): Promise<ContactCampaignResponse[]> {
    return request(`/v1/convocatorias/campaign-recipients${query({ contactId: contactIds })}`)
  }

  getCampaign(id: string): Promise<CampaignResponse> {
    return request(`/v1/convocatorias/campaigns/${id}`)
  }

  createCampaign(body: CreateCampaignRequest): Promise<CampaignResponse> {
    return request(`/v1/convocatorias/campaigns`, {
      method: 'POST',
      body: JSON.stringify(body),
    })
  }

  updateCampaign(id: string, body: UpdateCampaignRequest): Promise<CampaignResponse> {
    return request(`/v1/convocatorias/campaigns/${id}`, {
      method: 'PATCH',
      body: JSON.stringify(body),
    })
  }

  selectCampaignRecipients(
    id: string,
    body: SelectRecipientsRequest,
  ): Promise<RecipientSelectionResponse> {
    return request(`/v1/convocatorias/campaigns/${id}/recipients/select`, {
      method: 'POST',
      body: JSON.stringify(body),
    })
  }

  launchCampaign(id: string, body: LaunchCampaignRequest): Promise<CampaignResponse> {
    return request(`/v1/convocatorias/campaigns/${id}/launch`, {
      method: 'POST',
      body: JSON.stringify(body),
    })
  }

  cancelCampaign(id: string): Promise<CampaignResponse> {
    return request(`/v1/convocatorias/campaigns/${id}/cancel`, { method: 'POST' })
  }

  retryCampaignFailed(id: string): Promise<CampaignResponse> {
    return request(`/v1/convocatorias/campaigns/${id}/retry-failed`, { method: 'POST' })
  }

  async stageCampaignRecipientUpload(id: string, file: File): Promise<RecipientUploadResponse> {
    const formData = new FormData()
    formData.append('file', file)
    const path = `/v1/convocatorias/campaigns/${id}/recipient-uploads`
    // Sin Content-Type: el navegador pone el multipart con su boundary.
    const res = await fetchWithSession(path, { method: 'POST', body: formData }, {})
    if (!res.ok) throw await apiErrorFrom(res)
    return res.json()
  }

  confirmCampaignRecipientUpload(id: string): Promise<RecipientUploadResponse> {
    return request(`/v1/convocatorias/campaigns/${id}/recipient-uploads/confirm`, {
      method: 'POST',
    })
  }

  sendCancellationNotice(eventId: string): Promise<CancellationNoticeResponse> {
    return request(`/v1/convocatorias/events/${eventId}/cancellation-notice`, { method: 'POST' })
  }

  getEventTracking(eventId: string): Promise<EventTrackingResponse> {
    return request(`/v1/convocatorias/events/${eventId}/tracking`)
  }

  closeEventRegistrations(eventId: string): Promise<RegistrationWindowResponse> {
    return request(`/v1/convocatorias/events/${eventId}/close-registrations`, { method: 'POST' })
  }

  reopenEventRegistrations(eventId: string): Promise<RegistrationWindowResponse> {
    return request(`/v1/convocatorias/events/${eventId}/reopen-registrations`, { method: 'POST' })
  }

  listEventParticipants(
    eventId: string,
    params: ListParticipantsParams,
  ): Promise<PagedResponse<ParticipantResponse>> {
    return request(`/v1/convocatorias/events/${eventId}/participants${query({ ...params })}`)
  }

  listCampaignRecipients(
    id: string,
    params: { page?: number; pageSize?: number },
  ): Promise<PagedResponse<ParticipantResponse>> {
    return request(`/v1/convocatorias/campaigns/${id}/recipients${query({ ...params })}`)
  }

  async exportEventParticipantsCsv(
    eventId: string,
    params: ListParticipantsParams,
  ): Promise<ParticipantsCsvFile> {
    const token = tokenProvider ? await tokenProvider() : null
    const headers: Record<string, string> = {}
    if (token) headers.Authorization = `Bearer ${token}`

    // El CSV ignora la paginación: viajan solo los cinco filtros.
    const { origin, sendStatus, registered, search, campaignId } = params
    const filters = query({ origin, sendStatus, registered, search, campaignId })
    const path = `/v1/convocatorias/events/${eventId}/participants.csv${filters}`
    const res = await fetch(`${config.eventosApiUrl}${path}`, { headers }).catch(() => {
      throw new ApiError(
        'No se pudo contactar el servicio. Verifique su conexión e intente nuevamente.',
        NETWORK_ERROR_STATUS,
      )
    })
    if (res.status === 401 && unauthorizedHandler) {
      unauthorizedHandler()
      throw new ApiError('La sesión expiró; redirigiendo al inicio de sesión…', 401)
    }
    if (!res.ok) {
      const problem: ProblemDetails = await res.json().catch(() => ({}))
      throw new ApiError(
        apiErrorCodeLabelFor(problem.code) ?? 'No se pudo exportar la lista de participantes.',
        res.status,
        problem.errors,
        problem.code,
      )
    }
    const disposition = res.headers.get('Content-Disposition') ?? ''
    const fileName =
      /filename="?([^";]+)"?/i.exec(disposition)?.[1] ?? `convocatoria-${eventId}.csv`
    return { blob: await res.blob(), fileName }
  }

  listCampaignExclusions(
    id: string,
    params: { page?: number; pageSize?: number },
  ): Promise<PagedResponse<CampaignExclusionRow>> {
    return request(`/v1/convocatorias/campaigns/${id}/exclusions${query({ ...params })}`)
  }

  async getFileContentUrl(storedFileId: string): Promise<string> {
    const res = await fetchWithSession(`/v1/files/${storedFileId}/content`, undefined, {})
    if (!res.ok) throw await apiErrorFrom(res)
    const blob = await res.blob()
    return URL.createObjectURL(blob)
  }

  // ---------- Historial de envíos (notification-service) ----------
  // El host de la aplicación enruta solo estos dos GET a notification-service (ADR-0023).

  listNotifications(
    params: ListNotificationsParams,
  ): Promise<PagedResponse<NotificationHistoryItem>> {
    return request(`/v1/notifications${query({ ...params })}`)
  }

  getNotificationBatch(batchId: string): Promise<NotificationBatch> {
    return request(`/v1/notifications/batches/${batchId}`)
  }

  // ---------- Sorteos ----------

  getRaffle(eventId: string): Promise<RaffleResponse> {
    return request(`/v1/events/${eventId}/raffle`)
  }

  createRaffle(eventId: string): Promise<string> {
    return request(`/v1/events/${eventId}/raffle`, { method: 'POST' })
  }

  updateRaffle(eventId: string, body: UpdateRaffleRequest): Promise<void> {
    return request(`/v1/events/${eventId}/raffle`, { method: 'PATCH', body: JSON.stringify(body) })
  }

  deleteRaffle(eventId: string): Promise<void> {
    return request(`/v1/events/${eventId}/raffle`, { method: 'DELETE' })
  }

  startRaffle(eventId: string): Promise<RaffleResponse> {
    return request(`/v1/events/${eventId}/raffle/start`, { method: 'POST' })
  }

  reopenRaffle(eventId: string): Promise<RaffleResponse> {
    return request(`/v1/events/${eventId}/raffle/reopen`, { method: 'POST' })
  }

  completeRaffle(eventId: string): Promise<RaffleResponse> {
    return request(`/v1/events/${eventId}/raffle/complete`, { method: 'POST' })
  }

  reissueRaffle(eventId: string, body: ReissueRaffleRequest): Promise<RaffleResponse> {
    return request(`/v1/events/${eventId}/raffle/reissue`, {
      method: 'POST',
      body: JSON.stringify(body),
    })
  }

  setRaffleDisplayFields(eventId: string, body: RaffleDisplayFields): Promise<RaffleResponse> {
    return request(`/v1/events/${eventId}/raffle/display-fields`, {
      method: 'PUT',
      body: JSON.stringify(body),
    })
  }

  setCertificateSigners(eventId: string, body: SetCertificateSignersRequest): Promise<void> {
    return request(`/v1/events/${eventId}/raffle/certificate-signers`, {
      method: 'PUT',
      body: JSON.stringify(body),
    })
  }

  getRaffleAttendees(eventId: string): Promise<RaffleAttendeesResponse> {
    return request(`/v1/events/${eventId}/raffle/attendees`)
  }

  setAttendeeOverride(eventId: string, body: SetAttendeeOverrideRequest): Promise<void> {
    return request(`/v1/events/${eventId}/raffle/attendees/overrides`, {
      method: 'POST',
      body: JSON.stringify(body),
    })
  }

  removeAttendeeOverride(eventId: string, email: string): Promise<void> {
    return request(`/v1/events/${eventId}/raffle/attendees/overrides`, {
      method: 'DELETE',
      body: JSON.stringify({ email }),
    })
  }

  createRaffleRound(eventId: string, body: RaffleRoundRequest): Promise<string> {
    return request(`/v1/events/${eventId}/raffle/rounds`, {
      method: 'POST',
      body: JSON.stringify(body),
    })
  }

  updateRaffleRound(eventId: string, roundId: string, body: RaffleRoundRequest): Promise<void> {
    return request(`/v1/events/${eventId}/raffle/rounds/${roundId}`, {
      method: 'PATCH',
      body: JSON.stringify(body),
    })
  }

  deleteRaffleRound(eventId: string, roundId: string): Promise<void> {
    return request(`/v1/events/${eventId}/raffle/rounds/${roundId}`, { method: 'DELETE' })
  }

  createPrizes(eventId: string, body: CreatePrizesRequest): Promise<PrizeResponse[]> {
    return request(`/v1/events/${eventId}/raffle/prizes`, {
      method: 'POST',
      body: JSON.stringify(body),
    })
  }

  async setPrizeImage(
    eventId: string,
    prizeIds: string[],
    file: File,
  ): Promise<{ imageUrl: string }> {
    const formData = new FormData()
    formData.append('file', file)
    for (const id of prizeIds) formData.append('prizeIds', id)
    // Sin Content-Type: el navegador pone el multipart con su boundary.
    const res = await fetchWithSession(
      `/v1/events/${eventId}/raffle/prizes/image`,
      { method: 'POST', body: formData },
      {},
    )
    if (!res.ok) throw await apiErrorFrom(res)
    return res.json()
  }

  removePrizeImage(eventId: string, prizeIds: string[]): Promise<void> {
    return request(`/v1/events/${eventId}/raffle/prizes/image/remove`, {
      method: 'POST',
      body: JSON.stringify({ prizeIds }),
    })
  }

  importPrizes(eventId: string, body: ImportPrizesRequest): Promise<ImportPrizesResponse> {
    return request(`/v1/events/${eventId}/raffle/prizes/import`, {
      method: 'POST',
      body: JSON.stringify(body),
    })
  }

  updatePrize(eventId: string, prizeId: string, body: UpdatePrizeRequest): Promise<void> {
    return request(`/v1/events/${eventId}/raffle/prizes/${prizeId}`, {
      method: 'PATCH',
      body: JSON.stringify(body),
    })
  }

  deletePrize(eventId: string, prizeId: string): Promise<void> {
    return request(`/v1/events/${eventId}/raffle/prizes/${prizeId}`, { method: 'DELETE' })
  }

  getPrizeEligible(eventId: string, prizeId: string, limit?: number): Promise<EligibleResponse> {
    const query = limit ? `?limit=${limit}` : ''
    return request(`/v1/events/${eventId}/raffle/prizes/${prizeId}/eligible${query}`)
  }

  drawPrize(eventId: string, prizeId: string): Promise<PrizeResponse> {
    return request(`/v1/events/${eventId}/raffle/prizes/${prizeId}/draw`, { method: 'POST' })
  }

  drawPrizeBatch(eventId: string, prizeIds: string[]): Promise<PrizeResponse[]> {
    return request(`/v1/events/${eventId}/raffle/prizes/draw-batch`, {
      method: 'POST',
      body: JSON.stringify({ prizeIds }),
    })
  }

  reinstateRaffleAttendee(eventId: string, attendeeId: string): Promise<RaffleResponse> {
    return request(`/v1/events/${eventId}/raffle/attendees/${attendeeId}/reinstate`, {
      method: 'POST',
    })
  }

  voidPrizeDraw(
    eventId: string,
    prizeId: string,
    body: VoidPrizeDrawRequest,
  ): Promise<PrizeResponse> {
    return request(`/v1/events/${eventId}/raffle/prizes/${prizeId}/void`, {
      method: 'POST',
      body: JSON.stringify(body),
    })
  }

  async handOverPrize(
    eventId: string,
    prizeId: string,
    body: HandOverPrizeRequest,
  ): Promise<PrizeResponse> {
    const formData = new FormData()
    formData.append('receiverName', body.receiverName)
    formData.append('receiverIdentityDocument', body.receiverIdentityDocument)
    formData.append('receiverIsWinner', String(body.receiverIsWinner))
    formData.append('signature', body.signature, 'firma.png')
    // Sin Content-Type: el navegador pone el multipart con su boundary.
    const response = await fetchWithSession(
      `/v1/events/${eventId}/raffle/prizes/${prizeId}/handover`,
      { method: 'POST', body: formData },
      {},
    )
    if (!response.ok) throw await apiErrorFrom(response)
    return (await response.json()) as PrizeResponse
  }

  reversePrizeHandover(
    eventId: string,
    prizeId: string,
    body: ReversePrizeHandoverRequest,
  ): Promise<PrizeResponse> {
    return request(`/v1/events/${eventId}/raffle/prizes/${prizeId}/handover/reverse`, {
      method: 'POST',
      body: JSON.stringify(body),
    })
  }

  findPrizeDrawByCode(code: string): Promise<PrizeDrawLookupResponse> {
    return request(`/v1/prize-draws/by-code/${encodeURIComponent(code)}`)
  }

  listRaffles(params: PagingParams): Promise<PagedResponse<RaffleListItem>> {
    return request(`/v1/raffles${query({ ...params })}`)
  }

  getRaffleTotals(): Promise<RaffleTotals> {
    return request(`/v1/raffles/totals`)
  }

  listRaffleParticipants(eventId: string): Promise<RaffleParticipantItem[]> {
    return request(`/v1/events/${eventId}/raffle/participants`)
  }

  importRaffleParticipants(
    eventId: string,
    body: ImportRaffleParticipantsRequest,
  ): Promise<ImportRaffleParticipantsResponse> {
    return request(`/v1/events/${eventId}/raffle/participants/import`, {
      method: 'POST',
      body: JSON.stringify(body),
    })
  }

  setRaffleParticipantCheckIn(
    eventId: string,
    registrationIdOrCode: string,
    checkedIn: boolean,
  ): Promise<RaffleParticipantItem> {
    return request(`/v1/events/${eventId}/raffle/participants/check-in`, {
      method: 'POST',
      body: JSON.stringify({ code: registrationIdOrCode, checkedIn }),
    })
  }

  bulkRaffleCheckIn(eventId: string, checkedIn: boolean): Promise<void> {
    return request(`/v1/events/${eventId}/raffle/participants/check-in-bulk`, {
      method: 'POST',
      body: JSON.stringify({ checkedIn }),
    })
  }

  removeRaffleParticipant(eventId: string, email: string): Promise<void> {
    return request(`/v1/events/${eventId}/raffle/participants/remove`, {
      method: 'POST',
      body: JSON.stringify({ email }),
    })
  }
}
