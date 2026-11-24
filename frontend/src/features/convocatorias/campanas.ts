import { useEffect, useMemo, useRef, useState } from 'react'
import {
  keepPreviousData,
  useMutation,
  useQueries,
  useQuery,
  useQueryClient,
} from '@tanstack/react-query'
import { api } from '@/api'
import { config } from '@/lib/config'
import { campaignTemplateLabel } from '@/lib/labels'
import type {
  CampaignResponse,
  CampaignState,
  CreateCampaignRequest,
  EventoListItem,
  LaunchCampaignRequest,
  ListCampaignsParams,
  ListParticipantsParams,
  RecipientUploadResponse,
  SelectRecipientsRequest,
  UpdateCampaignRequest,
} from '@/api/types'
import { campanasDeTodosLosEventos, type Campana } from './mockParticipants'
import { useCampanasExtra } from './store'

/**
 * Campañas contra convocatorias-service: el listado, el detalle con sus destinatarios y
 * exclusiones, y las mutaciones del ciclo de vida.
 */

export const campaignKeys = {
  list: (params: ListCampaignsParams) => ['campaigns', 'list', params] as const,
  detail: (id: string) => ['campaigns', 'detail', id] as const,
  // Cuelgan del detalle para que invalidar el detalle también los refresque.
  recipients: (id: string) => ['campaigns', 'detail', id, 'recipients'] as const,
  exclusions: (id: string) => ['campaigns', 'detail', id, 'exclusions'] as const,
  templates: (selectable?: boolean) => ['campaigns', 'templates', selectable] as const,
}

/** Cada cuánto se relee una campaña en `sending` hasta que el worker la cierre. */
const SENDING_POLL_MS = 3000

export const CAMPANAS_PAGE_SIZE = 20

/**
 * Una fila de la tabla de Campañas. Entregados, rebotados y reenviados no los sirve el
 * servicio (ADR-0017): llegan `null` en una campaña real y la pantalla pone un guion. Las
 * filas simuladas del modo mock los conservan.
 */
export type CampanaFila = Omit<
  Campana,
  'tipo' | 'idioma' | 'fechaEnvio' | 'entregados' | 'rebotados' | 'reenviados' | 'estado'
> & {
  tipo: string
  idioma: string
  fechaEnvio: string | null
  entregados: number | null
  rebotados: number | null
  reenviados: number | null
  estado: Campana['estado'] | 'Borrador' | 'Cancelada'
  /** Presente solo en una campaña de convocatorias-service. */
  campaign?: CampaignResponse
}

const ESTADO_FILA: Record<CampaignState, CampanaFila['estado']> = {
  draft: 'Borrador',
  sending: 'Enviando',
  sent: 'Enviada',
  cancelled: 'Cancelada',
}

/** Lo que la tabla y el detalle muestran en lugar del evento de un mensaje directo. */
export const SIN_EVENTO = 'Sin evento'

/** El nombre del evento se une al leer, desde los eventos que la página ya tiene. */
export function campanaFilaFrom(
  c: CampaignResponse,
  evento?: Pick<EventoListItem, 'name'>,
): CampanaFila {
  return {
    id: c.id,
    eventoId: c.eventId ?? '',
    eventoNombre: c.eventId == null ? SIN_EVENTO : (evento?.name ?? c.eventId),
    tipo: campaignTemplateLabel[c.templateKey] ?? c.templateKey,
    idioma: c.language.toUpperCase(),
    fechaEnvio: (c.sentAt ?? c.launchedAt)?.slice(0, 10) ?? null,
    enviados: c.recipientCount,
    entregados: null,
    rebotados: null,
    reenviados: null,
    estado: ESTADO_FILA[c.state],
    campaign: c,
  }
}

/**
 * La tabla principal: `GET /campaigns` paginado por el servicio. En modo mock se suman,
 * solo en la primera página, las campañas simuladas por evento y las de `campanasExtra`,
 * para que la demostración conserve su embudo de entregas.
 */
export function useCampanasTabla(
  eventos: EventoListItem[],
  { page, eventId }: { page: number; eventId?: string },
) {
  const query = useCampaigns({ page, pageSize: CAMPANAS_PAGE_SIZE, eventId: eventId || undefined })
  const extra = useCampanasExtra()
  const isMock = config.apiMode !== 'http'

  const simuladas = useMemo(
    () =>
      isMock && page === 1
        ? [...campanasDeTodosLosEventos(eventos), ...extra].filter(
            (c) => !eventId || c.eventoId === eventId,
          )
        : [],
    [isMock, page, eventos, extra, eventId],
  )

  const filas = useMemo(() => {
    const eventosMap = new Map(eventos.map((e) => [e.id, e]))
    const reales = (query.data?.items ?? []).map((c) =>
      campanaFilaFrom(c, c.eventId ? eventosMap.get(c.eventId) : undefined),
    )
    return [...reales, ...simuladas]
  }, [query.data, eventos, simuladas])

  const apiTotal = query.data?.total ?? 0
  return {
    filas,
    total: apiTotal + simuladas.length,
    pageCount: Math.max(1, Math.ceil(apiTotal / CAMPANAS_PAGE_SIZE)),
    isPending: query.isPending,
    error: query.error,
  }
}

/** Fase 1: solo lectura. Sin `selectable`, trae también las dos transaccionales. */
export function useTemplates(selectable?: boolean) {
  return useQuery({
    queryKey: campaignKeys.templates(selectable),
    queryFn: () => api.listTemplates(selectable),
  })
}

export function useCampaigns(params: ListCampaignsParams) {
  return useQuery({
    queryKey: campaignKeys.list(params),
    queryFn: () => api.listCampaigns(params),
  })
}

/**
 * El lanzamiento contesta 202 en `sending`; la campaña pasa a `sent` cuando el worker
 * termina. Mientras siga en `sending` se relee sola, y React Query deja de hacerlo al
 * desmontarse el detalle. Al salir de `sending` refresca el listado y los destinatarios.
 */
export function useCampaign(id: string | undefined) {
  const queryClient = useQueryClient()
  const query = useQuery({
    queryKey: campaignKeys.detail(id ?? ''),
    queryFn: () => api.getCampaign(id as string),
    enabled: Boolean(id),
    refetchInterval: (q) => (q.state.data?.state === 'sending' ? SENDING_POLL_MS : false),
  })

  const state = query.data?.state
  const previous = useRef(state)
  useEffect(() => {
    if (previous.current === 'sending' && state !== 'sending' && id) {
      void queryClient.invalidateQueries({ queryKey: ['campaigns', 'list'] })
      void queryClient.invalidateQueries({ queryKey: campaignKeys.recipients(id) })
    }
    previous.current = state
  }, [state, id, queryClient])

  return query
}

/**
 * Los destinatarios de una campaña, leídos por su id: en una de evento son los participantes del
 * evento filtrados por ella, y un mensaje directo no tiene evento por el que leerlos.
 */
export function useCampaignRecipients(campaign: CampaignResponse | undefined) {
  return useQuery({
    queryKey: campaignKeys.recipients(campaign?.id ?? ''),
    queryFn: () => api.listCampaignRecipients(campaign!.id, { pageSize: 100 }),
    enabled: Boolean(campaign),
  })
}

export function useCampaignExclusions(id: string | undefined) {
  return useQuery({
    queryKey: campaignKeys.exclusions(id ?? ''),
    queryFn: () => api.listCampaignExclusions(id as string, { pageSize: 100 }),
    enabled: Boolean(id),
  })
}

/**
 * Alta, edición, selección de destinatarios, lanzamiento, cancelación y la carga por
 * spreadsheet. Cada mutación invalida el listado y, cuando aplica, el detalle de esa
 * campaña — nunca escribe el caché a mano, para no desincronizarse de lo que el
 * servicio realmente guardó.
 */
export function useCampaignMutations() {
  const queryClient = useQueryClient()
  const invalidate = (id?: string) => {
    void queryClient.invalidateQueries({ queryKey: ['campaigns', 'list'] })
    if (id) void queryClient.invalidateQueries({ queryKey: campaignKeys.detail(id) })
  }

  const create = useMutation({
    mutationFn: (body: CreateCampaignRequest) => api.createCampaign(body),
    onSuccess: () => invalidate(),
  })

  const update = useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateCampaignRequest }) =>
      api.updateCampaign(id, body),
    onSuccess: (_data, vars) => invalidate(vars.id),
  })

  const selectRecipients = useMutation({
    mutationFn: ({ id, body }: { id: string; body: SelectRecipientsRequest }) =>
      api.selectCampaignRecipients(id, body),
    onSuccess: (_data, vars) => invalidate(vars.id),
  })

  const launch = useMutation({
    mutationFn: ({ id, body }: { id: string; body: LaunchCampaignRequest }) =>
      api.launchCampaign(id, body),
    onSuccess: (_data, vars) => invalidate(vars.id),
  })

  const cancel = useMutation({
    mutationFn: (id: string) => api.cancelCampaign(id),
    onSuccess: (_data, id) => invalidate(id),
  })

  /** Reabre la cola de los rechazados; los participantes cambian de estado con ella. */
  const retryFailed = useMutation({
    mutationFn: (id: string) => api.retryCampaignFailed(id),
    onSuccess: (_data, id) => {
      invalidate(id)
      void queryClient.invalidateQueries({ queryKey: ['campaigns', 'participants'] })
    },
  })

  /** Solo parsea y guarda en revisión; nada se escribe en CRM ni en la campaña todavía. */
  const stageUpload = useMutation({
    mutationFn: ({ id, file }: { id: string; file: File }) =>
      api.stageCampaignRecipientUpload(id, file),
  })

  const confirmUpload = useMutation({
    mutationFn: (id: string) => api.confirmCampaignRecipientUpload(id),
    onSuccess: (_data, id) => invalidate(id),
  })

  return {
    create,
    update,
    selectRecipients,
    launch,
    cancel,
    retryFailed,
    stageUpload,
    confirmUpload,
  }
}

/**
 * Una página de los destinatarios de una campaña, con los filtros del servicio: quién no se
 * ha inscrito, a quién no le salió el correo, a quién se le suprimió.
 */
export function useCampaignParticipants(
  campaign: CampaignResponse | undefined,
  params: Omit<ListParticipantsParams, 'campaignId'>,
) {
  // Los destinatarios se leen por evento: una campaña sin evento no tiene esta lectura.
  const eventId = campaign?.eventId
  return useQuery({
    queryKey: ['campaigns', 'participants', campaign?.id ?? '', params] as const,
    queryFn: () => api.listEventParticipants(eventId!, { ...params, campaignId: campaign!.id }),
    enabled: Boolean(eventId),
    placeholderData: keepPreviousData,
  })
}

/**
 * Los recuentos de una campaña que el servicio no trae sumados: cada uno es el `total` de
 * una lectura de una sola fila con su filtro, como los mosaicos del historial de envíos.
 */
export function useCampaignCounts(campaign: CampaignResponse | undefined) {
  const filtros = {
    registrados: { registered: true },
    sinRegistrar: { registered: false },
    fallidos: { sendStatus: 'failed' },
    suprimidos: { sendStatus: 'suppressed' },
    pendientes: { sendStatus: 'pending' },
  } as const satisfies Record<string, Omit<ListParticipantsParams, 'campaignId'>>

  const claves = Object.keys(filtros) as (keyof typeof filtros)[]
  const eventId = campaign?.eventId
  const queries = useQueries({
    queries: claves.map((clave) => ({
      queryKey: ['campaigns', 'participants', campaign?.id ?? '', 'count', clave] as const,
      queryFn: () =>
        api.listEventParticipants(eventId!, {
          ...filtros[clave],
          campaignId: campaign!.id,
          pageSize: 1,
        }),
      enabled: Boolean(eventId),
    })),
  })

  const counts = Object.fromEntries(
    claves.map((clave, i) => [clave, queries[i].data?.total]),
  ) as Record<keyof typeof filtros, number | undefined>
  return { counts, error: queries.find((q) => q.error != null)?.error ?? null }
}

/**
 * El asistente de Nueva campaña contra el servicio. El borrador nace la primera vez que hace
 * falta: al cargar un archivo (la carga necesita una campaña a la que colgarse) o al lanzar.
 * Si se vuelve atrás y cambia la plantilla, el idioma o el texto de un mensaje directo, el mismo
 * borrador se edita; si cambia el evento (o pasa a no tener), ese borrador se cancela y nace otro,
 * porque el evento no se puede editar.
 *
 * Lanzar hace el resto del ciclo en orden: confirma la carga pendiente, selecciona desde el
 * CRM y lanza con el recuento que el servicio acaba de devolver. Si algo falla, el borrador
 * queda y un segundo intento lo reutiliza: seleccionar y confirmar son idempotentes.
 *
 * Con contactos elegidos a mano (`contactIds`, ya sin repetidos) el CRM puede no devolver
 * alguno, porque se borró entretanto. Entonces no lanza: deja la cuenta en `omitidos` y
 * resuelve `null`, para que quien lanza lo vea; un segundo intento con la misma cuenta lanza.
 */
export function useCampaignWizard() {
  const { create, update, cancel, selectRecipients, stageUpload, confirmUpload, launch } =
    useCampaignMutations()
  const draft = useRef<CampaignResponse | null>(null)
  const [staged, setStaged] = useState<RecipientUploadResponse | null>(null)
  const [uploadConfirmed, setUploadConfirmed] = useState(false)
  const [omitidos, setOmitidos] = useState<number | null>(null)

  const ensureDraft = async (body: CreateCampaignRequest): Promise<CampaignResponse> => {
    const current = draft.current
    if (current && current.eventId === body.eventId) {
      const sameText =
        (current.subject ?? undefined) === body.subject &&
        (current.message ?? undefined) === body.message
      if (
        current.templateKey === body.templateKey &&
        current.language === body.language &&
        sameText
      )
        return current
      // Asunto y texto solo viajan en un mensaje directo: una campaña de evento los rechaza.
      const { templateKey, language, subject, message } = body
      draft.current = await update.mutateAsync({
        id: current.id,
        body:
          body.eventId == null
            ? { templateKey, language, subject, message }
            : { templateKey, language },
      })
      return draft.current
    }
    if (current) {
      await cancel.mutateAsync(current.id)
      draft.current = null
      setStaged(null)
      setUploadConfirmed(false)
    }
    draft.current = await create.mutateAsync(body)
    return draft.current
  }

  /** Solo analiza el archivo; nada entra a la campaña hasta confirmar. */
  const stage = async (body: CreateCampaignRequest, file: File) => {
    const { id } = await ensureDraft(body)
    setStaged(null)
    setUploadConfirmed(false)
    setStaged(await stageUpload.mutateAsync({ id, file }))
  }

  const confirm = async () => {
    if (!draft.current || uploadConfirmed) return
    await confirmUpload.mutateAsync(draft.current.id)
    setUploadConfirmed(true)
  }

  const launchWith = async (
    body: CreateCampaignRequest,
    audience: {
      fromCrm: boolean
      fromFile: boolean
      classifications?: string[]
      contactIds?: string[]
      contactListId?: string
    },
  ): Promise<CampaignResponse | null> => {
    let campaign = await ensureDraft(body)
    if (audience.fromFile && staged) await confirm()
    if (audience.fromCrm) {
      const { contactIds, contactListId, classifications } = audience
      const selection = await selectRecipients.mutateAsync({
        id: campaign.id,
        body: contactIds ? { contactIds } : contactListId ? { contactListId } : { classifications },
      })
      campaign = selection.campaign
      const sinMatch = contactIds ? contactIds.length - selection.matched : 0
      if (sinMatch > 0 && sinMatch !== omitidos) {
        setOmitidos(sinMatch)
        return null
      }
    } else {
      campaign = await api.getCampaign(campaign.id)
    }
    return launch.mutateAsync({
      id: campaign.id,
      body: { expectedRecipientCount: campaign.recipientCount },
    })
  }

  const mutations = [create, update, cancel, selectRecipients, stageUpload, confirmUpload, launch]
  return {
    stage,
    confirm,
    launchWith,
    staged,
    uploadConfirmed,
    omitidos,
    isStaging: stageUpload.isPending,
    isConfirming: confirmUpload.isPending,
    confirmError: confirmUpload.error,
    isPending: mutations.some((m) => m.isPending),
    error: mutations.find((m) => m.error != null)?.error ?? null,
  }
}
