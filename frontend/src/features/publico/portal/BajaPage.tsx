import { useMutation } from '@tanstack/react-query'
import { api } from '@/api'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { Callout } from '@/components/ui/Callout'
import { apiErrorCodeLabel } from '@/lib/labels'
import { PortalNotice, PortalPage } from './PortalParts'
import { portalErrorMessage, useFragmentParam } from './portal'

/**
 * `/publico/baja#u=<token>`: dejar de recibir invitaciones. Abrir el enlace no da de baja a
 * nadie; lo hace el botón, para que un escáner de correo que precarga enlaces no dé de baja a
 * una lista entera en silencio (spec convocatorias/06 §2.4).
 */
export function BajaPage() {
  const token = useFragmentParam('u')
  const mutation = useMutation({ mutationFn: () => api.unsubscribe(token!) })

  let content
  if (!token) {
    content = (
      <PortalNotice tone="danger" title="Enlace incompleto">
        {apiErrorCodeLabel.PUBLIC_UNSUBSCRIBE_NOT_FOUND}
      </PortalNotice>
    )
  } else if (mutation.data) {
    content = (
      <PortalNotice tone="success" title="Baja confirmada">
        La dirección <strong>{mutation.data.maskedEmail}</strong> ya no recibirá invitaciones del la
        organización. Si no reconoces esa dirección, el correo te llegó reenviado y tu suscripción
        no cambió.
      </PortalNotice>
    )
  } else {
    content = (
      <Card className="space-y-4 p-6">
        <h1 className="font-sans text-lg font-bold text-slate-900 dark:text-white">
          Dejar de recibir invitaciones
        </h1>
        <p className="text-sm text-slate-600 dark:text-slate-300">
          Al confirmar, la dirección a la que llegó este correo dejará de recibir invitaciones a
          eventos de la organización. Podrás seguir inscribiéndote desde la cartelera pública.
        </p>
        {mutation.error && <Callout tone="danger">{portalErrorMessage(mutation.error)}</Callout>}
        <div className="flex justify-end">
          <Button type="button" onClick={() => mutation.mutate()} disabled={mutation.isPending}>
            {mutation.isPending ? 'Procesando…' : 'Confirmar la baja'}
          </Button>
        </div>
      </Card>
    )
  }

  return <PortalPage>{content}</PortalPage>
}
