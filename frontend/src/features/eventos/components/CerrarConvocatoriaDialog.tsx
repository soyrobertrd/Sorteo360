import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import type { useConvocatoria } from '../useConvocatoria'

/** La pregunta antes de cerrar la convocatoria, para los dos botones que la cierran. */
export function CerrarConvocatoriaDialog({
  convocatoria,
  eventoNombre,
}: {
  convocatoria: ReturnType<typeof useConvocatoria>
  eventoNombre: string
}) {
  if (!convocatoria.confirmandoCierre) return null

  return (
    <ConfirmDialog
      title="¿Cerrar la convocatoria?"
      confirmLabel="Cerrar convocatoria"
      destructive
      pending={convocatoria.cerrando}
      error={convocatoria.cierreError}
      onConfirm={convocatoria.confirmarCierre}
      onCancel={convocatoria.cancelarCierre}
    >
      <p>
        El formulario de registro de <strong>{eventoNombre}</strong> dejará de aceptar inscripciones
        nuevas.
      </p>
      <p>
        Quienes ya se inscribieron conservan su entrada y su código QR, y no se le envía ningún
        aviso a nadie. Puedes volver a abrirla después con "Abrir convocatoria".
      </p>
    </ConfirmDialog>
  )
}
