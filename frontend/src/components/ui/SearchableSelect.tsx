import {
  Children,
  isValidElement,
  useEffect,
  useId,
  useRef,
  useState,
  type ChangeEvent,
  type ReactElement,
  type ReactNode,
} from 'react'
import { ChevronDown, Search } from 'lucide-react'
import { cn } from '@/lib/utils'

type Opcion = { value: string; label: string; disabled?: boolean }

/** Las `<option>` de los hijos, también dentro de `<optgroup>`, fragmentos y arreglos. */
function opcionesDe(children: ReactNode): Opcion[] {
  const out: Opcion[] = []
  Children.forEach(children, (child) => {
    if (!isValidElement(child)) return
    const el = child as ReactElement<{
      value?: string | number
      children?: ReactNode
      disabled?: boolean
    }>
    if (el.type === 'option') {
      const label = Children.toArray(el.props.children).join('')
      out.push({
        value: String(el.props.value ?? label),
        label,
        disabled: el.props.disabled,
      })
    } else {
      out.push(...opcionesDe(el.props.children))
    }
  })
  return out
}

const sinAcentos = (s: string) => s.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase()

/**
 * Un `<select>` con campo de búsqueda. Recibe las mismas props que el nativo (`value`,
 * `onChange` con `e.target.value`, `className`, `<option>` como hijos), así que cambiar uno
 * por otro no toca el formato del filtro: el botón lleva la misma clase que llevaba el select.
 */
export function SearchableSelect({
  value,
  onChange,
  className,
  children,
  id,
  disabled,
  placeholder = 'Buscar...',
  'aria-label': ariaLabel,
  title,
}: {
  value: string | number
  onChange: (e: ChangeEvent<HTMLSelectElement>) => void
  className?: string
  children: ReactNode
  id?: string
  disabled?: boolean
  placeholder?: string
  'aria-label'?: string
  title?: string
}) {
  const opciones = opcionesDe(children)
  const [abierto, setAbierto] = useState(false)
  const [busqueda, setBusqueda] = useState('')
  const [activo, setActivo] = useState(0)
  const raiz = useRef<HTMLDivElement>(null)
  const listaId = useId()
  const actual = opciones.find((o) => o.value === String(value))
  const filtradas = opciones.filter((o) => sinAcentos(o.label).includes(sinAcentos(busqueda)))

  useEffect(() => {
    if (!abierto) return
    const fuera = (e: MouseEvent) => {
      if (!raiz.current?.contains(e.target as Node)) setAbierto(false)
    }
    document.addEventListener('mousedown', fuera)
    return () => document.removeEventListener('mousedown', fuera)
  }, [abierto])

  const abrir = () => {
    setBusqueda('')
    setActivo(Math.max(0, opciones.indexOf(actual!)))
    setAbierto(true)
  }
  const elegir = (o: Opcion | undefined) => {
    if (!o || o.disabled) return
    onChange({ target: { value: o.value } } as ChangeEvent<HTMLSelectElement>)
    setAbierto(false)
  }

  return (
    <div ref={raiz} className="relative">
      <button
        type="button"
        id={id}
        title={title}
        disabled={disabled}
        aria-label={ariaLabel}
        aria-haspopup="listbox"
        aria-expanded={abierto}
        onClick={() => (abierto ? setAbierto(false) : abrir())}
        className={cn('flex items-center justify-between gap-2 text-left', className)}
      >
        <span className="truncate">{actual?.label ?? ''}</span>
        <ChevronDown size={14} className="shrink-0 text-slate-400" aria-hidden />
      </button>
      {abierto && (
        <div className="absolute left-0 z-50 mt-1 w-full min-w-48 overflow-hidden rounded-lg border border-slate-200 bg-white shadow-lg dark:border-slate-700 dark:bg-slate-900">
          <div className="flex items-center gap-2 border-b border-slate-100 px-2.5 py-2 dark:border-slate-800">
            <Search size={13} className="shrink-0 text-slate-400" aria-hidden />
            <input
              autoFocus
              value={busqueda}
              placeholder={placeholder}
              aria-label={placeholder}
              aria-controls={listaId}
              onChange={(e) => {
                setBusqueda(e.target.value)
                setActivo(0)
              }}
              onKeyDown={(e) => {
                if (e.key === 'ArrowDown') {
                  e.preventDefault()
                  setActivo((i) => Math.min(filtradas.length - 1, i + 1))
                } else if (e.key === 'ArrowUp') {
                  e.preventDefault()
                  setActivo((i) => Math.max(0, i - 1))
                } else if (e.key === 'Enter') {
                  e.preventDefault()
                  elegir(filtradas[activo])
                } else if (e.key === 'Escape') {
                  setAbierto(false)
                }
              }}
              className="w-full bg-transparent text-xs text-slate-900 placeholder:text-slate-400 focus:outline-none dark:text-white"
            />
          </div>
          <ul id={listaId} role="listbox" className="max-h-60 overflow-y-auto py-1 text-xs">
            {filtradas.length === 0 && <li className="px-3 py-2 text-slate-400">Sin resultados</li>}
            {filtradas.map((o, i) => (
              <li
                key={o.value}
                role="option"
                aria-selected={o.value === String(value)}
                aria-disabled={o.disabled}
                onMouseEnter={() => setActivo(i)}
                onMouseDown={(e) => e.preventDefault()}
                onClick={() => elegir(o)}
                className={cn(
                  'cursor-pointer px-3 py-2 text-slate-700 dark:text-slate-200',
                  i === activo && 'bg-slate-100 dark:bg-slate-800',
                  o.value === String(value) && 'font-bold text-brand dark:text-blue-400',
                  o.disabled && 'cursor-not-allowed opacity-50',
                )}
              >
                {o.label}
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  )
}
