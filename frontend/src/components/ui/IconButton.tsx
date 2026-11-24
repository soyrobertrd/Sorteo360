import * as React from 'react'
import { cn } from '@/lib/utils'

export interface IconButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  /** `subtle` gris (edición/menús), `danger` para borrar, `plain` sin hover de fondo. */
  tone?: 'subtle' | 'danger' | 'plain'
  size?: 'sm' | 'md'
  /** Etiqueta accesible obligatoria: un botón de solo ícono no tiene texto visible. */
  'aria-label': string
}

/**
 * Botón de solo ícono (editar, borrar, cerrar, abrir menú…). Reemplaza los ~50
 * `<button className="rounded p-1 text-slate-500 hover:bg-slate-100">` sueltos.
 */
const toneStyles: Record<NonNullable<IconButtonProps['tone']>, string> = {
  subtle:
    'text-slate-400 hover:bg-slate-100 hover:text-slate-700 dark:text-slate-500 dark:hover:bg-slate-800 dark:hover:text-slate-200',
  danger:
    'text-slate-400 hover:bg-rose-50 hover:text-rose-600 dark:text-slate-500 dark:hover:bg-rose-950/40 dark:hover:text-rose-400',
  plain: 'text-slate-400 hover:text-slate-700 dark:text-slate-500 dark:hover:text-slate-200',
}

export const IconButton = React.forwardRef<HTMLButtonElement, IconButtonProps>(
  ({ className, tone = 'subtle', size = 'md', type = 'button', ...props }, ref) => (
    <button
      ref={ref}
      type={type}
      className={cn(
        'inline-flex shrink-0 items-center justify-center rounded-lg transition-colors cursor-pointer disabled:pointer-events-none disabled:opacity-40',
        size === 'sm' ? 'h-6 w-6' : 'h-8 w-8',
        toneStyles[tone],
        className,
      )}
      {...props}
    />
  ),
)
IconButton.displayName = 'IconButton'
