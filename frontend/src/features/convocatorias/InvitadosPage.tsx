import { useMemo, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  CalendarDays,
  ChevronLeft,
  ChevronRight,
  Mail,
  Search,
  UserCheck,
  Users,
  MoreVertical,
  ExternalLink,
  QrCode,
} from 'lucide-react'
import { Breadcrumbs } from '@/components/ui/Breadcrumbs'
import { Card } from '@/components/ui/Card'
import { EmptyState, ErrorNote, Spinner } from '@/components/ui/Feedback'
import { DataTable } from '@/components/ui/DataTable'
import { ActionMenu } from '@/components/ui/ActionMenu'
import { IconButton } from '@/components/ui/IconButton'
import { MenuItem } from '@/components/ui/MenuItem'
import { ViewModeToggle } from '@/components/ui/ViewModeToggle'
import {
  CardGrid,
  GridCard,
  GridCardCta,
  GridCardStats,
  GridCardTag,
} from '@/components/ui/GridCard'
import { StatCard } from '@/components/ui/StatCard'
import { useEventosList } from '@/features/eventos/hooks'
import { formatDateRange } from '@/lib/format'
import { eventoStatusLabel } from '@/lib/labels'
import { invitadosFor } from './mockParticipants'
import { useCheckinsOverrides, useRegistrosPublicos } from './store'
import { useEventTrackings } from './tracking'

const cifra = (n: number | undefined) => (n == null ? '—' : n.toLocaleString('es-DO'))

export function InvitadosPage() {
  const navigate = useNavigate()
  const [searchTerm, setSearchTerm] = useState('')
  const [vistaModo, setVistaModo] = useState<'tabla' | 'tarjetas'>('tabla')
  const [pagina, setPagina] = useState(1)
  const [porPagina, setPorPagina] = useState(10)

  const eventosQuery = useEventosList({ page: 1, pageSize: 100 })
  const eventos = useMemo(() => eventosQuery.data?.items ?? [], [eventosQuery.data])
  const eventIds = useMemo(() => eventos.map((e) => e.id), [eventos])
  const trackings = useEventTrackings(eventIds)

  const registrosPublicos = useRegistrosPublicos()
  const checkinsOverrides = useCheckinsOverrides()

  const filas = useMemo(() => {
    return eventos.map((evento, index) => {
      const tracking = trackings[index]?.data
      const propios = registrosPublicos.filter((r) => r.eventoId === evento.id)
      const listaInvitados = [...propios, ...invitadosFor(evento)].map((i) =>
        checkinsOverrides[i.id] ? { ...i, status: checkinsOverrides[i.id] } : i,
      )
      const totalInvitados = listaInvitados.length
      const totalRegistrados = listaInvitados.filter((i) =>
        ['RSVP_YES', 'REGISTERED', 'CONFIRMED', 'CHECKED_IN'].includes(i.status),
      ).length
      const totalCheckin = listaInvitados.filter((i) =>
        ['CHECKED_IN', 'RAFFLE_ELIGIBLE', 'RAFFLE_WINNER'].includes(i.status),
      ).length

      return {
        evento,
        tracking,
        totalInvitados,
        totalRegistrados,
        totalCheckin,
        capacidad: evento.invitedCount || 0,
      }
    })
  }, [eventos, trackings, registrosPublicos, checkinsOverrides])

  const filtradas = useMemo(() => {
    const termino = searchTerm.trim().toLowerCase()
    return filas.filter((f) => {
      const matchSearch = !termino || f.evento.name.toLowerCase().includes(termino)
      return matchSearch
    })
  }, [filas, searchTerm])

  const totalPaginas = Math.max(1, Math.ceil(filtradas.length / porPagina))
  const inicioIndice = (pagina - 1) * porPagina
  const finIndice = Math.min(inicioIndice + porPagina, filtradas.length)
  const filasPaginadas = useMemo(
    () => filtradas.slice(inicioIndice, finIndice),
    [filtradas, inicioIndice, finIndice],
  )

  const metrics = useMemo(() => {
    const totalEventos = filas.length
    const totalCapacidad = filas.reduce((acc, f) => acc + f.capacidad, 0)
    const totalInvitados = filas.reduce((acc, f) => acc + f.totalInvitados, 0)
    const totalRegistrados = filas.reduce((acc, f) => acc + f.totalRegistrados, 0)
    return { totalEventos, totalCapacidad, totalInvitados, totalRegistrados }
  }, [filas])

  if (eventosQuery.isLoading) return <Spinner label="Cargando directorio de eventos..." />
  if (eventosQuery.error != null) return <ErrorNote error={eventosQuery.error} />

  return (
    <div className="mx-auto max-w-7xl space-y-6 pb-12">
      <Breadcrumbs
        items={[{ label: 'Invitaciones', to: '/convocatorias' }, { label: 'Invitados' }]}
      />

      {/* Cabecera Principal */}
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="font-sans text-2xl font-extrabold tracking-tight text-slate-900 sm:text-3xl dark:text-white">
            Invitados y Acreditación por evento
          </h1>
          <p className="mt-1 text-xs text-slate-500 sm:text-sm dark:text-slate-400">
            Explora los eventos institucionales y haz clic en cualquiera para consultar su lista de
            invitados, cupos por segmento y pases QR.
          </p>
        </div>
      </div>

      {/* Tarjetas de Métricas Resumen */}
      <div className="grid grid-cols-2 gap-3 sm:gap-4 lg:grid-cols-4">
        <StatCard
          icon={CalendarDays}
          label="Total Eventos"
          value={metrics.totalEventos}
          color="blue"
        />
        <StatCard
          icon={Users}
          label="Capacidad Total"
          value={cifra(metrics.totalCapacidad)}
          color="violet"
        />
        <StatCard
          icon={Mail}
          label="Total Invitados"
          value={cifra(metrics.totalInvitados)}
          color="amber"
        />
        <StatCard
          icon={UserCheck}
          label="Total Registrados / RSVP"
          value={cifra(metrics.totalRegistrados)}
          color="emerald"
        />
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

        {filtradas.length === 0 ? (
          <EmptyState>No se encontraron eventos con los filtros seleccionados.</EmptyState>
        ) : vistaModo === 'tabla' ? (
          <DataTable
            rows={filasPaginadas}
            getRowKey={(f) => f.evento.id}
            onRowClick={(f) => navigate(`/convocatorias/invitados/${f.evento.id}`)}
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
                cell: (f) => (
                  <span className="rounded-md bg-slate-100 px-2 py-0.5 text-[10px] font-extrabold text-slate-700 dark:bg-slate-800 dark:text-slate-300">
                    {eventoStatusLabel[f.evento.status]}
                  </span>
                ),
              },
              {
                key: 'capacidad',
                header: 'Capacidad',
                align: 'center',
                cell: (f) => (
                  <span className="font-bold text-slate-700 dark:text-slate-300">
                    {cifra(f.capacidad)}
                  </span>
                ),
              },
              {
                key: 'invitaciones',
                header: 'Invitaciones',
                align: 'center',
                cell: (f) => (
                  <span className="font-bold text-violet-700 dark:text-violet-400">
                    {cifra(f.totalInvitados)}
                  </span>
                ),
              },
              {
                key: 'registrados',
                header: 'Registrados',
                align: 'center',
                cell: (f) => (
                  <span className="font-bold text-emerald-600 dark:text-emerald-400">
                    {cifra(f.totalRegistrados)}
                  </span>
                ),
              },
              {
                key: 'checkin',
                header: 'Check-in',
                align: 'center',
                cell: (f) => (
                  <span className="font-bold text-teal-600 dark:text-teal-400">
                    {cifra(f.totalCheckin)}
                  </span>
                ),
              },
              {
                key: 'accion',
                header: 'Acciones',
                align: 'right',
                cell: (f) => (
                  <EventoInvitadosActionsMenu
                    eventoId={f.evento.id}
                    onVerInvitados={() => navigate(`/convocatorias/invitados/${f.evento.id}`)}
                  />
                ),
              },
            ]}
          />
        ) : (
          /* Modo Cuadrícula de Tarjetas */
          <CardGrid>
            {filasPaginadas.map((f) => (
              <GridCard
                key={f.evento.id}
                onClick={() => navigate(`/convocatorias/invitados/${f.evento.id}`)}
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
                    Check-in: <strong className="text-teal-600">{cifra(f.totalCheckin)}</strong>
                  </span>
                }
                footerEnd={<GridCardCta variant="link">Ver detalle de invitados →</GridCardCta>}
              >
                <GridCardStats
                  items={[
                    { label: 'Capacidad', value: cifra(f.capacidad) },
                    { label: 'Invitados', value: cifra(f.totalInvitados), tone: 'violet' },
                    { label: 'Registrados', value: cifra(f.totalRegistrados), tone: 'emerald' },
                  ]}
                />
              </GridCard>
            ))}
          </CardGrid>
        )}

        {/* Barra de Paginación */}
        {filtradas.length > 0 && (
          <div className="mt-6 flex flex-wrap items-center justify-between gap-4 border-t border-slate-100 pt-4 text-xs text-slate-500 dark:border-slate-800 dark:text-slate-400">
            <div className="flex flex-wrap items-center gap-3">
              <span>
                Mostrando{' '}
                <strong className="font-semibold text-slate-900 dark:text-white">
                  {inicioIndice + 1}–{finIndice}
                </strong>{' '}
                de{' '}
                <strong className="font-semibold text-slate-900 dark:text-white">
                  {filtradas.length}
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

/** Menú dropdown con tres puntos para acciones del evento en el catálogo de invitados. */
function EventoInvitadosActionsMenu({
  eventoId,
  onVerInvitados,
}: {
  eventoId: string
  onVerInvitados: () => void
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
            onVerInvitados()
          }}
          className="text-blue-700 hover:bg-blue-50 dark:text-blue-300 dark:hover:bg-blue-950/40 font-bold"
        >
          <Users size={14} className="text-blue-600" /> Ver Lista de Invitados
        </MenuItem>

        <MenuItem
          onClick={() => {
            setOpen(false)
            navigate(`/convocatorias/checkin?eventoId=${eventoId}`)
          }}
          className="text-teal-700 hover:bg-teal-50 dark:text-teal-300 dark:hover:bg-teal-950/40 font-semibold"
        >
          <QrCode size={14} className="text-teal-600" /> Ir a Check-in & Acreditación
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
