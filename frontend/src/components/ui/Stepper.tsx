import { Fragment } from 'react'
import { cn } from '@/lib/utils'

/**
 * Indicador de pasos de un asistente (wizard). Reemplaza los `<button>` con círculo
 * numerado + línea que se repiten en los formularios de empresa y proveedor.
 *
 * El paso se puede tocar para saltar hacia atrás (o hacia adelante si `canGoTo` lo permite).
 */
export function Stepper({
  steps,
  current,
  onStepClick,
  canGoTo,
  className,
}: {
  /** Etiquetas de cada paso, en orden. El primero es el paso 1. */
  steps: string[]
  /** Paso activo, 1-indexado. */
  current: number
  onStepClick?: (step: number) => void
  /** Devuelve si se permite navegar a ese paso (1-indexado). Por defecto: solo hacia atrás. */
  canGoTo?: (step: number) => boolean
  className?: string
}) {
  const allow = canGoTo ?? ((step: number) => step <= current)

  return (
    <div className={cn('flex items-center justify-between', className)}>
      {steps.map((label, i) => {
        const step = i + 1
        const active = step === current
        const done = step < current
        const enabled = Boolean(onStepClick) && allow(step)

        return (
          <Fragment key={label}>
            <button
              type="button"
              disabled={!enabled}
              onClick={() => enabled && onStepClick?.(step)}
              aria-current={active ? 'step' : undefined}
              className={cn(
                'flex items-center gap-2 text-xs font-extrabold transition-colors',
                enabled ? 'cursor-pointer' : 'cursor-default',
                active || done
                  ? 'text-brand-deep dark:text-blue-400'
                  : 'text-slate-400 hover:text-slate-600',
              )}
            >
              <span
                className={cn(
                  'flex h-6 w-6 items-center justify-center rounded-full text-[11px]',
                  active || done
                    ? 'bg-brand-deep text-white dark:bg-blue-600'
                    : 'bg-slate-200 text-slate-600 dark:bg-slate-800 dark:text-slate-400',
                )}
              >
                {step}
              </span>
              <span>
                {step}. {label}
              </span>
            </button>

            {i < steps.length - 1 && (
              <div className="hidden h-0.5 w-8 bg-slate-200 dark:bg-slate-800 sm:block" />
            )}
          </Fragment>
        )
      })}
    </div>
  )
}
