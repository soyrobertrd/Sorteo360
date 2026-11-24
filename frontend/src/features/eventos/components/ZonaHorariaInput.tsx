import { useId, useMemo } from 'react'
import { TextInput } from '@/components/ui/Field'
import { detalleDeZonaHoraria } from '../zonasHorarias'

/**
 * Selector de la zona horaria de una ciudad: cualquier id IANA que conozca el navegador, con su
 * diferencia con UTC de hoy. Lo escrito fuera de la lista lo rechaza el servicio. Solo el control:
 * la etiqueta, la pista y el error los pone cada formulario con su propio estilo.
 */
export function ZonaHorariaInput({
  value,
  onChange,
  className,
  invalid,
}: {
  value: string
  onChange: (zona: string) => void
  className?: string
  invalid?: boolean
}) {
  const listaId = useId()
  const opciones = useMemo(() => {
    const hoy = new Date()
    return Intl.supportedValuesOf('timeZone').map((zona) => ({
      zona,
      detalle: detalleDeZonaHoraria(zona, hoy),
    }))
  }, [])

  return (
    <>
      <TextInput
        value={value}
        onChange={(e) => onChange(e.target.value)}
        list={listaId}
        required
        aria-invalid={invalid}
        className={className}
      />
      <datalist id={listaId}>
        {opciones.map(({ zona, detalle }) => (
          <option key={zona} value={zona}>
            {detalle}
          </option>
        ))}
      </datalist>
    </>
  )
}
