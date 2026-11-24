import { Trophy } from 'lucide-react'
import { PARTICIPANT_STATUS_LABEL, type ParticipantStatus } from '../mockParticipants'

const STATUS_CLASS: Record<ParticipantStatus, string> = {
  INVITED:
    'border-slate-200 bg-slate-100 text-slate-700 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-300',
  REGISTERED:
    'border-blue-200 bg-blue-50 text-blue-800 dark:border-blue-900/60 dark:bg-blue-950/60 dark:text-blue-300',
  CHECKED_IN:
    'border-teal-200 bg-teal-50 text-teal-800 dark:border-teal-900/60 dark:bg-teal-950/60 dark:text-teal-300',
  NO_ASISTIRE:
    'border-rose-200 bg-rose-50 text-rose-800 dark:border-rose-900/60 dark:bg-rose-950/60 dark:text-rose-300',
  RSVP_YES:
    'border-blue-200 bg-blue-50 text-blue-800 dark:border-blue-900/60 dark:bg-blue-950/60 dark:text-blue-300',
  RSVP_NO:
    'border-rose-200 bg-rose-50 text-rose-800 dark:border-rose-900/60 dark:bg-rose-950/60 dark:text-rose-300',
  RSVP_MAYBE:
    'border-slate-200 bg-slate-100 text-slate-700 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-300',
  WAITLIST:
    'border-amber-200 bg-amber-50 text-amber-800 dark:border-amber-900/60 dark:bg-amber-950/60 dark:text-amber-300',
  CONFIRMED:
    'border-blue-200 bg-blue-50 text-blue-800 dark:border-blue-900/60 dark:bg-blue-950/60 dark:text-blue-300',
  NO_SHOW:
    'border-rose-200 bg-rose-50 text-rose-800 dark:border-rose-900/60 dark:bg-rose-950/60 dark:text-rose-300',
  CANCELLED:
    'border-slate-200 bg-slate-100 text-slate-500 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-400',
  RAFFLE_ELIGIBLE:
    'border-purple-200 bg-purple-50 text-purple-800 dark:border-purple-900/60 dark:bg-purple-950/60 dark:text-purple-300',
  RAFFLE_WINNER:
    'border-amber-300 bg-amber-100 text-amber-900 dark:border-amber-800 dark:bg-amber-950/80 dark:text-amber-300',
}

export function EstadoParticipanteBadge({ status }: { status: ParticipantStatus }) {
  return (
    <span
      className={`inline-flex items-center gap-1 rounded-full border px-2.5 py-0.5 text-[10px] font-extrabold uppercase tracking-wider ${STATUS_CLASS[status]}`}
    >
      {status === 'RAFFLE_WINNER' && <Trophy size={10} />}
      {PARTICIPANT_STATUS_LABEL[status]}
    </span>
  )
}
