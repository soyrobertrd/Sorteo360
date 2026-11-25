import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { ArrowRight, type LucideIcon } from 'lucide-react'
import { cn } from '@/lib/utils'

export type StatCardColor =
  'blue' | 'indigo' | 'emerald' | 'violet' | 'purple' | 'amber' | 'rose' | 'teal' | 'cyan' | 'slate'

export interface StatCardColorStyles {
  cardBorder: string
  iconWrapper: string
  numberText: string
}

const STAT_CARD_COLORS: Record<StatCardColor, StatCardColorStyles> = {
  blue: {
    cardBorder:
      'border-blue-200/90 hover:border-blue-400/90 dark:border-blue-900/60 dark:hover:border-blue-700',
    iconWrapper:
      'border-blue-200 bg-blue-50 text-blue-700 dark:border-blue-900/70 dark:bg-blue-950/60 dark:text-blue-300',
    numberText: 'text-brand dark:text-blue-400',
  },
  indigo: {
    cardBorder:
      'border-indigo-200/90 hover:border-indigo-400/90 dark:border-indigo-900/60 dark:hover:border-indigo-700',
    iconWrapper:
      'border-indigo-200 bg-indigo-50 text-indigo-700 dark:border-indigo-900/70 dark:bg-indigo-950/60 dark:text-indigo-300',
    numberText: 'text-indigo-900 dark:text-indigo-300',
  },
  emerald: {
    cardBorder:
      'border-emerald-200/90 hover:border-emerald-400/90 dark:border-emerald-900/60 dark:hover:border-emerald-700',
    iconWrapper:
      'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900/70 dark:bg-emerald-950/60 dark:text-emerald-300',
    numberText: 'text-emerald-700 dark:text-emerald-400',
  },
  violet: {
    cardBorder:
      'border-violet-200/90 hover:border-violet-400/90 dark:border-violet-900/60 dark:hover:border-violet-700',
    iconWrapper:
      'border-violet-200 bg-violet-50 text-violet-700 dark:border-violet-900/70 dark:bg-violet-950/60 dark:text-violet-300',
    numberText: 'text-violet-900 dark:text-violet-300',
  },
  purple: {
    cardBorder:
      'border-purple-200/90 hover:border-purple-400/90 dark:border-purple-900/60 dark:hover:border-purple-700',
    iconWrapper:
      'border-purple-200 bg-purple-50 text-purple-700 dark:border-purple-900/70 dark:bg-purple-950/60 dark:text-purple-300',
    numberText: 'text-purple-900 dark:text-purple-300',
  },
  amber: {
    cardBorder:
      'border-amber-200/90 hover:border-amber-400/90 dark:border-amber-900/60 dark:hover:border-amber-700',
    iconWrapper:
      'border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-900/70 dark:bg-amber-950/60 dark:text-amber-300',
    numberText: 'text-amber-700 dark:text-amber-400',
  },
  rose: {
    cardBorder:
      'border-rose-200/90 hover:border-rose-400/90 dark:border-rose-900/60 dark:hover:border-rose-700',
    iconWrapper:
      'border-rose-200 bg-rose-50 text-rose-700 dark:border-rose-900/70 dark:bg-rose-950/60 dark:text-rose-300',
    numberText: 'text-rose-700 dark:text-rose-400',
  },
  teal: {
    cardBorder:
      'border-teal-200/90 hover:border-teal-400/90 dark:border-teal-900/60 dark:hover:border-teal-700',
    iconWrapper:
      'border-teal-200 bg-teal-50 text-teal-700 dark:border-teal-900/70 dark:bg-teal-950/60 dark:text-teal-300',
    numberText: 'text-teal-700 dark:text-teal-400',
  },
  cyan: {
    cardBorder:
      'border-cyan-200/90 hover:border-cyan-400/90 dark:border-cyan-900/60 dark:hover:border-cyan-700',
    iconWrapper:
      'border-cyan-200 bg-cyan-50 text-cyan-700 dark:border-cyan-900/70 dark:bg-cyan-950/60 dark:text-cyan-300',
    numberText: 'text-cyan-800 dark:text-cyan-300',
  },
  slate: {
    cardBorder:
      'border-slate-200/80 hover:border-slate-300 dark:border-slate-800 dark:hover:border-slate-700',
    iconWrapper:
      'border-slate-200 bg-slate-100 text-slate-700 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-300',
    numberText: 'text-slate-900 dark:text-white',
  },
}

export interface StatCardProps {
  /** Ícono lucide a mostrar */
  icon: LucideIcon
  /** Número o valor estadístico grande */
  value: ReactNode
  /** Etiqueta o texto descriptivo */
  label: ReactNode
  /** Color temático (aplica borde, contenedor de ícono y tono del valor) */
  color?: StatCardColor
  /** Texto pequeño explicativo o hint secundario debajo de la etiqueta */
  hint?: ReactNode
  /** Disposición interna: 'horizontal' (por defecto) o 'vertical' */
  layout?: 'horizontal' | 'vertical'
  /** Si se pasa, la tarjeta funciona como enlace */
  to?: string
  /** Click handler opcional */
  onClick?: () => void
  /** Clases de personalización directa para el contenedor del ícono */
  iconClassName?: string
  /** Clases adicionales para la tarjeta */
  className?: string
  /** Mostrar flecha si es interactivo (por defecto true) */
  showArrow?: boolean
}

/**
 * Componente unificado y reutilizable de tarjeta de estadísticas / métricas KPI.
 * Soporta ícono, número/valor, texto descriptivo, paletas de colores semánticos y borde armonizado.
 */
export function StatCard({
  icon: Icon,
  value,
  label,
  color = 'slate',
  hint,
  layout = 'horizontal',
  to,
  onClick,
  iconClassName,
  className,
  showArrow = true,
}: StatCardProps) {
  const interactive = Boolean(to || onClick)
  const colorPreset = STAT_CARD_COLORS[color] ?? STAT_CARD_COLORS.slate

  const body = (
    <>
      <span
        className={cn(
          'flex h-10 w-10 shrink-0 items-center justify-center rounded-xl border sm:h-12 sm:w-12 transition-transform duration-200 group-hover:scale-105',
          iconClassName || colorPreset.iconWrapper,
        )}
      >
        <Icon size={20} className="stroke-[2.2] sm:size-[22px]" />
      </span>

      <span className="min-w-0 flex-1">
        <span
          className={cn(
            'block font-sans text-xl font-extrabold tracking-tight sm:text-2xl',
            colorPreset.numberText,
          )}
        >
          {value}
        </span>
        <span className="block truncate font-body text-xs font-semibold text-slate-500 dark:text-slate-400 mt-0.5">
          {label}
        </span>
        {hint && (
          <span className="mt-1 block truncate text-[11px] font-medium text-slate-400 dark:text-slate-500">
            {hint}
          </span>
        )}
      </span>

      {interactive && showArrow && layout !== 'vertical' && (
        <ArrowRight
          size={16}
          className="hidden shrink-0 sm:block text-slate-300 transition-colors group-hover:text-slate-500 dark:text-slate-600 dark:group-hover:text-slate-400"
        />
      )}
    </>
  )

  const baseClasses = cn(
    'min-w-0 rounded-2xl border bg-white p-4 shadow-2xs transition-all sm:p-5 dark:bg-slate-900',
    colorPreset.cardBorder,
    layout === 'vertical'
      ? 'flex flex-col items-start justify-between gap-3'
      : // En el teléfono, apilada: así caben dos por fila.
        'flex flex-col items-start gap-3 sm:flex-row sm:items-center sm:gap-4',
    interactive &&
      'group cursor-pointer hover:-translate-y-0.5 hover:shadow-md dark:hover:bg-slate-800/40',
    className,
  )

  if (to) {
    return (
      <Link to={to} className={baseClasses}>
        {body}
      </Link>
    )
  }

  if (onClick) {
    return (
      <button type="button" onClick={onClick} className={cn(baseClasses, 'text-left w-full')}>
        {body}
      </button>
    )
  }

  return <div className={baseClasses}>{body}</div>
}
