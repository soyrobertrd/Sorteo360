import { useState } from 'react'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { Field, Select, TextInput } from '@/components/ui/Field'
import { ErrorNote, WarningNote } from '@/components/ui/Feedback'
import type { MontajeEvento } from '@/api/types'
import { type Invitado } from '../../mockParticipants'
import {
  sillasDe,
  grupoContiguo,
  moverGrupo,
  categoriaDeInvitado,
  unidadesDe,
  asignarAsiento,
  claveDe,
  CONTACTOS_SIN_CRM,
  companerosSinMesa,
  distribucionDe,
  esReservada,
  etiquetasDe,
  nombreDeMesa,
  sentarEnMesa,
  sillasLibres,
  textoAsiento,
  useMesasDeEvento,
  type Asiento,
  type ConfigMesas,
  type Etiquetas,
} from '../../mesas'
import { minus } from './texto'

/** Liberar todo el salón o una mesa o fila: quienes estaban ahí quedan sin asiento. */
export function LiberarAsientosModal({
  config,
  et,
  asientos,
  onClose,
  onLiberar,
}: {
  config: ConfigMesas
  et: Etiquetas
  asientos: Record<string, Asiento>
  onClose: () => void
  onLiberar: (mesa: number | null) => void
}) {
  const [eleccion, setEleccion] = useState('todas')
  const ocupadasEn = (mesa: number) => Object.values(asientos).filter((a) => a.mesa === mesa).length
  const conGente = unidadesDe(config).filter((m) => ocupadasEn(m) > 0)
  const total = Object.keys(asientos).length
  const cuantos = eleccion === 'todas' ? total : ocupadasEn(Number(eleccion))
  return (
    <Modal title="Liberar asientos" onClose={onClose} width="max-w-md">
      <div className="space-y-4 text-xs">
        <Field label="¿Qué quieres liberar?">
          <Select value={eleccion} onChange={(e) => setEleccion(e.target.value)}>
            <option value="todas">Todo el salón ({total} ocupados)</option>
            {conGente.map((m) => (
              <option key={m} value={m}>
                {nombreDeMesa(config, m, et)} ({ocupadasEn(m)} ocupados)
              </option>
            ))}
          </Select>
        </Field>
        <WarningNote>
          {cuantos === 1 ? 'Una persona queda' : `${cuantos} personas quedan`} sin asiento. En modo
          automático lo reciben de nuevo al hacer check-in.
        </WarningNote>
        <div className="flex justify-end gap-2">
          <Button size="sm" variant="outline" onClick={onClose}>
            Cancelar
          </Button>
          <Button
            size="sm"
            variant="destructive"
            onClick={() => onLiberar(eleccion === 'todas' ? null : Number(eleccion))}
          >
            Liberar {cuantos}
          </Button>
        </div>
      </div>
    </Modal>
  )
}

/** Elegir a quién sentar en un asiento libre: primero los que no tienen, después el resto. */
export function ElegirPersonaModal({
  titulo,
  registrados,
  asientoDe,
  config,
  et,
  onClose,
  onElegir,
  nombreManual,
}: {
  titulo: string
  registrados: Invitado[]
  asientoDe: (i: Invitado) => Asiento | undefined
  config: ConfigMesas
  et: Etiquetas
  onClose: () => void
  onElegir: (persona: Invitado) => void
  /** Mesa principal: permite escribir el nombre de un invitado sin QR; null lo quita. */
  nombreManual?: { valor: string; onGuardar: (nombre: string | null) => void }
}) {
  const [elegido, setElegido] = useState('')
  const [nombre, setNombre] = useState(nombreManual?.valor ?? '')
  const sinMesa = registrados.filter((r) => !asientoDe(r))
  const conMesa = registrados.filter((r) => asientoDe(r))
  return (
    <Modal title={titulo} onClose={onClose} width="max-w-md">
      <div className="space-y-4 text-xs">
        <Field label="Registrado">
          <Select value={elegido} onChange={(e) => setElegido(e.target.value)}>
            <option value="">Elige a la persona</option>
            {sinMesa.length > 0 && (
              <optgroup label="Sin asiento">
                {sinMesa.map((c) => (
                  <option key={c.email} value={c.email}>
                    {c.nombre}
                    {c.empresa ? ` · ${c.empresa}` : ''}
                  </option>
                ))}
              </optgroup>
            )}
            {conMesa.length > 0 && (
              <optgroup label="Mover desde otro asiento">
                {conMesa.map((c) => (
                  <option key={c.email} value={c.email}>
                    {c.nombre} ({textoAsiento(asientoDe(c)!, config, et)})
                  </option>
                ))}
              </optgroup>
            )}
          </Select>
        </Field>
        {nombreManual && (
          <Field
            label="O escribe el nombre del invitado"
            hint="Para quien no se registró ni tiene código QR, como autoridades u oradores."
          >
            <TextInput
              value={nombre}
              disabled={Boolean(elegido)}
              placeholder="Ej. Ministro de Turismo"
              onChange={(e) => setNombre(e.target.value)}
            />
          </Field>
        )}
        <div className="flex justify-end gap-2">
          {nombreManual?.valor && (
            <Button
              size="sm"
              variant="outline"
              className="mr-auto"
              onClick={() => nombreManual.onGuardar(null)}
            >
              Quitar invitado
            </Button>
          )}
          <Button size="sm" variant="outline" onClick={onClose}>
            Cancelar
          </Button>
          <Button
            size="sm"
            disabled={!elegido && !(nombreManual && nombre.trim())}
            onClick={() => {
              const persona = registrados.find((c) => c.email === elegido)
              if (persona) onElegir(persona)
              else if (nombreManual && nombre.trim()) nombreManual.onGuardar(nombre.trim())
            }}
          >
            Asignar
          </Button>
        </div>
      </div>
    </Modal>
  )
}

/**
 * Elegir asiento para una persona (desde el esquema, la lista de invitados o el check-in).
 * Con un asiento ocupado y la persona ya sentada, las dos intercambian lugar.
 */
export function AsignarMesaModal({
  eventoId,
  montaje,
  invitado,
  registrados,
  onClose,
}: {
  eventoId: string
  montaje: MontajeEvento | null | undefined
  invitado: Invitado
  /** Los invitados del evento, para sugerir sentar juntos a los de la misma empresa. */
  registrados: Invitado[]
  onClose: () => void
}) {
  const et = etiquetasDe(montaje)
  const { config, asientos } = useMesasDeEvento(eventoId)
  const [sugerirEn, setSugerirEn] = useState<number | null>(null)
  const actual = asientos[claveDe(invitado.email)]
  const [mesa, setMesa] = useState(actual?.mesa ?? unidadesDe(config)[0] ?? 1)
  const [silla, setSilla] = useState<number | ''>(actual?.silla ?? '')
  const [error, setError] = useState<unknown>(null)
  const ocupadaPor = (s: number) =>
    Object.entries(asientos).find(
      ([k, a]) => k !== claveDe(invitado.email) && a.mesa === mesa && a.silla === s,
    )
  const sillas = Array.from({ length: sillasDe(config, mesa) }, (_, j) => j + 1).filter(
    (s) => actual || !ocupadaPor(s),
  )
  const circular = distribucionDe(montaje) === 'mesas'
  const contactos = CONTACTOS_SIN_CRM
  const [grupo, setGrupo] = useState<{
    asiento: Pick<Asiento, 'mesa' | 'silla'>
    claves: string[]
    motivo: string
  } | null>(null)

  // Quién está sentado seguido a esta persona y comparte su empresa o, si no, su categoría.
  const buscarGrupo = (): { claves: string[]; motivo: string } | null => {
    if (!actual) return null
    const porClave = new Map(registrados.map((r) => [claveDe(r.email), r]))
    const porCorreo = new Map(contactos.map((c) => [claveDe(c.email), c]))
    const total = sillasDe(config, actual.mesa)
    const empresa = invitado.empresa.trim().toLowerCase()
    if (empresa) {
      const claves = grupoContiguo(
        asientos,
        invitado.email,
        (k) => porClave.get(k)?.empresa.trim().toLowerCase() === empresa,
        total,
        circular,
      )
      if (claves.length) return { claves, motivo: `de ${invitado.empresa}` }
    }
    const categoria = categoriaDeInvitado(invitado, porCorreo)
    if (categoria) {
      const claves = grupoContiguo(
        asientos,
        invitado.email,
        (k) => {
          const otro = porClave.get(k)
          return Boolean(otro) && categoriaDeInvitado(otro!, porCorreo) === categoria
        },
        total,
        circular,
      )
      if (claves.length) return { claves, motivo: `de la categoría ${categoria}` }
    }
    return null
  }

  const nombreDe = (clave: string) =>
    registrados.find((r) => claveDe(r.email) === clave)?.nombre ?? clave

  const guardar = (asiento: Pick<Asiento, 'mesa' | 'silla'> | null, soloElla = false) => {
    if (asiento && actual && !soloElla && !grupo) {
      const encontrado = buscarGrupo()
      const cambia = asiento.mesa !== actual.mesa || asiento.silla !== actual.silla
      if (encontrado && cambia) {
        setGrupo({ asiento, ...encontrado })
        return
      }
    }
    try {
      asignarAsiento(eventoId, invitado, asiento)
      if (asiento && companerosSinMesa(invitado, registrados, asientos).length > 0) {
        setSugerirEn(asiento.mesa)
      } else {
        onClose()
      }
    } catch (e) {
      setError(e)
    }
  }

  if (sugerirEn !== null) {
    return (
      <SugerirCompanerosModal
        eventoId={eventoId}
        montaje={montaje}
        persona={invitado}
        mesa={sugerirEn}
        registrados={registrados}
        onClose={onClose}
      />
    )
  }

  if (grupo) {
    const todos = [claveDe(invitado.email), ...grupo.claves]
    return (
      <Modal title="Mover a un grupo sentado junto" onClose={onClose} width="max-w-md">
        <div className="space-y-4 text-xs">
          {error != null && <ErrorNote error={error} />}
          <WarningNote>
            {invitado.nombre} está sentado junto a {grupo.claves.length}{' '}
            {grupo.claves.length === 1 ? 'persona' : 'personas'} {grupo.motivo}:{' '}
            <strong>{grupo.claves.map(nombreDe).join(', ')}</strong>.
          </WarningNote>
          <p className="text-slate-600 dark:text-slate-300">
            ¿Quieres mover al grupo completo a{' '}
            <strong>{textoAsiento({ ...grupo.asiento }, config, et)}</strong> y los asientos
            siguientes, o solo a {invitado.nombre}?
          </p>
          <div className="flex flex-wrap justify-end gap-2">
            <Button size="sm" variant="outline" onClick={onClose}>
              Cancelar
            </Button>
            <Button size="sm" variant="outline" onClick={() => guardar(grupo.asiento, true)}>
              Mover solo a {invitado.nombre.split(' ')[0]}
            </Button>
            <Button
              size="sm"
              onClick={() => {
                try {
                  moverGrupo(eventoId, todos, grupo.asiento, circular)
                  onClose()
                } catch (e) {
                  setError(e)
                }
              }}
            >
              Mover al grupo ({todos.length})
            </Button>
          </div>
        </div>
      </Modal>
    )
  }

  return (
    <Modal
      title={`${et.unidad} y ${minus(et.asiento)} · ${invitado.nombre}`}
      onClose={onClose}
      width="max-w-md"
    >
      <div className="space-y-4 text-xs">
        {error != null && <ErrorNote error={error} />}
        {actual && (
          <p className="text-slate-500">
            Ahora en <strong>{textoAsiento(actual, config, et)}</strong>. Elegir un asiento ocupado
            intercambia el lugar con quien lo tiene.
          </p>
        )}
        <div className="grid grid-cols-2 gap-3">
          <Field label={et.unidad}>
            <Select
              value={mesa}
              onChange={(e) => {
                setMesa(Number(e.target.value))
                setSilla('')
              }}
            >
              {unidadesDe(config).map((m) => (
                <option key={m} value={m}>
                  {nombreDeMesa(config, m, et)}
                  {esReservada(config, m) ? ' · VIP' : ''}
                  {config.categorias[m] ? ` · ${config.categorias[m]}` : ''}
                </option>
              ))}
            </Select>
          </Field>
          <Field label={et.asiento}>
            <Select
              value={silla}
              onChange={(e) => setSilla(e.target.value ? Number(e.target.value) : '')}
            >
              <option value="">
                {sillas.length ? `Elige ${minus(et.asiento)}` : `${et.unidad} llena`}
              </option>
              {sillas.map((s) => (
                <option key={s} value={s}>
                  {et.asiento} {s}
                  {ocupadaPor(s) ? ' (intercambiar)' : ''}
                </option>
              ))}
            </Select>
          </Field>
        </div>
        <div className="flex justify-between gap-2">
          {actual ? (
            <Button size="sm" variant="outline" onClick={() => guardar(null)}>
              Quitar {minus(et.asiento)}
            </Button>
          ) : (
            <span />
          )}
          <div className="flex gap-2">
            <Button size="sm" variant="outline" onClick={onClose}>
              Cancelar
            </Button>
            <Button
              size="sm"
              disabled={silla === ''}
              onClick={() => guardar({ mesa, silla: Number(silla) })}
            >
              Guardar
            </Button>
          </div>
        </div>
      </div>
    </Modal>
  )
}

/**
 * Después de sentar a alguien, sugiere sentar en la misma mesa o fila a los de su empresa que
 * todavía no tienen asiento, hasta donde alcancen los libres.
 */
export function SugerirCompanerosModal({
  eventoId,
  montaje,
  persona,
  mesa,
  registrados,
  onClose,
}: {
  eventoId: string
  montaje: MontajeEvento | null | undefined
  persona: Invitado
  mesa: number
  registrados: Invitado[]
  onClose: () => void
}) {
  const et = etiquetasDe(montaje)
  const { config, asientos } = useMesasDeEvento(eventoId)
  // Se fija al abrir: sentar a alguien no debe reordenar la lista mientras se elige.
  const [companeros] = useState(() => companerosSinMesa(persona, registrados, asientos))
  const libres = sillasLibres(eventoId, mesa).length
  const [elegidos, setElegidos] = useState<Set<string>>(
    () => new Set(companeros.slice(0, libres).map((c) => c.email)),
  )
  const nombreMesa = nombreDeMesa(config, mesa, et)

  const alternar = (email: string) =>
    setElegidos((prev) => {
      const next = new Set(prev)
      if (next.has(email)) next.delete(email)
      else if (next.size < libres) next.add(email)
      return next
    })

  return (
    <Modal title="Sentar juntos a los de su empresa" onClose={onClose} width="max-w-md">
      <div className="space-y-4 text-xs">
        <p className="text-slate-600 dark:text-slate-300">
          {persona.nombre} quedó en <strong>{nombreMesa}</strong>.{' '}
          {companeros.length === 1 ? 'Otra persona' : `Otras ${companeros.length} personas`} de{' '}
          <strong>{persona.empresa}</strong> no {companeros.length === 1 ? 'tiene' : 'tienen'}{' '}
          asiento.{' '}
          {libres > 0
            ? `Quedan ${libres} ${libres === 1 ? 'asiento libre' : 'asientos libres'} ahí.`
            : `La ${minus(et.unidad)} ya está llena.`}
        </p>
        {libres > 0 && (
          <ul className="space-y-1.5">
            {companeros.map((c) => {
              const marcado = elegidos.has(c.email)
              return (
                <li key={c.email}>
                  <label className="flex cursor-pointer items-center gap-2">
                    <input
                      type="checkbox"
                      checked={marcado}
                      disabled={!marcado && elegidos.size >= libres}
                      onChange={() => alternar(c.email)}
                    />
                    <span className="font-semibold text-slate-800 dark:text-slate-200">
                      {c.nombre}
                    </span>
                    <span className="truncate text-slate-400">{c.email}</span>
                  </label>
                </li>
              )
            })}
          </ul>
        )}
        <div className="flex justify-end gap-2">
          <Button size="sm" variant="outline" onClick={onClose}>
            Ahora no
          </Button>
          {libres > 0 && (
            <Button
              size="sm"
              disabled={elegidos.size === 0}
              onClick={() => {
                sentarEnMesa(
                  eventoId,
                  mesa,
                  companeros.filter((c) => elegidos.has(c.email)),
                  distribucionDe(montaje) === 'mesas',
                )
                onClose()
              }}
            >
              Sentar {elegidos.size} en {nombreMesa}
            </Button>
          )}
        </div>
      </div>
    </Modal>
  )
}
