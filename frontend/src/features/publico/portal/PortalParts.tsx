import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { Calendar, MapPin } from 'lucide-react'
import type { PublicEventoDetail } from '@/api/types'
import { Card } from '@/components/ui/Card'
import { Callout, type CalloutTone } from '@/components/ui/Callout'
import { formatDateRange } from '@/lib/format'

/** El marco de las tres páginas de los enlaces del correo, dentro de `PublicLayout`. */
export function PortalPage({ children }: { children: ReactNode }) {
  return <div className="mx-auto w-full max-w-2xl space-y-6 px-4 py-10 sm:py-14">{children}</div>
}

/** Nombre, fechas y sedes del evento, con la cabecera azul del detalle público. */
export function PortalEvent({ event }: { event: PublicEventoDetail }) {
  const sedes = event.cities.map((c) => `${c.name}, ${c.country}`).join(' · ')
  return (
    <Card className="overflow-hidden p-0">
      <div className="bg-brand-deep px-6 py-4">
        <h1 className="font-sans text-lg font-bold text-white">{event.name}</h1>
      </div>
      <div className="space-y-2 p-6 text-sm text-slate-700 dark:text-slate-300">
        <p className="flex items-center gap-2">
          <Calendar size={16} className="shrink-0 text-blue-600" />
          {formatDateRange(event.startDate, event.endDate)}
        </p>
        {sedes && (
          <p className="flex items-center gap-2">
            <MapPin size={16} className="shrink-0 text-rose-600" />
            {sedes}
          </p>
        )}
      </div>
    </Card>
  )
}

/** Un estado final de la página: enlace inválido, puerta cerrada, demasiados intentos. */
export function PortalNotice({
  tone,
  title,
  children,
}: {
  tone: CalloutTone
  title: string
  children: ReactNode
}) {
  return (
    <div className="space-y-4">
      <Callout tone={tone} title={title}>
        {children}
      </Callout>
      <Link
        to="/publico/eventos"
        className="inline-block text-sm font-bold text-blue-700 hover:underline dark:text-blue-400"
      >
        Ver la cartelera de eventos
      </Link>
    </div>
  )
}

/** La imagen del QR de una entrada, servida por convocatorias-service. */
export function TicketQr({ src }: { src: string }) {
  return (
    <img
      src={src}
      alt="Código QR de la entrada"
      width={200}
      height={200}
      className="mx-auto h-50 w-50 rounded-lg border border-slate-200 bg-white p-2"
    />
  )
}
