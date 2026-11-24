import { useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { MenuItem, MenuSeparator } from '@/components/ui/MenuItem'
import { RowActionMenu } from '@/components/ui/RowActionMenu'
import { ArrowRight, CalendarDays, FileText, QrCode, Search, Send, Users } from 'lucide-react'
import { useAuth } from '@/auth/AuthProvider'
import { useFeatureSwitches } from '@/features/admin/hooks'
import { Card } from '@/components/ui/Card'
import { Breadcrumbs } from '@/components/ui/Breadcrumbs'
import { Spinner, ErrorNote } from '@/components/ui/Feedback'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { useEventosList } from '@/features/eventos/hooks'
import { formatDateRange } from '@/lib/format'
import { eventoStatusLabel, eventoTypeLabel } from '@/lib/labels'
import type { EventoListItem, EventTrackingResponse } from '@/api/types'
import { useCampanasPorEvento, useEventTrackings } from './tracking'

/** Un evento con lo que se sabe de su convocatoria. Sin campañas no hay resumen que leer. */
interface EventoRow {
  evento: EventoListItem
  campanas: number
  lanzadas: number
  tracking?: EventTrackingResponse
}

type Filtro = 'vigentes' | 'conCampanas' | 'sinCampanas' | 'todos'

// Cada acceso lleva a una función y se esconde con su interruptor (docs/adr/0039).
const MODULE_LINKS = [
  {
    to: '/convocatorias/eventos',
    featureKey: 'convocatorias.events',
    icon: CalendarDays,
    label: 'Eventos con convocatoria',
    description: 'Directorio y gestión de campañas por evento.',
    tone: 'bg-indigo-50 text-indigo-700 border-indigo-200 dark:bg-indigo-950/50 dark:text-indigo-300 dark:border-indigo-900',
  },
  {
    to: '/convocatorias/campanas',
    featureKey: 'convocatorias.campaigns',
    icon: Send,
    label: 'Todas las campañas',
    description: 'La lista completa, de todos los eventos.',
    tone: 'bg-violet-50 text-violet-700 border-violet-200 dark:bg-violet-950/50 dark:text-violet-300 dark:border-violet-900',
  },
  {
    to: '/convocatorias/plantillas',
    featureKey: 'convocatorias.templates',
    icon: FileText,
    label: 'Plantillas',
    description: 'Edición de mensajes, multi-idioma y vista previa HTML.',
    tone: 'bg-amber-50 text-amber-700 border-amber-200 dark:bg-amber-950/50 dark:text-amber-300 dark:border-amber-900',
  },
  {
    to: '/convocatorias/invitados',
    featureKey: 'convocatorias.guests',
    icon: Users,
    label: 'Invitados',
    description: 'Registro, RSVP, capacidad por segmento y scoring.',
    tone: 'bg-blue-50 text-blue-700 border-blue-200 dark:bg-blue-950/50 dark:text-blue-300 dark:border-blue-900',
  },
  {
    to: '/convocatorias/checkin',
    featureKey: 'convocatorias.check-in',
    icon: QrCode,
    label: 'Check-in',
    description: 'Ticket con QR, asistencia en vivo y sorteo.',
    tone: 'bg-teal-50 text-teal-700 border-teal-200 dark:bg-teal-950/50 dark:text-teal-300 dark:border-teal-900',
  },
] as const

const esVigente = (e: EventoListItem) => e.status === 'planning' || e.status === 'in_progress'

/** Un dato que el servicio no tiene para ese evento se muestra con guion, nunca con un cero. */
const cifra = (n: number | undefined) => (n == null ? '—' : n.toLocaleString())

/**
 * La entrada del módulo, organizada por evento: cada fila es un evento con su convocatoria, y
 * lleva a la página donde se gestionan sus campañas. Todas las cifras salen de
 * convocatorias-service; lo que el servicio no sirve no se muestra.
 */
export function ConvocatoriasDashboardPage() {
  const { user } = useAuth()
  const [searchTerm, setSearchTerm] = useState('')
  const [tipoFilter, setTipoFilter] = useState('')
  const [filtro, setFiltro] = useState<Filtro>('todos')
  const navigate = useNavigate()
  const { isEnabled } = useFeatureSwitches()

  const eventosQuery = useEventosList({ page: 1, pageSize: 100 })
  const campanasQuery = useCampanasPorEvento()
  const eventos = useMemo(() => eventosQuery.data?.items ?? [], [eventosQuery.data])

  // El resumen se lee solo de los eventos que tienen alguna campaña: de los demás no hay nada
  // que contar, y así no se hace una lectura por cada evento del listado.
  const conCampanas = useMemo(
    () => eventos.filter((e) => campanasQuery.data?.has(e.id)),
    [eventos, campanasQuery.data],
  )
  const trackings = useEventTrackings(conCampanas.map((e) => e.id))
  const isLoading = eventosQuery.isLoading || campanasQuery.isLoading
  const error =
    eventosQuery.error ?? campanasQuery.error ?? trackings.find((t) => t.error)?.error ?? null

  const filas = useMemo<EventoRow[]>(() => {
    const trackingPorEvento = new Map(conCampanas.map((e, i) => [e.id, trackings[i]?.data]))
    return eventos.map((evento) => {
      const cuenta = campanasQuery.data?.get(evento.id)
      return {
        evento,
        campanas: cuenta?.total ?? 0,
        lanzadas: cuenta?.lanzadas ?? 0,
        tracking: trackingPorEvento.get(evento.id),
      }
    })
  }, [eventos, conCampanas, trackings, campanasQuery.data])

  const filtradas = useMemo(() => {
    const termino = searchTerm.trim().toLowerCase()
    return filas.filter((f) => {
      const coincide = !termino || f.evento.name.toLowerCase().includes(termino)
      const pasa =
        filtro === 'todos' ||
        (filtro === 'vigentes' && esVigente(f.evento)) ||
        (filtro === 'conCampanas' && f.campanas > 0) ||
        (filtro === 'sinCampanas' && f.campanas === 0)
      const pasaTipo = !tipoFilter || f.evento.type === tipoFilter
      return coincide && pasa && pasaTipo
    })
  }, [filas, searchTerm, filtro, tipoFilter])

  const columns = useMemo<Column<EventoRow>[]>(
    () => [
      {
        key: 'evento',
        header: 'Evento',
        align: 'left',
        cell: (f) => (
          <div>
            <Link
              to={`/convocatorias/eventos/${f.evento.id}`}
              className="font-bold text-slate-900 hover:text-blue-600 dark:text-white dark:hover:text-blue-400"
            >
              {f.evento.name}
            </Link>
            <p className="flex flex-wrap items-center gap-x-2 text-[11px] text-slate-500 mt-0.5">
              <span className="font-semibold text-blue-700 dark:text-blue-300 bg-blue-50 dark:bg-blue-950/60 px-1.5 py-0.5 rounded text-[10px]">
                {eventoTypeLabel[f.evento.type] ?? f.evento.type}
              </span>
              <span className="inline-flex items-center gap-1">
                <CalendarDays size={11} />
                {formatDateRange(f.evento.startDate, f.evento.endDate)}
              </span>
              <span>{eventoStatusLabel[f.evento.status]}</span>
            </p>
          </div>
        ),
      },
      {
        key: 'registro',
        header: 'Registro',
        align: 'left',
        cell: (f) => <RegistroBadge tracking={f.tracking} />,
      },
      {
        key: 'campanas',
        header: 'Campañas',
        align: 'center',
        cell: (f) => (
          <div>
            <span className="font-bold text-slate-800 dark:text-slate-200">{f.campanas}</span>
            {f.campanas > 0 && (
              <span className="block text-[10px] text-slate-500">{f.lanzadas} lanzadas</span>
            )}
          </div>
        ),
      },
      {
        key: 'invitados',
        header: 'Invitados',
        align: 'center',
        cell: (f) => (
          <span className="font-bold text-slate-800 dark:text-slate-200">
            {cifra(f.tracking?.people.invited)}
          </span>
        ),
      },
      {
        key: 'inscritos',
        header: 'Inscritos',
        align: 'center',
        cell: (f) => (
          <span className="font-extrabold text-emerald-600 dark:text-emerald-400">
            {cifra(f.tracking?.registrations.total)}
          </span>
        ),
      },
      {
        key: 'sinInscribirse',
        header: 'Sin inscribirse',
        align: 'center',
        cell: (f) => (
          <span className="font-semibold text-blue-600 dark:text-blue-400">
            {cifra(f.tracking?.notRegistered)}
          </span>
        ),
      },
      {
        key: 'acciones',
        header: 'ACCIONES',
        align: 'right',
        cell: (f) => (
          <RowActionMenu label="Acciones de convocatoria">
            <MenuItem onClick={() => navigate(`/convocatorias/eventos/${f.evento.id}`)}>
              <Send size={14} className="text-blue-600 dark:text-blue-400" />
              {f.campanas > 0 ? 'Gestionar campañas' : 'Crear campaña'}
            </MenuItem>
            <MenuItem onClick={() => navigate(`/convocatorias/invitados?eventoId=${f.evento.id}`)}>
              <Users size={14} className="text-violet-600 dark:text-violet-400" />
              Ver invitados
            </MenuItem>
            <MenuItem onClick={() => navigate(`/convocatorias/checkin?eventoId=${f.evento.id}`)}>
              <QrCode size={14} className="text-emerald-600 dark:text-emerald-400" />
              Check-in del evento
            </MenuItem>
            <MenuSeparator />
            <MenuItem onClick={() => navigate(`/eventos/${f.evento.id}`)}>
              <CalendarDays size={14} className="text-slate-500" />
              Ficha del evento
            </MenuItem>
          </RowActionMenu>
        ),
      },
    ],
    [navigate],
  )

  if (isLoading) return <Spinner label="Cargando convocatorias…" />
  if (error != null) return <ErrorNote error={error} />

  return (
    <div className="space-y-6">
      <Breadcrumbs items={[{ label: 'Invitaciones' }]} />

      {/* Saludo de Bienvenida al Usuario */}
      <Card className="p-6 bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 rounded-3xl shadow-2xs">
        <div className="flex items-center gap-4">
          <div className="h-14 w-14 rounded-full bg-brand-deep text-white font-black text-xl flex items-center justify-center border-2 border-white shadow-md shrink-0">
            {user?.nombre
              ? user.nombre
                  .split(' ')
                  .map((n) => n[0])
                  .join('')
                  .slice(0, 2)
              : 'RG'}
          </div>
          <div>
            <h1 className="font-sans text-xl sm:text-2xl font-black text-slate-900 dark:text-white">
              ¡Bienvenido, {user?.nombre || 'Lic. Roberto Guzmán'}!
            </h1>
            <p className="text-xs font-semibold text-slate-500 dark:text-slate-400">
              {user?.department || 'Organización'}
            </p>
          </div>
        </div>
      </Card>

      {/* Accesos a las secciones del módulo Convocatorias */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5">
        {MODULE_LINKS.filter((link) => isEnabled(link.featureKey)).map(
          ({ to, icon: Icon, label, description, tone }) => (
            <Link key={to} to={to}>
              <Card className="group flex h-full items-start gap-3.5 bg-white p-5 rounded-2xl border border-slate-200/80 shadow-2xs transition-all hover:-translate-y-0.5 hover:shadow-md dark:bg-slate-900 dark:border-slate-800">
                <div
                  className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-xl border ${tone}`}
                >
                  <Icon size={18} />
                </div>
                <div className="min-w-0 flex-1">
                  <p className="flex items-center justify-between font-sans text-sm font-bold text-slate-900 dark:text-white">
                    {label}
                    <ArrowRight
                      size={14}
                      className="text-slate-300 transition-transform group-hover:translate-x-1 group-hover:text-blue-600"
                    />
                  </p>
                  <p className="mt-0.5 text-[11px] text-slate-500 dark:text-slate-400">
                    {description}
                  </p>
                </div>
              </Card>
            </Link>
          ),
        )}
      </div>

      {/* Convocatorias por evento */}
      <Card className="bg-white p-6 dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 rounded-2xl shadow-2xs">
        <div className="mb-4">
          <h2 className="font-sans text-base font-bold text-slate-900 dark:text-white">
            Convocatorias por evento
          </h2>
          <span className="text-xs text-slate-500 dark:text-slate-400">
            Entra a un evento para ver y crear sus campañas.
          </span>
        </div>

        <div className="mb-4 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <div className="sm:col-span-2 lg:col-span-2">
            <label className="mb-1.5 block text-xs font-bold text-slate-700 dark:text-slate-300">
              Buscar evento
            </label>
            <div className="relative">
              <Search
                size={16}
                className="absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400"
              />
              <input
                type="search"
                placeholder="Buscar por nombre de evento…"
                aria-label="Buscar evento"
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                className="w-full rounded-xl border border-slate-200 bg-slate-50 py-2 pl-9 pr-4 text-xs text-slate-900 focus:border-blue-600 focus:outline-none dark:border-slate-700 dark:bg-slate-800 dark:text-white"
              />
            </div>
          </div>
          <div>
            <label className="mb-1.5 block text-xs font-bold text-slate-700 dark:text-slate-300">
              Tipo de evento
            </label>
            <select
              value={tipoFilter}
              aria-label="Filtrar por tipo de evento"
              onChange={(e) => setTipoFilter(e.target.value)}
              className="w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2 text-xs font-semibold text-slate-900 focus:border-blue-600 focus:outline-none dark:border-slate-700 dark:bg-slate-800 dark:text-white cursor-pointer"
            >
              <option value="">Seleccionar</option>
              {Object.entries(eventoTypeLabel).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-1.5 block text-xs font-bold text-slate-700 dark:text-slate-300">
              Estado de convocatoria
            </label>
            <select
              value={filtro}
              aria-label="Filtrar eventos"
              onChange={(e) => setFiltro(e.target.value as Filtro)}
              className="w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2 text-xs font-semibold text-slate-900 focus:border-blue-600 focus:outline-none dark:border-slate-700 dark:bg-slate-800 dark:text-white cursor-pointer"
            >
              <option value="todos">Seleccionar</option>
              <option value="vigentes">Eventos vigentes</option>
              <option value="conCampanas">Con campañas</option>
              <option value="sinCampanas">Sin campañas</option>
            </select>
          </div>
        </div>

        <DataTable
          columns={columns}
          rows={filtradas}
          getRowKey={(row) => row.evento.id}
          empty="No hay eventos que coincidan con la búsqueda."
        />
      </Card>
    </div>
  )
}

function registroLabel(tracking?: EventTrackingResponse): string {
  if (!tracking) return ''
  return tracking.registrationWindow.open ? 'Abierto' : 'Cerrado'
}

function RegistroBadge({ tracking }: { tracking?: EventTrackingResponse }) {
  if (!tracking) return <span className="text-slate-400">—</span>
  const abierto = tracking.registrationWindow.open
  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-[10px] font-extrabold ${
        abierto
          ? 'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-800 dark:bg-emerald-950/60 dark:text-emerald-300'
          : 'border-rose-200 bg-rose-50 text-rose-700 dark:border-rose-800 dark:bg-rose-950/60 dark:text-rose-300'
      }`}
    >
      {registroLabel(tracking)}
    </span>
  )
}
