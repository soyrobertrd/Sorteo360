import { useState } from 'react'
import { useParams } from 'react-router-dom'
import { Ban, RefreshCw, Search, Send } from 'lucide-react'
import { Breadcrumbs } from '@/components/ui/Breadcrumbs'
import { Button } from '@/components/ui/Button'
import { Callout } from '@/components/ui/Callout'
import { Card } from '@/components/ui/Card'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { EmptyState, ErrorNote, Spinner } from '@/components/ui/Feedback'
import { Pagination } from '@/components/ui/Pagination'
import { ProgressBar } from '@/components/ui/ProgressBar'
import { Tabs, type TabItem } from '@/components/ui/Tabs'
import { useCan } from '@/auth/AuthProvider'
import { ROLES } from '@/auth/roles'
import { useEvento } from '@/features/eventos/hooks'
import { formatDate, formatDateTime } from '@/lib/format'
import { campaignTemplateLabel, participantSendStatusLabel } from '@/lib/labels'
import type {
  CampaignResponse,
  ListParticipantsParams,
  ParticipantOrigin,
  ParticipantSendStatus,
} from '@/api/types'
import {
  campanaFilaFrom,
  useCampaign,
  useCampaignCounts,
  useCampaignMutations,
  useCampaignParticipants,
  type CampanaFila,
} from './campanas'
import { CAMPAIGN_STATE_CLASS } from './CampanasPage'
import { CampanaApprovalModal } from './components/CampanaApprovalModal'
import { useEntregas } from './entregas'

const PAGE_SIZE = 20

type Vista = 'todos' | 'registrados' | 'sinRegistrar' | 'entregados' | 'rebotados' | 'abiertos'

/** El filtro del servicio detrás de cada pestaña de destinatarios. */
const FILTRO_VISTA: Record<Vista, Omit<ListParticipantsParams, 'campaignId'>> = {
  todos: {},
  registrados: { registered: true },
  sinRegistrar: { registered: false },
  entregados: { sendStatus: 'handed_off' },
  rebotados: { sendStatus: 'failed' },
  abiertos: {},
}

const SEND_STATUS_CLASS: Record<ParticipantSendStatus, string> = {
  handed_off: 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-300',
  pending: 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300',
  failed: 'bg-rose-50 text-rose-700 dark:bg-rose-950/60 dark:text-rose-300',
  suppressed: 'bg-amber-50 text-amber-700 dark:bg-amber-950/60 dark:text-amber-300',
  none: 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300',
}

const cifra = (n: number | undefined) => (n == null ? '—' : n.toLocaleString())

/**
 * Una campaña por dentro: en qué estado está, cómo va la inscripción de sus destinatarios, a
 * quién no le llegó el correo y por qué, y las acciones que el servicio permite sobre ella.
 */
export function CampanaDetallePage() {
  const { id: eventoId = '', campaignId = '' } = useParams()
  const evento = useEvento(eventoId)
  const campaign = useCampaign(campaignId)
  const [vista, setVista] = useState<Vista>('todos')
  const [porAprobar, setPorAprobar] = useState<CampanaFila | null>(null)
  const [confirmandoCancelar, setConfirmandoCancelar] = useState(false)
  const { cancel, retryFailed } = useCampaignMutations()
  const canWrite = useCan(ROLES.campaignsWrite)
  const { counts, error: countsError } = useCampaignCounts(campaign.data)
  const entregas = useEntregas(campaign.data?.id)

  if (campaign.isLoading || evento.isLoading) return <Spinner label="Cargando la campaña…" />
  if (campaign.error != null || !campaign.data) {
    return <ErrorNote error={campaign.error ?? new Error('Campaña no encontrada')} />
  }

  const c = campaign.data
  const e = evento.data
  const fila = campanaFilaFrom(c, e)
  const nombre = campaignTemplateLabel[c.templateKey] ?? c.templateKey
  const lanzada = Boolean(c.launchedAt)
  const enCurso = c.state === 'sending' || c.state === 'sent'

  const tabs: TabItem<Vista>[] = [
    { key: 'todos', label: 'Todos', badge: <Contador n={c.recipientCount} /> },
    {
      key: 'registrados',
      label: 'Registrados',
      badge: <Contador n={counts.registrados} tone="emerald" />,
    },
    {
      key: 'sinRegistrar',
      label: 'Sin registrarse',
      badge: <Contador n={counts.sinRegistrar} tone="blue" />,
    },
    {
      key: 'entregados',
      label: 'Entregados',
      badge: <Contador n={entregas.counts.entregados} tone="emerald" />,
    },
    {
      key: 'rebotados',
      label: 'Rebotados',
      badge: <Contador n={entregas.counts.rebotados} tone="rose" />,
    },
    { key: 'abiertos', label: 'Abiertos' },
  ]

  return (
    <div className="mx-auto max-w-7xl space-y-6 pb-12">
      <Breadcrumbs
        items={[
          { label: 'Invitaciones', to: '/convocatorias' },
          { label: e?.name ?? 'Evento', to: `/convocatorias/eventos/${eventoId}` },
          { label: nombre },
        ]}
      />

      {/* Estado de la campaña y lo que se puede hacer con ella */}
      <Card className="rounded-3xl border border-slate-200/80 bg-white p-6 shadow-2xs dark:border-slate-800 dark:bg-slate-900">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="space-y-2">
            <span
              className={`inline-block rounded-md px-2.5 py-0.5 text-[11px] font-extrabold ${CAMPAIGN_STATE_CLASS[c.state]}`}
            >
              {fila.estado}
            </span>
            <h1 className="font-sans text-2xl font-extrabold tracking-tight text-slate-900 dark:text-white">
              {nombre}
            </h1>
            <p className="text-xs text-slate-500 dark:text-slate-400">
              {e?.name ?? 'Evento no disponible'} · Idioma {fila.idioma} · Creada el{' '}
              {formatDate(c.createdAt.slice(0, 10))}
              {c.launchedAt && ` · Lanzada el ${formatDateTime(c.launchedAt)}`}
              {c.sentAt && ` · Envío terminado el ${formatDateTime(c.sentAt)}`}
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-2.5">
            {c.state === 'draft' && (
              <Button
                type="button"
                onClick={() => setPorAprobar(fila)}
                className="cursor-pointer bg-amber-600 text-xs font-bold text-white hover:bg-amber-700"
              >
                <Send size={14} className="mr-1.5" /> Aprobar / Lanzar
              </Button>
            )}
            {enCurso && (
              <Button
                type="button"
                onClick={() => retryFailed.mutate(c.id)}
                disabled={!canWrite.allowed || retryFailed.isPending || !counts.fallidos}
                title={
                  canWrite.title ??
                  (counts.fallidos ? undefined : 'No hay envíos fallidos que reintentar')
                }
                className="cursor-pointer bg-brand-deep text-xs font-bold text-white hover:bg-brand-deep/90"
              >
                <RefreshCw size={14} className="mr-1.5" />
                Reintentar envíos fallidos ({cifra(counts.fallidos)})
              </Button>
            )}
            {lanzada && (
              <Button
                type="button"
                variant="outline"
                disabled
                title="Pendiente de backend: todavía no existe una ruta para recordar a quienes no se han registrado"
                className="text-xs font-bold"
              >
                <Send size={14} className="mr-1.5" />
                Reenviar a no registrados ({cifra(counts.sinRegistrar)})
              </Button>
            )}
            {(c.state === 'draft' || c.state === 'sending') && (
              <Button
                type="button"
                variant="outline"
                onClick={() => setConfirmandoCancelar(true)}
                disabled={!canWrite.allowed || cancel.isPending}
                title={canWrite.title}
                className="cursor-pointer text-xs font-bold text-rose-700"
              >
                <Ban size={14} className="mr-1.5" /> Cancelar campaña
              </Button>
            )}
          </div>
        </div>

        {retryFailed.error != null && <ErrorNote error={retryFailed.error} className="mt-4" />}
        {retryFailed.isSuccess && (
          <Callout tone="success" className="mt-4">
            Los envíos fallidos volvieron a la cola. La lista se actualiza cuando el servicio
            termine de procesarlos.
          </Callout>
        )}

        {/* Cómo va el registro de los destinatarios de esta campaña */}
        {lanzada && (
          <ProgressBar
            className="mt-6"
            color="emerald"
            value={counts.registrados ?? 0}
            max={c.recipientCount}
            label="Avance del registro"
            right={`${cifra(counts.registrados)} de ${c.recipientCount.toLocaleString()} destinatarios registrados`}
          />
        )}
      </Card>

      {countsError != null && <ErrorNote error={countsError} />}

      {/* Métricas consolidadas de la campaña en una sola fila */}
      {entregas.error != null && <ErrorNote error={entregas.error} />}
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
        <Cifra label="Destinatarios" value={cifra(c.recipientCount)} />
        <Cifra label="Registrados" value={cifra(counts.registrados)} tone="emerald" />
        <Cifra label="Sin registrarse" value={cifra(counts.sinRegistrar)} tone="blue" />
        {lanzada && entregas.disponible ? (
          <>
            <Cifra label="Entregados" value={cifra(entregas.counts.entregados)} tone="emerald" />
            <Cifra label="Rebotados" value={cifra(entregas.counts.rebotados)} tone="rose" />
            <Cifra label="Abiertos" value="—" hint="Pendiente de backend" />
          </>
        ) : lanzada ? (
          <>
            <Cifra label="Entregados" value="—" hint="Sin permisos" />
            <Cifra label="Rebotados" value="—" hint="Sin permisos" />
            <Cifra label="Abiertos" value="—" hint="Pendiente de backend" />
          </>
        ) : null}
      </div>

      {/* Destinatarios, por situación */}
      <Card className="rounded-3xl border border-slate-200/80 bg-white p-5 shadow-2xs dark:border-slate-800 dark:bg-slate-900">
        <Tabs items={tabs} value={vista} onChange={setVista} size="sm" className="mb-4" />
        <Destinatarios key={vista} campaign={c} filtro={FILTRO_VISTA[vista]} />
      </Card>

      <CampanaApprovalModal
        campana={porAprobar}
        onClose={() => setPorAprobar(null)}
        onApprove={() => {}}
        onReject={() => {}}
      />

      {confirmandoCancelar && (
        <ConfirmDialog
          title="¿Cancelar la campaña?"
          confirmLabel="Cancelar campaña"
          cancelLabel="No cancelar"
          destructive
          pending={cancel.isPending}
          error={cancel.error}
          onConfirm={() => cancel.mutate(c.id, { onSuccess: () => setConfirmandoCancelar(false) })}
          onCancel={() => {
            cancel.reset()
            setConfirmandoCancelar(false)
          }}
        >
          <p>
            La campaña <strong>{nombre}</strong> quedará cancelada y no se puede reactivar.
          </p>
          <p>
            {c.state === 'sending'
              ? 'Los correos que todavía no han salido ya no se envían. Los que ya salieron no se pueden retirar.'
              : 'Es un borrador: no se le ha enviado nada a nadie. Para retomarla habría que crear una campaña nueva.'}
          </p>
        </ConfirmDialog>
      )}
    </div>
  )
}

function Contador({
  n,
  tone = 'default',
}: {
  n: number | undefined
  tone?: 'default' | 'emerald' | 'blue' | 'rose'
}) {
  if (n == null) return null
  const toneClass =
    tone === 'emerald'
      ? 'bg-emerald-500/20 text-emerald-700 dark:text-emerald-300'
      : tone === 'blue'
        ? 'bg-blue-500/20 text-blue-700 dark:text-blue-300'
        : tone === 'rose'
          ? 'bg-rose-500/20 text-rose-700 dark:text-rose-300'
          : 'bg-black/10 dark:bg-white/15 text-inherit'

  return (
    <span
      className={`ml-1.5 inline-flex items-center justify-center rounded-full px-2 py-0.5 text-[11px] font-bold ${toneClass}`}
    >
      {n.toLocaleString()}
    </span>
  )
}

const CIFRA_TONE = {
  slate: 'text-slate-900 dark:text-white',
  emerald: 'text-emerald-600 dark:text-emerald-400',
  blue: 'text-blue-600 dark:text-blue-400',
  rose: 'text-rose-600 dark:text-rose-400',
  amber: 'text-amber-600 dark:text-amber-400',
}

function Cifra({
  label,
  value,
  hint,
  tone = 'slate',
}: {
  label: string
  value: string
  hint?: string
  tone?: keyof typeof CIFRA_TONE
}) {
  return (
    <div className="rounded-2xl border border-slate-200/80 bg-white p-4 shadow-2xs dark:border-slate-800 dark:bg-slate-900">
      <p className={`font-sans text-xl font-extrabold ${CIFRA_TONE[tone]}`}>{value}</p>
      <p className="mt-0.5 text-[10px] font-bold uppercase tracking-wider text-slate-400">
        {label}
      </p>
      {hint && <p className="mt-0.5 text-[10px] text-slate-400">{hint}</p>}
    </div>
  )
}

const PARTICIPANT_ORIGIN_LABEL: Record<ParticipantOrigin, string> = {
  invited: 'Invitación directa',
  referred: 'Referido',
  walk_in: 'Registro libre',
}

const PARTICIPANT_ORIGIN_BADGE: Record<ParticipantOrigin, string> = {
  invited:
    'bg-blue-50 text-blue-700 dark:bg-blue-950/60 dark:text-blue-300 border-blue-200 dark:border-blue-800',
  referred:
    'bg-purple-50 text-purple-700 dark:bg-purple-950/60 dark:text-purple-300 border-purple-200 dark:border-purple-800',
  walk_in:
    'bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300 border-slate-200 dark:border-slate-700',
}

function Destinatarios({
  campaign,
  filtro,
}: {
  campaign: CampaignResponse
  filtro: Omit<ListParticipantsParams, 'campaignId'>
}) {
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const { data, error, isPending } = useCampaignParticipants(campaign, {
    ...filtro,
    search: search.trim() || undefined,
    page,
    pageSize: PAGE_SIZE,
  })

  return (
    <div className="space-y-3">
      <div className="relative max-w-sm">
        <Search size={15} className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
        <input
          type="search"
          value={search}
          onChange={(ev) => {
            setSearch(ev.target.value)
            setPage(1)
          }}
          placeholder="Buscar por nombre o correo…"
          aria-label="Buscar destinatario"
          className="w-full rounded-xl border border-slate-200 bg-slate-50 py-2 pl-9 pr-3 text-xs text-slate-900 focus:border-blue-600 focus:outline-none dark:border-slate-700 dark:bg-slate-800 dark:text-white"
        />
      </div>

      {error != null && <ErrorNote error={error} />}
      {isPending ? (
        <Spinner label="Cargando destinatarios…" />
      ) : !data || data.items.length === 0 ? (
        <EmptyState>No hay destinatarios en esta situación.</EmptyState>
      ) : (
        <div className="overflow-x-auto rounded-xl border border-slate-200 dark:border-slate-800">
          <table className="w-full text-left text-xs">
            <thead className="bg-slate-50 font-bold uppercase tracking-wider text-slate-500 dark:bg-slate-800">
              <tr>
                <th className="px-3 py-2.5">Destinatario</th>
                <th className="px-3 py-2.5">Empresa</th>
                <th className="px-3 py-2.5">Origen del contacto</th>
                <th className="px-3 py-2.5">Envío</th>
                <th className="px-3 py-2.5">Registro</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
              {data.items.map((p) => (
                <tr key={p.email}>
                  <td className="px-3 py-2.5">
                    <p className="font-bold text-slate-900 dark:text-white">
                      {`${p.firstName} ${p.lastName}`.trim() || '—'}
                    </p>
                    <p className="text-[11px] text-slate-500">{p.email}</p>
                  </td>
                  <td className="px-3 py-2.5 text-slate-600 dark:text-slate-300">
                    {p.company || '—'}
                  </td>
                  <td className="px-3 py-2.5">
                    <span
                      className={`inline-flex items-center rounded-full border px-2 py-0.5 text-[10px] font-bold ${
                        PARTICIPANT_ORIGIN_BADGE[p.origin] ?? 'bg-slate-100 text-slate-700'
                      }`}
                    >
                      {PARTICIPANT_ORIGIN_LABEL[p.origin] ?? p.origin}
                    </span>
                    {p.origin === 'referred' && p.referredBy?.name && (
                      <span className="block text-[10px] text-slate-400">
                        por {p.referredBy.name}
                      </span>
                    )}
                  </td>
                  <td className="px-3 py-2.5">
                    <span
                      className={`rounded-full px-2 py-0.5 text-[10px] font-extrabold ${SEND_STATUS_CLASS[p.sendStatus]}`}
                    >
                      {participantSendStatusLabel[p.sendStatus]}
                    </span>
                  </td>
                  <td className="px-3 py-2.5">
                    {p.registered ? (
                      <span className="font-bold text-emerald-700 dark:text-emerald-400">
                        Registrado
                        {p.registeredAt ? ` el ${formatDate(p.registeredAt.slice(0, 10))}` : ''}
                      </span>
                    ) : (
                      <span className="text-slate-500">Sin registrarse</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <Pagination
        page={page}
        pageCount={Math.max(1, Math.ceil((data?.total ?? 0) / PAGE_SIZE))}
        onPageChange={setPage}
        total={data?.total}
      />
    </div>
  )
}
