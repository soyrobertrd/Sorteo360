export const HEIC_NOT_SUPPORTED_MESSAGE =
  'HEIC no está soportado. Convierte tu foto a JPEG, PNG o WebP antes de subir.'

/**
 * Chequeo previo al envío, no un reemplazo del que hace el servicio: HEIC es el único
 * formato que el iPhone produce por defecto y que ningún navegador puede previsualizar,
 * así que vale la pena avisar antes de subir en vez de esperar el rechazo del backend.
 * La detección es por extensión de archivo — no hay sniffing de contenido ni de tamaño.
 */
export function validateHeicFile(
  file: File,
): { code: 'FILE_TYPE_HEIC_NOT_SUPPORTED'; message: string } | null {
  const name = file.name.toLowerCase()
  if (!name.endsWith('.heic') && !name.endsWith('.heif')) return null
  return { code: 'FILE_TYPE_HEIC_NOT_SUPPORTED', message: HEIC_NOT_SUPPORTED_MESSAGE }
}
