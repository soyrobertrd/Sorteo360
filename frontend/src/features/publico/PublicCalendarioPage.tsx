import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { ChevronLeft, ChevronRight, Calendar as CalendarIcon } from 'lucide-react'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Spinner, ErrorNote } from '@/components/ui/Feedback'
import { EVENTO_TYPES, eventoTypeLabel } from '@/lib/labels'
import { formatDateRange } from '@/lib/format'
import type { EventoType, PublicEvento } from '@/api/types'
import { usePublicEventos } from './hooks'
import { PublicNav } from './PublicNav'

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

/** Vista pública de solo lectura del calendario interno (`eventos/CalendarioPage.tsx`),
 * sin creación de eventos ni gating de permisos. Lee la misma cartelera que
 * `PublicEventosListPage`, que ya viene recortada a lo público y vigente. */
export function PublicCalendarioPage() {
  const navigate = useNavigate()
  const today = new Date()
  const [currentYear, setCurrentYear] = useState(today.getFullYear())
  const [currentMonth, setCurrentMonth] = useState(today.getMonth())
  const [filterType, setFilterType] = useState<EventoType | ''>('')

  const { data, isLoading, error } = usePublicEventos()
  const eventosVigentes = data ?? []

  if (isLoading) return <Spinner label="Cargando calendario de eventos..." />
  if (error) return <ErrorNote error={error} />

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

  const daysInMonth = new Date(currentYear, currentMonth + 1, 0).getDate()
  const firstDayIndex = (new Date(currentYear, currentMonth, 1).getDay() + 6) % 7

  const filteredEventos = eventosVigentes.filter((e) => !filterType || e.type === filterType)

  const eventsByDay: Record<number, PublicEvento[]> = {}
  for (let day = 1; day <= daysInMonth; day++) {
    const currentDateStr = `${currentYear}-${String(currentMonth + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`
    eventsByDay[day] = filteredEventos.filter(
      (e) => e.startDate <= currentDateStr && currentDateStr <= (e.endDate || e.startDate),
    )
  }

  const monthEvents = filteredEventos.filter((e) => {
    const eDate = new Date(e.startDate)
    return eDate.getFullYear() === currentYear && eDate.getMonth() === currentMonth
  })

  return (
    <div className="space-y-6 pb-8">
      <PublicNav />

      <div className="mx-auto max-w-7xl space-y-6 px-4 sm:px-6 lg:px-8">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div>
            <h1 className="font-sans text-2xl font-extrabold tracking-tight text-slate-900 sm:text-3xl dark:text-white">
              Calendario público de eventos
            </h1>
            <p className="mt-1 font-body text-xs text-slate-500 sm:text-sm dark:text-slate-400">
              Los eventos abiertos, por mes.
            </p>
          </div>

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
        </div>

        <div className="grid grid-cols-1 gap-6 lg:grid-cols-4">
          <Card className="p-6 bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 rounded-2xl shadow-2xs lg:col-span-3 space-y-4">
            <div className="flex items-center justify-between border-b border-slate-100 pb-4 dark:border-slate-800">
              <div className="flex items-center gap-3">
                <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-blue-50 text-blue-700 border border-blue-200 dark:bg-blue-950/50 dark:text-blue-300">
                  <CalendarIcon size={20} />
                </div>
                <div>
                  <h2 className="font-sans text-xl font-extrabold text-slate-900 dark:text-white">
                    {MONTH_NAMES[currentMonth]} {currentYear}
                  </h2>
                  <p className="text-xs font-semibold text-slate-400">
                    {monthEvents.length} eventos vigentes este mes
                  </p>
                </div>
              </div>

              <div className="flex items-center gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => {
                    const now = new Date()
                    setCurrentMonth(now.getMonth())
                    setCurrentYear(now.getFullYear())
                  }}
                  className="text-xs font-bold border-slate-200 bg-white hover:bg-slate-50 dark:border-slate-700 dark:bg-slate-800 cursor-pointer"
                >
                  Hoy
                </Button>
                <div className="flex items-center gap-1">
                  <Button
                    variant="outline"
                    size="icon"
                    onClick={handlePrevMonth}
                    aria-label="Mes anterior"
                    className="h-8 w-8 rounded-lg border-slate-200 dark:border-slate-700 cursor-pointer"
                  >
                    <ChevronLeft size={16} />
                  </Button>
                  <Button
                    variant="outline"
                    size="icon"
                    onClick={handleNextMonth}
                    aria-label="Mes siguiente"
                    className="h-8 w-8 rounded-lg border-slate-200 dark:border-slate-700 cursor-pointer"
                  >
                    <ChevronRight size={16} />
                  </Button>
                </div>
              </div>
            </div>

            <div className="grid grid-cols-7 text-center font-sans text-xs font-bold text-slate-400 uppercase tracking-wider border-b border-slate-100 pb-2 dark:border-slate-800">
              {DAYS_OF_WEEK.map((d) => (
                <div key={d} className="py-1">
                  {d}
                </div>
              ))}
            </div>

            <div className="grid grid-cols-7 gap-1 sm:gap-2">
              {Array.from({ length: firstDayIndex }).map((_, idx) => (
                <div
                  key={`empty-${idx}`}
                  className="min-h-22.5 rounded-xl bg-slate-50/40 p-2 dark:bg-slate-900/30 opacity-40"
                />
              ))}

              {Array.from({ length: daysInMonth }).map((_, idx) => {
                const dayNum = idx + 1
                const dayEvents = eventsByDay[dayNum] ?? []
                const isToday =
                  dayNum === today.getDate() &&
                  currentMonth === today.getMonth() &&
                  currentYear === today.getFullYear()

                return (
                  <div
                    key={`day-${dayNum}`}
                    className={`min-h-25 flex flex-col justify-between rounded-xl border p-2 transition-all ${
                      isToday
                        ? 'border-blue-500 bg-blue-50/30 dark:border-blue-500 dark:bg-blue-950/20'
                        : 'border-slate-100 bg-white hover:border-slate-300 dark:border-slate-800 dark:bg-slate-900'
                    }`}
                  >
                    <div className="flex items-center justify-between">
                      <span
                        className={`inline-flex h-6 w-6 items-center justify-center rounded-full text-xs font-bold ${
                          isToday
                            ? 'bg-brand-deep text-white shadow-xs'
                            : 'text-slate-700 dark:text-slate-300'
                        }`}
                      >
                        {dayNum}
                      </span>
                      {dayEvents.length > 0 && (
                        <span className="text-[10px] font-extrabold text-blue-600 dark:text-blue-400">
                          {dayEvents.length} {dayEvents.length === 1 ? 'evento' : 'eventos'}
                        </span>
                      )}
                    </div>

                    <div className="mt-1.5 space-y-1 overflow-y-auto max-h-18.75">
                      {dayEvents.map((evt) => (
                        <div
                          key={evt.id}
                          onClick={() => navigate(`/publico/eventos/${evt.id}`)}
                          title={`${evt.name} (${eventoTypeLabel[evt.type]})`}
                          className="cursor-pointer rounded-lg border border-blue-200 bg-blue-100 px-1.5 py-1 text-[11px] font-bold truncate text-blue-900 transition-transform hover:scale-[1.02] dark:border-blue-900 dark:bg-blue-950 dark:text-blue-300"
                        >
                          {evt.name}
                        </div>
                      ))}
                    </div>
                  </div>
                )
              })}
            </div>
          </Card>

          <div className="space-y-6">
            <Card className="p-5 bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 rounded-2xl shadow-2xs">
              <h3 className="font-sans text-base font-extrabold text-slate-900 dark:text-white mb-3">
                Eventos de {MONTH_NAMES[currentMonth]}
              </h3>

              {monthEvents.length === 0 ? (
                <p className="text-xs text-slate-400 py-4 text-center font-medium">
                  No hay eventos vigentes en este mes.
                </p>
              ) : (
                <div className="space-y-3 max-h-125 overflow-y-auto pr-1">
                  {monthEvents.map((evt) => (
                    <div
                      key={evt.id}
                      onClick={() => navigate(`/publico/eventos/${evt.id}`)}
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
                    </div>
                  ))}
                </div>
              )}
            </Card>
          </div>
        </div>
      </div>
    </div>
  )
}
