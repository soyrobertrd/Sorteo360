import type { ReactNode } from 'react'
import type { LucideIcon } from 'lucide-react'
import { cn } from '@/lib/utils'

/**
 * La vista de cuadrícula, la misma en toda la aplicación: `CardGrid` reparte las tarjetas y
 * `GridCard` es cada una. Reemplaza las tarjetas que cada listado (empresas, contactos,
 * proveedores, eventos, convocatorias, sorteos…) armaba a mano con sus propias clases.
 *
 * Una tarjeta se lee de arriba abajo: portada, etiquetas, título, cuerpo y pie. El cuerpo se
 * arma con `GridCardDetails` (filas de datos) o `GridCardStats` (cifras), y el pie suele cerrar
 * con `GridCardCta`.
 */

const COLUMNAS = {
  3: 'sm:grid-cols-2 lg:grid-cols-3',
  4: 'sm:grid-cols-2 lg:grid-cols-4',
} as const

export function CardGrid({
  columns = 3,
  className,
  children,
}: {
  /** Tarjetas por fila en pantalla ancha. */
  columns?: keyof typeof COLUMNAS
  className?: string
  children: ReactNode
}) {
  return (
    <div className={cn('grid grid-cols-1 gap-4', COLUMNAS[columns], className)}>{children}</div>
  )
}

export interface GridCardProps {
  title: ReactNode
  subtitle?: ReactNode
  /** Ícono o avatar a la izquierda del título. */
  media?: ReactNode
  /** Etiquetas arriba a la izquierda: categoría, tipo, casilla de selección. */
  tags?: ReactNode
  /** Arriba a la derecha: estado, menú de acciones. Sin `tags` se alinea con el título. */
  aside?: ReactNode
  /** Imagen de cabecera, a sangre. Lo que lleve encima va dentro, en posición absoluta. */
  cover?: ReactNode
  /** El cuerpo: `GridCardDetails`, `GridCardStats` o lo que la tarjeta necesite. */
  children?: ReactNode
  footerStart?: ReactNode
  footerEnd?: ReactNode
  /** Toda la tarjeta abre el detalle. Lo que tenga su propio clic debe frenar el evento. */
  onClick?: () => void
  /** Marcada en una selección múltiple. */
  selected?: boolean
  /** Cuántas líneas ocupa el título antes de cortarse. */
  titleLines?: 1 | 2
  className?: string
}

export function GridCard({
  title,
  subtitle,
  media,
  tags,
  aside,
  cover,
  children,
  footerStart,
  footerEnd,
  onClick,
  selected = false,
  titleLines,
  className,
}: GridCardProps) {
  const heading = (
    <div className="min-w-0">
      <h3
        className={cn(
          'font-sans text-lg font-semibold tracking-tight text-slate-900 dark:text-white',
          titleLines === 1 && 'line-clamp-1',
          titleLines === 2 && 'line-clamp-2',
        )}
      >
        {title}
      </h3>
      {subtitle && (
        <div className="mt-1 text-[13px] text-slate-500 dark:text-slate-400">{subtitle}</div>
      )}
    </div>
  )

  return (
    <div
      onClick={onClick}
      // Clicable, es un botón para el teclado y el lector de pantalla. Solo reacciona a la
      // tarjeta misma: Enter sobre un botón de adentro (el menú de acciones) es de ese botón.
      role={onClick ? 'button' : undefined}
      tabIndex={onClick ? 0 : undefined}
      onKeyDown={
        onClick
          ? (e) => {
              if (e.target !== e.currentTarget) return
              if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault()
                onClick()
              }
            }
          : undefined
      }
      className={cn(
        onClick &&
          'outline-none focus-visible:ring-2 focus-visible:ring-brand-deep/40 dark:focus-visible:ring-blue-400/50',
        'group flex flex-col justify-between gap-4 rounded-[20px] border bg-white p-5 text-slate-900 shadow-[0_1px_2px_rgb(15_23_42/0.04),0_10px_28px_-16px_rgb(15_23_42/0.18)] transition-shadow hover:shadow-[0_1px_2px_rgb(15_23_42/0.05),0_16px_36px_-16px_rgb(15_23_42/0.28)] dark:bg-slate-900 dark:text-slate-100',
        selected
          ? 'border-brand-deep ring-2 ring-brand-deep/15 dark:border-blue-500'
          : 'border-slate-200/80 dark:border-slate-800',
        onClick && 'cursor-pointer',
        cover != null && 'overflow-hidden',
        className,
      )}
    >
      <div className="space-y-2.5">
        {cover != null && (
          <div className="relative mb-3.5 h-40 overflow-hidden rounded-2xl bg-slate-900">
            {cover}
          </div>
        )}

        {tags != null && (
          <div className="flex items-start justify-between gap-2">
            <div className="flex min-w-0 flex-wrap items-center gap-1.5">{tags}</div>
            {aside != null && <div className="flex shrink-0 items-center gap-1.5">{aside}</div>}
          </div>
        )}

        <div className="flex items-start justify-between gap-2">
          <div className="flex min-w-0 items-center gap-2.5">
            {media}
            {heading}
          </div>
          {tags == null && aside != null && (
            <div className="flex shrink-0 items-center gap-1.5">{aside}</div>
          )}
        </div>
      </div>

      {children}

      {(footerStart != null || footerEnd != null) && (
        <div className="flex items-center justify-between gap-2 border-t border-dashed border-slate-200 pt-4 dark:border-slate-800">
          <div className="min-w-0">{footerStart}</div>
          {footerEnd != null && <div className="flex shrink-0 items-center gap-2">{footerEnd}</div>}
        </div>
      )}
    </div>
  )
}

const TAG_TONES = {
  blue: 'border-sky-200 bg-sky-50 text-sky-700 dark:border-sky-900 dark:bg-sky-950/50 dark:text-sky-300',
  slate:
    'border-slate-200 bg-slate-50 text-slate-700 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-300',
  amber:
    'border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-900 dark:bg-amber-950/50 dark:text-amber-300',
} as const

/** La etiqueta de categoría o tipo de una tarjeta. */
export function GridCardTag({
  tone = 'blue',
  className,
  children,
}: {
  tone?: keyof typeof TAG_TONES
  className?: string
  children: ReactNode
}) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 rounded-md border px-2 py-0.5 text-xs font-medium',
        TAG_TONES[tone],
        className,
      )}
    >
      {children}
    </span>
  )
}

/** El bloque de datos de la tarjeta, entre dos líneas finas. */
export function GridCardDetails({
  className,
  children,
}: {
  className?: string
  children: ReactNode
}) {
  return (
    <div
      className={cn(
        'space-y-1.5 border-y border-slate-100 py-2.5 text-xs text-slate-600 dark:border-slate-800/80 dark:text-slate-300',
        className,
      )}
    >
      {children}
    </div>
  )
}

/** Una fila de `GridCardDetails`: un ícono o un rótulo, y el dato. */
export function GridCardDetail({
  icon: Icon,
  label,
  className,
  children,
}: {
  icon?: LucideIcon
  label?: string
  className?: string
  children: ReactNode
}) {
  return (
    <p className={cn('flex items-center gap-1.5', className)}>
      {Icon && <Icon size={13} className="shrink-0 text-slate-400" />}
      {label && <strong className="shrink-0 text-[10px] uppercase text-slate-400">{label}:</strong>}
      <span className="min-w-0 truncate">{children}</span>
    </p>
  )
}

const STAT_TONES = {
  slate: 'text-slate-800 dark:text-slate-200',
  blue: 'text-blue-700 dark:text-blue-400',
  emerald: 'text-emerald-600 dark:text-emerald-400',
  violet: 'text-violet-700 dark:text-violet-400',
  teal: 'text-teal-600 dark:text-teal-400',
} as const

export interface GridCardStat {
  label: string
  value: ReactNode
  tone?: keyof typeof STAT_TONES
}

/** Las cifras rápidas de la tarjeta, una al lado de la otra. */
export function GridCardStats({ items }: { items: GridCardStat[] }) {
  return (
    <div
      className="grid gap-2 rounded-xl bg-slate-50 p-2.5 text-center text-[11px] dark:bg-slate-800/60"
      style={{ gridTemplateColumns: `repeat(${items.length}, minmax(0, 1fr))` }}
    >
      {items.map((item) => (
        <div key={item.label} className="min-w-0">
          <span className={cn('block truncate font-bold', STAT_TONES[item.tone ?? 'slate'])}>
            {item.value}
          </span>
          <span className="block truncate text-[10px] text-slate-500">{item.label}</span>
        </div>
      ))}
    </div>
  )
}

/** Una cifra con su rótulo encima, para el pie: "Costo total". */
export function GridCardFigure({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <span className="block text-[10px] font-bold uppercase text-slate-400">{label}</span>
      <span className="font-sans text-sm font-black text-brand-deep dark:text-white">
        {children}
      </span>
    </div>
  )
}

/**
 * La llamada del pie. No es un botón: toda la tarjeta ya abre el detalle, y esto solo lo
 * anuncia. `link` es la variante de texto, para un destino que no es la ficha.
 */
export function GridCardCta({
  variant = 'solid',
  children = 'Ver detalles',
}: {
  variant?: 'solid' | 'link'
  children?: ReactNode
}) {
  return (
    <span
      className={
        variant === 'solid'
          ? 'inline-flex items-center justify-center rounded-md bg-brand-deep px-3.5 py-1.5 text-xs font-bold text-white shadow-2xs transition-colors group-hover:bg-brand-deep/90 dark:bg-blue-600 dark:group-hover:bg-blue-500'
          : 'inline-flex items-center gap-1 text-xs font-bold text-blue-600 dark:text-blue-400'
      }
    >
      {children}
    </span>
  )
}
