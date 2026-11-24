import { useEffect, useRef } from 'react'

/**
 * Libera las `blob:` URLs que dejan de estar en `urls` y, al desmontar, las que quedan. Las demás
 * (http, data) no se tocan: no se crearon con `URL.createObjectURL`.
 */
export function useRevokeObjectUrls(urls: string[]) {
  const previas = useRef<string[]>([])
  useEffect(() => {
    const actuales = new Set(urls)
    previas.current
      .filter((u) => u.startsWith('blob:') && !actuales.has(u))
      .forEach((u) => URL.revokeObjectURL(u))
    previas.current = urls
  }, [urls])
  useEffect(
    () => () => previas.current.filter((u) => u.startsWith('blob:')).forEach(URL.revokeObjectURL),
    [],
  )
}
