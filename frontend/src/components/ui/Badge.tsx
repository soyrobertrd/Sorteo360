import * as React from 'react'
import { cn } from '@/lib/utils'
import { eventoStatusLabel } from '@/lib/labels'
import type { EventoStatus } from '@/api/types'

export interface BadgeProps extends React.HTMLAttributes<HTMLDivElement> {
  variant?:
    | 'default'
    | 'secondary'
    | 'destructive'
    | 'outline'
    | 'amber'
    | 'blue'
    | 'emerald'
    | 'rose'
    | 'slate'
}

export function Badge({ className, variant = 'default', ...props }: BadgeProps) {
  const variantStyles = {
    default:
      'border-slate-200/80 bg-slate-100/90 text-slate-700 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-300',
    secondary:
      'border-transparent bg-slate-200/80 text-slate-800 dark:bg-slate-800 dark:text-slate-200',
    destructive:
      'border-rose-200 bg-rose-50 text-rose-800 dark:border-rose-900/60 dark:bg-rose-950/60 dark:text-rose-300',
    outline:
      'text-slate-700 border-slate-200 bg-transparent dark:border-slate-700 dark:text-slate-300',
    amber:
      'border-amber-200 bg-amber-50 text-amber-800 dark:border-amber-900/60 dark:bg-amber-950/60 dark:text-amber-300',
    blue: 'border-blue-200 bg-blue-50 text-blue-800 dark:border-blue-900/60 dark:bg-blue-950/60 dark:text-blue-300',
    emerald:
      'border-emerald-200 bg-emerald-50 text-emerald-800 dark:border-emerald-900/60 dark:bg-emerald-950/60 dark:text-emerald-300',
    rose: 'border-rose-200 bg-rose-50 text-rose-800 dark:border-rose-900/60 dark:bg-rose-950/60 dark:text-rose-300',
    slate:
      'border-slate-200 bg-slate-100 text-slate-700 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-300',
  }

  return (
    <div
      className={cn(
        'inline-flex items-center rounded-md border px-2.5 py-0.5 font-sans text-[11px] font-bold uppercase tracking-wider transition-colors',
        variantStyles[variant],
        className,
      )}
      {...props}
    />
  )
}

export function EventoStatusBadge({ status }: { status: EventoStatus }) {
  const colorMap: Record<EventoStatus, string> = {
    planning:
      'border-amber-200/90 bg-amber-100/80 text-amber-900 dark:border-amber-900/60 dark:bg-amber-950/80 dark:text-amber-300',
    in_progress:
      'border-blue-200/90 bg-blue-100/80 text-blue-900 dark:border-blue-900/60 dark:bg-blue-950/80 dark:text-blue-300',
    closed:
      'border-slate-300 bg-slate-200 text-slate-800 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-200',
    cancelled:
      'border-rose-200 bg-rose-100 text-rose-800 dark:border-rose-900/60 dark:bg-rose-950/80 dark:text-rose-300',
  }

  return (
    <span
      className={cn(
        'inline-flex items-center rounded-md border px-2.5 py-0.5 font-sans text-[10px] font-extrabold uppercase tracking-wider',
        colorMap[status] || 'border-slate-200 bg-slate-100 text-slate-700',
      )}
    >
      {eventoStatusLabel[status] || status}
    </span>
  )
}
