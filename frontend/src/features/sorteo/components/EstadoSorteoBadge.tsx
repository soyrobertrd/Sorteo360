import type { RaffleState } from '@/api/types'
import { raffleStateLabel } from '@/lib/labels'

const styles: Record<RaffleState, string> = {
  draft:
    'bg-slate-100 text-slate-700 border-slate-200 dark:bg-slate-800 dark:text-slate-300 dark:border-slate-700',
  in_progress:
    'bg-blue-100 text-blue-800 border-blue-200 dark:bg-blue-950/80 dark:text-blue-300 dark:border-blue-900',
  completed:
    'bg-emerald-100 text-emerald-800 border-emerald-200 dark:bg-emerald-950/80 dark:text-emerald-300 dark:border-emerald-900',
}

export function EstadoSorteoBadge({ estado }: { estado: RaffleState }) {
  return (
    <span
      className={`inline-block rounded-md border px-2.5 py-1 font-sans text-[10px] font-extrabold uppercase tracking-wider ${styles[estado]}`}
    >
      {raffleStateLabel[estado]}
    </span>
  )
}
