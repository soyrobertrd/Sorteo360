import { useBranding } from '@/lib/branding'

export function PortalFooter() {
  const branding = useBranding()
  return (
    <footer className="mt-auto border-t border-slate-200/80 bg-white/80 py-4 text-center text-xs text-slate-500 transition-colors dark:border-slate-800 dark:bg-slate-900/80 dark:text-slate-400">
      <span>
        {branding.appName} · {branding.organizationName} · Plataforma de Rifas y Eventos
        Corporativos
      </span>
    </footer>
  )
}
