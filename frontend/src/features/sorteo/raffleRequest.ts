import type { RaffleResponse, UpdateRaffleRequest } from '@/api/types'

/** Lo que un panel cambia del sorteo. Lo que no menciona se manda como está. */
export type RaffleChanges = Partial<UpdateRaffleRequest>

const TEXT_FIELDS = [
  'notaryName',
  'notaryIdentityDocument',
  'witnessOneName',
  'witnessOneIdentityDocument',
  'witnessTwoName',
  'witnessTwoIdentityDocument',
  'pickupInstructions',
] as const

/**
 * El único lugar que arma el PATCH del sorteo (spec 00 §12, punto 2). El servicio reemplaza la
 * representación editable completa y borra lo opcional omitido, así que un panel que mandara solo
 * su parte dejaría en blanco las de los demás. Se parte de la última lectura y se le pone encima
 * lo que cambió; un texto en blanco viaja como nulo.
 */
export function toUpdateRaffleRequest(
  raffle: RaffleResponse,
  changes: RaffleChanges,
): UpdateRaffleRequest {
  const request: UpdateRaffleRequest = {
    visualStyle: raffle.visualStyle,
    showPrizeOnScreen: raffle.showPrizeOnScreen,
    animationSeconds: raffle.animationSeconds,
    requiresPresenceConfirmation: raffle.requiresPresenceConfirmation,
    requiresCheckIn: raffle.requiresCheckIn ?? true,
    onePrizePerPerson: raffle.onePrizePerPerson,
    onePrizePerCompany: raffle.onePrizePerCompany,
    linkedEventId: raffle.linkedEventId ?? null,
    notaryName: raffle.notaryName,
    notaryIdentityDocument: raffle.notaryIdentityDocument,
    witnessOneName: raffle.witnessOneName,
    witnessOneIdentityDocument: raffle.witnessOneIdentityDocument,
    witnessTwoName: raffle.witnessTwoName,
    witnessTwoIdentityDocument: raffle.witnessTwoIdentityDocument,
    pickupInstructions: raffle.pickupInstructions,
  }
  for (const [key, value] of Object.entries(changes)) {
    if (value !== undefined) Object.assign(request, { [key]: value })
  }
  for (const field of TEXT_FIELDS) {
    request[field] = request[field]?.trim() || null
  }
  return request
}
