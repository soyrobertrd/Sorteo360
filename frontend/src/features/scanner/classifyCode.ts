/** Lo que el terminal entiende de un código leído (spec convocatorias/10 §2). */
export type ScannedCode =
  | { kind: 'ticket'; qrCode: string }
  /** `code` en su forma canónica, `RD-XXXX-XXXX`. */
  | { kind: 'prize'; code: string }
  /** Se busca en la lista de la puerta; nunca hace check-in por sí sola (§2.2). */
  | { kind: 'reference'; reference: string }
  | { kind: 'unrecognised' }

const TICKET_PATH = /\/publico\/entrada\/([^/]+)\/?$/

/** 32 bytes en base64url, sin relleno: 43 caracteres. */
const QR_CODE = /^[A-Za-z0-9_-]{43}$/

/** `RD` y ocho caracteres Crockford base-32, sin `I L O U`, ya sin guiones ni espacios. */
const PRIZE_CODE = /^RD([0-9A-HJKMNP-TV-Z]{4})([0-9A-HJKMNP-TV-Z]{4})$/

/** Referencias corporativas o heredadas (`EMP-...`, `REF-...`). */
const REFERENCE = /^(EMP|REF)-\d{4}-\d+$/

/** Códigos QR alfanuméricos cortos de participantes importados (`QR-...`). */
const SIMPLE_QR = /^QR-[A-Za-z0-9_-]{4,}$/

export function classifyCode(input: string): ScannedCode {
  const text = input.trim()
  const fromUrl = ticketFromUrl(text)
  if (fromUrl) return { kind: 'ticket', qrCode: fromUrl }
  if (QR_CODE.test(text) || SIMPLE_QR.test(text)) return { kind: 'ticket', qrCode: text }
  const prize = PRIZE_CODE.exec(text.toUpperCase().replace(/[-\s]/g, ''))
  if (prize) return { kind: 'prize', code: `RD-${prize[1]}-${prize[2]}` }
  const reference = text.toUpperCase()
  if (REFERENCE.test(reference)) return { kind: 'reference', reference }
  return { kind: 'unrecognised' }
}

/** Solo cuenta la ruta: el host se ignora (§2.1). */
function ticketFromUrl(text: string): string | null {
  let url: URL
  try {
    url = new URL(text)
  } catch {
    return null
  }
  return TICKET_PATH.exec(url.pathname)?.[1] ?? null
}
