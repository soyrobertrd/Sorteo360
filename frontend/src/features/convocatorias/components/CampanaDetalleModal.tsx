import { useMemo } from 'react'
import { RefreshCw, CalendarPlus, Mail, MailCheck, MousePointerClick, MailX } from 'lucide-react'
import { Modal } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { ErrorNote, Spinner } from '@/components/ui/Feedback'
import { formatDate } from '@/lib/format'
import { downloadIcs } from '@/lib/ics'
import { campaignExclusionReasonLabel, participantSendStatusLabel } from '@/lib/labels'
import type { ParticipantSendStatus } from '@/api/types'
import { destinatariosFor, type DestinatarioEstado } from '../mockParticipants'
import {
  useCampaign,
  useCampaignExclusions,
  useCampaignRecipients,
  type CampanaFila,
} from '../campanas'

type EstadoStyle = { icon: typeof Mail; className: string }

const ESTADO_STYLE: Record<DestinatarioEstado, EstadoStyle> = {
  Entregado: {
    icon: Mail,
    className: 'text-slate-600 bg-slate-100 dark:bg-slate-800 dark:text-slate-300',
  },
  Abierto: {
    icon: MailCheck,
    className: 'text-blue-700 bg-blue-50 dark:bg-blue-950/60 dark:text-blue-300',
  },
  Click: {
    icon: MousePointerClick,
    className: 'text-emerald-700 bg-emerald-50 dark:bg-emerald-950/60 dark:text-emerald-300',
  },
  Rebotado: {
    icon: MailX,
    className: 'text-rose-700 bg-rose-50 dark:bg-rose-950/60 dark:text-rose-300',
  },
}

/** Estado de envío real de un destinatario: hasta dónde llegó su correo de nuestro lado. */
const SEND_STATUS_STYLE: Record<ParticipantSendStatus, EstadoStyle> = {
  handed_off: ESTADO_STYLE.Click,
  pending: ESTADO_STYLE.Entregado,
  failed: ESTADO_STYLE.Rebotado,
  suppressed: {
    icon: MailX,
    className: 'text-amber-700 bg-amber-50 dark:bg-amber-950/60 dark:text-amber-300',
  },
  none: ESTADO_STYLE.Entregado,
}

type FilaDestinatario = { key: string; nombre: string; email: string; estado: string } & EstadoStyle

const numero = (n: number | null) => (n == null ? '—' : n)

export function CampanaDetalleModal({
  campana,
  evento,
  onClose,
  onReenviarRebotados,
}: {
  campana: CampanaFila
  evento?: { name: string; startDate: string; endDate: string; sede?: string }
  onClose: () => void
  onReenviarRebotados: () => void
}) {
  // Una campaña real se relee (y sigue sola mientras esté en envío); una simulada no tiene detrás nada.
  const { data: fresca } = useCampaign(campana.campaign?.id)
  const campaign = fresca ?? campana.campaign
  const recipients = useCampaignRecipients(campaign)
  const exclusions = useCampaignExclusions(campaign?.id)

  const destinatarios = useMemo<FilaDestinatario[]>(() => {
    if (campaign) {
      return (recipients.data?.items ?? []).map((p) => ({
        key: p.email,
        nombre: `${p.firstName} ${p.lastName}`.trim(),
        email: p.email,
        estado: participantSendStatusLabel[p.sendStatus],
        ...SEND_STATUS_STYLE[p.sendStatus],
      }))
    }
    return destinatariosFor({ ...campana, rebotados: campana.rebotados ?? 0 }).map((d) => ({
      key: d.email,
      nombre: d.nombre,
      email: d.email,
      estado: d.estado,
      ...ESTADO_STYLE[d.estado],
    }))
  }, [campaign, campana, recipients.data])

  /** `excludedByReason`: cuántos quedaron fuera por cada motivo. */
  const excludedByReason = useMemo(() => {
    const counts: Record<string, number> = {}
    for (const row of exclusions.data?.items ?? [])
      counts[row.reason] = (counts[row.reason] ?? 0) + 1
    return Object.entries(counts)
  }, [exclusions.data])

  const enviados = campaign ? campaign.recipientCount : campana.enviados
  const totalDestinatarios = campaign ? (recipients.data?.total ?? 0) : destinatarios.length

  const handleDescargarIcs = () => {
    if (!evento) return
    downloadIcs(`convocatoria-${campana.eventoId}`, {
      title: evento.name,
      description: `Convocatoria oficial · Campaña: ${campana.tipo}`,
      location: evento.sede,
      startDate: evento.startDate,
      endDate: evento.endDate,
    })
  }

  return (
    <Modal title={`Detalle de envío · ${campana.tipo}`} onClose={onClose} width="max-w-2xl">
      <div className="space-y-4 text-xs">
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-100 pb-3 dark:border-slate-800">
          <div>
            <p className="font-bold text-slate-900 dark:text-white">{campana.eventoNombre}</p>
            <p className="text-slate-500">
              {campana.idioma} ·{' '}
              {campaign && !campaign.launchedAt
                ? campana.estado
                : `Enviada el ${formatDate((campaign?.sentAt ?? campaign?.launchedAt)?.slice(0, 10) ?? campana.fechaEnvio)}`}
            </p>
            {campaign?.subject && (
              <p className="text-slate-500">
                Asunto: <span className="font-bold">{campaign.subject}</span>
              </p>
            )}
          </div>
          <div className="flex items-center gap-2">
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={handleDescargarIcs}
              disabled={!evento}
              title={!evento ? 'Evento no disponible' : undefined}
              className="cursor-pointer font-bold"
            >
              <CalendarPlus size={14} className="mr-1.5" /> Descargar .ics
            </Button>
            <Button
              type="button"
              size="sm"
              onClick={onReenviarRebotados}
              disabled={!campana.rebotados}
              className="cursor-pointer bg-amber-600 font-bold text-white hover:bg-amber-700"
            >
              <RefreshCw size={14} className="mr-1.5" />
              {`Reenviar rebotados (${numero(campana.rebotados)})`}
            </Button>
          </div>
        </div>

        <div className="grid grid-cols-4 gap-2 text-center">
          <div className="rounded-xl border border-slate-200 p-2.5 dark:border-slate-800">
            <p className="font-sans text-base font-extrabold text-slate-900 dark:text-white">
              {enviados}
            </p>
            <p className="text-[10px] font-bold uppercase text-slate-400">Enviados</p>
          </div>
          <div className="rounded-xl border border-slate-200 p-2.5 dark:border-slate-800">
            <p className="font-sans text-base font-extrabold text-emerald-600">
              {numero(campana.entregados)}
            </p>
            <p className="text-[10px] font-bold uppercase text-slate-400">Entregados</p>
          </div>
          <div className="rounded-xl border border-slate-200 p-2.5 dark:border-slate-800">
            <p className="font-sans text-base font-extrabold text-amber-600">
              {numero(campana.rebotados)}
            </p>
            <p className="text-[10px] font-bold uppercase text-slate-400">Rebotados</p>
          </div>
          <div className="rounded-xl border border-slate-200 p-2.5 dark:border-slate-800">
            <p className="font-sans text-base font-extrabold text-blue-600">
              {numero(campana.reenviados)}
            </p>
            <p className="text-[10px] font-bold uppercase text-slate-400">Reenviados</p>
          </div>
        </div>

        <div>
          <p className="mb-1.5 font-bold text-slate-800 dark:text-slate-200">
            Muestra de destinatarios ({totalDestinatarios})
          </p>
          {recipients.error != null && <ErrorNote error={recipients.error} />}
          {campaign && recipients.isPending && <Spinner label="Cargando destinatarios…" />}
          <div className="max-h-72 overflow-y-auto rounded-xl border border-slate-200 dark:border-slate-800">
            <table className="w-full text-left">
              <thead className="sticky top-0 bg-slate-50 text-slate-500 uppercase tracking-wider font-bold dark:bg-slate-800">
                <tr>
                  <th className="px-3 py-2">Destinatario</th>
                  <th className="px-3 py-2 text-right">Estado del envío</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                {destinatarios.map((d) => {
                  const Icon = d.icon
                  return (
                    <tr key={d.key}>
                      <td className="px-3 py-2">
                        <p className="font-bold text-slate-900 dark:text-white">{d.nombre}</p>
                        <p className="text-[11px] text-slate-500">{d.email}</p>
                      </td>
                      <td className="px-3 py-2 text-right">
                        <span
                          className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[10px] font-extrabold ${d.className}`}
                        >
                          <Icon size={11} /> {d.estado}
                        </span>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        </div>

        {campaign && (
          <div>
            <p className="mb-1.5 font-bold text-slate-800 dark:text-slate-200">
              Excluidos por la selección ({exclusions.data?.total ?? campaign.exclusionCount})
            </p>
            {exclusions.error != null && <ErrorNote error={exclusions.error} />}
            {excludedByReason.length > 0 && (
              <p className="mb-1.5 text-[11px] text-slate-500">
                {excludedByReason
                  .map(([reason, n]) => `${campaignExclusionReasonLabel[reason] ?? reason}: ${n}`)
                  .join(' · ')}
              </p>
            )}
            {(exclusions.data?.items.length ?? 0) > 0 && (
              <div className="max-h-48 overflow-y-auto rounded-xl border border-slate-200 dark:border-slate-800">
                <table className="w-full text-left">
                  <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                    {exclusions.data!.items.map((x) => (
                      <tr key={x.id}>
                        <td className="px-3 py-2">
                          <p className="font-bold text-slate-900 dark:text-white">
                            {`${x.firstName} ${x.lastName}`.trim() || '—'}
                          </p>
                          <p className="text-[11px] text-slate-500">{x.email ?? 'Sin correo'}</p>
                        </td>
                        <td className="px-3 py-2 text-right text-[11px] text-rose-600">
                          {campaignExclusionReasonLabel[x.reason] ?? x.reason}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        )}
      </div>
    </Modal>
  )
}
