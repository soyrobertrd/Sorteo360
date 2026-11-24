import type { Money } from '@/api/types'

/** `US$ 50,000` / `€ 12,500.50` — formato del wireframe de alto nivel. */
export function formatMoney(money?: Money | null): string {
  if (!money) return 'US$ —'
  const symbol = money.currency === 'USD' ? 'US$' : '€'
  const amount = money.amount.toLocaleString('en-US', {
    minimumFractionDigits: 0,
    maximumFractionDigits: 2,
  })
  return `${symbol} ${amount}`
}

/** DateOnly del backend (`YYYY-MM-DD`) → `12 mar 2026`. */
export function formatDate(iso?: string | null): string {
  if (!iso) return '—'
  const [y, m, d] = iso.split('-').map(Number)
  return new Date(y, m - 1, d).toLocaleDateString('es-DO', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  })
}

/** Un instante del backend (ISO con hora) → `2 nov`, en la hora local, sin año. */
export function formatDayMonth(iso: string): string {
  return new Date(iso).toLocaleDateString('es-DO', { day: 'numeric', month: 'short' })
}

export function formatDateRange(desde: string, hasta: string): string {
  return desde === hasta ? formatDate(desde) : `${formatDate(desde)} – ${formatDate(hasta)}`
}

/** Timestamp ISO-8601 → `12 mar 2026, 10:45 a. m.` */
export function formatDateTime(iso?: string | null): string {
  if (!iso) return '—'
  return new Date(iso).toLocaleString('es-DO', { dateStyle: 'medium', timeStyle: 'short' })
}

/** Timestamp ISO-8601 o milisegundos → `06:42 p. m.` */
export function formatTime(at: string | number): string {
  return new Date(at).toLocaleTimeString('es-DO', { hour: '2-digit', minute: '2-digit' })
}

/** El nombre de una persona como se muestra: nombre y apellido. */
export function fullName(person: { firstName: string; lastName: string }): string {
  return `${person.firstName} ${person.lastName}`.trim()
}
