import { createContext } from 'react'

/** "Mesas en U" -> "mesas en U": solo la primera letra, para no romper la sigla. */
export const minus = (t: string) => t.charAt(0).toLowerCase() + t.slice(1)

/** La persona que se buscó en el salón: su asiento se resalta en el esquema. */
export const AsientoResaltado = createContext<string | null>(null)
