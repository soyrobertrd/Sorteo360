import { useEffect, useState } from 'react'
import { Loader2, Search, Undo2, UserCheck } from 'lucide-react'
import type { CheckInResponse, DoorListItem } from '@/api/types'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { ErrorNote, Spinner } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { formatDateTime, fullName } from '@/lib/format'
import { useCheckInByRegistration, useDoorList, useUndoCheckIn } from './hooks'

type Filtro = 'todos' | 'pendientes' | 'ingresaron'

const CHECKED_IN: Record<Filtro, boolean | undefined> = {
  todos: undefined,
  pendientes: false,
  ingresaron: true,
}

const PAGE_SIZE = 50

/**
 * La lista de la puerta del evento (spec convocatorias/09 §5): el camino a mano. Se busca a la
 * persona, se mira su documento y se le hace check-in por id. Deshacer solo se ofrece aquí, en una
 * fila que ya entró, y siempre con confirmación (spec convocatorias/10 §7). Nunca lista premios.
 */
export function DoorListSheet({
  eventId,
  eventName,
  initialSearch,
  onCheckedIn,
  onClose,
}: {
  eventId: string
  eventName: string
  initialSearch: string
  onCheckedIn: (checkIn: CheckInResponse) => void
  onClose: () => void
}) {
  const [texto, setTexto] = useState(initialSearch)
  const [search, setSearch] = useState(initialSearch.trim())
  const [filtro, setFiltro] = useState<Filtro>('todos')
  const [deshacer, setDeshacer] = useState<DoorListItem | null>(null)

  // Una consulta por pausa al escribir, no por tecla
  useEffect(() => {
    const t = setTimeout(() => setSearch(texto.trim()), 300)
    return () => clearTimeout(t)
  }, [texto])

  const list = useDoorList(eventId, {
    search: search || undefined,
    checkedIn: CHECKED_IN[filtro],
    pageSize: PAGE_SIZE,
  })
  const checkIn = useCheckInByRegistration(eventId)
  const undo = useUndoCheckIn(eventId)

  const items = list.data?.items ?? []

  return (
    <Modal
      sheet
      width="sm:max-w-md"
      // Escape con la confirmación abierta cierra solo la confirmación
      onClose={deshacer ? () => undefined : onClose}
      title={
        <h3 className="font-bold text-sm text-slate-900 flex items-center gap-2 min-w-0">
          <UserCheck className="w-5 h-5 text-blue-600 shrink-0" />
          <span className="truncate">Lista de la puerta: {eventName}</span>
        </h3>
      }
    >
      <div>
        {/* La búsqueda queda arriba mientras se recorre la lista */}
        <div className="sticky -top-4 -mt-4 pt-4 pb-3 space-y-2 bg-white z-10">
          <div className="relative">
            <Search className="w-4 h-4 absolute left-3 top-2.5 text-slate-400" />
            <input
              type="search"
              value={texto}
              onChange={(e) => setTexto(e.target.value)}
              placeholder="Nombre, apellido, referencia o correo exacto"
              aria-label="Buscar en la lista de la puerta"
              className="w-full bg-slate-50 border border-slate-200 rounded-xl pl-9 pr-3 py-2 text-xs text-slate-900 placeholder:text-slate-400 focus:outline-none focus:border-blue-500"
            />
          </div>
          <div className="flex gap-1.5">
            {(
              [
                ['todos', 'Todos'],
                ['pendientes', 'Pendientes'],
                ['ingresaron', 'Ingresaron'],
              ] as const
            ).map(([valor, etiqueta]) => (
              <button
                key={valor}
                type="button"
                onClick={() => setFiltro(valor)}
                aria-pressed={filtro === valor}
                className={`text-[10px] font-bold px-2.5 py-1 rounded-lg transition-all cursor-pointer ${
                  filtro === valor
                    ? 'bg-brand-deep text-white'
                    : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
                }`}
              >
                {etiqueta}
              </button>
            ))}
          </div>
          {checkIn.error != null && <ErrorNote error={checkIn.error} />}
        </div>

        <div className="divide-y divide-slate-100">
          {list.isLoading ? (
            <Spinner />
          ) : list.error != null ? (
            <ErrorNote error={list.error} />
          ) : items.length === 0 ? (
            <p className="text-center text-xs text-slate-400 py-8">
              {search || filtro !== 'todos'
                ? 'Nadie en la lista coincide con la búsqueda.'
                : 'Nadie se ha inscrito todavía en este evento.'}
            </p>
          ) : (
            items.map((fila) => {
              const nombre = fullName(fila)
              const enCurso = checkIn.isPending && checkIn.variables === fila.registrationId
              return (
                <div
                  key={fila.registrationId}
                  className="py-2 px-1 flex items-center justify-between gap-2"
                >
                  <div className="min-w-0">
                    <p className="text-xs font-bold text-slate-900 truncate">{nombre}</p>
                    <p className="text-[10px] text-slate-500 truncate">
                      {fila.company ?? 'Sin empresa'} • {fila.reference}
                    </p>
                    {fila.checkedInAt && (
                      <p className="text-[10px] text-emerald-700">
                        Ingresó {formatDateTime(fila.checkedInAt)}
                      </p>
                    )}
                  </div>
                  {fila.checkedInAt ? (
                    <button
                      type="button"
                      onClick={() => setDeshacer(fila)}
                      aria-label={`Deshacer el check-in de ${nombre}`}
                      className="shrink-0 flex items-center gap-1 px-2 py-1 rounded-lg text-[10px] font-bold text-slate-600 bg-slate-100 hover:bg-slate-200 cursor-pointer"
                    >
                      <Undo2 className="w-3 h-3" />
                      Deshacer
                    </button>
                  ) : (
                    <button
                      type="button"
                      disabled={checkIn.isPending}
                      onClick={() =>
                        checkIn.mutate(fila.registrationId, { onSuccess: onCheckedIn })
                      }
                      aria-label={`Hacer check-in de ${nombre}`}
                      className="shrink-0 flex items-center gap-1 px-2.5 py-1 rounded-lg text-[10px] font-black bg-blue-600 text-white hover:bg-blue-700 disabled:opacity-60 cursor-pointer"
                    >
                      {enCurso && <Loader2 className="w-3 h-3 animate-spin" />}
                      Check-in
                    </button>
                  )}
                </div>
              )
            })
          )}
          {list.data && list.data.total > items.length && (
            <p className="text-center text-[10px] text-slate-400 py-3">
              Se muestran {items.length} de {list.data.total}. Afine la búsqueda para ver al resto.
            </p>
          )}
        </div>
      </div>

      {deshacer && (
        <ConfirmDialog
          title="Deshacer el check-in"
          confirmLabel="Deshacer check-in"
          destructive
          pending={undo.isPending}
          error={undo.error}
          onCancel={() => {
            undo.reset()
            setDeshacer(null)
          }}
          onConfirm={() =>
            undo.mutate(deshacer.registrationId, { onSuccess: () => setDeshacer(null) })
          }
        >
          <p>
            <strong>{fullName(deshacer)}</strong> volverá a figurar como pendiente de entrada, y su
            entrada podrá leerse otra vez.
          </p>
        </ConfirmDialog>
      )}
    </Modal>
  )
}
