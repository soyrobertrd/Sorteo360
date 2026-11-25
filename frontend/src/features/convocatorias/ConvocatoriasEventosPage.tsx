import { useEffect, useMemo, useState } from 'react'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { Link, useNavigate } from 'react-router-dom'
import { MenuItem } from '@/components/ui/MenuItem'
import { RowActionMenu } from '@/components/ui/RowActionMenu'
import {
  CalendarDays,
  Check,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  Copy,
  QrCode,
  Search,
  Send,
  UserCheck,
} from 'lucide-react'
import { Breadcrumbs } from '@/components/ui/Breadcrumbs'
import { Card } from '@/components/ui/Card'
import { EmptyState, ErrorNote, Spinner } from '@/components/ui/Feedback'
import { DataTable } from '@/components/ui/DataTable'
import { ViewModeToggle } from '@/components/ui/ViewModeToggle'
import { CardGrid, GridCard, GridCardStats, GridCardTag } from '@/components/ui/GridCard'
import { StatCard } from '@/components/ui/StatCard'
import { useEventosList } from '@/features/eventos/hooks'
import { formatDateRange } from '@/lib/format'
import { eventoStatusLabel } from '@/lib/labels'
import type { EventoListItem, EventTrackingResponse } from '@/api/types'
import { useCampanasPorEvento, useEventTrackings } from './tracking'
import { invitadosFor } from './mockParticipants'
import { useCheckinsOverrides, useRegistrosPublicos } from './store'

interface EventoConvocatoriaRow {
  evento: EventoListItem
  campanas: number
  lanzadas: number
  /** Personas con check-in hecho: alimenta la ocupación del evento. */
  checkins: number
  tracking?: EventTrackingResponse
}

type Filtro = 'todos' | 'conCampanas' | 'sinCampanas' | 'vigentes' | 'registroAbierto'

const FILTRO_LABEL: Record<Filtro, string> = {
  todos: 'Todos los eventos',
  conCampanas: 'Con campañas activas',
  sinCampanas: 'Sin campañas aún',
  vigentes: 'Eventos vigentes',
  registroAbierto: 'Con registro abierto',
}

const cifra = (n: number | undefined) => (n == null ? '—' : n.toLocaleString('es-DO'))
const esVigente = (e: EventoListItem) => e.status === 'planning' || e.status === 'in_progress'

/** Menú de acciones (tres puntos) de cada evento: copiar enlace, check-in y campañas. */
function AccionesMenu({
  eventoId,
  esCopiado,
  onCopiar,
}: {
  eventoId: string
  esCopiado: boolean
  onCopiar: () => void
}) {
  const navigate = useNavigate()

  return (
    <RowActionMenu label="Acciones del evento">
      <MenuItem onClick={onCopiar}>
        {esCopiado ? <Check size={14} className="text-emerald-600" /> : <Copy size={14} />}
        {esCopiado ? '¡Enlace copiado!' : 'Copiar enlace de inscripción'}
      </MenuItem>
      <MenuItem onClick={() => navigate(`/convocatorias/checkin?eventoId=${eventoId}`)}>
        <QrCode size={14} />
        Check-in del evento
      </MenuItem>
      <MenuItem onClick={() => navigate(`/convocatorias/eventos/${eventoId}`)}>
        <Send size={14} />
        Campañas
      </MenuItem>
    </RowActionMenu>
  )
}

/**
 * Ocupación del evento: porcentaje de invitados (meta) que ya hicieron check-in.
 * En la tabla (`compact`) es solo el número; en tarjetas lleva barra de avance.
 */
function SemaforoOcupacion({
  checkins = 0,
  invitados = 0,
  compact = false,
}: {
  checkins?: number
  invitados?: number
  compact?: boolean
}) {
  const pct =
    invitados > 0 ? Math.min(100, Math.round((checkins / invitados) * 100)) : checkins > 0 ? 100 : 0

  if (compact) {
    return <span className="font-bold text-slate-800 dark:text-slate-200">{pct}%</span>
  }

  return (
    <div className="space-y-1.5 rounded-xl bg-slate-50/80 p-2.5 dark:bg-slate-800/40">
      <div className="flex items-center justify-between text-[11px]">
        <span className="font-semibold text-slate-600 dark:text-slate-300">Ocupación</span>
        <span className="font-extrabold text-slate-900 dark:text-white">
          {pct}% ({cifra(checkins)}/{cifra(invitados || checkins)})
        </span>
      </div>
      <div className="h-2 w-full overflow-hidden rounded-full bg-slate-200 dark:bg-slate-700">
        <div
          className="h-full rounded-full bg-emerald-500 transition-all duration-500"
          style={{ width: `${pct}%` }}
        />
      </div>
    </div>
  )
}

/**
 * Vista de listado de todos los eventos dentro del Módulo de Convocatorias.
 * Permite explorar el estado de convocatorias y campañas de cada evento, y navegar
 * a la pantalla de gestión detallada de campañas del evento seleccionado.
 */
export function ConvocatoriasEventosPage() {
  const [searchTerm, setSearchTerm] = useState('')
  const [filtro, setFiltro] = useState<Filtro>('todos')
  const [vistaModo, setVistaModo] = useState<'tabla' | 'tarjetas'>('tabla')
  const [copiadoId, setCopiadoId] = useState<string | null>(null)
  const [pagina, setPagina] = useState(1)
  const [porPagina, setPorPagina] = useState(10)

  const eventosQuery = useEventosList({ page: 1, pageSize: 100 })
  const campanasQuery = useCampanasPorEvento()
  const eventos = useMemo(() => eventosQuery.data?.items ?? [], [eventosQuery.data])

  const conCampanas = useMemo(
    () => eventos.filter((e) => campanasQuery.data?.has(e.id)),
    [eventos, campanasQuery.data],
  )
  const conCampanasIds = useMemo(() => conCampanas.map((e) => e.id), [conCampanas])
  const trackings = useEventTrackings(conCampanasIds)

  const isLoading = eventosQuery.isLoading || campanasQuery.isLoading
  const error =
    eventosQuery.error ?? campanasQuery.error ?? trackings.find((t) => t.error)?.error ?? null

  const registrosPublicos = useRegistrosPublicos()
  const checkinsOverrides = useCheckinsOverrides()

  const filas = useMemo<EventoConvocatoriaRow[]>(() => {
    const trackingPorEvento = new Map<string, EventTrackingResponse | undefined>()
    conCampanas.forEach((e, i) => {
      const data = trackings[i]?.data
      if (data) {
        trackingPorEvento.set(e.id, data)
      }
    })
    return eventos.map((evento) => {
      const cuenta = campanasQuery.data?.get(evento.id)
      const personas = [
        ...registrosPublicos.filter((r) => r.eventoId === evento.id),
        ...invitadosFor(evento),
      ]
      const checkins = personas.filter((p) => {
        const estado = checkinsOverrides[p.id] ?? p.status
        return estado === 'CHECKED_IN' || estado === 'RAFFLE_ELIGIBLE' || estado === 'RAFFLE_WINNER'
      }).length
      return {
        evento,
        campanas: cuenta?.total ?? 0,
        lanzadas: cuenta?.lanzadas ?? 0,
        checkins,
        tracking: trackingPorEvento.get(evento.id),
      }
    })
  }, [eventos, conCampanas, trackings, campanasQuery.data, registrosPublicos, checkinsOverrides])

  const filtradas = useMemo(() => {
    const termino = searchTerm.trim().toLowerCase()
    return filas.filter((f) => {
      const matchSearch = !termino || f.evento.name.toLowerCase().includes(termino)

      const matchFiltro =
        filtro === 'todos' ||
        (filtro === 'conCampanas' && f.campanas > 0) ||
        (filtro === 'sinCampanas' && f.campanas === 0) ||
        (filtro === 'vigentes' && esVigente(f.evento)) ||
        (filtro === 'registroAbierto' && f.tracking?.registrationWindow.open === true)

      return matchSearch && matchFiltro
    })
  }, [filas, searchTerm, filtro])

  // Reset de página al cambiar filtros, búsqueda o tamaño de página
  useEffect(() => {
    setPagina(1)
  }, [searchTerm, filtro, porPagina])

  const totalPaginas = Math.max(1, Math.ceil(filtradas.length / porPagina))
  const inicioIndice = (pagina - 1) * porPagina
  const finIndice = Math.min(inicioIndice + porPagina, filtradas.length)
  const filasPaginadas = useMemo(
    () => filtradas.slice(inicioIndice, finIndice),
    [filtradas, inicioIndice, finIndice],
  )

  const metrics = useMemo(() => {
    const totalEventos = filas.length
    const conCampanasCount = filas.filter((f) => f.campanas > 0).length
    const registrosAbiertosCount = filas.filter(
      (f) => f.tracking?.registrationWindow.open === true,
    ).length
    const totalInscritos = filas.reduce((acc, f) => acc + (f.tracking?.registrations.total ?? 0), 0)
    return { totalEventos, conCampanasCount, registrosAbiertosCount, totalInscritos }
  }, [filas])

  // El mismo enlace que copia la ficha del evento: su página pública, que lleva al formulario.
  const handleCopiarEnlace = async (evento: EventoListItem) => {
    const url = evento.externalUrl || `${window.location.origin}/publico/eventos/${evento.id}`
    try {
      await navigator.clipboard.writeText(url)
      setCopiadoId(evento.id)
      setTimeout(() => setCopiadoId(null), 2500)
    } catch {
      setCopiadoId(null)
    }
  }

  if (isLoading) return <Spinner label="Cargando eventos y convocatorias…" />
  if (error != null) return <ErrorNote error={error} />

  return (
    <div className="mx-auto max-w-7xl space-y-6 pb-12">
      <Breadcrumbs
        items={[{ label: 'Invitaciones', to: '/convocatorias' }, { label: 'Eventos' }]}
      />

      {/* Cabecera Principal */}
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="font-sans text-2xl font-extrabold tracking-tight text-slate-900 sm:text-3xl dark:text-white">
            Convocatorias por evento
          </h1>
          <p className="mt-1 text-xs text-slate-500 sm:text-sm dark:text-slate-400">
            Explora todos los eventos institucionales, copia sus enlaces públicos de inscripción y
            gestiona campañas o pase de lista QR.
          </p>
        </div>
      </div>

      {/* Tarjetas de Métricas Resumen */}
      <div className="grid grid-cols-2 gap-3 sm:gap-4 lg:grid-cols-4">
        <StatCard
          icon={CalendarDays}
          label="Total Eventos"
          value={metrics.totalEventos}
          iconClassName="border-blue-200 bg-blue-50 text-blue-700 dark:border-blue-900 dark:bg-blue-950/50 dark:text-blue-300"
        />
        <StatCard
          icon={Send}
          label="Con Campañas Creadas"
          value={metrics.conCampanasCount}
          iconClassName="border-violet-200 bg-violet-50 text-violet-700 dark:border-violet-900 dark:bg-violet-950/50 dark:text-violet-300"
        />
        <StatCard
          icon={CheckCircle2}
          label="Registro Público Abierto"
          value={metrics.registrosAbiertosCount}
          iconClassName="border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900 dark:bg-emerald-950/50 dark:text-emerald-300"
        />
        <StatCard
          icon={UserCheck}
          label="Total Inscritos Global"
          value={cifra(metrics.totalInscritos)}
          iconClassName="border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-900 dark:bg-amber-950/50 dark:text-amber-300"
        />
      </div>

      {/* Tarjeta de Lista / Tabla */}
      <Card className="rounded-3xl border border-slate-200/80 bg-white p-6 shadow-2xs dark:border-slate-800 dark:bg-slate-900">
        {/* Barra de Filtros, Búsqueda y Selector de Vista (Tabla / Tarjetas) */}
        <div className="mb-5 flex flex-wrap items-center justify-between gap-3">
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 flex-1 min-w-70">
            <div className="relative">
              <Search
                size={16}
                className="absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400"
              />
              <input
                type="search"
                placeholder="Buscar evento…"
                aria-label="Buscar evento"
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                className="w-full rounded-xl border border-slate-200 bg-slate-50 py-2 pl-9 pr-4 text-xs text-slate-900 focus:border-blue-600 focus:outline-none dark:border-slate-700 dark:bg-slate-800 dark:text-white"
              />
            </div>

            <div>
              <SearchableSelect
                value={filtro}
                aria-label="Filtrar por estado de convocatoria"
                onChange={(e) => setFiltro(e.target.value as Filtro)}
                className="w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2 text-xs font-bold text-slate-900 focus:border-blue-600 focus:outline-none dark:border-slate-700 dark:bg-slate-800 dark:text-white cursor-pointer"
              >
                {(Object.keys(FILTRO_LABEL) as Filtro[]).map((clave) => (
                  <option key={clave} value={clave}>
                    {FILTRO_LABEL[clave]}
                  </option>
                ))}
              </SearchableSelect>
            </div>
          </div>

          {/* Selector de Modo de Vista */}
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
            columns={[
              {
                key: 'evento',
                header: 'Evento y Fechas',
                cell: (f) => (
                  <div>
                    <Link
                      to={`/convocatorias/eventos/${f.evento.id}`}
                      className="font-bold text-brand hover:underline dark:text-blue-400"
                    >
                      {f.evento.name}
                    </Link>
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
                key: 'registro',
                header: 'Registro',
                align: 'center',
                cell: (f) => {
                  const registroAbierto = f.tracking?.registrationWindow.open === true
                  return f.tracking ? (
                    <span
                      className={`text-xs font-bold ${
                        registroAbierto
                          ? 'text-emerald-700 dark:text-emerald-400'
                          : 'text-rose-700 dark:text-rose-400'
                      }`}
                    >
                      {registroAbierto ? 'Abierto' : 'Cerrado'}
                    </span>
                  ) : (
                    <span className="text-slate-400 text-xs">—</span>
                  )
                },
              },
              {
                key: 'campanas',
                header: 'Campañas',
                align: 'center',
                cell: (f) => (
                  <span className="font-bold text-slate-800 dark:text-slate-200">{f.campanas}</span>
                ),
              },
              {
                key: 'ocupacion',
                header: 'Ocupación / Meta',
                align: 'center',
                cell: (f) => (
                  <SemaforoOcupacion
                    checkins={f.checkins}
                    invitados={f.tracking?.people.invited}
                    compact
                  />
                ),
              },
              {
                key: 'inscritos',
                header: 'Inscritos',
                align: 'center',
                cell: (f) => (
                  <div>
                    <span className="font-extrabold text-emerald-600 dark:text-emerald-400">
                      {cifra(f.tracking?.registrations.total ?? 0)}
                    </span>
                    <span className="font-semibold text-slate-500 dark:text-slate-400">
                      /{cifra(f.tracking?.people.invited ?? 0)}
                    </span>
                  </div>
                ),
              },
              {
                key: 'acciones',
                header: 'Acciones',
                align: 'right',
                cell: (f) => (
                  <div className="flex items-center justify-end">
                    <AccionesMenu
                      eventoId={f.evento.id}
                      esCopiado={copiadoId === f.evento.id}
                      onCopiar={() => handleCopiarEnlace(f.evento)}
                    />
                  </div>
                ),
              },
            ]}
          />
        ) : (
          /* Modo Cuadrícula de Tarjetas */
          <CardGrid>
            {filasPaginadas.map((f) => {
              const registroAbierto = f.tracking?.registrationWindow.open === true
              const esCopiado = copiadoId === f.evento.id

              return (
                <GridCard
                  key={f.evento.id}
                  titleLines={2}
                  tags={
                    <GridCardTag tone="slate" className="normal-case tracking-normal">
                      {eventoStatusLabel[f.evento.status]}
                    </GridCardTag>
                  }
                  aside={
                    <>
                      {f.tracking && (
                        <span
                          className={`text-[10px] font-extrabold ${
                            registroAbierto
                              ? 'text-emerald-700 dark:text-emerald-400'
                              : 'text-rose-700 dark:text-rose-400'
                          }`}
                        >
                          {registroAbierto ? 'Registro Abierto' : 'Registro Cerrado'}
                        </span>
                      )}
                      <AccionesMenu
                        eventoId={f.evento.id}
                        esCopiado={esCopiado}
                        onCopiar={() => handleCopiarEnlace(f.evento)}
                      />
                    </>
                  }
                  title={<Link to={`/convocatorias/eventos/${f.evento.id}`}>{f.evento.name}</Link>}
                  subtitle={
                    <span className="flex items-center gap-1">
                      <CalendarDays size={12} className="shrink-0" />
                      {formatDateRange(f.evento.startDate, f.evento.endDate)}
                    </span>
                  }
                >
                  <div className="space-y-3">
                    {/* Semáforo de Ocupación / Conversión */}
                    <SemaforoOcupacion
                      checkins={f.checkins}
                      invitados={f.tracking?.people.invited}
                    />
                    <GridCardStats
                      items={[
                        { label: 'Campañas', value: f.campanas },
                        { label: 'Invitados', value: cifra(f.tracking?.people.invited) },
                        {
                          label: 'Inscritos',
                          value: cifra(f.tracking?.registrations.total),
                          tone: 'emerald',
                        },
                      ]}
                    />
                  </div>
                </GridCard>
              )
            })}
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
                  value={porPagina}
                  onChange={(e) => {
                    setPorPagina(Number(e.target.value))
                    setPagina(1)
                  }}
                  aria-label="Eventos por página"
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
                aria-label="Página anterior"
                className="inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-2.5 py-1 font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-40 disabled:cursor-not-allowed dark:border-slate-700 dark:bg-slate-800 dark:text-slate-300 dark:hover:bg-slate-700 cursor-pointer"
              >
                <ChevronLeft size={14} />
                <span>Anterior</span>
              </button>

              <div className="flex items-center gap-1 px-1">
                {Array.from({ length: totalPaginas }, (_, i) => i + 1).map((num) => {
                  // Mostrar primera, última y páginas adyacentes si son muchas
                  if (
                    totalPaginas > 7 &&
                    num !== 1 &&
                    num !== totalPaginas &&
                    Math.abs(num - pagina) > 1
                  ) {
                    if (num === 2 || num === totalPaginas - 1) {
                      return (
                        <span key={num} className="px-1 text-slate-400">
                          …
                        </span>
                      )
                    }
                    return null
                  }

                  const esActual = num === pagina
                  return (
                    <button
                      key={num}
                      type="button"
                      onClick={() => setPagina(num)}
                      className={`h-7 w-7 rounded-lg text-xs font-bold transition-colors cursor-pointer ${
                        esActual
                          ? 'bg-brand-deep text-white shadow-2xs'
                          : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900 dark:text-slate-400 dark:hover:bg-slate-800 dark:hover:text-white'
                      }`}
                    >
                      {num}
                    </button>
                  )
                })}
              </div>

              <button
                type="button"
                onClick={() => setPagina((p) => Math.min(totalPaginas, p + 1))}
                disabled={pagina >= totalPaginas}
                aria-label="Página siguiente"
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
