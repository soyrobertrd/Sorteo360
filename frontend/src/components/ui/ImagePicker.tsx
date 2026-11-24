import { useState, type ReactNode } from 'react'
import { ImagePlus, Upload, X } from 'lucide-react'
import { cn } from '@/lib/utils'

const IMAGENES = 'image/jpeg,image/png,image/webp'

/** Abre el selector de archivos del sistema, solo con imágenes. */
function elegirImagenes(multiple: boolean, onFiles: (files: File[]) => void) {
  const input = document.createElement('input')
  input.type = 'file'
  input.accept = IMAGENES
  input.multiple = multiple
  input.onchange = () => onFiles(Array.from(input.files ?? []))
  input.click()
}

/**
 * Una imagen principal (portada, banner): la vista previa es también la zona para hacer clic o
 * soltar la imagen.
 */
export function CoverPicker({
  src,
  onFile,
  badge,
  aspectClassName = 'aspect-[16/6]',
  label = 'Cambiar portada',
}: {
  src?: string | null
  onFile: (file: File) => void
  /** Una marca sobre la imagen, como "Imagen por defecto". */
  badge?: ReactNode
  aspectClassName?: string
  label?: string
}) {
  const [arrastrando, setArrastrando] = useState(false)
  return (
    <button
      type="button"
      aria-label={label}
      onDragOver={(e) => {
        e.preventDefault()
        setArrastrando(true)
      }}
      onDragLeave={() => setArrastrando(false)}
      onDrop={(e) => {
        e.preventDefault()
        setArrastrando(false)
        const file = e.dataTransfer.files?.[0]
        if (file) onFile(file)
      }}
      onClick={() => elegirImagenes(false, (files) => files[0] && onFile(files[0]))}
      className={cn(
        'group relative block w-full overflow-hidden rounded-xl border-2 bg-slate-100 transition-all dark:bg-slate-800',
        aspectClassName,
        arrastrando
          ? 'border-dashed border-blue-500 ring-4 ring-blue-500/20'
          : 'border-transparent hover:border-blue-500',
      )}
    >
      {src && <img src={src} alt="" className="h-full w-full object-cover" />}
      <span
        className={cn(
          'absolute inset-0 flex flex-col items-center justify-center gap-1 bg-slate-900/50 text-white transition-opacity',
          arrastrando || !src
            ? 'opacity-100'
            : 'opacity-0 group-hover:opacity-100 group-focus-visible:opacity-100',
        )}
      >
        <Upload size={20} />
        <span className="text-xs font-bold">{arrastrando ? 'Suelta la imagen aquí' : label}</span>
        <span className="text-[10px]">Haz clic o arrastra una imagen</span>
      </span>
      {badge && (
        <span className="absolute left-2 top-2 rounded-md bg-black/60 px-2 py-0.5 text-[10px] font-bold text-white">
          {badge}
        </span>
      )}
    </button>
  )
}

export interface GalleryPhoto {
  key: string
  src: string
}

/**
 * Una galería de fotos con tope: cuadrícula con el botón para quitar cada una y un último
 * recuadro para añadir (clic o soltar varias). Deja de aceptar al llegar al máximo.
 */
export function GalleryPicker({
  photos,
  max,
  multiple = true,
  onAdd,
  onRemove,
}: {
  photos: GalleryPhoto[]
  max: number
  /** Si se pueden elegir varias de una vez. */
  multiple?: boolean
  onAdd: (files: File[]) => void
  onRemove: (index: number) => void
}) {
  const quedan = max - photos.length
  const agregar = (files: File[]) => {
    if (files.length) onAdd(files.slice(0, quedan))
  }
  return (
    <div>
      <div className="grid grid-cols-3 gap-2 sm:grid-cols-5">
        {photos.map((foto, idx) => (
          <div
            key={foto.key}
            className="relative aspect-square overflow-hidden rounded-lg border border-slate-200 dark:border-slate-800"
          >
            <img
              src={foto.src}
              alt={`Foto ${idx + 1} de la galería`}
              className="h-full w-full object-cover"
            />
            <button
              type="button"
              aria-label={`Quitar la foto ${idx + 1}`}
              title="Quitar foto"
              onClick={() => onRemove(idx)}
              className="absolute right-1 top-1 flex h-6 w-6 items-center justify-center rounded-full bg-black/60 text-white hover:bg-rose-600"
            >
              <X size={13} />
            </button>
          </div>
        ))}
        {quedan > 0 && (
          <button
            type="button"
            onClick={() => elegirImagenes(multiple, agregar)}
            onDragOver={(e) => e.preventDefault()}
            onDrop={(e) => {
              e.preventDefault()
              agregar(Array.from(e.dataTransfer.files ?? []))
            }}
            className="flex aspect-square flex-col items-center justify-center gap-1 rounded-lg border-2 border-dashed border-slate-300 bg-white text-slate-500 transition-colors hover:border-blue-500 hover:text-blue-600 dark:border-slate-700 dark:bg-slate-900"
          >
            <ImagePlus size={18} />
            <span className="text-[11px] font-bold">Añadir fotos</span>
          </button>
        )}
      </div>
      {photos.length === 0 && (
        <p className="mt-1.5 text-[11px] text-slate-500">
          Sin fotos todavía. Puedes arrastrarlas sobre el recuadro.
        </p>
      )}
    </div>
  )
}
