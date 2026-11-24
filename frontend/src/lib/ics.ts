import { descargarBlob } from './download'
/** Genera y descarga un adjunto .ics (RFC 5545) para agregar el evento al calendario del destinatario. */
export interface IcsEvent {
  title: string
  description?: string
  location?: string
  /** Fecha `YYYY-MM-DD`, evento de día completo. */
  startDate: string
  endDate: string
}

function toIcsDate(dateStr: string): string {
  return dateStr.replace(/-/g, '')
}

function escapeIcsText(text: string): string {
  return text.replace(/\\/g, '\\\\').replace(/\n/g, '\\n').replace(/,/g, '\\,').replace(/;/g, '\\;')
}

export function downloadIcs(filename: string, event: IcsEvent): void {
  const uid = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}@sorteo360.example.com`
  const dtstamp = new Date().toISOString().replace(/[-:]/g, '').split('.')[0] + 'Z'

  const lines = [
    'BEGIN:VCALENDAR',
    'VERSION:2.0',
    'PRODID:-//Sorteo360//Convocatorias//ES',
    'CALSCALE:GREGORIAN',
    'BEGIN:VEVENT',
    `UID:${uid}`,
    `DTSTAMP:${dtstamp}`,
    `DTSTART;VALUE=DATE:${toIcsDate(event.startDate)}`,
    `DTEND;VALUE=DATE:${toIcsDate(event.endDate)}`,
    `SUMMARY:${escapeIcsText(event.title)}`,
    event.location ? `LOCATION:${escapeIcsText(event.location)}` : null,
    event.description ? `DESCRIPTION:${escapeIcsText(event.description)}` : null,
    'END:VEVENT',
    'END:VCALENDAR',
  ].filter((line): line is string => line !== null)

  const blob = new Blob([lines.join('\r\n')], { type: 'text/calendar;charset=utf-8' })
  descargarBlob(blob, `${filename}.ics`)
}
