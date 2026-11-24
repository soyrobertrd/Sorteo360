import type { CertificateSigner, SetCertificateSignersRequest } from '@/api/types'

/** Una línea de firma del certificado, tal como se escribe en pantalla. */
export interface Firmante {
  nombre: string
  cargo: string
}

const SIN_FIRMANTE: Firmante = { nombre: '', cargo: '' }

/** El tope del nombre y del cargo de un firmante, el del validator del servicio. */
export const CERTIFICATE_SIGNER_MAX_LENGTH = 100

/** Una línea sin nombre ni cargo: no viaja, y el certificado la deja para firmar a mano. */
export function lineaEnBlanco(firmante: Firmante): boolean {
  return firmante.nombre.trim() === '' && firmante.cargo.trim() === ''
}

/**
 * El cuerpo del PUT de los firmantes: recortados y en el orden de sus líneas. Una línea en blanco no
 * viaja; una a medias sí, para que el servicio diga qué campo le falta en vez de perderla callado.
 */
export function toSetCertificateSignersRequest(
  firmantes: Firmante[],
): SetCertificateSignersRequest {
  return {
    signers: firmantes
      .filter((f) => !lineaEnBlanco(f))
      .map((f) => ({ name: f.nombre.trim(), title: f.cargo.trim() })),
  }
}

/** Las dos líneas del certificado, a partir de los firmantes guardados; las que faltan, en blanco. */
export function firmantesDelCertificado(signers: CertificateSigner[]): [Firmante, Firmante] {
  const [primero, segundo] = signers.map((s) => ({ nombre: s.name, cargo: s.title }))
  return [primero ?? SIN_FIRMANTE, segundo ?? SIN_FIRMANTE]
}
