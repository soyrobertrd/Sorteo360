import { useMemo, useState } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import {
  CalendarDays,
  ChevronRight,
  ExternalLink,
  Languages,
  Mail,
  MailX,
  MapPin,
  Plus,
  ShieldOff,
  UserCheck,
  UserX,
} from 'lucide-react'
import { Breadcrumbs } from '@/components/ui/Breadcrumbs'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { DataTable } from '@/components/ui/DataTable'
import { Tabs } from '@/components/ui/Tabs'
import { EmptyState, ErrorNote, Spinner } from '@/components/ui/Feedback'
import { StatCard } from '@/components/ui/StatCard'
import { useCan } from '@/auth/AuthProvider'
import { ROLES } from '@/auth/roles'
import { useEvento } from '@/features/eventos/hooks'
import { formatDate, formatDateRange } from '@/lib/format'
import { campaignTemplateLabel, eventoStatusLabel, idiomaLabel } from '@/lib/labels'
import type { CampaignResponse } from '@/api/types'
import { campanaFilaFrom, useCampaigns, type CampanaFila } from './campanas'
import { CAMPAIGN_STATE_CLASS, NuevaCampanaModal } from './CampanasPage'
import { CampanaApprovalModal } from './components/CampanaApprovalModal'
import { useEventTracking } from './tracking'
import { useInvitadosDeEventos } from './participants'
import { MesasSillasCard } from './components/asientos/MesasSillasCard'
import { invitadosDemo, useInvitadosDemo } from './asientosDemo'
import { puedeSentarse } from './mesas'
import { InvitadosEventoDetailPage } from './InvitadosEventoDetailPage'
import { CheckinPage } from './CheckinPage'
import { SectionHeader } from '@/components/ui/SectionHeader'

/** El servicio pagina a 100 como tope; un evento con más campañas que eso no existe todavía. */
const CAMPANAS_DEL_EVENTO = 100

type TabEvento = 'campanas' | 'invitados' | 'asientos' | 'checkin'
const TABS_EVENTO: [TabEvento, string][] = [
  ['campanas', 'Campañas'],
  ['invitados', 'Invitados'],
  ['asientos', 'Asientos'],
  ['checkin', 'Check-in'],
]

const fechaDe = (c: CampaignResponse) => (c.sentAt ?? c.launchedAt)?.slice(0, 10) ?? null

/**
 * La convocatoria de un evento: sus datos, cómo va la inscripción y todas sus campañas, en
 * cualquier estado. Es a donde lleva "Gestionar campañas" desde la ficha del evento y a donde
 * llevan los eventos del módulo de Convocatorias.
 */
export function ConvocatoriaEventoPage() {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const evento = useEvento(id)
  const tracking = useEventTracking(id)
  const campanas = useCampaigns({ eventId: id, pageSize: CAMPANAS_DEL_EVENTO })
  const canWrite = useCan(ROLES.campaignsWrite)
  const [showCreate, setShowCreate] = useState(false)
  const [porAprobar, setPorAprobar] = useState<CampanaFila | null>(null)
  // La pestaña va en la URL para que un enlace o volver atrás abra la misma.
  const [searchParams, setSearchParams] = useSearchParams()
  const tab = (searchParams.get('tab') as TabEvento | null) ?? 'campanas'
  const eventosDeLaLista = useMemo(() => (evento.data ? [evento.data] : []), [evento.data])
  const participantes = useInvitadosDeEventos(eventosDeLaLista)
  const demo = useInvitadosDemo(id)
  const conDemo = demo.activo && evento.data != null
  const invitadosAsientos = useMemo(
    () => (conDemo && evento.data ? invitadosDemo(evento.data) : participantes.invitados),
    [conDemo, evento.data, participantes.invitados],
  )
  const sinRegistrados = !participantes.invitados.some(puedeSentarse)

  // Las más recientes primero; un borrador no tiene fecha de envío y se ordena por su alta.
  const filas = useMemo(
    () =>
      (campanas.data?.items ?? []).toSorted((a, b) =>
        (b.launchedAt ?? b.createdAt).localeCompare(a.launchedAt ?? a.createdAt),
      ),
    [campanas.data],
  )

  if (evento.isLoading) return <Spinner label="Cargando la convocatoria del evento…" />
  if (evento.error != null || !evento.data) {
    return <ErrorNote error={evento.error ?? new Error('Evento no encontrado')} />
  }

  const e = evento.data
  const terminado = e.status === 'closed' || e.status === 'cancelled'
  const sede = e.cities[0]
  const resumen = tracking.data
  const cifra = (n: number | undefined) => (n == null ? '—' : n.toLocaleString())

  return (
    <div className="mx-auto max-w-7xl space-y-6 pb-12">
      <Breadcrumbs items={[{ label: 'Invitaciones', to: '/convocatorias' }, { label: e.name }]} />

      {/* Datos del evento */}
      <Card className="rounded-3xl border border-slate-200/80 bg-white p-6 shadow-2xs dark:border-slate-800 dark:bg-slate-900">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="min-w-0 space-y-2">
            <div className="flex flex-wrap items-center gap-2">
              <span className="rounded-md bg-slate-100 px-2.5 py-0.5 text-[11px] font-extrabold text-slate-700 dark:bg-slate-800 dark:text-slate-300">
                {eventoStatusLabel[e.status]}
              </span>
              {resumen && (
                <span
                  className={`rounded-md px-2.5 py-0.5 text-[11px] font-extrabold ${
                    resumen.registrationWindow.open
                      ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300'
                      : 'bg-rose-100 text-rose-800 dark:bg-rose-950 dark:text-rose-300'
                  }`}
                >
                  {resumen.registrationWindow.open ? 'Registro abierto' : 'Registro cerrado'}
                </span>
              )}
            </div>
            <h1 className="font-sans text-2xl font-extrabold tracking-tight text-slate-900 sm:text-3xl dark:text-white">
              {e.name}
            </h1>
            <div className="flex flex-wrap gap-x-5 gap-y-1.5 text-xs text-slate-600 dark:text-slate-300">
              <span className="flex items-center gap-1.5">
                <CalendarDays size={14} className="shrink-0 text-slate-400" />
                {formatDateRange(e.startDate, e.endDate)}
              </span>
              {sede && (
                <span className="flex items-center gap-1.5">
                  <MapPin size={14} className="shrink-0 text-slate-400" />
                  {sede.name}, {sede.country}
                </span>
              )}
              {e.language && (
                <span className="flex items-center gap-1.5">
                  <Languages size={14} className="shrink-0 text-slate-400" />
                  {idiomaLabel[e.language]}
                </span>
              )}
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-2.5">
            <Link to={`/eventos/${e.id}`}>
              <Button type="button" variant="outline" className="cursor-pointer text-xs font-bold">
                <ExternalLink size={14} className="mr-1.5" /> Ver ficha del evento
              </Button>
            </Link>
            <Button
              type="button"
              onClick={() => setShowCreate(true)}
              disabled={terminado || !canWrite.allowed}
              title={terminado ? 'El evento ya terminó: no admite campañas nuevas' : canWrite.title}
              className="cursor-pointer bg-brand-deep text-xs font-bold text-white shadow-sm hover:bg-brand-deep/90"
            >
              <Plus size={16} className="mr-1 stroke-[2.5]" /> Crear nueva campaña
            </Button>
          </div>
        </div>
      </Card>

      {/* Cómo va la inscripción del evento, sumando todas sus campañas lanzadas */}
      {tracking.error != null && <ErrorNote error={tracking.error} />}
      <div className="grid grid-cols-2 gap-3 sm:gap-4 lg:grid-cols-5">
        <StatCard
          icon={Mail}
          label="Invitados"
          value={cifra(resumen?.people.invited)}
          color="violet"
        />
        <StatCard
          icon={UserCheck}
          label="Inscritos"
          value={cifra(resumen?.registrations.total)}
          color="emerald"
        />
        <StatCard
          icon={UserX}
          label="No inscrito"
          value={cifra(resumen?.notRegistered)}
          color="blue"
        />
        <StatCard
          icon={MailX}
          label="Envíos fallidos"
          value={cifra(resumen?.people.failed)}
          color="rose"
        />
        <StatCard
          icon={ShieldOff}
          label="Suprimidos"
          value={cifra(resumen?.people.suppressed)}
          color="amber"
        />
      </div>

      <Tabs
        variant="card"
        items={TABS_EVENTO.map(([key, label]) => ({ key, label }))}
        value={tab}
        onChange={(clave) => setSearchParams({ tab: clave }, { replace: true })}
      />

      {tab === 'campanas' && (
        <>
          <SectionHeader
            title="Campañas del evento"
            description="Todas, en cualquier estado. Haz clic en una para ver su avance y sus destinatarios."
            actions={
              campanas.data ? (
                <span className="text-xs font-bold text-slate-400">
                  {campanas.data.total} campañas
                </span>
              ) : undefined
            }
          />
          <Card className="overflow-hidden rounded-3xl border border-slate-200/80 bg-white p-0 shadow-2xs dark:border-slate-800 dark:bg-slate-900">
            {campanas.error != null && (
              <div className="p-5">
                <ErrorNote error={campanas.error} />
              </div>
            )}
            {campanas.isPending ? (
              <Spinner label="Cargando campañas…" />
            ) : filas.length === 0 ? (
              <div className="p-5">
                <EmptyState>
                  Este evento todavía no tiene campañas. Crea la primera con "Crear nueva campaña".
                </EmptyState>
              </div>
            ) : (
              <DataTable<CampaignResponse>
                rows={filas}
                getRowKey={(c) => c.id}
                onRowClick={(c) => navigate(`/convocatorias/eventos/${e.id}/campanas/${c.id}`)}
                columns={[
                  {
                    key: 'campana',
                    header: 'Campaña',
                    cell: (c) => (
                      <Link
                        to={`/convocatorias/eventos/${e.id}/campanas/${c.id}`}
                        onClick={(ev) => ev.stopPropagation()}
                        className="font-bold text-slate-900 hover:text-blue-600 dark:text-white dark:hover:text-blue-400"
                      >
                        {campaignTemplateLabel[c.templateKey] ?? c.templateKey}
                      </Link>
                    ),
                  },
                  {
                    key: 'idioma',
                    header: 'Idioma',
                    cellClassName: 'font-mono font-bold text-slate-500',
                    cell: (c) => campanaFilaFrom(c, e).idioma,
                  },
                  {
                    key: 'creada',
                    header: 'Creada',
                    cellClassName: 'text-slate-500',
                    cell: (c) => formatDate(c.createdAt.slice(0, 10)),
                  },
                  {
                    key: 'enviada',
                    header: 'Enviada',
                    cellClassName: 'text-slate-500',
                    cell: (c) => (fechaDe(c) ? formatDate(fechaDe(c)) : '—'),
                  },
                  {
                    key: 'destinatarios',
                    header: 'Destinatarios',
                    align: 'center',
                    cellClassName: 'font-bold text-slate-900 dark:text-white',
                    cell: (c) => c.recipientCount.toLocaleString(),
                  },
                  {
                    key: 'excluidos',
                    header: 'Excluidos',
                    align: 'center',
                    cellClassName: 'text-slate-600 dark:text-slate-300',
                    cell: (c) => c.exclusionCount.toLocaleString(),
                  },
                  {
                    key: 'estado',
                    header: 'Estado',
                    align: 'center',
                    cell: (c) => (
                      <span
                        className={`rounded-md px-2.5 py-0.5 text-[11px] font-extrabold ${CAMPAIGN_STATE_CLASS[c.state]}`}
                      >
                        {campanaFilaFrom(c, e).estado}
                      </span>
                    ),
                  },
                  {
                    key: 'acciones',
                    header: 'Acciones',
                    align: 'right',
                    cell: (c) => (
                      <div className="flex items-center justify-end gap-2">
                        {c.state === 'draft' && (
                          <Button
                            type="button"
                            size="sm"
                            onClick={(ev) => {
                              ev.stopPropagation()
                              setPorAprobar(campanaFilaFrom(c, e))
                            }}
                            className="cursor-pointer bg-amber-600 text-xs font-bold text-white hover:bg-amber-700"
                          >
                            Aprobar / Lanzar
                          </Button>
                        )}
                        <ChevronRight size={16} className="text-slate-300" />
                      </div>
                    ),
                  },
                ]}
              />
            )}
          </Card>
        </>
      )}

      {tab === 'invitados' && (
        <>
          <SectionHeader
            title="Invitados del evento"
            description="Quiénes recibieron una campaña, quiénes se registraron y los cupos por segmento."
          />
          <InvitadosEventoDetailPage embebido />
        </>
      )}

      {tab === 'asientos' && (conDemo || sinRegistrados) && (
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-dashed border-amber-300 bg-amber-50 px-4 py-2.5 text-xs text-amber-900 dark:border-amber-800 dark:bg-amber-950/30 dark:text-amber-200">
          <span className="min-w-0 flex-1">
            {[
              conDemo
                ? 'Estás viendo invitados de prueba.'
                : sinRegistrados && 'Este evento no tiene registrados todavía.',
              'Lo de prueba solo existe en este navegador y no se guarda en el servicio.',
            ]
              .filter(Boolean)
              .join(' ')}
          </span>
          <div className="flex flex-wrap items-center gap-2">
            <Button size="sm" variant="outline" onClick={() => demo.cambiar(!conDemo)}>
              {conDemo ? 'Quitar invitados de prueba' : 'Usar invitados de prueba'}
            </Button>
          </div>
        </div>
      )}

      {tab === 'asientos' && (
        <MesasSillasCard
          eventoId={e.id}
          montaje={e.seatingLayout}
          invitados={invitadosAsientos}
          listo={conDemo || (!participantes.isLoading && participantes.error == null)}
        />
      )}

      {tab === 'checkin' && <CheckinPage eventoIdFijo={e.id} />}

      <CampanaApprovalModal
        campana={porAprobar}
        onClose={() => setPorAprobar(null)}
        onApprove={() => {}}
        onReject={() => {}}
      />

      {showCreate && (
        <NuevaCampanaModal
          eventos={[e]}
          fijarEvento
          onClose={() => setShowCreate(false)}
          onLaunched={() => setShowCreate(false)}
        />
      )}
    </div>
  )
}
