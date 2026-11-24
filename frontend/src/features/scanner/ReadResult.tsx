import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { AlertCircle, Award, Ban, CheckCircle2, Loader2, Search } from 'lucide-react'
import { ApiError } from '@/api'
import type { PrizeDrawLookupResponse } from '@/api/types'
import { errorMessage } from '@/lib/errorMessage'
import { formatDateTime } from '@/lib/format'
import { prizeDrawStatusLabel, prizeDrawVoidCodeLabel, scannerRefusalTitleFor } from '@/lib/labels'
import { usePrizeDrawByCode } from '@/features/sorteo/hooks'
import { describeRead, type ReadOutcome, type ReadTone, type ReadView } from './readOutcome'

const CARD_TONE: Record<ReadTone, string> = {
  ok: 'bg-emerald-50 border-emerald-200 text-emerald-900',
  warning: 'bg-amber-50 border-amber-200 text-amber-900',
  error: 'bg-rose-50 border-rose-200 text-rose-900',
  info: 'bg-white border-slate-200 text-slate-900',
}

const ICON_TONE: Record<ReadTone, string> = {
  ok: 'text-emerald-600',
  warning: 'text-amber-600',
  error: 'text-rose-600',
  info: 'text-blue-600',
}

function ReadCard({
  view,
  onOpenDoorList,
  children,
}: {
  view: ReadView
  onOpenDoorList?: (search: string) => void
  children?: ReactNode
}) {
  const Icon = view.icon
  const search = view.doorListSearch
  return (
    <div
      role="status"
      className={`p-3.5 rounded-2xl border flex items-start gap-3 shadow-xs ${CARD_TONE[view.tone]}`}
    >
      <Icon
        className={`w-5 h-5 shrink-0 mt-0.5 ${ICON_TONE[view.tone]} ${view.busy ? 'animate-spin' : ''}`}
      />
      <div className="flex-1 min-w-0 space-y-1 text-xs leading-relaxed font-medium">
        <h4 className="font-black uppercase tracking-wider">{view.title}</h4>
        {view.subject && <p className="font-bold text-sm">{view.subject}</p>}
        {view.lines.map((line) => (
          <p key={line}>{line}</p>
        ))}
        {children}
        {search !== undefined && onOpenDoorList && (
          <button
            type="button"
            onClick={() => onOpenDoorList(search)}
            className="mt-1 inline-flex items-center gap-1 text-[11px] font-bold text-blue-700 hover:underline cursor-pointer"
          >
            <Search className="w-3.5 h-3.5" />
            Buscar en la lista de la puerta
          </button>
        )}
      </div>
    </div>
  )
}

/** La tarjeta de la última lectura. */
export function ReadResult({
  outcome,
  onOpenDoorList,
}: {
  outcome: ReadOutcome
  onOpenDoorList: (search: string) => void
}) {
  return outcome.kind === 'prize' ? (
    <PrizeCard code={outcome.code} />
  ) : (
    <ReadCard view={describeRead(outcome)} onOpenDoorList={onOpenDoorList} />
  )
}

/**
 * Lo que la consulta dice de un código de premio. El terminal nunca registra la entrega: la enlaza
 * en la pantalla del sorteo, donde están la firma y el documento (spec convocatorias/10 §3.2).
 */
function PrizeCard({ code }: { code: string }) {
  const lookup = usePrizeDrawByCode(code)

  if (lookup.isPending) {
    return (
      <ReadCard
        view={{
          tone: 'info',
          icon: Loader2,
          busy: true,
          title: 'Consultando el código…',
          lines: [],
        }}
      />
    )
  }
  if (lookup.isError) {
    const refusal = lookup.error instanceof ApiError ? lookup.error.code : undefined
    return (
      <ReadCard
        view={{
          tone: 'error',
          icon: AlertCircle,
          title: scannerRefusalTitleFor(refusal) ?? 'No se pudo consultar el código',
          lines: [errorMessage(lookup.error)],
        }}
      />
    )
  }
  return <PrizeDetail code={code} draw={lookup.data} />
}

function PrizeDetail({ code, draw }: { code: string; draw: PrizeDrawLookupResponse }) {
  return (
    <ReadCard
      view={{
        tone: draw.status === 'voided' ? 'error' : draw.status === 'redeemed' ? 'ok' : 'info',
        icon: draw.status === 'voided' ? Ban : draw.status === 'redeemed' ? CheckCircle2 : Award,
        title: `Premio: ${prizeDrawStatusLabel[draw.status]}`,
        subject: draw.prizeName,
        lines: [`Ganador(a): ${draw.winnerName} • ${draw.winnerCompany ?? 'Particular'}`],
      }}
    >
      <p className="font-mono text-[11px] text-slate-500">{code}</p>
      {draw.void ? (
        <p className="text-rose-800">
          El sorteo de este premio se declaró nulo el {formatDateTime(draw.void.voidedAt)} (
          {prizeDrawVoidCodeLabel[draw.void.code]}). El código ya no da derecho al premio.
        </p>
      ) : draw.handover ? (
        <p className="text-emerald-800">
          Entregado el {formatDateTime(draw.handover.redeemedAt)} a{' '}
          <strong>{draw.handover.receiverName}</strong>.
        </p>
      ) : (
        <Link
          to={`/sorteo/sorteos/${draw.eventId}?entrega=${draw.prizeId}`}
          className="mt-2 flex items-center justify-center gap-1.5 w-full py-2.5 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white font-bold text-xs"
        >
          <CheckCircle2 className="w-4 h-4" />
          Registrar entrega
        </Link>
      )}
    </ReadCard>
  )
}
