import { useState, type FormEvent } from 'react'
import { RotateCcw } from 'lucide-react'
import { Button } from '@/components/ui/Button'
import { Field, TextInput } from '@/components/ui/Field'
import { Modal } from '@/components/ui/Modal'
import { Switch } from '@/components/ui/Switch'
import { agregarAListaBlanca, enviarInvitacion, restaurarEnListaBlanca } from '../listaBlanca'

const CORREO_VALIDO = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

/** Agrega a alguien a los invitados de un evento privado, para que se registre sin campaña. */
export function AgregarInvitadoModal({
  eventoId,
  yaInvitados,
  onClose,
}: {
  eventoId: string
  /** Correos que ya están en la lista, para no duplicar. */
  yaInvitados: string[]
  onClose: () => void
}) {
  const [correo, setCorreo] = useState('')
  const [nombre, setNombre] = useState('')
  const [enviarAhora, setEnviarAhora] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const agregar = (e: FormEvent) => {
    e.preventDefault()
    const limpio = correo.trim()
    if (!CORREO_VALIDO.test(limpio)) return setError('Escribe un correo válido.')
    if (yaInvitados.some((x) => x.toLowerCase() === limpio.toLowerCase()))
      return setError('Esa persona ya está invitada.')
    agregarAListaBlanca(eventoId, { email: limpio, nombre: nombre.trim() || limpio })
    if (enviarAhora) enviarInvitacion(eventoId, limpio)
    onClose()
  }

  return (
    <Modal title="Agregar invitado" onClose={onClose} width="sm:max-w-md">
      <form onSubmit={agregar} className="space-y-4">
        <p className="text-sm text-slate-500 dark:text-slate-400">
          Podrá registrarse en este evento privado aunque no haya recibido ninguna campaña.
        </p>
        <Field label="Correo" required error={error ?? undefined}>
          <TextInput
            type="email"
            value={correo}
            onChange={(e) => setCorreo(e.target.value)}
            placeholder="persona@empresa.com"
            aria-invalid={Boolean(error)}
            autoFocus
          />
        </Field>
        <Field label="Nombre">
          <TextInput
            value={nombre}
            onChange={(e) => setNombre(e.target.value)}
            placeholder="Nombre y apellido"
          />
        </Field>
        <Switch
          checked={enviarAhora}
          onChange={setEnviarAhora}
          label="Enviarle la invitación ahora"
        />
        <div className="flex justify-end gap-2 border-t border-slate-100 pt-4 dark:border-slate-800">
          <Button type="button" variant="outline" onClick={onClose}>
            Cancelar
          </Button>
          <Button type="submit">Agregar invitado</Button>
        </div>
      </form>
    </Modal>
  )
}

/** Los que se sacaron de la lista de un evento privado, con la opción de devolverlos. */
export function QuitadosDeLaLista({
  eventoId,
  quitados,
  nombrePorCorreo,
}: {
  eventoId: string
  quitados: string[]
  nombrePorCorreo: Map<string, string>
}) {
  if (quitados.length === 0) return null
  return (
    <details className="mt-4 rounded-xl border border-slate-200 p-3 text-xs dark:border-slate-800">
      <summary className="cursor-pointer font-bold text-slate-600 dark:text-slate-300">
        Quitados de los invitados ({quitados.length})
      </summary>
      <ul className="mt-2 space-y-1">
        {quitados.map((email) => (
          <li key={email} className="flex items-center justify-between gap-2">
            <span className="truncate text-slate-500">
              {nombrePorCorreo.get(email.toLowerCase()) ?? email}{' '}
              <span className="text-slate-400">({email})</span>
            </span>
            <button
              type="button"
              onClick={() => restaurarEnListaBlanca(eventoId, email)}
              className="flex shrink-0 items-center gap-1 font-bold text-blue-600 hover:underline dark:text-blue-400"
            >
              <RotateCcw size={12} /> Devolver
            </button>
          </li>
        ))}
      </ul>
    </details>
  )
}
