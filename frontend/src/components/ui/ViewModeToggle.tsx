import { LayoutList, LayoutGrid } from 'lucide-react'
import { cn } from '@/lib/utils'

export type ViewMode = 'list' | 'grid'

export interface ViewModeToggleProps<T extends string = ViewMode> {
  value: T
  onChange: (value: T) => void
  listKey?: T
  gridKey?: T
  className?: string
  size?: 'sm' | 'md'
}

/**
 * Componente unificado para alternar entre vista de Lista (Tabla) y Cuadrícula (Tarjetas/Grid).
 * Garantiza consistencia visual, textos idénticos ("Lista" y "Cuadrícula") y el orden
 * oficial ("Lista" primero, "Cuadrícula" segundo) en todo el sistema Sorteo360.
 */
export function ViewModeToggle<T extends string = ViewMode>({
  value,
  onChange,
  listKey = 'list' as T,
  gridKey = 'grid' as T,
  className,
  size = 'md',
}: ViewModeToggleProps<T>) {
  const isList = value === listKey
  const isGrid = value === gridKey

  const pad = size === 'sm' ? 'px-2.5 py-1 text-xs' : 'px-3.5 py-1.5 text-sm'

  return (
    <div
      className={cn(
        'inline-flex items-center gap-0.5 rounded-xl bg-slate-100 p-1 dark:bg-slate-800/80 select-none shrink-0',
        className,
      )}
      role="group"
      aria-label="Seleccionar modo de vista"
    >
      <button
        type="button"
        onClick={() => onChange(listKey)}
        aria-pressed={isList}
        className={cn(
          'flex items-center gap-1.5 rounded-lg font-medium transition-all cursor-pointer',
          pad,
          isList
            ? 'bg-white text-slate-900 shadow-[0_1px_2px_rgb(15_23_42/0.08)] ring-1 ring-slate-200/70 dark:bg-slate-900 dark:text-white dark:ring-slate-700'
            : 'text-slate-500 hover:text-slate-900 dark:text-slate-400 dark:hover:text-white',
        )}
      >
        <LayoutList size={size === 'sm' ? 14 : 15} />
        <span>Lista</span>
      </button>

      <button
        type="button"
        onClick={() => onChange(gridKey)}
        aria-pressed={isGrid}
        className={cn(
          'flex items-center gap-1.5 rounded-lg font-medium transition-all cursor-pointer',
          pad,
          isGrid
            ? 'bg-white text-slate-900 shadow-[0_1px_2px_rgb(15_23_42/0.08)] ring-1 ring-slate-200/70 dark:bg-slate-900 dark:text-white dark:ring-slate-700'
            : 'text-slate-500 hover:text-slate-900 dark:text-slate-400 dark:hover:text-white',
        )}
      >
        <LayoutGrid size={size === 'sm' ? 14 : 15} />
        <span>Cuadrícula</span>
      </button>
    </div>
  )
}
