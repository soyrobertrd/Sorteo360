import { History } from 'lucide-react'
import { Card } from '@/components/ui/Card'
import type { EventoDetail } from '@/api/types'

export interface EntradaBitacora {
  /** ISO, o `YYYY-MM-DD` cuando solo se conoce el día. */
  fecha: string
  accion: string
  detalle?: string
  usuario: string
}

/**
 * Bitácora del evento: historial de creación y modificaciones registradas en el evento.
 */
export function BitacoraEvento({ evento }: { evento: EventoDetail }) {
  const entradas: EntradaBitacora[] = [
    { fecha: evento.createdAt, accion: 'Evento creado', usuario: 'Administración' },
    ...(evento.updatedAt && evento.updatedAt !== evento.createdAt
      ? [
          {
            fecha: evento.updatedAt,
            accion: 'Ficha del evento modificada',
            usuario: 'Administración',
          },
        ]
      : []),
  ].sort((a, b) => b.fecha.localeCompare(a.fecha))

  const fechaHora = (iso: string) =>
    iso.length <= 10
      ? new Date(`${iso}T12:00:00`).toLocaleDateString('es-DO', { dateStyle: 'medium' })
      : new Date(iso).toLocaleString('es-DO', { dateStyle: 'medium', timeStyle: 'short' })

  return (
    <Card className="rounded-2xl border border-slate-200/80 bg-white p-5 shadow-2xs dark:border-slate-800 dark:bg-slate-900">
      <h3 className="mb-4 flex items-center gap-2 text-sm font-bold text-slate-900 dark:text-white">
        <History size={15} /> Historial del evento
      </h3>
      <ol className="relative ml-2 space-y-4 border-l-2 border-slate-200 pl-4 text-xs dark:border-slate-700">
        {entradas.map((e, i) => (
          <li key={i} className="relative">
            <span className="absolute -left-5.25 top-1 h-2.5 w-2.5 rounded-full border-2 border-white bg-blue-600 dark:border-slate-900" />
            <p className="font-bold text-slate-900 dark:text-slate-100">{e.accion}</p>
            {e.detalle && (
              <p className="text-[11px] text-slate-600 dark:text-slate-300">{e.detalle}</p>
            )}
            <p className="text-[11px] text-slate-500">
              {fechaHora(e.fecha)} · {e.usuario}
            </p>
          </li>
        ))}
      </ol>
    </Card>
  )
}
