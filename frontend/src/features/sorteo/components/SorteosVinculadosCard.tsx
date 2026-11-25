import { Link } from 'react-router-dom'
import { Link2 } from 'lucide-react'
import type { RaffleListItem, RaffleResponse } from '@/api/types'
import { Card } from '@/components/ui/Card'
import { Field } from '@/components/ui/Field'
import { formatDate } from '@/lib/format'
import { raffleStateLabel } from '@/lib/labels'
import { useAllRaffles } from '../hooks'
import type { RaffleChanges } from '../raffleRequest'

const SELECT =
  'w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 disabled:opacity-60 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100'

/** El grupo de un sorteo: el evento al que se vinculó o, sin vínculo, el suyo. */
const grupoDe = (s: { eventId: string; linkedEventId?: string | null }) =>
  s.linkedEventId ?? s.eventId

/**
 * Sorteos vinculados: varios sorteos de un mismo evento que no repiten ganadores. Quien tiene un
 * premio vivo en uno queda fuera del bombo de los demás. Se vincula eligiendo otro sorteo; el
 * servicio lo lleva al evento de su grupo.
 */
export function SorteosVinculadosCard({
  sorteo,
  canEdit,
  title,
  onSave,
}: {
  sorteo: RaffleResponse
  canEdit: boolean
  title?: string
  onSave: (changes: RaffleChanges) => void
}) {
  const { data: sorteos } = useAllRaffles()
  const todos = sorteos ?? []
  const grupo = grupoDe(sorteo)
  const vinculados = todos.filter((s) => s.eventId !== sorteo.eventId && grupoDe(s) === grupo)
  // Un sorteo del que otros dependen no se vincula a otro: el servicio lo rechaza.
  const esAncla = sorteo.linkedEventId == null && vinculados.length > 0
  const opciones = todos.filter((s) => s.eventId !== sorteo.eventId)
  const valor = sorteo.linkedEventId ?? ''

  return (
    <Card className="space-y-4 border border-slate-200 bg-white p-6 dark:border-slate-800 dark:bg-card">
      <div className="flex items-center gap-2 border-b border-slate-100 pb-3 dark:border-slate-800">
        <Link2 className="h-5 w-5 text-brand" />
        <h3 className="text-base font-bold text-slate-900 dark:text-white">Sorteos vinculados</h3>
      </div>
      <p className="text-xs text-slate-600 dark:text-slate-300">
        Los sorteos vinculados a un mismo evento no repiten ganadores: quien ya ganó en uno no entra
        al bombo de los demás. El acta lo deja escrito.
      </p>

      <Field label="Vincular con">
        <select
          aria-label="Vincular con"
          className={SELECT}
          value={valor}
          disabled={!canEdit || esAncla}
          title={esAncla ? 'Otros sorteos están vinculados a este.' : title}
          onChange={(e) => onSave({ linkedEventId: e.target.value || null })}
        >
          <option value="">Sin vincular</option>
          {/* El vínculo actual puede ser un evento sin sorteo propio: igual se muestra. */}
          {valor && !opciones.some((s) => s.eventId === valor) && (
            <option value={valor}>Evento vinculado</option>
          )}
          {opciones.map((s) => (
            <option key={s.eventId} value={s.eventId}>
              {s.eventName} · {formatDate(s.eventStartDate)}
            </option>
          ))}
        </select>
      </Field>

      {vinculados.length > 0 ? (
        <ul className="divide-y divide-slate-100 rounded-lg border border-slate-200 text-xs dark:divide-slate-800 dark:border-slate-800">
          {vinculados.map((s: RaffleListItem) => (
            <li key={s.eventId} className="flex items-center justify-between gap-3 px-3 py-2">
              <span>
                <span className="font-semibold text-slate-900 dark:text-slate-100">
                  {s.eventName}
                </span>{' '}
                <span className="text-slate-500">· {raffleStateLabel[s.state]}</span>
              </span>
              <Link
                to={`/sorteo/sorteos/${s.eventId}?tab=ganadores`}
                className="font-semibold text-brand hover:underline"
              >
                {s.drawnCount} ganador(es)
              </Link>
            </li>
          ))}
        </ul>
      ) : (
        <p className="text-xs text-slate-500">Este sorteo no está vinculado con otro.</p>
      )}
    </Card>
  )
}
