import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

export type TabItem<T extends string = string> = {
  key: T
  label: ReactNode
  /** Contador o punto junto a la etiqueta. */
  badge?: ReactNode
  /** Contenido alineado a la derecha (solo `orientation="vertical"`): un punto de estado, etc. */
  trailing?: ReactNode
  disabled?: boolean
}

/**
 * Grupo de pestañas / control segmentado / nav vertical. Reemplaza los `<button>` con
 * `className={cond ? 'bg-brand-deep text-white' : '…'}` repartidos por el sistema.
 *
 * - `variant="pill"`      — píldora con relleno azul marino en la activa (detalle de evento)
 * - `variant="segmented"` — la activa es una tarjeta blanca sobre fondo gris (listas)
 * - `orientation="vertical"` — botones apilados a lo ancho (nav de Configuración)
 */
export function Tabs<T extends string>({
  items,
  value,
  onChange,
  variant = 'pill',
  orientation = 'horizontal',
  size = 'md',
  activeClassName,
  className,
}: {
  items: TabItem<T>[]
  value: T
  onChange: (key: T) => void
  /** `underline`: texto con una raya bajo la activa, para navegar dentro de una ficha. */
  variant?: 'pill' | 'segmented' | 'card' | 'underline'
  orientation?: 'horizontal' | 'vertical'
  size?: 'sm' | 'md'
  /** Reemplaza el azul marino de la pestaña activa: un módulo con color propio (sorteo). */
  activeClassName?: string
  className?: string
}) {
  const vertical = orientation === 'vertical'

  const wrap = vertical
    ? 'flex flex-col gap-1'
    : variant === 'underline'
      ? 'flex flex-wrap items-end gap-6'
      : variant === 'card'
        ? 'w-full rounded-xl border border-slate-200/80 bg-white p-2.5 px-3.5 shadow-2xs dark:border-slate-800 dark:bg-slate-900 flex flex-wrap items-center gap-2'
        : variant === 'pill'
          ? 'flex flex-wrap items-center gap-1.5 rounded-2xl bg-slate-200/60 p-1.5 dark:bg-slate-800/80'
          : 'inline-flex items-center rounded-xl border border-slate-200/80 bg-slate-100 p-1 dark:border-slate-800 dark:bg-slate-800/80'

  const pad = vertical
    ? 'px-4 py-3'
    : variant === 'underline'
      ? 'px-0.5 py-3 text-sm'
      : variant === 'card'
        ? 'h-10 px-4 py-2 text-sm'
        : size === 'sm'
          ? 'px-2.5 py-1 text-xs'
          : 'px-4 py-2 text-xs'

  return (
    <div className={cn(wrap, className)} role="tablist">
      {items.map((tab) => {
        const active = tab.key === value

        const btn = vertical
          ? active
            ? 'bg-brand-deep text-white shadow-2xs dark:bg-blue-600'
            : 'text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800'
          : variant === 'underline'
            ? active
              ? 'relative font-semibold text-foreground after:absolute after:inset-x-0 after:-bottom-px after:h-0.5 after:rounded-full after:bg-brand'
              : 'font-medium text-muted-foreground hover:text-foreground'
            : variant === 'card'
              ? active
                ? 'bg-brand-deep text-white font-bold shadow-2xs dark:bg-blue-600 dark:text-white'
                : 'font-medium text-slate-600 hover:text-slate-900 hover:bg-slate-100 dark:text-slate-400 dark:hover:text-white dark:hover:bg-slate-800'
              : variant === 'pill'
                ? active
                  ? 'bg-brand-deep text-white shadow-sm dark:bg-blue-600'
                  : 'text-slate-600 hover:text-slate-900 dark:text-slate-300 dark:hover:text-white'
                : active
                  ? 'bg-white text-slate-900 shadow-2xs dark:bg-slate-700 dark:text-white'
                  : 'text-slate-500 hover:text-slate-800 dark:text-slate-400 dark:hover:text-slate-200'

        return (
          <button
            key={tab.key}
            type="button"
            role="tab"
            aria-selected={active}
            disabled={tab.disabled}
            onClick={() => onChange(tab.key)}
            className={cn(
              'flex items-center gap-2 font-bold transition-all cursor-pointer disabled:cursor-not-allowed disabled:opacity-50',
              vertical
                ? 'w-full rounded-xl text-xs'
                : variant === 'underline'
                  ? 'rounded-none'
                  : 'rounded-lg',
              variant === 'segmented' && !vertical && 'rounded-lg',
              vertical && (tab.trailing != null ? 'justify-between' : 'justify-start'),
              pad,
              btn,
              active && activeClassName,
            )}
          >
            <span className="flex items-center gap-2">
              {tab.label}
              {tab.badge}
            </span>
            {vertical && tab.trailing}
          </button>
        )
      })}
    </div>
  )
}
