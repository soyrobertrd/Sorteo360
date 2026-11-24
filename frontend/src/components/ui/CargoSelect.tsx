import { useState } from 'react'
import { cargosDe } from '@/lib/cargos'
import { Select, TextInput } from './Field'

const OTRO = '__other__'

/**
 * El campo de cargo de todos los formularios: la misma lista en todas partes (`lib/cargos.ts`)
 * y "Otro" para escribir uno que no esté. El valor es siempre el texto del cargo, que es lo
 * que guardan los servicios.
 *
 * Un cargo que ya venía guardado y no está en la lista (un contacto anterior a la lista, uno
 * importado) se muestra como "Otro" con su texto, para que editar el formulario no lo borre.
 */
export function CargoSelect({
  value,
  onChange,
  lang,
  required,
  id,
  placeholder,
  className,
  'aria-label': ariaLabel,
}: {
  value: string
  onChange: (cargo: string) => void
  /** El idioma de la lista; sin él, español. */
  lang?: string | null
  required?: boolean
  id?: string
  /** El del campo de texto que aparece con "Otro". */
  placeholder?: string
  /** Reemplaza el estilo del desplegable y del campo de texto, para formularios con look propio. */
  className?: string
  'aria-label'?: string
}) {
  const cargos = cargosDe(lang)
  const [eligioOtro, setEligioOtro] = useState(false)
  const fueraDeLista = value !== '' && !cargos.options.includes(value)
  const otro = eligioOtro || fueraDeLista
  const estilo = className ? { className } : {}

  return (
    <>
      <Select
        id={id}
        value={otro ? OTRO : value}
        onChange={(e) => {
          const elegido = e.target.value
          setEligioOtro(elegido === OTRO)
          onChange(elegido === OTRO ? '' : elegido)
        }}
        required={required}
        aria-label={ariaLabel}
        {...estilo}
      >
        <option value="" disabled={required}>
          {cargos.select}
        </option>
        {cargos.options.map((opcion) => (
          <option key={opcion} value={opcion}>
            {opcion}
          </option>
        ))}
        <option value={OTRO}>{cargos.other}</option>
      </Select>
      {otro && (
        <div className="mt-2">
          <TextInput
            value={value}
            onChange={(e) => onChange(e.target.value)}
            placeholder={placeholder}
            aria-label={ariaLabel ?? cargos.other}
            required={required}
            {...estilo}
          />
        </div>
      )}
    </>
  )
}
