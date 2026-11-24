import { useState, useMemo } from 'react'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { Link, useNavigate } from 'react-router-dom'
import { Building2, CalendarDays, Gift, Plus, Trash2, Eye, Filter, Play } from 'lucide-react'
import { Breadcrumbs } from '@/components/ui/Breadcrumbs'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { ErrorNote, Spinner } from '@/components/ui/Feedback'
import { TextInput } from '@/components/ui/Field'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { ViewModeToggle, type ViewMode } from '@/components/ui/ViewModeToggle'
import { CardGrid, GridCard, GridCardStats } from '@/components/ui/GridCard'
import { RowActionMenu } from '@/components/ui/RowActionMenu'
import { MenuItem } from '@/components/ui/MenuItem'
import type { RaffleListItem } from '@/api/types'
import { useCan } from '@/auth/AuthProvider'
import { ROLES } from '@/auth/roles'
import { formatDate } from '@/lib/format'
import { raffleStateLabel } from '@/lib/labels'
import { getBranding } from '@/lib/branding'
import { useDeleteRaffle, useRaffleList } from './hooks'
import { EstadoSorteoBadge } from './components/EstadoSorteoBadge'
import { NuevoSorteoModal } from './components/NuevoSorteoModal'

export function SorteosListPage() {
  const navigate = useNavigate()
  const { data, isLoading, error } = useRaffleList()
  const deleteRaffle = useDeleteRaffle()
  const canWrite = useCan(ROLES.rafflesWrite)
  const [viewMode, setViewMode] = useState<ViewMode>('list')
  const [search, setSearch] = useState('')
  const [estadoFilter, setEstadoFilter] = useState<string>('todos')
  const [showNuevoModal, setShowNuevoModal] = useState(false)
  const [sorteoABorrar, setSorteoABorrar] = useState<RaffleListItem | null>(null)

  const sorteos = useMemo(() => data?.items ?? [], [data])
  const filteredSorteos = useMemo(() => {
    return sorteos.filter((s) => {
      const matchSearch = s.eventName.toLowerCase().includes(search.toLowerCase())
      const matchEstado = estadoFilter === 'todos' || s.state === estadoFilter
      return matchSearch && matchEstado
    })
  }, [sorteos, search, estadoFilter])

  // Solo un borrador se puede eliminar, y solo con permiso de armado (Decisión 12)
  const sePuedeBorrar = (sorteo: RaffleListItem) => sorteo.state === 'draft' && canWrite.allowed
  const enVivo = (sorteo: RaffleListItem) => sorteo.state !== 'completed' && sorteo.prizeCount > 0

  const sorteoColumns: Column<RaffleListItem>[] = [
    {
      key: 'evento',
      header: 'EVENTO',
      cell: (sorteo) => {
        const custom = getBranding(sorteo.eventId)
        return (
          <div className="flex items-center gap-2.5">
            {custom.hasCustomEventBranding && custom.logoUrl && (
              <img
                src={custom.logoUrl}
                alt={custom.companyName}
                className="h-6 w-6 rounded object-contain bg-white border border-slate-200 dark:border-slate-700 p-0.5 shrink-0"
              />
            )}
            <div>
              <span className="font-sans text-sm font-bold text-slate-900 group-hover:text-blue-600 dark:text-white dark:group-hover:text-blue-400">
                {sorteo.eventName}
              </span>
              {custom.hasCustomEventBranding && (
                <div className="text-[11px] font-medium text-slate-500 dark:text-slate-400">
                  {custom.companyName}
                </div>
              )}
            </div>
          </div>
        )
      },
    },
    {
      key: 'fecha',
      header: 'FECHA DEL EVENTO',
      cell: (sorteo) => (
        <span className="font-body text-xs text-slate-600 dark:text-slate-300">
          {formatDate(sorteo.eventStartDate)}
        </span>
      ),
    },
    {
      key: 'premios',
      header: 'PREMIOS / GANADORES',
      align: 'center',
      cell: (sorteo) => (
        <span className="inline-flex items-center gap-1 font-semibold text-xs text-slate-700 dark:text-slate-300">
          <span>{sorteo.prizeCount} premios</span>
          <span className="text-slate-400">•</span>
          <span className="font-bold text-emerald-600 dark:text-emerald-400">
            {sorteo.drawnCount} ganadores
          </span>
        </span>
      ),
    },
    {
      key: 'entregados',
      header: 'ENTREGADOS',
      align: 'center',
      cell: (sorteo) => (
        <span className="font-semibold text-xs text-slate-700 dark:text-slate-300">
          {sorteo.redeemedCount}
        </span>
      ),
    },
    {
      key: 'estado',
      header: 'ESTADO',
      cell: (sorteo) => <EstadoSorteoBadge estado={sorteo.state} />,
    },
    {
      key: 'actions',
      header: 'ACCIONES',
      align: 'right',
      cell: (sorteo) => (
        <div className="flex items-center justify-end gap-1" onClick={(e) => e.stopPropagation()}>
          {enVivo(sorteo) && (
            <Button
              size="sm"
              onClick={() => navigate(`/sorteo/sorteos/${sorteo.eventId}/ejecutar`)}
              className="bg-blue-600 hover:bg-blue-700 text-white font-bold text-xs cursor-pointer h-7 px-2.5"
            >
              <Play className="w-3 h-3 mr-1 fill-current" /> En Vivo
            </Button>
          )}
          <RowActionMenu label="Opciones del sorteo">
            <MenuItem onClick={() => navigate(`/sorteo/sorteos/${sorteo.eventId}`)}>
              <Eye size={14} className="text-blue-600" /> Ver detalles
            </MenuItem>
            {sePuedeBorrar(sorteo) && (
              <MenuItem tone="danger" onClick={() => setSorteoABorrar(sorteo)}>
                <Trash2 size={14} /> Eliminar sorteo
              </MenuItem>
            )}
          </RowActionMenu>
        </div>
      ),
    },
  ]

  return (
    <div className="space-y-6">
      {/* Header + Breadcrumbs */}
      <div className="space-y-2">
        <Breadcrumbs
          items={[
            { label: 'Inicio', to: '/app' },
            { label: 'Rifas en vivo', to: '/sorteo' },
            { label: 'Todos los sorteos' },
          ]}
        />
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div>
            <h1 className="text-2xl font-black tracking-tight text-slate-900 dark:text-slate-100">
              Sorteos & Rifas
            </h1>
            <p className="text-sm text-slate-500 dark:text-slate-400">
              Gestión de rifas institucionales, una por evento.
            </p>
          </div>
          <div className="flex items-center gap-2.5 shrink-0">
            <ViewModeToggle value={viewMode} onChange={setViewMode} size="sm" />
            <Button
              onClick={() => setShowNuevoModal(true)}
              disabled={!canWrite.allowed}
              title={canWrite.title}
              className="bg-brand-deep hover:bg-brand-deep/90 text-white font-bold cursor-pointer"
            >
              <Plus className="w-4 h-4 mr-2" />
              Nuevo Sorteo
            </Button>
          </div>
        </div>
      </div>

      {/* Barra de Búsqueda y Filtros */}
      <Card className="p-4 bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 rounded-2xl shadow-2xs">
        <div className="flex flex-col sm:flex-row gap-4 items-center justify-between">
          <div className="w-full sm:w-96">
            <TextInput
              aria-label="Buscar por evento"
              placeholder="Buscar por evento…"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>

          <div className="flex items-center gap-2 w-full sm:w-auto">
            <Filter className="w-4 h-4 text-slate-400" />
            <SearchableSelect
              value={estadoFilter}
              onChange={(e) => setEstadoFilter(e.target.value)}
              className="rounded-xl border border-slate-200 bg-white px-3 py-2 text-xs font-semibold text-slate-700 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-200 cursor-pointer"
            >
              <option value="todos">Todos los Estados</option>
              {(['draft', 'in_progress', 'completed'] as const).map((state) => (
                <option key={state} value={state}>
                  {raffleStateLabel[state]}
                </option>
              ))}
            </SearchableSelect>
          </div>
        </div>
      </Card>

      {error != null && <ErrorNote error={error} />}
      {data && data.total > sorteos.length && (
        <p className="text-xs text-slate-500">
          Se muestran los {sorteos.length} sorteos de los eventos más recientes, de {data.total}.
        </p>
      )}

      {/* Listado / Tabla según ViewMode */}
      {isLoading ? (
        <Spinner />
      ) : filteredSorteos.length === 0 ? (
        <Card className="p-12 text-center text-slate-400 bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 rounded-2xl shadow-2xs">
          <Gift className="w-12 h-12 mx-auto mb-3 text-slate-300 dark:text-slate-600" />
          <p className="font-semibold text-slate-700 dark:text-slate-300">
            No se encontraron sorteos.
          </p>
          <p className="text-xs text-slate-500 mt-1">
            Prueba cambiando los filtros de búsqueda o crea uno nuevo.
          </p>
        </Card>
      ) : viewMode === 'list' ? (
        <Card className="bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 rounded-2xl overflow-hidden p-0 shadow-2xs">
          <div className="hidden md:block">
            <DataTable<RaffleListItem>
              rows={filteredSorteos}
              getRowKey={(sorteo) => sorteo.raffleId}
              onRowClick={(sorteo) => navigate(`/sorteo/sorteos/${sorteo.eventId}`)}
              columns={sorteoColumns}
              variant="default"
              className="rounded-none border-0 shadow-none"
            />
          </div>

          {/* Versión móvil lista */}
          <ul className="divide-y divide-slate-100 md:hidden dark:divide-slate-800">
            {filteredSorteos.map((sorteo) => (
              <li key={sorteo.raffleId} className="relative">
                <div
                  onClick={() => navigate(`/sorteo/sorteos/${sorteo.eventId}`)}
                  className="block px-4 py-3.5 hover:bg-slate-50 dark:hover:bg-slate-800/40 cursor-pointer"
                >
                  <div className="flex items-start justify-between gap-2">
                    {(() => {
                      const custom = getBranding(sorteo.eventId)
                      return (
                        <div className="flex items-center gap-2">
                          {custom.hasCustomEventBranding && custom.logoUrl && (
                            <img
                              src={custom.logoUrl}
                              alt={custom.companyName}
                              className="h-6 w-6 rounded object-contain bg-white border border-slate-200 dark:border-slate-700 p-0.5 shrink-0"
                            />
                          )}
                          <div>
                            <p className="font-sans text-sm font-bold text-slate-900 dark:text-white">
                              {sorteo.eventName}
                            </p>
                            {custom.hasCustomEventBranding && (
                              <p className="text-[11px] font-medium text-slate-500 dark:text-slate-400">
                                {custom.companyName}
                              </p>
                            )}
                          </div>
                        </div>
                      )
                    })()}
                    <EstadoSorteoBadge estado={sorteo.state} />
                  </div>
                  <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">
                    {formatDate(sorteo.eventStartDate)}
                  </p>
                  <div className="mt-1.5 flex items-center justify-between text-xs text-slate-500">
                    <span>{sorteo.prizeCount} premios</span>
                    <span className="font-bold text-emerald-600 dark:text-emerald-400">
                      {sorteo.drawnCount}/{sorteo.prizeCount} ganadores
                    </span>
                  </div>
                </div>
              </li>
            ))}
          </ul>
        </Card>
      ) : (
        <CardGrid>
          {filteredSorteos.map((sorteo) => (
            <GridCard
              key={sorteo.raffleId}
              tags={<EstadoSorteoBadge estado={sorteo.state} />}
              aside={
                sePuedeBorrar(sorteo) && (
                  <button
                    type="button"
                    onClick={() => setSorteoABorrar(sorteo)}
                    className="text-slate-400 hover:text-rose-600 p-1 rounded transition-colors cursor-pointer"
                    title="Eliminar sorteo"
                    aria-label={`Eliminar el sorteo de ${sorteo.eventName}`}
                  >
                    <Trash2 className="w-4 h-4" />
                  </button>
                )
              }
              title={sorteo.eventName}
              subtitle={(() => {
                const custom = getBranding(sorteo.eventId)
                return (
                  <span className="flex flex-wrap items-center gap-x-3 gap-y-1">
                    <span className="inline-flex items-center gap-1.5">
                      <CalendarDays className="h-4 w-4 text-slate-400" />
                      {formatDate(sorteo.eventStartDate)}
                    </span>
                    {custom.hasCustomEventBranding && (
                      <span className="inline-flex items-center gap-1.5 border-l border-slate-200 pl-3 dark:border-slate-700">
                        {custom.logoUrl ? (
                          <img
                            src={custom.logoUrl}
                            alt=""
                            className="h-4 w-4 rounded object-contain"
                          />
                        ) : (
                          <Building2 className="h-4 w-4 text-slate-400" />
                        )}
                        {custom.companyName}
                      </span>
                    )}
                  </span>
                )
              })()}
              footerStart={
                <Link to={`/sorteo/sorteos/${sorteo.eventId}`}>
                  <Button
                    variant="outline"
                    size="sm"
                    className="text-xs font-semibold cursor-pointer"
                  >
                    <Eye className="w-3.5 h-3.5 mr-1" />
                    Ver Detalles
                  </Button>
                </Link>
              }
              footerEnd={
                enVivo(sorteo) && (
                  <Link to={`/sorteo/sorteos/${sorteo.eventId}/ejecutar`}>
                    <Button
                      size="sm"
                      className="text-xs font-bold bg-blue-600 hover:bg-blue-700 text-white cursor-pointer"
                    >
                      <Play className="w-3.5 h-3.5 mr-1 fill-current" />
                      En Vivo
                    </Button>
                  </Link>
                )
              }
            >
              <GridCardStats
                items={[
                  { label: 'Premios', value: sorteo.prizeCount },
                  {
                    label: 'Ganadores',
                    value: `${sorteo.drawnCount}/${sorteo.prizeCount}`,
                    tone: 'emerald',
                  },
                  { label: 'Entregados', value: sorteo.redeemedCount },
                ]}
              />
            </GridCard>
          ))}
        </CardGrid>
      )}

      {/* Modal Nuevo Sorteo */}
      {showNuevoModal && (
        <NuevoSorteoModal
          onClose={() => setShowNuevoModal(false)}
          onCreated={(eventId) => navigate(`/sorteo/sorteos/${eventId}`)}
        />
      )}

      {sorteoABorrar && (
        <ConfirmDialog
          title="Eliminar sorteo"
          confirmLabel="Eliminar sorteo"
          destructive
          pending={deleteRaffle.isPending}
          error={deleteRaffle.error}
          onCancel={() => {
            deleteRaffle.reset()
            setSorteoABorrar(null)
          }}
          onConfirm={() =>
            deleteRaffle.mutate(sorteoABorrar.eventId, {
              onSuccess: () => setSorteoABorrar(null),
            })
          }
        >
          <p>
            Se eliminará el sorteo de <strong>{sorteoABorrar.eventName}</strong> con sus rondas y
            premios. El evento podrá tener un sorteo nuevo.
          </p>
        </ConfirmDialog>
      )}
    </div>
  )
}
