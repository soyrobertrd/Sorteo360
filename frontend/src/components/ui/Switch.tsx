import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

/**
 * Interruptor encendido/apagado con su título y una línea de ayuda. Para una opción que
 * muestra u oculta algo, en vez de un checkbox suelto.
 */
export function Switch({
  checked,
  onChange,
  label,
  description,
  className,
}: {
  checked: boolean
  onChange: (checked: boolean) => void
  label: ReactNode
  description?: ReactNode
  className?: string
}) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      onClick={() => onChange(!checked)}
      className={cn(
        'flex items-center gap-3 rounded-xl border px-3 py-2 text-left transition-colors',
        checked
          ? 'border-brand bg-brand/5 dark:border-blue-500 dark:bg-blue-950/40'
          : 'border-slate-200 bg-white hover:border-slate-300 dark:border-slate-700 dark:bg-slate-900',
        className,
      )}
    >
      <span
        className={cn(
          'relative h-5 w-9 shrink-0 rounded-full transition-colors',
          checked ? 'bg-brand dark:bg-blue-500' : 'bg-slate-300 dark:bg-slate-600',
        )}
      >
        <span
          className={cn(
            'absolute top-0.5 h-4 w-4 rounded-full bg-white shadow transition-all',
            checked ? 'left-[18px]' : 'left-0.5',
          )}
        />
      </span>
      <span>
        <span className="block text-xs font-bold text-slate-800 dark:text-slate-200">{label}</span>
        {description && <span className="block text-[11px] text-slate-500">{description}</span>}
      </span>
    </button>
  )
}
