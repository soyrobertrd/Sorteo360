import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

export interface RadioCardOption<T extends string> {
  value: T
  label: ReactNode
  description?: ReactNode
}

/**
 * Opciones excluyentes como tarjetas: cada una con su título y su explicación, y un borde
 * marcado en la elegida. Para cuando la diferencia entre opciones necesita contarse.
 */
export function RadioCards<T extends string>({
  name,
  legend,
  options,
  value,
  onChange,
  className,
}: {
  /** El `name` del grupo de radios; único en la pantalla. */
  name: string
  legend?: ReactNode
  options: RadioCardOption<T>[]
  value: T
  onChange: (value: T) => void
  className?: string
}) {
  return (
    <fieldset className="space-y-2">
      {legend && (
        <legend className="mb-1 text-xs font-bold text-slate-700 dark:text-slate-200">
          {legend}
        </legend>
      )}
      <div className={cn('grid grid-cols-1 gap-2 sm:grid-cols-3', className)}>
        {options.map((opcion) => (
          <label
            key={opcion.value}
            className={cn(
              'flex cursor-pointer items-center gap-2 rounded-xl border p-3 text-xs',
              value === opcion.value
                ? 'border-brand bg-white ring-1 ring-brand dark:border-blue-500 dark:bg-slate-900 dark:ring-blue-500'
                : 'border-slate-200 bg-white dark:border-slate-800 dark:bg-slate-900',
            )}
          >
            <input
              type="radio"
              name={name}
              className="shrink-0"
              checked={value === opcion.value}
              onChange={() => onChange(opcion.value)}
            />
            <span>
              <span className="block font-bold text-slate-800 dark:text-slate-100">
                {opcion.label}
              </span>
              {opcion.description && (
                <span className="text-[11px] text-slate-500">{opcion.description}</span>
              )}
            </span>
          </label>
        ))}
      </div>
    </fieldset>
  )
}
