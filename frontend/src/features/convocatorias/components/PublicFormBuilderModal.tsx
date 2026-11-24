import { useState } from 'react'
import { ArrowDown, ArrowUp, Database, Eye, Plus, Trash2 } from 'lucide-react'
import { Modal } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { Callout } from '@/components/ui/Callout'
import { Field, Select, TextArea, TextInput } from '@/components/ui/Field'

export interface CampoPersonalizado {
  id: string
  label: string
  tipo: 'text' | 'email' | 'phone' | 'select' | 'checkbox' | 'textarea'
  opciones?: string[]
  requerido: boolean
  campoCrmDestino?:
    'nombre' | 'empresa' | 'cargo' | 'pais' | 'ciudad' | 'telefono' | 'whatsapp' | 'otro'
}

export interface FormularioConvocatoriaConfig {
  id: string
  titulo: string
  descripcion: string
  tipoFormulario: 'registro_evento' | 'actualizacion_datos_crm'
  sincronizarCrmAutomatico: boolean
  campos: CampoPersonalizado[]
}

interface PublicFormBuilderModalProps {
  configInicial?: FormularioConvocatoriaConfig
  eventoNombre?: string
  isOpen: boolean
  onClose: () => void
  onSave: (config: FormularioConvocatoriaConfig) => void
}

type TipoCampo = CampoPersonalizado['tipo']
type DestinoCrm = NonNullable<CampoPersonalizado['campoCrmDestino']>

const TIPO_LABEL: Record<TipoCampo, string> = {
  text: 'Texto corto',
  email: 'Correo electrónico',
  phone: 'Teléfono',
  select: 'Lista desplegable',
  checkbox: 'Casilla de verificación',
  textarea: 'Párrafo',
}

const DESTINO_LABEL: Record<DestinoCrm, string> = {
  nombre: 'Nombre',
  empresa: 'Empresa',
  cargo: 'Cargo',
  pais: 'País',
  ciudad: 'Ciudad',
  telefono: 'Teléfono',
  whatsapp: 'WhatsApp',
  otro: 'Otro dato',
}

const DEFAULT_CAMPOS_ACTUALIZACION: CampoPersonalizado[] = [
  { id: 'f-1', label: 'Nombre Completo', tipo: 'text', requerido: true, campoCrmDestino: 'nombre' },
  {
    id: 'f-2',
    label: 'Empresa / Agencia / Tour Operador',
    tipo: 'text',
    requerido: true,
    campoCrmDestino: 'empresa',
  },
  { id: 'f-3', label: 'Cargo Oficial', tipo: 'text', requerido: true, campoCrmDestino: 'cargo' },
  { id: 'f-4', label: 'Correo Corporativo', tipo: 'email', requerido: true },
  {
    id: 'f-5',
    label: 'Teléfono Directo / WhatsApp',
    tipo: 'phone',
    requerido: true,
    campoCrmDestino: 'whatsapp',
  },
  {
    id: 'f-6',
    label: 'País y Ciudad de Operación',
    tipo: 'text',
    requerido: true,
    campoCrmDestino: 'pais',
  },
  {
    id: 'f-7',
    label: 'Categoría Principal de Negocio',
    tipo: 'select',
    opciones: [
      'Agencia de Viajes',
      'Tour Operador',
      'Cadena Hotelera',
      'OTA',
      'Aerolínea',
      'Medio de Prensa',
    ],
    requerido: true,
  },
  {
    id: 'f-8',
    label: 'Destinos de Interés en Rep. Dominicana',
    tipo: 'select',
    opciones: [
      'Punta Cana / Bávaro',
      'Santo Domingo Colonial',
      'Samaná',
      'Puerto Plata',
      'Miches',
      'Pedernales',
    ],
    requerido: false,
  },
]

const controlClass =
  'w-full rounded-lg border border-slate-300 bg-white px-2.5 py-1.5 text-xs text-slate-900 focus:border-blue-500 focus:outline-none dark:border-slate-700 dark:bg-slate-900 dark:text-white'

/**
 * El diseñador del formulario de una campaña: a la izquierda se arma, a la derecha se ve
 * como lo verá quien lo llene. El diseño acompaña a la campaña en pantalla; convocatorias-service
 * todavía no guarda formularios, así que no viaja en ningún request.
 */
export function PublicFormBuilderModal({
  configInicial,
  eventoNombre,
  isOpen,
  onClose,
  onSave,
}: PublicFormBuilderModalProps) {
  const [titulo, setTitulo] = useState(
    configInicial?.titulo ||
      (eventoNombre
        ? `Formulario Oficial de Inscripción: ${eventoNombre}`
        : 'Campaña de Actualización de Datos de Agencias y Tour Operadores 2026'),
  )
  const [descripcion, setDescripcion] = useState(
    configInicial?.descripcion ||
      'Por favor complete o actualice sus datos para confirmar su registro y recibir las invitaciones del evento.',
  )
  const tipoFormulario = configInicial?.tipoFormulario || 'actualizacion_datos_crm'
  const [sincronizarCrm, setSincronizarCrm] = useState(
    configInicial?.sincronizarCrmAutomatico ?? true,
  )
  const [campos, setCampos] = useState<CampoPersonalizado[]>(
    configInicial?.campos || DEFAULT_CAMPOS_ACTUALIZACION,
  )
  const [nuevoLabel, setNuevoLabel] = useState('')
  const [nuevoTipo, setNuevoTipo] = useState<TipoCampo>('text')

  const cambiarCampo = (id: string, cambio: Partial<CampoPersonalizado>) =>
    setCampos((prev) => prev.map((c) => (c.id === id ? { ...c, ...cambio } : c)))

  const cambiarTipo = (campo: CampoPersonalizado, tipo: TipoCampo) =>
    cambiarCampo(campo.id, {
      tipo,
      // Una lista sin opciones no se puede llenar; al dejar de ser lista, las opciones sobran.
      opciones: tipo === 'select' ? (campo.opciones ?? ['Opción 1', 'Opción 2']) : undefined,
    })

  const moverCampo = (index: number, delta: -1 | 1) =>
    setCampos((prev) => {
      const destino = index + delta
      if (destino < 0 || destino >= prev.length) return prev
      const copia = [...prev]
      ;[copia[index], copia[destino]] = [copia[destino], copia[index]]
      return copia
    })

  const handleAddCampo = () => {
    const label = nuevoLabel.trim()
    if (!label) return
    setCampos((prev) => [
      ...prev,
      {
        id: `custom-${Date.now()}`,
        label,
        tipo: nuevoTipo,
        requerido: false,
        opciones: nuevoTipo === 'select' ? ['Opción 1', 'Opción 2'] : undefined,
      },
    ])
    setNuevoLabel('')
    setNuevoTipo('text')
  }

  const sinTitulo = !titulo.trim()
  const campoSinNombre = campos.some((c) => !c.label.trim())
  const puedeGuardar = !sinTitulo && !campoSinNombre && campos.length > 0

  const handleGuardar = () => {
    if (!puedeGuardar) return
    onSave({
      id: configInicial?.id || `form-${Date.now()}`,
      titulo: titulo.trim(),
      descripcion: descripcion.trim(),
      tipoFormulario,
      sincronizarCrmAutomatico: sincronizarCrm,
      campos: campos.map((c) => ({
        ...c,
        label: c.label.trim(),
        // Mientras se escribe, la lista conserva las comas sueltas; al guardar se limpian.
        opciones: c.opciones?.map((o) => o.trim()).filter(Boolean),
      })),
    })
    onClose()
  }

  if (!isOpen) return null

  return (
    <Modal title="Diseñar formulario de la campaña" onClose={onClose} width="max-w-5xl">
      <div className="space-y-4 text-xs">
        <div className="grid max-h-[68vh] grid-cols-1 gap-5 overflow-y-auto pr-1 lg:grid-cols-5">
          {/* Columna de edición */}
          <div className="space-y-4 lg:col-span-3">
            <div className="space-y-3">
              <Field label="Título del formulario" required>
                <TextInput
                  value={titulo}
                  onChange={(e) => setTitulo(e.target.value)}
                  placeholder="Ej: Actualización de directorio de agencias de viajes"
                />
              </Field>
              <Field label="Instrucciones para quien lo llena">
                <TextArea
                  value={descripcion}
                  onChange={(e) => setDescripcion(e.target.value)}
                  placeholder="Un mensaje breve antes de las preguntas…"
                />
              </Field>
            </div>

            <label className="flex cursor-pointer items-start gap-2.5 rounded-xl border border-slate-200 bg-slate-50/60 p-3 dark:border-slate-700 dark:bg-slate-800/40">
              <input
                type="checkbox"
                checked={sincronizarCrm}
                onChange={(e) => setSincronizarCrm(e.target.checked)}
                className="mt-0.5 h-4 w-4 rounded border-slate-300 text-blue-600"
              />
              <span>
                <span className="flex items-center gap-1.5 font-bold text-slate-800 dark:text-slate-200">
                  <Database size={13} className="text-blue-600" /> Llevar las respuestas al CRM
                </span>
                <span className="block text-[11px] text-slate-500">
                  Cada campo con destino en el CRM actualiza ese dato del contacto o de su empresa.
                </span>
              </span>
            </label>

            {/* Campos */}
            <div className="space-y-2">
              <h4 className="text-[11px] font-bold uppercase tracking-wider text-slate-500">
                Campos del formulario ({campos.length})
              </h4>

              {campos.length === 0 && (
                <p className="rounded-xl border border-dashed border-slate-300 p-3 text-center text-[11px] text-slate-500 dark:border-slate-700">
                  El formulario no tiene campos. Agrega al menos uno.
                </p>
              )}

              {campos.map((campo, index) => (
                <div
                  key={campo.id}
                  className="space-y-2 rounded-xl border border-slate-200 bg-white p-3 shadow-2xs dark:border-slate-800 dark:bg-slate-900"
                >
                  <div className="flex items-center gap-2">
                    <span className="flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-slate-100 font-mono text-[10px] font-bold text-slate-500 dark:bg-slate-800">
                      {index + 1}
                    </span>
                    <TextInput
                      type="text"
                      value={campo.label}
                      onChange={(e) => cambiarCampo(campo.id, { label: e.target.value })}
                      aria-label={`Nombre del campo ${index + 1}`}
                      placeholder="Nombre del campo"
                      className="font-bold"
                    />
                    <div className="flex shrink-0 items-center">
                      <button
                        type="button"
                        onClick={() => moverCampo(index, -1)}
                        disabled={index === 0}
                        aria-label="Subir campo"
                        className="cursor-pointer rounded-lg p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-700 disabled:cursor-not-allowed disabled:opacity-30 dark:hover:bg-slate-800"
                      >
                        <ArrowUp size={14} />
                      </button>
                      <button
                        type="button"
                        onClick={() => moverCampo(index, 1)}
                        disabled={index === campos.length - 1}
                        aria-label="Bajar campo"
                        className="cursor-pointer rounded-lg p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-700 disabled:cursor-not-allowed disabled:opacity-30 dark:hover:bg-slate-800"
                      >
                        <ArrowDown size={14} />
                      </button>
                      <button
                        type="button"
                        onClick={() => setCampos((prev) => prev.filter((c) => c.id !== campo.id))}
                        aria-label="Quitar campo"
                        className="cursor-pointer rounded-lg p-1 text-slate-400 hover:bg-rose-50 hover:text-rose-600 dark:hover:bg-rose-950/50"
                      >
                        <Trash2 size={14} />
                      </button>
                    </div>
                  </div>

                  <div className="grid grid-cols-1 gap-2 pl-7 sm:grid-cols-3">
                    <Select
                      value={campo.tipo}
                      onChange={(e) => cambiarTipo(campo, e.target.value as TipoCampo)}
                      aria-label="Tipo de campo"
                      className={controlClass}
                    >
                      {(Object.keys(TIPO_LABEL) as TipoCampo[]).map((tipo) => (
                        <option key={tipo} value={tipo}>
                          {TIPO_LABEL[tipo]}
                        </option>
                      ))}
                    </Select>
                    <Select
                      value={campo.campoCrmDestino ?? ''}
                      onChange={(e) =>
                        cambiarCampo(campo.id, {
                          campoCrmDestino: (e.target.value || undefined) as DestinoCrm | undefined,
                        })
                      }
                      aria-label="Dato del CRM que actualiza"
                      className={controlClass}
                    >
                      <option value="">No actualiza el CRM</option>
                      {(Object.keys(DESTINO_LABEL) as DestinoCrm[]).map((destino) => (
                        <option key={destino} value={destino}>
                          CRM: {DESTINO_LABEL[destino]}
                        </option>
                      ))}
                    </Select>
                    <label className="flex cursor-pointer items-center gap-1.5 text-slate-600 dark:text-slate-300">
                      <input
                        type="checkbox"
                        checked={campo.requerido}
                        onChange={(e) => cambiarCampo(campo.id, { requerido: e.target.checked })}
                        className="h-3.5 w-3.5 rounded border-slate-300 text-blue-600"
                      />
                      Obligatorio
                    </label>
                  </div>

                  {campo.tipo === 'select' && (
                    <div className="pl-7">
                      <TextInput
                        type="text"
                        value={(campo.opciones ?? []).join(', ')}
                        onChange={(e) =>
                          cambiarCampo(campo.id, {
                            opciones: e.target.value.split(',').map((o) => o.trimStart()),
                          })
                        }
                        aria-label="Opciones de la lista"
                        placeholder="Opciones separadas por coma"
                        className={controlClass}
                      />
                      <p className="mt-1 text-[10px] text-slate-400">
                        Opciones de la lista, separadas por coma.
                      </p>
                    </div>
                  )}
                </div>
              ))}
            </div>

            {/* Agregar campo */}
            <div className="rounded-xl border border-dashed border-slate-300 bg-slate-50/50 p-3 dark:border-slate-700 dark:bg-slate-800/30">
              <p className="mb-2 text-[11px] font-bold text-slate-700 dark:text-slate-300">
                Agregar un campo
              </p>
              <div className="flex flex-wrap items-center gap-2">
                <TextInput
                  type="text"
                  value={nuevoLabel}
                  onChange={(e) => setNuevoLabel(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key !== 'Enter') return
                    e.preventDefault()
                    handleAddCampo()
                  }}
                  aria-label="Nombre del campo nuevo"
                  placeholder="Nombre del campo…"
                  className="min-w-40 flex-1"
                />
                <div className="w-44">
                  <Select
                    value={nuevoTipo}
                    onChange={(e) => setNuevoTipo(e.target.value as TipoCampo)}
                    aria-label="Tipo del campo nuevo"
                  >
                    {(Object.keys(TIPO_LABEL) as TipoCampo[]).map((tipo) => (
                      <option key={tipo} value={tipo}>
                        {TIPO_LABEL[tipo]}
                      </option>
                    ))}
                  </Select>
                </div>
                <Button
                  type="button"
                  size="sm"
                  onClick={handleAddCampo}
                  disabled={!nuevoLabel.trim()}
                  className="cursor-pointer bg-brand-deep font-bold text-white hover:bg-brand-deep/90"
                >
                  <Plus size={14} className="mr-1" /> Agregar
                </Button>
              </div>
            </div>
          </div>

          {/* Vista previa: el formulario como lo verá quien lo llene */}
          <div className="lg:col-span-2">
            <div className="sticky top-0 space-y-2">
              <p className="flex items-center gap-1.5 text-[11px] font-bold uppercase tracking-wider text-slate-500">
                <Eye size={13} /> Vista previa
              </p>
              <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-2xs dark:border-slate-800 dark:bg-slate-950">
                <div className="bg-linear-to-br from-brand-deep to-brand p-4 text-white">
                  <p className="text-[10px] font-black uppercase tracking-widest text-amber-300">
                    Registro de Participantes · Portal Corporativo
                  </p>
                  <p className="mt-0.5 font-sans text-sm font-extrabold leading-snug">
                    {titulo.trim() || 'Título del formulario'}
                  </p>
                </div>
                <div className="space-y-3 p-4">
                  {descripcion.trim() && (
                    <p className="text-[11px] leading-relaxed text-slate-500">{descripcion}</p>
                  )}
                  {campos.map((campo) => (
                    <CampoPreview key={campo.id} campo={campo} />
                  ))}
                  <div className="rounded-lg bg-brand-deep py-2 text-center text-[11px] font-bold text-white">
                    Enviar
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <Callout tone="neutral">
          El diseño queda asociado a esta campaña mientras la creas. El servicio todavía no guarda
          formularios, así que no se envía con la campaña.
        </Callout>

        <div className="flex flex-col-reverse items-stretch gap-2 border-t border-slate-100 pt-3 sm:flex-row sm:items-center sm:justify-end dark:border-slate-800">
          {!puedeGuardar && (
            <span className="text-[11px] font-semibold text-rose-600 sm:mr-auto">
              {sinTitulo
                ? 'El formulario necesita un título.'
                : campos.length === 0
                  ? 'El formulario necesita al menos un campo.'
                  : 'Hay un campo sin nombre.'}
            </span>
          )}
          <Button type="button" variant="outline" onClick={onClose} className="cursor-pointer">
            Cancelar
          </Button>
          <Button
            type="button"
            onClick={handleGuardar}
            disabled={!puedeGuardar}
            className="cursor-pointer bg-brand-deep px-5 font-bold text-white hover:bg-brand-deep/90"
          >
            Guardar formulario
          </Button>
        </div>
      </div>
    </Modal>
  )
}

const previewClass =
  'w-full rounded-md border border-slate-200 bg-slate-50 px-2.5 py-1.5 text-[11px] text-slate-400 dark:border-slate-800 dark:bg-slate-900'

/** Un campo tal como se verá. No se puede escribir en él: es una muestra. */
function CampoPreview({ campo }: { campo: CampoPersonalizado }) {
  const etiqueta = (
    <span className="text-[11px] font-semibold text-slate-700 dark:text-slate-200">
      {campo.label.trim() || 'Campo sin nombre'}
      {campo.requerido && <span className="ml-0.5 text-rose-600">*</span>}
    </span>
  )

  if (campo.tipo === 'checkbox') {
    return (
      <div className="flex items-center gap-2">
        <span className="h-3.5 w-3.5 shrink-0 rounded border border-slate-300 dark:border-slate-700" />
        {etiqueta}
      </div>
    )
  }

  const opciones = (campo.opciones ?? []).map((o) => o.trim()).filter(Boolean)
  return (
    <div className="space-y-1">
      {etiqueta}
      {campo.tipo === 'select' ? (
        <div className={previewClass}>{opciones[0] ?? 'Sin opciones'} ▾</div>
      ) : campo.tipo === 'textarea' ? (
        <div className={`${previewClass} h-12`} />
      ) : (
        <div className={previewClass}>
          {campo.tipo === 'email'
            ? 'nombre@empresa.com'
            : campo.tipo === 'phone'
              ? '+1 809 555 0199'
              : ' '}
        </div>
      )}
    </div>
  )
}
