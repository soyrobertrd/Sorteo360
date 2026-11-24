import { useRef, useState, type ReactNode } from 'react'
import { MoreVertical } from 'lucide-react'
import { ActionMenu } from './ActionMenu'
import { IconButton } from './IconButton'

interface RowActionMenuProps {
  /** Nombre accesible del botón, y su tooltip: "Opciones del evento". */
  label: string
  /** Las opciones: `MenuItem` y `MenuSeparator`. */
  children: ReactNode
  /** Ancho del desplegable (`w-48`, `w-56`…). */
  menuClassName?: string
  disabled?: boolean
  /** Reemplaza el tooltip, para explicar por qué está deshabilitado. */
  title?: string
}

/**
 * El menú de tres puntos, el mismo en toda la aplicación: el botón, su estado abierto y el
 * `ActionMenu` que despliega. Elegir cualquier opción lo cierra, y el clic no llega a la fila
 * o tarjeta que lo contiene.
 */
export function RowActionMenu({
  label,
  children,
  menuClassName = 'w-52',
  disabled,
  title,
}: RowActionMenuProps) {
  const [open, setOpen] = useState(false)
  const buttonRef = useRef<HTMLButtonElement>(null)

  return (
    <div className="relative inline-block" onClick={(e) => e.stopPropagation()}>
      <IconButton
        ref={buttonRef}
        aria-label={label}
        aria-haspopup="menu"
        aria-expanded={open}
        title={title ?? label}
        disabled={disabled}
        onClick={() => setOpen((prev) => !prev)}
      >
        <MoreVertical size={16} />
      </IconButton>
      <ActionMenu
        open={open}
        onClose={() => setOpen(false)}
        anchorRef={buttonRef}
        className={menuClassName}
      >
        <div onClick={() => setOpen(false)}>{children}</div>
      </ActionMenu>
    </div>
  )
}
