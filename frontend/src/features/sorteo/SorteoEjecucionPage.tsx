import { useEffect, useState } from 'react'
import { useParams, useNavigate, Link } from 'react-router-dom'
import {
  ArrowLeft,
  Maximize2,
  Minimize2,
  Trophy,
  Play,
  Check,
  Mic,
  MonitorUp,
  Image as ImageIcon,
} from 'lucide-react'
import type {
  EligibleParticipantResponse,
  PrizeDrawSummary,
  PrizeDrawVoidCode,
  PrizeResponse,
} from '@/api/types'
import { useCan } from '@/auth/AuthProvider'
import { ROLES } from '@/auth/roles'
import { Button } from '@/components/ui/Button'
import { ErrorNote, Spinner } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { Field, TextInput } from '@/components/ui/Field'
import { apiErrorCodeLabel } from '@/lib/labels'
import { useDrawPrize, usePrizeEligible, useRaffle, useReadPrize, useVoidPrizeDraw } from './hooks'
import { toast } from 'sonner'
import {
  alreadyDrawnWinner,
  isLostResponse,
  liveDraw,
  poolExhaustedCounts,
  prizesOf,
} from './raffleView'
import { RuletaAnimacion } from './components/RuletaAnimacion'
import { GanadorRevealCard } from './components/GanadorRevealCard'
import { camposDe, ganadorVisible } from './camposVisibles'

import { VoidDrawModal } from './components/VoidDrawModal'
import { fireConfetti } from './confetti'
import {
  abrirCanalProyector,
  rutaProyector,
  useProyectorConectado,
  type MensajeProyector,
} from './proyeccion'
import { usaTablero } from './tablero'
import { useEstilosClasicos } from '@/features/admin/hooks'
import { TableroConsola } from './components/TableroConsola'
import { useBranding } from '@/lib/branding'
import { cssUrl } from '@/lib/safeUrl'

/** Cuántos nombres trae la ruleta del bombo (R14): la ruleta clásica dibuja 36. */
const MUESTRA_RULETA = 36

// Presets de fondos de celebración corporativa y gala
const BACKGROUND_PRESETS = [
  {
    id: 'solido-navy',
    nombre: 'Azul Corporativo Sólido (Por defecto)',
    url: '',
  },
  {
    id: 'gala-dorada',
    nombre: 'Gala & Celebración Nocturna',
    url: 'https://images.unsplash.com/photo-1519671482749-fd09be7ccebf?auto=format&fit=crop&w=1920&q=80',
  },
  {
    id: 'navidad-luces',
    nombre: 'Fiesta de Navidad & Fin de Año',
    url: 'https://images.unsplash.com/photo-1512389142860-9c449e58a543?auto=format&fit=crop&w=1920&q=80',
  },
  {
    id: 'escenario-eventos',
    nombre: 'Escenario Corporativo Moderno',
    url: 'https://images.unsplash.com/photo-1540575467063-178a50c2df87?auto=format&fit=crop&w=1920&q=80',
  },
  {
    id: 'salon-hotel',
    nombre: 'Salón de Eventos Elegante',
    url: 'https://images.unsplash.com/photo-1519167758481-83f550bb49b3?auto=format&fit=crop&w=1920&q=80',
  },
]

/**
 * Un turno del sorteo: el servicio ya eligió, la animación va hacia ese ganador y después lo
 * muestra. Guarda el premio y el bombo de cuando se pulsó, porque el sorteo releído ya pasa al
 * premio siguiente.
 */
type Turn =
  | {
      phase: 'spinning'
      prize: PrizeResponse
      winner: PrizeDrawSummary
      /** El ganador como uno más del bombo: lo que busca la animación. */
      target: EligibleParticipantResponse
      pool: EligibleParticipantResponse[]
    }
  | { phase: 'revealed'; prize: PrizeResponse; winner: PrizeDrawSummary }

function asParticipant(draw: PrizeDrawSummary): EligibleParticipantResponse {
  return {
    attendeeId: draw.winnerAttendeeId,
    displayName: draw.winnerName,
    company: draw.winnerCompany,
  }
}

/**
 * La pantalla del sorteo en vivo. Girar pide el sorteo al servicio, y la animación va hacia el
 * ganador que él devuelve: aquí no se elige a nadie (spec raffles/02 §2). Sortear nunca se
 * reintenta solo; tras una respuesta perdida o un 409 se relee el sorteo y se decide por el código.
 */
export function SorteoEjecucionPage() {
  const { eventId = '' } = useParams<{ eventId: string }>()
  const estilos = useEstilosClasicos()
  // El tablero de ganadores es otra consola: sortea por lotes y no usa la ruleta. Con la ruleta y
  // los nombres pasando apagados en Administración, es la única, en cualquier equipo.
  const clasicos = estilos.ruleta || estilos.nombres
  return usaTablero(eventId) || !clasicos ? (
    <TableroConsola eventId={eventId} />
  ) : (
    <SorteoEjecucionClasica />
  )
}

function SorteoEjecucionClasica() {
  const { eventId = '' } = useParams<{ eventId: string }>()
  const navigate = useNavigate()
  const branding = useBranding(eventId)
  const { data: sorteo, isLoading } = useRaffle(eventId)
  const canDraw = useCan(ROLES.rafflesDraw)
  const drawPrize = useDrawPrize(eventId)
  const readPrize = useReadPrize(eventId)
  const voidDraw = useVoidPrizeDraw(eventId)

  const [isFullscreen, setIsFullscreen] = useState(false)
  const [turn, setTurn] = useState<Turn | null>(null)
  /** Los premios que el operador dejó sin sortear en esta pantalla, porque no quedaba nadie. */
  const [skippedPrizeIds, setSkippedPrizeIds] = useState<string[]>([])
  const [drawError, setDrawError] = useState<unknown>(null)
  /** Releyendo tras una respuesta perdida: no se vuelve a pulsar hasta saber qué pasó. */
  const [recovering, setRecovering] = useState(false)
  const [voiding, setVoiding] = useState<{ code?: PrizeDrawVoidCode } | null>(null)
  /** El premio que se vuelve a sortear solo, porque su ganador no apareció en 60 segundos. */
  const [reSortear, setReSortear] = useState<string | null>(null)

  /**
   * Se acabaron los 60 segundos sin que el ganador apareciera: se anula por ausente sin preguntar.
   * El premio vuelve a quedar disponible y, como todo anulado, la persona sale de los premios
   * siguientes hasta que un administrador la reactive desde el detalle del sorteo.
   */
  const anularPorAusencia = () => {
    if (turn?.phase !== 'revealed' || voidDraw.isPending) return
    const { prize, winner } = turn
    voidDraw.mutate(
      {
        prizeId: prize.id,
        body: { code: 'absent', note: 'No confirmó su presencia en los 60 segundos.' },
      },
      {
        onSuccess: () => {
          setTurn(null)
          setDrawError(null)
          // Re-sorteo instantáneo: la ruleta vuelve a girar sola para el mismo premio.
          setReSortear(prize.id)
          toast.warning(`${winner.winnerName} no se presentó a tiempo.`, {
            description: `Se vuelve a sortear ${prize.name}. Se puede reactivar a la persona desde el detalle del sorteo.`,
          })
        },
        // La tarjeta sigue abierta: el operador puede anular a mano con "No está presente".
        onError: () =>
          toast.error('No se pudo anular automáticamente.', {
            description: 'Usa «No está presente» para anularlo a mano.',
          }),
      },
    )
  }

  // Configuración de fondo con persistencia
  const [bgUrl, setBgUrl] = useState<string>(() => {
    return localStorage.getItem('sorteo360_bg') || BACKGROUND_PRESETS[0].url
  })
  const [showBgModal, setShowBgModal] = useState(false)
  const [customBgInput, setCustomBgInput] = useState('')

  const handleSelectBg = (url: string) => {
    setBgUrl(url)
    localStorage.setItem('sorteo360_bg', url)
  }

  // El primer premio pendiente de sorteo, salvo los que se dejaron sin sortear
  const premios = sorteo ? prizesOf(sorteo) : []
  const undrawnPrizes = premios.filter(
    (p) => p.state === 'undrawn' && !skippedPrizeIds.includes(p.id),
  )
  // Mientras el servicio sortea, el premio sigue en pantalla aunque la caché ya lo dé por sorteado
  const prizeInPlay = drawPrize.isPending
    ? premios.find((p) => p.id === drawPrize.variables)
    : undefined
  const premioActivo = turn?.prize ?? prizeInPlay ?? undrawnPrizes[0] ?? null
  const inProgress = sorteo?.state === 'in_progress'
  const eligible = usePrizeEligible(
    eventId,
    inProgress && !turn ? premioActivo?.id : undefined,
    drawPrize.isPending,
    // La ruleta pinta a lo sumo 36 nombres; el ganador lo pone el servicio aparte.
    MUESTRA_RULETA,
  )
  const eligibleCount = eligible.data?.count

  const spinTo = (drawn: PrizeResponse, pool: EligibleParticipantResponse[]) => {
    const winner = liveDraw(drawn)
    if (!winner) return
    setTurn({ phase: 'spinning', prize: drawn, winner, target: asParticipant(winner), pool })
  }

  const handleDraw = () => {
    if (!premioActivo || turn) return
    const prize = premioActivo
    const pool = eligible.data?.participants ?? []
    setDrawError(null)
    drawPrize.mutate(prize.id, {
      onSuccess: (drawn) => spinTo(drawn, pool),
      onError: async (error) => {
        // Una respuesta perdida no se reintenta: la relectura dice si el servicio sorteó
        if (isLostResponse(error)) {
          setRecovering(true)
          const reread = await readPrize(prize.id)
          setRecovering(false)
          if (reread && liveDraw(reread)) {
            spinTo(reread, pool)
            return
          }
        }
        setDrawError(error)
      },
    })
  }

  // Gira en cuanto el bombo del premio está releído sin el ausente (la anulación invalida la lectura).
  useEffect(() => {
    if (!reSortear || turn || drawPrize.isPending) return
    if (premioActivo?.id !== reSortear) {
      // El premio ya no es el siguiente (otra pantalla lo sorteó): no se fuerza nada.
      setReSortear(null)
      return
    }
    if (!eligible.data || eligible.isFetching) return
    setReSortear(null)
    handleDraw()
    // handleDraw cambia en cada render; lo que dispara el giro es el bombo listo.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [reSortear, turn, premioActivo?.id, eligible.data, eligible.isFetching, drawPrize.isPending])

  // El proyector: un canal entre ventanas de esta laptop. Se abre una vez por sorteo. Con él
  // abierto, esta pantalla es el control remoto: la ruleta grande la ve el público allá.
  const control = useProyectorConectado(eventId)
  const [canalProyector, setCanalProyector] = useState<BroadcastChannel | null>(null)
  useEffect(() => {
    const canal = abrirCanalProyector(eventId)
    setCanalProyector(canal)
    return () => canal?.close()
  }, [eventId])

  const mensajeProyector: MensajeProyector | null = !sorteo
    ? null
    : turn?.phase === 'spinning'
      ? {
          tipo: 'girando',
          premio: turn.prize,
          ganador: turn.target,
          bombo: turn.pool,
          mostrarPremio: sorteo.showPrizeOnScreen,
          modo: sorteo.visualStyle,
          duracionSec: sorteo.animationSeconds,
        }
      : turn?.phase === 'revealed'
        ? {
            tipo: 'ganador',
            premio: turn.prize,
            ganador: turn.winner,
            visible: ganadorVisible(turn.winner, camposDe(sorteo)),
          }
        : { tipo: 'espera', premio: premioActivo, mostrarPremio: sorteo.showPrizeOnScreen }
  // Se compara por contenido: un objeto nuevo en cada render no debe volver a girar el proyector.
  const claveProyector = mensajeProyector
    ? `${mensajeProyector.tipo}:${'premio' in mensajeProyector ? (mensajeProyector.premio?.id ?? '') : ''}`
    : ''
  useEffect(() => {
    if (canalProyector && mensajeProyector) canalProyector.postMessage(mensajeProyector)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [canalProyector, claveProyector])
  // Un proyector que se abre tarde pide el estado actual.
  useEffect(() => {
    if (!canalProyector) return
    canalProyector.onmessage = (e: MessageEvent<{ tipo?: string }>) => {
      if (e.data?.tipo === 'hola' && mensajeProyector) canalProyector.postMessage(mensajeProyector)
    }
  })

  const handleNext = () => {
    setTurn(null)
    setDrawError(null)
  }

  const toggleFullscreen = () => {
    if (!document.fullscreenElement) {
      document.documentElement
        .requestFullscreen()
        .then(() => setIsFullscreen(true))
        .catch(() => {})
    } else {
      document
        .exitFullscreen()
        .then(() => setIsFullscreen(false))
        .catch(() => {})
    }
  }

  if (isLoading) {
    return (
      <div className="min-h-screen bg-slate-900 text-white flex items-center justify-center">
        <Spinner />
      </div>
    )
  }

  if (!sorteo) {
    return (
      <div className="min-h-screen bg-slate-900 text-white flex flex-col items-center justify-center p-4">
        <p className="text-lg font-bold">Sorteo no encontrado</p>
        <Button
          onClick={() => navigate('/sorteo/sorteos')}
          className="mt-4 bg-blue-600 hover:bg-blue-700 text-white font-bold"
        >
          Volver a Sorteos
        </Button>
      </div>
    )
  }

  const sorteoTerminado = premios.length > 0 && !premioActivo
  const undrawnCount = premios.filter((p) => p.state === 'undrawn').length
  const previousWinner = alreadyDrawnWinner(drawError)
  const poolCounts = poolExhaustedCounts(drawError)

  const drawDisabled =
    !canDraw.allowed ||
    !inProgress ||
    drawPrize.isPending ||
    recovering ||
    turn !== null ||
    eligibleCount === 0
  const drawTitle = !canDraw.allowed
    ? canDraw.title
    : sorteo.state === 'draft'
      ? apiErrorCodeLabel.RAFFLE_NOT_IN_PROGRESS
      : sorteo.state === 'completed'
        ? apiErrorCodeLabel.RAFFLE_COMPLETED
        : eligibleCount === 0
          ? apiErrorCodeLabel.RAFFLE_POOL_EXHAUSTED
          : undefined

  return (
    <div className="fixed inset-0 z-50 bg-brand-deep text-white overflow-hidden flex flex-col justify-between select-none p-4 md:p-6">
      {/* Fondo de pantalla de Playa / Hotel RD con Overlay Azul Institucional Claro */}
      {bgUrl && (
        <div
          className="fixed inset-0 pointer-events-none z-0 bg-cover bg-center bg-no-repeat transition-all duration-700"
          style={{ backgroundImage: cssUrl(bgUrl) }}
        />
      )}
      {/* Overlay Azul Institucional más claro y vibrante */}
      <div className="fixed inset-0 pointer-events-none z-0 bg-blue-950/65 backdrop-brightness-95" />

      {/* Top Header Bar */}
      <header className="relative z-20 flex items-center justify-between w-full">
        <div className="flex items-center gap-3">
          <Link to={`/sorteo/sorteos/${eventId}`}>
            <Button
              variant="outline"
              size="sm"
              className="text-xs text-white border-blue-400/30 bg-brand-deep/80 hover:bg-brand-deep backdrop-blur-md cursor-pointer"
            >
              <ArrowLeft className="w-3.5 h-3.5 mr-1" />
              Salir
            </Button>
          </Link>
          <div className="hidden sm:flex items-center gap-2 rounded-xl border border-white/15 bg-white/10 px-3 py-1.5 backdrop-blur-md">
            <img
              src={branding.logoDarkUrl}
              alt={branding.companyName}
              className="h-5 w-auto max-w-[100px] object-contain"
            />
            {branding.hasCustomEventBranding && (
              <span className="text-xs font-bold text-white/90">{branding.companyName}</span>
            )}
          </div>
        </div>

        {/* Botones de Control: Fondo y Pantalla Completa */}
        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={() =>
              window.open(rutaProyector(eventId), 'sorteo360-proyector', 'width=1366,height=768')
            }
            className="p-2 text-blue-200 hover:text-white rounded-lg bg-brand-deep/80 hover:bg-brand-deep border border-blue-400/30 backdrop-blur-md transition-colors cursor-pointer flex items-center gap-1.5 text-xs font-bold px-3"
            title="Abre la pantalla del público en otra ventana; esta queda como control remoto"
          >
            <MonitorUp className="w-4 h-4" />
            <span className="hidden sm:inline">{control ? 'Traer proyector' : 'Proyector'}</span>
          </button>
          <button
            type="button"
            onClick={() => setShowBgModal(true)}
            className="p-2 text-blue-200 hover:text-white rounded-lg bg-brand-deep/80 hover:bg-brand-deep border border-blue-400/30 backdrop-blur-md transition-colors cursor-pointer flex items-center gap-1.5 text-xs font-bold px-3"
            title="Cambiar fondo del sorteo"
          >
            <ImageIcon className="w-4 h-4" />
            <span className="hidden sm:inline">Fondo</span>
          </button>

          <button
            type="button"
            onClick={toggleFullscreen}
            className="p-2 text-blue-200 hover:text-white rounded-lg bg-brand-deep/80 hover:bg-brand-deep border border-blue-400/30 backdrop-blur-md transition-colors cursor-pointer"
            title={isFullscreen ? 'Salir de pantalla completa' : 'Pantalla completa'}
          >
            {isFullscreen ? <Minimize2 className="w-4 h-4" /> : <Maximize2 className="w-4 h-4" />}
          </button>
        </div>
      </header>

      {/* Main Arena centrada verticalmente */}
      <main className="relative z-10 flex-1 flex flex-col items-center justify-center w-full max-w-6xl mx-auto my-auto overflow-y-auto py-2">
        {turn?.phase === 'revealed' ? (
          <GanadorRevealCard
            ganador={turn.winner}
            campos={camposDe(sorteo)}
            premio={turn.prize}
            requiereConfirmarPresencia={sorteo.requiresPresenceConfirmation}
            onTiempoAgotado={canDraw.allowed ? anularPorAusencia : undefined}
            onPresenciaConfirmada={() =>
              canalProyector?.postMessage({ tipo: 'presencia', confirmada: true })
            }
            onSiguientePremio={handleNext}
            onFinalizar={() => navigate(`/sorteo/sorteos/${eventId}?tab=ganadores`)}
            onVoid={(code) => setVoiding({ code })}
            voidTitle={canDraw.allowed ? undefined : canDraw.title}
            esUltimoPremio={undrawnPrizes.length === 0}
          />
        ) : sorteoTerminado ? (
          /* Sorteo Completado Plano */
          <div className="text-center py-8 space-y-5 max-w-xl mx-auto bg-brand-deep/90 border border-blue-400/30 rounded-2xl p-8 backdrop-blur-sm">
            <div className="w-16 h-16 mx-auto rounded-2xl bg-blue-600 flex items-center justify-center text-white font-black border border-white">
              <Trophy className="w-8 h-8" />
            </div>

            <div className="space-y-1.5">
              {undrawnCount === 0 ? (
                <>
                  <h2 className="text-2xl md:text-3xl font-black text-white">
                    ¡Todos los Premios han sido Sorteados!
                  </h2>
                  <p className="text-blue-200 text-xs sm:text-sm">
                    Se han completado los premios de este sorteo. Los ganadores quedan registrados
                    oficialmente.
                  </p>
                </>
              ) : (
                <>
                  <h2 className="text-2xl md:text-3xl font-black text-white">
                    {undrawnCount === 1
                      ? 'Queda 1 premio sin sortear'
                      : `Quedan ${undrawnCount} premios sin sortear`}
                  </h2>
                  <p className="text-blue-200 text-xs sm:text-sm">
                    Se dejaron sin sortear porque no quedaban participantes elegibles. Al volver a
                    abrir esta pantalla se pueden intentar de nuevo.
                  </p>
                </>
              )}
            </div>

            <div className="flex items-center justify-center gap-3 pt-2">
              <Link to="/sorteo/historial">
                <Button className="bg-blue-600 hover:bg-blue-700 text-white font-bold px-4 py-2 text-xs sm:text-sm cursor-pointer">
                  <Trophy className="w-4 h-4 mr-1.5" />
                  Ver Historial
                </Button>
              </Link>
              <Link to={`/sorteo/sorteos/${eventId}`}>
                <Button
                  variant="outline"
                  className="text-white border-blue-400/40 bg-slate-800 hover:bg-slate-700 text-xs sm:text-sm"
                >
                  Volver al Detalle
                </Button>
              </Link>
            </div>
          </div>
        ) : (
          /* Estado de Girar Ruleta */
          <div className="w-full flex-1 flex flex-col items-center justify-between py-2 max-h-full">
            {/* Cabecera del Sorteo: Título del Premio / Anuncio por Presentador (Sin badge de ronda) */}
            <div className="w-full text-center px-0 pt-0 pb-1 -mt-2 space-y-1 max-w-285 mx-auto shrink-0 group relative">
              {/* Título del Premio o Indicador de Presentador */}
              {control && (
                <p className="mx-auto mb-2 inline-flex items-center gap-2 rounded-full border border-emerald-300/40 bg-emerald-500/20 px-3 py-1 text-xs font-bold text-emerald-100">
                  <MonitorUp className="h-3.5 w-3.5" />
                  Control remoto: el público ve el sorteo en la ventana del proyector
                </p>
              )}
              {sorteo.showPrizeOnScreen && premioActivo ? (
                <div className="space-y-0.5">
                  <h2
                    className={
                      control
                        ? 'text-xl sm:text-2xl font-black text-white tracking-tight leading-tight line-clamp-2'
                        : 'text-2xl sm:text-4xl md:text-[44px] lg:text-[54px] font-black text-white tracking-tight drop-shadow-lg leading-tight line-clamp-2'
                    }
                  >
                    {premioActivo.name}
                  </h2>
                </div>
              ) : (
                /* Modo Presentador: Premio anunciado verbalmente */
                <div className="space-y-1 py-1">
                  <div className="inline-flex items-center justify-center gap-2 bg-amber-500/20 border border-amber-400/40 px-4 py-2 rounded-2xl backdrop-blur-md">
                    <Mic className="w-5 h-5 text-amber-400 animate-pulse" />
                    <span className="text-base sm:text-lg md:text-xl font-bold text-amber-200">
                      Premio anunciado por el presentador
                    </span>
                  </div>
                </div>
              )}
            </div>

            {/* Ruleta Animada Canvas / Tómbola: Bajada 20px más (mt-10). Como control remoto se
                achica a una vista previa: sigue corriendo porque su final revela al ganador. */}
            <div
              className={
                control
                  ? 'flex flex-col items-center justify-center shrink-0 mt-4 mb-auto [zoom:0.45] opacity-80'
                  : 'flex flex-col items-center justify-center shrink-0 mt-10 mb-auto'
              }
              aria-hidden={control || undefined}
            >
              <RuletaAnimacion
                participantes={
                  turn?.phase === 'spinning' ? turn.pool : (eligible.data?.participants ?? [])
                }
                winner={turn?.phase === 'spinning' ? turn.target : null}
                total={eligibleCount}
                onComplete={() => {
                  if (turn?.phase !== 'spinning') return
                  setTurn({ phase: 'revealed', prize: turn.prize, winner: turn.winner })
                  fireConfetti()
                }}
                running={turn?.phase === 'spinning'}
                modo={sorteo.visualStyle}
                duracionSec={sorteo.animationSeconds}
              />
            </div>

            {/* Botón de Girar Institucional Plano: Subido 20px (-mt-5) */}
            <div className="-mt-5 pb-1">
              {premios.length === 0 ? (
                <p className="text-white text-xs sm:text-sm font-bold bg-brand-deep px-4 py-2 rounded-xl border border-blue-400/30">
                  Este sorteo todavía no tiene premios.
                </p>
              ) : (
                // El conteo y "dejar sin sortear" van a los lados del botón, no debajo: la fila no
                // crece, y el botón no se sube sobre la ruleta en una pantalla baja
                <div className="relative">
                  <Button
                    type="button"
                    size="lg"
                    onClick={handleDraw}
                    disabled={drawDisabled}
                    title={drawTitle}
                    className="px-8 sm:px-12 py-3 sm:py-5 text-lg sm:text-xl font-black tracking-wider uppercase rounded-xl bg-blue-600 text-white border border-white shadow-xl disabled:opacity-60 disabled:cursor-not-allowed"
                  >
                    <Play className="w-5 h-5 sm:w-6 sm:h-6 mr-2 fill-current" />
                    ¡GIRAR RULETA!
                  </Button>
                  {inProgress && !turn && eligibleCount !== undefined && (
                    <p className="absolute left-full top-1/2 ml-4 -translate-y-1/2 whitespace-nowrap text-blue-100 text-xs sm:text-sm font-semibold">
                      {eligibleCount === 1
                        ? '1 participante elegible'
                        : `${eligibleCount} participantes elegibles`}
                    </p>
                  )}
                  {premioActivo && !turn && (eligibleCount === 0 || poolCounts) && (
                    <Button
                      type="button"
                      size="sm"
                      variant="outline"
                      onClick={() => {
                        setSkippedPrizeIds((prev) => [...prev, premioActivo.id])
                        setDrawError(null)
                      }}
                      className="absolute right-full top-1/2 mr-4 -translate-y-1/2 whitespace-nowrap text-xs font-bold text-white border-blue-400/40 bg-brand-deep/80 hover:bg-brand-deep cursor-pointer"
                    >
                      Dejar este premio sin sortear
                    </Button>
                  )}
                </div>
              )}
            </div>
          </div>
        )}
      </main>

      {/* Los avisos van al costado, fuera de la columna: así no empujan el botón bajo la ruleta */}
      {((inProgress && !turn && eligible.error != null) || (drawError != null && premioActivo)) && (
        <div className="fixed right-6 top-1/2 z-30 w-full max-w-sm -translate-y-1/2 space-y-2">
          {inProgress && !turn && eligible.error != null && (
            <div className="rounded-xl bg-white p-3 shadow-xl">
              <ErrorNote error={eligible.error} />
            </div>
          )}
          {drawError != null && premioActivo && (
            <div className="space-y-2 rounded-xl bg-white p-3 text-xs text-slate-700 shadow-xl">
              {previousWinner ? (
                <p>
                  Este premio ya tenía un ganador: <strong>{previousWinner.winnerName}</strong>. Se
                  pasa al siguiente premio.
                </p>
              ) : (
                <ErrorNote error={drawError} />
              )}
              {poolCounts && (
                <p>
                  De {poolCounts.participants} calificados, quedan fuera {poolCounts.excludedByVoid}{' '}
                  por una anulación, {poolCounts.excludedByPerson} por haber ganado ya y{' '}
                  {poolCounts.excludedByCompany} por la empresa de un ganador.
                </p>
              )}
            </div>
          )}
        </div>
      )}

      {voiding && turn?.phase === 'revealed' && (
        <VoidDrawModal
          eventId={eventId}
          prize={turn.prize}
          draw={turn.winner}
          initialCode={voiding.code}
          onVoided={() => {
            // Anular nunca vuelve a sortear: el premio queda a la espera de otro giro
            setVoiding(null)
            setTurn(null)
            setDrawError(null)
          }}
          onClose={() => setVoiding(null)}
        />
      )}

      {/* Modal de Configuración de Fondo */}
      {showBgModal && (
        <Modal title="Configurar Fondo del Sorteo en Vivo" onClose={() => setShowBgModal(false)}>
          <div className="space-y-4 text-slate-900 dark:text-white">
            <p className="text-xs text-slate-500 dark:text-slate-400">
              Selecciona una imagen de fondo de República Dominicana. Se aplicará automáticamente un
              filtro azul institucional para garantizar máxima legibilidad y contraste.
            </p>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 max-h-72 overflow-y-auto pr-1">
              {BACKGROUND_PRESETS.map((preset) => {
                const isSelected = bgUrl === preset.url
                return (
                  <button
                    key={preset.id}
                    type="button"
                    onClick={() => {
                      handleSelectBg(preset.url)
                      setShowBgModal(false)
                    }}
                    className={`relative rounded-xl overflow-hidden border-2 p-2 text-left transition-all cursor-pointer flex flex-col justify-between ${
                      isSelected
                        ? 'border-blue-600 bg-blue-50 dark:bg-blue-950/40'
                        : 'border-slate-200 dark:border-slate-700 hover:border-slate-400'
                    }`}
                  >
                    <div className="h-20 w-full rounded-lg overflow-hidden bg-slate-200 dark:bg-slate-800 mb-2 relative">
                      {preset.url ? (
                        <>
                          <img
                            src={preset.url}
                            alt={preset.nombre}
                            className="w-full h-full object-cover"
                          />
                          <div className="absolute inset-0 bg-slate-900/70" />
                        </>
                      ) : (
                        <div className="w-full h-full bg-slate-900" />
                      )}
                      {isSelected && (
                        <span className="absolute top-1.5 right-1.5 p-1 rounded-full bg-blue-600 text-white">
                          <Check className="w-3 h-3 stroke-3" />
                        </span>
                      )}
                    </div>
                    <span className="text-xs font-bold truncate block">{preset.nombre}</span>
                  </button>
                )
              })}
            </div>

            <div className="pt-2 border-t border-slate-200 dark:border-slate-700 space-y-2">
              <Field label="O pegar URL de imagen personalizada">
                <TextInput
                  value={customBgInput}
                  onChange={(e) => setCustomBgInput(e.target.value)}
                  placeholder="https://ejemplo.com/foto-playa.jpg"
                />
              </Field>
              {customBgInput && (
                <Button
                  size="sm"
                  onClick={() => {
                    handleSelectBg(customBgInput.trim())
                    setShowBgModal(false)
                  }}
                  className="bg-blue-600 hover:bg-blue-700 text-white font-bold cursor-pointer"
                >
                  Aplicar URL
                </Button>
              )}
            </div>

            <div className="flex justify-end pt-3 border-t border-slate-200 dark:border-slate-700">
              <Button variant="outline" onClick={() => setShowBgModal(false)}>
                Cerrar
              </Button>
            </div>
          </div>
        </Modal>
      )}
    </div>
  )
}
