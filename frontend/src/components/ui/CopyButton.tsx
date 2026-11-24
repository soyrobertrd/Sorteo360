import { useEffect, useRef, useState } from 'react'
import { Check, Copy } from 'lucide-react'
import { Button, type ButtonProps } from './Button'

/**
 * Botón que copia un texto al portapapeles y confirma con "¡Copiado!" un par de
 * segundos. Reemplaza el estado `copied` + `setTimeout` copiado a mano en el detalle
 * de evento y en convocatorias.
 */
export function CopyButton({
  value,
  label = 'Copiar enlace',
  copiedLabel = '¡Copiado!',
  variant = 'outline',
  size = 'sm',
  className,
  ...rest
}: {
  value: string
  label?: string
  copiedLabel?: string
} & Omit<ButtonProps, 'onClick' | 'children'>) {
  const [copied, setCopied] = useState(false)
  const timer = useRef<ReturnType<typeof setTimeout>>(undefined)

  useEffect(() => () => clearTimeout(timer.current), [])

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(value)
    } catch {
      /* el portapapeles puede estar bloqueado; no rompemos la UI */
    }
    setCopied(true)
    clearTimeout(timer.current)
    timer.current = setTimeout(() => setCopied(false), 2000)
  }

  return (
    <Button
      type="button"
      variant={variant}
      size={size}
      onClick={copy}
      className={className}
      {...rest}
    >
      {copied ? (
        <Check size={14} className="mr-1.5 text-emerald-600" />
      ) : (
        <Copy size={14} className="mr-1.5" />
      )}
      {copied ? copiedLabel : label}
    </Button>
  )
}
