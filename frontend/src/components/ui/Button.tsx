import * as React from 'react'
import { cn } from '@/lib/utils'

export interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: 'default' | 'destructive' | 'outline' | 'secondary' | 'ghost' | 'link'
  size?: 'default' | 'sm' | 'lg' | 'icon'
}

/**
 * Botón de acción del sistema. Las variantes ya traen su color: no hace falta pasar
 * `className` con `bg-…`, `text-white`, `cursor-pointer` ni `font-bold`.
 *
 * - `default`  — CTA primario (azul marino institucional)
 * - `secondary`— acción secundaria / "Cancelar" (gris)
 * - `destructive` — acción peligrosa (rojo)
 * - `outline`  — botón con borde sobre fondo claro
 * - `ghost`    — sin fondo hasta el hover (íconos, menús)
 * - `link`     — parece un enlace
 */
const variantStyles: Record<NonNullable<ButtonProps['variant']>, string> = {
  default:
    'bg-brand-deep text-white shadow-sm hover:bg-brand-deep/90 dark:bg-blue-600 dark:hover:bg-blue-700',
  destructive:
    'bg-rose-600 text-white shadow-sm hover:bg-rose-700 dark:bg-rose-600 dark:hover:bg-rose-500',
  outline:
    'border border-slate-200 bg-white text-slate-700 hover:bg-slate-50 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700',
  secondary:
    'bg-slate-100 text-slate-800 hover:bg-slate-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700',
  ghost:
    'text-slate-700 hover:bg-slate-100 hover:text-slate-900 dark:text-slate-200 dark:hover:bg-slate-800 dark:hover:text-slate-100',
  // `link` no lleva alto ni padding fijos: se comporta como texto (ver `sizeStyles` abajo).
  link: 'gap-1 font-bold text-blue-600 underline-offset-4 hover:underline dark:text-blue-400',
}

const sizeStyles: Record<NonNullable<ButtonProps['size']>, string> = {
  default: 'h-10 px-4 py-2 text-sm',
  sm: 'h-8 rounded-md px-3 text-xs',
  lg: 'h-11 rounded-md px-8 text-base',
  icon: 'h-9 w-9 p-0 flex items-center justify-center',
}

const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant = 'default', size = 'default', ...props }, ref) => (
    <button
      className={cn(
        'inline-flex items-center justify-center whitespace-nowrap rounded-md font-semibold transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-slate-400 disabled:pointer-events-none disabled:opacity-50 cursor-pointer',
        variantStyles[variant],
        variant === 'link' ? 'h-auto p-0' : sizeStyles[size],
        className,
      )}
      ref={ref}
      {...props}
    />
  ),
)
Button.displayName = 'Button'

export { Button }
