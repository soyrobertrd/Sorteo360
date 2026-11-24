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
  EventoImage,
  EventTrackingResponse,
  RegistrationWindowResponse,
  EventoDetail,
  EventoListItem,
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
  RecipientSelectionResponse,
  RecipientUploadResponse,
  SelectRecipientsRequest,
  PagedResponse,
  PublicEvento,
  PublicEventoDetail,
  PublicRegisterRequest,
  PublicRegistrationResponse,
  RegistrationTicketResponse,
  ResolvedInvitationResponse,
  UnsubscribeResponse,
  TemplateListItem,
  UpdateCampaignRequest,
  UpdateEventoRequest,
} from './types'
import type { CampaignExclusionRow } from './types'
import type { FeatureSwitchResponse, SetFeatureSwitchRequest } from './types'
import type {
  ActivateAccountRequest,
  ActivateAccountResponse,
  SignupRequest,
  SignupResponse,
  ImportPrizesRequest,
  ImportPrizesResponse,
} from './types'

/**
 * Puerto de datos de la aplicación. Dos implementaciones:
 *  - HttpApiClient  → eventos-service real (VITE_API_MODE=http)
 *  - MockApiClient  → datos de demostración en memoria (VITE_API_MODE=mock)
 */
export interface ApiClient {
  // Contactos (crm-service)
  /** Misma forma paginada que las empresas: `totalCount`, no el `total` de eventos. */
  listContacts(params: ListContactsParams): Promise<CrmPagedResponse<ContactResponse>>
  /** El catálogo de clasificaciones de contacto, ordenado por nombre. Lo administra `central_admin`. */
  listContactClassifications(): Promise<ContactClassificationResponse[]>

  // Listas de contactos (crm-service). Todo se acota a la OPT de quien pregunta.
  /** Las listas de mi OPT, sin paginar, por nombre. */
  listContactLists(): Promise<ContactListResponse[]>
  getContactList(id: string): Promise<ContactListDetailResponse>

  // Interruptores de funciones (crm-service, docs/adr/0039).
  /** Solo las filas guardadas; una clave sin fila está encendida. Pide `platform:staff`. */
  listFeatureSwitches(): Promise<FeatureSwitchResponse[]>
  /** Los mismos interruptores, sin sesión: el portal público los lee para saber si abrir. */
  listPublicFeatureSwitches(): Promise<FeatureSwitchResponse[]>

  // Cuentas (anónimas): registro, reenvío del correo y activación con la contraseña.
  signup(body: SignupRequest): Promise<SignupResponse>
  resendActivation(email: string): Promise<SignupResponse>
  activateAccount(body: ActivateAccountRequest): Promise<ActivateAccountResponse>
  /**
   * `feature-switches:write`. Guarda la fila a nombre de quien llama. Una clave mal formada es 400
   * `feature_switch.invalid_key`; que la clave exista no se revisa.
   */
  setFeatureSwitch(key: string, body: SetFeatureSwitchRequest): Promise<void>

  // Eventos
  listEventos(params: ListEventosParams): Promise<PagedResponse<EventoListItem>>
  /** Los eventos que el terminal de la puerta puede elegir; `check-in:read` basta. */
  listDoorEvents(): Promise<DoorEventResponse[]>
  getEvento(id: string): Promise<EventoDetail>
  createEvento(body: CreateEventoRequest): Promise<string>
  updateEvento(id: string, body: UpdateEventoRequest): Promise<EventoDetail>
  iniciarEvento(id: string): Promise<EventoDetail>
  cerrarEvento(id: string): Promise<EventoDetail>
  cancelarEvento(id: string, razon: string): Promise<EventoDetail>
  uploadEventoImage(eventId: string, file: File, kind: 'Cover' | 'Gallery'): Promise<EventoImage>
  listEventoImages(eventId: string, kind?: 'Cover' | 'Gallery'): Promise<EventoImage[]>
  deleteEventoImage(eventId: string, imageId: string): Promise<void>

  // Cartelera pública (convocatorias-service, anónima)
  listPublicEventos(): Promise<PublicEvento[]>
  getPublicEvento(id: string): Promise<PublicEventoDetail>

  // Portal de registro (convocatorias-service, anónimo): las páginas de los enlaces del correo
  resolveRegistration(token: string): Promise<ResolvedInvitationResponse>
  register(body: PublicRegisterRequest): Promise<PublicRegistrationResponse>
  getTicket(qrCode: string): Promise<RegistrationTicketResponse>
  /** La URL de la imagen del QR de una entrada, para un `<img src>`. */
  ticketQrUrl(qrCode: string): string
  unsubscribe(token: string): Promise<UnsubscribeResponse>

  // Ciudades
  updateCiudad(eventoId: string, ciudadId: string, body: CiudadRequest): Promise<CiudadResponse>

  // ---------- Convocatorias (convocatorias-service) ----------

  /** Solo lectura en fase 1: nadie crea, edita ni borra una plantilla desde la pantalla. */
  listTemplates(selectable?: boolean): Promise<TemplateListItem[]>

  listCampaigns(params: ListCampaignsParams): Promise<PagedResponse<CampaignResponse>>
  getCampaign(id: string): Promise<CampaignResponse>
  createCampaign(body: CreateCampaignRequest): Promise<CampaignResponse>
  /** PATCH: solo mientras la campaña sigue en `draft`. */
  updateCampaign(id: string, body: UpdateCampaignRequest): Promise<CampaignResponse>
  /** Construye la lista desde crm-service; sin filtros trae el directorio entero. */
  selectCampaignRecipients(
    id: string,
    body: SelectRecipientsRequest,
  ): Promise<RecipientSelectionResponse>
  /** `expectedRecipientCount` es la guarda: lo que quien lanza tenía en pantalla. */
  launchCampaign(id: string, body: LaunchCampaignRequest): Promise<CampaignResponse>
  cancelCampaign(id: string): Promise<CampaignResponse>
  /**
   * Devuelve a la cola los destinatarios que el emisor rechazó. Solo sobre una campaña en
   * `sending` o `sent`; sin rechazados no hace nada y contesta igual.
   */
  retryCampaignFailed(id: string): Promise<CampaignResponse>
  /** Sube y parsea sin escribir nada; la revisión decide si se confirma. */
  stageCampaignRecipientUpload(id: string, file: File): Promise<RecipientUploadResponse>
  /** Sin cuerpo: confirma la carga pendiente que dejó `stageCampaignRecipientUpload`. */
  confirmCampaignRecipientUpload(id: string): Promise<RecipientUploadResponse>
  /** Avisa a los inscritos de un evento ya cancelado. Repetirlo solo reintenta a quien falta. */
  sendCancellationNotice(eventId: string): Promise<CancellationNoticeResponse>
  /** El resumen del evento: invitados, inscritos y quién falta. Nunca 404. */
  getEventTracking(eventId: string): Promise<EventTrackingResponse>
  /** Deja de aceptar inscripciones nuevas. Cerrar una ventana ya cerrada no cambia nada. */
  closeEventRegistrations(eventId: string): Promise<RegistrationWindowResponse>
  /** Vuelve a aceptarlas. Reabrir una ventana abierta no cambia nada. */
  reopenEventRegistrations(eventId: string): Promise<RegistrationWindowResponse>
  /** Una página de los participantes del evento; `campaignId` la reduce a una campaña. */
  listEventParticipants(
    eventId: string,
    params: ListParticipantsParams,
  ): Promise<PagedResponse<ParticipantResponse>>
  /**
   * Los destinatarios de una campaña, tenga evento o no: en una de evento, las mismas filas que
   * `listEventParticipants` con su `campaignId`. 404 si la campaña no existe.
   */
  listCampaignRecipients(
    id: string,
    params: { page?: number; pageSize?: number },
  ): Promise<PagedResponse<ParticipantResponse>>
  /**
   * Los eventos a los que asistieron estos contactos, una fila por contacto y evento, lo más
   * reciente primero. De 1 a 100 ids por llamada: una página del listado es una sola.
   */
  listContactCheckIns(contactIds: string[]): Promise<ContactCheckInResponse[]>
  /**
   * El check-in de un QrCode ya sin la URL del boleto. Repetirlo no es un error: responde la misma
   * inscripción con `alreadyCheckedIn` y la hora del primero. 404 `CHECK_IN_CODE_NOT_FOUND`, 409
   * `CHECK_IN_WRONG_EVENT`.
   */
  checkIn(eventId: string, qrCode: string): Promise<CheckInResponse>
  /** El mismo check-in, elegido en la lista de la puerta tras ver un documento de identidad. */
  checkInByRegistration(eventId: string, registrationId: string): Promise<CheckInResponse>
  /** Deshace un check-in; no falla si no había. 404 `REGISTRATION_NOT_FOUND`. */
  undoCheckIn(eventId: string, registrationId: string): Promise<void>
  /** La lista de la puerta, con los totales del evento sin filtrar. */
  listDoorRegistrations(
    eventId: string,
    params: ListDoorRegistrationsParams,
  ): Promise<DoorListResponse>
  /**
   * Las campañas lanzadas que tuvieron a estos contactos en su lista, una fila por contacto y
   * campaña, lo más reciente primero. De 1 a 100 ids por llamada.
   */
  listContactCampaigns(contactIds: string[]): Promise<ContactCampaignResponse[]>
  /** Todas las filas bajo los mismos filtros, sin paginar: el CSV lo arma el servicio. */
  exportEventParticipantsCsv(
    eventId: string,
    params: ListParticipantsParams,
  ): Promise<ParticipantsCsvFile>
  listCampaignExclusions(
    id: string,
    params: { page?: number; pageSize?: number },
  ): Promise<PagedResponse<CampaignExclusionRow>>
  getFileContentUrl(storedFileId: string): Promise<string>

  // ---------- Historial de envíos (notification-service, solo lectura, ADR-0023) ----------

  /** Filtra y pagina el servidor. Exige `notifications:read`. */
  listNotifications(
    params: ListNotificationsParams,
  ): Promise<PagedResponse<NotificationHistoryItem>>
  /** Un envío con su cuerpo sin sustituir y todos sus destinatarios. */
  getNotificationBatch(batchId: string): Promise<NotificationBatch>

  // ---------- Sorteos (events-service) ----------
  // Uno por evento, así que toda ruta se direcciona por el evento. Leer no exige permiso; armar
  // exige `raffles:write`.

  getRaffle(eventId: string): Promise<RaffleResponse>
  /** Nace en `draft`, sin rondas. Devuelve el id del sorteo. */
  createRaffle(eventId: string): Promise<string>
  /** Reemplaza la representación editable completa: armarla solo con `toUpdateRaffleRequest`. */
  updateRaffle(eventId: string, body: UpdateRaffleRequest): Promise<void>
  /** Solo un `draft`; se lleva rondas, premios y ajustes de asistentes. */
  deleteRaffle(eventId: string): Promise<void>
  /** Congela el bombo con la lista de la puerta leída en vivo. Exige `raffles:draw`. */
  startRaffle(eventId: string): Promise<RaffleResponse>
  /** La vuelta atrás de `start`, solo sin ningún sorteo de premio. Exige `raffles:draw`. */
  reopenRaffle(eventId: string): Promise<RaffleResponse>
  /**
   * Cierra el sorteo: genera el acta, versión 1, y después avisa por correo a cada ganador vivo.
   * Exige las seis autoridades y las instrucciones de retiro, y `raffles:draw`.
   */
  completeRaffle(eventId: string): Promise<RaffleResponse>
  /**
   * Corrige las autoridades de un sorteo completado y genera una versión más del acta; las
   * anteriores quedan. No reenvía ningún correo. Exige `raffles:draw`.
   */
  reissueRaffle(eventId: string, body: ReissueRaffleRequest): Promise<RaffleResponse>
  /**
   * Reemplaza los firmantes que el certificado de premio propone: en cualquier estado del sorteo,
   * mientras el evento siga abierto. Armarlo solo con `toSetCertificateSignersRequest`. Exige
   * `raffles:write`.
   */
  setCertificateSigners(eventId: string, body: SetCertificateSignersRequest): Promise<void>
  /** Qué datos del ganador se ven en pantalla (§10); solo en borrador. */
  setRaffleDisplayFields(eventId: string, body: RaffleDisplayFields): Promise<RaffleResponse>
  /** Las dos listas en una lectura; tras `start`, la congelada. */
  getRaffleAttendees(eventId: string): Promise<RaffleAttendeesResponse>
  /** Mete o saca a alguien a mano; reemplaza la decisión anterior. Solo en `draft`. */
  setAttendeeOverride(eventId: string, body: SetAttendeeOverrideRequest): Promise<void>
  /** Retira la decisión manual. Idempotente. */
  removeAttendeeOverride(eventId: string, email: string): Promise<void>
  createRaffleRound(eventId: string, body: RaffleRoundRequest): Promise<string>
  updateRaffleRound(eventId: string, roundId: string, body: RaffleRoundRequest): Promise<void>
  /** Solo una ronda vacía; las que quedan se renumeran `1..n`. */
  deleteRaffleRound(eventId: string, roundId: string): Promise<void>
  /** El servicio expande `quantity` en filas idénticas y las devuelve todas. */
  createPrizes(eventId: string, body: CreatePrizesRequest): Promise<PrizeResponse[]>
  importPrizes(eventId: string, body: ImportPrizesRequest): Promise<ImportPrizesResponse>
  /** Una foto para varios premios a la vez (los iguales de un grupo). Devuelve su URL. */
  setPrizeImage(eventId: string, prizeIds: string[], file: File): Promise<{ imageUrl: string }>
  removePrizeImage(eventId: string, prizeIds: string[]): Promise<void>
  updatePrize(eventId: string, prizeId: string, body: UpdatePrizeRequest): Promise<void>
  deletePrize(eventId: string, prizeId: string): Promise<void>
  /** Quién puede ganar el premio ahora: los nombres de la ruleta. Solo en curso. */
  /**
   * El bombo de un premio. Con `limit`, `count` sigue siendo el total y `participants` es una
   * muestra al azar de ese tamaño (R14): con 25.000 en el padrón, la ruleta no baja a todos.
   */
  getPrizeEligible(eventId: string, prizeId: string, limit?: number): Promise<EligibleResponse>
  /**
   * Sortea el premio en el servicio y devuelve el premio entero; el ganador es el sorteo vivo.
   * Exige `raffles:draw`. Nunca se reintenta solo (spec raffles/02 §7.1).
   */
  drawPrize(eventId: string, prizeId: string): Promise<PrizeResponse>
  /**
   * Hasta 10 premios en una sola transacción (A33). Si el bombo se agota a mitad, responde
   * `409 RAFFLE_POOL_EXHAUSTED` con los ya sorteados en `extensions.drawn`.
   */
  drawPrizeBatch(eventId: string, prizeIds: string[]): Promise<PrizeResponse[]>
  /**
   * Vuelve a dejar entrar al bombo a un participante anulado (por ejemplo, el que no se presentó a
   * tiempo). Solo en curso. Exige `raffles:write`.
   */
  reinstateRaffleAttendee(eventId: string, attendeeId: string): Promise<RaffleResponse>
  /** Anula el ganador vivo; el premio vuelve a `undrawn` y el sorteo anulado queda. Exige `raffles:draw`. */
  voidPrizeDraw(
    eventId: string,
    prizeId: string,
    body: VoidPrizeDrawRequest,
  ): Promise<PrizeResponse>
  /**
   * Registra quién retiró el premio, con su firma, sobre el ganador vivo; el premio queda
   * `redeemed`. En curso o completado. Exige `raffles:draw`.
   */
  handOverPrize(
    eventId: string,
    prizeId: string,
    body: HandOverPrizeRequest,
  ): Promise<PrizeResponse>
  /** Revierte la entrega viva con un motivo; la entrega queda y el premio vuelve a `drawn`. Exige `raffles:draw`. */
  reversePrizeHandover(
    eventId: string,
    prizeId: string,
    body: ReversePrizeHandoverRequest,
  ): Promise<PrizeResponse>
  /** Busca un código de canje en todos los sorteos. Nunca entrega. Leer no exige permiso. */
  findPrizeDrawByCode(code: string): Promise<PrizeDrawLookupResponse>
  /** Los sorteos de todos los eventos, del evento más nuevo al más viejo. */
  listRaffles(params: PagingParams): Promise<PagedResponse<RaffleListItem>>
  getRaffleTotals(): Promise<RaffleTotals>

  /** Participantes vinculados al sorteo (convocatoria + Excel/CSV/TXT + API/Webhook + manuales). */
  listRaffleParticipants(eventId: string): Promise<RaffleParticipantItem[]>
  importRaffleParticipants(
    eventId: string,
    body: ImportRaffleParticipantsRequest,
  ): Promise<ImportRaffleParticipantsResponse>
  setRaffleParticipantCheckIn(
    eventId: string,
    registrationIdOrCode: string,
    checkedIn: boolean,
  ): Promise<RaffleParticipantItem>
  bulkRaffleCheckIn(eventId: string, checkedIn: boolean): Promise<void>
  removeRaffleParticipant(eventId: string, email: string): Promise<void>
}

/** Error de negocio/HTTP normalizado para la UI (RFC 7807 en modo http). */
export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly fieldErrors?: Record<string, string[]>,
    /**
     * Código máquina del rechazo, tal como lo publica el servicio. `message` ya viene
     * traducido a partir de él; se conserva el código crudo porque es lo único estable
     * sobre lo que se puede ramificar. Ausente en las validaciones de campo y en los
     * rechazos del mock que todavía no tienen contraparte en el servicio.
     */
    public readonly code?: string,
    /**
     * Las demás extensiones ProblemDetails del rechazo, tal como llegaron: lo que el servicio
     * adjunta para resolver el conflicto sin otra petición (`candidateId` en
     * `CANDIDATE_SUPPLIER_ALREADY_QUOTED`, `existingContact` en `DUPLICATE_CONTACT_EMAIL`).
     * Sin tipar a propósito: cada pantalla valida la forma de la que le toca leer.
     */
    public readonly extensions: Readonly<Record<string, unknown>> = {},
  ) {
    super(message)
    this.name = 'ApiError'
  }
}
