import { useState } from 'react'
import { Layers } from 'lucide-react'
import { Modal } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { ErrorNote } from '@/components/ui/Feedback'
import { Field, TextInput } from '@/components/ui/Field'
import { fieldError } from '@/lib/fieldErrors'

interface ConfigurarRondaModalProps {
  numeroRonda: number
  initialNombre?: string
  initialDescripcion?: string
  saving?: boolean
  error?: unknown
  onClose: () => void
  onSave: (nombre: string, descripcion?: string) => void
}

export function ConfigurarRondaModal({
  numeroRonda,
  initialNombre,
  initialDescripcion,
  saving = false,
  error,
  onClose,
  onSave,
}: ConfigurarRondaModalProps) {
  const [nombre, setNombre] = useState(initialNombre || `Ronda ${numeroRonda}`)
  const [descripcion, setDescripcion] = useState(initialDescripcion || '')

  const handleSave = () => {
    if (!nombre.trim()) return
    onSave(nombre.trim(), descripcion.trim() || undefined)
  }

  return (
    <Modal title={`Configurar Ronda ${numeroRonda}`} onClose={onClose}>
      <div className="space-y-4">
        <Field label="Nombre de la ronda *" error={fieldError(error, 'Name')}>
          <TextInput
            value={nombre}
            onChange={(e) => setNombre(e.target.value)}
            placeholder={`Ej: Ronda ${numeroRonda} — Premios de Bienvenida`}
          />
        </Field>

        <Field label="Descripción de la ronda (opcional)" error={fieldError(error, 'Description')}>
          <TextInput
            value={descripcion}
            onChange={(e) => setDescripcion(e.target.value)}
            placeholder="Ej: Sorteo de kits institucionales y obsequios a los primeros registrados."
          />
        </Field>

        {error != null && <ErrorNote error={error} />}

        <div className="flex justify-end gap-2 pt-4 border-t border-slate-100 dark:border-slate-800">
          <Button type="button" variant="outline" onClick={onClose}>
            Cancelar
          </Button>
          <Button
            type="button"
            onClick={handleSave}
            disabled={saving}
            className="bg-blue-600 hover:bg-blue-700 text-white font-bold cursor-pointer"
          >
            <Layers className="w-4 h-4 mr-1" />
            Guardar Ronda
          </Button>
        </div>
      </div>
    </Modal>
  )
}
