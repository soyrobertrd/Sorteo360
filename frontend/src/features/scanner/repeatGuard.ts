/** Cuánto dura una repetición: lo que tarda alguien en apartar el teléfono. */
export const REPEAT_WINDOW_MS = 2_000

/**
 * Los códigos de un fotograma que no se vieron en la ventana anterior. La cámara lee el mismo QR
 * muchas veces por segundo; se descarta solo para que la pantalla no parpadee, y el servidor es
 * idempotente igual (spec convocatorias/10 §2.3).
 *
 * Cada código lleva su propia última vez visto, y se renueva en cada fotograma que lo ve: uno
 * sostenido frente a la cámara no se vuelve a enviar, y dos en el mismo cuadro no se alternan.
 * `lastSeen` es del llamador y esta función lo actualiza.
 */
export function freshCodes(
  lastSeen: Map<string, number>,
  codes: readonly string[],
  now: number,
): string[] {
  const fresh: string[] = []
  for (const code of new Set(codes)) {
    const seenAt = lastSeen.get(code)
    if (seenAt === undefined || now - seenAt >= REPEAT_WINDOW_MS) fresh.push(code)
    lastSeen.set(code, now)
  }
  for (const [code, seenAt] of lastSeen) {
    if (now - seenAt >= REPEAT_WINDOW_MS) lastSeen.delete(code)
  }
  return fresh
}
