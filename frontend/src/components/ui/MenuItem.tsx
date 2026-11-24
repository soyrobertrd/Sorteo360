import * as React from 'react'
import { cn } from '@/lib/utils'

export interface MenuItemProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  /** `danger` la pinta en rojo (eliminar). */
  tone?: 'default' | 'danger'
}

/**
 * Fila de un menú desplegable (`ActionMenu`, menú "Acciones", etc.). Reemplaza los
 * ~35 `<button className="flex w-full items-center gap-2.5 px-3.5 py-2 …">`.
 */
export const MenuItem = React.forwardRef<HTMLButtonElement, MenuItemProps>(
  ({ className, tone = 'default', type = 'button', ...props }, ref) => (
    <button
      ref={ref}
      type={type}
      className={cn(
        'flex min-h-9 w-full items-center gap-2.5 rounded-lg px-2.5 py-1.5 text-left text-sm font-medium transition-colors cursor-pointer disabled:pointer-events-none disabled:opacity-50 [&_svg]:shrink-0',
        tone === 'danger'
          ? 'text-rose-600 hover:bg-rose-50 dark:text-rose-400 dark:hover:bg-rose-950/40'
          : 'text-slate-800 hover:bg-slate-100 dark:text-slate-100 dark:hover:bg-slate-800 [&_svg]:text-slate-400 dark:[&_svg]:text-slate-500',
        className,
      )}
      {...props}
    />
  ),
)
MenuItem.displayName = 'MenuItem'

/** Separador fino entre grupos de items. */
export function MenuSeparator() {
  return <div className="-mx-1.5 my-1.5 border-t border-slate-100 dark:border-slate-800" />
}
