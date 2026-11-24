const TELEFONO = /^[0-9 +\-()]{7,20}$/

/**
 * La misma regla que `PhoneRules.OptionalPhone` de crm-service, para el teléfono de empresas y
 * contactos: dígitos, espacios y `+ - ( )`, de 7 a 20 caracteres. En blanco o ausente vale,
 * porque el teléfono es opcional.
 */
export function esTelefonoValido(valor: string | null | undefined): boolean {
  return !valor?.trim() || TELEFONO.test(valor)
}
