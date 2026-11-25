import { useEffect, useRef, useState, type FormEvent } from 'react'
import { BitacoraEvento } from './components/BitacoraEvento'
import { Link, useParams } from 'react-router-dom'
import {
  Pencil,
  CalendarDays,
  Languages,
  Users,
  MapPin,
  ChevronDown,
  Play,
  Lock,
  Ban,
  CheckCircle2,
  AlertCircle,
  Check,
  Copy,
  Clock,
  Mail,
  BellOff,
  QrCode,
  Gift,
  Loader2,
} from 'lucide-react'
import { useEvento, useEventoLifecycle, useUpdateEvento } from './hooks'
import { useConvocatoria } from './useConvocatoria'
import { CerrarConvocatoriaDialog } from './components/CerrarConvocatoriaDialog'
import { sendCancellationNotice, type CancellationNoticeOutcome } from './cancellationNotice'
import { EventoFormModal } from './components/EventoFormModal'
import { GestionEventoArea } from './components/GestionEventoArea'
import {
  useConvocatoriaLanzada,
  useConvocatoriasDisponible,
  useEventTracking,
} from '@/features/convocatorias/tracking'
import { Breadcrumbs } from '@/components/ui/Breadcrumbs'
import { Button } from '@/components/ui/Button'
import { ActionMenu } from '@/components/ui/ActionMenu'
import { Callout } from '@/components/ui/Callout'
import { Card } from '@/components/ui/Card'
import { ProgressBar } from '@/components/ui/ProgressBar'
import { MenuItem, MenuSeparator } from '@/components/ui/MenuItem'
import { Tabs } from '@/components/ui/Tabs'
import { Field, TextArea } from '@/components/ui/Field'
import { ErrorNote, Spinner, ToastNotification } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { useCan } from '@/auth/AuthProvider'
import { ROLES } from '@/auth/roles'
import { formatDate } from '@/lib/format'
import { eventoStatusLabel, eventoTypeLabel, idiomaLabel } from '@/lib/labels'
import type { EventoDetail, EventoStatus } from '@/api/types'
import { SectionHeader } from '@/components/ui/SectionHeader'

export function EventoDetailPage() {
  const { id = '' } = useParams()
  const { data: evento, isLoading, error } = useEvento(id)
  const conConvocatorias = useConvocatoriasDisponible()
  const [tabElegida, setActiveTab] = useState<'convocatorias' | 'audit'>('convocatorias')
  // Sin Invitaciones no hay convocatorias que mostrar: la pestaña no existe.
  const activeTab = conConvocatorias ? tabElegida : 'audit'

  if (isLoading) return <Spinner label="Cargando detalles del evento..." />
  if (error != null) return <ErrorNote error={error} />
  if (!evento) return null

  const tabsItems = [
    ...(conConvocatorias
      ? [{ key: 'convocatorias' as const, label: 'Convocatorias & registro' }]
      : []),
    { key: 'audit' as const, label: 'Bitácora' },
  ]

  return (
    <div className="space-y-6">
      <Breadcrumbs
        items={[
          { label: 'Eventos', to: '/eventos/todos' },
          { label: 'Todos los eventos', to: '/eventos/todos' },
          { label: evento.name },
        ]}
      />

      {/* Encabezado y Acciones de Ciclo de Vida */}
      <HeaderSection evento={evento} onLanzarConvocatoria={() => setActiveTab('convocatorias')} />

      {/* Cuenta regresiva + avance: en móvil va aquí */}
      {evento.status !== 'closed' && evento.status !== 'cancelled' && (
        <CountdownReadinessStrip
          evento={evento}
          className="flex items-center justify-between gap-4 rounded-2xl border border-slate-200/80 bg-white p-4 shadow-2xs dark:border-slate-800 dark:bg-slate-900 lg:hidden"
        />
      )}

      {/* Resumen básico del evento */}
      <ResumenCardsSection evento={evento} />

      <Tabs value={activeTab} onChange={setActiveTab} items={tabsItems} variant="card" />

      <div className="space-y-6">
        {activeTab === 'convocatorias' && (
          <>
            <SectionHeader
              title="Convocatorias y registro"
              description="Campañas de invitación, registro de participantes y check-in del evento."
            />
            <GestionEventoArea evento={evento} />
          </>
        )}
        {activeTab === 'audit' && (
          <div className="space-y-6">
            <SectionHeader
              title="Bitácora"
              description="Registro de creación y cambios realizados sobre el evento."
            />
            <BitacoraEvento evento={evento} />
          </div>
        )}
      </div>
    </div>
  )
}

const HERO_STATUS_STYLE: Record<EventoStatus, string> = {
  planning: 'bg-amber-100 text-amber-900 dark:bg-amber-950 dark:text-amber-300',
  in_progress: 'bg-blue-100 text-blue-900 dark:bg-blue-950 dark:text-blue-300',
  closed: 'bg-slate-100 text-slate-800 dark:bg-slate-800 dark:text-slate-300',
  cancelled: 'bg-rose-100 text-rose-900 dark:bg-rose-950 dark:text-rose-300',
}

/** Cuenta regresiva hasta la medianoche local de `startDate`; se recalcula cada segundo. */
function useCountdown(startDateIso: string) {
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 1000)
    return () => clearInterval(timer)
  }, [])
  const [y, m, d] = startDateIso.split('-').map(Number)
  const diff = Math.max(0, new Date(y, m - 1, d).getTime() - now)
  return {
    days: Math.floor(diff / 86_400_000),
    hours: Math.floor((diff % 86_400_000) / 3_600_000),
    minutes: Math.floor((diff % 3_600_000) / 60_000),
    seconds: Math.floor((diff % 60_000) / 1000),
  }
}

function ReadinessGauge({ pct }: { pct: number }) {
  const r = 26
  const c = 2 * Math.PI * r
  const clamped = Math.min(100, Math.max(0, pct))
  const offset = c - (clamped / 100) * c
  const colorClass =
    clamped >= 75
      ? 'stroke-emerald-500'
      : clamped >= 50
        ? 'stroke-blue-600'
        : clamped >= 25
          ? 'stroke-amber-500'
          : 'stroke-rose-500'

  return (
    <div className="flex flex-col items-center gap-1">
      <div className="relative h-16 w-16">
        <svg viewBox="0 0 64 64" className="h-16 w-16 -rotate-90">
          <circle
            cx="32"
            cy="32"
            r={r}
            fill="none"
            strokeWidth="6"
            className="stroke-slate-200 dark:stroke-slate-800"
          />
          <circle
            cx="32"
            cy="32"
            r={r}
            fill="none"
            strokeWidth="6"
            strokeLinecap="round"
            strokeDasharray={c}
            strokeDashoffset={offset}
            className={colorClass}
          />
        </svg>
        <div className="absolute inset-0 flex items-center justify-center font-sans text-sm font-extrabold text-slate-900 dark:text-white">
          {clamped}%
        </div>
      </div>
      <p className="text-[8.5px] font-bold uppercase tracking-widest text-slate-400">Listo</p>
    </div>
  )
}

/**
 * Cuenta regresiva al inicio del evento + medidor de avance. En el header solo se ve en
 * escritorio (`lg:flex`); en móvil se muestra como tira dentro de la fila de resumen.
 */
function CountdownReadinessStrip({
  evento,
  className = '',
}: {
  evento: EventoDetail
  className?: string
}) {
  const countdown = useCountdown(evento.startDate)
  const convocatoriaLanzada = Boolean(useConvocatoriaLanzada(evento.id))
  const eventoActivo = evento.status === 'in_progress' || evento.status === 'closed'
  const listoPct = Math.round(
    ([true, convocatoriaLanzada, eventoActivo].filter(Boolean).length / 3) * 100,
  )

  return (
    <div className={className}>
      <div>
        <p className="mb-1.5 text-[10px] font-bold uppercase tracking-widest text-slate-500 dark:text-slate-400">
          Cuenta regresiva
        </p>
        <div className="flex items-center gap-1.5">
          {[
            { value: countdown.days, label: 'Días' },
            { value: countdown.hours, label: 'Hrs' },
            { value: countdown.minutes, label: 'Min' },
            { value: countdown.seconds, label: 'Seg' },
          ].map((box) => (
            <div
              key={box.label}
              className="flex w-13 flex-col items-center justify-center rounded-xl border border-slate-200 bg-slate-50 dark:border-slate-800 dark:bg-slate-800/60 py-2"
            >
              <span className="font-sans text-lg font-black tabular-nums text-slate-900 dark:text-white">
                {String(box.value).padStart(2, '0')}
              </span>
              <span className="text-[8.5px] font-bold uppercase tracking-widest text-slate-500 dark:text-slate-400">
                {box.label}
              </span>
            </div>
          ))}
        </div>
      </div>

      <ReadinessGauge pct={listoPct} />
    </div>
  )
}

function HeaderSection({
  evento,
  onLanzarConvocatoria,
}: {
  evento: EventoDetail
  onLanzarConvocatoria: () => void
}) {
  const { iniciar, cerrar, cancelar } = useEventoLifecycle(evento.id)
  const updateEvento = useUpdateEvento(evento.id)
  const convocatoria = useConvocatoria(evento)
  const [showCancel, setShowCancel] = useState(false)
  const [showEdit, setShowEdit] = useState(false)
  const [confirmAction, setConfirmAction] = useState<'iniciar' | 'cerrar' | null>(null)
  const [menuOpen, setMenuOpen] = useState(false)
  const actionsRef = useRef<HTMLButtonElement>(null)
  const [razon, setRazon] = useState('')
  const [avisoCancelacion, setAvisoCancelacion] = useState<CancellationNoticeOutcome | null>(null)
  const canIniciar = useCan(ROLES.eventsStart)
  const canCerrar = useCan(ROLES.eventsClose)
  const canCancelar = useCan(ROLES.eventsCancel)
  const canEditar = useCan(ROLES.eventsWrite)

  const [copiedLink, setCopiedLink] = useState(false)
  const publicUrl = evento.externalUrl || `${window.location.origin}/publico/eventos/${evento.id}`

  const handleCopyLink = () => {
    navigator.clipboard.writeText(publicUrl)
    setCopiedLink(true)
    setTimeout(() => setCopiedLink(false), 2500)
  }

  const isTerminal = evento.status === 'closed' || evento.status === 'cancelled'
  const venueNombre = evento.cities[0]?.name || 'Por definir'

  const convocatoriaLanzada = Boolean(useConvocatoriaLanzada(evento.id))

  const handleCancel = (e: FormEvent) => {
    e.preventDefault()
    cancelar.mutate(razon.trim(), {
      onSuccess: () => {
        setShowCancel(false)
        void sendCancellationNotice(evento.id).then(setAvisoCancelacion)
      },
    })
  }

  return (
    <div className="space-y-4">
      {avisoCancelacion && (
        <ToastNotification
          tone={avisoCancelacion.ok ? 'success' : 'error'}
          onClose={() => setAvisoCancelacion(null)}
        >
          {avisoCancelacion.message}
        </ToastNotification>
      )}
      <div className="rounded-2xl border border-slate-200/80 bg-white shadow-xs dark:border-slate-800 dark:bg-slate-900">
        <div className="flex flex-col gap-6 p-6 sm:p-7 lg:flex-row lg:items-center lg:justify-between">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2.5">
              <span
                className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-[10.5px] font-extrabold uppercase tracking-wider ${HERO_STATUS_STYLE[evento.status]}`}
              >
                <span className="h-1.5 w-1.5 rounded-full bg-current" />{' '}
                {eventoStatusLabel[evento.status]}
              </span>
              <span className="text-[10.5px] font-bold uppercase tracking-widest text-slate-500 dark:text-slate-400">
                {[eventoTypeLabel[evento.type], evento.cities[0]?.name].filter(Boolean).join(' · ')}
              </span>
            </div>

            <h1 className="mt-2 font-sans text-2xl font-black tracking-tight text-slate-900 dark:text-white sm:text-3xl">
              {evento.name}
            </h1>

            <div className="mt-2.5 flex flex-wrap items-center gap-x-4 gap-y-2 text-xs font-semibold text-slate-600 dark:text-slate-300">
              <span className="flex items-center gap-1.5">
                <CalendarDays size={14} className="text-slate-400 shrink-0" />{' '}
                {formatDate(evento.startDate)}
              </span>

              {evento.invitedCount != null && (
                <span className="flex items-center gap-1.5">
                  <Users size={14} className="text-slate-400 shrink-0" /> {evento.invitedCount}{' '}
                  invitados
                </span>
              )}

              {evento.language && (
                <span className="flex items-center gap-1.5">
                  <Languages size={14} className="text-slate-400 shrink-0" />{' '}
                  {idiomaLabel[evento.language]}
                </span>
              )}

              {/* Venue al final con estilo estándar */}
              <span className="flex items-center gap-1.5 min-w-0">
                <MapPin size={14} className="text-slate-400 shrink-0" />
                <span className="truncate" title={venueNombre}>
                  {venueNombre}
                </span>
              </span>
            </div>

            {evento.description && (
              <p className="mt-3.5 text-xs text-slate-600 dark:text-slate-300 leading-relaxed font-body max-w-2xl bg-slate-50 dark:bg-slate-800/40 p-3 rounded-xl border border-slate-100 dark:border-slate-800/60">
                {evento.description}
              </p>
            )}

            <CerrarConvocatoriaDialog convocatoria={convocatoria} eventoNombre={evento.name} />

            {/* El botón de la cabecera cambia de pestaña: su error tiene que verse acá. */}
            {!isTerminal &&
              convocatoria.errors.map((error, i) => (
                <div key={i} className="mt-3">
                  <ErrorNote error={error} />
                </div>
              ))}

            {!isTerminal && (
              <div className="mt-4 flex flex-wrap items-center gap-2.5">
                {/* El mismo botón que el del área de convocatoria: abre o cierra el registro
                    y lleva a esa pestaña, que es donde se ve el resultado. */}
                <Button
                  size="sm"
                  onClick={() => {
                    convocatoria.toggle()
                    onLanzarConvocatoria()
                  }}
                  disabled={convocatoria.disabled}
                  title={convocatoria.title}
                  className={`font-bold text-white cursor-pointer shadow-2xs ${
                    convocatoria.registroAbierto
                      ? 'bg-rose-700 hover:bg-rose-800'
                      : 'bg-emerald-600 hover:bg-emerald-700'
                  }`}
                >
                  {convocatoria.guardando && <Loader2 size={14} className="mr-1.5 animate-spin" />}
                  {convocatoria.registroAbierto ? 'Cerrar convocatoria' : 'Abrir convocatoria'}
                </Button>

                {evento.status === 'in_progress' && (
                  <>
                    <Link to={`/convocatorias/checkin?eventoId=${evento.id}`}>
                      <Button
                        size="sm"
                        className="bg-brand-deep font-bold text-white hover:bg-brand-deep/90 cursor-pointer shadow-2xs"
                      >
                        <QrCode size={14} className="mr-1.5" /> Check-in en vivo
                      </Button>
                    </Link>

                    <Link to="/sorteo">
                      <Button
                        size="sm"
                        className="bg-amber-500 font-bold text-slate-950 hover:bg-amber-600 cursor-pointer shadow-2xs"
                      >
                        <Gift size={14} className="mr-1.5" /> Realizar Sorteo
                      </Button>
                    </Link>
                  </>
                )}

                <div className="relative">
                  <Button
                    ref={actionsRef}
                    size="sm"
                    variant="outline"
                    onClick={() => setMenuOpen((prev) => !prev)}
                    className="gap-1.5 bg-slate-50 font-bold dark:bg-slate-800"
                  >
                    Acciones <ChevronDown size={14} />
                  </Button>

                  <ActionMenu
                    open={menuOpen}
                    onClose={() => setMenuOpen(false)}
                    anchorRef={actionsRef}
                    align="start"
                    className="w-48"
                  >
                    {canEditar.allowed && (
                      <MenuItem
                        onClick={() => {
                          setMenuOpen(false)
                          setShowEdit(true)
                        }}
                      >
                        <Pencil size={14} className="text-slate-600" /> Editar evento
                      </MenuItem>
                    )}

                    <MenuItem
                      onClick={() => {
                        handleCopyLink()
                        setMenuOpen(false)
                      }}
                    >
                      {copiedLink ? (
                        <Check size={14} className="text-emerald-600" />
                      ) : (
                        <Copy size={14} className="text-blue-600" />
                      )}
                      {copiedLink ? '¡Enlace copiado!' : 'Copiar enlace (Link)'}
                    </MenuItem>

                    <MenuSeparator />

                    {evento.status === 'planning' && (
                      <MenuItem
                        disabled={iniciar.isPending || !canIniciar.allowed}
                        onClick={() => {
                          setMenuOpen(false)
                          setConfirmAction('iniciar')
                        }}
                      >
                        <Play size={14} className="text-blue-600" /> Iniciar evento
                      </MenuItem>
                    )}

                    {evento.status === 'in_progress' && (
                      <MenuItem
                        disabled={cerrar.isPending || !canCerrar.allowed}
                        onClick={() => {
                          setMenuOpen(false)
                          setConfirmAction('cerrar')
                        }}
                      >
                        <Lock size={14} className="text-slate-600" /> Cerrar evento
                      </MenuItem>
                    )}

                    {canCancelar.allowed && (
                      <>
                        <MenuSeparator />
                        <MenuItem
                          tone="danger"
                          onClick={() => {
                            setMenuOpen(false)
                            setShowCancel(true)
                          }}
                        >
                          <Ban size={14} /> Cancelar evento
                        </MenuItem>
                      </>
                    )}
                  </ActionMenu>
                </div>
              </div>
            )}
          </div>

          {!isTerminal && (
            <CountdownReadinessStrip
              evento={evento}
              className="hidden shrink-0 items-center gap-5 lg:flex"
            />
          )}
        </div>
      </div>

      {confirmAction && (
        <Modal
          title={
            <div className="flex items-center gap-2.5">
              <span
                className={`flex size-7 shrink-0 items-center justify-center rounded-full ${
                  confirmAction === 'iniciar'
                    ? 'bg-blue-100 text-blue-600 dark:bg-blue-950/60 dark:text-blue-400'
                    : 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300'
                }`}
              >
                {confirmAction === 'iniciar' ? <Play size={15} /> : <Lock size={15} />}
              </span>
              <h3 className="font-sans text-base font-bold text-slate-900 dark:text-white">
                {confirmAction === 'iniciar' ? '¿Iniciar evento?' : '¿Cerrar evento?'}
              </h3>
            </div>
          }
          onClose={() => setConfirmAction(null)}
        >
          <div className="space-y-4">
            <p className="text-xs sm:text-sm text-slate-600 dark:text-slate-300">
              {confirmAction === 'iniciar'
                ? 'El evento pasará a estado Activo. A partir de este momento:'
                : 'El evento pasará a estado Cerrado de forma definitiva. A partir de este momento:'}
            </p>

            <ul className="space-y-2 rounded-xl border border-slate-100 bg-slate-50 p-3.5 dark:border-slate-800 dark:bg-slate-800/40">
              {(confirmAction === 'iniciar'
                ? [
                    'La campaña de convocatoria y el check-in QR quedan habilitados',
                    'El evento queda disponible para vincular sorteos activos',
                    'El evento aparece como Activo en el listado general',
                  ]
                : [
                    'Se cierra el registro y la actividad operativa del evento',
                    'Los sorteos vinculados permanecen disponibles para consulta de actas y premios',
                    'El estado pasa a Cerrado de forma definitiva',
                  ]
              ).map((item) => (
                <li
                  key={item}
                  className="flex items-start gap-2 text-xs text-slate-600 dark:text-slate-300"
                >
                  <CheckCircle2 size={14} className="mt-0.5 shrink-0 text-emerald-600" />
                  <span>{item}</span>
                </li>
              ))}
            </ul>

            {confirmAction === 'cerrar' && (
              <div className="flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 p-3 text-[11px] font-medium text-amber-800 dark:border-amber-900/60 dark:bg-amber-950/30 dark:text-amber-300">
                <AlertCircle size={14} className="mt-0.5 shrink-0" />
                <span>Esta acción no se puede deshacer.</span>
              </div>
            )}

            <div className="flex justify-end gap-2.5 border-t border-slate-100 pt-3 dark:border-slate-800">
              <Button
                variant="secondary"
                onClick={() => setConfirmAction(null)}
                className="cursor-pointer font-bold"
              >
                Cancelar
              </Button>
              <Button
                onClick={() => {
                  if (confirmAction === 'iniciar') {
                    iniciar.mutate(undefined, { onSuccess: () => setConfirmAction(null) })
                  } else {
                    cerrar.mutate(undefined, { onSuccess: () => setConfirmAction(null) })
                  }
                }}
                disabled={iniciar.isPending || cerrar.isPending}
                className="bg-brand-deep text-white hover:bg-brand-deep/90 cursor-pointer font-bold"
              >
                {iniciar.isPending || cerrar.isPending ? 'Procesando…' : 'Aceptar'}
              </Button>
            </div>
          </div>
        </Modal>
      )}

      {showEdit && (
        <EventoFormModal
          evento={evento}
          venueNombre={venueNombre}
          onClose={() => setShowEdit(false)}
          onUpdate={(body, ciudad) =>
            updateEvento.mutate({ body, ciudad }, { onSuccess: () => setShowEdit(false) })
          }
          pending={updateEvento.isPending}
          error={updateEvento.error}
        />
      )}

      {showCancel && (
        <Modal
          title={
            <div className="flex items-center gap-2.5">
              <span className="flex size-7 items-center justify-center rounded-full bg-rose-100 text-rose-600 dark:bg-rose-950/60 dark:text-rose-400">
                <Ban size={15} />
              </span>
              <h3 className="font-sans text-base font-bold text-slate-900 dark:text-white">
                Cancelar evento
              </h3>
            </div>
          }
          onClose={() => setShowCancel(false)}
        >
          <form onSubmit={handleCancel} className="space-y-4">
            <p className="text-sm leading-relaxed text-slate-600 dark:text-slate-300">
              El evento{' '}
              <strong className="font-semibold text-slate-900 dark:text-white">
                {evento.name}
              </strong>{' '}
              pasará a estado{' '}
              <span className="font-semibold text-rose-600 dark:text-rose-400">Cancelado</span> y no
              admitirá más cambios.
            </p>

            {convocatoriaLanzada ? (
              <Callout tone="warning" icon={Mail} title="Se notificará a los participantes">
                La convocatoria de este evento ya fue lanzada. Al confirmar, se enviará de inmediato
                un correo a todos los participantes registrados informándoles de la cancelación
                oficial y del motivo indicado abajo.
              </Callout>
            ) : (
              <Callout tone="neutral" icon={BellOff}>
                Este evento no tiene convocatoria lanzada, así que{' '}
                <strong className="font-semibold">no se enviará ningún correo</strong>: no hay
                participantes registrados a quienes notificar.
              </Callout>
            )}

            <Field label="Motivo de la cancelación" required>
              <TextArea
                value={razon}
                onChange={(e) => setRazon(e.target.value)}
                placeholder="Escriba la justificación oficial de la cancelación…"
                required
              />
            </Field>

            <div className="flex justify-end gap-2 border-t border-slate-100 pt-3 dark:border-slate-800">
              <Button type="button" variant="secondary" onClick={() => setShowCancel(false)}>
                Volver
              </Button>
              <Button type="submit" variant="destructive" disabled={cancelar.isPending}>
                {cancelar.isPending
                  ? 'Cancelando…'
                  : convocatoriaLanzada
                    ? 'Cancelar evento y notificar'
                    : 'Cancelar evento'}
              </Button>
            </div>
          </form>
        </Modal>
      )}
    </div>
  )
}

function InvitadosCard({ evento }: { evento: EventoDetail }) {
  const capacidad = evento.invitedCount ?? 0
  const registrados = useEventTracking(evento.id).data?.registrations.total ?? 0
  const confirmados = 0
  const convocatoriaLanzada = Boolean(useConvocatoriaLanzada(evento.id))
  const pct = (n: number) => (capacidad > 0 ? Math.min(100, Math.round((n / capacidad) * 100)) : 0)

  return (
    <Card className="bg-white p-5 dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 rounded-2xl shadow-2xs">
      <h3 className="mb-4 flex items-center gap-2 font-sans text-sm font-extrabold text-slate-900 dark:text-white">
        <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-blue-50 text-brand-deep dark:bg-blue-950/60 dark:text-blue-300">
          <Users size={15} />
        </span>
        Invitados
      </h3>

      <p className="flex items-baseline gap-1.5">
        <span className="text-2xl font-extrabold text-slate-900 dark:text-white">
          {registrados}
        </span>
        <span className="text-sm font-semibold text-slate-400">/ {capacidad || '—'}</span>
      </p>

      <div className="mt-3.5 space-y-2.5">
        <ProgressBar label="Capacidad" right={capacidad} value={100} color="primary" />
        <ProgressBar
          label="Registrados"
          right={registrados}
          value={pct(registrados)}
          color="emerald"
        />
        <ProgressBar
          label="Confirmados"
          right={confirmados}
          value={pct(confirmados)}
          color="blue"
        />
      </div>

      {!convocatoriaLanzada && (
        <p className="mt-3.5 flex items-center gap-1.5 text-[11px] font-semibold text-amber-600 dark:text-amber-400">
          <Clock size={12} /> Convocatoria en espera
        </p>
      )}
    </Card>
  )
}

function ResumenCardsSection({ evento }: { evento: EventoDetail }) {
  return (
    <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
      <Card className="bg-white p-5 dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 rounded-2xl shadow-2xs space-y-3">
        <h3 className="font-sans text-sm font-extrabold text-slate-900 dark:text-white">
          Información General del Evento
        </h3>
        <div className="grid grid-cols-2 gap-3 text-xs">
          <div>
            <p className="text-[10px] font-bold uppercase tracking-wider text-slate-400">Tipo</p>
            <p className="mt-0.5 font-semibold text-slate-900 dark:text-white">
              {eventoTypeLabel[evento.type]}
            </p>
          </div>
          <div>
            <p className="text-[10px] font-bold uppercase tracking-wider text-slate-400">Estado</p>
            <p className="mt-0.5 font-semibold text-slate-900 dark:text-white">
              {eventoStatusLabel[evento.status]}
            </p>
          </div>
          <div>
            <p className="text-[10px] font-bold uppercase tracking-wider text-slate-400">Fechas</p>
            <p className="mt-0.5 font-semibold text-slate-900 dark:text-white">
              {formatDate(evento.startDate)}
              {evento.endDate && evento.endDate !== evento.startDate
                ? ` – ${formatDate(evento.endDate)}`
                : ''}
            </p>
          </div>
          <div>
            <p className="text-[10px] font-bold uppercase tracking-wider text-slate-400">
              Ubicación
            </p>
            <p className="mt-0.5 font-semibold text-slate-900 dark:text-white">
              {evento.cities.map((c) => `${c.name}, ${c.country}`).join(' · ') || 'Por definir'}
            </p>
          </div>
        </div>
      </Card>
      <InvitadosCard evento={evento} />
    </div>
  )
}
