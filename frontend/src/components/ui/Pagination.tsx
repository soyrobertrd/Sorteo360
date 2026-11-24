import { ChevronLeft, ChevronRight } from 'lucide-react'
import { cn } from '@/lib/utils'
import { IconButton } from './IconButton'

/**
 * Controles de paginación estándar (anterior / página X de N / siguiente), con conteo
 * opcional. Reemplaza los ~5 paginadores reimplementados en los listados.
 */
export function Pagination({
  page,
  pageCount,
  onPageChange,
  total,
  className,
}: {
  /** Página actual, 1-indexada. */
  page: number
  pageCount: number
  onPageChange: (page: number) => void
  /** Total de elementos, para el texto "N resultados". */
  total?: number
  className?: string
}) {
  if (pageCount <= 1 && total == null) return null

  return (
    <div
      className={cn(
        'flex flex-wrap items-center justify-between gap-3 text-xs text-slate-500 dark:text-slate-400',
        className,
      )}
    >
      <span>
        {total != null && (
          <>
            {total.toLocaleString()} {total === 1 ? 'resultado' : 'resultados'}
            {pageCount > 1 && ' · '}
          </>
        )}
        {pageCount > 1 && (
          <>
            Página <span className="font-bold text-slate-700 dark:text-slate-200">{page}</span> de{' '}
            {pageCount}
          </>
        )}
      </span>

      {pageCount > 1 && (
        <div className="flex items-center gap-1">
          <IconButton
            aria-label="Página anterior"
            disabled={page <= 1}
            onClick={() => onPageChange(page - 1)}
            className="border border-slate-200 dark:border-slate-700"
          >
            <ChevronLeft size={16} />
          </IconButton>
          <IconButton
            aria-label="Página siguiente"
            disabled={page >= pageCount}
            onClick={() => onPageChange(page + 1)}
            className="border border-slate-200 dark:border-slate-700"
          >
            <ChevronRight size={16} />
          </IconButton>
        </div>
      )}
    </div>
  )
}
