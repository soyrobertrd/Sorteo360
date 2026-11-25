import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'
import { EmptyState, Spinner } from './Feedback'

export type Column<T> = {
  /** Identificador único de la columna. */
  key: string
  header: ReactNode
  cell: (row: T) => ReactNode
  align?: 'left' | 'center' | 'right'
  /** Clase extra para las celdas de esta columna. */
  cellClassName?: string
  headerClassName?: string
  /** Oculta la columna sin cambiar el resto. */
  hidden?: boolean
}

export type TableVariant = 'default' | 'striped-blue'

/**
 * Tabla estándar del sistema con soporte para vista regular y vista institucional 'striped-blue'
 * (cabecera con el color de la marca, filas alternadas con tonos azules, sticky header opcional y responsive).
 */
export function DataTable<T>({
  columns,
  rows,
  getRowKey,
  onRowClick,
  isLoading,
  empty = 'Sin resultados.',
  className,
  tableClassName,
  dense,
  rowClassName,
  // Neutral por omisión: cabecera gris clara y filas blancas, más sobrio que la franja de color.
  variant = 'default',
  stickyHeader = false,
  footer,
}: {
  columns: Column<T>[]
  rows: T[]
  getRowKey: (row: T, index: number) => string
  onRowClick?: (row: T) => void
  isLoading?: boolean
  empty?: ReactNode
  className?: string
  tableClassName?: string
  /** Filas más compactas. */
  dense?: boolean
  /** Clase personalizada para cada fila según su índice o dato. */
  rowClassName?: (row: T, index: number) => string
  /** Variante visual: 'striped-blue' (con el color de la marca) o 'default' (gris neutral). */
  variant?: TableVariant
  /** Mantener cabecera fija al hacer scroll. */
  stickyHeader?: boolean
  /** Debajo de la tabla, dentro del mismo contenedor con borde. */
  footer?: ReactNode
}) {
  const cols = columns.filter((c) => !c.hidden)
  const alignClass = (a?: Column<T>['align']) =>
    a === 'right' ? 'text-right' : a === 'center' ? 'text-center' : 'text-left'
  const pad = dense ? 'px-3 py-2' : 'px-4 py-3.5'

  if (isLoading) return <Spinner />
  if (rows.length === 0)
    return (
      <>
        <EmptyState>{empty}</EmptyState>
        {footer}
      </>
    )

  const isStripedBlue = variant === 'striped-blue'

  return (
    <div
      className={cn(
        'w-full table-scrollbar overflow-hidden rounded-2xl border border-slate-200 bg-card shadow-2xs dark:border-slate-800',
        className,
      )}
    >
      <table className={cn('w-full text-left text-xs', tableClassName)}>
        <thead
          className={cn(
            stickyHeader && 'sticky top-0 z-10',
            isStripedBlue
              ? 'bg-brand text-white font-bold uppercase tracking-wider'
              : 'border-b border-slate-200 bg-slate-50/80 text-[11px] font-bold uppercase tracking-wider text-slate-500 dark:border-slate-800 dark:bg-slate-900/60 dark:text-slate-400',
          )}
        >
          <tr>
            {cols.map((c) => (
              <th
                key={c.key}
                className={cn(pad, 'whitespace-nowrap', alignClass(c.align), c.headerClassName)}
                scope="col"
              >
                {c.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100 dark:divide-slate-800/60">
          {rows.map((row, i) => {
            const defaultRowClass = isStripedBlue
              ? i % 2 === 0
                ? 'bg-blue-50/40 hover:bg-blue-100/70 dark:bg-slate-900/90 dark:hover:bg-blue-950/60'
                : 'bg-blue-100/50 hover:bg-blue-200/70 dark:bg-blue-950/40 dark:hover:bg-blue-900/60'
              : onRowClick
                ? 'hover:bg-slate-50/80 dark:hover:bg-slate-800/40'
                : ''

            return (
              <tr
                key={getRowKey(row, i)}
                onClick={onRowClick ? () => onRowClick(row) : undefined}
                className={cn(
                  'transition-colors',
                  onRowClick && 'cursor-pointer',
                  rowClassName ? rowClassName(row, i) : defaultRowClass,
                )}
              >
                {cols.map((c) => (
                  <td
                    key={c.key}
                    className={cn(
                      pad,
                      alignClass(c.align),
                      'text-slate-700 dark:text-slate-300',
                      c.cellClassName,
                    )}
                  >
                    {c.cell(row)}
                  </td>
                ))}
              </tr>
            )
          })}
        </tbody>
      </table>
      {footer}
    </div>
  )
}
