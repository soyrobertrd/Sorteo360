import { Moon, Sun } from 'lucide-react'
import { matchPath, Outlet, useLocation } from 'react-router-dom'
import { useBranding } from '@/lib/branding'
import { useTheme } from '@/lib/theme'

// Páginas que ocupan la pantalla entera: no llevan el pie del portal ni el botón de tema.
const RUTAS_PANTALLA_COMPLETA = ['/publico/coexpositores/acceso']

export function PublicLayout() {
  const [theme, toggleTheme] = useTheme()
  const branding = useBranding()
  const { pathname } = useLocation()
  const pantallaCompleta = RUTAS_PANTALLA_COMPLETA.some((ruta) => matchPath(ruta, pathname))
  const isDark = theme === 'dark'

  return (
    <div className="min-h-screen flex flex-col justify-between overflow-x-hidden bg-background text-foreground transition-colors dark:bg-card dark:text-foreground">
      {/* Encabezado Marca Blanca */}
      <header className="border-b border-slate-200/80 bg-white/90 px-4 py-3 backdrop-blur-xs dark:border-slate-800 dark:bg-slate-900/90 sm:px-6">
        <div className="mx-auto flex max-w-7xl items-center justify-between gap-3">
          <div className="flex items-center gap-3">
            <img
              src={branding.logoUrl}
              alt={branding.organizationName}
              className="h-7 w-auto object-contain dark:hidden"
            />
            <img
              src={branding.logoDarkUrl}
              alt={branding.organizationName}
              className="hidden h-7 w-auto object-contain dark:block"
            />
            <span className="hidden text-xs font-medium text-slate-500 sm:inline dark:text-slate-400">
              {branding.organizationName}
            </span>
          </div>
          <button
            type="button"
            onClick={toggleTheme}
            title={isDark ? 'Modo claro' : 'Modo oscuro'}
            aria-label="Cambiar tema"
            className="flex h-9 w-9 items-center justify-center rounded-lg border border-slate-200 bg-white text-slate-600 transition-colors hover:bg-slate-100 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-300 dark:hover:bg-slate-700 cursor-pointer"
          >
            {isDark ? <Sun size={16} /> : <Moon size={16} />}
          </button>
        </div>
      </header>

      {/* Contenido Principal sin vacíos ni desbordamiento */}
      <main className="flex-1 w-full overflow-x-hidden">
        <Outlet />
      </main>

      {/* Pie de página Marca Blanca */}
      {!pantallaCompleta && (
        <footer className="border-t border-slate-200/80 bg-white/80 py-4 text-center text-xs text-slate-500 dark:border-slate-800 dark:bg-slate-900/80 dark:text-slate-400">
          {branding.appName} · {branding.organizationName}
        </footer>
      )}
    </div>
  )
}
