import { Eye } from 'lucide-react'
import { toast } from 'sonner'
import type { RaffleDisplayFields, RaffleResponse } from '@/api/types'
import { Card } from '@/components/ui/Card'
import { ErrorNote } from '@/components/ui/Feedback'
import { Field } from '@/components/ui/Field'
import {
  OPCIONES_DETALLE,
  OPCIONES_IDENTIFICADOR,
  OPCIONES_NOMBRE,
  camposDe,
  ganadorVisible,
} from '../camposVisibles'
import { useSetRaffleDisplayFields } from '../hooks'
import { useEtiquetasPadron, type EtiquetasPadron } from '../etiquetasPadron'

/** Las opciones que nombran un dato del padrón llevan el nombre que se le dio en la carga. */
const nombrar = (etq: EtiquetasPadron, valor: string, etiqueta: string) =>
  valor === 'employee_code'
    ? etq.employeeCode
    : valor === 'department' || valor === 'location' || valor === 'company'
      ? etq[valor]
      : etiqueta

const SELECT =
  'w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 disabled:opacity-60 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100'

/** Un ganador de ejemplo para la vista previa: muestra qué sale con cada elección. */
const EJEMPLO = {
  winnerName: 'María Altagracia Rodríguez Peña',
  winnerFirstName: 'María Altagracia',
  winnerLastName: 'Rodríguez Peña',
  winnerCompany: 'Banca Personal',
  winnerTicketNumber: '482-917',
  winnerEmployeeCode: '0029370',
  winnerDepartment: 'Banca Personal',
  winnerLocation: 'Oficina Principal',
}

/**
 * Qué datos del ganador se ven en la consola, el proyector, el tablero y el historial (§10). El acta
 * y el certificado no cambian: siempre llevan nombre completo, boleto e ID de empleado. Se elige en
 * borrador; al iniciar el sorteo queda fija.
 */
export function CamposVisiblesCard({
  eventId,
  sorteo,
  canEdit,
  title,
}: {
  eventId: string
  sorteo: RaffleResponse
  canEdit: boolean
  title?: string
}) {
  const guardar = useSetRaffleDisplayFields(eventId)
  const etiquetas = useEtiquetasPadron(eventId)
  const campos = camposDe(sorteo)
  const editable = canEdit && sorteo.state === 'draft'
  const ejemplo = ganadorVisible(EJEMPLO, campos)

  const cambiar = (cambio: Partial<RaffleDisplayFields>) =>
    guardar.mutate({ ...campos, ...cambio }, { onSuccess: () => toast.success('Guardado.') })

  return (
    <Card className="space-y-4 border border-slate-200 bg-white p-6 dark:border-slate-800 dark:bg-card">
      <div className="flex items-center gap-2 border-b border-slate-100 pb-3 dark:border-slate-800">
        <Eye className="h-5 w-5 text-brand dark:text-brand-accent" />
        <h3 className="text-base font-bold text-slate-900 dark:text-white">
          Datos del ganador en pantalla
        </h3>
      </div>
      <p className="text-xs text-slate-500 dark:text-slate-400">
        Lo que se ve en la consola, el proyector, el tablero y el historial. El acta y el
        certificado siempre llevan el nombre completo, el boleto y el ID de empleado.
        {sorteo.state !== 'draft' && ' El sorteo ya inició: quedó fijo.'}
      </p>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3" title={editable ? undefined : title}>
        <Field label="Nombre">
          <select
            className={SELECT}
            value={campos.name}
            disabled={!editable || guardar.isPending}
            onChange={(e) => cambiar({ name: e.target.value as RaffleDisplayFields['name'] })}
          >
            {OPCIONES_NOMBRE.map((o) => (
              <option key={o.valor} value={o.valor}>
                {o.etiqueta}
              </option>
            ))}
          </select>
        </Field>
        <Field label="Identificador">
          <select
            className={SELECT}
            value={campos.identifier}
            disabled={!editable || guardar.isPending}
            onChange={(e) =>
              cambiar({ identifier: e.target.value as RaffleDisplayFields['identifier'] })
            }
          >
            {OPCIONES_IDENTIFICADOR.map((o) => (
              <option key={o.valor} value={o.valor}>
                {nombrar(etiquetas, o.valor, o.etiqueta)}
              </option>
            ))}
          </select>
        </Field>
        <Field label="Detalle">
          <select
            className={SELECT}
            value={campos.detail}
            disabled={!editable || guardar.isPending}
            onChange={(e) => cambiar({ detail: e.target.value as RaffleDisplayFields['detail'] })}
          >
            {OPCIONES_DETALLE.map((o) => (
              <option key={o.valor} value={o.valor}>
                {nombrar(etiquetas, o.valor, o.etiqueta)}
              </option>
            ))}
          </select>
        </Field>
      </div>

      <div className="rounded-lg bg-muted px-4 py-3" aria-label="Vista previa">
        <p className="text-[11px] font-semibold uppercase tracking-wider text-slate-500">
          Así se ve
        </p>
        <p className="mt-1 text-lg font-black text-brand-deep dark:text-white">{ejemplo.nombre}</p>
        {(ejemplo.detalle || ejemplo.identificador) && (
          <p className="text-sm text-slate-600 dark:text-slate-300">
            {[ejemplo.detalle, ejemplo.identificador].filter(Boolean).join(' · ')}
          </p>
        )}
      </div>

      {guardar.error != null && <ErrorNote error={guardar.error} />}
    </Card>
  )
}
