import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

/**
 * El encabezado de una pestaña o sección: título, una línea que explica qué hay debajo y, a la
 * derecha, las acciones principales. Va justo debajo de la barra de pestañas, igual en todas.
 */
export function SectionHeader({
  title,
  description,
  badge,
  actions,
  className,
}: {
  title: ReactNode
  description?: ReactNode
  /** Una etiqueta junto al título, como el montaje del salón. */
  badge?: ReactNode
  actions?: ReactNode
  className?: string
}) {
  return (
    <div
      className={cn(
        'flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between',
        className,
      )}
    >
      <div className="min-w-0">
        <h2 className="flex flex-wrap items-center gap-2 font-sans text-2xl font-bold text-foreground dark:text-white">
          {title}
          {badge}
        </h2>
        {description && (
          <p className="mt-0.5 text-xs font-medium text-slate-500 dark:text-slate-400">
            {description}
          </p>
        )}
      </div>
      {actions && <div className="flex shrink-0 flex-wrap items-center gap-2">{actions}</div>}
    </div>
  )
}
