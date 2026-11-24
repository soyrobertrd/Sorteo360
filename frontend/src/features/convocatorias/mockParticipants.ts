import type { EventoListItem } from '@/api/types'
import { campanaMetricsFor, campanaStatusFor } from './campanaMetrics'

/**
 * Datos simulados para las secciones 2-6 del diseño de "Invitación, Registro y
 * Check-in" (audiencia/CRM — sección 1 — se deja fuera a propósito, vive en el
 * módulo CRM). Es la parte que convocatorias-service no sirve, o que solo existe
 * en modo mock, y es determinista por `evento.id`: el mismo evento siempre
 * genera los mismos invitados y campañas, sin importar cuántas veces se
 * recalcule ni en qué pantalla se mire. Las campañas simuladas solo se suman a
 * la tabla en modo mock (`useCampanasTabla`); las reales salen del servicio.
 *
 * Invitados ya lee la lista real de convocatorias-service (`participants.ts`): de acá solo
 * toma la mitad que el servicio no tiene (tipo, puntaje, RSVP y lista de espera), y el mock de
 * la API siembra con estos invitados la lista de cada evento.
 */

// ---------- PRNG determinista (mulberry32) sembrado por el id del evento ----------

function hashStringToInt(str: string): number {
  let hash = 2166136261
  for (let i = 0; i < str.length; i++) {
    hash ^= str.charCodeAt(i)
    hash = Math.imul(hash, 16777619)
  }
  return hash >>> 0
}

function mulberry32(seed: number) {
  let state = seed
  return function next(): number {
    state |= 0
    state = (state + 0x6d2b79f5) | 0
    let t = Math.imul(state ^ (state >>> 15), 1 | state)
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296
  }
}

function pick<T>(rng: () => number, items: readonly T[]): T {
  return items[Math.floor(rng() * items.length)]
}

// ---------- Catálogos (sección 3.8 y leyenda de estados) ----------

export const PARTICIPANT_TYPES = [
  'Invitado General',
  'Buyer',
  'Supplier',
  'Prensa',
  'Influencer',
  'Expositor',
  'Patrocinador',
  'VIP',
  'Autoridad',
  'Staff',
] as const
export type ParticipantType = (typeof PARTICIPANT_TYPES)[number]

/**
 * Los 4 estados posibles de una persona al recibir una invitación a un evento:
 * - Invitado (INVITED)
 * - Registrado (REGISTERED)
 * - Check-in (CHECKED_IN)
 * - No asistiré (NO_ASISTIRE)
 */
export const ESTADOS_INVITACION = ['INVITED', 'REGISTERED', 'CHECKED_IN', 'NO_ASISTIRE'] as const

export type EstadoInvitacion = (typeof ESTADOS_INVITACION)[number]

export const ESTADO_INVITACION_LABEL: Record<EstadoInvitacion, string> = {
  INVITED: 'Invitado',
  REGISTERED: 'Registrado',
  CHECKED_IN: 'Check-in',
  NO_ASISTIRE: 'No asistiré',
}

export type ParticipantStatus =
  | 'INVITED'
  | 'REGISTERED'
  | 'CHECKED_IN'
  | 'NO_ASISTIRE'
  | 'RSVP_YES'
  | 'RSVP_NO'
  | 'RSVP_MAYBE'
  | 'WAITLIST'
  | 'CONFIRMED'
  | 'NO_SHOW'
  | 'CANCELLED'
  | 'RAFFLE_ELIGIBLE'
  | 'RAFFLE_WINNER'

/** Los estados locales que implican una inscripción hecha (`registered` en el servicio). */
export const ESTADOS_INSCRITOS: readonly ParticipantStatus[] = [
  'REGISTERED',
  'CONFIRMED',
  'CHECKED_IN',
  'RAFFLE_ELIGIBLE',
  'RAFFLE_WINNER',
]

export const PARTICIPANT_STATUS_LABEL: Record<ParticipantStatus, string> = {
  INVITED: 'Invitado',
  REGISTERED: 'Registrado',
  CHECKED_IN: 'Check-in',
  NO_ASISTIRE: 'No asistiré',
  RSVP_YES: 'Registrado',
  RSVP_NO: 'No asistiré',
  RSVP_MAYBE: 'Invitado',
  WAITLIST: 'Registrado',
  CONFIRMED: 'Registrado',
  NO_SHOW: 'No asistiré',
  CANCELLED: 'No asistiré',
  RAFFLE_ELIGIBLE: 'Check-in',
  RAFFLE_WINNER: 'Check-in (Ganador)',
}

const FIRST_NAMES = [
  'María',
  'Carlos',
  'Ana',
  'Luis',
  'Sofía',
  'Diego',
  'Valentina',
  'Miguel',
  'Laura',
  'Roberto',
  'Elena',
  'Fernando',
  'Isabella',
  'Andrés',
  'Camila',
  'James',
  'Sarah',
  'Michael',
  'Emily',
  'David',
  'Jennifer',
  'Robert',
  'Jessica',
  'William',
  'Ashley',
  'Jean-Marc',
  'Dominique',
  'Claire',
] as const

const LAST_NAMES = [
  'González',
  'Rodríguez',
  'Martínez',
  'López',
  'Pérez',
  'Fernández',
  'Smith',
  'Johnson',
  'Williams',
  'Brown',
  'Jones',
  'García',
  'Sánchez',
  'Torres',
  'Ramírez',
  'Mitchell',
  'Rostova',
  'Fontaine',
  'Laurent',
] as const

const COMPANIES = [
  'TUI Group Europe',
  'Expedia Group',
  'Iberia Líneas Aéreas',
  'Meliá Hotels International',
  'Travel Weekly USA',
  'Air France-KLM',
  'Booking.com',
  'Marriott International',
  'Delta Air Lines',
  'American Express Travel',
  'Condor Airlines',
  'Journeys Unlimited',
  'Global Tours Alliance',
  'Sunwing Travel Group',
  'Prensa Independiente',
] as const

export interface Invitado {
  id: string
  /** Número de boleto del sorteo, si lo tiene: se imprime en el gafete. */
  boleto?: string | null
  eventoId: string
  eventoNombre: string
  nombre: string
  empresa: string
  email: string
  tipo: ParticipantType
  status: ParticipantStatus
  score: number
  /** Fecha en que se envió la invitación (YYYY-MM-DD o ISO) */
  fechaInvitado?: string | null
  /** Fecha en que se completó el registro / RSVP (YYYY-MM-DD o ISO) */
  fechaRegistro?: string | null
  /** `detailsDiffer` del servicio: la inscripción no coincide con el CRM. */
  detailsDiffer?: boolean
}

/**
 * Tipo y puntaje para una persona que el mock no generó: el servicio no los tiene, así que se
 * siembran por correo para que la misma persona muestre siempre lo mismo.
 */
export function rasgosLocalesPorCorreo(email: string): Pick<Invitado, 'tipo' | 'score'> {
  const rng = mulberry32(hashStringToInt(email.trim().toLowerCase()))
  return { tipo: pick(rng, PARTICIPANT_TYPES), score: Math.round(20 + rng() * 200) }
}

/**
 * El estado de cada invitado respeta la línea de tiempo real del evento: uno que
 * todavía no ocurrió no puede tener check-in ni no-show, y uno cancelado no
 * reparte invitados vivos.
 */
function statusPoolFor(evento: Pick<EventoListItem, 'status'>): readonly ParticipantStatus[] {
  if (evento.status === 'cancelled') {
    return ['NO_ASISTIRE', 'INVITED']
  }
  if (evento.status === 'closed') {
    return ['CHECKED_IN', 'CHECKED_IN', 'CHECKED_IN', 'CHECKED_IN', 'REGISTERED', 'NO_ASISTIRE']
  }
  // planning / in_progress: los 4 estados canónicos: Invitado, Registrado, Check-in, No asistiré
  return [
    'INVITED',
    'INVITED',
    'REGISTERED',
    'REGISTERED',
    'REGISTERED',
    'CHECKED_IN',
    'NO_ASISTIRE',
  ]
}

export function invitadosFor(evento: EventoListItem): Invitado[] {
  if (!evento.externalUrl) return []
  const rng = mulberry32(hashStringToInt(evento.id))
  const metrics = campanaMetricsFor(evento)
  const total = Math.min(60, Math.max(8, Math.round(metrics.registrados * 1.3)))
  const pool = statusPoolFor(evento)

  const invitados: Invitado[] = []
  const startDateStr = evento.startDate || '2026-08-15'
  const baseDate = new Date(startDateStr)

  for (let i = 0; i < total; i++) {
    const nombre = `${pick(rng, FIRST_NAMES)} ${pick(rng, LAST_NAMES)}`
    const empresa = pick(rng, COMPANIES)
    let status = pick(rng, pool)
    // Exactamente un ganador de rifa por evento cerrado, y solo entre los elegibles.
    if (status === 'RAFFLE_ELIGIBLE' && i === total - 1 && evento.status === 'closed') {
      status = 'RAFFLE_WINNER'
    }

    // Cálculo determinista de fechas de invitación y registro
    const daysBefore = Math.floor(15 + rng() * 30)
    const invDate = new Date(baseDate.getTime() - daysBefore * 86400000)
    const fechaInvitado = invDate.toISOString().slice(0, 10)

    let fechaRegistro: string | null = null
    if (status !== 'INVITED') {
      const regDaysAfter = Math.floor(1 + rng() * 10)
      const regDate = new Date(invDate.getTime() + regDaysAfter * 86400000)
      fechaRegistro = regDate.toISOString().slice(0, 10)
    }

    invitados.push({
      id: `${evento.id}-inv-${i}`,
      eventoId: evento.id,
      eventoNombre: evento.name,
      nombre,
      empresa,
      email: `${nombre.toLowerCase().replace(/\s+/g, '.').normalize('NFD').replace(/[̀-ͯ]/g, '')}@${empresa.toLowerCase().split(' ')[0]}.com`,
      tipo: pick(rng, PARTICIPANT_TYPES),
      status,
      score: Math.round(20 + rng() * 200),
      fechaInvitado,
      fechaRegistro,
    })
  }
  return invitados
}

// ---------- Campañas (sección 2) ----------

export const CAMPANA_TIPOS = [
  'Save the Date',
  'Apertura de Registro',
  'Reminder',
  'Venue Confirmado',
  'Pase VIP & Acreditación',
  'Convocatoria de Prensa & Medios',
  'Agenda & Programa Oficial',
  'Confirmación de RSVP',
  'Guía del Participante & Protocolo',
  'Rueda de Negocios B2B',
  'Actualización de Datos CRM (Agencias / Tour Operadores)',
  'Cambio de Fecha',
  'Cambio de Venue',
  'Posposición',
  'Cancelación',
  'Carta de Agradecimiento',
  'Encuesta Post Evento',
  'Resumen & Highlights del Evento',
  'Certificado de Asistencia',
] as const

export type CampanaTipo = (typeof CAMPANA_TIPOS)[number]

export const CAMPANA_IDIOMAS = ['ES', 'EN', 'FR', 'PT', 'DE', 'IT'] as const
export type CampanaIdioma = (typeof CAMPANA_IDIOMAS)[number]

export interface Campana {
  id: string
  eventoId: string
  eventoNombre: string
  tipo: CampanaTipo
  idioma: CampanaIdioma
  fechaEnvio: string
  enviados: number
  entregados: number
  rebotados: number
  reenviados: number
  estado: 'Enviada' | 'Enviando' | 'Programada' | 'PendienteAprobacion' | 'Rechazada'
}

/** Cada evento con campaña lanzada tiene al menos un envío de apertura, y uno de recordatorio si sigue vigente. */
export function campanasFor(evento: EventoListItem): Campana[] {
  if (!evento.externalUrl) return []
  const rng = mulberry32(hashStringToInt(evento.id) ^ 0x9e3779b9)
  const metrics = campanaMetricsFor(evento)
  const idioma = pick(rng, CAMPANA_IDIOMAS)
  const estadoGeneral = campanaStatusFor(evento)

  const campanas: Campana[] = [
    {
      id: `${evento.id}-camp-apertura`,
      eventoId: evento.id,
      eventoNombre: evento.name,
      tipo: 'Apertura de Registro',
      idioma,
      fechaEnvio: evento.updatedAt.slice(0, 10),
      enviados: metrics.enviados,
      entregados: metrics.enviados - metrics.rechazados,
      rebotados: metrics.rechazados,
      reenviados: Math.round(metrics.rechazados * 0.6),
      estado: 'Enviada',
    },
  ]

  if (estadoGeneral === 'ACTIVA') {
    const reminderEnviados = Math.round(metrics.enviados * 0.4)
    campanas.push({
      id: `${evento.id}-camp-reminder`,
      eventoId: evento.id,
      eventoNombre: evento.name,
      tipo: 'Reminder',
      idioma,
      fechaEnvio: evento.updatedAt.slice(0, 10),
      enviados: reminderEnviados,
      entregados: Math.round(reminderEnviados * 0.97),
      rebotados: Math.round(reminderEnviados * 0.03),
      reenviados: Math.round(reminderEnviados * 0.02),
      estado: 'Enviada',
    })
    campanas.push({
      id: `${evento.id}-camp-pending-approval`,
      eventoId: evento.id,
      eventoNombre: evento.name,
      tipo: 'Venue Confirmado',
      idioma,
      fechaEnvio: evento.updatedAt.slice(0, 10),
      enviados: 150,
      entregados: 0,
      rebotados: 0,
      reenviados: 0,
      estado: 'PendienteAprobacion',
    })
  } else if (estadoGeneral === 'COMPLETADA') {
    campanas.push({
      id: `${evento.id}-camp-encuesta`,
      eventoId: evento.id,
      eventoNombre: evento.name,
      tipo: 'Encuesta Post Evento',
      idioma,
      fechaEnvio: evento.updatedAt.slice(0, 10),
      enviados: metrics.registrados,
      entregados: Math.round(metrics.registrados * 0.96),
      rebotados: Math.round(metrics.registrados * 0.04),
      reenviados: 0,
      estado: 'Enviada',
    })
  }

  return campanas
}

export function campanasDeTodosLosEventos(eventos: EventoListItem[]): Campana[] {
  return eventos.flatMap((e) => campanasFor(e))
}

// ---------- Detalle de envío por destinatario (sección 2 — cola de envío) ----------

export type DestinatarioEstado = 'Entregado' | 'Abierto' | 'Click' | 'Rebotado'

export interface CampanaDestinatario {
  nombre: string
  empresa: string
  email: string
  estado: DestinatarioEstado
}

/** Muestra determinista de destinatarios de una campaña, para el detalle de envío. */
export function destinatariosFor(
  campana: Pick<Campana, 'id' | 'enviados' | 'rebotados'>,
): CampanaDestinatario[] {
  const rng = mulberry32(hashStringToInt(campana.id) ^ 0x51ed270b)
  const total = Math.min(25, Math.max(6, Math.round(campana.enviados / 20)))
  const rebotadoRatio = campana.enviados > 0 ? campana.rebotados / campana.enviados : 0

  const items: CampanaDestinatario[] = []
  for (let i = 0; i < total; i++) {
    const nombre = `${pick(rng, FIRST_NAMES)} ${pick(rng, LAST_NAMES)}`
    const empresa = pick(rng, COMPANIES)
    const roll = rng()
    let estado: DestinatarioEstado
    if (roll < rebotadoRatio) estado = 'Rebotado'
    else if (roll < rebotadoRatio + 0.25) estado = 'Click'
    else if (roll < rebotadoRatio + 0.6) estado = 'Abierto'
    else estado = 'Entregado'

    items.push({
      nombre,
      empresa,
      email: `${nombre.toLowerCase().replace(/\s+/g, '.').normalize('NFD').replace(/[̀-ͯ]/g, '')}@${empresa.toLowerCase().split(' ')[0]}.com`,
      estado,
    })
  }
  return items
}
