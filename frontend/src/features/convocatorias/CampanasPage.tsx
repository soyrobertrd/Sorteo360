import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { Link } from 'react-router-dom'
import {
  CalendarDays,
  Check,
  type LucideIcon,
  Download,
  SlidersHorizontal,
  Languages,
  MapPin,
  Plus,
  Upload,
  FileCheck,
  Building2,
  ArrowRight,
  ArrowLeft,
  Eye,
  FileText,
  ShieldCheck,
  AlertTriangle,
  XCircle,
  Copy,
  UserX,
  MailX,
  CheckCircle2,
  ChevronDown,
  ChevronUp,
  Loader2,
  MoreVertical,
} from 'lucide-react'
import * as XLSX from 'xlsx'
import { Card } from '@/components/ui/Card'
import { Breadcrumbs } from '@/components/ui/Breadcrumbs'
import { Button } from '@/components/ui/Button'
import { ActionMenu } from '@/components/ui/ActionMenu'
import { IconButton } from '@/components/ui/IconButton'
import { MenuItem } from '@/components/ui/MenuItem'
import { inputClass, Field, Select, TextInput } from '@/components/ui/Field'
import { Modal } from '@/components/ui/Modal'
import { EmptyState, Spinner, ErrorNote, WarningNote } from '@/components/ui/Feedback'
import { DataTable } from '@/components/ui/DataTable'
import { ViewModeToggle, type ViewMode } from '@/components/ui/ViewModeToggle'
import {
  CardGrid,
  GridCard,
  GridCardCta,
  GridCardStats,
  GridCardTag,
} from '@/components/ui/GridCard'
import { ExportButtons } from '@/components/ui/ExportButtons'
import { useCan } from '@/auth/AuthProvider'
import { ROLES } from '@/auth/roles'
import { useEvento, useEventosList } from '@/features/eventos/hooks'
import { formatDate, formatDateRange } from '@/lib/format'
import { Callout } from '@/components/ui/Callout'
import { Stepper } from '@/components/ui/Stepper'
import type { ReportColumn } from '@/lib/exportUtils'
import type { CampaignState, ContactResponse } from '@/api/types'
import { config } from '@/lib/config'
import { campaignTemplateLabel } from '@/lib/labels'
import {
  CAMPANA_TIPOS,
  CAMPANA_IDIOMAS,
  type CampanaTipo,
  type CampanaIdioma,
} from './mockParticipants'
import { useCampanasTabla, useCampaignWizard, useTemplates, type CampanaFila } from './campanas'
import { Pagination } from '@/components/ui/Pagination'
import {
  codigoDeClasificacion,
  useContactClassifications,
  useContactList,
  useContactLists,
  useContacts,
} from './destinatarios'
import { PlantillaEditorModal } from './components/PlantillaEditorModal'
import { PLANTILLAS_MULTI_IDIOMA, PLANTILLAS_DEFAULT } from './plantillasCorreo'
import { CampanaDetalleModal } from './components/CampanaDetalleModal'
import { UploadReconciliationModal } from './components/UploadReconciliationModal'
import { CampanaApprovalModal } from './components/CampanaApprovalModal'
import {
  PublicFormBuilderModal,
  type FormularioConvocatoriaConfig,
} from './components/PublicFormBuilderModal'
import { descargarBlob } from '@/lib/download'

// eslint-disable-next-line react-refresh/only-export-components
export const CAMPAIGN_STATE_CLASS: Record<CampaignState, string> = {
  draft: 'bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300',
  sending: 'bg-amber-100 text-amber-900 dark:bg-amber-950 dark:text-amber-300',
  sent: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300',
  cancelled: 'bg-rose-100 text-rose-800 dark:bg-rose-950 dark:text-rose-300',
}

const isMock = config.apiMode !== 'http'

// Contactos MOCK del CRM para selección de audiencia, solo en modo mock: en http la lista sale de
// crm-service (ciudad, país y agente no vienen del servicio y quedan vacíos).
const MOCK_CRM_CONTACTOS: CrmContactoFila[] = [
  {
    id: 'c1',
    nombre: 'María González',
    empresa: 'TUI Group Europe',
    companyCategory: 'TourOperator',
    email: 'maria.gonzalez@tui.com',
    clasificacion: 'Tour Operador',
    status: 'SIN_REGISTRAR',
    ciudad: 'Madrid',
    pais: 'España',
    agenteAsignado: 'Ana Coordinadora',
    canBeEmailed: true,
    suppressionReason: null,
  },
  {
    id: 'c2',
    nombre: 'Carlos Rodríguez',
    empresa: 'Expedia Group',
    companyCategory: 'TravelAgency',
    email: 'crodriguez@expedia.com',
    clasificacion: 'Agencia de viajes',
    status: 'REGISTRADOS',
    ciudad: 'Miami',
    pais: 'Estados Unidos',
    agenteAsignado: 'Lic. Roberto Guzmán',
    canBeEmailed: true,
    suppressionReason: null,
  },
  {
    id: 'c3',
    nombre: 'Ana Martínez',
    empresa: 'Iberia Líneas Aéreas',
    companyCategory: 'Airline',
    email: 'amartinez@iberia.com',
    clasificacion: 'Aerolínea',
    status: 'PENDIENTE_CHECKIN',
    ciudad: 'Madrid',
    pais: 'España',
    agenteAsignado: 'Ana Coordinadora',
    canBeEmailed: false,
    suppressionReason: 'opted_out' as const,
  },
  {
    id: 'c4',
    nombre: 'Luis López',
    empresa: 'Travel Weekly USA',
    companyCategory: 'Press',
    email: 'llopez@travelweekly.com',
    clasificacion: 'Prensa',
    status: 'SIN_REGISTRAR',
    ciudad: 'Nueva York',
    pais: 'Estados Unidos',
    agenteAsignado: 'Oficial Carlos Gómez',
    canBeEmailed: true,
    suppressionReason: null,
  },
  {
    id: 'c5',
    nombre: 'Sofía Fernández',
    empresa: 'Booking.com',
    companyCategory: 'OTAAgent',
    email: 'sfernandez@booking.com',
    clasificacion: 'Aliado',
    status: 'CHECKED_IN',
    ciudad: 'Ámsterdam',
    pais: 'Países Bajos',
    agenteAsignado: 'Ana Coordinadora',
    canBeEmailed: false,
    suppressionReason: 'do_not_invite' as const,
  },
  {
    id: 'c6',
    nombre: 'Miguel Sánchez',
    empresa: 'Meliá Hotels International',
    companyCategory: 'HotelChain',
    email: 'msanchez@melia.com',
    clasificacion: 'Cadena Hotelera',
    status: 'REGISTRADOS',
    ciudad: 'Toronto',
    pais: 'Canadá',
    agenteAsignado: 'Lic. Alicia Directora',
    canBeEmailed: true,
    suppressionReason: null,
  },
  {
    id: 'c7',
    nombre: 'Jean-Pierre Dubois',
    empresa: 'Club Med',
    companyCategory: 'TourOperator',
    email: 'jp.dubois@clubmed.com',
    clasificacion: 'Tour Operador',
    status: 'SIN_REGISTRAR',
    ciudad: 'Toronto',
    pais: 'Canadá',
    agenteAsignado: 'Lic. Alicia Directora',
    canBeEmailed: true,
    suppressionReason: null,
  },
  {
    id: 'c8',
    nombre: 'Laura Martins',
    empresa: 'TAP Air Portugal',
    companyCategory: 'Airline',
    email: '',
    clasificacion: 'Aerolínea',
    status: 'REGISTRADOS',
    ciudad: 'Miami',
    pais: 'Estados Unidos',
    agenteAsignado: 'Lic. Roberto Guzmán',
    canBeEmailed: false,
    suppressionReason: 'no_email' as const,
  },
  {
    id: 'c9',
    nombre: 'Andrea Rossi',
    empresa: 'Alpitour World',
    email: 'arossi@alpitour.it',
    clasificacion: 'Tour Operador',
    status: 'PENDIENTE_CHECKIN',
    ciudad: 'Nueva York',
    pais: 'Estados Unidos',
    agenteAsignado: 'Oficial Carlos Gómez',
  },
  {
    id: 'c10',
    nombre: 'Karen Wilson',
    empresa: 'Virtuoso',
    email: 'kwilson@virtuoso.com',
    clasificacion: 'Agencia de viajes',
    status: 'SIN_REGISTRAR',
    ciudad: 'Madrid',
    pais: 'España',
    agenteAsignado: 'Ana Coordinadora',
  },
]

const CLASIFICACION_LABEL: Record<string, string> = {
  'Tour Operador': 'Tour Operadores',
  'Agencia de viajes': 'Agencias de Viajes',
  Aerolínea: 'Aerolíneas',
  Prensa: 'Prensa',
  Aliado: 'Aliados',
  'Cadena Hotelera': 'Cadenas Hoteleras',
}

type CrmContactoFila = {
  id: string
  nombre: string
  empresa: string
  companyCategory?: string
  email: string
  clasificacion: string
  status: string
  ciudad: string
  pais: string
  agenteAsignado: string
  canBeEmailed?: boolean
  suppressionReason?: string | null
}

type CrmFiltros = {
  clasificacion: string
  ciudad: string
  pais: string
  agente: string
}

/** Contactos que cumplen todos los filtros salvo el indicado en `exclude` — así cada selector muestra solo lo que realmente puede combinarse con los demás filtros activos (filtros incluyentes/en cascada). */
function crmContactosPara(
  contactos: CrmContactoFila[],
  filtros: CrmFiltros,
  exclude?: keyof CrmFiltros,
) {
  return contactos.filter((c) => {
    const matchClas =
      exclude === 'clasificacion' ||
      !filtros.clasificacion ||
      c.clasificacion === filtros.clasificacion
    const matchCiudad = exclude === 'ciudad' || !filtros.ciudad || c.ciudad === filtros.ciudad
    const matchPais = exclude === 'pais' || !filtros.pais || c.pais === filtros.pais
    const matchAgente =
      exclude === 'agente' || !filtros.agente || c.agenteAsignado === filtros.agente
    return matchClas && matchCiudad && matchPais && matchAgente
  })
}

export type SegmentoEstado =
  'TODOS' | 'SIN_REGISTRAR' | 'REGISTRADOS' | 'PENDIENTE_CHECKIN' | 'CHECKED_IN'

const SEGMENTO_ESTADO_LABEL: Record<SegmentoEstado, { label: string }> = {
  TODOS: { label: 'Todos los contactos seleccionados' },
  SIN_REGISTRAR: { label: 'Contactos sin registrar (pendientes de inscripción)' },
  REGISTRADOS: { label: 'Contactos registrados / confirmados' },
  PENDIENTE_CHECKIN: { label: 'Participantes con check-in pendiente' },
  CHECKED_IN: { label: 'Asistentes con check-in realizado' },
}

export function CampanasPage() {
  const { data, isLoading, error } = useEventosList({ page: 1, pageSize: 100 })
  const eventos = useMemo(() => data?.items ?? [], [data])

  const [viewMode, setViewMode] = useState<ViewMode>('list')
  const [page, setPage] = useState(1)
  const [filtroEvento, setFiltroEvento] = useState('')
  const [filtroTipo, setFiltroTipo] = useState<CampanaTipo | ''>('')
  const [showCreate, setShowCreate] = useState(false)
  const [showPlantillasModal, setShowPlantillasModal] = useState(false)
  const [detalleCampana, setDetalleCampana] = useState<CampanaFila | null>(null)
  const [campanaPorAprobar, setCampanaPorAprobar] = useState<CampanaFila | null>(null)
  const canCampaignsWrite = useCan(ROLES.campaignsWrite)
  const [estadoOverrides, setEstadoOverrides] = useState<Record<string, CampanaFila['estado']>>({})
  const [reboteOverrides, setReboteOverrides] = useState<
    Record<string, { rebotados: number; reenviados: number }>
  >({})

  const eventosMap = useMemo(() => new Map(eventos.map((e) => [e.id, e])), [eventos])

  const tabla = useCampanasTabla(eventos, { page, eventId: filtroEvento })

  const campanas = useMemo(() => {
    return tabla.filas.map((c): CampanaFila => {
      const reboteOverride = reboteOverrides[c.id]
      const estadoOverride = estadoOverrides[c.id]
      return {
        ...c,
        ...(reboteOverride
          ? { rebotados: reboteOverride.rebotados, reenviados: reboteOverride.reenviados }
          : {}),
        ...(estadoOverride ? { estado: estadoOverride } : {}),
      }
    })
  }, [tabla.filas, reboteOverrides, estadoOverrides])

  const eventosVigentes = useMemo(
    () => eventos.filter((e) => e.status === 'planning' || e.status === 'in_progress'),
    [eventos],
  )

  const handleReenviarRebotados = (campana: CampanaFila) => {
    const reenviados = (campana.reenviados ?? 0) + (campana.rebotados ?? 0)
    setReboteOverrides((prev) => ({ ...prev, [campana.id]: { rebotados: 0, reenviados } }))
    setDetalleCampana((prev) =>
      prev && prev.id === campana.id ? { ...prev, rebotados: 0, reenviados } : prev,
    )
  }

  // El evento filtra en el servicio; el tipo solo refina la página que llegó.
  const filtradas = useMemo(
    () => campanas.filter((c) => !filtroTipo || c.tipo === filtroTipo),
    [campanas, filtroTipo],
  )

  const columns: ReportColumn<CampanaFila>[] = [
    { header: 'Evento', accessor: (c) => c.eventoNombre },
    { header: 'Tipo de campaña', accessor: (c) => c.tipo },
    { header: 'Idioma', accessor: (c) => c.idioma },
    { header: 'Fecha de envío', accessor: (c) => formatDate(c.fechaEnvio) },
    { header: 'Enviados', accessor: (c) => c.enviados },
    { header: 'Entregados', accessor: (c) => c.entregados ?? '' },
    { header: 'Rebotados', accessor: (c) => c.rebotados ?? '' },
    { header: 'Reenviados', accessor: (c) => c.reenviados ?? '' },
    { header: 'Estado', accessor: (c) => c.estado },
  ]

  if (isLoading) return <Spinner label="Cargando campañas…" />
  if (error != null) return <ErrorNote error={error} />

  return (
    <div className="space-y-6 max-w-7xl mx-auto pb-12">
      <Breadcrumbs
        items={[{ label: 'Invitaciones', to: '/convocatorias' }, { label: 'Campañas' }]}
      />

      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="font-sans text-2xl font-extrabold tracking-tight text-slate-900 sm:text-3xl dark:text-white">
            Campañas de Convocatoria
          </h1>
          <p className="mt-1 font-body text-xs text-slate-500 sm:text-sm dark:text-slate-400">
            Múltiples campañas por evento (Save the date, Registro, Recordatorio, Venue) con idioma
            derivado del evento.
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-3">
          <Button
            type="button"
            variant="outline"
            onClick={() => setShowPlantillasModal(true)}
            className="font-bold text-xs cursor-pointer border-slate-300 dark:border-slate-700"
          >
            <FileText size={15} className="mr-1.5 text-blue-600" /> Gestor de Plantillas
          </Button>

          <Button
            onClick={() => setShowCreate(true)}
            disabled={!canCampaignsWrite.allowed}
            title={canCampaignsWrite.title}
            className="bg-brand-deep font-bold text-white hover:bg-brand-deep/90 shadow-sm cursor-pointer"
          >
            <Plus size={16} className="mr-1 stroke-[2.5]" /> Nueva campaña
          </Button>
        </div>
      </div>

      <div className="flex flex-wrap items-end gap-3">
        <Field label="Evento">
          <SearchableSelect
            className={inputClass}
            value={filtroEvento}
            onChange={(e) => {
              setFiltroEvento(e.target.value)
              setPage(1)
            }}
          >
            <option value="">Todos los eventos</option>
            {eventos.map((e) => (
              <option key={e.id} value={e.id}>
                {e.name}
              </option>
            ))}
          </SearchableSelect>
        </Field>

        <Field label="Tipo de campaña">
          <SearchableSelect
            aria-label="Tipo de evento"
            className={inputClass}
            value={filtroTipo}
            onChange={(e) => setFiltroTipo(e.target.value as CampanaTipo | '')}
          >
            <option value="">Todos los tipos</option>
            {CAMPANA_TIPOS.map((t) => (
              <option key={t} value={t}>
                {t}
              </option>
            ))}
          </SearchableSelect>
        </Field>

        <div className="flex items-center gap-2.5 ml-auto">
          <ViewModeToggle value={viewMode} onChange={setViewMode} size="sm" />

          <ExportButtons
            filename="campanas-convocatoria"
            data={filtradas}
            columns={columns}
            title="Campañas de Convocatoria"
          />
        </div>
      </div>

      {filtradas.length === 0 ? (
        <Card className="p-8 bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 rounded-3xl shadow-2xs">
          <EmptyState>No se encontraron campañas con los filtros seleccionados.</EmptyState>
        </Card>
      ) : viewMode === 'grid' ? (
        <CardGrid>
          {filtradas.map((c) => (
            <GridCard
              key={c.id}
              onClick={() => setDetalleCampana(c)}
              titleLines={1}
              tags={<GridCardTag className="normal-case tracking-normal">{c.tipo}</GridCardTag>}
              aside={
                <GridCardTag tone="slate" className="font-mono">
                  {c.idioma}
                </GridCardTag>
              }
              title={c.eventoNombre}
              footerStart={
                <span className="flex items-center gap-1 text-xs text-slate-500">
                  <CalendarDays size={13} className="text-slate-400" />
                  {formatDate(c.fechaEnvio)}
                </span>
              }
              footerEnd={
                <GridCardCta variant="link">
                  Ver Métricas <ArrowRight size={13} />
                </GridCardCta>
              }
            >
              <GridCardStats
                items={[
                  { label: 'Enviados', value: c.enviados },
                  { label: 'Abiertos', value: '—', tone: 'emerald' },
                  { label: 'Clics', value: '—', tone: 'blue' },
                ]}
              />
            </GridCard>
          ))}
        </CardGrid>
      ) : (
        <Card className="p-0 overflow-hidden bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 rounded-3xl shadow-2xs">
          <DataTable
            rows={filtradas}
            getRowKey={(c) => c.id}
            columns={[
              {
                key: 'evento',
                header: 'Evento',
                cell: (c) => (
                  <Link
                    to={`/convocatorias/eventos/${c.eventoId}`}
                    className="font-bold text-brand hover:underline dark:text-blue-400"
                  >
                    {c.eventoNombre}
                  </Link>
                ),
              },
              {
                key: 'tipo',
                header: 'Tipo de Campaña',
                cell: (c) => (
                  <span className="rounded-md bg-blue-100 px-2 py-0.5 font-bold text-blue-900 dark:bg-blue-950 dark:text-blue-300">
                    {c.tipo}
                  </span>
                ),
              },
              {
                key: 'idioma',
                header: 'Idioma',
                cell: (c) => (
                  <span className="font-mono font-bold text-slate-700 dark:text-slate-300">
                    {c.idioma}
                  </span>
                ),
              },
              {
                key: 'fechaEnvio',
                header: 'Fecha Envío',
                cell: (c) => (
                  <span className="text-slate-600 dark:text-slate-400">
                    {formatDate(c.fechaEnvio)}
                  </span>
                ),
              },
              {
                key: 'enviados',
                header: 'Enviados',
                align: 'center',
                cell: (c) => (
                  <span className="font-bold text-slate-900 dark:text-white">
                    {c.enviados.toLocaleString()}
                  </span>
                ),
              },
              {
                key: 'entregados',
                header: 'Entregados',
                align: 'center',
                cell: (c) => (
                  <span className="text-emerald-700 font-bold dark:text-emerald-400">
                    {c.entregados?.toLocaleString() ?? '—'}
                  </span>
                ),
              },
              {
                key: 'estado',
                header: 'Estado',
                align: 'center',
                cell: (c) => (
                  <span
                    className={`rounded-md px-2.5 py-0.5 text-[11px] font-extrabold ${
                      c.campaign
                        ? CAMPAIGN_STATE_CLASS[c.campaign.state]
                        : c.estado === 'PendienteAprobacion'
                          ? 'bg-amber-100 text-amber-900 dark:bg-amber-950 dark:text-amber-300'
                          : c.estado === 'Rechazada'
                            ? 'bg-rose-100 text-rose-800 dark:bg-rose-950 dark:text-rose-300'
                            : 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300'
                    }`}
                  >
                    {c.estado === 'PendienteAprobacion' ? 'Pendiente Aprobación' : c.estado}
                  </span>
                ),
              },
              {
                key: 'acciones',
                header: 'Acciones',
                align: 'right',
                cell: (c) => (
                  <CampanaActionsMenu
                    campana={c}
                    onVerDetalle={() => setDetalleCampana(c)}
                    onAprobarLanzar={() => setCampanaPorAprobar(c)}
                  />
                ),
              },
            ]}
          />
          {tabla.isPending && <Spinner label="Cargando campañas…" />}
        </Card>
      )}

      {tabla.error != null && <ErrorNote error={tabla.error} />}
      <Pagination
        page={page}
        pageCount={tabla.pageCount}
        onPageChange={setPage}
        total={tabla.total}
      />

      {/* Modal de Gestor de Plantillas */}
      {showPlantillasModal && (
        <PlantillaEditorModal
          open={showPlantillasModal}
          onClose={() => setShowPlantillasModal(false)}
          onSave={() => {}}
        />
      )}

      {/* Modal de Aprobación y Lanzamiento de Convocatoria */}
      <CampanaApprovalModal
        campana={campanaPorAprobar}
        onClose={() => setCampanaPorAprobar(null)}
        onApprove={(id) => {
          setEstadoOverrides((prev) => ({ ...prev, [id]: 'Enviada' }))
        }}
        onReject={(id) => {
          setEstadoOverrides((prev) => ({ ...prev, [id]: 'Rechazada' }))
        }}
      />

      {/* Modal de Creación de Nueva Campaña */}
      {showCreate && (
        <NuevaCampanaModal
          eventos={eventosVigentes}
          onClose={() => setShowCreate(false)}
          onLaunched={() => setShowCreate(false)}
        />
      )}

      {/* Modal de Detalle de Envío: destinatarios, rebotes, .ics */}
      {detalleCampana && (
        <CampanaDetalleModal
          campana={detalleCampana}
          evento={(() => {
            const evento = eventosMap.get(detalleCampana.eventoId)
            if (!evento) return undefined
            return { name: evento.name, startDate: evento.startDate, endDate: evento.endDate }
          })()}
          onClose={() => setDetalleCampana(null)}
          onReenviarRebotados={() => handleReenviarRebotados(detalleCampana)}
        />
      )}
    </div>
  )
}

/** Menú dropdown con tres puntos para acciones de campaña. */
function CampanaActionsMenu({
  campana,
  onVerDetalle,
  onAprobarLanzar,
}: {
  campana: CampanaFila
  onVerDetalle: () => void
  onAprobarLanzar: () => void
}) {
  const [open, setOpen] = useState(false)
  const anchorRef = useRef<HTMLButtonElement>(null)
  const canAprobar = campana.estado === 'PendienteAprobacion' || campana.campaign?.state === 'draft'

  return (
    <div className="relative inline-block" onClick={(e) => e.stopPropagation()}>
      <IconButton
        ref={anchorRef}
        onClick={() => setOpen((prev) => !prev)}
        aria-label="Acciones de la campaña"
        title="Acciones de la campaña"
      >
        <MoreVertical size={16} />
      </IconButton>

      <ActionMenu open={open} onClose={() => setOpen(false)} anchorRef={anchorRef} className="w-48">
        <MenuItem
          onClick={() => {
            setOpen(false)
            onVerDetalle()
          }}
        >
          <Eye size={14} className="text-blue-600" /> Ver detalle
        </MenuItem>

        {canAprobar && (
          <MenuItem
            onClick={() => {
              setOpen(false)
              onAprobarLanzar()
            }}
            className="text-amber-700 hover:bg-amber-50 dark:text-amber-300 dark:hover:bg-amber-950/40 font-bold"
          >
            <CheckCircle2 size={14} className="text-amber-600" /> Aprobar / Lanzar
          </MenuItem>
        )}
      </ActionMenu>
    </div>
  )
}

/** Una de las dos fuentes de contactos del asistente. Es una casilla: se pueden marcar ambas. */
function OrigenCard({
  icon: Icon,
  titulo,
  descripcion,
  checked,
  onChange,
}: {
  icon: LucideIcon
  titulo: string
  descripcion: string
  checked: boolean
  onChange: (checked: boolean) => void
}) {
  return (
    <label
      className={`flex cursor-pointer items-start gap-3 rounded-2xl border p-3.5 transition-colors focus-within:ring-2 focus-within:ring-blue-200 ${
        checked
          ? 'border-brand-deep bg-blue-50/50 dark:border-blue-500 dark:bg-blue-950/30'
          : 'border-slate-200 bg-white hover:border-slate-300 dark:border-slate-700 dark:bg-slate-900'
      }`}
    >
      <input
        type="checkbox"
        checked={checked}
        onChange={(e) => onChange(e.target.checked)}
        className="sr-only"
      />
      <span
        className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-xl ${
          checked
            ? 'bg-brand-deep text-white dark:bg-blue-600'
            : 'bg-slate-100 text-slate-500 dark:bg-slate-800 dark:text-slate-400'
        }`}
      >
        <Icon size={18} />
      </span>
      <span className="min-w-0 flex-1">
        <span className="block text-xs font-bold text-slate-900 dark:text-white">{titulo}</span>
        <span className="block text-[11px] text-slate-500 dark:text-slate-400">{descripcion}</span>
      </span>
      <span
        aria-hidden
        className={`mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center rounded-md border ${
          checked
            ? 'border-brand-deep bg-brand-deep text-white dark:border-blue-500 dark:bg-blue-600'
            : 'border-slate-300 bg-white dark:border-slate-600 dark:bg-slate-900'
        }`}
      >
        {checked && <Check size={13} className="stroke-3" />}
      </span>
    </label>
  )
}

export interface ContactFileRowIssue {
  fila: number
  nombre: string
  email: string
  empresa: string
  telefono: string
  tipo: 'sin_nombre' | 'sin_correo' | 'email_invalido' | 'duplicado'
  detalle: string
}

export interface FileValidationReport {
  fileName: string
  totalFilas: number
  totalValidos: number
  validos: Array<{ nombre: string; email: string; empresa: string; telefono: string }>
  sinNombre: ContactFileRowIssue[]
  sinCorreo: ContactFileRowIssue[]
  invalidos: ContactFileRowIssue[]
  duplicados: ContactFileRowIssue[]
  todasLasIncidencias: ContactFileRowIssue[]
}

const EMAIL_REGEX =
  /^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)+$/

async function parseAndValidateContactsFile(file: File): Promise<FileValidationReport> {
  const data = await file.arrayBuffer()
  const workbook = XLSX.read(data, { type: 'array' })
  const firstSheetName = workbook.SheetNames[0]
  const worksheet = workbook.Sheets[firstSheetName]
  const rows = XLSX.utils.sheet_to_json<Record<string, unknown>>(worksheet, { defval: '' })

  const validos: FileValidationReport['validos'] = []
  const sinNombre: ContactFileRowIssue[] = []
  const sinCorreo: ContactFileRowIssue[] = []
  const invalidos: ContactFileRowIssue[] = []
  const duplicados: ContactFileRowIssue[] = []
  const emailsVistos = new Set<string>()

  rows.forEach((row, idx) => {
    const fila = idx + 2 // Base 1 + 1 fila de encabezados

    // Encontrar claves ignorando mayúsculas/espacios/tildes
    let nombreVal = ''
    let apellidoVal = ''
    let emailVal = ''
    let empresaVal = ''
    let telVal = ''

    Object.entries(row).forEach(([rawKey, val]) => {
      const k = rawKey
        .trim()
        .toLowerCase()
        .normalize('NFD')
        .replace(/[\u0300-\u036f]/g, '')
      const sVal = String(val ?? '').trim()
      if (k === 'nombre' || k === 'first name' || k === 'firstname' || k === 'nombres') {
        nombreVal = sVal
      } else if (k === 'apellido' || k === 'apellidos' || k === 'last name' || k === 'lastname') {
        apellidoVal = sVal
      } else if (k === 'correo' || k === 'email' || k === 'e-mail' || k === 'correo electronico') {
        emailVal = sVal
      } else if (
        k === 'empresa' ||
        k === 'company' ||
        k === 'organizacion' ||
        k === 'institution'
      ) {
        empresaVal = sVal
      } else if (k === 'telefono' || k === 'phone' || k === 'celular' || k === 'tel') {
        telVal = sVal
      }
    })

    const nombreCompleto = [nombreVal, apellidoVal].filter(Boolean).join(' ').trim()
    const emailNorm = emailVal.toLowerCase().trim()

    // Caso 1: Fila completamente vacía
    if (!nombreCompleto && !emailNorm && !empresaVal && !telVal) {
      return
    }

    // Caso 2: Contacto sin correo
    if (!emailNorm) {
      sinCorreo.push({
        fila,
        nombre: nombreCompleto || '(Sin nombre)',
        email: '(Vacío)',
        empresa: empresaVal,
        telefono: telVal,
        tipo: 'sin_correo',
        detalle: 'El contacto tiene nombre u otros datos pero no incluye correo electrónico.',
      })
      return
    }

    // Caso 3: Correo con sintaxis inválida
    if (!EMAIL_REGEX.test(emailNorm)) {
      invalidos.push({
        fila,
        nombre: nombreCompleto || '(Sin nombre)',
        email: emailVal,
        empresa: empresaVal,
        telefono: telVal,
        tipo: 'email_invalido',
        detalle:
          'Formato de correo electrónico no válido (sintaxis incorrecta o dominio faltante).',
      })
      return
    }

    // Caso 4: Correo duplicado en el archivo
    if (emailsVistos.has(emailNorm)) {
      duplicados.push({
        fila,
        nombre: nombreCompleto || '(Sin nombre)',
        email: emailNorm,
        empresa: empresaVal,
        telefono: telVal,
        tipo: 'duplicado',
        detalle: `El correo "${emailNorm}" ya apareció previamente en otra fila del archivo.`,
      })
      return
    }
    emailsVistos.add(emailNorm)

    // Caso 5: Correo válido pero sin nombre de contacto
    if (!nombreCompleto) {
      sinNombre.push({
        fila,
        nombre: '(Sin nombre)',
        email: emailNorm,
        empresa: empresaVal,
        telefono: telVal,
        tipo: 'sin_nombre',
        detalle: 'Tiene correo válido pero carece de nombre o apellido para la personalización.',
      })
      // Aunque no tenga nombre, el correo es válido y se puede usar
      validos.push({
        nombre: emailNorm.split('@')[0],
        email: emailNorm,
        empresa: empresaVal,
        telefono: telVal,
      })
      return
    }

    // Caso 6: Válido completo
    validos.push({
      nombre: nombreCompleto,
      email: emailNorm,
      empresa: empresaVal,
      telefono: telVal,
    })
  })

  const todasLasIncidencias = [...invalidos, ...duplicados, ...sinCorreo, ...sinNombre]

  return {
    fileName: file.name,
    totalFilas: rows.length,
    totalValidos: validos.length,
    validos,
    sinNombre,
    sinCorreo,
    invalidos,
    duplicados,
    todasLasIncidencias,
  }
}

/**
 * La plantilla de la carga por archivo. Los títulos son de los que acepta el servicio
 * (`UploadColumnMap`), y el BOM hace que Excel abra los acentos bien. Las dos filas son de
 * ejemplo y se borran antes de cargar.
 */
function descargarPlantillaCsv() {
  const filas = [
    ['Nombre', 'Apellido', 'Correo', 'Teléfono', 'Empresa'],
    ['María', 'González', 'maria.gonzalez@ejemplo.com', '+1 305 555 0101', 'Caribe Travel Tours'],
    ['John', 'Smith', 'john.smith@ejemplo.com', '+1 212 555 0188', 'Atlantic Holidays'],
  ]
  const csv = '﻿' + filas.map((fila) => fila.join(',')).join('\r\n')
  descargarBlob(
    new Blob([csv], { type: 'text/csv;charset=utf-8' }),
    'plantilla-contactos-convocatoria.csv',
  )
}

/** El idioma del evento en la forma del selector; un evento sin idioma se toma en español. */
function mapEventoLanguageToCampanaIdioma(lang?: string | null): CampanaIdioma {
  const code = (lang ?? 'es').toUpperCase()
  return (CAMPANA_IDIOMAS as readonly string[]).includes(code) ? (code as CampanaIdioma) : 'ES'
}

/** Un contacto de crm-service en la forma de la lista de audiencia. */
function crmContactoFilaFrom(c: ContactResponse): CrmContactoFila {
  return {
    id: c.id,
    nombre: c.name,
    empresa: c.companies?.[0]?.name ?? '',
    email: c.email ?? '',
    clasificacion: c.classifications.map((x) => x.name).join(', '),
    status: '',
    ciudad: '',
    pais: '',
    agenteAsignado: '',
    canBeEmailed: c.canBeEmailed,
    suppressionReason: c.suppressionReason,
  }
}

/**
 * Con qué abre el asistente: sin evento, con el asunto y el texto ya escritos si los hay, y con
 * los contactos ya elegidos en el CRM (`contactIds`, de 1 a 1.000) en lugar de los filtros.
 */
export type NuevaCampanaInicial = {
  sinEvento: true
  asunto?: string
  mensaje?: string
  contactIds?: string[]
}

/** La plantilla de una campaña sin evento: no se elige, y solo existe en español. */
const DIRECT_MESSAGE = 'direct_message'

/**
 * El asistente crea la campaña de verdad (`useCampaignWizard`): borrador, selección desde el
 * CRM, carga por archivo y lanzamiento, en los dos modos. El asunto y el cuerpo del paso 2, el
 * formulario dinámico, el estado del invitado y los filtros de ciudad, país y agente
 * siguen en pantalla y no se envían: el servicio no los recibe (las plantillas no se editan).
 * La excepción es una campaña **sin evento**, un mensaje directo: su asunto y su texto son los
 * del paso 2 y sí viajan. `inicial` la abre ya en ese modo, para quien llega desde otra pantalla.
 */
export function NuevaCampanaModal({
  eventos,
  fijarEvento = false,
  onClose,
  onLaunched,
  inicial,
}: {
  eventos: Array<{ id: string; name: string; language?: string | null }>
  /**
   * La campaña se crea desde dentro de un evento: el evento y su idioma ya están decididos y no
   * se preguntan, y la audiencia arranca en los contactos de la ciudad donde se realiza.
   */
  fijarEvento?: boolean
  onClose: () => void
  onLaunched: () => void
  inicial?: NuevaCampanaInicial
}) {
  const [step, setStep] = useState<1 | 2 | 3>(1)
  const initialEvento = eventos[0]
  const [eventoId, setEventoId] = useState(inicial?.sinEvento ? '' : (initialEvento?.id ?? ''))
  const sinEvento = !eventoId
  const wizard = useCampaignWizard()
  // Los contactos que llegan elegidos desde el CRM, sin repetidos: el servicio cuenta así.
  const idsElegidos = useMemo(
    () => (inicial?.contactIds ? Array.from(new Set(inicial.contactIds)) : null),
    [inicial?.contactIds],
  )
  const canWrite = useCan(ROLES.campaignsWrite)
  const canLaunch = useCan(ROLES.campaignsLaunch)

  // Una opción por clave de plantilla elegible; el idioma solo entre los que esa clave tiene.
  const templates = useTemplates(true)
  const templateKeys = useMemo(
    () => Array.from(new Set((templates.data ?? []).map((t) => t.templateKey))),
    [templates.data],
  )
  const [templateKeyElegida, setTemplateKey] = useState('')
  const templateKey = sinEvento ? DIRECT_MESSAGE : templateKeyElegida || templateKeys[0] || ''
  const tipo = (campaignTemplateLabel[templateKey] ?? templateKey) as CampanaTipo
  const idiomasDisponibles = sinEvento
    ? ['ES']
    : (templates.data ?? [])
        .filter((t) => t.templateKey === templateKey)
        .map((t) => t.language.toUpperCase())

  // El idioma sale del evento mientras nadie elija otro; si el del evento no tiene plantilla
  // para esta clave, se ofrece el primero que sí la tenga.
  const [idiomaElegido, setIdioma] = useState<CampanaIdioma | null>(null)
  const idiomaEvento = mapEventoLanguageToCampanaIdioma(
    eventos.find((e) => e.id === eventoId)?.language,
  )
  const idioma: CampanaIdioma =
    [idiomaElegido, idiomaEvento].find(
      (i): i is CampanaIdioma => i != null && idiomasDisponibles.includes(i),
    ) ??
    ((idiomasDisponibles[0] as CampanaIdioma | undefined) || idiomaEvento)

  // Plantilla seleccionada: del catálogo (según la categoría/tipo elegida) o un texto nuevo desde
  // cero. Sin evento no hay catálogo: el texto es siempre el propio.
  const [modoElegido, setPlantillaMode] = useState<'catalogo' | 'personalizada'>('catalogo')
  const plantillaMode = sinEvento ? 'personalizada' : modoElegido
  const [asuntoCustom, setAsuntoCustom] = useState(inicial?.asunto ?? '')
  const [cuerpoCustom, setCuerpoCustom] = useState(inicial?.mensaje ?? '')
  const textoListo = !sinEvento || (asuntoCustom.trim() !== '' && cuerpoCustom.trim() !== '')

  const draftBody = sinEvento
    ? {
        eventId: null,
        templateKey,
        language: idioma.toLowerCase(),
        subject: asuntoCustom,
        message: cuerpoCustom,
      }
    : { eventId: eventoId, templateKey, language: idioma.toLowerCase() }

  const defaultTplFor = (idiomaArg: CampanaIdioma, tipoArg: CampanaTipo) => {
    const tplMap = PLANTILLAS_MULTI_IDIOMA[idiomaArg] ?? PLANTILLAS_DEFAULT
    return tplMap[tipoArg] ?? tplMap['Personalizada'] ?? { asunto: '', cuerpo: '' }
  }
  const tplCatalogo = defaultTplFor(idioma, tipo)
  const asunto = plantillaMode === 'catalogo' ? tplCatalogo.asunto : asuntoCustom
  const cuerpo = plantillaMode === 'catalogo' ? tplCatalogo.cuerpo : cuerpoCustom

  // Audiencia, Segmentación Fina y Origen (CRM y/o Lista personalizada — no son excluyentes)
  const [segmentoEstado, setSegmentoEstado] = useState<SegmentoEstado>('TODOS')
  const [origenCrm, setOrigenCrm] = useState(true)
  const [origenArchivo, setOrigenArchivo] = useState(false)
  const [selectedCrmIds, setSelectedCrmIds] = useState<string[]>(
    isMock ? MOCK_CRM_CONTACTOS.map((c) => c.id) : [],
  )
  // Mis listas del CRM: las listas fijas de contactos
  const listasCrm = useContactLists()
  const [listaCrmId, setListaCrmId] = useState('')
  const listaCrm = useContactList(listaCrmId || null)
  const [showFormBuilderModal, setShowFormBuilderModal] = useState(false)
  const [showReconciliation, setShowReconciliation] = useState(false)
  const [formPersonalizado, setFormPersonalizado] = useState<FormularioConvocatoriaConfig | null>(
    null,
  )

  // En http la clasificación es el código del catálogo de crm-service, el único filtro que
  // la selección de destinatarios acepta; en mock sigue siendo la etiqueta de los contactos demo.
  const [filterClasificacion, setFilterClasificacion] = useState('')
  const [filterCiudad, setFilterCiudad] = useState('')
  const [filterPais, setFilterPais] = useState('')
  const [filterAgente, setFilterAgente] = useState('')
  const [showFiltros, setShowFiltros] = useState(false)
  const filtrosActivos =
    [filterCiudad, filterPais, filterAgente].filter(Boolean).length +
    (isMock && segmentoEstado !== 'TODOS' ? 1 : 0)

  const catalogoClasificaciones = useContactClassifications()
  const contactosApi = useContacts({
    pageSize: 100,
    classification: (!isMock && filterClasificacion) || undefined,
  })
  const contactosFuente = useMemo(
    () => (isMock ? MOCK_CRM_CONTACTOS : (contactosApi.data?.items ?? []).map(crmContactoFilaFrom)),
    [contactosApi.data],
  )
  useEffect(() => {
    if (!isMock) setSelectedCrmIds(contactosFuente.map((c) => c.id))
  }, [contactosFuente])

  /**
   * Elegir una lista marca en la tabla a sus miembros que estén en ella. Lo que viaja es la
   * lista (`contactListId`), no las casillas: convocatorias lee sus miembros del CRM al lanzar.
   */
  useEffect(() => {
    if (!listaCrm.data) return
    const miembros = new Set(listaCrm.data.contactIds)
    setSelectedCrmIds(contactosFuente.filter((c) => miembros.has(c.id)).map((c) => c.id))
  }, [listaCrm.data, contactosFuente])

  // El acceso restringido (whitelist) no se elige aquí: lo define el propio evento (público o privado).
  const { data: eventoSeleccionado } = useEvento(eventoId)

  const handleEventoChange = (selectedId: string) => {
    setEventoId(selectedId)
    setIdioma(null)
  }

  const eventoNombre = eventos.find((e) => e.id === eventoId)?.name ?? ''
  const sedeEvento = eventoSeleccionado?.cities[0]
  const fechasEvento = eventoSeleccionado
    ? formatDateRange(eventoSeleccionado.startDate, eventoSeleccionado.endDate)
    : ''
  const idiomaDelEventoDisponible = idiomasDisponibles.includes(idiomaEvento)

  // Dentro de un evento la audiencia arranca en su ciudad, una sola vez: después manda quien
  // usa el filtro. Si ningún contacto es de esa ciudad no se filtra, y la pantalla lo dice.
  const ciudadEvento = sedeEvento?.name ?? ''
  const mismaCiudad = (c: CrmContactoFila) =>
    c.ciudad.trim().toLowerCase() === ciudadEvento.trim().toLowerCase()
  const hayContactosEnLaCiudad = Boolean(ciudadEvento) && contactosFuente.some(mismaCiudad)
  const ciudadAplicada = useRef(false)
  useEffect(() => {
    if (!fijarEvento || ciudadAplicada.current) return
    if (!ciudadEvento || contactosFuente.length === 0) return
    ciudadAplicada.current = true
    const deLaCiudad = contactosFuente.filter(mismaCiudad)
    if (deLaCiudad.length === 0) return
    setFilterCiudad(deLaCiudad[0].ciudad)
    setSelectedCrmIds(deLaCiudad.map((c) => c.id))
    // `mismaCiudad` se recrea en cada render y solo depende de `ciudadEvento`.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [fijarEvento, ciudadEvento, contactosFuente])

  // En http el servicio arma la lista por clasificación y nada más: lo que se filtre o se
  // desmarque en esta pantalla por ciudad, país, agente o a mano no llega a la campaña.
  const filtrosQueNoViajan =
    !isMock && origenCrm && Boolean(filterCiudad || filterPais || filterAgente)

  /** Los comodines de la plantilla, con los datos del evento elegido. */
  const conDatosDelEvento = (texto: string) =>
    texto
      .replaceAll('{{nombre}}', 'Lic. María González')
      .replaceAll('{{empresa}}', 'TUI Group Europe')
      .replaceAll('{{evento}}', eventoNombre || 'Nombre del evento')
      .replaceAll('{{fecha}}', fechasEvento || 'Fechas del evento')
      .replaceAll(
        '{{lugar}}',
        sedeEvento ? `${sedeEvento.name}, ${sedeEvento.country}` : 'Sede del evento',
      )

  // Filtros CRM incluyentes/en cascada: cada selector solo ofrece los valores que aún son
  // alcanzables dado lo ya elegido en los demás (p. ej. País = España deja solo sus ciudades
  // y agentes). En http la clasificación ya filtró en el servidor.
  const crmFiltros: CrmFiltros = {
    clasificacion: isMock ? filterClasificacion : '',
    ciudad: filterCiudad,
    pais: filterPais,
    agente: filterAgente,
  }
  const disponibles = (campo: keyof CrmFiltros, valor: (c: CrmContactoFila) => string) =>
    Array.from(new Set(crmContactosPara(contactosFuente, crmFiltros, campo).map(valor)))
      .filter(Boolean)
      .sort()
  const clasificacionesDisponibles = isMock
    ? disponibles('clasificacion', (c) => c.clasificacion).map((clas) => ({
        value: clas,
        label: CLASIFICACION_LABEL[clas] ?? clas,
      }))
    : (catalogoClasificaciones.data ?? []).map((c) => ({ value: c.code, label: c.name }))
  const ciudadesDisponibles = disponibles('ciudad', (c) => c.ciudad)
  const paisesDisponibles = disponibles('pais', (c) => c.pais)
  const agentesDisponibles = disponibles('agente', (c) => c.agenteAsignado)

  // Si un filtro deja de ser alcanzable por lo elegido en los demás, se limpia en vez de
  // quedar "seleccionado" apuntando a una combinación imposible.
  const clasificacionValida =
    !filterClasificacion ||
    clasificacionesDisponibles.length === 0 ||
    clasificacionesDisponibles.some((c) => c.value === filterClasificacion)
  useEffect(() => {
    if (!clasificacionValida) setFilterClasificacion('')
  }, [clasificacionValida])
  useEffect(() => {
    if (filterCiudad && !ciudadesDisponibles.includes(filterCiudad)) setFilterCiudad('')
  }, [ciudadesDisponibles, filterCiudad])
  useEffect(() => {
    if (filterPais && !paisesDisponibles.includes(filterPais)) setFilterPais('')
  }, [paisesDisponibles, filterPais])
  useEffect(() => {
    if (filterAgente && !agentesDisponibles.includes(filterAgente)) setFilterAgente('')
  }, [agentesDisponibles, filterAgente])

  // Estado del análisis y validación exhaustiva de la lista cargada (Excel/CSV)
  const [fileValidation, setFileValidation] = useState<FileValidationReport | null>(null)
  const [fileAnalyzing, setFileAnalyzing] = useState(false)
  const [showIssueDetails, setShowIssueDetails] = useState(false)

  // Filtrado final de contactos CRM: clasificación, segmento de estado, ciudad, país y
  // agente asignado. El estado del invitado solo existe en los contactos demo.
  const filteredCrmContactos = crmContactosPara(contactosFuente, crmFiltros).filter(
    (c) => !isMock || segmentoEstado === 'TODOS' || c.status === segmentoEstado,
  )

  const toggleSelectAllCrm = (checked: boolean) => {
    if (checked) {
      setSelectedCrmIds(filteredCrmContactos.map((c) => c.id))
    } else {
      setSelectedCrmIds([])
    }
  }

  const toggleCrmContact = (id: string) => {
    setSelectedCrmIds((prev) => (prev.includes(id) ? prev.filter((i) => i !== id) : [...prev, id]))
  }

  // La carga necesita una campaña: el primer archivo crea el borrador.
  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    e.target.value = ''
    if (!file) return

    setFileAnalyzing(true)
    try {
      const report = await parseAndValidateContactsFile(file)
      setFileValidation(report)
      setShowIssueDetails(report.todasLasIncidencias.length > 0)
    } catch {
      setFileValidation(null)
    } finally {
      setFileAnalyzing(false)
    }

    if (templateKey && textoListo) {
      wizard.stage(draftBody, file).catch(() => {})
    }
  }
  const uploadedFile = wizard.staged
    ? { name: wizard.staged.originalFileName, count: wizard.staged.rowCount }
    : fileValidation
      ? { name: fileValidation.fileName, count: fileValidation.totalValidos }
      : null

  const totalDestinatarios =
    (origenCrm
      ? (idsElegidos ?? listaCrm.data?.contactIds ?? selectedCrmIds).length - (wizard.omitidos ?? 0)
      : 0) + (origenArchivo ? (wizard.staged?.confirmableCount ?? 0) : 0)

  // Las casillas de cada contacto no viajan: el servicio selecciona por filtro. Los contactos
  // elegidos en el CRM sí viajan, como ids, y una de Mis listas viaja como lista; las dos van
  // sin filtro.
  const handleSubmit = (e: FormEvent) => {
    e.preventDefault()
    if (!templateKey || !textoListo) return
    wizard
      .launchWith(draftBody, {
        fromCrm: origenCrm,
        fromFile: origenArchivo,
        contactIds: idsElegidos ?? undefined,
        contactListId: idsElegidos || !listaCrmId ? undefined : listaCrmId,
        classifications: filterClasificacion
          ? [isMock ? codigoDeClasificacion(filterClasificacion) : filterClasificacion]
          : undefined,
      })
      .then((lanzada) => lanzada && onLaunched())
      .catch(() => {})
  }

  return (
    <Modal open onClose={onClose} title="Nueva campaña de convocatoria" width="max-w-3xl">
      <div className="p-1 max-h-[72vh] overflow-y-auto pr-1">
        <form onSubmit={handleSubmit} className="space-y-5 text-xs">
          <Stepper
            steps={['Campaña', 'Mensaje', 'Destinatarios']}
            current={step}
            onStepClick={(s) => setStep(s as 1 | 2 | 3)}
          />

          {/* El evento al que pertenece la campaña, siempre a la vista */}
          {eventoId && (
            <div className="rounded-2xl border border-slate-200 bg-slate-50 p-4 dark:border-slate-700 dark:bg-slate-800/50">
              <p className="text-[10px] font-bold uppercase tracking-widest text-slate-400">
                Campaña para el evento
              </p>
              <p className="mt-0.5 font-sans text-sm font-extrabold leading-snug text-brand-deep dark:text-white">
                {eventoNombre}
              </p>
              <div className="mt-2 flex flex-wrap gap-x-5 gap-y-1.5 text-[11px] text-slate-600 dark:text-slate-300">
                {fechasEvento && (
                  <span className="inline-flex items-center gap-1.5">
                    <CalendarDays size={13} className="shrink-0 text-slate-400" />
                    {fechasEvento}
                  </span>
                )}
                {sedeEvento && (
                  <span className="inline-flex items-center gap-1.5">
                    <MapPin size={13} className="shrink-0 text-slate-400" />
                    {sedeEvento.name}, {sedeEvento.country}
                  </span>
                )}
                <span className="inline-flex items-center gap-1.5">
                  <Languages size={13} className="shrink-0 text-slate-400" />
                  Idioma {idioma}
                </span>
              </div>
            </div>
          )}

          {/* PASO 1: Parámetros del Evento y Tipo de Campaña */}
          {step === 1 && (
            <div className="space-y-4">
              {templates.error != null && <ErrorNote error={templates.error} />}
              {!fijarEvento && (
                <Field label="Evento destino" required>
                  <Select
                    value={eventoId}
                    onChange={(e) => handleEventoChange(e.target.value)}
                    required
                  >
                    {eventos.length === 0 && <option value="">Sin eventos vigentes</option>}
                    {eventos.map((e) => (
                      <option key={e.id} value={e.id}>
                        {e.name}
                      </option>
                    ))}
                    <option value="">Sin evento (mensaje directo)</option>
                  </Select>
                </Field>
              )}

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <Field label="Tipo de campaña" required>
                  <Select
                    value={templateKey}
                    onChange={(e) => setTemplateKey(e.target.value)}
                    disabled={sinEvento}
                  >
                    {(sinEvento ? [DIRECT_MESSAGE] : templateKeys).map((key) => (
                      <option key={key} value={key}>
                        {campaignTemplateLabel[key] ?? key}
                      </option>
                    ))}
                  </Select>
                </Field>

                {/* Dentro de un evento el idioma es el del evento; solo se pregunta si esa
                    plantilla no existe en ese idioma. */}
                {fijarEvento && idiomaDelEventoDisponible ? null : (
                  <Field
                    label="Idioma de la convocatoria"
                    required
                    hint={
                      fijarEvento
                        ? `Esta plantilla no existe en el idioma del evento (${idiomaEvento}); elige uno de los disponibles.`
                        : undefined
                    }
                  >
                    <Select
                      value={idioma}
                      onChange={(e) => setIdioma(e.target.value as CampanaIdioma)}
                    >
                      {CAMPANA_IDIOMAS.map((i) => (
                        <option key={i} value={i} disabled={!idiomasDisponibles.includes(i)}>
                          {i}
                        </option>
                      ))}
                    </Select>
                  </Field>
                )}
              </div>

              {/* Formulario Personalizado & Sincronización CRM */}
              <div className="p-3.5 bg-slate-50 dark:bg-slate-800/60 border border-slate-200 dark:border-slate-700 rounded-2xl flex items-center justify-between gap-3">
                <div className="space-y-0.5">
                  <span className="font-bold text-slate-800 dark:text-slate-200 text-xs block">
                    Formulario Dinámico de Entrada / Actualización
                  </span>
                  <p className="text-[11px] text-slate-500">
                    {formPersonalizado
                      ? `Formulario activo: "${formPersonalizado.titulo}" (${formPersonalizado.campos.length} campos mapeados a CRM)`
                      : 'Personaliza las preguntas y sincroniza datos de agencias directamente al CRM.'}
                  </p>
                </div>

                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => setShowFormBuilderModal(true)}
                  className="shrink-0 font-bold border-blue-300 dark:border-blue-800 text-blue-700 dark:text-blue-300 cursor-pointer"
                >
                  {formPersonalizado ? 'Editar Formulario' : 'Diseñar Formulario CRM'}
                </Button>
              </div>

              <div className="flex justify-end pt-3">
                <Button
                  type="button"
                  onClick={() => setStep(2)}
                  className="bg-brand-deep text-white font-bold cursor-pointer"
                >
                  Siguiente: mensaje <ArrowRight size={14} className="ml-1" />
                </Button>
              </div>
            </div>
          )}

          {/* PASO 2: Plantilla & Mensaje con Modelo Visual */}
          {step === 2 && (
            <div className="space-y-4">
              <div className="space-y-2.5 bg-slate-50 p-3 rounded-xl border border-slate-200 dark:bg-slate-800 dark:border-slate-700">
                <span className="block font-bold text-slate-800 dark:text-slate-200">
                  Contenido del mensaje para <code>{tipo}</code> ({idioma})
                </span>

                {sinEvento ? (
                  <p className="text-[11px] font-medium text-slate-500 dark:text-slate-400">
                    Sin evento no hay plantilla de catálogo: escribe el asunto y el texto que
                    recibirán los destinatarios. Sale como texto, sin enlace de registro.
                  </p>
                ) : (
                  <div className="flex flex-col sm:flex-row gap-2">
                    <label
                      className={`flex flex-1 items-start gap-2 rounded-xl border p-3 cursor-pointer transition-colors ${
                        plantillaMode === 'catalogo'
                          ? 'border-blue-500 bg-blue-50/70 dark:bg-blue-950/30'
                          : 'border-slate-200 bg-white dark:border-slate-700 dark:bg-slate-900'
                      }`}
                    >
                      <input
                        type="radio"
                        name="plantillaMode"
                        checked={plantillaMode === 'catalogo'}
                        onChange={() => setPlantillaMode('catalogo')}
                        className="mt-0.5 h-4 w-4 border-slate-300 text-blue-600"
                      />
                      <span>
                        <span className="block font-bold text-slate-900 dark:text-white">
                          Usar plantilla del catálogo
                        </span>
                        <span className="block text-[11px] font-medium text-slate-500 dark:text-slate-400">
                          Carga la plantilla estándar de esta categoría, editable si hace falta.
                        </span>
                      </span>
                    </label>

                    <label
                      className={`flex flex-1 items-start gap-2 rounded-xl border p-3 cursor-pointer transition-colors ${
                        plantillaMode === 'personalizada'
                          ? 'border-blue-500 bg-blue-50/70 dark:bg-blue-950/30'
                          : 'border-slate-200 bg-white dark:border-slate-700 dark:bg-slate-900'
                      }`}
                    >
                      <input
                        type="radio"
                        name="plantillaMode"
                        checked={plantillaMode === 'personalizada'}
                        onChange={() => {
                          setPlantillaMode('personalizada')
                          setAsuntoCustom('')
                          setCuerpoCustom('')
                        }}
                        className="mt-0.5 h-4 w-4 border-slate-300 text-blue-600"
                      />
                      <span>
                        <span className="block font-bold text-slate-900 dark:text-white">
                          Crear desde cero
                        </span>
                        <span className="block text-[11px] font-medium text-slate-500 dark:text-slate-400">
                          Escribe un asunto y un cuerpo nuevos para este envío.
                        </span>
                      </span>
                    </label>
                  </div>
                )}
              </div>

              {plantillaMode === 'personalizada' ? (
                <div className="space-y-3">
                  <Field label="Asunto del Correo" required={sinEvento}>
                    <TextInput
                      value={asuntoCustom}
                      maxLength={sinEvento ? 200 : undefined}
                      onChange={(e) => setAsuntoCustom(e.target.value)}
                      placeholder="Escribe el asunto del correo..."
                    />
                  </Field>

                  <div>
                    <label className="mb-1 block font-bold text-slate-800 dark:text-slate-200">
                      Cuerpo del Correo Saliente
                      {sinEvento && <span className="ml-0.5 text-red-600">*</span>}
                    </label>
                    <textarea
                      rows={5}
                      value={cuerpoCustom}
                      maxLength={sinEvento ? 10000 : undefined}
                      onChange={(e) => setCuerpoCustom(e.target.value)}
                      placeholder="Escribe el mensaje para los destinatarios..."
                      className="w-full rounded-2xl border border-slate-200 bg-white p-3 font-mono text-xs leading-relaxed text-slate-900 focus:border-blue-600 focus:outline-none dark:border-slate-700 dark:bg-slate-900 dark:text-white"
                    />
                  </div>
                </div>
              ) : null}

              {/* Modelo Visual del Correo (Sin etiquetas {{}}) */}
              <div className="rounded-2xl border border-slate-200 bg-slate-50/80 p-4 dark:border-slate-800 dark:bg-slate-900/80 space-y-3">
                <div className="flex items-center justify-between border-b border-slate-200 pb-2 dark:border-slate-700">
                  <span className="text-[11px] font-bold text-slate-500 uppercase tracking-wider">
                    Modelo Visual del Correo Saliente
                  </span>
                </div>

                <div className="bg-white dark:bg-slate-950 rounded-xl border border-slate-200 dark:border-slate-800 p-4 space-y-3 shadow-2xs">
                  <div className="border-b border-slate-100 dark:border-slate-800 pb-2.5 flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      <div className="h-6 w-6 rounded-lg bg-brand-deep text-white flex items-center justify-center text-[10px] font-black">
                        RD
                      </div>
                      <div>
                        <p className="text-xs font-extrabold text-slate-900 dark:text-white">
                          Organización Anfitriona
                        </p>
                        <p className="text-[10px] text-slate-400">convocatorias@empresa.com</p>
                      </div>
                    </div>
                    <span className="text-[10px] text-slate-400 font-mono">Hoy, 09:30 AM</span>
                  </div>

                  <div>
                    <span className="text-[10px] font-bold text-slate-400 uppercase">Asunto:</span>
                    <h4 className="text-xs font-extrabold text-slate-900 dark:text-white">
                      {conDatosDelEvento(asunto || 'Sin asunto configurado todavía')}
                    </h4>
                  </div>

                  <div className="text-xs text-slate-700 dark:text-slate-300 leading-relaxed whitespace-pre-line border-t border-slate-100 dark:border-slate-800 pt-2.5">
                    {conDatosDelEvento(
                      cuerpo || 'Escribe el cuerpo del mensaje para ver la vista previa aquí.',
                    )}
                  </div>

                  {!sinEvento && (
                    <div className="pt-2 text-center">
                      <span className="inline-block bg-brand-deep text-white text-xs font-bold px-5 py-2 rounded-xl shadow-xs">
                        Confirmar Asistencia / Ver Registro
                      </span>
                    </div>
                  )}

                  <div className="border-t border-slate-100 dark:border-slate-800 pt-2.5 text-[10px] text-slate-400 text-center">
                    <p>© 2026 Plataforma Corporativa de Eventos · Todos los derechos reservados.</p>
                  </div>
                </div>
              </div>

              {/* La aprobación del texto por el administrador central todavía no existe en
                  ningún servicio (pedido P6 de docs/CAMBIOS_FIX_EVENTOS.md): el botón queda a la
                  vista y desactivado, y no frena el paso siguiente hasta que exista. */}
              <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-dashed border-slate-300 bg-slate-50/60 p-3 dark:border-slate-700 dark:bg-slate-800/30">
                <p className="min-w-0 flex-1 text-[11px] text-slate-500 dark:text-slate-400">
                  <strong className="text-slate-700 dark:text-slate-200">
                    Aprobación del correo.
                  </strong>{' '}
                  Enviará este texto al administrador central para que lo apruebe antes de lanzar la
                  campaña. Pendiente de backend.
                </p>
                <Button
                  type="button"
                  variant="outline"
                  disabled
                  title="Pendiente de backend: todavía no existe la aprobación del correo"
                  className="text-xs font-bold"
                >
                  Confirmar correo
                </Button>
              </div>

              <div className="flex justify-between pt-3">
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => setStep(1)}
                  className="cursor-pointer"
                >
                  <ArrowLeft size={14} className="mr-1" /> Anterior
                </Button>
                <Button
                  type="button"
                  onClick={() => setStep(3)}
                  className="bg-brand-deep text-white font-bold cursor-pointer"
                >
                  Siguiente: destinatarios <ArrowRight size={14} className="ml-1" />
                </Button>
              </div>
            </div>
          )}

          {/* PASO 3: Segmentación Fina de Estado & Origen de Audiencia */}
          {step === 3 && (
            <div className="space-y-4">
              {/* Origen de Contactos: CRM y/o Lista personalizada, no son excluyentes */}
              <fieldset className="space-y-2">
                <legend className="text-sm font-medium text-slate-700 dark:text-slate-200">
                  Origen de los contactos
                  <span className="ml-1.5 text-xs font-normal text-slate-400">
                    Puedes combinar los dos
                  </span>
                </legend>

                <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                  <OrigenCard
                    icon={Building2}
                    titulo="Contactos del CRM"
                    descripcion="Elige del directorio, por clasificación y filtros."
                    checked={origenCrm}
                    onChange={setOrigenCrm}
                  />
                  <OrigenCard
                    icon={Upload}
                    titulo="Lista en archivo"
                    descripcion="Sube un CSV o Excel con tus propios contactos."
                    checked={origenArchivo}
                    onChange={setOrigenArchivo}
                  />
                </div>
              </fieldset>

              {/* El acceso queda determinado por la visibilidad del evento, no por una selección manual aquí */}
              {eventoSeleccionado?.registrationType === 'whitelist' ? (
                <div className="flex items-start gap-2.5 rounded-2xl border border-amber-200 bg-amber-50/60 p-3.5 text-amber-900 dark:border-amber-900/50 dark:bg-amber-950/20 dark:text-amber-300">
                  <ShieldCheck size={18} className="mt-0.5 shrink-0 text-amber-600" />
                  <span>
                    Este evento es <strong>privado</strong>: solo podrán registrarse los invitados
                    definidos en la ficha del evento, aunque la convocatoria llegue a más contactos.
                  </span>
                </div>
              ) : eventoSeleccionado?.registrationType === 'abierto' ? (
                <div className="flex items-start gap-2.5 rounded-2xl border border-blue-200 bg-blue-50/60 p-3.5 text-blue-900 dark:border-blue-900/50 dark:bg-blue-950/20 dark:text-blue-300">
                  <ShieldCheck size={18} className="mt-0.5 shrink-0 text-blue-600" />
                  <span>
                    Este evento es <strong>público</strong>: cualquier destinatario de esta
                    convocatoria podrá registrarse.
                  </span>
                </div>
              ) : null}

              {/* Contactos elegidos en el CRM: reemplazan los filtros y la lista */}
              {origenCrm && idsElegidos && (
                <Card className="p-4 space-y-1 bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl">
                  <p className="font-bold text-slate-800 dark:text-slate-200">
                    {idsElegidos.length} contactos elegidos en el CRM
                  </p>
                  <p className="text-[11px] text-slate-500 dark:text-slate-400">
                    Al lanzar, Convocatorias excluye a quien no puede recibir correos (sin correo,
                    dado de baja o no invitar).
                  </p>
                </Card>
              )}

              {/* Selección Directa de Segmentos Guardados del CRM */}
              <div
                hidden={idsElegidos != null}
                className="space-y-1.5 p-3 rounded-2xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200 dark:border-slate-700"
              >
                <div className="flex items-center justify-between">
                  <label className="font-bold text-slate-800 dark:text-slate-200 text-xs">
                    Aplicar una de Mis listas del CRM (Opcional)
                  </label>
                  <span className="text-[10px] text-blue-600 dark:text-blue-400 font-bold">
                    {listasCrm.data?.length ?? 0} listas disponibles
                  </span>
                </div>
                <Select
                  value={listaCrmId}
                  onChange={(e) => setListaCrmId(e.target.value)}
                  className="font-semibold text-xs"
                >
                  <option value="">Seleccionar una lista del CRM...</option>
                  {(listasCrm.data ?? []).map((lista) => (
                    <option key={lista.id} value={lista.id}>
                      {lista.name} ({lista.memberCount} contactos)
                    </option>
                  ))}
                </Select>
                {listaCrm.data && (
                  <p className="text-[11px] text-slate-500 dark:text-slate-400">
                    {listaCrm.data.contactIds.filter((id) => selectedCrmIds.includes(id)).length} de{' '}
                    {listaCrm.data.contactIds.length} contactos de la lista están en la tabla y
                    quedaron marcados. Al lanzar, la campaña va a todos los de la lista, en vez de a
                    la clasificación elegida; Convocatorias excluye a quien no puede recibir
                    correos.
                  </p>
                )}
                {(listasCrm.error ?? listaCrm.error) != null && (
                  <ErrorNote error={listasCrm.error ?? listaCrm.error} />
                )}
              </div>

              {/* CRM LIST */}
              {origenCrm && !idsElegidos && (
                <Card className="p-4 space-y-3 bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl">
                  <div className="flex items-center justify-between">
                    <p className="text-[11px] font-bold uppercase tracking-wider text-slate-400">
                      Contactos del directorio CRM
                    </p>
                    {formPersonalizado && (
                      <span className="px-2 py-0.5 rounded-full text-[10px] font-bold bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300 border border-emerald-300">
                        Formulario Vinculado ({formPersonalizado.campos.length} campos)
                      </span>
                    )}
                  </div>

                  {fijarEvento && hayContactosEnLaCiudad && (
                    <Callout tone="info">
                      La lista arranca con los contactos de <strong>{ciudadEvento}</strong>, la
                      ciudad del evento. Cambia el filtro de ciudad para ampliarla.
                    </Callout>
                  )}
                  {fijarEvento &&
                    ciudadEvento &&
                    contactosFuente.length > 0 &&
                    !hayContactosEnLaCiudad && (
                      <Callout tone="warning">
                        No se pudo preseleccionar por ciudad: ningún contacto del directorio figura
                        en {ciudadEvento}.
                        {!isMock &&
                          ' El CRM todavía no entrega la ciudad de cada contacto (pendiente de backend).'}
                      </Callout>
                    )}
                  {filtrosQueNoViajan && (
                    <Callout tone="warning">
                      El servicio arma la lista solo por clasificación: los filtros de ciudad, país
                      y agente se ven aquí pero no reducen a quién se le envía.
                    </Callout>
                  )}

                  <div className="flex flex-wrap items-end gap-2.5">
                    <div className="min-w-48 flex-1">
                      <Field label="Clasificación">
                        <Select
                          value={filterClasificacion}
                          onChange={(e) => setFilterClasificacion(e.target.value)}
                        >
                          <option value="">Todas</option>
                          {clasificacionesDisponibles.map((clas) => (
                            <option key={clas.value} value={clas.value}>
                              {clas.label}
                            </option>
                          ))}
                        </Select>
                      </Field>
                    </div>
                    <Button
                      type="button"
                      variant="outline"
                      onClick={() => setShowFiltros((prev) => !prev)}
                      aria-expanded={showFiltros}
                      className="cursor-pointer text-xs font-bold"
                    >
                      <SlidersHorizontal size={14} className="mr-1.5" />
                      Filtros{filtrosActivos > 0 ? ` (${filtrosActivos})` : ''}
                    </Button>
                  </div>

                  {/* Ciudad, país, agente y estado en el evento: plegados
                      hasta que se piden */}
                  {showFiltros && (
                    <div className="grid grid-cols-2 gap-2.5 sm:grid-cols-4">
                      {/* El estado del invitado solo existe en los contactos de demostración. */}
                      {isMock && (
                        <div className="col-span-2 sm:col-span-4">
                          <Field label="Estado del invitado en el evento">
                            <Select
                              value={segmentoEstado}
                              onChange={(e) => setSegmentoEstado(e.target.value as SegmentoEstado)}
                            >
                              {(Object.keys(SEGMENTO_ESTADO_LABEL) as SegmentoEstado[]).map(
                                (segKey) => (
                                  <option key={segKey} value={segKey}>
                                    {SEGMENTO_ESTADO_LABEL[segKey].label}
                                  </option>
                                ),
                              )}
                            </Select>
                          </Field>
                        </div>
                      )}
                      <Field label="Ciudad">
                        <Select
                          value={filterCiudad}
                          onChange={(e) => setFilterCiudad(e.target.value)}
                        >
                          <option value="">Todas</option>
                          {ciudadesDisponibles.map((ciudad) => (
                            <option key={ciudad} value={ciudad}>
                              {ciudad}
                            </option>
                          ))}
                        </Select>
                      </Field>

                      <Field label="País">
                        <Select value={filterPais} onChange={(e) => setFilterPais(e.target.value)}>
                          <option value="">Todos</option>
                          {paisesDisponibles.map((pais) => (
                            <option key={pais} value={pais}>
                              {pais}
                            </option>
                          ))}
                        </Select>
                      </Field>

                      <Field label="Agente">
                        <Select
                          value={filterAgente}
                          onChange={(e) => setFilterAgente(e.target.value)}
                        >
                          <option value="">Todos</option>
                          {agentesDisponibles.map((agente) => (
                            <option key={agente} value={agente}>
                              {agente}
                            </option>
                          ))}
                        </Select>
                      </Field>
                    </div>
                  )}

                  {/* Resumen transparente de supresiones (Regla del backend: los excluidos se muestran con motivo explícito) */}
                  {selectedCrmIds.length > 0 &&
                    (() => {
                      const seleccionados = contactosFuente.filter((c) =>
                        selectedCrmIds.includes(c.id),
                      )
                      const enviables = seleccionados.filter((c) => c.canBeEmailed)
                      const excluidos = seleccionados.filter((c) => !c.canBeEmailed)
                      const optOuts = excluidos.filter(
                        (c) => c.suppressionReason === 'opted_out',
                      ).length
                      const doNotInvites = excluidos.filter(
                        (c) => c.suppressionReason === 'do_not_invite',
                      ).length
                      const noEmails = excluidos.filter(
                        (c) => c.suppressionReason === 'no_email',
                      ).length

                      return (
                        <div className="rounded-xl border border-slate-200 bg-slate-50 p-3 text-xs dark:border-slate-800 dark:bg-slate-950/50 space-y-1">
                          <div className="flex items-center justify-between font-bold">
                            <span className="text-slate-700 dark:text-slate-200">
                              Destinatarios seleccionados ({seleccionados.length}):
                            </span>
                            <span className="text-emerald-600 dark:text-emerald-400 font-black">
                              {enviables.length} enviables
                            </span>
                          </div>
                          {excluidos.length > 0 && (
                            <p className="text-[11px] text-rose-600 dark:text-rose-400 font-semibold">
                              {excluidos.length} excluidos por supresión ({optOuts} dados de baja,{' '}
                              {doNotInvites} no invitar, {noEmails} sin correo). No recibirán la
                              convocatoria.
                            </p>
                          )}
                        </div>
                      )
                    })()}

                  <label className="flex items-center gap-2 font-bold text-xs text-slate-700 dark:text-slate-300 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={
                        selectedCrmIds.length === filteredCrmContactos.length &&
                        filteredCrmContactos.length > 0
                      }
                      onChange={(e) => toggleSelectAllCrm(e.target.checked)}
                      className="rounded border-slate-300 text-blue-600"
                    />
                    Seleccionar filtrados ({filteredCrmContactos.length})
                  </label>

                  <div className="max-h-40 overflow-y-auto divide-y divide-slate-100 border border-slate-200 rounded-xl dark:border-slate-800 dark:divide-slate-800">
                    {filteredCrmContactos.length === 0 && (
                      <p className="p-3 text-center text-[11px] font-semibold text-slate-400">
                        Ningún contacto coincide con estos filtros.
                      </p>
                    )}
                    {filteredCrmContactos.map((c) => (
                      <label
                        key={c.id}
                        className="flex items-center justify-between p-2.5 hover:bg-slate-50 dark:hover:bg-slate-800/60 cursor-pointer text-xs"
                      >
                        <div className="flex items-center gap-2.5">
                          <input
                            type="checkbox"
                            checked={selectedCrmIds.includes(c.id)}
                            onChange={() => toggleCrmContact(c.id)}
                            className="rounded border-slate-300 text-blue-600 h-4 w-4"
                          />
                          <div>
                            <div className="flex items-center gap-2">
                              <p className="font-bold text-slate-900 dark:text-white">{c.nombre}</p>
                              {!c.canBeEmailed && (
                                <span className="rounded bg-rose-100 px-1.5 py-0.5 text-[9px] font-bold text-rose-800 dark:bg-rose-950 dark:text-rose-300">
                                  Excluido ({c.suppressionReason})
                                </span>
                              )}
                            </div>
                            <p className="text-[11px] text-slate-400">
                              {[
                                c.empresa,
                                c.email || 'Sin correo',
                                [c.ciudad, c.pais].filter(Boolean).join(', '),
                              ]
                                .filter(Boolean)
                                .join(' · ')}
                            </p>
                          </div>
                        </div>
                        {c.status && (
                          <span className="rounded bg-blue-100 px-2 py-0.5 text-[10px] font-bold text-blue-800 dark:bg-blue-950 dark:text-blue-300">
                            {c.status}
                          </span>
                        )}
                      </label>
                    ))}
                  </div>
                </Card>
              )}

              {/* ARCHIVO ADJUNTO */}
              {origenArchivo && (
                <Card className="p-5 space-y-4 bg-slate-50/70 dark:bg-slate-800/50 border-2 border-dashed border-slate-300 dark:border-slate-700 rounded-2xl">
                  <div className="text-center space-y-2">
                    <Upload size={30} className="mx-auto text-emerald-600" />
                    <p className="font-bold text-sm text-slate-800 dark:text-slate-200">
                      Carga tu lista de contactos en CSV o Excel (.xlsx)
                    </p>
                    <p className="text-[11px] text-slate-500 dark:text-slate-400">
                      Columnas recomendadas: Nombre, Apellido, Correo, Teléfono y Empresa.{' '}
                      <button
                        type="button"
                        onClick={descargarPlantillaCsv}
                        className="inline-flex cursor-pointer items-center gap-1 font-bold text-blue-700 underline hover:text-blue-900 dark:text-blue-400"
                      >
                        <Download size={12} /> Descargar plantilla de ejemplo
                      </button>
                    </p>
                    <input
                      type="file"
                      id="file-input-3"
                      accept=".csv,.xlsx,.xls,.txt"
                      onChange={handleFileUpload}
                      className="hidden"
                    />
                    <div className="pt-1">
                      <Button
                        type="button"
                        variant="outline"
                        disabled={fileAnalyzing || wizard.isStaging || !canWrite.allowed}
                        title={canWrite.title}
                        onClick={() => document.getElementById('file-input-3')?.click()}
                        className="text-xs font-bold cursor-pointer inline-flex items-center gap-1.5"
                      >
                        {fileAnalyzing ? (
                          <>
                            <Loader2 className="h-3 w-3 animate-spin" /> Analizando archivo...
                          </>
                        ) : (
                          <>
                            <Upload size={14} /> Examinar archivos locales
                          </>
                        )}
                      </Button>
                    </div>
                  </div>

                  {/* RESULTADOS DE VALIDACIÓN Y DESGLOSE DETALLADO */}
                  {fileValidation && (
                    <div className="mt-4 pt-4 border-t border-slate-200 dark:border-slate-700/80 text-left space-y-3">
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <div className="flex items-center gap-2">
                          <FileCheck
                            size={18}
                            className="text-emerald-600 dark:text-emerald-400 shrink-0"
                          />
                          <span className="text-xs font-bold text-slate-800 dark:text-slate-200">
                            Archivo:{' '}
                            <code className="text-blue-700 dark:text-blue-400">
                              {fileValidation.fileName}
                            </code>
                          </span>
                          <span className="text-[11px] text-slate-500 dark:text-slate-400">
                            ({fileValidation.totalFilas} filas procesadas)
                          </span>
                        </div>
                        {uploadedFile && (
                          <Button
                            type="button"
                            onClick={() => setShowReconciliation(true)}
                            className="bg-emerald-700 hover:bg-emerald-800 text-white font-bold text-xs py-1 px-3 h-auto cursor-pointer"
                          >
                            Revisar Buckets &rarr;
                          </Button>
                        )}
                      </div>

                      {/* Tarjetas de métricas / Resumen de validación */}
                      <div className="grid grid-cols-2 sm:grid-cols-5 gap-2 pt-1">
                        {/* 1. Válidos */}
                        <div className="rounded-xl border border-emerald-200 bg-emerald-50/80 p-2.5 dark:border-emerald-900/60 dark:bg-emerald-950/40">
                          <div className="flex items-center gap-1.5 text-emerald-700 dark:text-emerald-400 text-[11px] font-bold">
                            <CheckCircle2 size={14} /> Válidos
                          </div>
                          <div className="text-lg font-black text-emerald-900 dark:text-emerald-200 mt-1">
                            {fileValidation.totalValidos}
                          </div>
                          <p className="text-[10px] text-emerald-700/80 dark:text-emerald-400/80">
                            Listos para enviar
                          </p>
                        </div>

                        {/* 2. Correos inválidos / rechazados */}
                        <div
                          className={`rounded-xl border p-2.5 ${
                            fileValidation.invalidos.length > 0
                              ? 'border-rose-300 bg-rose-50/80 dark:border-rose-900/60 dark:bg-rose-950/40'
                              : 'border-slate-200 bg-slate-50/50 dark:border-slate-700/50 dark:bg-slate-800/30 opacity-70'
                          }`}
                        >
                          <div className="flex items-center gap-1.5 text-rose-700 dark:text-rose-400 text-[11px] font-bold">
                            <XCircle size={14} /> Inválidos
                          </div>
                          <div className="text-lg font-black text-rose-900 dark:text-rose-200 mt-1">
                            {fileValidation.invalidos.length}
                          </div>
                          <p className="text-[10px] text-rose-700/80 dark:text-rose-400/80">
                            Sintaxis errónea
                          </p>
                        </div>

                        {/* 3. Duplicados */}
                        <div
                          className={`rounded-xl border p-2.5 ${
                            fileValidation.duplicados.length > 0
                              ? 'border-purple-300 bg-purple-50/80 dark:border-purple-900/60 dark:bg-purple-950/40'
                              : 'border-slate-200 bg-slate-50/50 dark:border-slate-700/50 dark:bg-slate-800/30 opacity-70'
                          }`}
                        >
                          <div className="flex items-center gap-1.5 text-purple-700 dark:text-purple-400 text-[11px] font-bold">
                            <Copy size={14} /> Duplicados
                          </div>
                          <div className="text-lg font-black text-purple-900 dark:text-purple-200 mt-1">
                            {fileValidation.duplicados.length}
                          </div>
                          <p className="text-[10px] text-purple-700/80 dark:text-purple-400/80">
                            En el archivo
                          </p>
                        </div>

                        {/* 4. Correos sin nombre */}
                        <div
                          className={`rounded-xl border p-2.5 ${
                            fileValidation.sinNombre.length > 0
                              ? 'border-amber-300 bg-amber-50/80 dark:border-amber-900/60 dark:bg-amber-950/40'
                              : 'border-slate-200 bg-slate-50/50 dark:border-slate-700/50 dark:bg-slate-800/30 opacity-70'
                          }`}
                        >
                          <div className="flex items-center gap-1.5 text-amber-700 dark:text-amber-400 text-[11px] font-bold">
                            <UserX size={14} /> Sin nombre
                          </div>
                          <div className="text-lg font-black text-amber-900 dark:text-amber-200 mt-1">
                            {fileValidation.sinNombre.length}
                          </div>
                          <p className="text-[10px] text-amber-700/80 dark:text-amber-400/80">
                            Falta contacto
                          </p>
                        </div>

                        {/* 5. Contacto sin correo */}
                        <div
                          className={`rounded-xl border p-2.5 ${
                            fileValidation.sinCorreo.length > 0
                              ? 'border-amber-300 bg-amber-50/80 dark:border-amber-900/60 dark:bg-amber-950/40'
                              : 'border-slate-200 bg-slate-50/50 dark:border-slate-700/50 dark:bg-slate-800/30 opacity-70'
                          }`}
                        >
                          <div className="flex items-center gap-1.5 text-amber-700 dark:text-amber-400 text-[11px] font-bold">
                            <MailX size={14} /> Sin correo
                          </div>
                          <div className="text-lg font-black text-amber-900 dark:text-amber-200 mt-1">
                            {fileValidation.sinCorreo.length}
                          </div>
                          <p className="text-[10px] text-amber-700/80 dark:text-amber-400/80">
                            Email vacío
                          </p>
                        </div>
                      </div>

                      {/* Lista desplegable de registros observados / rechazados */}
                      {fileValidation.todasLasIncidencias.length > 0 ? (
                        <div className="mt-2 rounded-xl border border-slate-200 bg-white dark:border-slate-700 dark:bg-slate-900/80 overflow-hidden">
                          <button
                            type="button"
                            onClick={() => setShowIssueDetails(!showIssueDetails)}
                            className="w-full flex items-center justify-between p-2.5 bg-slate-100/70 hover:bg-slate-100 dark:bg-slate-800/70 dark:hover:bg-slate-800 text-left cursor-pointer transition-colors"
                          >
                            <div className="flex items-center gap-2">
                              <AlertTriangle
                                size={15}
                                className="text-amber-600 dark:text-amber-400"
                              />
                              <span className="font-bold text-xs text-slate-800 dark:text-slate-200">
                                Detalle de registros rechazados u observados (
                                {fileValidation.todasLasIncidencias.length})
                              </span>
                            </div>
                            <div className="flex items-center gap-1 text-[11px] font-semibold text-slate-500 dark:text-slate-400">
                              {showIssueDetails ? (
                                <>
                                  Ocultar <ChevronUp size={14} />
                                </>
                              ) : (
                                <>
                                  Ver detalle <ChevronDown size={14} />
                                </>
                              )}
                            </div>
                          </button>

                          {showIssueDetails && (
                            <div className="max-h-52 overflow-y-auto divide-y divide-slate-100 dark:divide-slate-800 text-[11px]">
                              {fileValidation.todasLasIncidencias.map((item, idx) => {
                                let badgeColor =
                                  'bg-rose-100 text-rose-800 dark:bg-rose-950 dark:text-rose-300'
                                let badgeLabel = 'Inválido'
                                if (item.tipo === 'duplicado') {
                                  badgeColor =
                                    'bg-purple-100 text-purple-800 dark:bg-purple-950 dark:text-purple-300'
                                  badgeLabel = 'Duplicado'
                                } else if (item.tipo === 'sin_nombre') {
                                  badgeColor =
                                    'bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300'
                                  badgeLabel = 'Sin nombre'
                                } else if (item.tipo === 'sin_correo') {
                                  badgeColor =
                                    'bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300'
                                  badgeLabel = 'Sin correo'
                                }

                                return (
                                  <div
                                    key={idx}
                                    className="p-2.5 flex items-start justify-between gap-3 hover:bg-slate-50/60 dark:hover:bg-slate-800/40"
                                  >
                                    <div className="space-y-0.5 min-w-0">
                                      <div className="flex items-center gap-2">
                                        <span className="font-bold text-slate-700 dark:text-slate-300">
                                          Fila {item.fila}:
                                        </span>
                                        <span className="font-semibold text-slate-900 dark:text-slate-100 truncate">
                                          {item.nombre !== '(Sin nombre)'
                                            ? item.nombre
                                            : item.email}
                                        </span>
                                        {item.empresa && (
                                          <span className="text-[10px] text-slate-500 dark:text-slate-400 truncate">
                                            · {item.empresa}
                                          </span>
                                        )}
                                      </div>
                                      <p className="text-[10px] text-slate-600 dark:text-slate-400">
                                        {item.detalle}
                                      </p>
                                    </div>
                                    <span
                                      className={`rounded-full px-2 py-0.5 text-[10px] font-bold shrink-0 ${badgeColor}`}
                                    >
                                      {badgeLabel}
                                    </span>
                                  </div>
                                )
                              })}
                            </div>
                          )}
                        </div>
                      ) : (
                        <div className="p-2 bg-emerald-50 text-emerald-800 dark:bg-emerald-950/50 dark:text-emerald-300 rounded-xl text-xs font-semibold flex items-center gap-2">
                          <CheckCircle2 size={15} className="text-emerald-600 shrink-0" />
                          ¡Excelente! Todos los correos y nombres en el archivo son válidos y están
                          completos.
                        </div>
                      )}
                    </div>
                  )}
                </Card>
              )}

              {!origenCrm && !origenArchivo && (
                <p className="rounded-xl border border-dashed border-slate-300 bg-slate-50 p-3 text-center text-[11px] font-semibold text-slate-500 dark:border-slate-700 dark:bg-slate-800/50 dark:text-slate-400">
                  Selecciona al menos un origen de contactos (CRM o lista personalizada) para
                  continuar.
                </p>
              )}

              {wizard.error != null && <ErrorNote error={wizard.error} />}
              {wizard.omitidos != null && idsElegidos && (
                <WarningNote>
                  {wizard.omitidos} de los {idsElegidos.length} contactos elegidos ya no están en el
                  CRM y no recibirán el mensaje.{' '}
                  {wizard.omitidos < idsElegidos.length
                    ? 'Vuelve a lanzar para enviarlo a los demás.'
                    : 'No queda nadie a quien enviarlo.'}
                </WarningNote>
              )}

              <div className="flex justify-between pt-3 border-t border-slate-100 dark:border-slate-800">
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => setStep(2)}
                  className="cursor-pointer"
                >
                  <ArrowLeft size={14} className="mr-1" /> Anterior
                </Button>
                <Button
                  type="submit"
                  disabled={
                    wizard.isPending ||
                    !templateKey ||
                    !textoListo ||
                    wizard.omitidos === idsElegidos?.length ||
                    !canWrite.allowed ||
                    !canLaunch.allowed
                  }
                  title={
                    !textoListo
                      ? 'Escribe el asunto y el texto del mensaje en el paso 2.'
                      : canWrite.allowed
                        ? canLaunch.title
                        : canWrite.title
                  }
                  className="bg-brand-deep text-white font-bold cursor-pointer"
                >
                  {wizard.isPending
                    ? 'Lanzando…'
                    : `Lanzar Campaña (${totalDestinatarios} Destinatarios)`}
                </Button>
              </div>
            </div>
          )}
        </form>
      </div>

      {/* Reconciliación de la carga en 2 pasos, con lo que el servicio contó del archivo */}
      <UploadReconciliationModal
        upload={showReconciliation ? wizard.staged : null}
        isConfirming={wizard.isConfirming}
        error={wizard.confirmError}
        onClose={() => setShowReconciliation(false)}
        onConfirm={() =>
          wizard
            .confirm()
            .then(() => setShowReconciliation(false))
            .catch(() => {})
        }
      />

      {showFormBuilderModal && (
        <PublicFormBuilderModal
          isOpen={showFormBuilderModal}
          onClose={() => setShowFormBuilderModal(false)}
          configInicial={formPersonalizado ?? undefined}
          eventoNombre={eventos.find((e) => e.id === eventoId)?.name}
          onSave={(config) => {
            setFormPersonalizado(config)
          }}
        />
      )}
    </Modal>
  )
}
