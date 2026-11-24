import { AlertTriangle, CheckCircle2, Info, XCircle } from 'lucide-react'
import { Toaster } from 'sonner'
import { useTheme } from '@/lib/theme'

/**
 * Contenedor único de los avisos (toasts) de Sonner. Se monta una sola vez en `App`;
 * cualquier pantalla dispara un aviso con `import { toast } from 'sonner'`
 * (`toast.success('Guardado')`, `toast.error('...')`, etc.).
 * Sigue el tema de la app (`useTheme`), que es la clase `.dark` en el documento.
 * Los íconos son los de lucide (contorno), no los rellenos que trae Sonner.
 */
export function AppToaster() {
  const [theme] = useTheme()
  return (
    <Toaster
      theme={theme}
      position="bottom-right"
      richColors
      icons={{
        success: <CheckCircle2 size={18} />,
        error: <XCircle size={18} />,
        warning: <AlertTriangle size={18} />,
        info: <Info size={18} />,
      }}
    />
  )
}
