import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

/**
 * Barra de progreso. Reemplaza los `<div class="h-1.5 rounded-full bg-slate-100"><div .../></div>`
 * anidados repartidos por el detalle de evento, la ficha de proveedor, invitados, etc.
 */
export function ProgressBar({
  value,
  max = 100,
  color = 'primary',
  size = 'md',
  label,
  right,
  className,
}: {
  value: number
  max?: number
  color?: 'primary' | 'emerald' | 'blue' | 'amber' | 'rose' | 'slate'
  size?: 'sm' | 'md'
  /** Etiqueta a la izquierda, sobre la barra. */
  label?: ReactNode
  /** Contenido a la derecha de la etiqueta (normalmente el valor). */
  right?: ReactNode
  className?: string
}) {
  const pct = max > 0 ? Math.min(100, Math.max(0, (value / max) * 100)) : 0
  const fill = {
    primary: 'bg-brand-deep dark:bg-blue-500',
    emerald: 'bg-emerald-500',
    blue: 'bg-blue-500',
    amber: 'bg-amber-500',
    rose: 'bg-rose-500',
    slate: 'bg-slate-400 dark:bg-slate-500',
  }[color]
  const h = size === 'sm' ? 'h-1' : 'h-1.5'

  return (
    <div className={className}>
      {(label != null || right != null) && (
        <div className="mb-1 flex items-center justify-between text-xs text-slate-500 dark:text-slate-400">
          <span>{label}</span>
          {right != null && (
            <span className="font-bold text-slate-700 dark:text-slate-300">{right}</span>
          )}
        </div>
      )}
      <div
        className={cn('overflow-hidden rounded-full bg-slate-100 dark:bg-slate-800', h)}
        role="progressbar"
        aria-valuenow={Math.round(pct)}
        aria-valuemin={0}
        aria-valuemax={100}
      >
        <div className={cn('rounded-full transition-all', h, fill)} style={{ width: `${pct}%` }} />
      </div>
    </div>
  )
}
