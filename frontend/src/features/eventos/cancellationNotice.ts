import { api } from '@/api'

export interface CancellationNoticeOutcome {
  /** `false` cuando alguien quedó sin aviso o la llamada falló: la pantalla lo tiene que decir. */
  ok: boolean
  message: string
}

function inscritos(n: number): string {
  return n === 1 ? '1 inscrito' : `${n} inscritos`
}

/**
 * Segunda llamada del diálogo de cancelar (spec `06` §4.7): con el evento ya cancelado en
 * events-service, pide a convocatorias-service el aviso a los inscritos. Nunca lanza: un aviso
 * fallido no deshace ni esconde la cancelación. Volver a pedirlo es seguro, porque el servicio solo
 * le escribe a quien sigue sin aviso.
 */
export async function sendCancellationNotice(eventId: string): Promise<CancellationNoticeOutcome> {
  try {
    const { notified, failed } = await api.sendCancellationNotice(eventId)
    if (failed > 0) {
      return {
        ok: false,
        message: `El evento quedó cancelado. Se avisó a ${inscritos(notified)}, pero a ${inscritos(failed)} no les llegó el aviso; se puede reintentar sin duplicar correos.`,
      }
    }
    return {
      ok: true,
      message:
        notified > 0
          ? `Evento cancelado. Se avisó por correo a ${inscritos(notified)}.`
          : 'Evento cancelado. No había inscritos pendientes de aviso.',
    }
  } catch (error) {
    const motivo = error instanceof Error ? ` (${error.message})` : ''
    return {
      ok: false,
      message: `El evento quedó cancelado, pero el aviso a los inscritos no se pudo enviar${motivo}. Se puede reintentar sin duplicar correos.`,
    }
  }
}
