/** Si el archivo cumple `accept`. El navegador no filtra lo que se suelta, solo lo que se elige. */
export function matchesAccept(file: File, accept: string): boolean {
  const rules = accept
    .split(',')
    .map((r) => r.trim().toLowerCase())
    .filter(Boolean)
  if (rules.length === 0) return true
  const name = file.name.toLowerCase()
  const type = file.type.toLowerCase()
  return rules.some((r) =>
    r.startsWith('.')
      ? name.endsWith(r)
      : r.endsWith('/*')
        ? type.startsWith(r.slice(0, -1))
        : type === r,
  )
}
