/**
 * La clave de una persona dentro de un evento, igual que `PersonIdentity` de events-service: el
 * correo normalizado si lo tiene y, si no, `id:` + su ID recortado y en minúsculas. Es interna:
 * nunca se muestra ni se usa como dirección.
 */
export function claveDePersona(
  email: string | null | undefined,
  employeeCode: string | null | undefined,
): string | null {
  const correo = email?.trim().toLowerCase()
  if (correo) return correo
  const id = employeeCode?.trim().toLowerCase()
  return id ? `id:${id}` : null
}

/** El correo que lleva la clave, o `null` si es la clave de un ID (o de una fila vieja). */
export function correoDeClave(identityKey: string): string | null {
  return identityKey.startsWith('id:') || identityKey.startsWith('legacy:') ? null : identityKey
}
