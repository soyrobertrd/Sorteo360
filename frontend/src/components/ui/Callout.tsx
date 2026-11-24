import type { ReactNode } from 'react'
import {
  CircleAlert,
  CircleCheck,
  Info,
  Lightbulb,
  OctagonAlert,
  X,
  type LucideIcon,
} from 'lucide-react'
import { cn } from '@/lib/utils'

export type CalloutTone = 'info' | 'success' | 'warning' | 'danger' | 'tip' | 'neutral'

/**
 * Caja de aviso en línea del sistema. Reemplaza los ~130 `<div>` a mano con
 * `rounded-xl border border-amber-200 bg-amber-50 …`. Para el error de una mutación
 * usa `ErrorNote`; para un aviso flotante usa `ToastNotification`.
 */
const toneConfig: Record<CalloutTone, { box: string; icon: LucideIcon; iconColor: string }> = {
  info: {
    box: 'border-blue-200/80 bg-blue-50/70 text-blue-950 dark:border-blue-900/70 dark:bg-blue-950/30 dark:text-blue-300',
    icon: Info,
    iconColor: 'text-blue-600 dark:text-blue-400',
  },
  success: {
    box: 'border-emerald-200/80 bg-emerald-50/70 text-emerald-950 dark:border-emerald-900/70 dark:bg-emerald-950/30 dark:text-emerald-300',
    icon: CircleCheck,
    iconColor: 'text-emerald-600 dark:text-emerald-400',
  },
  warning: {
    box: 'border-amber-200/80 bg-amber-50/60 text-amber-950 dark:border-amber-900/70 dark:bg-amber-950/30 dark:text-amber-300',
    icon: OctagonAlert,
    iconColor: 'text-amber-600 dark:text-amber-400',
  },
  danger: {
    box: 'border-rose-200/80 bg-rose-50/70 text-rose-950 dark:border-rose-900/70 dark:bg-rose-950/30 dark:text-rose-300',
    icon: CircleAlert,
    iconColor: 'text-rose-600 dark:text-rose-400',
  },
  tip: {
    box: 'border-blue-200/80 bg-blue-50/70 text-blue-950 dark:border-blue-900/50 dark:bg-blue-950/40 dark:text-blue-100',
    icon: Lightbulb,
    iconColor: 'text-blue-600 dark:text-blue-400',
  },
  neutral: {
    box: 'border-slate-200 bg-slate-50 text-slate-600 dark:border-slate-800 dark:bg-slate-800/50 dark:text-slate-300',
    icon: Info,
    iconColor: 'text-slate-400 dark:text-slate-500',
  },
}

export function Callout({
  tone = 'info',
  title,
  icon,
  onDismiss,
  className,
  children,
}: {
  tone?: CalloutTone
  title?: ReactNode
  /** `false` oculta el ícono; un `LucideIcon` lo cambia. */
  icon?: LucideIcon | false
  onDismiss?: () => void
  className?: string
  children: ReactNode
}) {
  const cfg = toneConfig[tone]
  const Icon = icon === false ? null : (icon ?? cfg.icon)

  return (
    <div
      className={cn(
        'flex gap-2.5 rounded-lg border px-3.5 py-2.5 text-[13px] leading-relaxed',
        cfg.box,
        className,
      )}
    >
      {Icon && <Icon size={16} className={cn('mt-[3px] shrink-0', cfg.iconColor)} />}
      <div className="min-w-0 flex-1">
        {/* El título va en la misma línea que el texto, como una sola frase con su remate. */}
        {title && <span className="mr-1.5 font-semibold">{title}</span>}
        {children}
      </div>
      {onDismiss && (
        <button
          type="button"
          onClick={onDismiss}
          aria-label="Descartar"
          className="-mr-1 mt-px shrink-0 self-start rounded-md p-1 opacity-60 transition-opacity hover:bg-black/5 hover:opacity-100 cursor-pointer dark:hover:bg-white/10"
        >
          <X size={14} />
        </button>
      )}
    </div>
  )
}
