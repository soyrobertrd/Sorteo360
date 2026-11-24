// Descarga de archivos desde el navegador.

/** Descarga lo que hay en `href` con el nombre dado, simulando un clic en un enlace. */
export function descargarUrl(href: string, fileName: string): void {
  const link = document.createElement('a')
  link.href = href
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
}

/**
 * Descarga bytes locales. La URL se libera en el turno siguiente: revocarla en el mismo tick del
 * clic puede cancelar la descarga en algunos navegadores.
 */
export function descargarBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob)
  descargarUrl(url, fileName)
  setTimeout(() => URL.revokeObjectURL(url), 0)
}
