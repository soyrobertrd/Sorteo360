import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  ArrowRight,
  ChevronLeft,
  ChevronRight,
  Calendar as CalendarIcon,
  Building,
} from 'lucide-react'
import { useEventosList, useCreateEvento } from './hooks'
import { Breadcrumbs } from '@/components/ui/Breadcrumbs'
import { Card } from '@/components/ui/Card'
import { Spinner, ErrorNote } from '@/components/ui/Feedback'
import { EventoFormModal } from './components/EventoFormModal'
import { EVENTO_TYPES, eventoStatusLabel, eventoTypeLabel } from '@/lib/labels'
import { formatDateRange, formatMoney } from '@/lib/format'
import type { EventoListItem, EventoType } from '@/api/types'
import { useCan } from '@/auth/AuthProvider'
import { ROLES } from '@/auth/roles'
import { api } from '@/api'
import { NewButton } from '@/components/ui/NewButton'

const MONTH_NAMES = [
  'Enero',
  'Febrero',
  'Marzo',
  'Abril',
  'Mayo',
  'Junio',
  'Julio',
  'Agosto',
  'Septiembre',
  'Octubre',
  'Noviembre',
  'Diciembre',
]

const DAYS_OF_WEEK = ['Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb', 'Dom']

export function CalendarioPage() {
  const navigate = useNavigate()
  const canCrear = useCan(ROLES.eventsWrite)
  const [showCreateModal, setShowCreateModal] = useState(false)
  const createEvento = useCreateEvento()

  // Estado del mes seleccionado: Inicializado automáticamente en el día y mes actual
  const today = new Date()
  const [currentYear, setCurrentYear] = useState(today.getFullYear())
  const [currentMonth, setCurrentMonth] = useState(today.getMonth())
  const [filterType, setFilterType] = useState<EventoType | ''>('')
  const [hoveredDia, setHoveredDia] = useState<number | null>(null)

  const { data, isLoading, error } = useEventosList({ page: 1, pageSize: 100 })

  if (isLoading) return <Spinner label="Cargando calendario de eventos..." />
  if (error) return <ErrorNote error={error} />

  const eventos = data?.items ?? []

  // Navegar meses
  const handlePrevMonth = () => {
    if (currentMonth === 0) {
      setCurrentMonth(11)
      setCurrentYear((y) => y - 1)
    } else {
      setCurrentMonth((m) => m - 1)
    }
  }

  const handleNextMonth = () => {
    if (currentMonth === 11) {
      setCurrentMonth(0)
      setCurrentYear((y) => y + 1)
    } else {
      setCurrentMonth((m) => m + 1)
    }
  }

  // Días en el mes
  const daysInMonth = new Date(currentYear, currentMonth + 1, 0).getDate()
  // Primer día de la semana (1 = Lunes, 0 = Domingo)
  const firstDayIndex = (new Date(currentYear, currentMonth, 1).getDay() + 6) % 7

  // Filtrar eventos por tipo
  const filteredEventos = eventos.filter((e) => {
    if (filterType && e.type !== filterType) return false
    return true
  })

  // Mapear eventos por día del mes seleccionado
  const eventsByDay: Record<number, EventoListItem[]> = {}
  for (let day = 1; day <= daysInMonth; day++) {
    const currentDateStr = `${currentYear}-${String(currentMonth + 1).padStart(2, '0')}-${String(
      day,
    ).padStart(2, '0')}`

    eventsByDay[day] = filteredEventos.filter((e) => {
      // Evento cae dentro de este día si fechaInicio <= currentDateStr <= fechaFin
      return e.startDate <= currentDateStr && currentDateStr <= (e.endDate || e.startDate)
    })
  }

  // Lista total de eventos programados para el mes seleccionado
  const monthEvents = filteredEventos.filter((e) => {
    const eDate = new Date(e.startDate)
    return eDate.getFullYear() === currentYear && eDate.getMonth() === currentMonth
  })

  return (
    <div className="space-y-6">
      {/* Breadcrumb Oficial sin íconos */}
      <Breadcrumbs items={[{ label: 'Eventos', to: '/eventos/todos' }, { label: 'Calendario' }]} />

      {/* Encabezado Principal y Acciones */}
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="font-sans text-2xl font-extrabold tracking-tight text-slate-900 sm:text-3xl dark:text-white">
            Calendario de eventos
          </h1>
          <p className="mt-1 font-body text-xs text-slate-500 sm:text-sm dark:text-slate-400">
            Los eventos del mes, día por día.
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-3">
          <select
            aria-label="Tipo de evento"
            value={filterType}
            onChange={(e) => setFilterType(e.target.value as EventoType | '')}
            className="rounded-xl border border-slate-200 bg-white px-3 py-2 text-xs font-bold text-slate-800 transition-colors focus:border-blue-600 focus:outline-none dark:border-slate-700 dark:bg-slate-800 dark:text-slate-200 cursor-pointer"
          >
            <option value="">Todos los tipos</option>
            {EVENTO_TYPES.map((t) => (
              <option key={t} value={t}>
                {eventoTypeLabel[t]}
              </option>
            ))}
          </select>

          <NewButton
            onClick={() => setShowCreateModal(true)}
            disabled={!canCrear.allowed}
            title={canCrear.title}
          >
            Nuevo evento
          </NewButton>
        </div>
      </div>

      {/* Contenedor Principal del Calendario */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-4">
        {/* Calendario del mes, por semanas */}
        <Card className="min-w-0 space-y-4 rounded-xl border border-slate-200/80 bg-white p-5 shadow-2xs lg:col-span-3 dark:border-slate-800 dark:bg-slate-900">
          <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-100 pb-3 dark:border-slate-800">
            <div className="flex items-center gap-2">
              <span className="flex h-7 w-7 items-center justify-center rounded-md bg-blue-50 text-blue-600 dark:bg-blue-950/60 dark:text-blue-400">
                <CalendarIcon size={15} />
              </span>
              <div>
                <h3 className="text-sm font-bold text-slate-900 dark:text-white">
                  Calendario de eventos por mes
                </h3>
                <p className="text-[11px] font-medium text-slate-400">
                  Pasa el cursor sobre los días con evento para ver sus detalles
                </p>
              </div>
            </div>

            <div className="flex items-center gap-1.5">
              <button
                type="button"
                onClick={handlePrevMonth}
                aria-label="Mes anterior"
                className="flex h-7 w-7 cursor-pointer items-center justify-center rounded-md border border-slate-200 text-slate-600 transition-colors hover:bg-slate-100 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
              >
                <ChevronLeft size={14} />
              </button>
              <div className="min-w-28 rounded-md border border-slate-200/60 bg-slate-50 px-2.5 py-1 text-center text-xs font-bold text-slate-800 dark:border-slate-700 dark:bg-slate-800/70 dark:text-slate-200">
                {MONTH_NAMES[currentMonth]} {currentYear}
              </div>
              <button
                type="button"
                onClick={handleNextMonth}
                aria-label="Mes siguiente"
                className="flex h-7 w-7 cursor-pointer items-center justify-center rounded-md border border-slate-200 text-slate-600 transition-colors hover:bg-slate-100 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
              >
                <ChevronRight size={14} />
              </button>
            </div>
          </div>

          {/* Los 12 meses del año, con un punto en los que tienen eventos */}
          <div className="grid grid-cols-6 gap-1 pb-1 sm:grid-cols-12">
            {MONTH_NAMES.map((nombre, idx) => {
              const seleccionado = idx === currentMonth
              const conEventos = filteredEventos.some((e) => {
                const inicio = new Date(`${e.startDate}T00:00:00`)
                return inicio.getFullYear() === currentYear && inicio.getMonth() === idx
              })
              return (
                <button
                  key={nombre}
                  type="button"
                  onClick={() => setCurrentMonth(idx)}
                  className={`relative cursor-pointer rounded-md py-1.5 text-center text-xs font-semibold transition-all ${
                    seleccionado
                      ? 'bg-blue-600 font-bold text-white shadow-xs'
                      : 'text-slate-600 hover:bg-slate-100 dark:text-slate-400 dark:hover:bg-slate-800/70'
                  }`}
                >
                  {nombre.slice(0, 3)}
                  {conEventos && (
                    <span
                      className={`absolute right-1 top-1 h-1.5 w-1.5 rounded-full ${
                        seleccionado ? 'bg-white' : 'bg-blue-500'
                      }`}
                    />
                  )}
                </button>
              )
            })}
          </div>

          <div className="space-y-1 pt-1">
            <div className="grid grid-cols-7 gap-1.5 text-center">
              {DAYS_OF_WEEK.map((d) => (
                <div
                  key={d}
                  className="rounded-md bg-slate-50/70 py-1 text-[11px] font-bold text-slate-500 dark:bg-slate-800/40 dark:text-slate-400"
                >
                  {d}
                </div>
              ))}
            </div>

            <div className="grid grid-cols-7 gap-1.5">
              {Array.from({ length: firstDayIndex }).map((_, i) => (
                <div
                  key={`empty-${i}`}
                  className="min-h-15.5 rounded-md border border-dashed border-slate-100 bg-slate-50/20 p-2 opacity-30 sm:min-h-17.5 dark:border-slate-800/40"
                />
              ))}

              {Array.from({ length: daysInMonth }, (_, i) => i + 1).map((dia) => {
                const posicion = firstDayIndex + dia - 1
                const columna = posicion % 7
                const fila = Math.floor(posicion / 7)
                const delDia = eventsByDay[dia] ?? []
                const evento = delDia[0]
                const esHoy =
                  dia === today.getDate() &&
                  currentMonth === today.getMonth() &&
                  currentYear === today.getFullYear()
                const abierto = hoveredDia === dia && Boolean(evento)

                return (
                  <div
                    key={`dia-${dia}`}
                    onMouseEnter={() => evento && setHoveredDia(dia)}
                    onMouseLeave={() => setHoveredDia(null)}
                    onClick={() => evento && setHoveredDia(abierto ? null : dia)}
                    className={`relative flex min-h-15.5 min-w-0 flex-col justify-between rounded-md p-2 transition-all sm:min-h-17.5 ${
                      evento
                        ? 'cursor-pointer border border-blue-200 bg-blue-50/60 hover:border-blue-400 hover:shadow-md dark:border-blue-800/80 dark:bg-blue-950/30'
                        : 'border border-slate-100 bg-white hover:bg-slate-50 dark:border-slate-800/80 dark:bg-slate-900 dark:hover:bg-slate-800/30'
                    } ${esHoy ? 'ring-2 ring-brand-deep dark:ring-blue-500' : ''}`}
                  >
                    <div className="flex items-center justify-between">
                      <span
                        className={`text-xs ${
                          evento
                            ? 'font-black text-blue-700 dark:text-blue-300'
                            : 'font-semibold text-slate-700 dark:text-slate-400'
                        }`}
                      >
                        {dia}
                      </span>
                      {delDia.length > 1 && (
                        <span className="text-[10px] font-bold text-blue-600 dark:text-blue-400">
                          +{delDia.length - 1}
                        </span>
                      )}
                    </div>

                    {evento ? (
                      <div className="mt-1 min-w-0">
                        <div className="truncate rounded bg-blue-600 px-1.5 py-0.5 text-[10px] font-bold text-white shadow-xs">
                          {evento.name}
                        </div>
                      </div>
                    ) : (
                      <div className="h-4" />
                    )}

                    {abierto && (
                      <div
                        className={`absolute z-50 w-72 pt-1.5 ${
                          fila < 2 ? 'top-full' : 'bottom-full pb-1.5'
                        } ${
                          columna <= 1
                            ? 'left-0'
                            : columna >= 5
                              ? 'right-0'
                              : 'left-1/2 -translate-x-1/2'
                        }`}
                      >
                        <div className="space-y-2.5 rounded-lg border border-slate-200 bg-white p-3.5 text-left shadow-2xl dark:border-slate-700 dark:bg-slate-900">
                          {delDia.map((e) => (
                            <div
                              key={e.id}
                              className="space-y-1.5 border-b border-slate-100 pb-2 last:border-0 last:pb-0 dark:border-slate-800"
                            >
                              <div className="flex items-center justify-between gap-2">
                                <span className="text-[10px] font-extrabold uppercase tracking-wider text-blue-700 dark:text-blue-400">
                                  {eventoTypeLabel[e.type]}
                                </span>
                                <span className="rounded-md bg-slate-100 px-2 py-0.5 text-[10px] font-bold text-slate-600 dark:bg-slate-800 dark:text-slate-300">
                                  {eventoStatusLabel[e.status]}
                                </span>
                              </div>
                              <h4 className="text-sm font-black text-slate-900 dark:text-white">
                                {e.name}
                              </h4>
                              <p className="flex items-center gap-1 text-xs font-semibold text-blue-600 dark:text-blue-400">
                                <CalendarIcon size={12} />
                                {formatDateRange(e.startDate, e.endDate)}
                              </p>
                              <div className="flex items-center justify-between text-[11px] text-slate-500">
                                <span className="flex items-center gap-1 truncate">
                                  <Building size={12} /> {e.cities?.[0]?.name ?? '—'}
                                </span>
                                <button
                                  type="button"
                                  onClick={(ev) => {
                                    ev.stopPropagation()
                                    navigate(`/eventos/${e.id}`)
                                  }}
                                  className="flex shrink-0 items-center gap-1 text-xs font-bold text-blue-600 hover:text-blue-700 dark:text-blue-400"
                                >
                                  Ver evento <ArrowRight size={12} />
                                </button>
                              </div>
                            </div>
                          ))}
                        </div>
                      </div>
                    )}
                  </div>
                )
              })}
            </div>
          </div>

          <div className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-100 pt-2 text-xs dark:border-slate-800">
            <div className="flex items-center gap-2 font-medium text-slate-500 dark:text-slate-400">
              <span className="h-2 w-2 rounded-sm bg-blue-600" />
              <span>Días con evento</span>
              <span className="text-slate-300 dark:text-slate-700">·</span>
              <span className="text-[11px] text-slate-400">
                Pasa el ratón sobre los días azules para ver la ficha
              </span>
            </div>
            <span className="font-bold text-blue-600 dark:text-blue-400">
              {monthEvents.length}{' '}
              {monthEvents.length === 1 ? 'evento en agenda' : 'eventos en agenda'} (
              {MONTH_NAMES[currentMonth]})
            </span>
          </div>
        </Card>

        {/* Panel Lateral: Lista de Eventos del Mes */}
        <div className="space-y-6">
          <Card className="p-5 bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 rounded-2xl shadow-2xs">
            <h3 className="font-sans text-base font-extrabold text-slate-900 dark:text-white mb-3">
              Eventos de {MONTH_NAMES[currentMonth]}
            </h3>

            {monthEvents.length === 0 ? (
              <p className="text-xs text-slate-400 py-4 text-center font-medium">
                No hay eventos agendados en este mes.
              </p>
            ) : (
              <div className="space-y-3 max-h-125 overflow-y-auto pr-1">
                {monthEvents.map((evt) => (
                  <div
                    key={evt.id}
                    onClick={() => navigate(`/eventos/${evt.id}`)}
                    className="group cursor-pointer rounded-xl border border-slate-200/80 bg-slate-50/60 p-3 hover:border-blue-600 hover:bg-blue-50/50 transition-all dark:border-slate-800 dark:bg-slate-800/40"
                  >
                    <div className="flex items-center justify-between mb-1">
                      <span className="text-[10px] font-extrabold uppercase tracking-wider text-blue-700 dark:text-blue-400">
                        {eventoTypeLabel[evt.type]}
                      </span>
                      <span className="text-[11px] font-bold text-slate-500">
                        {formatDateRange(evt.startDate, evt.endDate)}
                      </span>
                    </div>

                    <h4 className="font-sans text-xs font-extrabold text-slate-900 group-hover:text-blue-600 dark:text-white dark:group-hover:text-blue-400">
                      {evt.name}
                    </h4>

                    <div className="mt-2 flex items-center justify-between text-[11px] text-slate-500">
                      <span className="flex items-center gap-1">
                        <Building size={12} /> {evt.cities?.[0]?.name ?? '—'}
                      </span>
                      {evt.estimatedBudget && (
                        <span className="font-bold text-slate-800 dark:text-slate-200">
                          {formatMoney(evt.estimatedBudget)}
                        </span>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </Card>
        </div>
      </div>

      {/* Modal para Crear Evento */}
      {showCreateModal && (
        <EventoFormModal
          onClose={() => {
            setShowCreateModal(false)
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
              setShowCreateModal(false)
              navigate(`/eventos/${id}`)
            } catch (err) {
              console.error(err)
            }
          }}
          pending={createEvento.isPending}
          error={createEvento.error}
        />
      )}
    </div>
  )
}
