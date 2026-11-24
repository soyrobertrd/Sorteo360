import { FileSpreadsheet, FileText } from 'lucide-react'
import { Button } from './Button'
import { exportToExcel, exportToPdf, type ReportColumn } from '@/lib/exportUtils'

export function ExportButtons<T>({
  filename,
  title,
  sheetName,
  columns,
  rows,
  data,
  onExcel,
  onPdf,
  disabled = false,
}: {
  filename: string
  title: string
  sheetName?: string
  columns: ReportColumn<T>[]
  rows?: T[]
  data?: T[]
  /** Reemplaza el xlsx armado en el navegador, p. ej. por un CSV que arma el servicio. */
  onExcel?: () => void
  /** Reemplaza el pdf armado con `data`, p. ej. cuando hay que leer más filas antes de exportar. */
  onPdf?: () => void
  /** Deshabilita los dos botones, p. ej. mientras se prepara una exportación. */
  disabled?: boolean
}) {
  const items = rows ?? data ?? []
  const sheet = sheetName ?? title

  return (
    <div className="flex items-center gap-2">
      <Button
        type="button"
        variant="outline"
        size="sm"
        disabled={disabled || items.length === 0}
        onClick={() => (onExcel ? onExcel() : exportToExcel(filename, sheet, columns, items))}
        className="border-slate-200 bg-white font-semibold text-slate-700 hover:bg-slate-50 cursor-pointer dark:border-slate-700 dark:bg-slate-800 dark:text-slate-200"
      >
        <FileSpreadsheet size={14} className="mr-1.5" /> Excel
      </Button>
      <Button
        type="button"
        variant="outline"
        size="sm"
        disabled={disabled || items.length === 0}
        onClick={() => (onPdf ? onPdf() : exportToPdf(filename, title, columns, items))}
        className="border-slate-200 bg-white font-semibold text-slate-700 hover:bg-slate-50 cursor-pointer dark:border-slate-700 dark:bg-slate-800 dark:text-slate-200"
      >
        <FileText size={14} className="mr-1.5" /> PDF
      </Button>
    </div>
  )
}
