import { twMerge } from 'tailwind-merge'

type ClassValue =
  | string
  | number
  | boolean
  | undefined
  | null
  | { [key: string]: boolean | undefined | null }
  | ClassValue[]

function collect(inputs: ClassValue[], out: string[]): void {
  for (const input of inputs) {
    if (!input) continue
    if (typeof input === 'string' || typeof input === 'number') {
      out.push(String(input))
    } else if (Array.isArray(input)) {
      collect(input, out)
    } else if (typeof input === 'object') {
      for (const key in input) {
        if (input[key]) out.push(key)
      }
    }
  }
}

/**
 * Une listas de clases y resuelve conflictos de Tailwind: la última gana. Así el
 * `className` que pasa quien usa un componente siempre vence a los estilos por
 * defecto de la variante (`<Button variant="default" className="bg-emerald-600">`
 * sale verde, no azul marino).
 */
export function cn(...inputs: ClassValue[]): string {
  const classes: string[] = []
  collect(inputs, classes)
  return twMerge(classes.join(' '))
}

// Simula latencia de red para operaciones asíncronas de demostración y mocks.
export const esperar = (ms: number): Promise<void> =>
  new Promise((resolve) => setTimeout(resolve, ms))
