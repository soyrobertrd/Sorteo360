import { useEffect, type ReactNode } from 'react'
import { Info, Loader2 } from 'lucide-react'
import { errorMessage } from '@/lib/errorMessage'
import { Callout } from './Callout'

export function Spinner({ label = 'Cargando…' }: { label?: string }) {
  return (
    <div className="flex items-center justify-center gap-2 py-10 text-sm text-slate-500">
      <Loader2 size={18} className="animate-spin" />
      {label}
    </div>
  )
}

export function ErrorNote({ error, className = '' }: { error: unknown; className?: string }) {
  return (
    <Callout tone="danger" className={className}>
      {errorMessage(error)}
    </Callout>
  )
}

/**
 * Un aviso, no un fallo: la pantalla trae lo que pidió, pero algo de lo que muestra
 * está degradado y quien la mira tiene que saberlo para no leerlo como un dato malo.
 */
export function WarningNote({
  children,
  className = '',
}: {
  children: ReactNode
  className?: string
}) {
  return (
    <Callout tone="warning" icon={Info} className={className}>
      {children}
    </Callout>
  )
}

export function ToastNotification({
  children,
  onClose,
  tone = 'warning',
}: {
  children: ReactNode
  onClose?: () => void
  tone?: 'warning' | 'info' | 'error' | 'success'
}) {
  useEffect(() => {
    if (!onClose) return
    const timer = setTimeout(() => {
      onClose()
    }, 6000)
    return () => clearTimeout(timer)
  }, [onClose])

  const toneStyles = {
    warning:
      'bg-amber-900/90 text-amber-100 border-amber-700 dark:bg-amber-950/95 dark:text-amber-200',
    info: 'bg-blue-900/90 text-blue-100 border-blue-700 dark:bg-blue-950/95 dark:text-blue-200',
    error: 'bg-rose-900/90 text-rose-100 border-rose-700 dark:bg-rose-950/95 dark:text-rose-200',
    success:
      'bg-emerald-900/90 text-emerald-100 border-emerald-700 dark:bg-emerald-950/95 dark:text-emerald-200',
  }[tone]

  return (
    <div className="fixed bottom-6 right-6 z-50 max-w-md animate-in fade-in slide-in-from-bottom-5 duration-300">
      <div
        className={`flex items-start gap-3 rounded-2xl border p-4 shadow-xl backdrop-blur-md text-xs font-semibold ${toneStyles}`}
      >
        <Info size={18} className="mt-0.5 shrink-0 text-amber-400" />
        <div className="flex-1 leading-relaxed">{children}</div>
        {onClose && (
          <button
            type="button"
            onClick={onClose}
            className="shrink-0 text-amber-300 hover:text-white transition-colors cursor-pointer"
          >
            ✕
          </button>
        )}
      </div>
    </div>
  )
}

export function EmptyState({ children }: { children: ReactNode }) {
  return (
    <div className="rounded-md border border-dashed border-slate-300 bg-slate-50 px-4 py-8 text-center text-sm text-slate-500">
      {children}
    </div>
  )
}
