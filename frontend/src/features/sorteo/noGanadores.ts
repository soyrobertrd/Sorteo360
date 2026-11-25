import type { PrizeDrawSummary, RaffleParticipantInput, RaffleParticipantItem } from '@/api/types'

const normalizar = (v: string | null | undefined) => v?.trim().toLowerCase() || null

/**
 * Los participantes del padrón que no tienen un premio vivo, listos para cargarse como padrón de
 * otro sorteo. Un ganador anulado no se llevó nada: pasa. El ganador se reconoce por su boleto
 * (único en el sorteo) y, si no lo tiene, por su ID de empleado.
 */
export function noGanadores(
  participantes: RaffleParticipantItem[],
  draws: PrizeDrawSummary[],
): RaffleParticipantInput[] {
  const boletos = new Set<string>()
  const ids = new Set<string>()
  for (const d of draws) {
    if (d.void !== null) continue
    const boleto = normalizar(d.winnerTicketNumber)
    const id = normalizar(d.winnerEmployeeCode)
    if (boleto) boletos.add(boleto)
    else if (id) ids.add(id)
  }

  return participantes
    .filter((p) => {
      const boleto = normalizar(p.ticketNumber)
      if (boleto && boletos.has(boleto)) return false
      const id = normalizar(p.employeeCode)
      return !(id && ids.has(id))
    })
    .map((p) => ({
      fullName: p.fullName,
      firstName: p.firstName ?? null,
      lastName: p.lastName ?? null,
      email: p.email,
      company: p.company,
      department: p.department ?? null,
      location: p.location ?? null,
      employeeCode: p.employeeCode ?? null,
      phone: p.phone ?? null,
      checkedIn: true,
    }))
}
