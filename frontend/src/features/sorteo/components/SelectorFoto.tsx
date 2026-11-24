import { useEffect, useId, useState } from 'react'
import { ImagePlus } from 'lucide-react'
import type { FotoElegida } from '../hooks'
import { FotoPremio } from './FotoPremio'

const TIPOS = 'image/png,image/jpeg,image/webp'
const MAX_BYTES = 5 * 1024 * 1024

/**
 * La foto de un premio: un archivo del equipo (PNG transparente, JPG o WebP, hasta 5 MB). Muestra cómo queda y avisa lo que no sirve antes de guardar. Devuelve la elección con
 * `onChange`; `undefined` mientras no haya una válida.
 */
export function SelectorFoto({
  actual,
  onChange,
}: {
  /** La foto que ya tiene el premio, para mostrarla mientras no se elija otra. */
  actual?: string | null
  onChange: (foto: Exclude<FotoElegida, null> | undefined) => void
}) {
  const id = useId()
  const [archivo, setArchivo] = useState<File | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [vista, setVista] = useState<string | null>(null)

  // La vista previa de un archivo es un object URL: se libera al cambiarlo o al cerrar.
  useEffect(() => {
    if (!archivo) return
    const u = URL.createObjectURL(archivo)
    setVista(u)
    return () => URL.revokeObjectURL(u)
  }, [archivo])

  const elegirArchivo = (f: File | undefined) => {
    setError(null)
    if (!f) return
    if (!TIPOS.split(',').includes(f.type)) {
      setError('La foto tiene que ser PNG, JPG o WebP.')
      return
    }
    if (f.size > MAX_BYTES) {
      setError('La foto pasa de 5 MB.')
      return
    }
    setArchivo(f)
    onChange({ file: f })
  }

  const muestra = vista ?? actual ?? null

  return (
    <div className="space-y-3">
      <div className="grid gap-3 sm:grid-cols-[1fr_8rem]">
        <label
          htmlFor={id}
          className="relative flex min-h-24 cursor-pointer flex-col items-center justify-center gap-1 rounded-xl border border-dashed border-slate-300 p-3 text-center text-xs text-muted-foreground hover:border-brand hover:text-foreground has-focus-visible:outline-2 has-focus-visible:outline-brand dark:border-slate-700"
        >
          <ImagePlus className="h-5 w-5" />
          <span className="font-semibold">{archivo ? archivo.name : 'Elegir foto del equipo'}</span>
          <span>PNG transparente, JPG o WebP · hasta 5 MB</span>
          <input
            id={id}
            type="file"
            accept={TIPOS}
            className="sr-only"
            onChange={(e) => {
              elegirArchivo(e.target.files?.[0])
              e.target.value = ''
            }}
          />
        </label>
        <div className="flex h-24 items-center justify-center rounded-xl bg-[repeating-conic-gradient(var(--color-muted)_0_25%,transparent_0_50%)] bg-size-[16px_16px] p-1">
          {muestra ? (
            <FotoPremio
              src={muestra}
              alt="Vista previa de la foto"
              className="max-h-full max-w-full object-contain"
            />
          ) : (
            <span className="text-[11px] text-muted-foreground">Sin foto</span>
          )}
        </div>
      </div>
      {error && <p className="text-xs font-semibold text-rose-600">{error}</p>}
    </div>
  )
}
