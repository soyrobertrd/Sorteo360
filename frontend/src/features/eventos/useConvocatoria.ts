import { useState } from 'react'
import { useCan } from '@/auth/AuthProvider'
import { ROLES } from '@/auth/roles'
import { useEventTracking, useToggleRegistrations } from '@/features/convocatorias/tracking'
import type { EventoDetail } from '@/api/types'
import { useUpdateEvento } from './hooks'

/**
 * El estado de la convocatoria de un evento y el botón que la abre y la cierra. Lo comparten
 * la cabecera de la ficha y el área de convocatoria, para que los dos botones digan y hagan
 * lo mismo.
 *
 * Son dos cosas distintas. `externalUrl` es la marca de que la convocatoria se abrió alguna
 * vez: se pone al abrirla y ya no se quita, y check-in la lee igual. Lo que habilita el
 * formulario público es la ventana de inscripción de convocatorias-service, que es la que el
 * botón abre y cierra.
 */
export function useConvocatoria(evento: EventoDetail) {
  const updateEvento = useUpdateEvento(evento.id)
  const tracking = useEventTracking(evento.id)
  const toggleRegistrations = useToggleRegistrations(evento.id)
  const canWrite = useCan(ROLES.campaignsWrite)
  const [confirmandoCierre, setConfirmandoCierre] = useState(false)

  const publicUrl = `${window.location.origin}/publico/eventos/${evento.id}`
  const iniciada = Boolean(evento.externalUrl)
  const ventana = tracking.data?.registrationWindow
  const registroAbierto = ventana?.open ?? false
  // El servicio da por abierta la ventana de un evento recién creado: si el cierre al crear
  // no llegó a hacerse, el formulario acepta inscripciones aunque acá nadie haya abierto nada.
  const abiertaSinIniciar = !iniciada && ventana?.open === true
  const guardando = toggleRegistrations.isPending || updateEvento.isPending

  const marcarIniciada = () => {
    updateEvento.mutate({
      body: {
        name: evento.name,
        startDate: evento.startDate,
        endDate: evento.endDate,
        // Nunca nulo: el PATCH lo exige. La ficha no edita el idioma, así que viaja tal como
        // vino, y solo cae a español si el evento es anterior a que fuera obligatorio.
        language: evento.language ?? 'es',
        // Tampoco edita la visibilidad: viaja tal como vino, para no despublicar nada.
        visibility: evento.visibility,
        // Lo que la ficha no edita viaja tal como vino: si lo omite, el PATCH lo borra.
        seatingLayout: evento.seatingLayout ?? null,
        invitedCount: evento.invitedCount ?? null,
        estimatedBudget: evento.estimatedBudget ?? null,
        description: evento.description ?? null,
        externalUrl: publicUrl,
      },
    })
  }

  // Abrir es inmediato. Cerrar deja de aceptar inscripciones a la vista de todo el que tenga
  // el enlace, así que primero se pregunta (`CerrarConvocatoriaDialog`).
  const toggle = () => {
    if (registroAbierto) {
      setConfirmandoCierre(true)
      return
    }
    toggleRegistrations.mutate(true, {
      onSuccess: () => {
        if (!iniciada) marcarIniciada()
      },
    })
  }

  const confirmarCierre = () => {
    toggleRegistrations.mutate(false, { onSuccess: () => setConfirmandoCierre(false) })
  }

  const cancelarCierre = () => {
    toggleRegistrations.reset()
    setConfirmandoCierre(false)
  }

  return {
    publicUrl,
    iniciada,
    ventana,
    registroAbierto,
    abiertaSinIniciar,
    guardando,
    /** Sin la ventana leída no se sabe qué haría el botón, y sin permiso el servicio lo niega. */
    disabled: !ventana || guardando || !canWrite.allowed,
    title: canWrite.title,
    toggle,
    confirmandoCierre,
    confirmarCierre,
    cancelarCierre,
    cerrando: toggleRegistrations.isPending,
    /** Mientras la pregunta está abierta, el fallo del cierre se muestra dentro de ella. */
    cierreError: confirmandoCierre ? toggleRegistrations.error : null,
    consultando: tracking.isLoading,
    /** La lectura de la ventana, el cambio de la ventana y la marca del evento, en ese orden. */
    errors: [
      tracking.error,
      confirmandoCierre ? null : toggleRegistrations.error,
      updateEvento.error,
    ].filter((error) => error != null),
  }
}
