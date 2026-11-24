import { useState, FormEvent } from 'react'
import { FileText, Save, RotateCcw, Check, Sparkles } from 'lucide-react'
import { Modal } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { Select, TextArea, TextInput } from '@/components/ui/Field'
import type { CampanaTipo, CampanaIdioma } from '../mockParticipants'

import {
  PLANTILLAS_DEFAULT,
  PLANTILLAS_MULTI_IDIOMA,
  type PlantillaCorreo,
} from '../plantillasCorreo'

interface PlantillaEditorModalProps {
  open: boolean
  tipoInicial?: CampanaTipo
  idiomaInicial?: CampanaIdioma
  onClose: () => void
  onSave: (plantilla: PlantillaCorreo) => void
}

export function PlantillaEditorModal({
  open,
  tipoInicial = 'Save the Date',
  idiomaInicial = 'ES',
  onClose,
  onSave,
}: PlantillaEditorModalProps) {
  const [tipo, setTipo] = useState<CampanaTipo>(tipoInicial)
  const [idioma, setIdioma] = useState<CampanaIdioma>(idiomaInicial)

  const initialTpl = PLANTILLAS_MULTI_IDIOMA[idiomaInicial]?.[tipoInicial] ??
    PLANTILLAS_DEFAULT[tipoInicial] ?? { asunto: '', cuerpo: '' }

  const [asunto, setAsunto] = useState(initialTpl.asunto)
  const [cuerpo, setCuerpo] = useState(initialTpl.cuerpo)
  const [savedSuccess, setSavedSuccess] = useState(false)

  const updateTemplateContent = (newTipo: CampanaTipo, newIdioma: CampanaIdioma) => {
    const tplMap = PLANTILLAS_MULTI_IDIOMA[newIdioma] ?? PLANTILLAS_DEFAULT
    const tpl = tplMap[newTipo] ?? tplMap['Personalizada'] ?? { asunto: '', cuerpo: '' }
    setAsunto(tpl.asunto)
    setCuerpo(tpl.cuerpo)
  }

  const handleTipoChange = (newTipo: CampanaTipo) => {
    setTipo(newTipo)
    updateTemplateContent(newTipo, idioma)
  }

  const handleIdiomaChange = (newIdioma: CampanaIdioma) => {
    setIdioma(newIdioma)
    updateTemplateContent(tipo, newIdioma)
  }

  const handleRestoreDefault = () => {
    updateTemplateContent(tipo, idioma)
  }

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault()
    onSave({ tipo, idioma, asunto, cuerpo })
    setSavedSuccess(true)
    setTimeout(() => {
      setSavedSuccess(false)
      onClose()
    }, 1200)
  }

  return (
    <Modal open={open} onClose={onClose} width="max-w-3xl">
      <div className="space-y-5 p-2">
        <div className="border-b border-slate-100 dark:border-slate-800 pb-3 flex items-center justify-between">
          <div>
            <h2 className="font-sans text-lg font-black text-slate-900 dark:text-white flex items-center gap-2">
              <FileText size={20} className="text-blue-600" /> Gestor de Plantillas Multi-Idioma
            </h2>
            <p className="text-xs text-slate-500">
              Edita los mensajes oficiales predefinidos por tipo e idioma.
            </p>
          </div>
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={handleRestoreDefault}
            className="text-xs font-bold cursor-pointer"
          >
            <RotateCcw size={13} className="mr-1" /> Restaurar Predeterminada
          </Button>
        </div>

        {savedSuccess && (
          <div className="p-3 rounded-xl bg-emerald-50 text-emerald-800 text-xs font-bold flex items-center gap-2 dark:bg-emerald-950 dark:text-emerald-300">
            <Check size={16} /> ¡Plantilla guardada correctamente!
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4 text-xs">
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <div>
              <label className="mb-1 block font-bold text-slate-800 dark:text-slate-200">
                Idioma de la Plantilla
              </label>
              <Select
                value={idioma}
                onChange={(e) => handleIdiomaChange(e.target.value as CampanaIdioma)}
              >
                <option value="ES">Español (ES)</option>
                <option value="EN">English (EN)</option>
                <option value="FR">Français (FR)</option>
                <option value="PT">Português (PT)</option>
                <option value="DE">Deutsch (DE)</option>
                <option value="IT">Italiano (IT)</option>
              </Select>
            </div>

            <div>
              <label className="mb-1 block font-bold text-slate-800 dark:text-slate-200">
                Tipo de Campaña
              </label>
              <Select
                value={tipo}
                onChange={(e) => handleTipoChange(e.target.value as CampanaTipo)}
              >
                <option value="Save the Date">Save the Date</option>
                <option value="Apertura de Registro">Apertura de Registro</option>
                <option value="Pase VIP & Acreditación">Pase VIP & Acreditación</option>
                <option value="Convocatoria de Prensa & Medios">
                  Convocatoria de Prensa & Medios
                </option>
                <option value="Agenda & Programa Oficial">Agenda & Programa Oficial</option>
                <option value="Confirmación de RSVP">Confirmación de RSVP</option>
                <option value="Guía del Participante & Protocolo">
                  Guía del Participante & Protocolo
                </option>
                <option value="Rueda de Negocios B2B">Rueda de Negocios B2B</option>
                <option value="Venue Confirmado">Venue Confirmado</option>
                <option value="Reminder">Reminder / Recordatorio</option>
                <option value="Cambio de Fecha">Cambio de Fecha</option>
                <option value="Cambio de Venue">Cambio de Venue</option>
                <option value="Posposición">Posposición</option>
                <option value="Cancelación">Cancelación</option>
                <option value="Carta de Agradecimiento">Carta de Agradecimiento</option>
                <option value="Encuesta Post Evento">Encuesta Post Evento</option>
                <option value="Resumen & Highlights del Evento">
                  Resumen & Highlights del Evento
                </option>
                <option value="Certificado de Asistencia">Certificado de Asistencia</option>
                <option value="Personalizada">Personalizada</option>
              </Select>
            </div>

            <div>
              <label className="mb-1 block font-bold text-slate-800 dark:text-slate-200">
                Asunto del Correo
              </label>
              <TextInput value={asunto} onChange={(e) => setAsunto(e.target.value)} required />
            </div>
          </div>

          <div>
            <label className="mb-1 block font-bold text-slate-800 dark:text-slate-200">
              Cuerpo del Mensaje ({idioma})
            </label>
            <TextArea
              rows={8}
              value={cuerpo}
              onChange={(e) => setCuerpo(e.target.value)}
              className="font-mono"
              required
            />
          </div>

          <div className="rounded-2xl bg-blue-50/70 border border-blue-200/80 p-3 text-[11px] text-blue-900 dark:bg-blue-950/40 dark:border-blue-900 dark:text-blue-300 flex items-center justify-between">
            <span className="flex items-center gap-1 font-bold">
              <Sparkles size={14} className="text-amber-500" /> Variables Dinámicas Disponibles:
            </span>
            <code>{`{{nombre}} {{empresa}} {{evento}} {{fecha}} {{qr_url}} {{checkin_url}}`}</code>
          </div>

          <div className="flex justify-end gap-2 pt-3 border-t border-slate-100 dark:border-slate-800">
            <Button type="button" variant="outline" onClick={onClose} className="cursor-pointer">
              Cancelar
            </Button>
            <Button type="submit" className="bg-brand-deep text-white font-bold cursor-pointer">
              <Save size={14} className="mr-1.5" /> Guardar Plantilla ({idioma})
            </Button>
          </div>
        </form>
      </div>
    </Modal>
  )
}
