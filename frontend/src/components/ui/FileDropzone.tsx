import { useId, useRef, useState, type DragEvent, type ReactNode } from 'react'
import { Upload } from 'lucide-react'
import { cn } from '@/lib/utils'
import { matchesAccept } from './fileAccept'

interface FileDropzoneProps {
  /** Extensiones o tipos MIME, como en `<input accept>`: `.xlsx,.csv` o `image/*`. */
  accept: string
  onFile: (file: File) => void
  /** Texto principal de la zona. */
  title: ReactNode
  /** Línea secundaria: formatos, tamaño. */
  hint?: ReactNode
  disabled?: boolean
  /** Versión compacta (una línea), para formularios. */
  compact?: boolean
  className?: string
}

/**
 * Zona para adjuntar un archivo: un botón que abre el explorador y, a la vez, un área donde
 * soltarlo. El `<input type="file">` queda oculto solo a la vista (no con `display: none`) y se
 * abre desde el botón, así el explorador abre siempre, también con teclado.
 */
export function FileDropzone({
  accept,
  onFile,
  title,
  hint,
  disabled = false,
  compact = false,
  className,
}: FileDropzoneProps) {
  const inputRef = useRef<HTMLInputElement>(null)
  const hintId = useId()
  const [over, setOver] = useState(false)
  const [rejected, setRejected] = useState<string | null>(null)

  const take = (file: File | undefined) => {
    if (!file) return
    if (!matchesAccept(file, accept)) {
      setRejected(`"${file.name}" no es de un formato admitido.`)
      return
    }
    setRejected(null)
    onFile(file)
  }

  const onDrop = (e: DragEvent<HTMLDivElement>) => {
    e.preventDefault()
    setOver(false)
    if (!disabled) take(e.dataTransfer.files?.[0])
  }

  return (
    <div className={className}>
      <div
        onDragOver={(e) => {
          e.preventDefault()
          if (!disabled) setOver(true)
        }}
        onDragLeave={() => setOver(false)}
        onDrop={onDrop}
        className={cn(
          'flex rounded-2xl border-2 border-dashed transition-colors',
          compact
            ? 'items-center gap-3 p-2.5'
            : 'flex-col items-center justify-center gap-2 p-6 text-center',
          over
            ? 'border-brand-accent bg-brand-accent/10'
            : 'border-slate-300 bg-slate-50/60 dark:border-slate-700 dark:bg-slate-800/40',
          disabled && 'opacity-60',
        )}
      >
        <button
          type="button"
          disabled={disabled}
          aria-describedby={hint ? hintId : undefined}
          onClick={() => inputRef.current?.click()}
          className={cn(
            'inline-flex shrink-0 cursor-pointer items-center gap-2 rounded-xl bg-brand font-bold text-white transition-colors hover:bg-brand/90 disabled:cursor-not-allowed',
            compact ? 'px-3 py-1.5 text-[11px]' : 'px-4 py-2 text-xs',
          )}
        >
          <Upload size={compact ? 13 : 15} /> {compact ? 'Elegir archivo' : 'Buscar en el equipo'}
        </button>
        <div className={compact ? 'min-w-0 text-left' : ''}>
          <p className="text-xs font-bold text-slate-800 dark:text-slate-100">{title}</p>
          <p id={hintId} className="text-[11px] text-slate-500 dark:text-slate-400">
            {hint ? <>{hint} · </> : null}o arrástralo y suéltalo aquí
          </p>
        </div>
        <input
          ref={inputRef}
          type="file"
          accept={accept}
          tabIndex={-1}
          aria-hidden="true"
          aria-label={typeof title === 'string' ? title : 'Elegir archivo'}
          className="sr-only"
          onChange={(e) => {
            take(e.target.files?.[0])
            // Limpiar permite volver a elegir el mismo archivo después de corregirlo.
            e.target.value = ''
          }}
        />
      </div>
      {rejected && (
        <p
          role="alert"
          className="mt-1.5 text-[11px] font-semibold text-rose-600 dark:text-rose-400"
        >
          {rejected}
        </p>
      )}
    </div>
  )
}
