import { toast } from 'sonner'
import { useState } from 'react'
import { getBrandColors } from '@/lib/branding'
import { MenuItem, MenuSeparator } from '@/components/ui/MenuItem'
import { RowActionMenu } from '@/components/ui/RowActionMenu'
import {
  Save,
  RotateCcw,
  Sparkles,
  Eye,
  Edit3,
  Mail,
  QrCode,
  ExternalLink,
  CheckCircle2,
  Plus,
  Trash2,
  ArrowUp,
  ArrowDown,
  ArrowLeft,
  Columns,
  Image as ImageIcon,
  Heading,
  Type,
  MousePointerClick,
  Code,
  Minus,
  CheckSquare,
  Layers,
  LayoutGrid,
  Copy,
  List,
  Search,
} from 'lucide-react'
import { Card } from '@/components/ui/Card'
import { CardGrid, GridCard } from '@/components/ui/GridCard'
import { Callout } from '@/components/ui/Callout'
import { Tabs } from '@/components/ui/Tabs'
import { Breadcrumbs } from '@/components/ui/Breadcrumbs'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { Field, Select, TextInput } from '@/components/ui/Field'
import { PLANTILLAS_MULTI_IDIOMA } from './plantillasCorreo'
import type { CampanaTipo } from './mockParticipants'
import { safeUrl } from '@/lib/safeUrl'

type Plantilla = { asunto: string; cuerpo: string }

/** Una entrada del catálogo. Una copia o una plantilla nueva toma su texto de `origen`. */
type TipoCatalogo = { key: CampanaTipo; label: string; desc: string; origen?: CampanaTipo }

const TIPOS_CAMPANA_BASE: TipoCatalogo[] = [
  {
    key: 'Save the Date',
    label: 'Save the Date',
    desc: 'Reserva inicial de fecha para agendar el evento',
  },
  {
    key: 'Apertura de Registro',
    label: 'Apertura de Registro',
    desc: 'Invitación oficial con formulario de inscripción',
  },
  {
    key: 'Pase VIP & Acreditación',
    label: 'Pase VIP & Acreditación',
    desc: 'Emisión de pase digital con QR para accesos especiales',
  },
  {
    key: 'Convocatoria de Prensa & Medios',
    label: 'Prensa & Medios',
    desc: 'Convocatoria para acreditación de medios y rueda de prensa',
  },
  {
    key: 'Agenda & Programa Oficial',
    label: 'Agenda & Programa',
    desc: 'Envío del cronograma de conferencias y reuniones B2B',
  },
  {
    key: 'Confirmación de RSVP',
    label: 'Confirmación de RSVP',
    desc: 'Confirmación automática tras aceptar asistencia',
  },
  {
    key: 'Guía del Participante & Protocolo',
    label: 'Guía del Participante',
    desc: 'Instrucciones de llegada, vestimenta y protocolo',
  },
  {
    key: 'Rueda de Negocios B2B',
    label: 'Rueda de Negocios B2B',
    desc: 'Cita agendada de negocios y match-making',
  },
  {
    key: 'Venue Confirmado',
    label: 'Venue & Logística',
    desc: 'Confirmación de sede, mapas e indicaciones de acceso',
  },
  {
    key: 'Reminder',
    label: 'Recordatorio Pre-Evento',
    desc: 'Alerta de cierre de inscripciones y confirmación RSVP',
  },
  {
    key: 'Cambio de Fecha',
    label: 'Cambio de Fecha',
    desc: 'Notificación de actualización de calendario',
  },
  { key: 'Cambio de Venue', label: 'Cambio de Venue', desc: 'Aviso de reubicación de la sede' },
  {
    key: 'Carta de Agradecimiento',
    label: 'Carta de Agradecimiento',
    desc: 'Carta oficial de agradecimiento tras finalizar el evento',
  },
  {
    key: 'Encuesta Post Evento',
    label: 'Encuesta de Satisfacción',
    desc: 'Encuesta de valoración y comentarios post-evento',
  },
  {
    key: 'Resumen & Highlights del Evento',
    label: 'Resumen & Highlights',
    desc: 'Galería de fotos, notas de prensa y video resumen',
  },
  {
    key: 'Certificado de Asistencia',
    label: 'Certificado de Asistencia',
    desc: 'Envío de diploma/certificado oficial tras el evento',
  },
]

const IDIOMAS: { key: string; label: string }[] = [
  { key: 'ES', label: 'Español (ES)' },
  { key: 'EN', label: 'English (EN)' },
  { key: 'FR', label: 'Français (FR)' },
  { key: 'PT', label: 'Português (PT)' },
  { key: 'DE', label: 'Deutsch (DE)' },
  { key: 'IT', label: 'Italiano (IT)' },
]

const CATALOGO = PLANTILLAS_MULTI_IDIOMA as Record<string, Record<string, Plantilla>>

/** La clave bajo la que está el texto de una entrada: la suya, o la de la que se copió. */
const claveDeTexto = (item: TipoCatalogo) => item.origen ?? item.key

/** El texto de una plantilla en un idioma; `undefined` si no existe en ese idioma. */
const plantillaEn = (item: TipoCatalogo, idioma: string): Plantilla | undefined =>
  CATALOGO[idioma]?.[claveDeTexto(item)]

/** Los idiomas en los que una plantilla tiene texto de verdad, en el orden de `IDIOMAS`. */
const idiomasDe = (item: TipoCatalogo): string[] =>
  IDIOMAS.filter((i) => plantillaEn(item, i.key)).map((i) => i.key)

export type BlockType =
  'heading' | 'text' | 'image' | 'button' | 'columns' | 'form' | 'iframe' | 'divider' | 'qrcode'

export interface EmailBlock {
  id: string
  type: BlockType
  content?: string
  url?: string
  alt?: string
  buttonText?: string
  buttonUrl?: string
  color?: string
  col1Text?: string
  col2Text?: string
  formLabel?: string
  formPlaceholder?: string
  iframeSrc?: string
}

const PALETTE_ITEMS: { type: BlockType; label: string; icon: typeof Heading; desc: string }[] = [
  { type: 'heading', label: 'Encabezado / Título', icon: Heading, desc: 'Título principal H1/H2' },
  { type: 'text', label: 'Campo de Texto', icon: Type, desc: 'Párrafo o texto informativo' },
  {
    type: 'image',
    label: 'Imagen / Banner',
    icon: ImageIcon,
    desc: 'Imagen destacada o banner hero',
  },
  {
    type: 'button',
    label: 'Botón CTA',
    icon: MousePointerClick,
    desc: 'Botón de llamada a la acción con link',
  },
  { type: 'columns', label: '2 Columnas', icon: Columns, desc: 'Layout paralelo de 2 columnas' },
  {
    type: 'form',
    label: 'Campo / Encuesta',
    icon: CheckSquare,
    desc: 'Pregunta o input de respuesta',
  },
  {
    type: 'iframe',
    label: 'Iframe / Embebido',
    icon: Code,
    desc: 'Mapa Google o código HTML embebido',
  },
  {
    type: 'divider',
    label: 'Separador / Divisor',
    icon: Minus,
    desc: 'Línea decorativa horizontal',
  },
  { type: 'qrcode', label: 'Código QR', icon: QrCode, desc: 'Pase digital VIP con QR' },
]

const DEFAULT_BLOCKS: EmailBlock[] = [
  {
    id: 'blk-1',
    type: 'heading',
    content: 'Convocatoria Oficial: {{evento}}',
  },
  {
    id: 'blk-2',
    type: 'text',
    content:
      'Estimado/a {{nombre}},\n\nLe invitamos cordialmente a participar en la jornada promocional de turismo {{evento}} que se celebrará en {{lugar}}.',
  },
  {
    id: 'blk-3',
    type: 'image',
    url: 'https://images.unsplash.com/photo-1540575467063-178a50c2df87?auto=format&fit=crop&w=1200&q=80',
    alt: 'Banner Promocional del Evento',
  },
  {
    id: 'blk-4',
    type: 'columns',
    col1Text: 'Fecha: {{fecha}}',
    col2Text: 'Sede: Hotel Meliá Caribe Resort',
  },
  {
    id: 'blk-5',
    type: 'button',
    buttonText: 'Confirmar Asistencia / Ver Registro',
    buttonUrl: 'https://sorteos.empresa.com',
    color: getBrandColors().primary,
  },
  {
    id: 'blk-6',
    type: 'form',
    formLabel: '¿Requiere traslado desde el aeropuerto?',
    formPlaceholder: 'Indique su itinerario de vuelo o sugerencias...',
  },
  {
    id: 'blk-7',
    type: 'iframe',
    iframeSrc: 'https://maps.google.com/maps?q=Punta+Cana&t=&z=13&ie=UTF8&iwloc=&output=embed',
  },
  {
    id: 'blk-8',
    type: 'qrcode',
  },
]

export function PlantillasConvocatoriaPage() {
  const [selectedTipo, setSelectedTipo] = useState<CampanaTipo>('Apertura de Registro')
  const [selectedIdioma, setSelectedIdioma] = useState<string>('ES')
  const [viewMode, setViewMode] = useState<'catalogo' | 'drag_drop' | 'edicion' | 'preview'>(
    'catalogo',
  )
  const [catalogoDisplayMode, setCatalogoDisplayMode] = useState<'grid' | 'list'>('grid')
  const [selectedDetailTemplate, setSelectedDetailTemplate] = useState<{
    key: string
    label: string
    desc: string
    tpl: { asunto: string; cuerpo: string }
  } | null>(null)
  const [catalogoSearch, setCatalogoSearch] = useState('')
  const [catalogoIdioma, setCatalogoIdioma] = useState('ES')
  const [tiposCampana, setTiposCampana] = useState<TipoCatalogo[]>(TIPOS_CAMPANA_BASE)
  const [deleteTarget, setDeleteTarget] = useState<TipoCatalogo | null>(null)
  const [savedSuccess, setSavedSuccess] = useState(false)

  // Cargar plantilla actual desde el catálogo multi-idioma
  const defaultTemplate = (
    PLANTILLAS_MULTI_IDIOMA as Record<string, Record<string, { asunto: string; cuerpo: string }>>
  )[selectedIdioma]?.[selectedTipo] ?? {
    asunto: 'Convocatoria Oficial: {{evento}}',
    cuerpo: 'Estimado/a {{nombre}},\n\nLe invitamos a participar en el evento {{evento}}...',
  }

  const [asunto, setAsunto] = useState(defaultTemplate.asunto)
  const [cuerpo, setCuerpo] = useState(defaultTemplate.cuerpo)
  const [ctaTexto, setCtaTexto] = useState('Confirmar Asistencia / Ver Registro')

  // Drag & Drop State
  const [blocks, setBlocks] = useState<EmailBlock[]>(DEFAULT_BLOCKS)
  const [selectedBlockId, setSelectedBlockId] = useState<string | null>('blk-1')
  const [draggedBlockType, setDraggedBlockType] = useState<BlockType | null>(null)

  // Manejador al cambiar tipo o idioma
  const handleTipoOrIdiomaChange = (tipo: CampanaTipo, idioma: string) => {
    setSelectedTipo(tipo)
    setSelectedIdioma(idioma)
    const tpl = (
      PLANTILLAS_MULTI_IDIOMA as Record<string, Record<string, { asunto: string; cuerpo: string }>>
    )[idioma]?.[tipo] ?? {
      asunto: `Convocatoria Oficial: {{evento}}`,
      cuerpo: `Estimado/a {{nombre}},\n\nLe invitamos a participar en el evento {{evento}}...`,
    }
    setAsunto(tpl.asunto)
    setCuerpo(tpl.cuerpo)
    setSavedSuccess(false)
  }

  const insertVariable = (variableTag: string) => {
    setCuerpo((prev: string) => `${prev} ${variableTag}`)
  }

  const handleSave = () => {
    setSavedSuccess(true)
    setTimeout(() => setSavedSuccess(false), 3000)
  }

  const handleReset = () => {
    setBlocks(DEFAULT_BLOCKS)
    const tpl = (
      PLANTILLAS_MULTI_IDIOMA as Record<string, Record<string, { asunto: string; cuerpo: string }>>
    )[selectedIdioma]?.[selectedTipo]
    if (tpl) {
      setAsunto(tpl.asunto)
      setCuerpo(tpl.cuerpo)
      setSavedSuccess(false)
    }
  }

  // Operaciones de Bloques en Constructor Drag & Drop
  const handleAddBlock = (type: BlockType) => {
    const newBlock: EmailBlock = {
      id: `blk-${Date.now()}`,
      type,
      content:
        type === 'heading'
          ? 'Nuevo Título de Sección'
          : type === 'text'
            ? 'Texto descriptivo adicional del correo...'
            : undefined,
      url:
        type === 'image'
          ? 'https://images.unsplash.com/photo-1540575467063-178a50c2df87?auto=format&fit=crop&w=1200&q=80'
          : undefined,
      buttonText: type === 'button' ? 'Hacer clic aquí' : undefined,
      buttonUrl: type === 'button' ? 'https://empresa.com' : undefined,
      color: type === 'button' ? getBrandColors().primary : undefined,
      col1Text: type === 'columns' ? 'Columna 1: Información A' : undefined,
      col2Text: type === 'columns' ? 'Columna 2: Información B' : undefined,
      formLabel: type === 'form' ? 'Pregunta o comentario' : undefined,
      formPlaceholder: type === 'form' ? 'Escriba aquí...' : undefined,
      iframeSrc:
        type === 'iframe'
          ? 'https://maps.google.com/maps?q=Santo+Domingo&t=&z=13&ie=UTF8&iwloc=&output=embed'
          : undefined,
    }

    setBlocks((prev) => [...prev, newBlock])
    setSelectedBlockId(newBlock.id)
  }

  const handleDropCanvas = (e: React.DragEvent) => {
    e.preventDefault()
    if (!draggedBlockType) return
    handleAddBlock(draggedBlockType)
    setDraggedBlockType(null)
  }

  const handleMoveBlock = (index: number, direction: 'up' | 'down') => {
    const newBlocks = [...blocks]
    const targetIndex = direction === 'up' ? index - 1 : index + 1
    if (targetIndex < 0 || targetIndex >= newBlocks.length) return
    const temp = newBlocks[index]
    newBlocks[index] = newBlocks[targetIndex]
    newBlocks[targetIndex] = temp
    setBlocks(newBlocks)
  }

  const handleDeleteBlock = (id: string) => {
    setBlocks((prev) => prev.filter((b) => b.id !== id))
    if (selectedBlockId === id) setSelectedBlockId(null)
  }

  const updateBlockById = (id: string, field: keyof EmailBlock, value: string) => {
    setBlocks((prev) => prev.map((b) => (b.id === id ? { ...b, [field]: value } : b)))
  }

  // Interpolación de datos demo para el diseño previsualizado
  const demoData = {
    nombre: 'Carlos Mendoza',
    empresa: 'Organización Anfitriona',
    evento: 'Celebración Corporativa & Rifa Anual 2026',
    fecha: '15 de Diciembre 2026',
    lugar: 'Salón Principal de Convenciones',
    checkin_url: 'https://sorteos.empresa.com/p/eventos/ev-navidad-2026',
  }

  const renderInterpolatedText = (text: string) => {
    return text
      .replaceAll('{{nombre}}', demoData.nombre)
      .replaceAll('{{empresa}}', demoData.empresa)
      .replaceAll('{{evento}}', demoData.evento)
      .replaceAll('{{fecha}}', demoData.fecha)
      .replaceAll('{{lugar}}', demoData.lugar)
      .replaceAll('{{checkin_url}}', demoData.checkin_url)
  }

  // State para creación y duplicación de plantillas
  const [showNewModal, setShowNewModal] = useState(false)
  const [newNombreTpl, setNewNombreTpl] = useState('')
  const [duplicateMsg, setDuplicateMsg] = useState('')
  /** La entrada del catálogo que se está editando, para decirlo en la cabecera. */
  const [editando, setEditando] = useState<TipoCatalogo | null>(null)

  const avisar = (mensaje: string) => {
    setDuplicateMsg(mensaje)
    setTimeout(() => setDuplicateMsg(''), 4000)
  }

  /** Abre el constructor con el texto de una entrada del catálogo, en un idioma. */
  const editar = (item: TipoCatalogo, idioma: string) => {
    handleTipoOrIdiomaChange(claveDeTexto(item), idioma)
    setEditando(item)
    setViewMode('drag_drop')
  }

  /** La copia aparece en el catálogo, justo debajo de la original, con su mismo texto. */
  const handleDuplicateTemplate = (item: TipoCatalogo) => {
    const copia: TipoCatalogo = {
      key: `${item.key}-copia-${Date.now()}` as CampanaTipo,
      label: `${item.label} (copia)`,
      desc: item.desc,
      origen: claveDeTexto(item),
    }
    setTiposCampana((prev) => {
      const i = prev.findIndex((t) => t.key === item.key)
      return [...prev.slice(0, i + 1), copia, ...prev.slice(i + 1)]
    })
    avisar(`Se creó "${copia.label}".`)
  }

  const handleConfirmDelete = () => {
    if (!deleteTarget) return
    setTiposCampana((prev) => prev.filter((t) => t.key !== deleteTarget.key))
    avisar(`Se eliminó "${deleteTarget.label}" del catálogo.`)
    setDeleteTarget(null)
  }

  /** La plantilla nueva entra al catálogo partiendo del texto de la base elegida, y se abre. */
  const handleCreateNewTemplate = (e: React.FormEvent) => {
    e.preventDefault()
    const nombre = newNombreTpl.trim()
    if (!nombre) return
    const nueva: TipoCatalogo = {
      key: `nueva-${Date.now()}` as CampanaTipo,
      label: nombre,
      desc: 'Plantilla creada en esta sesión',
      origen: selectedTipo,
    }
    setTiposCampana((prev) => [nueva, ...prev])
    setBlocks(DEFAULT_BLOCKS)
    setShowNewModal(false)
    setNewNombreTpl('')
    editar(nueva, selectedIdioma)
    avisar(`Se creó la plantilla "${nombre}".`)
  }

  const idiomaLabelDe = (clave: string) => IDIOMAS.find((i) => i.key === clave)?.label ?? clave

  return (
    <div className="space-y-6">
      <Breadcrumbs
        items={
          viewMode === 'catalogo'
            ? [{ label: 'Invitaciones', to: '/convocatorias' }, { label: 'Plantillas' }]
            : [
                { label: 'Invitaciones', to: '/convocatorias' },
                { label: 'Plantillas' },
                { label: editando?.label ?? 'Edición' },
              ]
        }
      />

      {/* Encabezado: el catálogo o la plantilla que se está editando */}
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div className="min-w-0">
          <h1 className="font-sans text-2xl font-extrabold tracking-tight text-slate-900 sm:text-3xl dark:text-white">
            {viewMode === 'catalogo'
              ? 'Gestor de Plantillas de Convocatoria'
              : (editando?.label ?? 'Editar plantilla')}
          </h1>
          <p className="mt-1 text-xs text-slate-500 sm:text-sm dark:text-slate-400">
            {viewMode === 'catalogo'
              ? `${tiposCampana.length} plantillas de correo, en hasta ${IDIOMAS.length} idiomas.`
              : `Editando la versión en ${idiomaLabelDe(selectedIdioma)}.`}
          </p>
        </div>

        {viewMode === 'catalogo' ? (
          <Button
            onClick={() => setShowNewModal(true)}
            className="bg-brand-deep font-bold text-white hover:bg-brand-deep/90 cursor-pointer shadow-sm"
          >
            <Plus size={16} className="mr-1 stroke-[2.5]" /> Nueva plantilla
          </Button>
        ) : (
          <Button
            type="button"
            variant="outline"
            onClick={() => setViewMode('catalogo')}
            className="cursor-pointer text-xs font-bold"
          >
            <ArrowLeft size={14} className="mr-1.5" /> Volver al catálogo
          </Button>
        )}
      </div>

      <Callout tone="neutral">
        Las plantillas que se envían de verdad son las del servicio, que todavía no se editan desde
        aquí. Lo que crees o cambies en esta pantalla es de trabajo: se pierde al recargar la
        página.
      </Callout>

      {duplicateMsg && (
        <Callout tone="success" onDismiss={() => setDuplicateMsg('')}>
          {duplicateMsg}
        </Callout>
      )}

      {/* Barra de edición: modo, y el idioma que se está editando */}
      {viewMode !== 'catalogo' && (
        <Card className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-slate-200/80 bg-white p-3 dark:border-slate-800 dark:bg-slate-900">
          <Tabs
            size="sm"
            value={viewMode}
            onChange={setViewMode}
            items={[
              {
                key: 'drag_drop',
                label: (
                  <>
                    <LayoutGrid size={14} /> Constructor
                  </>
                ),
              },
              {
                key: 'edicion',
                label: (
                  <>
                    <Edit3 size={14} /> Texto
                  </>
                ),
              },
              {
                key: 'preview',
                label: (
                  <>
                    <Eye size={14} /> Vista previa
                  </>
                ),
              },
            ]}
          />

          <div className="flex items-center gap-2">
            <label
              htmlFor="idioma-edicion"
              className="text-xs font-bold text-slate-600 dark:text-slate-300"
            >
              Idioma
            </label>
            <div className="w-48">
              <Select
                id="idioma-edicion"
                value={selectedIdioma}
                onChange={(e) => handleTipoOrIdiomaChange(selectedTipo, e.target.value)}
              >
                {IDIOMAS.map((i) => (
                  <option key={i.key} value={i.key}>
                    {i.label}
                    {editando && !plantillaEn(editando, i.key) ? ' · sin versión' : ''}
                  </option>
                ))}
              </Select>
            </div>
          </div>
        </Card>
      )}

      {/* =========================================================
          MODO 0: CATÁLOGO DE PLANTILLAS EN VISTA CUADRÍCULA / LISTA
         ========================================================= */}
      {viewMode === 'catalogo' &&
        (() => {
          const q = catalogoSearch.trim().toLowerCase()
          const filteredTemplates = tiposCampana.filter((t) => {
            const asunto = (plantillaEn(t, catalogoIdioma) ?? plantillaEn(t, 'ES'))?.asunto ?? ''
            return (
              !q ||
              t.label.toLowerCase().includes(q) ||
              t.desc.toLowerCase().includes(q) ||
              asunto.toLowerCase().includes(q)
            )
          })

          /** El texto que se muestra de una entrada: el del idioma elegido, o el español. */
          const vista = (item: TipoCatalogo) => {
            const enIdioma = plantillaEn(item, catalogoIdioma)
            return {
              tpl: enIdioma ??
                plantillaEn(item, 'ES') ?? {
                  asunto: `Convocatoria oficial: ${item.label}`,
                  cuerpo: '',
                },
              falta: !enIdioma,
            }
          }

          const verPrevia = (item: TipoCatalogo) =>
            setSelectedDetailTemplate({
              key: item.key,
              label: item.label,
              desc: item.desc,
              tpl: vista(item).tpl,
            })

          const menu = (item: TipoCatalogo) => (
            <RowActionMenu label={`Más opciones de ${item.label}`} menuClassName="w-48">
              <MenuItem onClick={() => handleDuplicateTemplate(item)}>
                <Copy size={14} className="text-slate-500" /> Duplicar
              </MenuItem>
              <MenuSeparator />
              <MenuItem tone="danger" onClick={() => setDeleteTarget(item)}>
                <Trash2 size={14} /> Eliminar
              </MenuItem>
            </RowActionMenu>
          )

          const idiomasChips = (item: TipoCatalogo) => {
            const disponibles = idiomasDe(item)
            return (
              <div className="flex flex-wrap gap-1">
                {IDIOMAS.map((i) => {
                  const tiene = disponibles.includes(i.key)
                  return (
                    <span
                      key={i.key}
                      title={tiene ? i.label : `Sin versión en ${i.label}`}
                      className={`rounded px-1.5 py-0.5 font-mono text-[10px] font-bold ${
                        tiene
                          ? 'bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-200'
                          : 'text-slate-300 line-through dark:text-slate-600'
                      }`}
                    >
                      {i.key}
                    </span>
                  )
                })}
              </div>
            )
          }

          return (
            <Card className="space-y-4 rounded-3xl border border-slate-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div className="relative min-w-52 flex-1 sm:max-w-sm">
                  <Search
                    size={15}
                    className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400"
                  />
                  <input
                    type="search"
                    value={catalogoSearch}
                    onChange={(e) => setCatalogoSearch(e.target.value)}
                    placeholder="Buscar por nombre o asunto…"
                    aria-label="Buscar plantilla"
                    className="w-full rounded-xl border border-slate-200 bg-slate-50 py-2 pl-9 pr-4 text-xs text-slate-900 focus:border-blue-600 focus:outline-none dark:border-slate-700 dark:bg-slate-800 dark:text-white"
                  />
                </div>

                <div className="flex flex-wrap items-center gap-2">
                  <label
                    htmlFor="idioma-catalogo"
                    className="text-xs font-bold text-slate-600 dark:text-slate-300"
                  >
                    Ver en
                  </label>
                  <div className="w-40">
                    <Select
                      id="idioma-catalogo"
                      value={catalogoIdioma}
                      onChange={(e) => setCatalogoIdioma(e.target.value)}
                    >
                      {IDIOMAS.map((i) => (
                        <option key={i.key} value={i.key}>
                          {i.label}
                        </option>
                      ))}
                    </Select>
                  </div>
                  <Tabs
                    size="sm"
                    variant="segmented"
                    value={catalogoDisplayMode}
                    onChange={setCatalogoDisplayMode}
                    items={[
                      {
                        key: 'grid',
                        label: (
                          <>
                            <LayoutGrid size={14} /> Cuadrícula
                          </>
                        ),
                      },
                      {
                        key: 'list',
                        label: (
                          <>
                            <List size={14} /> Lista
                          </>
                        ),
                      },
                    ]}
                  />
                </div>
              </div>

              <p className="text-[11px] font-semibold text-slate-400">
                {filteredTemplates.length} de {tiposCampana.length} plantillas
              </p>

              {filteredTemplates.length === 0 ? (
                <p className="py-10 text-center text-xs font-semibold text-slate-400">
                  Ninguna plantilla coincide con la búsqueda.
                </p>
              ) : catalogoDisplayMode === 'grid' ? (
                <CardGrid>
                  {filteredTemplates.map((item) => {
                    const { tpl, falta } = vista(item)
                    return (
                      <GridCard
                        key={item.key}
                        title={item.label}
                        subtitle={<span className="font-normal leading-relaxed">{item.desc}</span>}
                        footerStart={
                          <div className="flex flex-wrap items-center gap-2">
                            <Button
                              size="sm"
                              onClick={() => editar(item, catalogoIdioma)}
                              className="cursor-pointer bg-brand-deep text-xs font-bold text-white hover:bg-brand-deep/90"
                            >
                              <Edit3 size={13} className="mr-1" /> Editar
                            </Button>
                            <Button
                              variant="outline"
                              size="sm"
                              onClick={() => verPrevia(item)}
                              className="cursor-pointer text-xs font-bold"
                            >
                              <Eye size={13} className="mr-1" /> Vista previa
                            </Button>
                          </div>
                        }
                        footerEnd={menu(item)}
                      >
                        <div className="space-y-2">
                          <p className="line-clamp-2 rounded-lg bg-slate-50 px-2.5 py-1.5 text-[11px] text-slate-600 dark:bg-slate-800/60 dark:text-slate-300">
                            <span className="font-bold text-slate-400">Asunto: </span>
                            {tpl.asunto}
                          </p>
                          {falta && (
                            <p className="text-[11px] font-semibold text-amber-600 dark:text-amber-400">
                              Sin versión en {idiomaLabelDe(catalogoIdioma)}; se muestra la de
                              español.
                            </p>
                          )}
                          {idiomasChips(item)}
                        </div>
                      </GridCard>
                    )
                  })}
                </CardGrid>
              ) : (
                <div className="overflow-x-auto rounded-2xl border border-slate-200 dark:border-slate-800">
                  <table className="w-full text-left text-xs">
                    <thead className="bg-slate-50 text-[10px] font-extrabold uppercase tracking-wider text-slate-500 dark:bg-slate-800 dark:text-slate-300">
                      <tr>
                        <th className="p-3.5">Plantilla</th>
                        <th className="p-3.5">Asunto</th>
                        <th className="p-3.5">Idiomas</th>
                        <th className="p-3.5" />
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                      {filteredTemplates.map((item) => {
                        const { tpl } = vista(item)
                        return (
                          <tr
                            key={item.key}
                            className="hover:bg-slate-50 dark:hover:bg-slate-800/50"
                          >
                            <td className="p-3.5">
                              <p className="font-extrabold text-slate-900 dark:text-white">
                                {item.label}
                              </p>
                              <p className="text-[11px] text-slate-500">{item.desc}</p>
                            </td>
                            <td className="max-w-xs truncate p-3.5 text-slate-600 dark:text-slate-300">
                              {tpl.asunto}
                            </td>
                            <td className="p-3.5">{idiomasChips(item)}</td>
                            <td className="p-3.5">
                              <div className="flex items-center justify-end gap-1.5">
                                <Button
                                  size="sm"
                                  onClick={() => editar(item, catalogoIdioma)}
                                  className="cursor-pointer bg-brand-deep text-xs font-bold text-white hover:bg-brand-deep/90"
                                >
                                  <Edit3 size={13} className="mr-1" /> Editar
                                </Button>
                                <Button
                                  variant="outline"
                                  size="sm"
                                  onClick={() => verPrevia(item)}
                                  className="cursor-pointer text-xs font-bold"
                                >
                                  <Eye size={13} className="mr-1" /> Vista previa
                                </Button>
                                {menu(item)}
                              </div>
                            </td>
                          </tr>
                        )
                      })}
                    </tbody>
                  </table>
                </div>
              )}
            </Card>
          )
        })()}

      {/* =========================================================
          MODO 1: CONSTRUCTOR INLINE (SIN PANEL DERECHO DE PROPIEDADES)
         ========================================================= */}
      {viewMode === 'drag_drop' && (
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
          {/* Paleta de Bloques Arrastrables (Left Panel - 3 Cols) */}
          <div className="lg:col-span-3 space-y-4">
            <Card className="p-4 bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-3xl space-y-3 sticky top-6">
              <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-2.5">
                <span className="text-xs font-extrabold text-slate-900 dark:text-white flex items-center gap-2">
                  <Layers size={16} className="text-blue-600" /> Paleta de Elementos
                </span>
                <span className="text-[10px] font-bold text-slate-400">Arrastrar o Clic</span>
              </div>

              <div className="space-y-2">
                {PALETTE_ITEMS.map((item) => {
                  const Icon = item.icon
                  return (
                    <div
                      key={item.type}
                      draggable
                      onDragStart={() => setDraggedBlockType(item.type)}
                      onClick={() => handleAddBlock(item.type)}
                      className="p-3 rounded-2xl border border-slate-200/80 bg-slate-50 hover:bg-blue-50/70 hover:border-blue-300 dark:border-slate-800 dark:bg-slate-800/60 dark:hover:bg-blue-950/40 cursor-grab active:cursor-grabbing transition-all group flex items-center justify-between"
                    >
                      <div className="flex items-center gap-2.5">
                        <div className="flex h-8 w-8 items-center justify-center rounded-xl bg-white text-slate-700 shadow-2xs group-hover:bg-brand-deep group-hover:text-white dark:bg-slate-700 dark:text-slate-200 transition-colors">
                          <Icon size={16} />
                        </div>
                        <div>
                          <p className="text-xs font-bold text-slate-900 dark:text-white">
                            {item.label}
                          </p>
                          <p className="text-[10px] text-slate-400 truncate">{item.desc}</p>
                        </div>
                      </div>
                      <Plus
                        size={14}
                        className="text-slate-400 group-hover:text-blue-600 shrink-0"
                      />
                    </div>
                  )
                })}
              </div>
            </Card>
          </div>

          {/* Lienzo Dropzone (Canvas Ampliado - 9 Cols - Edición Directa Inline) */}
          <div className="lg:col-span-9 space-y-4">
            <Card className="p-5 bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-3xl space-y-4">
              <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-3">
                <span className="text-xs font-extrabold text-slate-900 dark:text-white flex items-center gap-2">
                  <Mail size={16} className="text-blue-600" /> Lienzo del Correo Saliente (
                  {blocks.length} Bloques)
                </span>

                <div className="flex items-center gap-2">
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={handleReset}
                    className="text-xs font-bold cursor-pointer"
                  >
                    <RotateCcw size={13} className="mr-1" /> Reiniciar
                  </Button>
                  <Button
                    type="button"
                    size="sm"
                    onClick={handleSave}
                    className="bg-brand-deep text-white font-bold text-xs cursor-pointer"
                  >
                    <Save size={13} className="mr-1" /> Guardar
                  </Button>
                </div>
              </div>

              {savedSuccess && (
                <div className="p-3 bg-emerald-50 border border-emerald-200 rounded-xl text-emerald-900 dark:bg-emerald-950/40 dark:border-emerald-900 dark:text-emerald-300 text-xs font-bold flex items-center gap-2">
                  <CheckCircle2 size={16} className="text-emerald-600 shrink-0" />
                  <span>
                    Cambios aplicados a la versión en {idiomaLabelDe(selectedIdioma)}. Se mantienen
                    hasta que recargues la página.
                  </span>
                </div>
              )}

              {/* Header Fijo del Correo Marca Blanca */}
              <div className="bg-brand-deep text-white p-4 rounded-2xl text-center space-y-1">
                <p className="text-xs font-extrabold uppercase tracking-widest text-amber-400">
                  Organización Anfitriona · Invitación Oficial
                </p>
                <p className="text-[10px] text-blue-200 font-mono">
                  Comunicación & Convocatorias de Eventos
                </p>
              </div>

              {/* Dropzone Canvas */}
              <div
                onDragOver={(e) => e.preventDefault()}
                onDrop={handleDropCanvas}
                className="min-h-96 space-y-4 rounded-2xl border-2 border-dashed border-slate-200 p-4 bg-slate-50/60 dark:border-slate-800 dark:bg-slate-950/50"
              >
                {blocks.length === 0 && (
                  <div className="p-12 text-center text-slate-400 space-y-2">
                    <Layers size={36} className="mx-auto text-slate-300 dark:text-slate-700" />
                    <p className="font-bold text-xs">El lienzo está vacío</p>
                    <p className="text-[11px]">
                      Arrastra elementos desde la paleta izquierda o haz clic en + para agregarlos
                      aquí
                    </p>
                  </div>
                )}

                {blocks.map((block, idx) => {
                  const isSelected = block.id === selectedBlockId

                  return (
                    <div
                      key={block.id}
                      onClick={() => setSelectedBlockId(block.id)}
                      className={`relative p-4 rounded-2xl border transition-all ${
                        isSelected
                          ? 'border-blue-600 bg-white ring-2 ring-blue-500/20 dark:bg-slate-900 shadow-md'
                          : 'border-slate-200 bg-white hover:border-blue-300 dark:border-slate-800 dark:bg-slate-900'
                      }`}
                    >
                      {/* Controls Toolbar Bar */}
                      <div className="absolute right-2 top-2 flex items-center gap-1 bg-slate-100 dark:bg-slate-800 p-1 rounded-xl border border-slate-200 dark:border-slate-700 z-10">
                        <button
                          type="button"
                          onClick={(e) => {
                            e.stopPropagation()
                            handleMoveBlock(idx, 'up')
                          }}
                          disabled={idx === 0}
                          className="p-1 text-slate-500 hover:text-slate-900 disabled:opacity-30 cursor-pointer"
                        >
                          <ArrowUp size={12} />
                        </button>
                        <button
                          type="button"
                          onClick={(e) => {
                            e.stopPropagation()
                            handleMoveBlock(idx, 'down')
                          }}
                          disabled={idx === blocks.length - 1}
                          className="p-1 text-slate-500 hover:text-slate-900 disabled:opacity-30 cursor-pointer"
                        >
                          <ArrowDown size={12} />
                        </button>
                        <button
                          type="button"
                          onClick={(e) => {
                            e.stopPropagation()
                            handleDeleteBlock(block.id)
                          }}
                          className="p-1 text-rose-600 hover:bg-rose-50 rounded-lg cursor-pointer dark:hover:bg-rose-950"
                        >
                          <Trash2 size={12} />
                        </button>
                      </div>

                      {/* EDICIÓN DIRECTA INLINE EN EL LIENZO */}
                      <div className="space-y-3 pr-24">
                        {block.type === 'heading' && (
                          <div className="space-y-1">
                            <label className="text-[10px] font-extrabold uppercase text-blue-600 tracking-wider">
                              Título de Sección (Inline)
                            </label>
                            <input
                              type="text"
                              value={block.content || ''}
                              onChange={(e) => updateBlockById(block.id, 'content', e.target.value)}
                              className="w-full font-black text-sm text-slate-900 dark:text-white bg-slate-50 dark:bg-slate-800/80 p-2.5 rounded-xl border border-slate-200 dark:border-slate-700 focus:outline-none focus:border-blue-600"
                              placeholder="Escriba el título..."
                            />
                          </div>
                        )}

                        {block.type === 'text' && (
                          <div className="space-y-1">
                            <label className="text-[10px] font-extrabold uppercase text-blue-600 tracking-wider">
                              Cuerpo de Texto (Inline)
                            </label>
                            <textarea
                              rows={3}
                              value={block.content || ''}
                              onChange={(e) => updateBlockById(block.id, 'content', e.target.value)}
                              className="w-full text-xs text-slate-800 dark:text-slate-200 bg-slate-50 dark:bg-slate-800/80 p-2.5 rounded-xl border border-slate-200 dark:border-slate-700 focus:outline-none focus:border-blue-600 leading-relaxed"
                              placeholder="Escriba el mensaje..."
                            />
                          </div>
                        )}

                        {block.type === 'image' && (
                          <div className="space-y-2">
                            <div className="rounded-xl overflow-hidden border border-slate-200 dark:border-slate-800 max-h-40 bg-slate-100">
                              <img
                                src={
                                  block.url ||
                                  'https://images.unsplash.com/photo-1540575467063-178a50c2df87?auto=format&fit=crop&w=1200&q=80'
                                }
                                alt="Imagen"
                                className="w-full h-32 object-cover"
                              />
                            </div>
                            <TextInput
                              value={block.url || ''}
                              onChange={(e) => updateBlockById(block.id, 'url', e.target.value)}
                              placeholder="URL de la imagen (https://...)"
                              className="text-xs font-mono"
                            />
                          </div>
                        )}

                        {block.type === 'button' && (
                          <div className="space-y-2 bg-slate-50 dark:bg-slate-800/60 p-3 rounded-xl border border-slate-200 dark:border-slate-700">
                            <div className="text-center pb-2">
                              <span
                                style={{ backgroundColor: block.color || 'var(--brand-primary)' }}
                                className="inline-block text-white px-5 py-2 rounded-xl font-bold shadow-xs text-xs"
                              >
                                {block.buttonText || 'Botón CTA'}
                              </span>
                            </div>
                            <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                              <TextInput
                                value={block.buttonText || ''}
                                onChange={(e) =>
                                  updateBlockById(block.id, 'buttonText', e.target.value)
                                }
                                placeholder="Texto del Botón"
                                className="text-xs"
                              />
                              <TextInput
                                value={block.buttonUrl || ''}
                                onChange={(e) =>
                                  updateBlockById(block.id, 'buttonUrl', e.target.value)
                                }
                                placeholder="Enlace de Destino (https://...)"
                                className="text-xs font-mono"
                              />
                            </div>
                          </div>
                        )}

                        {block.type === 'columns' && (
                          <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 bg-slate-50 dark:bg-slate-800/60 p-3 rounded-xl border border-slate-200 dark:border-slate-700">
                            <TextInput
                              value={block.col1Text || ''}
                              onChange={(e) =>
                                updateBlockById(block.id, 'col1Text', e.target.value)
                              }
                              placeholder="Columna 1"
                              className="text-xs"
                            />
                            <TextInput
                              value={block.col2Text || ''}
                              onChange={(e) =>
                                updateBlockById(block.id, 'col2Text', e.target.value)
                              }
                              placeholder="Columna 2"
                              className="text-xs"
                            />
                          </div>
                        )}

                        {block.type === 'form' && (
                          <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 bg-blue-50/70 dark:bg-blue-950/40 p-3 rounded-xl border border-blue-200 dark:border-blue-900">
                            <TextInput
                              value={block.formLabel || ''}
                              onChange={(e) =>
                                updateBlockById(block.id, 'formLabel', e.target.value)
                              }
                              placeholder="Pregunta del Formulario"
                              className="text-xs font-bold"
                            />
                            <TextInput
                              value={block.formPlaceholder || ''}
                              onChange={(e) =>
                                updateBlockById(block.id, 'formPlaceholder', e.target.value)
                              }
                              placeholder="Placeholder"
                              className="text-xs"
                            />
                          </div>
                        )}

                        {block.type === 'iframe' && (
                          <div className="space-y-2">
                            <div className="rounded-xl overflow-hidden border border-slate-300 dark:border-slate-800">
                              <iframe
                                src={
                                  safeUrl(block.iframeSrc, true) ||
                                  'https://maps.google.com/maps?q=Punta+Cana&t=&z=13&ie=UTF8&iwloc=&output=embed'
                                }
                                sandbox="allow-scripts allow-same-origin"
                                className="w-full h-28 border-0"
                                title="Mapa o Iframe Embebido"
                              />
                            </div>
                            <TextInput
                              value={block.iframeSrc || ''}
                              onChange={(e) =>
                                updateBlockById(block.id, 'iframeSrc', e.target.value)
                              }
                              placeholder="URL del Mapa / Iframe"
                              className="text-xs font-mono"
                            />
                          </div>
                        )}

                        {block.type === 'divider' && (
                          <hr className="my-2 border-t-2 border-dashed border-slate-300 dark:border-slate-700" />
                        )}

                        {block.type === 'qrcode' && (
                          <div className="p-3 bg-blue-50 dark:bg-blue-950/50 rounded-xl border border-blue-200 dark:border-blue-900 text-center space-y-2">
                            <p className="font-extrabold text-blue-900 dark:text-blue-300">
                              Pase Digital VIP con Código QR
                            </p>
                            <div className="h-20 w-20 mx-auto bg-white p-1.5 rounded-lg border flex items-center justify-center">
                              <QrCode size={60} className="text-slate-900" />
                            </div>
                          </div>
                        )}
                      </div>
                    </div>
                  )
                })}
              </div>

              {/* Footer Fijo Marca Blanca */}
              <div className="bg-slate-100 dark:bg-slate-950 p-4 rounded-2xl text-center text-[10px] text-slate-400">
                <p>© 2026 Organización Anfitriona · Todos los derechos reservados.</p>
              </div>
            </Card>
          </div>
        </div>
      )}

      {/* =========================================================
          MODO 2: EDICIÓN DE TEXTO PLANO & ASUNTO
         ========================================================= */}
      {viewMode === 'edicion' && (
        <Card className="p-6 bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 rounded-3xl space-y-5">
          <div className="flex items-center justify-between border-b border-slate-100 pb-3 dark:border-slate-800">
            <h2 className="font-sans text-base font-extrabold text-slate-900 dark:text-white flex items-center gap-2">
              <Edit3 size={18} className="text-blue-600" /> Edición de Textos & Asunto (
              {selectedIdioma})
            </h2>

            <div className="flex items-center gap-2">
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={handleReset}
                className="cursor-pointer"
              >
                <RotateCcw size={14} className="mr-1" /> Restablecer por Defecto
              </Button>
              <Button
                type="button"
                size="sm"
                onClick={handleSave}
                className="bg-brand-deep text-white hover:bg-brand-deep/90 font-bold cursor-pointer"
              >
                <Save size={14} className="mr-1" /> Guardar Plantilla
              </Button>
            </div>
          </div>

          {savedSuccess && (
            <div className="p-3 bg-emerald-50 border border-emerald-200 rounded-xl text-emerald-900 dark:bg-emerald-950/40 dark:border-emerald-900 dark:text-emerald-300 text-xs font-bold flex items-center gap-2">
              <CheckCircle2 size={16} className="text-emerald-600 shrink-0" />
              <span>
                Cambios aplicados a la versión en {idiomaLabelDe(selectedIdioma)}. Se mantienen
                hasta que recargues la página.
              </span>
            </div>
          )}

          {/* Campo Asunto */}
          <div>
            <label className="block text-xs font-bold text-slate-800 dark:text-slate-200 mb-1">
              Asunto del Correo Saliente
            </label>
            <TextInput
              value={asunto}
              onChange={(e) => setAsunto(e.target.value)}
              placeholder="Asunto con variables {{evento}}..."
              className="font-semibold text-xs"
            />
          </div>

          {/* Insertar Variables Rápida */}
          <div className="p-3 bg-slate-50 dark:bg-slate-800/60 rounded-2xl border border-slate-200 dark:border-slate-700 space-y-2">
            <div className="flex items-center gap-2 text-xs font-bold text-slate-800 dark:text-slate-200">
              <Sparkles size={14} className="text-amber-500" /> Insertar Variables Dinámicas
            </div>
            <div className="flex flex-wrap gap-2 text-[11px]">
              <button
                type="button"
                onClick={() => insertVariable('{{nombre}}')}
                className="px-2.5 py-1 bg-white border border-slate-300 rounded-lg hover:border-blue-500 font-mono cursor-pointer dark:bg-slate-900 dark:border-slate-700"
              >
                + &#123;&#123;nombre&#125;&#125;
              </button>
              <button
                type="button"
                onClick={() => insertVariable('{{empresa}}')}
                className="px-2.5 py-1 bg-white border border-slate-300 rounded-lg hover:border-blue-500 font-mono cursor-pointer dark:bg-slate-900 dark:border-slate-700"
              >
                + &#123;&#123;empresa&#125;&#125;
              </button>
              <button
                type="button"
                onClick={() => insertVariable('{{evento}}')}
                className="px-2.5 py-1 bg-white border border-slate-300 rounded-lg hover:border-blue-500 font-mono cursor-pointer dark:bg-slate-900 dark:border-slate-700"
              >
                + &#123;&#123;evento&#125;&#125;
              </button>
              <button
                type="button"
                onClick={() => insertVariable('{{fecha}}')}
                className="px-2.5 py-1 bg-white border border-slate-300 rounded-lg hover:border-blue-500 font-mono cursor-pointer dark:bg-slate-900 dark:border-slate-700"
              >
                + &#123;&#123;fecha&#125;&#125;
              </button>
              <button
                type="button"
                onClick={() => insertVariable('{{checkin_url}}')}
                className="px-2.5 py-1 bg-white border border-slate-300 rounded-lg hover:border-blue-500 font-mono cursor-pointer dark:bg-slate-900 dark:border-slate-700"
              >
                + &#123;&#123;checkin_url&#125;&#125;
              </button>
            </div>
          </div>

          {/* Cuerpo del Mensaje */}
          <div>
            <label className="block text-xs font-bold text-slate-800 dark:text-slate-200 mb-1">
              Cuerpo del Mensaje (Soporta formateo con saltos de línea)
            </label>
            <textarea
              rows={10}
              value={cuerpo}
              onChange={(e) => setCuerpo(e.target.value)}
              className="w-full rounded-2xl border border-slate-200 bg-white p-4 font-mono text-xs leading-relaxed focus:border-blue-600 focus:outline-none dark:border-slate-700 dark:bg-slate-900 dark:text-white"
            />
          </div>

          {/* Botón CTA Texto */}
          <div>
            <label className="block text-xs font-bold text-slate-800 dark:text-slate-200 mb-1">
              Texto del Botón de Acción Principal (CTA)
            </label>
            <TextInput
              value={ctaTexto}
              onChange={(e) => setCtaTexto(e.target.value)}
              placeholder="Ej: Confirmar Asistencia / Ver Registro"
              className="font-semibold text-xs"
            />
          </div>
        </Card>
      )}

      {/* =========================================================
          MODO 3: VISTA PREVIA HTML / CLIENTE DE CORREO INSTITUCIONAL
         ========================================================= */}
      {viewMode === 'preview' && (
        <Card className="p-0 bg-background dark:bg-slate-950 border border-slate-300 dark:border-slate-800 rounded-3xl overflow-hidden shadow-md">
          {/* Cabecera del Cliente de Correo Simulado (Gmail / Outlook style) */}
          <div className="bg-slate-800 text-white px-6 py-3 flex items-center justify-between border-b border-slate-700">
            <div className="flex items-center gap-3 text-xs">
              <Mail size={16} className="text-blue-400" />
              <span className="font-bold">Previsualización de Cliente de Correo</span>
              <span className="px-2 py-0.5 rounded-full bg-blue-600 text-white text-[10px] font-extrabold uppercase">
                {selectedIdioma}
              </span>
            </div>
            <span className="text-[11px] text-slate-400 font-mono">
              Diseño Institucional Responsive
            </span>
          </div>

          {/* Metadatos de Cabecera del Correo */}
          <div className="bg-white dark:bg-slate-900 p-6 border-b border-slate-200 dark:border-slate-800 space-y-2 text-xs">
            <p className="text-slate-600 dark:text-slate-400">
              <strong className="text-slate-900 dark:text-white">De:</strong> Organización
              Anfitriona &lt;convocatorias@empresa.com&gt;
            </p>
            <p className="text-slate-600 dark:text-slate-400">
              <strong className="text-slate-900 dark:text-white">Para:</strong> Carlos Mendoza
              &lt;carlos.mendoza@empresa.com&gt;
            </p>
            <p className="text-slate-800 dark:text-slate-200 font-bold text-sm pt-1">
              Asunto: {renderInterpolatedText(asunto)}
            </p>
          </div>

          {/* Plantilla de Correo HTML Institucional Renderizada */}
          <div className="p-4 sm:p-8 flex justify-center">
            <div className="w-full max-w-xl bg-white dark:bg-slate-900 rounded-2xl shadow-xl overflow-hidden border border-slate-200 dark:border-slate-800">
              {/* Top Banner Marca Blanca */}
              <div className="bg-brand-deep p-6 text-white text-center border-b-4 border-amber-500">
                <img
                  src="/logo-brand-dark.svg"
                  alt="Logo Plataforma"
                  className="h-10 w-auto mx-auto object-contain"
                />
                <p className="text-[11px] font-extrabold uppercase tracking-widest text-amber-400 mt-2">
                  Organización Anfitriona · Evento Corporativo
                </p>
              </div>

              {/* Contenido Renderizado desde Bloques del Constructor */}
              <div className="p-6 space-y-5 text-slate-800 dark:text-slate-200 leading-relaxed text-xs">
                {blocks.map((block) => (
                  <div key={block.id}>
                    {block.type === 'heading' && (
                      <h3 className="text-base font-extrabold text-slate-900 dark:text-white">
                        {renderInterpolatedText(block.content || '')}
                      </h3>
                    )}

                    {block.type === 'text' && (
                      <div className="whitespace-pre-line text-xs">
                        {renderInterpolatedText(block.content || '')}
                      </div>
                    )}

                    {block.type === 'image' && (
                      <div className="rounded-xl overflow-hidden border border-slate-200 dark:border-slate-800">
                        <img
                          src={
                            block.url ||
                            'https://images.unsplash.com/photo-1540575467063-178a50c2df87?auto=format&fit=crop&w=1200&q=80'
                          }
                          alt={block.alt || 'Banner del correo'}
                          className="w-full h-44 object-cover"
                        />
                      </div>
                    )}

                    {block.type === 'button' && (
                      <div className="text-center pt-2">
                        <a
                          href={safeUrl(block.buttonUrl) ?? demoData.checkin_url}
                          target="_blank"
                          rel="noopener noreferrer"
                          style={{ backgroundColor: block.color || 'var(--brand-primary)' }}
                          className="inline-flex items-center gap-2 text-white px-6 py-3 rounded-xl font-extrabold text-xs shadow-md transition-all"
                        >
                          {block.buttonText || ctaTexto} <ExternalLink size={14} />
                        </a>
                      </div>
                    )}

                    {block.type === 'columns' && (
                      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 p-4 bg-slate-50 dark:bg-slate-800/80 rounded-xl border border-slate-200 dark:border-slate-700">
                        <div className="font-semibold">
                          {renderInterpolatedText(block.col1Text || '')}
                        </div>
                        <div className="font-semibold">
                          {renderInterpolatedText(block.col2Text || '')}
                        </div>
                      </div>
                    )}

                    {block.type === 'form' && (
                      <div className="p-4 bg-blue-50/80 dark:bg-blue-950/50 rounded-xl border border-blue-200 dark:border-blue-900 space-y-2">
                        <label className="font-extrabold text-blue-900 dark:text-blue-300">
                          {block.formLabel || 'Campo de Pregunta'}
                        </label>
                        <input
                          type="text"
                          disabled
                          placeholder={block.formPlaceholder || 'Respuesta...'}
                          className="w-full rounded-lg border border-slate-300 bg-white p-2.5 text-xs dark:border-slate-700 dark:bg-slate-900"
                        />
                      </div>
                    )}

                    {block.type === 'iframe' && (
                      <div className="rounded-xl overflow-hidden border border-slate-300 dark:border-slate-800 shadow-2xs">
                        <iframe
                          src={
                            safeUrl(block.iframeSrc, true) ||
                            'https://maps.google.com/maps?q=Punta+Cana&t=&z=13&ie=UTF8&iwloc=&output=embed'
                          }
                          sandbox="allow-scripts allow-same-origin"
                          className="w-full h-40 border-0"
                          title="Mapa embebido"
                        />
                      </div>
                    )}

                    {block.type === 'divider' && (
                      <hr className="my-3 border-t-2 border-dashed border-slate-300 dark:border-slate-700" />
                    )}

                    {block.type === 'qrcode' && (
                      <div className="p-4 bg-blue-50/80 dark:bg-blue-950/50 rounded-xl border border-blue-200 dark:border-blue-900 text-center space-y-3">
                        <div className="inline-flex items-center gap-1.5 text-xs font-extrabold text-blue-900 dark:text-blue-300">
                          <QrCode size={16} className="text-blue-600" /> Pase Digital VIP & Código
                          QR
                        </div>
                        <div className="h-28 w-28 mx-auto bg-white p-2 rounded-xl shadow-xs border border-slate-200 flex items-center justify-center">
                          <QrCode size={90} className="text-slate-900" />
                        </div>
                      </div>
                    )}
                  </div>
                ))}
              </div>

              {/* Pie de Página Institucional con Redes Sociales y Desuscripción */}
              <div className="bg-brand-deep text-slate-300 p-6 text-center text-xs border-t-4 border-amber-500 space-y-4">
                {/* Redes Sociales Institucionales */}
                <div className="flex items-center justify-center gap-4 pb-3 border-b border-white/10">
                  <span className="text-[11px] font-extrabold uppercase tracking-wider text-amber-400">
                    {selectedIdioma === 'EN'
                      ? 'Follow us:'
                      : selectedIdioma === 'FR'
                        ? 'Suivez-nous:'
                        : selectedIdioma === 'PT'
                          ? 'Siga-nos:'
                          : selectedIdioma === 'DE'
                            ? 'Folgen Sie uns:'
                            : selectedIdioma === 'IT'
                              ? 'Seguici:'
                              : 'Síguenos:'}
                  </span>
                  <a
                    href="https://instagram.com/godominicanrepublic"
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-slate-200 hover:text-amber-400 font-bold transition-colors"
                  >
                    Instagram
                  </a>
                  <span className="text-slate-600">·</span>
                  <a
                    href="https://facebook.com/godominicanrepublic"
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-slate-200 hover:text-amber-400 font-bold transition-colors"
                  >
                    Facebook
                  </a>
                  <span className="text-slate-600">·</span>
                  <a
                    href="https://twitter.com/GoDomRep"
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-slate-200 hover:text-amber-400 font-bold transition-colors"
                  >
                    X (Twitter)
                  </a>
                  <span className="text-slate-600">·</span>
                  <a
                    href="https://youtube.com/godominicanrepublic"
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-slate-200 hover:text-amber-400 font-bold transition-colors"
                  >
                    YouTube
                  </a>
                </div>

                {/* Contacto del evento */}
                <div className="rounded-xl bg-white/5 p-4 border border-white/10 text-center space-y-1.5">
                  <span className="block text-[10px] font-extrabold uppercase tracking-widest text-amber-400">
                    {selectedIdioma === 'EN' ? 'Event contact' : 'Contacto del evento'}
                  </span>
                  <p className="font-extrabold text-white text-xs">
                    Coordinación de Eventos Corporativos
                  </p>
                  <div className="flex flex-wrap items-center justify-center gap-x-3 gap-y-1 text-[11px] text-slate-300">
                    <span>
                      <strong>Contacto:</strong> Comité Organizador
                    </span>
                    <span className="text-slate-500">|</span>
                    <span>
                      <strong>Email:</strong> eventos@empresa.com
                    </span>
                    <span className="text-slate-500">|</span>
                    <span>
                      <strong>Teléfono:</strong> +1 (305) 448-5600
                    </span>
                  </div>
                </div>

                {/* Unsubscribe Traducido e Informativo Legal */}
                <div className="pt-2 text-[10px] text-slate-400 flex items-center justify-center gap-3 border-t border-white/10">
                  <a
                    href="https://godominicanrepublic.com/privacy-policy"
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-slate-400 hover:text-white underline"
                  >
                    {selectedIdioma === 'EN'
                      ? 'Privacy Policy'
                      : selectedIdioma === 'FR'
                        ? 'Politique de Confidentialité'
                        : selectedIdioma === 'PT'
                          ? 'Política de Privacidade'
                          : selectedIdioma === 'DE'
                            ? 'Datenschutzbestimmungen'
                            : selectedIdioma === 'IT'
                              ? 'Informativa sulla Privacy'
                              : 'Política de Privacidad'}
                  </a>
                  <span>·</span>
                  <button
                    type="button"
                    onClick={() => toast.success('Solicitud de desuscripción registrada.')}
                    className="text-amber-400 hover:underline font-bold cursor-pointer"
                  >
                    {selectedIdioma === 'EN'
                      ? 'Unsubscribe'
                      : selectedIdioma === 'FR'
                        ? 'Se désabonner'
                        : selectedIdioma === 'PT'
                          ? 'Cancelar inscrição'
                          : selectedIdioma === 'DE'
                            ? 'Abmelden'
                            : selectedIdioma === 'IT'
                              ? 'Disiscriviti'
                              : 'Cancelar suscripción'}
                  </button>
                </div>
              </div>
            </div>
          </div>
        </Card>
      )}

      {/* Modal Crear Nueva Plantilla Personalizada */}
      {showNewModal && (
        <Modal title="Nueva plantilla" onClose={() => setShowNewModal(false)}>
          <form onSubmit={handleCreateNewTemplate} className="space-y-4 text-xs">
            <Field label="Nombre de la plantilla" required>
              <TextInput
                value={newNombreTpl}
                onChange={(e) => setNewNombreTpl(e.target.value)}
                placeholder="Ej: Invitación Fiesta de Navidad 2026"
                required
              />
            </Field>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <Field
                label="Partir del texto de"
                hint="La plantilla nueva empieza con este texto y luego lo cambias."
              >
                <Select
                  value={selectedTipo}
                  onChange={(e) => setSelectedTipo(e.target.value as CampanaTipo)}
                >
                  {TIPOS_CAMPANA_BASE.map((t) => (
                    <option key={t.key} value={t.key}>
                      {t.label}
                    </option>
                  ))}
                </Select>
              </Field>

              <Field label="Idioma inicial">
                <Select value={selectedIdioma} onChange={(e) => setSelectedIdioma(e.target.value)}>
                  {IDIOMAS.map((i) => (
                    <option key={i.key} value={i.key}>
                      {i.label}
                    </option>
                  ))}
                </Select>
              </Field>
            </div>

            <div className="flex justify-end gap-2 border-t border-slate-100 pt-3 dark:border-slate-800">
              <Button variant="secondary" type="button" onClick={() => setShowNewModal(false)}>
                Cancelar
              </Button>
              <Button type="submit" className="bg-brand-deep text-white font-bold">
                Crear Plantilla
              </Button>
            </div>
          </form>
        </Modal>
      )}
      {/* Modal Vista Previa del Cuerpo de la Plantilla (Renderizado Exacto de Correo Electrónico) */}
      {selectedDetailTemplate && (
        <Modal
          title={`Previsualización Oficial de Correo — ${selectedDetailTemplate.label}`}
          onClose={() => setSelectedDetailTemplate(null)}
          wide
        >
          <div className="space-y-4 text-xs">
            {/* Contenedor del Cliente de Correo Simulado */}
            <div className="overflow-hidden rounded-2xl border border-slate-200 shadow-xl dark:border-slate-800 bg-background dark:bg-slate-950">
              {/* Cabecera del Cliente de Correo Simulado */}
              <div className="bg-slate-800 text-white px-5 py-2.5 flex items-center justify-between">
                <div className="flex items-center gap-2.5">
                  <Mail size={15} className="text-blue-400" />
                  <span className="font-bold text-xs">Cliente de Correo (Vista Previa)</span>
                  <span className="px-2 py-0.5 rounded-full bg-blue-600 text-white text-[9.5px] font-extrabold uppercase">
                    {selectedIdioma}
                  </span>
                </div>
                <span className="text-[10.5px] text-slate-400 font-mono">
                  Render Real de Salida
                </span>
              </div>

              {/* Encabezado con datos De / Para / Asunto / Fecha */}
              <div className="bg-white dark:bg-slate-900 p-5 border-b border-slate-200 dark:border-slate-800 space-y-1.5 text-xs">
                <div className="flex items-center justify-between text-slate-500">
                  <p>
                    <strong className="text-slate-900 dark:text-white">De:</strong> Organización
                    Anfitriona &lt;convocatorias@empresa.com&gt;
                  </p>
                  <span className="text-[11px] text-slate-400 font-medium">Hoy, 10:30 a. m.</span>
                </div>
                <p className="text-slate-600 dark:text-slate-400">
                  <strong className="text-slate-900 dark:text-white">Para:</strong> Carlos Mendoza
                  &lt;carlos.mendoza@empresa.com&gt;
                </p>
                <div className="pt-1.5 flex items-center gap-2">
                  <span className="font-bold text-slate-900 dark:text-white text-sm">Asunto:</span>
                  <span className="font-extrabold text-blue-900 dark:text-blue-300 text-sm">
                    {renderInterpolatedText(selectedDetailTemplate.tpl.asunto)}
                  </span>
                </div>
              </div>

              {/* Cuerpo del Correo HTML Exacto */}
              <div className="p-4 sm:p-6 flex justify-center">
                <div className="w-full max-w-xl bg-white dark:bg-slate-900 rounded-2xl shadow-md overflow-hidden border border-slate-200 dark:border-slate-800">
                  {/* Header Banner Marca Blanca */}
                  <div className="bg-brand-deep p-6 text-white text-center border-b-4 border-amber-500">
                    <img
                      src="/logo-brand-dark.svg"
                      alt="Logo Plataforma"
                      className="h-9 w-auto mx-auto object-contain"
                    />
                    <p className="text-[10.5px] font-extrabold uppercase tracking-widest text-amber-400 mt-2">
                      Organización Anfitriona · Evento Corporativo
                    </p>
                  </div>

                  {/* Mensaje Interpolado */}
                  <div className="p-6 space-y-5 text-slate-800 dark:text-slate-200 leading-relaxed text-xs">
                    <div className="whitespace-pre-line text-xs font-sans">
                      {renderInterpolatedText(selectedDetailTemplate.tpl.cuerpo) ||
                        'Esta plantilla todavía no tiene un cuerpo configurado.'}
                    </div>

                    {/* Botón de Acción Principal (CTA) */}
                    <div className="text-center pt-2">
                      <a
                        href={demoData.checkin_url}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="inline-flex items-center gap-2 text-white bg-brand-deep hover:bg-brand-deep px-6 py-3 rounded-xl font-extrabold text-xs shadow-md transition-all cursor-pointer"
                      >
                        Confirmar Asistencia / Ver Pase Digital <ExternalLink size={14} />
                      </a>
                    </div>

                    {/* Pase Digital QR Simulado */}
                    <div className="p-4 bg-blue-50/80 dark:bg-blue-950/50 rounded-2xl border border-blue-200 dark:border-blue-900 text-center space-y-2">
                      <div className="inline-flex items-center gap-1.5 text-xs font-extrabold text-blue-900 dark:text-blue-300">
                        <QrCode size={16} className="text-blue-600" /> Acreditación Oficial & Pase
                        de Entrada
                      </div>
                      <div className="h-24 w-24 mx-auto bg-white p-2 rounded-xl shadow-xs border border-slate-200 flex items-center justify-center">
                        <QrCode size={80} className="text-slate-900" />
                      </div>
                      <p className="text-[10px] text-slate-500 font-mono">
                        ID: REF-{selectedDetailTemplate.key.toUpperCase().replace(/\s+/g, '-')}-2026
                      </p>
                    </div>
                  </div>

                  {/* Footer Institucional con Redes y Desuscripción */}
                  <div className="bg-brand-deep text-slate-300 p-5 text-center text-xs border-t-4 border-amber-500 space-y-3.5">
                    {/* Redes Sociales */}
                    <div className="flex items-center justify-center gap-3.5 pb-2.5 border-b border-white/10">
                      <span className="text-[10px] font-extrabold uppercase tracking-wider text-amber-400">
                        {selectedIdioma === 'EN'
                          ? 'Follow us:'
                          : selectedIdioma === 'FR'
                            ? 'Suivez-nous:'
                            : selectedIdioma === 'PT'
                              ? 'Siga-nos:'
                              : selectedIdioma === 'DE'
                                ? 'Folgen Sie uns:'
                                : selectedIdioma === 'IT'
                                  ? 'Seguici:'
                                  : 'Síguenos:'}
                      </span>
                      <a
                        href="https://instagram.com/godominicanrepublic"
                        target="_blank"
                        rel="noopener noreferrer"
                        className="text-slate-200 hover:text-amber-400 font-bold transition-colors"
                      >
                        Instagram
                      </a>
                      <span className="text-slate-600">·</span>
                      <a
                        href="https://facebook.com/godominicanrepublic"
                        target="_blank"
                        rel="noopener noreferrer"
                        className="text-slate-200 hover:text-amber-400 font-bold transition-colors"
                      >
                        Facebook
                      </a>
                      <span className="text-slate-600">·</span>
                      <a
                        href="https://twitter.com/GoDomRep"
                        target="_blank"
                        rel="noopener noreferrer"
                        className="text-slate-200 hover:text-amber-400 font-bold transition-colors"
                      >
                        X (Twitter)
                      </a>
                      <span className="text-slate-600">·</span>
                      <a
                        href="https://youtube.com/godominicanrepublic"
                        target="_blank"
                        rel="noopener noreferrer"
                        className="text-slate-200 hover:text-amber-400 font-bold transition-colors"
                      >
                        YouTube
                      </a>
                    </div>

                    {/* Contacto del evento */}
                    <div className="rounded-xl bg-white/5 p-3.5 border border-white/10 text-center space-y-1">
                      <span className="block text-[9.5px] font-extrabold uppercase tracking-widest text-amber-400">
                        {selectedIdioma === 'EN' ? 'Event contact' : 'Contacto del evento'}
                      </span>
                      <p className="font-extrabold text-white text-xs">
                        Coordinación de Eventos Corporativos
                      </p>
                      <div className="flex flex-wrap items-center justify-center gap-x-3 gap-y-1 text-[10.5px] text-slate-300">
                        <span>
                          <strong>Contacto:</strong> Comité Organizador
                        </span>
                        <span className="text-slate-500">|</span>
                        <span>
                          <strong>Email:</strong> eventos@empresa.com
                        </span>
                        <span className="text-slate-500">|</span>
                        <span>
                          <strong>Teléfono:</strong> +1 (305) 448-5600
                        </span>
                      </div>
                    </div>

                    {/* Unsubscribe Traducido e Informativo Legal */}
                    <div className="pt-1.5 text-[9.5px] text-slate-400 flex items-center justify-center gap-3 border-t border-white/10">
                      <a
                        href="https://godominicanrepublic.com/privacy-policy"
                        target="_blank"
                        rel="noopener noreferrer"
                        className="text-slate-400 hover:text-white underline"
                      >
                        {selectedIdioma === 'EN'
                          ? 'Privacy Policy'
                          : selectedIdioma === 'FR'
                            ? 'Politique de Confidentialité'
                            : selectedIdioma === 'PT'
                              ? 'Política de Privacidade'
                              : selectedIdioma === 'DE'
                                ? 'Datenschutzbestimmungen'
                                : selectedIdioma === 'IT'
                                  ? 'Informativa sulla Privacy'
                                  : 'Política de Privacidad'}
                      </a>
                      <span>·</span>
                      <button
                        type="button"
                        onClick={() => toast.success('Solicitud de desuscripción registrada.')}
                        className="text-amber-400 hover:underline font-bold cursor-pointer"
                      >
                        {selectedIdioma === 'EN'
                          ? 'Unsubscribe'
                          : selectedIdioma === 'FR'
                            ? 'Se désabonner'
                            : selectedIdioma === 'PT'
                              ? 'Cancelar inscrição'
                              : selectedIdioma === 'DE'
                                ? 'Abmelden'
                                : selectedIdioma === 'IT'
                                  ? 'Disiscriviti'
                                  : 'Cancelar suscripción'}
                      </button>
                    </div>
                  </div>
                </div>
              </div>
            </div>

            <div className="flex justify-end gap-2 border-t border-slate-100 pt-3 dark:border-slate-800">
              <Button
                variant="secondary"
                onClick={() => setSelectedDetailTemplate(null)}
                className="cursor-pointer"
              >
                Cerrar
              </Button>
              <Button
                onClick={() => {
                  const item = tiposCampana.find((t) => t.key === selectedDetailTemplate.key)
                  setSelectedDetailTemplate(null)
                  if (item) editar(item, catalogoIdioma)
                }}
                className="bg-brand-deep text-white font-bold cursor-pointer"
              >
                <Edit3 size={14} className="mr-1.5" /> Editar esta plantilla
              </Button>
            </div>
          </div>
        </Modal>
      )}

      {/* Modal Confirmar Eliminación de Plantilla */}
      {deleteTarget && (
        <Modal title="Eliminar plantilla" onClose={() => setDeleteTarget(null)}>
          <div className="space-y-4 text-xs">
            <p className="text-slate-600 dark:text-slate-300">
              La plantilla{' '}
              <strong className="text-slate-900 dark:text-white">{deleteTarget.label}</strong> se
              quitará del catálogo.
            </p>
            <div className="flex justify-end gap-2 border-t border-slate-100 pt-3 dark:border-slate-800">
              <Button variant="secondary" onClick={() => setDeleteTarget(null)}>
                Cancelar
              </Button>
              <Button variant="destructive" onClick={handleConfirmDelete}>
                Eliminar
              </Button>
            </div>
          </div>
        </Modal>
      )}
    </div>
  )
}
