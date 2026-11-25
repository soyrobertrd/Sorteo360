import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/api'
import { ApiError } from '@/api/client'
import type {
  CreatePrizesRequest,
  HandOverPrizeRequest,
  ImportRaffleParticipantsRequest,
  PagingParams,
  PrizeResponse,
  RaffleResponse,
  RaffleRoundRequest,
  RaffleRoundResponse,
  ReissueRaffleRequest,
  ReversePrizeHandoverRequest,
  SetAttendeeOverrideRequest,
  BulkExcludeAttendeesRequest,
  SetCertificateSignersRequest,
  RaffleDisplayFields,
  VoidPrizeDrawRequest,
} from '@/api/types'
import { toUpdateRaffleRequest, type RaffleChanges } from './raffleRequest'
import { prizesOf } from './raffleView'
import type { PremioImportado } from './premioFile'

/** El tope de página del servicio: los listados del módulo leen una sola página así de grande. */
export const RAFFLE_PAGE_SIZE = 100

/** Una sola página, la primera, del tamaño del tope. */
const FIRST_PAGE: PagingParams = { page: 1, pageSize: RAFFLE_PAGE_SIZE }

export const raffleKeys = {
  all: ['raffles'] as const,
  detail: (eventId: string) => ['raffles', 'detail', eventId] as const,
  attendees: (eventId: string) => ['raffles', 'attendees', eventId] as const,
  eligible: (eventId: string, prizeId: string, limit?: number) =>
    ['raffles', 'eligible', eventId, prizeId, limit ?? 'todos'] as const,
  list: (params: PagingParams) => ['raffles', 'list', params] as const,
  totals: () => ['raffles', 'totals'] as const,
  byCode: (code: string) => ['raffles', 'by-code', code] as const,
  participants: (eventId: string) => ['raffles', 'participants', eventId] as const,
}

export function useRaffle(eventId: string) {
  return useQuery({
    queryKey: raffleKeys.detail(eventId),
    queryFn: () => api.getRaffle(eventId),
    enabled: Boolean(eventId),
  })
}

/**
 * Un código de canje, de cualquier sorteo. Un 404 es una respuesta, no un fallo pasajero: no se
 * reintenta. Entregar invalida el sorteo, y con él esta lectura.
 */
export function usePrizeDrawByCode(code: string) {
  return useQuery({
    queryKey: raffleKeys.byCode(code),
    queryFn: () => api.findPrizeDrawByCode(code),
    enabled: Boolean(code),
    retry: false,
  })
}

/**
 * Las dos listas de la puerta. No hay botón de refrescar (spec raffles/01 §10): se vuelve a leer
 * cuando la ventana recupera el foco, y `start` es quien lee la puerta en vivo.
 */
export function useRaffleAttendees(eventId: string) {
  return useQuery({
    queryKey: raffleKeys.attendees(eventId),
    queryFn: () => api.getRaffleAttendees(eventId),
    enabled: Boolean(eventId),
    // Llega gente mientras se arma el sorteo: la pantalla del operador relee al volver a ella.
    refetchOnWindowFocus: true,
  })
}

/**
 * El bombo de un premio: los nombres de la ruleta. Sin premio no se pide; con `paused` conserva lo
 * leído sin volver a pedirlo, como mientras ese premio se sortea.
 */
export function usePrizeEligible(
  eventId: string,
  prizeId: string | undefined,
  paused = false,
  limit?: number,
) {
  return useQuery({
    queryKey: raffleKeys.eligible(eventId, prizeId ?? '', limit),
    queryFn: () => api.getPrizeEligible(eventId, prizeId ?? '', limit),
    enabled: Boolean(eventId && prizeId) && !paused,
  })
}

export function useRaffleList(enabled = true) {
  return useQuery({
    queryKey: raffleKeys.list(FIRST_PAGE),
    queryFn: () => api.listRaffles(FIRST_PAGE),
    enabled,
  })
}

export function useRaffleTotals() {
  return useQuery({ queryKey: raffleKeys.totals(), queryFn: () => api.getRaffleTotals() })
}

/** Toda escritura mueve el detalle, los listados y los totales: se invalida el módulo entero. */
function useInvalidateRaffles() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: raffleKeys.all })
}

/**
 * El PATCH del sorteo, y la única forma de mandarlo (Decisión 5): recibe solo lo que cambió y lo
 * monta sobre el sorteo en caché con `toUpdateRaffleRequest`. El cambio entra en la caché antes de
 * salir, así que un segundo cambio que llegue antes de la relectura se monta sobre el primero en
 * vez de deshacerlo. Pase lo que pase se vuelve a leer, y un 409 de concurrencia se corrige solo.
 */
export function useUpdateRaffle(eventId: string) {
  const queryClient = useQueryClient()
  const invalidate = useInvalidateRaffles()
  const key = raffleKeys.detail(eventId)
  return useMutation({
    mutationFn: (changes: RaffleChanges) => {
      const raffle = queryClient.getQueryData<RaffleResponse>(key)
      if (!raffle) throw new Error('El sorteo todavía no se leyó.')
      return api.updateRaffle(eventId, toUpdateRaffleRequest(raffle, changes))
    },
    onMutate: async (changes) => {
      await queryClient.cancelQueries({ queryKey: key })
      queryClient.setQueryData<RaffleResponse>(key, (raffle) =>
        raffle ? { ...raffle, ...toUpdateRaffleRequest(raffle, changes) } : raffle,
      )
    },
    onSettled: invalidate,
  })
}

export function useCreateRaffle() {
  const invalidate = useInvalidateRaffles()
  return useMutation({
    mutationFn: (eventId: string) => api.createRaffle(eventId),
    onSuccess: invalidate,
  })
}

export function useDeleteRaffle() {
  const invalidate = useInvalidateRaffles()
  return useMutation({
    mutationFn: (eventId: string) => api.deleteRaffle(eventId),
    onSettled: invalidate,
  })
}

export function useSaveRaffleRound(eventId: string) {
  const invalidate = useInvalidateRaffles()
  return useMutation({
    mutationFn: ({ roundId, body }: { roundId?: string; body: RaffleRoundRequest }) =>
      roundId
        ? api.updateRaffleRound(eventId, roundId, body)
        : api.createRaffleRound(eventId, body).then(() => undefined),
    onSettled: invalidate,
  })
}

export function useDeleteRaffleRound(eventId: string) {
  const invalidate = useInvalidateRaffles()
  return useMutation({
    mutationFn: (roundId: string) => api.deleteRaffleRound(eventId, roundId),
    onSettled: invalidate,
  })
}

export function useCreatePrizes(eventId: string) {
  const invalidate = useInvalidateRaffles()
  return useMutation({
    mutationFn: (body: CreatePrizesRequest) => api.createPrizes(eventId, body),
    onSettled: invalidate,
  })
}

/**
 * El listado de premios de un archivo. Crea antes las rondas que falten (el servicio numera la
 * siguiente como la mayor + 1) y después los premios en el orden del archivo. Si algo falla a mitad,
 * lo ya creado queda y la lista se relee.
 */
export function useImportPrizes(eventId: string) {
  const invalidate = useInvalidateRaffles()
  return useMutation({
    mutationFn: async ({
      rounds,
      prizes,
    }: {
      rounds: Pick<RaffleRoundResponse, 'id' | 'number'>[]
      prizes: PremioImportado[]
    }) => {
      try {
        // Una petición para todo el archivo: 10.000 premios no pueden ser 10.000 llamadas.
        const r = await api.importPrizes(eventId, {
          prizes: prizes.map((p) => ({
            roundNumber: p.ronda,
            name: p.nombre,
            value: p.valor,
            quantity: p.cantidad,
            sponsorName: p.patrocinador,
          })),
        })
        return r.created
      } catch (err) {
        // shortcut: servicio sin `prizes/import` todavía (A30); se quita cuando A lo publique.
        if (!(err instanceof ApiError) || (err.status !== 404 && err.status !== 405)) throw err
        return importarPremioPorPremio(eventId, rounds, prizes)
      }
    },
    onSettled: invalidate,
  })
}

async function importarPremioPorPremio(
  eventId: string,
  rounds: Pick<RaffleRoundResponse, 'id' | 'number'>[],
  prizes: PremioImportado[],
): Promise<number> {
  const roundIds = new Map(rounds.map((r) => [r.number, r.id]))
  const needed = Math.max(0, ...prizes.map((p) => p.ronda))
  for (let next = Math.max(0, ...rounds.map((r) => r.number)) + 1; next <= needed; next++) {
    roundIds.set(
      next,
      await api.createRaffleRound(eventId, { name: `Ronda ${next}`, description: null }),
    )
  }
  let created = 0
  for (const p of prizes) {
    // `createPrizes` acepta hasta 50 por llamada: una línea de 100 son dos llamadas.
    for (let resto = p.cantidad; resto > 0; resto -= 50) {
      const quantity = Math.min(50, resto)
      // Una ronda con número menor que no existe (se borró): va a la primera que sí.
      await api.createPrizes(eventId, {
        roundId: roundIds.get(p.ronda) ?? roundIds.values().next().value!,
        name: p.nombre,
        description: null,
        value: p.valor,
        quantity,
        sponsorName: p.patrocinador,
      })
      created += quantity
    }
  }
  return created
}

/** La foto que se elige para un premio: un archivo o ninguna (quitarla). */
export type FotoElegida = { file: File } | null

/** Pone o quita la foto de varios premios a la vez: los iguales de un grupo. */
export function usePrizeImage(eventId: string) {
  const invalidate = useInvalidateRaffles()
  return useMutation({
    mutationFn: async ({ prizeIds, foto }: { prizeIds: string[]; foto: FotoElegida }) => {
      if (foto === null) await api.removePrizeImage(eventId, prizeIds)
      else await api.setPrizeImage(eventId, prizeIds, foto.file)
    },
    onSettled: invalidate,
  })
}

export function useDeletePrize(eventId: string) {
  const invalidate = useInvalidateRaffles()
  return useMutation({
    mutationFn: (prizeId: string) => api.deletePrize(eventId, prizeId),
    onSettled: invalidate,
  })
}

/** Congela el bombo. Lo que devuelve es el sorteo ya en curso. */
export function useStartRaffle(eventId: string) {
  const invalidate = useInvalidateRaffles()
  return useMutation({
    mutationFn: () => api.startRaffle(eventId),
    onSettled: invalidate,
  })
}

/** Deshace un `start` apurado: solo mientras no hay ningún sorteo de premio. */
/** Reactivar a un anulado: vuelve al bombo de los premios siguientes. */
export function useReinstateRaffleAttendee(eventId: string) {
  const invalidate = useInvalidateRaffles()
  return useMutation({
    mutationFn: (attendeeId: string) => api.reinstateRaffleAttendee(eventId, attendeeId),
    onSettled: invalidate,
  })
}

export function useReopenRaffle(eventId: string) {
  const invalidate = useInvalidateRaffles()
  return useMutation({
    mutationFn: () => api.reopenRaffle(eventId),
    onSettled: invalidate,
  })
}

/** Cierra el sorteo: genera el acta y avisa a los ganadores. Lo que devuelve es el sorteo completado. */
export function useCompleteRaffle(eventId: string) {
  const invalidate = useInvalidateRaffles()
  return useMutation({
    mutationFn: () => api.completeRaffle(eventId),
    onSettled: invalidate,
  })
}

/** Corrige las autoridades de un sorteo completado: una versión más del acta. */
export function useReissueRaffle(eventId: string) {
  const invalidate = useInvalidateRaffles()
  return useMutation({
    mutationFn: (body: ReissueRaffleRequest) => api.reissueRaffle(eventId, body),
    onSettled: invalidate,
  })
}

/** Los campos visibles del ganador (§10); el servicio los acepta solo en borrador. */
export function useSetRaffleDisplayFields(eventId: string) {
  const invalidate = useInvalidateRaffles()
  return useMutation({
    mutationFn: (body: RaffleDisplayFields) => api.setRaffleDisplayFields(eventId, body),
    onSettled: invalidate,
  })
}

/** Los firmantes que el certificado de premio propone; en cualquier estado del sorteo. */
export function useSetCertificateSigners(eventId: string) {
  const invalidate = useInvalidateRaffles()
  return useMutation({
    mutationFn: (body: SetCertificateSignersRequest) => api.setCertificateSigners(eventId, body),
    onSettled: invalidate,
  })
}

/** Mover a alguien de lista es escribir un ajuste; también cambia si el sorteo puede arrancar. */
export function useSetAttendeeOverride(eventId: string) {
  const invalidate = useInvalidateRaffles()
  return useMutation({
    mutationFn: (body: SetAttendeeOverrideRequest) => api.setAttendeeOverride(eventId, body),
    onSettled: invalidate,
  })
}

/** Excluir en lote cambia la lista, el bombo y si el sorteo puede arrancar: se relee todo el sorteo. */
export function useBulkExcludeAttendees(eventId: string) {
  const invalidate = useInvalidateRaffles()
  return useMutation({
    mutationFn: (body: BulkExcludeAttendeesRequest) => api.bulkExcludeAttendees(eventId, body),
    onSettled: invalidate,
  })
}

export function useRemoveAttendeeOverride(eventId: string) {
  const invalidate = useInvalidateRaffles()
  return useMutation({
    mutationFn: (email: string) => api.removeAttendeeOverride(eventId, email),
    onSettled: invalidate,
  })
}

/**
 * Sortear y anular devuelven el premio entero: entra en la caché en lugar del viejo, así la
 * pantalla lo ve ya mismo, antes de la relectura.
 */
function useReplacePrize(eventId: string) {
  const queryClient = useQueryClient()
  return (prize: PrizeResponse) =>
    queryClient.setQueryData<RaffleResponse>(raffleKeys.detail(eventId), (raffle) =>
      raffle
        ? {
            ...raffle,
            rounds: raffle.rounds.map((r) => ({
              ...r,
              prizes: r.prizes.map((p) => (p.id === prize.id ? prize : p)),
            })),
          }
        : raffle,
    )
}

/**
 * Relee el sorteo y devuelve el premio como quedó. Tras una respuesta perdida es lo que dice si el
 * servicio escribió; sin respuesta tampoco ahora, `undefined`.
 */
export function useReadPrize(eventId: string) {
  const queryClient = useQueryClient()
  return async (prizeId: string): Promise<PrizeResponse | undefined> => {
    try {
      const raffle = await queryClient.fetchQuery({
        queryKey: raffleKeys.detail(eventId),
        queryFn: () => api.getRaffle(eventId),
        staleTime: 0,
      })
      return prizesOf(raffle).find((p) => p.id === prizeId)
    } catch {
      return undefined
    }
  }
}

/**
 * Sortear y anular nunca se reintentan solos (spec raffles/02 §7.1): un sorteo reintentado que
 * llegue después de una anulación sortearía otra vez sin que nadie lo pida. Pase lo que pase se
 * relee el sorteo: tras una respuesta perdida, la relectura es la que dice qué pasó.
 */
export function useDrawPrize(eventId: string) {
  const replacePrize = useReplacePrize(eventId)
  const invalidate = useInvalidateRaffles()
  return useMutation({
    mutationFn: (prizeId: string) => api.drawPrize(eventId, prizeId),
    retry: false,
    onSuccess: replacePrize,
    // Sin esperar la relectura: la ruleta arranca en cuanto llega el ganador
    onSettled: () => void invalidate(),
  })
}

/**
 * Un lote del tablero: sortea los premios uno tras otro (cada uno lo elige el servicio) y relee el
 * sorteo **una vez** al final, no una por premio: con 10.000 premios esa lectura pesa megas.
 * Nunca se reintenta solo; si uno falla, el lote se corta ahí y devuelve lo que salió y el error.
 */
export function useDrawPrizes(eventId: string) {
  const replacePrize = useReplacePrize(eventId)
  const invalidate = useInvalidateRaffles()
  return async (prizeIds: string[]): Promise<{ drawn: PrizeResponse[]; error: unknown }> => {
    // Un lote es una petición y una transacción (A33). Un 409 por bombo agotado trae los que sí
    // salieron; un servicio sin `draw-batch` cae al sorteo premio por premio.
    if (prizeIds.length > 1) {
      try {
        const lote = await api.drawPrizeBatch(eventId, prizeIds)
        lote.forEach(replacePrize)
        void invalidate()
        return { drawn: lote, error: null }
      } catch (err) {
        if (!(err instanceof ApiError) || (err.status !== 404 && err.status !== 405)) {
          const parcial =
            err instanceof ApiError && Array.isArray(err.extensions.drawn)
              ? (err.extensions.drawn as PrizeResponse[])
              : []
          parcial.forEach(replacePrize)
          void invalidate()
          return { drawn: parcial, error: err }
        }
      }
    }
    const drawn: PrizeResponse[] = []
    let error: unknown = null
    for (const id of prizeIds) {
      try {
        const prize = await api.drawPrize(eventId, id)
        replacePrize(prize)
        drawn.push(prize)
      } catch (err) {
        error = err
        break
      }
    }
    void invalidate()
    return { drawn, error }
  }
}

export function useVoidPrizeDraw(eventId: string) {
  const replacePrize = useReplacePrize(eventId)
  const invalidate = useInvalidateRaffles()
  return useMutation({
    mutationFn: ({ prizeId, body }: { prizeId: string; body: VoidPrizeDrawRequest }) =>
      api.voidPrizeDraw(eventId, prizeId, body),
    retry: false,
    onSuccess: replacePrize,
    onSettled: invalidate,
  })
}

/** Entregar y revertir devuelven el premio entero, como sortear y anular. */
export function useHandOverPrize(eventId: string) {
  const replacePrize = useReplacePrize(eventId)
  const invalidate = useInvalidateRaffles()
  return useMutation({
    mutationFn: ({ prizeId, body }: { prizeId: string; body: HandOverPrizeRequest }) =>
      api.handOverPrize(eventId, prizeId, body),
    onSuccess: replacePrize,
    onSettled: invalidate,
  })
}

export function useReversePrizeHandover(eventId: string) {
  const replacePrize = useReplacePrize(eventId)
  const invalidate = useInvalidateRaffles()
  return useMutation({
    mutationFn: ({ prizeId, body }: { prizeId: string; body: ReversePrizeHandoverRequest }) =>
      api.reversePrizeHandover(eventId, prizeId, body),
    onSuccess: replacePrize,
    onSettled: invalidate,
  })
}

export function useRaffleParticipants(eventId: string) {
  return useQuery({
    queryKey: raffleKeys.participants(eventId),
    queryFn: () => api.listRaffleParticipants(eventId),
    enabled: Boolean(eventId),
  })
}

export function useImportRaffleParticipants(eventId: string) {
  const invalidate = useInvalidateRaffles()
  return useMutation({
    mutationFn: (body: ImportRaffleParticipantsRequest) =>
      api.importRaffleParticipants(eventId, body),
    onSettled: invalidate,
  })
}

export function useSetRaffleParticipantCheckIn(eventId: string) {
  const invalidate = useInvalidateRaffles()
  return useMutation({
    mutationFn: ({
      registrationIdOrCode,
      checkedIn,
    }: {
      registrationIdOrCode: string
      checkedIn: boolean
    }) => api.setRaffleParticipantCheckIn(eventId, registrationIdOrCode, checkedIn),
    onSettled: invalidate,
  })
}

export function useBulkRaffleCheckIn(eventId: string) {
  const invalidate = useInvalidateRaffles()
  return useMutation({
    mutationFn: (checkedIn: boolean) => api.bulkRaffleCheckIn(eventId, checkedIn),
    onSettled: invalidate,
  })
}

export function useRemoveRaffleParticipant(eventId: string) {
  const invalidate = useInvalidateRaffles()
  return useMutation({
    mutationFn: (email: string) => api.removeRaffleParticipant(eventId, email),
    onSettled: invalidate,
  })
}
