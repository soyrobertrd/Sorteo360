import type { ImgHTMLAttributes } from 'react'
import { useQuery } from '@tanstack/react-query'
import { api } from '@/api'

const ARCHIVO = /\/v1\/files\/([0-9a-f-]{36})\/content$/i

/**
 * La foto de un premio. Las que sube el equipo viven en `/v1/files/{id}/content`, que pide sesión:
 * un `<img src>` no manda el token y sale rota. Esas se bajan con la sesión y se muestran como
 * blob, una sola vez por archivo aunque 83 premios iguales compartan la misma.
 */
export function FotoPremio({
  src,
  ...props
}: ImgHTMLAttributes<HTMLImageElement> & { src: string }) {
  const id = ARCHIVO.exec(src)?.[1]
  const { data } = useQuery({
    queryKey: ['archivo-blob', id],
    queryFn: () => api.getFileContentUrl(id!),
    enabled: id != null,
    staleTime: Infinity,
    gcTime: Infinity,
  })
  const url = id ? data : src
  return url ? <img src={url} {...props} /> : null
}
