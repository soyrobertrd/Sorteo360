import { useState, useEffect, useRef } from 'react'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import {
  ChevronLeft,
  ChevronRight,
  Search,
  MoreVertical,
  Edit3,
  Copy,
  Lock,
  XCircle,
  AlertTriangle,
  Mail,
} from 'lucide-react'
import { useCreateEvento, useEvento, useEventosList, useUpdateEvento } from './hooks'
import { sendCancellationNotice } from './cancellationNotice'
import { EventoFormModal } from './components/EventoFormModal'
import { EventoExitoModal } from './components/EventoExitoModal'
import { EventoStatusBadge } from '@/components/ui/Badge'
import { Breadcrumbs } from '@/components/ui/Breadcrumbs'
import { Button } from '@/components/ui/Button'
import { DataTable } from '@/components/ui/DataTable'
import { ActionMenu } from '@/components/ui/ActionMenu'
import { Card } from '@/components/ui/Card'
import { IconButton } from '@/components/ui/IconButton'
import { MenuItem } from '@/components/ui/MenuItem'
import { ViewModeToggle, type ViewMode } from '@/components/ui/ViewModeToggle'
import {
  CardGrid,
  GridCard,
  GridCardCta,
  GridCardDetail,
  GridCardDetails,
  GridCardFigure,
} from '@/components/ui/GridCard'
import { EmptyState, ErrorNote, Spinner } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { api } from '@/api'
import { formatDate, formatDateRange, formatMoney } from '@/lib/format'
import { eventoStatusLabel, eventoTypeLabel } from '@/lib/labels'
import type {
  EventoDetail,
  EventoListItem,
  EventoStatus,
  EventoType,
  EventoImage,
} from '@/api/types'
import { zonaHorariaDeCiudad } from './zonasHorarias'
import { NewButton } from '@/components/ui/NewButton'

type SortField = 'name' | 'startDate' | 'totalCost' | 'status'
type SortOrder = 'asc' | 'desc'

function EventCoverImage({
  cover,
  defaultSrc,
  className,
}: {
  cover?: EventoImage | null
  defaultSrc: string
  className?: string
}) {
  const [src, setSrc] = useState(defaultSrc)

  useEffect(() => {
    if (cover && !cover.missing) {
      api
        .getFileContentUrl(cover.storedFileId)
        .then(setSrc)
        .catch(() => {})
    } else {
      setSrc(defaultSrc)
    }
  }, [cover, defaultSrc])

  return <img src={src} className={className} alt="Cover" />
}

const ESTADOS_VALIDOS: EventoStatus[] = ['planning', 'in_progress', 'closed', 'cancelled']

export function EventosListPage() {
  const navigate = useNavigate()
  // El dashboard enlaza aquí con ?estado=<status> para abrir la lista ya filtrada.
  const [searchParams] = useSearchParams()
  const estadoParam = searchParams.get('estado')
  const estadoInicial: EventoStatus | '' = ESTADOS_VALIDOS.includes(estadoParam as EventoStatus)
    ? (estadoParam as EventoStatus)
    : ''

  const [viewMode, setViewMode] = useState<ViewMode>('list')
  const [pageSize, setPageSize] = useState(20)
  const [searchTermInput, setSearchTermInput] = useState('')
  const [searchTerm, setSearchTerm] = useState('')
  const [sortField, setSortField] = useState<SortField>('startDate')
  const [sortOrder, setSortOrder] = useState<SortOrder>('desc')
  const [filters, setFilters] = useState({
    type: '' as EventoType | '',
    status: estadoInicial,
    fechaInicioDesde: '',
    fechaInicioHasta: '',
  })
  const [page, setPage] = useState(1)
  const [showCreate, setShowCreate] = useState(false)

  // Action Modals State
  const [editingEventoId, setEditingEventoId] = useState<string | null>(null)
  const [closingEvento, setClosingEvento] = useState<EventoListItem | null>(null)
  const [cancelingEvento, setCancelingEvento] = useState<EventoListItem | null>(null)
  const [cancellationReason, setCancellationReason] = useState('')
  const [exitoEventoData, setExitoEventoData] = useState<{
    id: string
    nombre: string
    tipo?: string
    ciudad?: string
    pais?: string
    fecha?: string
    isEdit?: boolean
  } | null>(null)

  useEffect(() => {
    const delayDebounceFn = setTimeout(() => {
      setSearchTerm(searchTermInput)
      setPage(1)
    }, 300)

    return () => clearTimeout(delayDebounceFn)
  }, [searchTermInput])

  const { data, isLoading, error } = useEventosList({
    type: filters.type || undefined,
    status: filters.status || undefined,
    startDateFrom: filters.fechaInicioDesde || undefined,
    startDateTo: filters.fechaInicioHasta || undefined,
    page,
    pageSize,
    searchTerm: searchTerm || undefined,
    sortField,
    sortOrder,
  })
  const createEvento = useCreateEvento()
  const [duplicateError, setDuplicateError] = useState<unknown>(null)
  // Solo se muestra si el aviso de cancelación dejó a alguien sin correo; un aviso completo no dice nada.
  const [cancelNoticeError, setCancelNoticeError] = useState<{ message: string } | null>(null)
  const queryClient = useQueryClient()

  const setFilter = (key: keyof typeof filters) => (value: string) => {
    setFilters((f) => ({ ...f, [key]: value }))
    setPage(1)
  }

  const handleSort = (field: SortField) => {
    setPage(1)
    if (sortField === field) {
      setSortOrder((prev) => (prev === 'asc' ? 'desc' : 'asc'))
    } else {
      setSortField(field)
      setSortOrder('asc')
    }
  }

  const handleEdit = (item: EventoListItem) => setEditingEventoId(item.id)

  // El listado no trae las ciudades y crear exige un destino, así que la copia lo toma del
  // detalle del original en vez de inventarse uno.
  const handleDuplicate = async (item: EventoListItem) => {
    setDuplicateError(null)
    let origen: EventoDetail
    try {
      origen = await api.getEvento(item.id)
    } catch (err) {
      setDuplicateError(err)
      return
    }
    const destino = origen.cities[0]
    if (!destino) {
      setDuplicateError(new Error('El evento original no tiene un destino que copiar.'))
      return
    }
    createEvento.mutate(
      {
        body: {
          name: `${item.name} - Copia`,
          type: item.type,
          startDate: item.startDate,
          endDate: item.endDate,
          city: destino.name,
          country: destino.country,
          timeZone: destino.timeZone ?? zonaHorariaDeCiudad(destino.country, destino.name),
          language: item.language || 'es',
          invitedCount: item.invitedCount,
          estimatedBudget: origen.estimatedBudget || null,
        },
      },
      {
        onSuccess: (newId) => {
          navigate(`/eventos/${newId}`)
        },
        onError: (err) => setDuplicateError(err),
      },
    )
  }

  const totalPages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1

  return (
    <div className="space-y-6">
      {/* Breadcrumb Oficial sin íconos */}
      <Breadcrumbs
        items={[{ label: 'Eventos', to: '/eventos/todos' }, { label: 'Todos los eventos' }]}
      />

      {/* Encabezado Principal y Acciones */}
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="font-sans text-2xl font-extrabold tracking-tight text-slate-900 sm:text-3xl dark:text-white">
            Eventos
          </h1>
          <p className="mt-1 font-body text-xs text-slate-500 sm:text-sm dark:text-slate-400">
            Registro básico y calendario de celebraciones corporativas, galas y actividades.
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2.5">
          {/* Alternar Vista Lista / Cuadrícula */}
          <ViewModeToggle value={viewMode} onChange={setViewMode} size="sm" />

          <NewButton onClick={() => setShowCreate(true)}>Nuevo evento</NewButton>
        </div>
      </div>

      {/* Card de Búsqueda y Filtros */}
      <Card className="p-4 sm:p-5">
        <div className="grid grid-cols-1 gap-3.5 sm:grid-cols-2 lg:grid-cols-6">
          {/* Búsqueda por Nombre */}
          <div className="lg:col-span-1">
            <label className="mb-1.5 block text-xs font-bold text-slate-700 dark:text-slate-300">
              Buscar por Nombre
            </label>
            <div className="relative">
              <Search
                size={16}
                className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400"
              />
              <input
                type="text"
                placeholder="Nombre de evento..."
                aria-label="Nombre de evento"
                value={searchTermInput}
                onChange={(e) => setSearchTermInput(e.target.value)}
                className="w-full rounded-lg border border-slate-200 bg-white py-2 pl-9 pr-3 text-xs text-slate-900 transition-colors focus:border-blue-600 focus:outline-none dark:border-slate-700 dark:bg-slate-900 dark:text-white"
              />
            </div>
          </div>

          {/* Tipo de evento */}
          <div>
            <label className="mb-1.5 block text-xs font-bold text-slate-700 dark:text-slate-300">
              Tipo de evento
            </label>
            <SearchableSelect
              aria-label="Tipo de evento"
              value={filters.type}
              onChange={(e) => setFilter('type')(e.target.value)}
              className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-xs text-slate-900 transition-colors focus:border-blue-600 focus:outline-none dark:border-slate-700 dark:bg-slate-900 dark:text-white cursor-pointer"
            >
              <option value="">Todos los tipos</option>
              {Object.entries(eventoTypeLabel).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </SearchableSelect>
          </div>

          {/* Estado del evento */}
          <div>
            <label className="mb-1.5 block text-xs font-bold text-slate-700 dark:text-slate-300">
              Estado
            </label>
            <SearchableSelect
              value={filters.status}
              onChange={(e) => setFilter('status')(e.target.value)}
              className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-xs text-slate-900 transition-colors focus:border-blue-600 focus:outline-none dark:border-slate-700 dark:bg-slate-900 dark:text-white cursor-pointer"
            >
              <option value="">Todos los estados</option>
              {Object.entries(eventoStatusLabel).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </SearchableSelect>
          </div>

          {/* Inicio desde */}
          <div>
            <label className="mb-1.5 block text-xs font-bold text-slate-700 dark:text-slate-300">
              Inicio desde
            </label>
            <input
              type="date"
              aria-label="Inicio desde"
              value={filters.fechaInicioDesde}
              onChange={(e) => setFilter('fechaInicioDesde')(e.target.value)}
              className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-xs text-slate-900 transition-colors focus:border-blue-600 focus:outline-none dark:border-slate-700 dark:bg-slate-900 dark:text-white"
            />
          </div>

          {/* Inicio hasta */}
          <div>
            <label className="mb-1.5 block text-xs font-bold text-slate-700 dark:text-slate-300">
              Inicio hasta
            </label>
            <input
              type="date"
              aria-label="Inicio hasta"
              value={filters.fechaInicioHasta}
              min={filters.fechaInicioDesde || undefined}
              onChange={(e) => setFilter('fechaInicioHasta')(e.target.value)}
              className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-xs text-slate-900 transition-colors focus:border-blue-600 focus:outline-none dark:border-slate-700 dark:bg-slate-900 dark:text-white"
            />
          </div>
        </div>
      </Card>

      {/* Carga y Errores */}
      {isLoading && <Spinner />}
      {error != null && <ErrorNote error={error} />}
      {duplicateError != null && <ErrorNote error={duplicateError} />}
      {cancelNoticeError != null && <ErrorNote error={cancelNoticeError} />}

      {/* Estado vacío */}
      {data && data.items.length === 0 && (
        <EmptyState>No hay eventos que coincidan con los filtros seleccionados.</EmptyState>
      )}

      {/* Vista de Cuadrícula */}
      {viewMode === 'grid' && data && data.items.length > 0 && (
        <CardGrid>
          {data.items.map((evento) => (
            <GridCard
              key={evento.id}
              onClick={() => navigate(`/eventos/${evento.id}`)}
              titleLines={2}
              cover={
                <>
                  <EventCoverImage
                    cover={evento.cover}
                    defaultSrc="https://images.unsplash.com/photo-1511578314322-379afb476865?auto=format&fit=crop&w=800&q=80"
                    className="h-full w-full object-cover opacity-85 transition-transform duration-500 group-hover:scale-105"
                  />
                  <div className="absolute inset-0 bg-linear-to-t from-slate-950/80 via-slate-950/20 to-transparent" />

                  <div className="absolute left-3 top-3">
                    <span className="inline-flex items-center rounded-lg bg-slate-900/80 backdrop-blur-md px-2.5 py-1 text-[10px] font-extrabold uppercase tracking-wider text-white shadow-xs border border-white/10">
                      {eventoTypeLabel[evento.type]}
                    </span>
                  </div>

                  <div
                    className="absolute right-3 top-3 flex items-center gap-1.5"
                    onClick={(e) => e.stopPropagation()}
                  >
                    <EventoStatusBadge status={evento.status} />
                    <EventoActionsMenu
                      evento={evento}
                      onEdit={handleEdit}
                      onDuplicate={handleDuplicate}
                      onCloseEvent={setClosingEvento}
                      onCancelEvent={setCancelingEvento}
                    />
                  </div>
                </>
              }
              title={evento.name}
              footerStart={
                <GridCardFigure label="Costo Total">
                  {evento.totalCost ? formatMoney(evento.totalCost) : '—'}
                </GridCardFigure>
              }
              footerEnd={<GridCardCta />}
            >
              <GridCardDetails>
                <GridCardDetail label="Fecha">{formatDate(evento.startDate)}</GridCardDetail>
              </GridCardDetails>
            </GridCard>
          ))}
        </CardGrid>
      )}

      {/* Tabla de Resultados (Vista Lista) */}
      {viewMode === 'list' && data && data.items.length > 0 && (
        <Card className="bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 rounded-2xl overflow-hidden p-0 shadow-2xs">
          {/* Tabla de escritorio */}
          <div className="hidden md:block">
            <DataTable<EventoListItem>
              rows={data.items}
              getRowKey={(evento) => evento.id}
              onRowClick={(evento) => navigate(`/eventos/${evento.id}`)}
              columns={[
                {
                  key: 'name',
                  header: (
                    <div
                      onClick={() => handleSort('name')}
                      className="flex items-center gap-1.5 cursor-pointer select-none"
                    >
                      <span>EVENTO</span>
                    </div>
                  ),
                  cell: (evento) => (
                    <span className="font-sans text-sm font-bold text-slate-900 group-hover:text-blue-600 dark:text-white dark:group-hover:text-blue-400">
                      {evento.name}
                    </span>
                  ),
                },
                {
                  key: 'type',
                  header: 'TIPO',
                  cell: (evento) => (
                    <span className="font-body text-xs text-slate-600 dark:text-slate-300">
                      {eventoTypeLabel[evento.type]}
                    </span>
                  ),
                },
                {
                  key: 'dates',
                  header: (
                    <div
                      onClick={() => handleSort('startDate')}
                      className="flex items-center gap-1.5 cursor-pointer select-none"
                    >
                      <span>FECHAS</span>
                    </div>
                  ),
                  cell: (evento) => (
                    <span className="font-body text-xs text-slate-600 dark:text-slate-300">
                      {formatDateRange(evento.startDate, evento.endDate)}
                    </span>
                  ),
                },
                {
                  key: 'status',
                  header: (
                    <div
                      onClick={() => handleSort('status')}
                      className="flex items-center gap-1.5 cursor-pointer select-none"
                    >
                      <span>ESTADO</span>
                    </div>
                  ),
                  cell: (evento) => <EventoStatusBadge status={evento.status} />,
                },
                {
                  key: 'totalCost',
                  header: (
                    <div
                      onClick={() => handleSort('totalCost')}
                      className="flex items-center justify-end gap-1.5 cursor-pointer select-none"
                    >
                      <span>COSTO TOTAL</span>
                    </div>
                  ),
                  align: 'right',
                  cell: (evento) => (
                    <span className="font-sans text-sm font-black text-slate-900 dark:text-white">
                      {evento.totalCost ? formatMoney(evento.totalCost) : '—'}
                    </span>
                  ),
                },
                {
                  key: 'actions',
                  header: 'OPCIONES',
                  align: 'right',
                  cell: (evento) => (
                    <div onClick={(e) => e.stopPropagation()} className="inline-block">
                      <EventoActionsMenu
                        evento={evento}
                        onEdit={handleEdit}
                        onDuplicate={handleDuplicate}
                        onCloseEvent={setClosingEvento}
                        onCancelEvent={setCancelingEvento}
                      />
                    </div>
                  ),
                },
              ]}
            />
          </div>

          {/* Tarjetas móviles */}
          <ul className="divide-y divide-slate-100 md:hidden dark:divide-slate-800">
            {data.items.map((evento) => (
              <li key={evento.id} className="relative">
                <Link to={`/eventos/${evento.id}`} className="block px-4 py-3.5 hover:bg-slate-50">
                  <div className="flex items-start justify-between gap-2">
                    <p className="font-sans text-sm font-bold text-slate-900 dark:text-white">
                      {evento.name}
                    </p>
                    <div className="flex items-center gap-1">
                      <EventoStatusBadge status={evento.status} />
                      <EventoActionsMenu
                        evento={evento}
                        onEdit={handleEdit}
                        onDuplicate={handleDuplicate}
                        onCloseEvent={setClosingEvento}
                        onCancelEvent={setCancelingEvento}
                      />
                    </div>
                  </div>
                  <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">
                    {eventoTypeLabel[evento.type]} •{' '}
                    {formatDateRange(evento.startDate, evento.endDate)}
                  </p>
                  <div className="mt-1.5 flex items-center justify-between text-xs text-slate-500">
                    <span>{evento.cities?.[0]?.name ?? ''}</span>
                    <span className="font-bold text-slate-900 dark:text-white">
                      {evento.totalCost ? formatMoney(evento.totalCost) : '—'}
                    </span>
                  </div>
                </Link>
              </li>
            ))}
          </ul>

          {/* Paginación con Selector de Cantidad por Pantalla */}
          <div className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-200/80 px-5 py-3.5 text-xs text-slate-500 dark:border-slate-800 dark:text-slate-400">
            <div className="flex items-center gap-3">
              <span>
                Mostrando{' '}
                <strong className="font-semibold text-slate-900 dark:text-white">
                  {data.items.length}
                </strong>{' '}
                de{' '}
                <strong className="font-semibold text-slate-900 dark:text-white">
                  {data.total}
                </strong>{' '}
                eventos
              </span>
              <div className="flex items-center gap-1.5 border-l border-slate-200 pl-3 dark:border-slate-700">
                <span>Ver por página:</span>
                <select
                  aria-label="Eventos por página"
                  value={pageSize}
                  onChange={(e) => {
                    setPageSize(Number(e.target.value))
                    setPage(1)
                  }}
                  className="rounded-lg border border-slate-200 bg-white px-2 py-1 text-xs font-bold text-slate-800 transition-colors focus:border-blue-600 focus:outline-none dark:border-slate-700 dark:bg-slate-800 dark:text-slate-200 cursor-pointer"
                >
                  <option value={20}>20</option>
                  <option value={30}>30</option>
                  <option value={40}>40</option>
                  <option value={50}>50</option>
                  <option value={100}>100</option>
                </select>
              </div>
            </div>

            <div className="flex items-center gap-2">
              <span className="text-[11px] font-semibold text-slate-400">
                Página {data.page} de {totalPages}
              </span>
              <div className="flex items-center gap-1">
                <Button
                  variant="outline"
                  size="icon"
                  disabled={page <= 1}
                  onClick={() => setPage((p) => p - 1)}
                  aria-label="Página anterior"
                  className="h-8 w-8 rounded-lg border-slate-200 dark:border-slate-700 cursor-pointer"
                >
                  <ChevronLeft size={15} />
                </Button>
                <Button
                  variant="outline"
                  size="icon"
                  disabled={page >= totalPages}
                  onClick={() => setPage((p) => p + 1)}
                  aria-label="Página siguiente"
                  className="h-8 w-8 rounded-lg border-slate-200 dark:border-slate-700 cursor-pointer"
                >
                  <ChevronRight size={15} />
                </Button>
              </div>
            </div>
          </div>
        </Card>
      )}

      {/* Modal para Crear Evento */}
      {showCreate && (
        <EventoFormModal
          onClose={() => {
            setShowCreate(false)
            createEvento.reset()
          }}
          onCreate={async (body, files) => {
            try {
              const id = await createEvento.mutateAsync({ body })
              if (files.coverFile) {
                await api.uploadEventoImage(id, files.coverFile, 'Cover')
              }
              await Promise.all(
                files.galleryFiles.map((file) => api.uploadEventoImage(id, file, 'Gallery')),
              )
              setShowCreate(false)
              setExitoEventoData({
                id,
                nombre: body.name,
                tipo: body.type,
                ciudad: body.city,
                pais: body.country,
                fecha: body.startDate,
                isEdit: false,
              })
            } catch (err) {
              console.error('Error al crear evento o subir imágenes:', err)
            }
          }}
          pending={createEvento.isPending}
          error={createEvento.error}
        />
      )}

      {/* Modal para Editar Evento */}
      {editingEventoId && (
        <EditarEventoModal
          eventoId={editingEventoId}
          onClose={() => setEditingEventoId(null)}
          onSaved={(guardado) => {
            setEditingEventoId(null)
            setExitoEventoData({
              id: guardado.id,
              nombre: guardado.name,
              tipo: guardado.type,
              isEdit: true,
            })
          }}
        />
      )}

      {/* Modal Dedicado Independiente de Éxito */}
      {exitoEventoData && (
        <EventoExitoModal
          createdId={exitoEventoData.id}
          eventoNombre={exitoEventoData.nombre}
          eventoTipo={exitoEventoData.tipo}
          ciudad={exitoEventoData.ciudad}
          pais={exitoEventoData.pais}
          fecha={exitoEventoData.fecha}
          isEdit={exitoEventoData.isEdit}
          onClose={() => setExitoEventoData(null)}
          onNavigateToDetail={(id) => {
            setExitoEventoData(null)
            navigate(`/eventos/${id}`)
          }}
        />
      )}

      {/* Modal Confirmación de Cierre */}
      {closingEvento && (
        <Modal
          title={`Cerrar evento: ${closingEvento.name}`}
          onClose={() => setClosingEvento(null)}
        >
          <div className="space-y-4">
            <p className="text-xs text-slate-600 dark:text-slate-300">
              ¿Está seguro de que desea cerrar este evento? El estado cambiará a{' '}
              <strong>CERRADO</strong> y se congelará la edición de presupuestos.
            </p>

            <div className="flex justify-end gap-2 border-t border-slate-100 pt-3 dark:border-slate-800">
              <Button variant="secondary" onClick={() => setClosingEvento(null)}>
                Cancelar
              </Button>
              <Button
                onClick={() => {
                  api.cerrarEvento(closingEvento.id).then(() => {
                    setClosingEvento(null)
                    window.location.reload()
                  })
                }}
                className="bg-brand-deep text-white hover:bg-brand-deep/90 font-bold cursor-pointer"
              >
                Aceptar
              </Button>
            </div>
          </div>
        </Modal>
      )}

      {/* Modal Confirmación de Cancelación */}
      {cancelingEvento && (
        <Modal
          title={`Confirmar cancelación: ${cancelingEvento.name}`}
          onClose={() => setCancelingEvento(null)}
        >
          <form
            onSubmit={(e) => {
              e.preventDefault()
              if (!cancellationReason.trim()) return
              const eventId = cancelingEvento.id
              api.cancelarEvento(eventId, cancellationReason).then(async () => {
                setCancelingEvento(null)
                setCancellationReason('')
                // Sin recargar la página: la recarga borraría el aviso fallido antes de leerlo.
                void queryClient.invalidateQueries({ queryKey: ['eventos'] })
                const aviso = await sendCancellationNotice(eventId)
                setCancelNoticeError(aviso.ok ? null : { message: aviso.message })
              })
            }}
            className="space-y-4"
          >
            <div className="rounded-xl border border-rose-200 bg-rose-50/80 p-3.5 text-rose-900 dark:border-rose-900/50 dark:bg-rose-950/40 dark:text-rose-200">
              <h4 className="flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-rose-700 dark:text-rose-400">
                <AlertTriangle size={15} /> ¿Está seguro de que desea cancelar este evento?
              </h4>
              <p className="mt-1.5 text-xs sm:text-sm leading-relaxed">
                El evento cambiará a estado <strong>Cancelado</strong> y no admitirá más cambios.
              </p>
            </div>

            {cancelingEvento.externalUrl ? (
              <div className="rounded-xl border border-amber-200 bg-amber-50 p-3.5 text-amber-900 dark:border-amber-900/50 dark:bg-amber-950/40 dark:text-amber-200">
                <h5 className="flex items-center gap-1.5 text-xs font-bold uppercase tracking-wider text-amber-800 dark:text-amber-300">
                  <Mail size={14} /> Notificación Automática de Cancelación
                </h5>
                <p className="mt-1 text-xs leading-relaxed">
                  La campaña de este evento ya fue lanzada. Al confirmar la cancelación,{' '}
                  <strong>
                    se enviará inmediatamente un correo electrónico a todos los participantes
                    registrados
                  </strong>{' '}
                  informándoles sobre la cancelación oficial del evento.
                </p>
              </div>
            ) : (
              <p className="text-xs text-slate-500 dark:text-slate-400">
                Este evento aún no tiene convocatoria lanzada ni correos enviados a participantes.
              </p>
            )}

            <div>
              <label className="mb-1.5 block text-xs font-bold text-slate-700 dark:text-slate-300">
                Motivo / Justificación de cancelación <span className="text-rose-500">*</span>
              </label>
              <textarea
                rows={3}
                required
                value={cancellationReason}
                onChange={(e) => setCancellationReason(e.target.value)}
                placeholder="Escriba la justificación oficial para cancelar este evento..."
                className="w-full rounded-xl border border-slate-200 bg-white p-2.5 text-xs text-slate-900 focus:border-blue-600 focus:outline-none dark:border-slate-700 dark:bg-slate-900 dark:text-white"
              />
            </div>

            <div className="flex justify-end gap-2 border-t border-slate-100 pt-3 dark:border-slate-800">
              <Button variant="secondary" onClick={() => setCancelingEvento(null)}>
                Volver
              </Button>
              <Button
                type="submit"
                className="bg-rose-600 text-white hover:bg-rose-700 font-bold cursor-pointer"
              >
                Sí, cancelar evento y notificar
              </Button>
            </div>
          </form>
        </Modal>
      )}
    </div>
  )
}

/** Menú desplegable de 3 puntos con las opciones del evento. */
function EventoActionsMenu({
  evento,
  onEdit,
  onDuplicate,
  onCloseEvent,
  onCancelEvent,
}: {
  evento: EventoListItem
  onEdit: (e: EventoListItem) => void
  onDuplicate: (e: EventoListItem) => void
  onCloseEvent: (e: EventoListItem) => void
  onCancelEvent: (e: EventoListItem) => void
}) {
  const [open, setOpen] = useState(false)
  const anchorRef = useRef<HTMLButtonElement>(null)

  return (
    <div className="relative" onClick={(e) => e.stopPropagation()}>
      <IconButton
        ref={anchorRef}
        onClick={() => setOpen((prev) => !prev)}
        aria-label="Opciones del evento"
        title="Opciones del evento"
      >
        <MoreVertical size={16} />
      </IconButton>

      <ActionMenu open={open} onClose={() => setOpen(false)} anchorRef={anchorRef} className="w-48">
        <MenuItem
          onClick={() => {
            setOpen(false)
            onEdit(evento)
          }}
        >
          <Edit3 size={14} className="text-blue-600" /> Editar
        </MenuItem>

        <MenuItem
          onClick={() => {
            setOpen(false)
            onDuplicate(evento)
          }}
        >
          <Copy size={14} className="text-purple-600" /> Duplicar
        </MenuItem>

        {evento.status !== 'closed' && (
          <MenuItem
            className="text-emerald-700 hover:bg-emerald-50 dark:text-emerald-300 dark:hover:bg-emerald-950/40"
            onClick={() => {
              setOpen(false)
              onCloseEvent(evento)
            }}
          >
            <Lock size={14} /> Cerrar evento
          </MenuItem>
        )}

        {evento.status !== 'cancelled' && (
          <MenuItem
            tone="danger"
            onClick={() => {
              setOpen(false)
              onCancelEvent(evento)
            }}
          >
            <XCircle size={14} /> Cancelar evento
          </MenuItem>
        )}
      </ActionMenu>
    </div>
  )
}

/**
 * Editar reemplaza la representación completa del evento, y el listado no trae todo lo
 * editable (el link de la convocatoria, por ejemplo). Por eso el modal trabaja sobre el
 * detalle recién leído y no sobre la fila: con la fila, cada guardado borraría lo que la
 * fila no sabe.
 */
function EditarEventoModal({
  eventoId,
  onClose,
  onSaved,
}: {
  eventoId: string
  onClose: () => void
  onSaved: (evento: EventoDetail) => void
}) {
  const { data: evento, isLoading, error } = useEvento(eventoId)
  const updateEvento = useUpdateEvento(eventoId)

  if (isLoading || error != null || !evento) {
    return (
      <Modal title="Editar evento" onClose={onClose}>
        {error != null ? <ErrorNote error={error} /> : <Spinner />}
      </Modal>
    )
  }

  return (
    <EventoFormModal
      evento={evento}
      onClose={onClose}
      onUpdate={(body, ciudad) => updateEvento.mutate({ body, ciudad }, { onSuccess: onSaved })}
      pending={updateEvento.isPending}
      error={updateEvento.error}
    />
  )
}
