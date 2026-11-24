import { useEffect, useRef, type FormEvent, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { Check, X } from 'lucide-react'
import { Animate } from '@/components/ui/Animate'
import { useDialogFocus } from './useDialogFocus'
import { cn } from '@/lib/utils'

/**
 * Modal de pasos: la lista de pasos a la izquierda y el contenido del paso actual a la derecha.
 * Era el de las ediciones de feria; ahora lo usa el lanzamiento del sorteo en vivo.
 */
export interface StepperModalStep {
  number: number
  title: string
  subtitle: string
}

interface StepperModalProps {
  open?: boolean
  title: string
  subtitle: string
  steps: StepperModalStep[]
  currentStep: number
  isSubmitting?: boolean
  onClose: () => void
  onStepClick: (stepNumber: number) => void
  onSubmit: (e: FormEvent) => void
  children: ReactNode
  footer?: ReactNode
  // `false` oculta la lista de pasos de la izquierda y el contenido ocupa todo el ancho.
  showSteps?: boolean
  // Franja angosta pegada debajo del encabezado, a todo el ancho del modal (un aviso de guardado).
  banner?: ReactNode
}

export function StepperModal({
  open = true,
  title,
  subtitle,
  steps,
  currentStep,
  isSubmitting = false,
  onClose,
  onStepClick,
  onSubmit,
  children,
  footer,
  showSteps = true,
  banner,
}: StepperModalProps) {
  useEffect(() => {
    if (!open) return
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !isSubmitting) {
        onClose()
      }
    }
    document.addEventListener('keydown', onKey)
    document.body.style.overflow = 'hidden'
    return () => {
      document.removeEventListener('keydown', onKey)
      document.body.style.overflow = ''
    }
  }, [open, isSubmitting, onClose])

  // Foco inicial, Tab que no sale del diálogo y vuelta a quien lo abrió.
  const dialogRef = useRef<HTMLDivElement>(null)
  useDialogFocus(dialogRef, open)

  if (!open) return null

  return createPortal(
    <div
      ref={dialogRef}
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/60 p-3 sm:p-5 backdrop-blur-xs overflow-y-auto"
      onClick={() => {
        if (!isSubmitting) onClose()
      }}
      role="dialog"
      aria-modal="true"
    >
      <Animate
        variant="zoom"
        duration={180}
        className={cn(
          'w-full max-h-[92vh] rounded-lg bg-white shadow-2xl dark:bg-slate-900 dark:border dark:border-slate-800 flex flex-col overflow-hidden my-auto',
          showSteps ? 'max-w-5xl' : 'max-w-3xl',
        )}
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between px-6 py-3.5 sm:px-8 sm:py-4 border-b border-slate-100 dark:border-slate-800 relative bg-white dark:bg-slate-900 shrink-0">
          <div>
            <h1 className="text-base sm:text-lg font-bold text-slate-900 dark:text-white">
              {title}
            </h1>
            <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5">{subtitle}</p>
          </div>

          <button
            type="button"
            onClick={() => {
              if (!isSubmitting) onClose()
            }}
            aria-label="Cerrar modal"
            className="text-slate-400 hover:text-slate-700 dark:hover:text-slate-200 p-1.5 rounded-full hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors cursor-pointer"
          >
            <X size={18} />
          </button>
        </div>

        {banner}

        <form onSubmit={onSubmit} className="flex flex-col flex-1 min-h-0 overflow-hidden">
          <div className="grid grid-cols-1 md:grid-cols-12 flex-1 min-h-0 overflow-hidden">
            {showSteps && (
              <div className="md:col-span-4 border-b md:border-b-0 md:border-r border-slate-100 dark:border-slate-800 p-6 sm:p-8 bg-slate-50/50 dark:bg-slate-900/40 flex flex-col justify-center">
                <div className="flex flex-col gap-10 sm:gap-12">
                  {steps.map((step, index) => {
                    const isActive = currentStep === step.number
                    const isCompleted = currentStep > step.number
                    const isLast = index === steps.length - 1

                    return (
                      <div key={step.number} className="relative flex items-center">
                        <button
                          type="button"
                          disabled={isSubmitting}
                          onClick={() => onStepClick(step.number)}
                          className={cn(
                            'w-full flex items-center justify-between text-right group relative z-10 transition-all focus:outline-none',
                            isSubmitting ? 'cursor-default' : 'cursor-pointer',
                          )}
                        >
                          <div className="pr-4 min-w-0 flex-1">
                            <h4
                              className={cn(
                                'text-xs sm:text-sm font-semibold transition-colors',
                                isActive
                                  ? 'text-slate-900 dark:text-white font-bold'
                                  : isCompleted
                                    ? 'text-slate-700 dark:text-slate-300'
                                    : 'text-slate-400 dark:text-slate-500',
                              )}
                            >
                              {step.title}
                            </h4>
                            <p
                              className={cn(
                                'text-[10px] sm:text-[11px] mt-0.5 line-clamp-1 transition-colors',
                                isActive
                                  ? 'text-brand-deep dark:text-blue-400 font-medium'
                                  : 'text-slate-400 dark:text-slate-500',
                              )}
                            >
                              {step.subtitle}
                            </p>
                          </div>

                          <div
                            className={cn(
                              'w-7 h-7 sm:w-8 sm:h-8 rounded-full flex items-center justify-center shrink-0 transition-all duration-200 bg-white dark:bg-slate-900 relative z-10',
                              isActive
                                ? 'bg-brand-deep dark:bg-blue-600 text-white shadow-sm ring-4 ring-slate-100 dark:ring-blue-950/60'
                                : isCompleted
                                  ? 'border-2 border-brand-deep text-brand-deep dark:border-blue-400 dark:text-blue-400'
                                  : 'border-2 border-slate-300 dark:border-slate-700 text-slate-400 dark:text-slate-600',
                            )}
                          >
                            <Check size={14} strokeWidth={2.6} />
                          </div>
                        </button>

                        {!isLast && (
                          <div
                            className="absolute right-3.25 sm:right-3.75 top-1/2 w-0.5 bg-slate-200 dark:bg-slate-700 z-0 pointer-events-none h-[calc(100%+2.5rem)] sm:h-[calc(100%+3rem)]"
                            aria-hidden="true"
                          />
                        )}
                      </div>
                    )
                  })}
                </div>
              </div>
            )}

            <div
              className={cn(
                'overflow-y-auto [scrollbar-width:none] [&::-webkit-scrollbar]:hidden p-6 sm:p-8 flex flex-col justify-between',
                showSteps ? 'md:col-span-8' : 'md:col-span-12',
              )}
            >
              {children}
            </div>
          </div>

          {footer && (
            <div className="p-4 sm:px-8 border-t border-slate-100 dark:border-slate-800 bg-white dark:bg-slate-900 flex items-center justify-end gap-3 shrink-0">
              {footer}
            </div>
          )}
        </form>
      </Animate>
    </div>,
    document.body,
  )
}
