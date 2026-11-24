import * as XLSX from 'xlsx'
import jsPDF from 'jspdf'
import autoTable from 'jspdf-autotable'

export interface ReportColumn<T> {
  header: string
  accessor: (row: T) => string | number
}

/** Exporta filas a un .xlsx con una sola hoja, usando las columnas ya formateadas para lectura. */
export function exportToExcel<T>(
  filename: string,
  sheetName: string,
  columns: ReportColumn<T>[],
  rows: T[],
): void {
  const data = rows.map((row) => {
    const record: Record<string, string | number> = {}
    for (const col of columns) record[col.header] = col.accessor(row)
    return record
  })
  const worksheet = XLSX.utils.json_to_sheet(data)
  const workbook = XLSX.utils.book_new()
  XLSX.utils.book_append_sheet(workbook, worksheet, sheetName.slice(0, 31))
  XLSX.writeFile(workbook, `${filename}.xlsx`)
}

/** Exporta filas a un .pdf en horizontal, con título y fecha de generación sobre la tabla. */
export function exportToPdf<T>(
  filename: string,
  title: string,
  columns: ReportColumn<T>[],
  rows: T[],
): void {
  const doc = new jsPDF({ orientation: 'landscape' })
  doc.setFontSize(14)
  doc.text(title, 14, 15)
  doc.setFontSize(9)
  doc.setTextColor(100)
  doc.text(`Generado el ${new Date().toLocaleDateString('es-DO', { dateStyle: 'long' })}`, 14, 21)

  autoTable(doc, {
    startY: 26,
    head: [columns.map((c) => c.header)],
    body: rows.map((row) => columns.map((c) => String(c.accessor(row)))),
    styles: { fontSize: 8, cellPadding: 3 },
    headStyles: { fillColor: [4, 17, 77] },
    alternateRowStyles: { fillColor: [248, 250, 252] },
  })

  doc.save(`${filename}.pdf`)
}
