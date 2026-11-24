import { useState, useEffect } from 'react'
import {
  Trophy,
  Gift,
  Award,
  CheckCircle2,
  Building2,
  Ban,
  UserCheck,
  Sparkles,
} from 'lucide-react'
import { Button } from '@/components/ui/Button'
import type {
  PrizeDrawSummary,
  PrizeDrawVoidCode,
  PrizeResponse,
  RaffleDisplayFields,
} from '@/api/types'
import { fireBurst } from '../confetti'
import { CAMPOS_VISIBLES_POR_DEFECTO, ganadorVisible } from '../camposVisibles'

interface GanadorRevealCardProps {
  /** El sorteo vivo que devolvió el servicio. */
  ganador: PrizeDrawSummary
  /** Qué datos del ganador se muestran (§10). */
  campos?: RaffleDisplayFields
  premio: PrizeResponse
  rondaNombre?: string
  premioNumeroEnRonda?: number
  totalPremiosEnRonda?: number
  onSiguientePremio?: () => void
  onFinalizar?: () => void
  /** Anular el ganador, con el motivo ya elegido si se sabe. Sin él, no se ofrece. */
  onVoid?: (motivo?: PrizeDrawVoidCode) => void
  voidTitle?: string
  esUltimoPremio?: boolean
  requiereConfirmarPresencia?: boolean
  /**
   * Se llama una vez cuando la cuenta llega a cero sin confirmar la presencia: quien la recibe
   * anula al ganador por ausente y el premio vuelve a quedar disponible.
   */
  onTiempoAgotado?: () => void
  /** Se llama al confirmar la presencia (el proyector lo muestra). */
  onPresenciaConfirmada?: () => void
}

/** Segundos que tiene el ganador para aparecer en la sala. */
const SEGUNDOS_PRESENCIA = 60

export function GanadorRevealCard({
  ganador,
  campos = CAMPOS_VISIBLES_POR_DEFECTO,
  premio,
  rondaNombre,
  premioNumeroEnRonda,
  totalPremiosEnRonda,
  onSiguientePremio,
  onFinalizar,
  onVoid,
  voidTitle,
  esUltimoPremio = false,
  requiereConfirmarPresencia = false,
  onTiempoAgotado,
  onPresenciaConfirmada,
}: GanadorRevealCardProps) {
  const visible = ganadorVisible(ganador, campos)
  const [segundos, setSegundos] = useState(SEGUNDOS_PRESENCIA)
  const [confirmado, setConfirmado] = useState(!requiereConfirmarPresencia)

  // Cada ganador nuevo reinicia la cuenta.
  useEffect(() => {
    setSegundos(SEGUNDOS_PRESENCIA)
    setConfirmado(!requiereConfirmarPresencia)
  }, [ganador.id, premio.id, requiereConfirmarPresencia])

  const contando = requiereConfirmarPresencia && !confirmado && segundos > 0
  useEffect(() => {
    if (!contando) return
    const timer = setInterval(() => setSegundos((s) => Math.max(0, s - 1)), 1000)
    return () => clearInterval(timer)
  }, [contando])

  // Sin confirmar a tiempo, la anulación es automática: nadie tiene que acordarse de pulsar nada.
  const agotadoSinConfirmar = requiereConfirmarPresencia && !confirmado && segundos === 0
  useEffect(() => {
    if (agotadoSinConfirmar) onTiempoAgotado?.()
    // Solo al llegar a cero; onTiempoAgotado cambia de identidad en cada render del padre.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [agotadoSinConfirmar])

  const confirmarPresencia = () => {
    setConfirmado(true)
    onPresenciaConfirmada?.()
    // Segunda ola de celebración: la persona subió al escenario.
    fireBurst()
  }

  const urgente = segundos <= 15
  const agotado = segundos === 0
  const anularTitulo = voidTitle ?? 'Anular este ganador: el premio vuelve a quedar por sortear'
  // El anillo de la cuenta: su largo es la fracción de tiempo que queda.
  const circunferencia = 2 * Math.PI * 26
  const avance = (segundos / SEGUNDOS_PRESENCIA) * circunferencia

  return (
    // La imagen de fondo del sorteo sigue a la vista; el color va en la tarjeta, que es sólida.
    <div className="mx-auto my-auto w-full max-w-2xl px-4">
      <div
        role="dialog"
        aria-labelledby="ganador-nombre"
        className="w-full max-w-2xl animate-in fade-in zoom-in-95 duration-300"
      >
        <div className="max-h-[88vh] overflow-y-auto rounded-3xl border-4 border-brand bg-card text-card-foreground shadow-2xl">
          {/* Encabezado: la franja de la marca con el trofeo y el ganador */}
          <div className="relative overflow-hidden rounded-t-3xl bg-brand px-6 pb-8 pt-6 text-center text-white sm:px-10">
            <div className="relative flex flex-wrap items-center justify-center gap-2 text-[11px] font-bold uppercase tracking-[0.2em] text-white">
              <Sparkles className="h-3.5 w-3.5" />
              ¡Tenemos ganador!
              {rondaNombre && premioNumeroEnRonda && totalPremiosEnRonda && (
                <span className="normal-case tracking-normal text-white">
                  · {rondaNombre} · Premio {premioNumeroEnRonda} de {totalPremiosEnRonda}
                </span>
              )}
            </div>

            <div className="relative mx-auto mt-4 flex h-20 w-20 items-center justify-center rounded-full bg-brand-deep ring-4 ring-white">
              <Trophy className="h-10 w-10 text-amber-300 drop-shadow" />
            </div>

            <h2
              id="ganador-nombre"
              className="relative mt-4 text-balance text-3xl font-black leading-tight tracking-tight sm:text-5xl"
            >
              {visible.nombre}
            </h2>
            {visible.detalle && (
              <p className="relative mt-2 inline-flex items-center gap-1.5 rounded-full bg-brand-deep px-3 py-1 text-sm font-semibold">
                <Building2 className="h-4 w-4" />
                {visible.detalle}
              </p>
            )}
            {visible.identificador && (
              <p className="relative mt-2 font-mono text-sm font-bold tabular-nums text-white/90">
                {visible.identificador}
              </p>
            )}
          </div>

          <div className="space-y-4 p-5 sm:p-7">
            {/* El premio */}
            <div className="flex items-center gap-4 rounded-2xl border border-slate-200 bg-muted p-4 dark:border-slate-700">
              <div className="flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl bg-brand text-white shadow-sm">
                <Gift className="h-7 w-7" />
              </div>
              <div className="min-w-0 flex-1">
                <p className="text-[10px] font-bold uppercase tracking-wider text-brand dark:text-brand-accent">
                  Premio obtenido
                </p>
                <h3 className="text-base font-black leading-snug text-foreground sm:text-lg">
                  {premio.name}
                </h3>
                {premio.description && (
                  <p className="mt-0.5 line-clamp-2 text-xs text-muted-foreground">
                    {premio.description}
                  </p>
                )}
              </div>
              {premio.value && (
                <span className="hidden shrink-0 rounded-xl bg-card px-3 py-1.5 text-sm font-black text-brand ring-1 ring-brand/20 sm:block dark:text-brand-accent">
                  {premio.value}
                </span>
              )}
            </div>

            <div className="flex items-center justify-between gap-3 rounded-2xl border border-dashed border-slate-300 px-4 py-3 dark:border-slate-700">
              <span className="text-xs font-semibold text-muted-foreground">Código de canje</span>
              <span className="font-mono text-lg font-black tracking-widest text-foreground">
                {ganador.redemptionCode}
              </span>
            </div>

            {/* Presencia en sala */}
            {requiereConfirmarPresencia &&
              (confirmado ? (
                <p className="flex items-center justify-center gap-2 rounded-2xl bg-emerald-50 py-3 text-sm font-bold text-emerald-700 dark:bg-emerald-950 dark:text-emerald-400">
                  <UserCheck className="h-5 w-5" />
                  Presencia confirmada en sala
                </p>
              ) : (
                <div
                  className={`flex flex-col items-center gap-4 rounded-2xl border-2 p-4 sm:flex-row ${
                    urgente
                      ? 'border-rose-300 bg-rose-50 dark:border-rose-900 dark:bg-rose-950'
                      : 'border-slate-200 bg-muted dark:border-slate-700'
                  }`}
                >
                  <div role="timer" aria-live="off" className="relative h-16 w-16 shrink-0">
                    <svg viewBox="0 0 60 60" className="h-16 w-16 -rotate-90">
                      <circle
                        cx="30"
                        cy="30"
                        r="26"
                        className="fill-none stroke-slate-200 dark:stroke-slate-700"
                        strokeWidth="5"
                      />
                      <circle
                        cx="30"
                        cy="30"
                        r="26"
                        className={`fill-none transition-[stroke-dashoffset] duration-1000 ease-linear ${
                          urgente ? 'stroke-rose-500' : 'stroke-brand-accent'
                        }`}
                        strokeWidth="5"
                        strokeLinecap="round"
                        strokeDasharray={circunferencia}
                        strokeDashoffset={circunferencia - avance}
                      />
                    </svg>
                    <span
                      className={`absolute inset-0 flex items-center justify-center font-mono text-lg font-black ${
                        urgente ? 'text-rose-600 dark:text-rose-400' : 'text-foreground'
                      }`}
                    >
                      {segundos}
                    </span>
                  </div>
                  <div className="flex-1 text-center sm:text-left">
                    <p className="text-sm font-black text-foreground">
                      {agotado ? 'No se presentó a tiempo' : '¿Está en la sala?'}
                    </p>
                    <p className="text-xs text-muted-foreground">
                      {agotado
                        ? 'Se anula: el premio vuelve a quedar disponible y esta persona sale de los próximos premios.'
                        : 'Confirma cuando se presente. Si se acaba el tiempo, se anula sola.'}
                    </p>
                  </div>
                  <div className="flex w-full flex-col gap-2 sm:w-auto">
                    <Button
                      type="button"
                      onClick={confirmarPresencia}
                      className="cursor-pointer rounded-xl bg-emerald-600 px-5 py-2.5 text-sm font-black text-white shadow-md shadow-emerald-600/30 hover:bg-emerald-700 active:scale-95"
                    >
                      <UserCheck className="mr-1.5 h-4 w-4" /> Confirmar presencia
                    </Button>
                    {onVoid && (
                      <Button
                        type="button"
                        variant="outline"
                        onClick={() => onVoid('absent')}
                        disabled={voidTitle !== undefined}
                        title={anularTitulo}
                        className="cursor-pointer rounded-xl border-rose-300 text-xs font-bold text-rose-600 hover:bg-rose-50 disabled:cursor-not-allowed disabled:opacity-50 dark:border-rose-800 dark:text-rose-400 dark:hover:bg-rose-950/50"
                      >
                        <Ban className="mr-1 h-3.5 w-3.5" /> No está presente
                      </Button>
                    )}
                  </div>
                </div>
              ))}

            {/* Acciones */}
            <div className="flex flex-col-reverse items-stretch gap-2.5 border-t border-slate-100 pt-4 sm:flex-row sm:items-center dark:border-slate-800">
              {onVoid && (
                <Button
                  type="button"
                  variant="ghost"
                  onClick={() => onVoid()}
                  disabled={voidTitle !== undefined}
                  title={anularTitulo}
                  className="cursor-pointer text-sm font-semibold text-muted-foreground hover:text-rose-600 disabled:cursor-not-allowed disabled:opacity-50 sm:mr-auto"
                >
                  <Ban className="mr-1.5 h-4 w-4" /> Anular ganador
                </Button>
              )}
              {!esUltimoPremio && onSiguientePremio && (
                <Button
                  type="button"
                  onClick={onSiguientePremio}
                  disabled={!confirmado}
                  title={
                    confirmado ? undefined : 'Primero confirma la presencia o anula al ganador.'
                  }
                  className="cursor-pointer rounded-xl bg-brand px-6 py-2.5 text-sm font-bold text-white hover:bg-brand/90 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  <Award className="mr-1.5 h-4 w-4" /> Siguiente premio
                </Button>
              )}
              {esUltimoPremio && onFinalizar && (
                <Button
                  type="button"
                  onClick={onFinalizar}
                  disabled={!confirmado}
                  title={
                    confirmado ? undefined : 'Primero confirma la presencia o anula al ganador.'
                  }
                  className="cursor-pointer rounded-xl bg-emerald-600 px-6 py-2.5 text-sm font-bold text-white hover:bg-emerald-700 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  <CheckCircle2 className="mr-1.5 h-4 w-4" /> Finalizar sorteo
                </Button>
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}
