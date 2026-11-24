import { useState, type FormEvent, type ReactNode } from 'react'
import {
  Monitor,
  Maximize2,
  Disc,
  CheckCircle2,
  Sparkles,
  ChevronLeft,
  ChevronRight,
  ListOrdered,
} from 'lucide-react'
import { Button } from '@/components/ui/Button'
import { ErrorNote, Spinner } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { StepperModal, type StepperModalStep } from '@/components/ui/StepperModal'
import type { RaffleResponse, RaffleVisualStyle } from '@/api/types'
import { useCan } from '@/auth/AuthProvider'
import { ROLES } from '@/auth/roles'
import { useRaffle, useRaffleAttendees, useStartRaffle, useUpdateRaffle } from '../hooks'
import type { RaffleChanges } from '../raffleRequest'
import { DoorListStatus } from './DoorListStatus'
import { marcarTablero, usaTablero } from '../tablero'
import { useEstilosClasicos } from '@/features/admin/hooks'

type ModoPantalla = 'misma_pantalla' | 'proyector'

interface IniciarSorteoModalProps {
  eventId: string
  /** Lo que se lee bajo el título: el nombre del evento. */
  subtitulo?: string
  textoBotonAccion: string
  onClose: () => void
  onLaunch: (modoPantalla: ModoPantalla) => void
}

const PASOS: StepperModalStep[] = [
  { number: 1, title: 'Formato visual', subtitle: 'Ruleta, nombres o tablero' },
  { number: 2, title: 'Pantalla', subtitle: 'Esta pestaña o proyector' },
  { number: 3, title: 'Presentación', subtitle: 'Premio, presencia y duración' },
]

/** Los formatos que se ofrecen. La tómbola existe en el contrato pero no se usa aquí. */
type FormatoOfrecido = Extract<RaffleVisualStyle, 'roulette' | 'sequential_names'>

/** Un sorteo guardado con un formato que ya no se ofrece se muestra como ruleta. */
function formatoOfrecido(style: RaffleVisualStyle): FormatoOfrecido {
  return style === 'sequential_names' ? 'sequential_names' : 'roulette'
}

const DURACIONES = [5, 10, 15, 20, 25, 30]

const etiquetaDuracion = (s: number) =>
  s <= 5
    ? 'Rápido'
    : s <= 10
      ? 'Estándar'
      : s <= 15
        ? 'Intermedio'
        : s <= 20
          ? 'Suspenso'
          : s <= 25
            ? 'Dramático'
            : 'Gran final'

export function IniciarSorteoModal({ eventId, onClose, ...props }: IniciarSorteoModalProps) {
  const { data: raffle, error } = useRaffle(eventId)
  if (!raffle) {
    return (
      <Modal title="Lanzar Sorteo en Vivo" onClose={onClose}>
        {error != null ? <ErrorNote error={error} /> : <Spinner />}
      </Modal>
    )
  }
  return <IniciarSorteoForm raffle={raffle} onClose={onClose} {...props} />
}

/**
 * Tres pasos: formato, pantalla y presentación. Los ajustes se guardan juntos, en un solo PATCH
 * al lanzar; cancelar no guarda nada. Sin `raffles:write`, o con el sorteo completado, se muestran
 * sin poder cambiarse. Un borrador además se inicia al lanzar: `start` congela el bombo.
 */
function IniciarSorteoForm({
  raffle,
  subtitulo,
  textoBotonAccion,
  onClose,
  onLaunch,
}: Omit<IniciarSorteoModalProps, 'eventId'> & { raffle: RaffleResponse }) {
  const canWrite = useCan(ROLES.rafflesWrite)
  const updateRaffle = useUpdateRaffle(raffle.eventId)
  const startRaffle = useStartRaffle(raffle.eventId)
  const draft = raffle.state === 'draft'
  const { data: attendees } = useRaffleAttendees(draft ? raffle.eventId : '')
  const editable = canWrite.allowed && raffle.state !== 'completed'

  const estilos = useEstilosClasicos()
  const permitido = (f: FormatoOfrecido) => (f === 'roulette' ? estilos.ruleta : estilos.nombres)
  const [paso, setPaso] = useState(1)
  const [formatoElegido, setFormato] = useState<FormatoOfrecido | 'tablero'>(
    usaTablero(raffle.eventId) ? 'tablero' : formatoOfrecido(raffle.visualStyle),
  )
  // Un formato que Administración apagó no se puede lanzar: queda el tablero.
  const formato =
    formatoElegido !== 'tablero' && !permitido(formatoElegido) ? 'tablero' : formatoElegido
  const [pantalla, setPantalla] = useState<ModoPantalla>('misma_pantalla')
  const [mostrarPremio, setMostrarPremio] = useState(raffle.showPrizeOnScreen)
  const [confirmarPresencia, setConfirmarPresencia] = useState(raffle.requiresPresenceConfirmation)
  const [duracion, setDuracion] = useState(raffle.animationSeconds)

  const pending = updateRaffle.isPending || startRaffle.isPending
  const ultimo = paso === PASOS.length

  const lanzar = () => {
    marcarTablero(raffle.eventId, formato === 'tablero')
    const changes: RaffleChanges = {
      // shortcut: el servicio todavía no conoce 'winner_board' (A33); el tablero se guarda en
      // este navegador y el sorteo queda como nombres pasando.
      visualStyle: formato === 'tablero' ? 'sequential_names' : formato,
      showPrizeOnScreen: mostrarPremio,
      requiresPresenceConfirmation: confirmarPresencia,
      animationSeconds: duracion,
    }
    const changed = (Object.keys(changes) as (keyof RaffleChanges)[]).some(
      (k) => changes[k] !== raffle[k],
    )
    const launch = () =>
      draft
        ? startRaffle.mutate(undefined, { onSuccess: () => onLaunch(pantalla) })
        : onLaunch(pantalla)
    if (!editable || !changed) {
      launch()
      return
    }
    updateRaffle.mutate(changes, { onSuccess: launch })
  }

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault()
    if (ultimo) lanzar()
    else setPaso((p) => p + 1)
  }

  const error = updateRaffle.error ?? startRaffle.error

  return (
    <StepperModal
      title="Lanzar Sorteo en Vivo"
      subtitle={subtitulo ?? ''}
      steps={PASOS}
      currentStep={paso}
      isSubmitting={pending}
      onClose={onClose}
      onStepClick={setPaso}
      onSubmit={handleSubmit}
      footer={
        <>
          <Button type="button" variant="ghost" onClick={onClose} disabled={pending}>
            Cancelar
          </Button>
          {paso > 1 && (
            <Button
              type="button"
              variant="outline"
              onClick={() => setPaso((p) => p - 1)}
              disabled={pending}
            >
              <ChevronLeft className="mr-1 h-4 w-4" /> Atrás
            </Button>
          )}
          <Button
            type="submit"
            disabled={pending}
            className="bg-brand text-white hover:bg-brand/90"
          >
            {ultimo ? (
              <>
                <Sparkles className="mr-1.5 h-4 w-4" /> {textoBotonAccion}
              </>
            ) : (
              <>
                Siguiente <ChevronRight className="ml-1 h-4 w-4" />
              </>
            )}
          </Button>
        </>
      }
    >
      <div className="space-y-5">
        {paso === 1 && (
          <>
            {draft && (
              <DoorListStatus
                state={raffle.state}
                attendees={attendees}
                warnings={raffle.warnings}
              />
            )}
            <Seccion titulo="¿Cómo se ve el sorteo?">
              <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
                {estilos.ruleta && (
                  <Opcion
                    activa={formato === 'roulette'}
                    disabled={!editable}
                    onClick={() => setFormato('roulette')}
                    icono={<Disc className="h-5 w-5" />}
                    titulo="Ruleta"
                    texto="Disco giratorio con los colores y el logo de la marca."
                  />
                )}
                {estilos.nombres && (
                  <Opcion
                    activa={formato === 'sequential_names'}
                    disabled={!editable}
                    onClick={() => setFormato('sequential_names')}
                    icono={<Sparkles className="h-5 w-5" />}
                    titulo="Nombres pasando"
                    texto="Los nombres pasan a alta velocidad hasta detenerse en el ganador."
                  />
                )}
                <Opcion
                  activa={formato === 'tablero'}
                  disabled={!editable}
                  onClick={() => setFormato('tablero')}
                  icono={<ListOrdered className="h-5 w-5" />}
                  titulo="Tablero de ganadores"
                  texto="Premios iguales por lotes: hasta 10 números y nombres por pantalla."
                />
              </div>
              {!estilos.ruleta && !estilos.nombres && (
                <p className="mt-2 text-xs text-slate-500">
                  La ruleta y los nombres pasando están desactivados. Se encienden en Administración
                  › Interruptores de funciones.
                </p>
              )}
            </Seccion>
          </>
        )}

        {paso === 2 && (
          <Seccion titulo="¿Dónde se proyecta?">
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <Opcion
                activa={pantalla === 'misma_pantalla'}
                onClick={() => setPantalla('misma_pantalla')}
                icono={<Monitor className="h-5 w-5" />}
                titulo="Esta pestaña"
                texto="Para pruebas o cuando se proyecta esta misma laptop."
              />
              <Opcion
                activa={pantalla === 'proyector'}
                onClick={() => setPantalla('proyector')}
                icono={<Maximize2 className="h-5 w-5" />}
                titulo="Modo proyector"
                texto="Abre una ventana aparte para arrastrarla a la TV, pantalla LED o proyector."
              />
            </div>
          </Seccion>
        )}

        {paso === 3 && (
          <>
            <Seccion titulo="En pantalla">
              <div className="space-y-3.5 rounded-2xl border border-slate-200 bg-slate-50/50 p-4 dark:border-slate-800 dark:bg-slate-800/30">
                <Casilla
                  checked={mostrarPremio}
                  disabled={!editable}
                  onChange={setMostrarPremio}
                  titulo="Mostrar el nombre del premio"
                  texto={
                    mostrarPremio
                      ? 'El premio se muestra en grande arriba de la animación.'
                      : 'El premio queda oculto y lo anuncia el presentador antes de girar.'
                  }
                />
                <Casilla
                  checked={confirmarPresencia}
                  disabled={!editable}
                  onChange={setConfirmarPresencia}
                  titulo="Confirmar presencia en sala (60 s)"
                  texto="Al salir el ganador corre una cuenta de 60 segundos; si no aparece, se anula y se vuelve a sortear."
                />
              </div>
            </Seccion>

            <Seccion
              titulo={`Duración de la animación: ${duracion} s (${etiquetaDuracion(duracion)})`}
            >
              <div className="grid grid-cols-6 gap-1.5 text-xs">
                {DURACIONES.map((t) => (
                  <button
                    key={t}
                    type="button"
                    disabled={!editable}
                    aria-pressed={duracion === t}
                    onClick={() => setDuracion(t)}
                    className={`cursor-pointer rounded-xl border py-2 text-center font-bold transition-colors disabled:cursor-not-allowed ${
                      duracion === t
                        ? 'border-brand bg-brand text-white'
                        : 'border-slate-200 bg-white text-slate-700 hover:border-brand-accent dark:border-slate-700 dark:bg-slate-800 dark:text-slate-300'
                    }`}
                  >
                    {t}s
                  </button>
                ))}
              </div>
            </Seccion>
          </>
        )}

        {error != null && <ErrorNote error={error} />}
      </div>
    </StepperModal>
  )
}

function Seccion({ titulo, children }: { titulo: string; children: ReactNode }) {
  return (
    <section className="space-y-3">
      <h3 className="text-xs font-black uppercase tracking-wider text-slate-600 dark:text-slate-300">
        {titulo}
      </h3>
      {children}
    </section>
  )
}

function Opcion({
  activa,
  disabled = false,
  onClick,
  icono,
  titulo,
  texto,
}: {
  activa: boolean
  disabled?: boolean
  onClick: () => void
  icono: ReactNode
  titulo: string
  texto: string
}) {
  return (
    <button
      type="button"
      disabled={disabled}
      aria-pressed={activa}
      onClick={onClick}
      className={`flex cursor-pointer items-start gap-3.5 rounded-2xl border p-4 text-left transition-all disabled:cursor-not-allowed ${
        activa
          ? 'border-brand bg-brand/5 shadow-md ring-2 ring-brand/30'
          : 'border-slate-200 bg-white hover:border-slate-300 dark:border-slate-800 dark:bg-slate-900 dark:hover:border-slate-700'
      }`}
    >
      <span
        className={`mt-0.5 shrink-0 rounded-xl p-2.5 ${
          activa
            ? 'bg-brand text-white'
            : 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400'
        }`}
      >
        {icono}
      </span>
      <span className="min-w-0 flex-1">
        <span className="flex items-center justify-between">
          <span className="text-sm font-black text-slate-900 dark:text-white">{titulo}</span>
          {activa && <CheckCircle2 className="h-4 w-4 text-brand" />}
        </span>
        <span className="mt-1 block text-xs leading-relaxed text-slate-500 dark:text-slate-400">
          {texto}
        </span>
      </span>
    </button>
  )
}

function Casilla({
  checked,
  disabled,
  onChange,
  titulo,
  texto,
}: {
  checked: boolean
  disabled: boolean
  onChange: (v: boolean) => void
  titulo: string
  texto: string
}) {
  return (
    <label className="flex cursor-pointer items-start gap-3">
      <input
        type="checkbox"
        checked={checked}
        disabled={disabled}
        onChange={(e) => onChange(e.target.checked)}
        className="mt-0.5 rounded border-slate-300"
      />
      <span>
        <span className="block text-xs font-bold text-slate-900 dark:text-white">{titulo}</span>
        <span className="mt-0.5 block text-[11px] leading-relaxed text-slate-500 dark:text-slate-400">
          {texto}
        </span>
      </span>
    </label>
  )
}
