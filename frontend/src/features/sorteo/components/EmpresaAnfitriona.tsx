import { useId, useState, type DragEvent } from 'react'
import { Building2, ImagePlus, Link2, X } from 'lucide-react'
import { cn } from '@/lib/utils'

const TIPOS = ['image/png', 'image/jpeg', 'image/webp', 'image/svg+xml']
// El logo vive en el navegador junto a la marca del sorteo: uno pesado llenaría el almacenamiento.
const MAX_BYTES = 1024 * 1024

/**
 * La empresa que organiza el sorteo: su nombre y su logo, con la franja de la pantalla del sorteo
 * como vista previa. El logo llega como archivo (se arrastra o se elige) o como enlace.
 */
export function EmpresaAnfitriona({
  nombre,
  logo,
  onNombre,
  onLogo,
}: {
  nombre: string
  logo: string
  onNombre: (nombre: string) => void
  onLogo: (logo: string) => void
}) {
  const id = useId()
  const [error, setError] = useState<string | null>(null)
  const [arrastrando, setArrastrando] = useState(false)
  const [conEnlace, setConEnlace] = useState(() => /^https?:\/\//i.test(logo))

  const leerArchivo = (file: File | undefined) => {
    setError(null)
    if (!file) return
    if (!TIPOS.includes(file.type)) {
      setError('El logo tiene que ser PNG, JPG, WebP o SVG.')
      return
    }
    if (file.size > MAX_BYTES) {
      setError('El logo pasa de 1 MB. Usa una versión más liviana o pega un enlace.')
      return
    }
    const reader = new FileReader()
    reader.onload = () => typeof reader.result === 'string' && onLogo(reader.result)
    reader.readAsDataURL(file)
  }

  const soltar = (e: DragEvent) => {
    e.preventDefault()
    setArrastrando(false)
    leerArchivo(e.dataTransfer.files[0])
  }

  const enlaceInvalido = conEnlace && logo.trim() !== '' && !/^https?:\/\/\S+$/i.test(logo.trim())

  return (
    <section
      aria-labelledby={`${id}-t`}
      className="space-y-4 rounded-2xl border border-slate-200 p-4 dark:border-slate-800"
    >
      <header className="flex items-start gap-3">
        <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-brand/10 text-brand dark:text-brand-accent">
          <Building2 className="h-[18px] w-[18px]" />
        </span>
        <div>
          <h3 id={`${id}-t`} className="text-sm font-semibold text-slate-900 dark:text-white">
            Empresa anfitriona{' '}
            <span className="font-normal text-slate-400 dark:text-slate-500">· opcional</span>
          </h3>
          <p className="text-xs leading-relaxed text-slate-500 dark:text-slate-400">
            Su nombre y su logo salen en la pantalla del sorteo, el tablero y los certificados.
          </p>
        </div>
      </header>

      <div className="grid gap-4 sm:grid-cols-[7.5rem_1fr]">
        {/* El logo: arrastrar, elegir o, con el logo puesto, cambiarlo o quitarlo. */}
        <div className="relative">
          <label
            htmlFor={`${id}-f`}
            onDragOver={(e) => {
              e.preventDefault()
              setArrastrando(true)
            }}
            onDragLeave={() => setArrastrando(false)}
            onDrop={soltar}
            className={cn(
              'group relative flex aspect-square w-full cursor-pointer flex-col items-center justify-center gap-1.5 overflow-hidden rounded-2xl border-2 border-dashed text-center transition-colors has-focus-visible:outline-2 has-focus-visible:outline-offset-2 has-focus-visible:outline-brand',
              arrastrando
                ? 'border-brand bg-brand/5'
                : logo
                  ? 'border-transparent bg-slate-50 dark:bg-slate-800/60'
                  : 'border-slate-300 hover:border-brand hover:bg-brand/5 dark:border-slate-700',
            )}
          >
            {logo && !enlaceInvalido ? (
              <>
                <img
                  src={logo}
                  alt={`Logo de ${nombre || 'la empresa'}`}
                  className="max-h-[70%] max-w-[80%] object-contain"
                />
                <span className="absolute inset-x-0 bottom-0 bg-slate-900/70 py-1 text-[11px] font-medium text-white opacity-0 transition-opacity group-hover:opacity-100 group-has-focus-visible:opacity-100">
                  Cambiar
                </span>
              </>
            ) : (
              <>
                <ImagePlus className="h-6 w-6 text-slate-400 group-hover:text-brand" />
                <span className="px-2 text-xs font-medium text-slate-600 dark:text-slate-300">
                  {arrastrando ? 'Suelta aquí' : 'Subir logo'}
                </span>
                <span className="text-[11px] text-slate-400">PNG o SVG · 1 MB</span>
              </>
            )}
            <input
              id={`${id}-f`}
              type="file"
              accept={TIPOS.join(',')}
              className="sr-only"
              onChange={(e) => {
                leerArchivo(e.target.files?.[0])
                e.target.value = ''
              }}
            />
          </label>
          {logo && (
            <button
              type="button"
              onClick={() => {
                onLogo('')
                setError(null)
              }}
              aria-label="Quitar el logo"
              className="absolute -right-2 -top-2 flex h-7 w-7 items-center justify-center rounded-full border border-slate-200 bg-white text-slate-500 shadow-sm hover:text-rose-600 dark:border-slate-700 dark:bg-slate-900"
            >
              <X className="h-3.5 w-3.5" />
            </button>
          )}
        </div>

        <div className="space-y-3">
          <div className="space-y-1.5">
            <label
              htmlFor={`${id}-n`}
              className="block text-sm font-medium text-slate-800 dark:text-slate-200"
            >
              Nombre de la empresa
            </label>
            <input
              id={`${id}-n`}
              value={nombre}
              onChange={(e) => onNombre(e.target.value)}
              maxLength={120}
              placeholder="Ej. Banreservas"
              className="h-11 w-full rounded-xl border border-slate-300 bg-white px-3.5 text-sm text-slate-900 outline-none transition-colors focus:border-brand focus:ring-2 focus:ring-brand/20 dark:border-slate-700 dark:bg-slate-900 dark:text-white"
            />
          </div>

          {conEnlace ? (
            <div className="space-y-1.5">
              <label
                htmlFor={`${id}-u`}
                className="block text-sm font-medium text-slate-800 dark:text-slate-200"
              >
                Enlace del logo
              </label>
              <input
                id={`${id}-u`}
                type="url"
                inputMode="url"
                value={logo.startsWith('data:') ? '' : logo}
                onChange={(e) => {
                  setError(null)
                  onLogo(e.target.value)
                }}
                placeholder="https://…/logo.png"
                aria-invalid={enlaceInvalido}
                aria-describedby={enlaceInvalido ? `${id}-e` : undefined}
                className="h-11 w-full rounded-xl border border-slate-300 bg-white px-3.5 text-sm text-slate-900 outline-none transition-colors focus:border-brand focus:ring-2 focus:ring-brand/20 aria-invalid:border-rose-400 dark:border-slate-700 dark:bg-slate-900 dark:text-white"
              />
            </div>
          ) : (
            <button
              type="button"
              onClick={() => setConEnlace(true)}
              className="inline-flex min-h-9 items-center gap-1.5 rounded-lg text-xs font-medium text-brand hover:underline dark:text-brand-accent"
            >
              <Link2 className="h-3.5 w-3.5" /> Usar un enlace en vez de un archivo
            </button>
          )}

          {(error || enlaceInvalido) && (
            <p id={`${id}-e`} role="alert" className="text-xs text-rose-600">
              {error ?? 'Escribe un enlace completo, que empiece con https://.'}
            </p>
          )}
        </div>
      </div>

      {/* Así se ve: la franja de la pantalla del sorteo, con el logo en blanco como allá. */}
      {(nombre.trim() || (logo && !enlaceInvalido)) && (
        <div aria-hidden="true">
          <p className="mb-1.5 text-[11px] font-medium text-slate-400">Así se ve en pantalla</p>
          <div className="flex h-12 items-center justify-center gap-4 rounded-xl bg-brand-deep px-4 text-white">
            {logo && !enlaceInvalido && (
              <img
                src={logo}
                alt=""
                className="h-7 w-auto max-w-32 object-contain brightness-0 invert"
              />
            )}
            {nombre.trim() && (
              <span className="truncate text-sm font-semibold tracking-tight">{nombre.trim()}</span>
            )}
          </div>
        </div>
      )}
    </section>
  )
}
