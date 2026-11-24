import type { ButtonHTMLAttributes, ReactNode } from 'react'
import { Plus } from 'lucide-react'
import { Button } from './Button'
import { cn } from '@/lib/utils'

/**
 * El botón principal para dar de alta algo desde un listado ("Nuevo contacto", "Nuevo
 * evento"): el mismo color, peso y signo + en todas las pantallas.
 */
export function NewButton({
  children,
  className,
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & { children: ReactNode }) {
  return (
    <Button
      {...props}
      className={cn(
        'bg-brand font-bold text-white shadow-sm hover:bg-brand/90 cursor-pointer disabled:cursor-not-allowed disabled:opacity-50',
        className,
      )}
    >
      <Plus size={16} className="mr-1 stroke-[2.5]" /> {children}
    </Button>
  )
}
