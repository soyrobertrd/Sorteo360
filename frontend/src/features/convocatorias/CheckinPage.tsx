import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import {
  Search,
  UserCheck,
  Users,
  UserX,
  Percent,
  QrCode,
  Gift,
  Ban,
  ScanLine,
  CheckCircle2,
  Mail,
  CalendarDays,
  ChevronLeft,
  ChevronRight,
  Download,
  ShieldOff,
  MoreVertical,
  RotateCcw,
  UserMinus,
  Sparkles,
  ExternalLink,
} from 'lucide-react'
import jsPDF from 'jspdf'
import QRCode from 'qrcode'
import { Card } from '@/components/ui/Card'
import { Breadcrumbs } from '@/components/ui/Breadcrumbs'
import { Button } from '@/components/ui/Button'
import { StatCard } from '@/components/ui/StatCard'
import { ActionMenu } from '@/components/ui/ActionMenu'
import { IconButton } from '@/components/ui/IconButton'
import { MenuItem } from '@/components/ui/MenuItem'
import { DataTable } from '@/components/ui/DataTable'
import { Modal } from '@/components/ui/Modal'
import { Field, TextInput } from '@/components/ui/Field'
import { Spinner, ErrorNote, EmptyState } from '@/components/ui/Feedback'
import { ViewModeToggle } from '@/components/ui/ViewModeToggle'
import {
  CardGrid,
  GridCard,
  GridCardCta,
  GridCardStats,
  GridCardTag,
} from '@/components/ui/GridCard'
import { useEventosList } from '@/features/eventos/hooks'
import { BadgesGeneratorModal } from './components/BadgesGeneratorModal'
import { type Invitado, type ParticipantStatus, PARTICIPANT_STATUS_LABEL } from './mockParticipants'
import { addCampanaExtra, setEstadoInvitado } from './store'
import { formatDateRange } from '@/lib/format'
import { eventoStatusLabel } from '@/lib/labels'
import { useEventTrackings } from './tracking'
import {
  asignaAsientos,
  claveDe,
  distribucionDe,
  etiquetasDe,
  nombreDeMesa,
  textoCorto,
  puedeSentarse,
  textoAsiento,
  useMesasDeEvento,
  useSentables,
  sentarSiEsAutomatico,
  type Asiento,
  type Etiquetas,
} from './mesas'
import { useInvitadosDeEventos } from './participants'
import { AsignarMesaModal } from './components/asientos/Modales'
import { SectionHeader } from '@/components/ui/SectionHeader'

const CUENTA_COMO_REGISTRADO: ParticipantStatus[] = [
  'RSVP_YES',
  'REGISTERED',
  'CONFIRMED',
  'CHECKED_IN',
  'NO_SHOW',
  'RAFFLE_ELIGIBLE',
  'RAFFLE_WINNER',
]
const CUENTA_COMO_CONFIRMADO: ParticipantStatus[] = [
  'CONFIRMED',
  'CHECKED_IN',
  'RAFFLE_ELIGIBLE',
  'RAFFLE_WINNER',
]
const PUEDE_HACER_CHECKIN: ParticipantStatus[] = ['RSVP_YES', 'REGISTERED', 'CONFIRMED']

const cifra = (n: number | undefined) => (n == null ? '—' : n.toLocaleString('es-DO'))

/** `eventoIdFijo`: dentro de la página del evento en Convocatorias, sin migas ni título. */
export function CheckinPage({ eventoIdFijo }: { eventoIdFijo?: string } = {}) {
  const [searchParams, setSearchParams] = useSearchParams()
  const paramEventoId = searchParams.get('eventoId') ?? ''
  const { data, isLoading, error } = useEventosList({ page: 1, pageSize: 100 })
  const todosEventos = useMemo(() => data?.items ?? [], [data])
  const eventIds = useMemo(() => todosEventos.map((e) => e.id), [todosEventos])
  const trackings = useEventTrackings(eventIds)

  const [eventoId, setEventoId] = useState<string>(eventoIdFijo ?? paramEventoId)
  const [searchTerm, setSearchTerm] = useState('')
  const [vistaModo, setVistaModo] = useState<'tabla' | 'tarjetas'>('tabla')
  const [pagina, setPagina] = useState(1)
  const [porPagina, setPorPagina] = useState(10)

  const listaDeInvitados = useInvitadosDeEventos(todosEventos)
  const invitadosPorEvento = useMemo(() => {
    const porEvento = new Map<string, Invitado[]>()
    for (const i of listaDeInvitados.invitados) {
      porEvento.set(i.eventoId, [...(porEvento.get(i.eventoId) ?? []), i])
    }
    return porEvento
  }, [listaDeInvitados.invitados])
  const [ticketFor, setTicketFor] = useState<Invitado | null>(null)
  const [showScanner, setShowScanner] = useState(false)
  const [encuestaEnviada, setEncuestaEnviada] = useState(false)
  const [certificadosEnviados, setCertificadosEnviados] = useState(false)
  const [showBadgesModal, setShowBadgesModal] = useState(false)

  const { config: configMesas, asientos } = useMesasDeEvento(eventoId)
  // El montaje sale del evento elegido: sin mesas ni filas, el check-in no muestra asientos.
  const montaje = todosEventos.find((e) => e.id === eventoId)?.seatingLayout
  const et = etiquetasDe(montaje)
  const dist = distribucionDe(montaje)
  const asignaMesas = asignaAsientos(configMesas) && Boolean(dist) && dist !== 'de_pie'
  const [asignarPara, setAsignarPara] = useState<Invitado | null>(null)
  const asientoDe = (i: Invitado) => asientos[claveDe(i.email)]

  useEffect(() => {
    if (!eventoIdFijo) setEventoId(paramEventoId)
  }, [paramEventoId, eventoIdFijo])

  const handleSelectEvento = (id: string) => {
    setEventoId(id)
    setSearchParams(id ? { eventoId: id } : {})
    setSearchTerm('')
  }

  const filas = useMemo(() => {
    return todosEventos.map((ev, index) => {
      const tracking = trackings[index]?.data
      const listaInvitados = invitadosPorEvento.get(ev.id) ?? []
      const totalInvitados = listaInvitados.length
      const totalRegistrados = listaInvitados.filter((i) =>
        CUENTA_COMO_REGISTRADO.includes(i.status),
      ).length
      const totalConfirmados = listaInvitados.filter((i) =>
        CUENTA_COMO_CONFIRMADO.includes(i.status),
      ).length
      const totalCheckin = listaInvitados.filter((i) =>
        ['CHECKED_IN', 'RAFFLE_ELIGIBLE', 'RAFFLE_WINNER'].includes(i.status),
      ).length
      const totalNoShow = listaInvitados.filter((i) => i.status === 'NO_SHOW').length

      return {
        evento: ev,
        tracking,
        totalInvitados,
        totalRegistrados,
        totalConfirmados,
        totalCheckin,
        totalNoShow,
        capacidad: ev.invitedCount || 0,
      }
    })
  }, [todosEventos, trackings, invitadosPorEvento])

  const filtradasEventos = useMemo(() => {
    const termino = searchTerm.trim().toLowerCase()
    return filas.filter((f) => {
      const matchSearch = !termino || f.evento.name.toLowerCase().includes(termino)
      return matchSearch
    })
  }, [filas, searchTerm])

  const totalPaginas = Math.max(1, Math.ceil(filtradasEventos.length / porPagina))
  const inicioIndice = (pagina - 1) * porPagina
  const finIndice = Math.min(inicioIndice + porPagina, filtradasEventos.length)
  const filasPaginadas = useMemo(
    () => filtradasEventos.slice(inicioIndice, finIndice),
    [filtradasEventos, inicioIndice, finIndice],
  )

  const eventoSeleccionado = useMemo(() => {
    return todosEventos.find((e) => e.id === eventoId) ?? null
  }, [todosEventos, eventoId])

  const invitadosEventoSeleccionado = useMemo(
    () => (eventoSeleccionado ? (invitadosPorEvento.get(eventoSeleccionado.id) ?? []) : []),
    [eventoSeleccionado, invitadosPorEvento],
  )
  const { sentables } = useSentables(
    eventoId,
    invitadosEventoSeleccionado,
    !listaDeInvitados.isLoading,
  )

  const filtradosInvitados = useMemo(() => {
    return invitadosEventoSeleccionado.filter(
      (i) =>
        !searchTerm ||
        i.nombre.toLowerCase().includes(searchTerm.toLowerCase()) ||
        i.empresa.toLowerCase().includes(searchTerm.toLowerCase()) ||
        i.email.toLowerCase().includes(searchTerm.toLowerCase()),
    )
  }, [invitadosEventoSeleccionado, searchTerm])

  const registrados = invitadosEventoSeleccionado.filter((i) =>
    CUENTA_COMO_REGISTRADO.includes(i.status),
  ).length
  const confirmados = invitadosEventoSeleccionado.filter((i) =>
    CUENTA_COMO_CONFIRMADO.includes(i.status),
  ).length
  const checkedIn = invitadosEventoSeleccionado.filter(
    (i) =>
      i.status === 'CHECKED_IN' || i.status === 'RAFFLE_ELIGIBLE' || i.status === 'RAFFLE_WINNER',
  ).length
  const noShow = invitadosEventoSeleccionado.filter((i) => i.status === 'NO_SHOW').length
  const cancelados = invitadosEventoSeleccionado.filter((i) => i.status === 'CANCELLED').length
  const elegiblesRifa = invitadosEventoSeleccionado.filter((i) => i.status === 'RAFFLE_ELIGIBLE')
  const ganador = invitadosEventoSeleccionado.find((i) => i.status === 'RAFFLE_WINNER')
  const tasaAsistencia = registrados ? Math.round((checkedIn / registrados) * 100) : 0

  const marcarCheckin = (invitado: Invitado) => {
    setEstadoInvitado(invitado.id, 'CHECKED_IN')
    // En automático, quien se registró antes de configurar los asientos recibe uno al entrar.
    sentarSiEsAutomatico(
      eventoId,
      sentables.find((s) => claveDe(s.email) === claveDe(invitado.email)) ?? {
        email: invitado.email,
        empresa: invitado.empresa,
        categoria: invitado.tipo,
        fechaRegistro: invitado.fechaRegistro,
      },
    )
  }

  const elegirGanador = () => {
    if (elegiblesRifa.length === 0) return
    const elegido = elegiblesRifa[Math.floor(Math.random() * elegiblesRifa.length)]
    setEstadoInvitado(elegido.id, 'RAFFLE_WINNER')
  }

  const pendientesEscaneo = invitadosEventoSeleccionado.filter((i) =>
    PUEDE_HACER_CHECKIN.includes(i.status),
  )

  const enviarCertificadosAsistencia = () => {
    if (!eventoSeleccionado) return
    addCampanaExtra({
      id: `cert-asistencia-${eventoSeleccionado.id}-${Date.now()}`,
      eventoId: eventoSeleccionado.id,
      eventoNombre: eventoSeleccionado.name,
      tipo: 'Certificado de Asistencia',
      idioma: eventoSeleccionado.language?.toUpperCase() === 'EN' ? 'EN' : 'ES',
      fechaEnvio: new Date().toISOString().slice(0, 10),
      enviados: checkedIn,
      entregados: Math.round(checkedIn * 0.98),
      rebotados: Math.round(checkedIn * 0.02),
      reenviados: 0,
      estado: 'Enviada',
    })
    setCertificadosEnviados(true)
  }

  const enviarEncuestaPostEvento = () => {
    if (!eventoSeleccionado) return
    addCampanaExtra({
      id: `encuesta-${eventoSeleccionado.id}-${Date.now()}`,
      eventoId: eventoSeleccionado.id,
      eventoNombre: eventoSeleccionado.name,
      tipo: 'Encuesta Post Evento',
      idioma: eventoSeleccionado.language?.toUpperCase() === 'EN' ? 'EN' : 'ES',
      fechaEnvio: new Date().toISOString().slice(0, 10),
      enviados: checkedIn,
      entregados: Math.round(checkedIn * 0.96),
      rebotados: Math.round(checkedIn * 0.04),
      reenviados: 0,
      estado: 'Enviada',
    })
    setEncuestaEnviada(true)
  }

  if (isLoading) return <Spinner label="Cargando módulo de check-in…" />
  if (error != null) return <ErrorNote error={error} />

  // --- VISTA 1: DETALLE DE CHECK-IN PARA UN EVENTO SELECCIONADO ---
  if (eventoSeleccionado) {
    return (
      <div className={eventoIdFijo ? 'space-y-6' : 'mx-auto max-w-7xl space-y-6 pb-12'}>
        {!eventoIdFijo && (
          <Breadcrumbs
            items={[
              { label: 'Invitaciones', to: '/convocatorias' },
              { label: 'Check-in', to: '/convocatorias/checkin' },
              { label: eventoSeleccionado.name },
            ]}
          />
        )}

        {/* Cabecera del Evento */}
        {eventoIdFijo ? (
          <SectionHeader
            title="Check-in y acreditación"
            description="Escanea el QR de cada invitado al llegar, imprime gafetes y sigue la asistencia."
            actions={
              <div className="flex items-center gap-2.5">
                <Button
                  type="button"
                  onClick={() => setShowScanner(true)}
                  className="cursor-pointer font-bold bg-brand-deep text-white hover:bg-brand-deep/90 shadow-sm"
                >
                  <ScanLine size={15} className="mr-1.5" /> Escanear QR
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => setShowBadgesModal(true)}
                  disabled={invitadosEventoSeleccionado.length === 0}
                  className="cursor-pointer font-bold border-blue-200 dark:border-blue-900 text-blue-700 dark:text-blue-300 hover:bg-blue-50 dark:hover:bg-blue-950/40"
                >
                  <QrCode size={14} className="mr-1.5" />
                  Gafetes QR ({invitadosEventoSeleccionado.length})
                </Button>
              </div>
            }
          />
        ) : (
          <div className="flex flex-wrap items-center justify-between gap-4">
            <div>
              <h1 className="font-sans text-2xl font-extrabold tracking-tight text-slate-900 sm:text-3xl dark:text-white">
                {eventoSeleccionado.name}
              </h1>
              <p className="mt-0.5 text-xs text-slate-500 dark:text-slate-400 flex items-center gap-2">
                <span>
                  {formatDateRange(eventoSeleccionado.startDate, eventoSeleccionado.endDate)}
                </span>
              </p>
            </div>

            <div className="flex items-center gap-2.5">
              <Button
                type="button"
                onClick={() => setShowScanner(true)}
                className="cursor-pointer font-bold bg-brand-deep text-white hover:bg-brand-deep/90 shadow-sm"
              >
                <ScanLine size={15} className="mr-1.5" /> Escanear QR
              </Button>
              <Button
                type="button"
                variant="outline"
                onClick={() => setShowBadgesModal(true)}
                disabled={invitadosEventoSeleccionado.length === 0}
                className="cursor-pointer font-bold border-blue-200 dark:border-blue-900 text-blue-700 dark:text-blue-300 hover:bg-blue-50 dark:hover:bg-blue-950/40"
              >
                <QrCode size={14} className="mr-1.5" />
                Gafetes QR ({invitadosEventoSeleccionado.length})
              </Button>
            </div>
          </div>
        )}

        {/* Tarjetas de Métricas de Asistencia */}
        <div className="grid grid-cols-2 gap-3 sm:gap-4 lg:grid-cols-5">
          <StatCard icon={Users} label="Registrados" value={String(registrados)} color="blue" />
          <StatCard
            icon={UserCheck}
            label="Confirmados"
            value={String(confirmados)}
            color="violet"
          />
          <StatCard icon={QrCode} label="Check-in" value={String(checkedIn)} color="teal" />
          <StatCard icon={UserX} label="No asistió" value={String(noShow)} color="rose" />
          <StatCard
            icon={Percent}
            label="% Asistencia"
            value={`${tasaAsistencia}%`}
            color="emerald"
          />
        </div>

        {/* Post check-in: rifa y cancelaciones */}
        <Card className="p-5 bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 rounded-2xl shadow-2xs">
          <div className="flex flex-wrap items-center justify-between gap-4">
            <div className="flex items-center gap-3">
              <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-amber-50 text-amber-700 border border-amber-200 dark:bg-amber-950/50 dark:text-amber-300 dark:border-amber-900">
                <Gift size={18} />
              </div>
              <div>
                <p className="text-xs font-bold text-slate-900 dark:text-white">
                  {ganador
                    ? `Ganador: ${ganador.nombre}`
                    : `${elegiblesRifa.length} elegibles para rifa`}
                </p>
                <p className="text-[11px] text-slate-500 dark:text-slate-400">
                  Todos los asistentes con Check-in son elegibles automáticamente para sorteos.
                </p>
              </div>
            </div>
            <div className="flex flex-wrap items-center gap-3">
              <span className="inline-flex items-center gap-1.5 text-xs font-semibold text-slate-500">
                <Ban size={13} /> {cancelados} cancelados
              </span>

              {eventoSeleccionado.status === 'closed' && (
                <div className="flex items-center gap-2">
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={enviarCertificadosAsistencia}
                    disabled={certificadosEnviados || checkedIn === 0}
                    className="cursor-pointer font-bold border-emerald-200 dark:border-emerald-900 text-emerald-700 dark:text-emerald-300 hover:bg-emerald-50 dark:hover:bg-emerald-950/40"
                  >
                    <CheckCircle2 size={14} className="mr-1.5" />
                    {certificadosEnviados
                      ? 'Certificados emitidos'
                      : `Emitir Certificados (${checkedIn})`}
                  </Button>

                  <Button
                    size="sm"
                    variant="outline"
                    onClick={enviarEncuestaPostEvento}
                    disabled={encuestaEnviada || checkedIn === 0}
                    className="cursor-pointer font-bold border-slate-200 dark:border-slate-700"
                  >
                    <Mail size={14} className="mr-1.5" />
                    {encuestaEnviada ? 'Encuesta enviada' : 'Enviar encuesta'}
                  </Button>
                </div>
              )}
              <Button
                size="sm"
                onClick={elegirGanador}
                disabled={Boolean(ganador) || elegiblesRifa.length === 0}
                className="cursor-pointer font-bold bg-amber-600 text-white hover:bg-amber-700"
              >
                <Gift size={14} className="mr-1.5" />{' '}
                {ganador ? 'Ganador elegido' : 'Elegir ganador'}
              </Button>
            </div>
          </div>
        </Card>

        {/* Tabla de Invitados para Check-in con colores intercalados de empresa */}
        <Card className="bg-white p-6 dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 rounded-2xl shadow-2xs">
          <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
            <div className="relative max-w-sm flex-1">
              <Search
                size={16}
                className="absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400"
              />
              <input
                type="text"
                placeholder="Buscar por nombre, empresa o correo..."
                aria-label="Buscar por nombre, empresa o correo"
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                className="w-full rounded-lg border border-slate-200 bg-white py-2 pl-9 pr-3 text-xs text-slate-900 focus:border-blue-600 focus:outline-none dark:border-slate-700 dark:bg-slate-900 dark:text-white"
              />
            </div>
            <span className="text-xs font-bold text-slate-400">
              {filtradosInvitados.length} participantes encontrados
            </span>
          </div>

          {filtradosInvitados.length === 0 ? (
            <EmptyState>
              Sin participantes que coincidan con la búsqueda para este evento.
            </EmptyState>
          ) : (
            <DataTable<Invitado>
              rows={filtradosInvitados}
              getRowKey={(i) => i.id}
              columns={[
                {
                  key: 'nombre',
                  header: 'Nombre y Empresa',
                  cell: (i) => (
                    <div>
                      <p className="font-bold text-brand dark:text-blue-400">{i.nombre}</p>
                      <p className="text-[11px] text-slate-500">{i.empresa}</p>
                    </div>
                  ),
                },
                {
                  key: 'email',
                  header: 'Correo',
                  cellClassName: 'font-mono text-[11px] text-slate-700 dark:text-slate-300',
                  cell: (i) => i.email,
                },
                {
                  key: 'tipo',
                  header: 'Tipo',
                  cellClassName: 'font-semibold text-slate-700 dark:text-slate-300',
                  cell: (i) => i.tipo,
                },
                {
                  key: 'fechaInvitado',
                  header: 'Fecha invitado',
                  align: 'center',
                  cell: (i) => i.fechaInvitado || '—',
                },
                {
                  key: 'estado',
                  header: 'Estado',
                  align: 'center',
                  cellClassName: 'font-semibold text-slate-700 dark:text-slate-300 text-xs',
                  cell: (i) => PARTICIPANT_STATUS_LABEL[i.status] ?? i.status,
                },
                ...(asignaMesas
                  ? [
                      {
                        key: 'mesa',
                        header: `${et.unidad} / ${et.asiento}`,
                        align: 'center' as const,
                        cellClassName: 'text-xs font-semibold text-slate-700 dark:text-slate-300',
                        cell: (i: Invitado) => {
                          const asiento = asientoDe(i)
                          if (asiento)
                            return (
                              <span
                                title={textoAsiento(asiento, configMesas, et)}
                                className="inline-flex rounded-full bg-blue-50 px-2.5 py-0.5 text-[11px] font-black text-brand ring-1 ring-blue-200 dark:bg-blue-950/50 dark:text-blue-300 dark:ring-blue-900"
                              >
                                {textoCorto(asiento, et)}
                              </span>
                            )
                          return puedeSentarse(i) ? 'Sin asiento' : '—'
                        },
                      },
                    ]
                  : []),
                {
                  key: 'acciones',
                  header: 'Acciones',
                  align: 'right',
                  cell: (i) => (
                    <ParticipanteActionsMenu
                      invitado={i}
                      eventoId={eventoSeleccionado.id}
                      onCheckin={() => marcarCheckin(i)}
                      onVerTicket={() => setTicketFor(i)}
                      onCambiarEstado={(nuevoEstado) => setEstadoInvitado(i.id, nuevoEstado)}
                      onRevocar={() => setEstadoInvitado(i.id, 'CANCELLED')}
                      onAsignarMesa={
                        asignaMesas && puedeSentarse(i) ? () => setAsignarPara(i) : undefined
                      }
                    />
                  ),
                },
              ]}
            />
          )}
        </Card>

        {ticketFor && eventoSeleccionado && (
          <TicketModal
            invitado={ticketFor}
            eventoId={eventoSeleccionado.id}
            onClose={() => setTicketFor(null)}
            onRevoke={() => {
              setEstadoInvitado(ticketFor.id, 'CANCELLED')
              setTicketFor(null)
            }}
          />
        )}

        {showScanner && (
          <EscanerQrModal
            pendientes={pendientesEscaneo}
            onClose={() => setShowScanner(false)}
            onCheckin={(invitado) => {
              marcarCheckin(invitado)
            }}
            asientoDe={asignaMesas ? asientoDe : undefined}
            asientoLibre={configMesas.modo === 'libre'}
            nombreMesa={(mesa) => nombreDeMesa(configMesas, mesa, et)}
            etiquetas={et}
            onAsignarMesa={(invitado) => {
              setShowScanner(false)
              setAsignarPara(invitado)
            }}
          />
        )}

        {asignarPara && (
          <AsignarMesaModal
            eventoId={eventoSeleccionado.id}
            montaje={montaje}
            invitado={asignarPara}
            registrados={invitadosEventoSeleccionado}
            onClose={() => setAsignarPara(null)}
          />
        )}

        {showBadgesModal && eventoSeleccionado && (
          <BadgesGeneratorModal
            isOpen={showBadgesModal}
            onClose={() => setShowBadgesModal(false)}
            eventoNombre={eventoSeleccionado.name}
            invitados={invitadosEventoSeleccionado}
          />
        )}
      </div>
    )
  }

  // --- VISTA 2: LISTADO DE EVENTOS (CATÁLOGO EN TABLA O TARJETAS CON DOS COLORES INTERCALADOS) ---
  return (
    <div className="mx-auto max-w-7xl space-y-6 pb-12">
      <Breadcrumbs
        items={[{ label: 'Invitaciones', to: '/convocatorias' }, { label: 'Check-in' }]}
      />

      {/* Cabecera Principal */}
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="font-sans text-2xl font-extrabold tracking-tight text-slate-900 sm:text-3xl dark:text-white">
            Check-in y Acreditación de Eventos
          </h1>
          <p className="mt-1 text-xs text-slate-500 sm:text-sm dark:text-slate-400">
            Selecciona un evento para gestionar la acreditación de invitados, escaneo de pases QR y
            registro de asistencia en tiempo real.
          </p>
        </div>
      </div>

      {/* Card de Lista / Tabla de Eventos */}
      <Card className="rounded-3xl border border-slate-200/80 bg-white p-6 shadow-2xs dark:border-slate-800 dark:bg-slate-900">
        {/* Barra de Filtros, Búsqueda y Selector de Vista */}
        <div className="mb-5 flex flex-wrap items-center justify-between gap-3">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 flex-1 min-w-70">
            <div className="relative">
              <Search
                size={16}
                className="absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400"
              />
              <input
                type="search"
                placeholder="Buscar evento u oficina/sede…"
                aria-label="Buscar evento u oficina/sede"
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                className="w-full rounded-xl border border-slate-200 bg-slate-50 py-2 pl-9 pr-4 text-xs text-slate-900 focus:border-blue-600 focus:outline-none dark:border-slate-700 dark:bg-slate-800 dark:text-white"
              />
            </div>
          </div>

          <div className="shrink-0">
            <ViewModeToggle
              value={vistaModo}
              onChange={setVistaModo}
              listKey="tabla"
              gridKey="tarjetas"
              size="sm"
            />
          </div>
        </div>

        {filtradasEventos.length === 0 ? (
          <EmptyState>No se encontraron eventos con los filtros seleccionados.</EmptyState>
        ) : vistaModo === 'tabla' ? (
          <DataTable<(typeof filasPaginadas)[0]>
            rows={filasPaginadas}
            getRowKey={(f) => f.evento.id}
            onRowClick={(f) => handleSelectEvento(f.evento.id)}
            columns={[
              {
                key: 'evento',
                header: 'Evento y Fechas',
                cell: (f) => (
                  <div>
                    <span className="font-bold text-brand hover:underline dark:text-blue-400">
                      {f.evento.name}
                    </span>
                    <p className="mt-0.5 flex flex-wrap items-center gap-x-2 text-[11px] text-slate-500">
                      <span className="inline-flex items-center gap-1">
                        <CalendarDays size={11} />
                        {formatDateRange(f.evento.startDate, f.evento.endDate)}
                      </span>
                    </p>
                  </div>
                ),
              },
              {
                key: 'estado',
                header: 'Estado',
                align: 'center',
                cellClassName: 'font-semibold text-slate-700 dark:text-slate-300 text-xs',
                cell: (f) => eventoStatusLabel[f.evento.status] ?? f.evento.status,
              },
              {
                key: 'capacidad',
                header: 'Capacidad',
                align: 'center',
                cellClassName: 'font-bold text-slate-700 dark:text-slate-300',
                cell: (f) => cifra(f.capacidad),
              },
              {
                key: 'registrados',
                header: 'Registrados',
                align: 'center',
                cellClassName: 'font-bold text-blue-700 dark:text-blue-400',
                cell: (f) => cifra(f.totalRegistrados),
              },
              {
                key: 'confirmados',
                header: 'Confirmados',
                align: 'center',
                cellClassName: 'font-bold text-violet-700 dark:text-violet-400',
                cell: (f) => cifra(f.totalConfirmados),
              },
              {
                key: 'checkin',
                header: 'Check-in',
                align: 'center',
                cellClassName: 'font-extrabold text-teal-600 dark:text-teal-400',
                cell: (f) => cifra(f.totalCheckin),
              },
              {
                key: 'accion',
                header: 'Acciones',
                align: 'right',
                cell: (f) => (
                  <EventoCheckinActionsMenu
                    eventoId={f.evento.id}
                    onHacerCheckin={() => handleSelectEvento(f.evento.id)}
                  />
                ),
              },
            ]}
          />
        ) : (
          /* Modo Cuadrícula de Tarjetas con bordes de color estilo Empresas */
          <CardGrid>
            {filasPaginadas.map((f) => (
              <GridCard
                key={f.evento.id}
                onClick={() => handleSelectEvento(f.evento.id)}
                titleLines={2}
                tags={
                  <GridCardTag tone="slate" className="normal-case tracking-normal">
                    {eventoStatusLabel[f.evento.status]}
                  </GridCardTag>
                }
                title={f.evento.name}
                subtitle={
                  <span className="flex items-center gap-1">
                    <CalendarDays size={12} className="shrink-0" />
                    {formatDateRange(f.evento.startDate, f.evento.endDate)}
                  </span>
                }
                footerStart={
                  <span className="text-xs font-semibold text-slate-500">
                    Confirmados:{' '}
                    <strong className="text-violet-600">{cifra(f.totalConfirmados)}</strong>
                  </span>
                }
                footerEnd={<GridCardCta variant="link">Hacer Check-in →</GridCardCta>}
              >
                <GridCardStats
                  items={[
                    { label: 'Capacidad', value: cifra(f.capacidad) },
                    { label: 'Registrados', value: cifra(f.totalRegistrados), tone: 'blue' },
                    { label: 'Check-in', value: cifra(f.totalCheckin), tone: 'teal' },
                  ]}
                />
              </GridCard>
            ))}
          </CardGrid>
        )}

        {/* Barra de Paginación */}
        {filtradasEventos.length > 0 && (
          <div className="mt-6 flex flex-wrap items-center justify-between gap-4 border-t border-slate-100 pt-4 text-xs text-slate-500 dark:border-slate-800 dark:text-slate-400">
            <div className="flex flex-wrap items-center gap-3">
              <span>
                Mostrando{' '}
                <strong className="font-semibold text-slate-900 dark:text-white">
                  {inicioIndice + 1}–{finIndice}
                </strong>{' '}
                de{' '}
                <strong className="font-semibold text-slate-900 dark:text-white">
                  {filtradasEventos.length}
                </strong>{' '}
                eventos
              </span>
              <div className="flex items-center gap-1.5 border-l border-slate-200 pl-3 dark:border-slate-700">
                <span>Registros por página:</span>
                <select
                  aria-label="Registros por página"
                  value={porPagina}
                  onChange={(e) => {
                    setPorPagina(Number(e.target.value))
                    setPagina(1)
                  }}
                  className="rounded-lg border border-slate-200 bg-white px-2.5 py-1 text-xs font-bold text-slate-800 transition-colors focus:border-blue-600 focus:outline-none dark:border-slate-700 dark:bg-slate-800 dark:text-slate-200 cursor-pointer"
                >
                  <option value={10}>10 por página</option>
                  <option value={20}>20 por página</option>
                  <option value={25}>25 por página</option>
                  <option value={50}>50 por página</option>
                  <option value={100}>100 por página</option>
                </select>
              </div>
            </div>

            <div className="flex items-center gap-1.5">
              <button
                type="button"
                onClick={() => setPagina((p) => Math.max(1, p - 1))}
                disabled={pagina <= 1}
                className="inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-2.5 py-1 font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-40 disabled:cursor-not-allowed dark:border-slate-700 dark:bg-slate-800 dark:text-slate-300 dark:hover:bg-slate-700 cursor-pointer"
              >
                <ChevronLeft size={14} />
                <span>Anterior</span>
              </button>

              <div className="flex items-center gap-1 px-1">
                {Array.from({ length: totalPaginas }, (_, i) => i + 1).map((num) => (
                  <button
                    key={num}
                    type="button"
                    onClick={() => setPagina(num)}
                    className={`h-7 w-7 rounded-lg text-xs font-bold transition-colors cursor-pointer ${
                      pagina === num
                        ? 'bg-brand-deep text-white shadow-2xs'
                        : 'border border-slate-200 bg-white text-slate-700 hover:bg-slate-50 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-300'
                    }`}
                  >
                    {num}
                  </button>
                ))}
              </div>

              <button
                type="button"
                onClick={() => setPagina((p) => Math.min(totalPaginas, p + 1))}
                disabled={pagina >= totalPaginas}
                className="inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-2.5 py-1 font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-40 disabled:cursor-not-allowed dark:border-slate-700 dark:bg-slate-800 dark:text-slate-300 dark:hover:bg-slate-700 cursor-pointer"
              >
                <span>Siguiente</span>
                <ChevronRight size={14} />
              </button>
            </div>
          </div>
        )}
      </Card>
    </div>
  )
}

function EscanerQrModal({
  pendientes,
  onClose,
  onCheckin,
  asientoDe,
  asientoLibre,
  nombreMesa,
  etiquetas,
  onAsignarMesa,
}: {
  pendientes: Invitado[]
  onClose: () => void
  onCheckin: (invitado: Invitado) => void
  /** Solo en un evento que asigna mesa: dónde se sienta cada persona. */
  asientoDe?: (invitado: Invitado) => Asiento | undefined
  asientoLibre: boolean
  nombreMesa: (mesa: number) => string
  etiquetas: Etiquetas
  onAsignarMesa: (invitado: Invitado) => void
}) {
  const [ticketIdManual, setTicketIdManual] = useState('')
  const [resultado, setResultado] = useState<{ invitado: Invitado; ok: boolean } | null>(null)

  const validar = (invitado: Invitado | undefined) => {
    if (!invitado) {
      setResultado(null)
      return
    }
    onCheckin(invitado)
    setResultado({ invitado, ok: true })
  }

  const handleSimularEscaneo = () => {
    if (pendientes.length === 0) return
    const elegido = pendientes[Math.floor(Math.random() * pendientes.length)]
    validar(elegido)
  }

  const handleValidarManual = () => {
    const codigo = ticketIdManual.trim().toLowerCase()
    const invitado = pendientes.find(
      (i) => i.id.toLowerCase() === codigo || i.email.toLowerCase() === codigo,
    )
    if (!invitado) {
      setResultado(null)
      return
    }
    validar(invitado)
  }

  return (
    <Modal title="Escáner de check-in" onClose={onClose}>
      <div className="space-y-4 text-center text-xs">
        <div className="relative mx-auto flex h-56 w-56 items-center justify-center overflow-hidden rounded-2xl border-2 border-dashed border-blue-400 bg-slate-900">
          <div className="absolute inset-x-6 top-1/2 h-0.5 -translate-y-1/2 animate-pulse bg-blue-400" />
          <ScanLine size={64} className="text-blue-400/70" />
        </div>
        <p className="text-slate-500">
          Apunta la cámara al código QR del pase digital del invitado. En este entorno de demo, el
          escaneo se simula.
        </p>

        {resultado && (
          <div
            className={`flex items-center justify-center gap-2 rounded-xl p-3 font-bold ${
              resultado.ok
                ? 'bg-emerald-50 text-emerald-800 dark:bg-emerald-950/60 dark:text-emerald-300'
                : 'bg-rose-50 text-rose-800 dark:bg-rose-950/60 dark:text-rose-300'
            }`}
          >
            <CheckCircle2 size={16} /> Check-in registrado: {resultado.invitado.nombre}
          </div>
        )}

        {resultado && asientoDe && (
          <AsientoAlEscanear
            asiento={asientoDe(resultado.invitado)}
            asientoLibre={asientoLibre}
            nombreMesa={nombreMesa}
            etiquetas={etiquetas}
            onAsignar={() => onAsignarMesa(resultado.invitado)}
          />
        )}

        <Button
          type="button"
          onClick={handleSimularEscaneo}
          disabled={pendientes.length === 0}
          className="w-full cursor-pointer bg-brand-deep font-bold text-white hover:bg-brand-deep/90"
        >
          <ScanLine size={15} className="mr-1.5" />
          {pendientes.length === 0
            ? 'Sin invitados pendientes de check-in'
            : 'Simular escaneo exitoso'}
        </Button>

        <div className="border-t border-slate-100 pt-3 text-left dark:border-slate-800">
          <Field label="O ingresa el ID del ticket manualmente">
            <div className="flex gap-2">
              <TextInput
                value={ticketIdManual}
                onChange={(e) => setTicketIdManual(e.target.value)}
                placeholder="Código del ticket o correo"
              />
              <Button
                type="button"
                variant="outline"
                onClick={handleValidarManual}
                className="shrink-0 cursor-pointer"
              >
                Validar
              </Button>
            </div>
          </Field>
        </div>

        <Button type="button" variant="secondary" onClick={onClose} className="cursor-pointer">
          Cerrar
        </Button>
      </div>
    </Modal>
  )
}

/** Lo que ve quien escanea: la mesa y la silla en grande, o que la persona no tiene. */
function AsientoAlEscanear({
  asiento,
  asientoLibre,
  nombreMesa,
  etiquetas,
  onAsignar,
}: {
  asiento: Asiento | undefined
  /** Asiento libre fuera de la reserva: quien no es de protocolo no recibe uno. */
  asientoLibre: boolean
  nombreMesa: (mesa: number) => string
  etiquetas: Etiquetas
  onAsignar: () => void
}) {
  if (asiento) {
    return (
      <div className="grid grid-cols-2 gap-3 rounded-2xl border-2 border-brand/20 bg-blue-50 p-4 dark:border-blue-900 dark:bg-blue-950/40">
        <div>
          <p className="text-[10px] font-bold uppercase tracking-wider text-slate-500">
            {etiquetas.unidad}
          </p>
          <p className="text-4xl font-black text-brand dark:text-blue-300">{asiento.mesa}</p>
          {nombreMesa(asiento.mesa) !== `${etiquetas.unidad} ${asiento.mesa}` && (
            <p className="text-[11px] font-bold text-slate-600 dark:text-slate-300">
              {nombreMesa(asiento.mesa)}
            </p>
          )}
        </div>
        <div>
          <p className="text-[10px] font-bold uppercase tracking-wider text-slate-500">
            {etiquetas.asiento}
          </p>
          <p className="text-4xl font-black text-brand dark:text-blue-300">{asiento.silla}</p>
        </div>
      </div>
    )
  }
  if (asientoLibre) {
    return (
      <div className="rounded-xl bg-slate-100 p-3 text-left font-bold text-slate-700 dark:bg-slate-800 dark:text-slate-200">
        Asiento libre: puede sentarse donde quiera, fuera del área VIP.
      </div>
    )
  }
  return (
    <div className="flex items-center justify-between gap-2 rounded-xl bg-amber-50 p-3 text-left font-bold text-amber-800 dark:bg-amber-950/50 dark:text-amber-300">
      Sin asiento asignado
      <Button type="button" size="sm" variant="outline" onClick={onAsignar}>
        Asignar asiento
      </Button>
    </div>
  )
}

function TicketModal({
  invitado,
  eventoId,
  onClose,
  onRevoke,
}: {
  invitado: Invitado
  eventoId: string
  onClose: () => void
  onRevoke: () => void
}) {
  const payload = useMemo(
    () =>
      JSON.stringify({
        ticket_id: invitado.id,
        participant_id: invitado.id,
        event_id: eventoId,
        timestamp: new Date().toISOString(),
      }),
    [invitado.id, eventoId],
  )
  const [qrSrc, setQrSrc] = useState('')
  useEffect(() => {
    let vigente = true
    QRCode.toDataURL(payload, { width: 220, margin: 1 })
      .then((url) => vigente && setQrSrc(url))
      .catch(() => vigente && setQrSrc(''))
    return () => {
      vigente = false
    }
  }, [payload])

  const handleDescargarBadge = async () => {
    const doc = new jsPDF({ format: [100, 150] })
    doc.setFillColor(4, 17, 77)
    doc.rect(0, 0, 100, 28, 'F')
    doc.setTextColor(255, 255, 255)
    doc.setFontSize(8)
    doc.text('ACREDITACIÓN DE EVENTO', 8, 10)
    doc.setFontSize(11)
    doc.text('Badge de acreditación', 8, 20, { maxWidth: 84 })

    try {
      doc.addImage(
        await QRCode.toDataURL(payload, { width: 220, margin: 1 }),
        'PNG',
        20,
        34,
        60,
        60,
      )
    } catch {
      // fallback
    }

    doc.setTextColor(20, 20, 20)
    doc.setFontSize(12)
    doc.text(invitado.nombre, 50, 102, { align: 'center', maxWidth: 90 })
    doc.setFontSize(9)
    doc.text(invitado.empresa, 50, 110, { align: 'center', maxWidth: 90 })
    doc.setFontSize(8)
    doc.text(invitado.tipo, 50, 117, { align: 'center' })
    doc.setFontSize(7)
    doc.text(invitado.id, 50, 130, { align: 'center' })

    doc.save(`badge-${invitado.id}.pdf`)
  }

  return (
    <Modal title={`Ticket · ${invitado.nombre}`} onClose={onClose}>
      <div className="space-y-4 text-center">
        <img
          src={qrSrc || undefined}
          alt="Código QR del ticket"
          className="mx-auto h-56 w-56 rounded-xl border border-slate-200 dark:border-slate-700"
        />
        <div className="rounded-xl bg-slate-50 p-3 text-left text-xs dark:bg-slate-800">
          <p>
            <strong className="text-slate-500">Participante:</strong> {invitado.nombre}
          </p>
          <p>
            <strong className="text-slate-500">Empresa:</strong> {invitado.empresa}
          </p>
          <p>
            <strong className="text-slate-500">Tipo:</strong> {invitado.tipo}
          </p>
          <p>
            <strong className="text-slate-500">Ticket ID:</strong>{' '}
            <span className="font-mono">{invitado.id}</span>
          </p>
        </div>
        <p className="text-[11px] text-slate-400">
          QR único, firmado y no falsificable (simulado) — listo para el escáner de check-in.
        </p>
        <div className="flex flex-wrap justify-center gap-2 border-t border-slate-100 pt-4 dark:border-slate-800">
          <Button variant="secondary" onClick={onClose} className="cursor-pointer">
            Cerrar
          </Button>
          <Button variant="outline" onClick={handleDescargarBadge} className="cursor-pointer">
            <Download size={14} className="mr-1.5" /> Descargar badge PDF
          </Button>
          <Button variant="destructive" onClick={onRevoke} className="cursor-pointer">
            <ShieldOff size={14} className="mr-1.5" /> Revocar ticket
          </Button>
        </div>
      </div>
    </Modal>
  )
}

/** Menú dropdown de 3 puntos con acciones completas de acreditación y gestión del participante. */
function ParticipanteActionsMenu({
  invitado,
  onCheckin,
  onVerTicket,
  onCambiarEstado,
  onRevocar,
  onAsignarMesa,
}: {
  invitado: Invitado
  eventoId: string
  onCheckin: () => void
  onVerTicket: () => void
  onCambiarEstado: (estado: ParticipantStatus) => void
  onRevocar: () => void
  /** Solo en un evento que asigna mesa. */
  onAsignarMesa?: () => void
}) {
  const [open, setOpen] = useState(false)
  const anchorRef = useRef<HTMLButtonElement>(null)
  const isCheckedIn = ['CHECKED_IN', 'RAFFLE_ELIGIBLE', 'RAFFLE_WINNER'].includes(invitado.status)
  const puedeCheckin = PUEDE_HACER_CHECKIN.includes(invitado.status)

  return (
    <div className="relative inline-block" onClick={(e) => e.stopPropagation()}>
      <IconButton
        ref={anchorRef}
        onClick={() => setOpen((prev) => !prev)}
        aria-label="Acciones del participante"
        title="Acciones del participante"
      >
        <MoreVertical size={16} />
      </IconButton>

      <ActionMenu open={open} onClose={() => setOpen(false)} anchorRef={anchorRef} className="w-56">
        {puedeCheckin && (
          <MenuItem
            onClick={() => {
              setOpen(false)
              onCheckin()
            }}
            className="text-teal-700 hover:bg-teal-50 dark:text-teal-300 dark:hover:bg-teal-950/40 font-bold"
          >
            <UserCheck size={14} className="text-teal-600" /> Realizar Check-in
          </MenuItem>
        )}

        {onAsignarMesa && (
          <MenuItem
            onClick={() => {
              setOpen(false)
              onAsignarMesa()
            }}
          >
            Asignar asiento
          </MenuItem>
        )}

        {invitado.status !== 'CANCELLED' && invitado.status !== 'RSVP_NO' && (
          <MenuItem
            onClick={() => {
              setOpen(false)
              onVerTicket()
            }}
          >
            <QrCode size={14} className="text-blue-600" /> Ver Ticket QR / Badge
          </MenuItem>
        )}

        {isCheckedIn && invitado.status !== 'RAFFLE_WINNER' && (
          <MenuItem
            onClick={() => {
              setOpen(false)
              onCambiarEstado('RAFFLE_WINNER')
            }}
            className="text-amber-700 hover:bg-amber-50 dark:text-amber-300 dark:hover:bg-amber-950/40"
          >
            <Sparkles size={14} className="text-amber-600" /> Marcar como Ganador de Rifa
          </MenuItem>
        )}

        {isCheckedIn && (
          <MenuItem
            onClick={() => {
              setOpen(false)
              onCambiarEstado('CONFIRMED')
            }}
            className="text-slate-600 hover:bg-slate-50 dark:text-slate-300 dark:hover:bg-slate-800"
          >
            <RotateCcw size={14} className="text-slate-500" /> Anular Check-in (Revertir)
          </MenuItem>
        )}

        {invitado.status !== 'NO_SHOW' && !isCheckedIn && (
          <MenuItem
            onClick={() => {
              setOpen(false)
              onCambiarEstado('NO_SHOW')
            }}
            className="text-rose-700 hover:bg-rose-50 dark:text-rose-300 dark:hover:bg-rose-950/40"
          >
            <UserX size={14} className="text-rose-600" /> Marcar No Show
          </MenuItem>
        )}

        {invitado.status !== 'CANCELLED' && (
          <MenuItem
            tone="danger"
            onClick={() => {
              setOpen(false)
              onRevocar()
            }}
          >
            <UserMinus size={14} /> Cancelar / Revocar invitación
          </MenuItem>
        )}
      </ActionMenu>
    </div>
  )
}

/** Menú dropdown con tres puntos para acciones del evento en el catálogo de check-in. */
function EventoCheckinActionsMenu({
  eventoId,
  onHacerCheckin,
}: {
  eventoId: string
  onHacerCheckin: () => void
}) {
  const navigate = useNavigate()
  const [open, setOpen] = useState(false)
  const anchorRef = useRef<HTMLButtonElement>(null)

  return (
    <div className="relative inline-block" onClick={(e) => e.stopPropagation()}>
      <IconButton
        ref={anchorRef}
        onClick={() => setOpen((prev) => !prev)}
        aria-label="Opciones del evento"
        title="Opciones del evento"
      >
        <MoreVertical size={16} />
      </IconButton>

      <ActionMenu open={open} onClose={() => setOpen(false)} anchorRef={anchorRef} className="w-56">
        <MenuItem
          onClick={() => {
            setOpen(false)
            onHacerCheckin()
          }}
          className="text-teal-700 hover:bg-teal-50 dark:text-teal-300 dark:hover:bg-teal-950/40 font-bold"
        >
          <UserCheck size={14} className="text-teal-600" /> Gestionar Check-in
        </MenuItem>

        <MenuItem
          onClick={() => {
            setOpen(false)
            navigate(`/convocatorias/invitados/${eventoId}`)
          }}
        >
          <Users size={14} className="text-blue-600" /> Ver Lista de Invitados
        </MenuItem>

        <MenuItem
          onClick={() => {
            setOpen(false)
            navigate(`/eventos/${eventoId}`)
          }}
        >
          <ExternalLink size={14} className="text-purple-600" /> Ver Ficha del Evento
        </MenuItem>
      </ActionMenu>
    </div>
  )
}
