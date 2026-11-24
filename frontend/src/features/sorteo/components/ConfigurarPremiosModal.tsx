import { useState } from 'react'
import { Plus, Layers } from 'lucide-react'
import { Modal } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { ErrorNote } from '@/components/ui/Feedback'
import { Field, TextInput } from '@/components/ui/Field'
import type { CreatePrizesRequest, RaffleRoundResponse } from '@/api/types'
import { fieldError } from '@/lib/fieldErrors'
import type { FotoElegida } from '../hooks'
import { SelectorFoto } from './SelectorFoto'

/** El tope del validador del servicio. */
const MAX_CANTIDAD = 50

interface ConfigurarPremiosModalProps {
  rondas: RaffleRoundResponse[]
  defaultRondaId?: string
  saving?: boolean
  error?: unknown
  onClose: () => void
  /** La foto viaja aparte: se sube después de crear los premios, a todas sus unidades. */
  onAdd: (premios: CreatePrizesRequest, foto?: Exclude<FotoElegida, null>) => void
}

/**
 * Uno o más premios idénticos para una ronda. La cantidad viaja tal cual: el servicio crea las
 * filas, todas con el mismo nombre, y las devuelve.
 */
export function ConfigurarPremiosModal({
  rondas,
  defaultRondaId,
  saving = false,
  error,
  onClose,
  onAdd,
}: ConfigurarPremiosModalProps) {
  const [nombre, setNombre] = useState('')
  const [descripcion, setDescripcion] = useState('')
  const [valor, setValor] = useState('')
  const [patrocinador, setPatrocinador] = useState('')
  const [cantidad, setCantidad] = useState<number>(1)
  const [rondaId, setRondaId] = useState(defaultRondaId || rondas[0]?.id || '')
  const [foto, setFoto] = useState<Exclude<FotoElegida, null> | undefined>(undefined)

  const handleAdd = () => {
    if (!nombre.trim() || !rondaId) return
    onAdd(
      {
        roundId: rondaId,
        name: nombre.trim(),
        description: descripcion.trim() || null,
        value: valor.trim() || null,
        quantity: Math.max(1, cantidad || 1),
        sponsorName: patrocinador.trim() || null,
      },
      foto,
    )
  }

  return (
    <Modal title="Agregar Premio a la Rifa" onClose={onClose}>
      <div className="space-y-4">
        {rondas.length > 1 && (
          <Field label="Ronda asignada *">
            <select
              value={rondaId}
              onChange={(e) => setRondaId(e.target.value)}
              className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
            >
              {rondas.map((r) => (
                <option key={r.id} value={r.id}>
                  {r.name} (Ronda {r.number})
                </option>
              ))}
            </select>
          </Field>
        )}

        <Field label="Nombre del premio *" error={fieldError(error, 'Name')}>
          <TextInput
            value={nombre}
            onChange={(e) => setNombre(e.target.value)}
            placeholder="Ej: Canasta Navideña o Viaje a Samaná"
          />
        </Field>

        {/* Pool / Cantidad de unidades */}
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <Field
            label="Cantidad / Unidades a sortear *"
            error={fieldError(error, 'Quantity')}
            hint={
              cantidad > 1
                ? `Se crearán ${cantidad} premios iguales en la ronda seleccionada.`
                : '1 unidad individual.'
            }
          >
            <div className="relative">
              <input
                type="number"
                min={1}
                max={MAX_CANTIDAD}
                value={cantidad}
                onChange={(e) => {
                  const val = parseInt(e.target.value, 10)
                  setCantidad(isNaN(val) ? 1 : Math.max(1, Math.min(MAX_CANTIDAD, val)))
                }}
                className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 font-semibold focus:border-blue-600 focus:outline-none focus:ring-1 focus:ring-blue-600 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
              />
              {cantidad > 1 && (
                <div className="absolute right-3 top-2.5 flex items-center gap-1 text-xs font-bold text-blue-600 dark:text-blue-400 bg-blue-50 dark:bg-blue-900/30 px-2 py-0.5 rounded pointer-events-none">
                  <Layers className="w-3.5 h-3.5" />
                  Pool x{cantidad}
                </div>
              )}
            </div>
          </Field>

          <Field label="Valor estimado (por unidad)" error={fieldError(error, 'Value')}>
            <TextInput
              value={valor}
              onChange={(e) => setValor(e.target.value)}
              placeholder="Ej: RD$10,000 o US$200"
            />
          </Field>
        </div>

        <Field label="Descripción" error={fieldError(error, 'Description')}>
          <TextInput
            value={descripcion}
            onChange={(e) => setDescripcion(e.target.value)}
            placeholder="Ej: Canasta con productos seleccionados para la temporada"
          />
        </Field>

        <Field
          label="Patrocinador (opcional)"
          error={fieldError(error, 'SponsorName')}
          hint="Queda guardado con el premio y sale en el acta."
        >
          <TextInput
            value={patrocinador}
            onChange={(e) => setPatrocinador(e.target.value)}
            maxLength={200}
            placeholder="Ej: Brugal & Co."
          />
        </Field>

        <Field
          label="Foto (opcional)"
          hint="Con foto, en el tablero se sortea como premio principal: uno a uno."
        >
          <SelectorFoto onChange={setFoto} />
        </Field>

        {error != null && <ErrorNote error={error} />}

        <div className="flex justify-end gap-2 pt-4 border-t border-slate-100 dark:border-slate-800">
          <Button type="button" variant="outline" onClick={onClose}>
            Cancelar
          </Button>
          <Button
            type="button"
            onClick={handleAdd}
            disabled={saving || !nombre.trim()}
            className="bg-blue-600 hover:bg-blue-700 text-white font-bold cursor-pointer"
          >
            <Plus className="w-4 h-4 mr-1" />
            {cantidad > 1 ? `Guardar Pool (${cantidad} Premios)` : 'Guardar Premio'}
          </Button>
        </div>
      </div>
    </Modal>
  )
}
