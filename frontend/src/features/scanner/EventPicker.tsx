import { Calendar, ChevronRight } from 'lucide-react'
import type { DoorEventResponse } from '@/api/types'
import { ErrorNote, Spinner } from '@/components/ui/Feedback'
import { formatDateRange } from '@/lib/format'

/**
 * Los eventos de entre ayer y mañana, para fijar el terminal a uno (spec convocatorias/10 §4).
 * Nunca hay uno elegido por defecto: quien toma el terminal lo elige a sabiendas.
 */
export function EventPicker({
  events,
  isLoading,
  error,
  currentId,
  onPick,
  onCancel,
}: {
  events: DoorEventResponse[] | undefined
  isLoading: boolean
  error: unknown
  currentId: string | null
  onPick: (event: DoorEventResponse) => void
  /** Solo si ya hay un evento elegido al que volver. */
  onCancel?: () => void
}) {
  return (
    <section className="bg-white p-4 rounded-2xl border border-slate-200/80 shadow-xs space-y-3">
      <div className="flex items-center justify-between">
        <h2 className="text-xs font-black uppercase tracking-wider text-slate-500 flex items-center gap-1.5">
          <Calendar className="w-4 h-4 text-blue-600" />
          Elija el evento de esta puerta
        </h2>
        {onCancel && (
          <button
            type="button"
            onClick={onCancel}
            className="text-[11px] font-bold text-slate-500 hover:text-slate-900 cursor-pointer"
          >
            Volver
          </button>
        )}
      </div>
      {isLoading ? (
        <Spinner />
      ) : error != null ? (
        <ErrorNote error={error} />
      ) : !events?.length ? (
        <p className="text-xs text-slate-500 py-6 text-center">
          No hay eventos entre ayer y mañana. El terminal solo ofrece los eventos de estos días.
        </p>
      ) : (
        <ul className="space-y-2">
          {events.map((ev) => (
            <li key={ev.id}>
              <button
                type="button"
                onClick={() => onPick(ev)}
                className={`w-full text-left p-3 rounded-xl border flex items-center justify-between gap-2 cursor-pointer transition-colors ${
                  ev.id === currentId
                    ? 'border-blue-500 bg-blue-50'
                    : 'border-slate-200 hover:border-blue-400 hover:bg-slate-50'
                }`}
              >
                <span className="min-w-0">
                  <span className="block text-sm font-bold text-slate-900">{ev.name}</span>
                  <span className="block text-[11px] text-slate-500">
                    {formatDateRange(ev.startDate, ev.endDate)}
                    {ev.cities.length > 0 && ` • ${ev.cities.map((c) => c.name).join(', ')}`}
                  </span>
                </span>
                <ChevronRight className="w-4 h-4 text-slate-400 shrink-0" />
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
