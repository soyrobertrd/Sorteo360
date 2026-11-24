/**
 * El número de boleto de un participante: seis dígitos al azar, `482-917`, fácil de leer en voz
 * alta y de copiar en un gafete. Al azar y no correlativo, para que el número no diga quién se
 * registró antes; único dentro del sorteo.
 */
export function nuevoBoleto(usados: ReadonlySet<string>, azar: () => number = aleatorio): string {
  // Con un millón de combinaciones, cien intentos fallidos significan un padrón casi lleno.
  for (let intento = 0; intento < 100; intento++) {
    const n = Math.floor(azar() * 1_000_000)
    const boleto = formatoBoleto(n)
    if (!usados.has(boleto)) return boleto
  }
  throw new Error('No quedan números de boleto libres para este sorteo.')
}

export function formatoBoleto(n: number): string {
  const s = String(n).padStart(6, '0')
  return `${s.slice(0, 3)}-${s.slice(3)}`
}

/** Azar del navegador (`crypto`), no `Math.random`: el boleto no debe poder adivinarse. */
function aleatorio(): number {
  const buf = new Uint32Array(1)
  crypto.getRandomValues(buf)
  return buf[0] / 2 ** 32
}
