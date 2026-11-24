import { useState } from 'react'
import { CheckCircle2, Send, ShieldAlert, XCircle } from 'lucide-react'
import { useCampaign, useCampaignMutations, type CampanaFila } from '../campanas'
import { useCan } from '@/auth/AuthProvider'
import { ROLES } from '@/auth/roles'
import { Button } from '@/components/ui/Button'
import { ErrorNote } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'

interface CampanaApprovalModalProps {
  campana: CampanaFila | null
  onClose: () => void
  onApprove: (campanaId: string) => void
  onReject: (campanaId: string) => void
}

export function CampanaApprovalModal({
  campana,
  onClose,
  onApprove,
  onReject,
}: CampanaApprovalModalProps) {
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [actionSuccess, setActionSuccess] = useState<string | null>(null)
  // Un borrador real de convocatorias-service: aprobar es lanzarlo y rechazar es cancelarlo.
  const { data: fresca } = useCampaign(campana?.campaign?.id)
  const draft = fresca ?? campana?.campaign
  const { launch, cancel } = useCampaignMutations()
  const canLaunch = useCan(ROLES.campaignsLaunch)
  const canWrite = useCan(ROLES.campaignsWrite)

  if (!campana) return null

  const handleApproveAndLaunch = () => {
    if (draft) {
      launch.mutate(
        { id: draft.id, body: { expectedRecipientCount: draft.recipientCount } },
        { onSuccess: onClose },
      )
      return
    }
    setIsSubmitting(true)
    setTimeout(() => {
      setIsSubmitting(false)
      setActionSuccess(
        'Campaña aprobada exitosamente. Los lotes se publicaron a notification-service.',
      )
      setTimeout(() => {
        onApprove(campana.id)
        onClose()
      }, 1000)
    }, 1200)
  }

  const handleRejectCampaign = () => {
    if (draft) {
      cancel.mutate(draft.id, { onSuccess: onClose })
      return
    }
    setIsSubmitting(true)
    setTimeout(() => {
      setIsSubmitting(false)
      onReject(campana.id)
      onClose()
    }, 800)
  }

  return (
    <Modal
      open={Boolean(campana)}
      onClose={onClose}
      title="Aprobación y Lanzamiento de Convocatoria (Rol Directivo)"
      width="max-w-xl"
    >
      <div className="space-y-4 text-xs">
        {actionSuccess && (
          <div className="p-3 rounded-xl bg-emerald-100 border border-emerald-300 text-emerald-900 font-bold flex items-center gap-2 dark:bg-emerald-950 dark:border-emerald-800 dark:text-emerald-300">
            <CheckCircle2 size={16} /> {actionSuccess}
          </div>
        )}

        <div className="rounded-2xl border border-amber-200 bg-amber-50/70 p-4 text-amber-900 dark:border-amber-900/50 dark:bg-amber-950/40 dark:text-amber-300 space-y-2">
          <div className="flex items-center gap-2 font-extrabold text-sm">
            <ShieldAlert size={18} className="text-amber-600" />
            <span>Aprobar es Lanzar (Decisión 08 - Arquitectura)</span>
          </div>
          <p className="leading-relaxed">
            No existe estado posterior de borrador ni edición tras aprobar. En el instante en que
            confirme, el sistema re-verificará la supresión en CRM e iniciará la entrega síncrona de
            correos en lotes.
          </p>
        </div>

        {/* Resumen de la Campaña */}
        <div className="rounded-2xl border border-slate-200 bg-white p-4 dark:border-slate-800 dark:bg-slate-900 space-y-2.5">
          <div className="flex justify-between border-b border-slate-100 pb-2 dark:border-slate-800">
            <span className="text-slate-400 font-bold text-[10px] uppercase">Evento Destino</span>
            <span className="font-extrabold text-slate-900 dark:text-white">
              {campana.eventoNombre}
            </span>
          </div>
          <div className="flex justify-between border-b border-slate-100 pb-2 dark:border-slate-800">
            <span className="text-slate-400 font-bold text-[10px] uppercase">
              Tipo de Convocatoria
            </span>
            <span className="font-bold text-blue-700 dark:text-blue-400">
              {campana.tipo} ({campana.idioma})
            </span>
          </div>
          <div className="flex justify-between">
            <span className="text-slate-400 font-bold text-[10px] uppercase">
              Destinatarios Estimados
            </span>
            <span className="font-black text-slate-900 dark:text-white text-sm">
              {draft?.recipientCount ?? campana.enviados} contactos
            </span>
          </div>
        </div>

        {launch.error != null && <ErrorNote error={launch.error} />}
        {cancel.error != null && <ErrorNote error={cancel.error} />}

        <div className="flex justify-between pt-3 border-t border-slate-100 dark:border-slate-800">
          <Button
            type="button"
            variant="outline"
            onClick={handleRejectCampaign}
            disabled={isSubmitting || cancel.isPending || (draft != null && !canWrite.allowed)}
            title={draft ? canWrite.title : undefined}
            className="border-rose-200 text-rose-700 hover:bg-rose-50 dark:border-rose-900 dark:text-rose-300 font-bold cursor-pointer"
          >
            <XCircle size={14} className="mr-1.5" /> Rechazar Campaña
          </Button>

          <Button
            type="button"
            onClick={handleApproveAndLaunch}
            disabled={isSubmitting || launch.isPending || (draft != null && !canLaunch.allowed)}
            title={draft ? canLaunch.title : undefined}
            className="bg-emerald-700 hover:bg-emerald-800 text-white font-bold cursor-pointer"
          >
            <Send size={14} className="mr-1.5" />{' '}
            {isSubmitting || launch.isPending
              ? 'Verificando & Lanzando…'
              : 'Aprobar & Lanzar Convocatoria'}
          </Button>
        </div>
      </div>
    </Modal>
  )
}
