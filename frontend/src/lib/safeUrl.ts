// Protocolos que una URL escrita por un usuario puede llevar: nada de `javascript:` ni `data:`.
const PROTOCOLOS_SEGUROS = ['https:', 'http:', 'mailto:', 'blob:']

/** La URL tal cual si su protocolo es seguro (o `soloHttps` y es https), o `undefined`. */
export function safeUrl(url: string | undefined | null, soloHttps = false): string | undefined {
  if (!url) return undefined
  try {
    const { protocol } = new URL(url.trim(), window.location.origin)
    const permitidos = soloHttps ? ['https:'] : PROTOCOLOS_SEGUROS
    return permitidos.includes(protocol) ? url.trim() : undefined
  } catch {
    return undefined
  }
}

/** Un `url("...")` de CSS que no se puede cerrar desde dentro: comillas y saltos van codificados. */
export function cssUrl(url: string): string {
  return `url("${url.replace(/["\\\n\r]/g, (c) => encodeURIComponent(c))}")`
}
