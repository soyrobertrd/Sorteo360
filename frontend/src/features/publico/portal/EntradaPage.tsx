import { useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { api } from '@/api'
import { Card } from '@/components/ui/Card'
import { Spinner } from '@/components/ui/Feedback'
import { PortalEvent, PortalNotice, PortalPage, TicketQr } from './PortalParts'
import { portalErrorMessage } from './portal'

/**
 * `/publico/entrada/<qrCode>`: la entrada de una persona inscrita. El código va en la ruta y no
 * en el fragmento porque es lo que codifica el QR (spec convocatorias/06 §2.3).
 */
export function EntradaPage() {
  const { qrCode = '' } = useParams<{ qrCode: string }>()
  const ticket = useQuery({
    queryKey: ['publico', 'entrada', qrCode],
    queryFn: () => api.getTicket(qrCode),
    retry: false,
  })

  let content
  if (ticket.isLoading) {
    content = <Spinner />
  } else if (ticket.error || !ticket.data) {
    content = (
      <PortalNotice tone="danger" title="No encontramos esta entrada">
        {portalErrorMessage(ticket.error)}
      </PortalNotice>
    )
  } else {
    const { reference, name, company, event } = ticket.data
    content = (
      <>
        <PortalEvent event={event} />
        <Card className="space-y-4 p-6 text-center">
          <div>
            <p className="font-sans text-xl font-bold text-slate-900 dark:text-white">{name}</p>
            {company && <p className="text-sm text-slate-600 dark:text-slate-300">{company}</p>}
          </div>
          <TicketQr src={api.ticketQrUrl(qrCode)} />
          <p className="text-sm text-slate-600 dark:text-slate-300">
            Referencia:{' '}
            <span className="font-mono font-bold text-slate-900 dark:text-white">{reference}</span>
          </p>
          <p className="text-xs text-slate-500 dark:text-slate-400">
            Presenta este código QR en la entrada del evento.
          </p>
        </Card>
      </>
    )
  }

  return <PortalPage>{content}</PortalPage>
}
