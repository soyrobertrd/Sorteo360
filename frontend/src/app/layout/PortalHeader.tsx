import { useState } from 'react'
import { Moon, Sun, LogOut, UserCheck } from 'lucide-react'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { Badge } from '@/components/ui/Badge'
import { useAuth } from '@/auth/AuthProvider'
import { MOCK_IDENTITIES, setMockIdentity } from '@/auth/mockIdentity'
import { compositeRoleLabel } from '@/auth/roles'
import { useBranding } from '@/lib/branding'
import { config } from '@/lib/config'
import { useTheme } from '@/lib/theme'

export function PortalHeader() {
  const { user, logout } = useAuth()
  const [theme, toggleTheme] = useTheme()
  const branding = useBranding()
  const isDark = theme === 'dark'
  const [showSwitch, setShowSwitch] = useState(false)

  const userName = user?.nombre || 'Usuario'
  const userDept = user?.department || compositeRoleLabel(user?.roles || [])
  const initials = userName
    .split(' ')
    .map((n) => n[0])
    .join('')
    .substring(0, 2)
    .toUpperCase()

  return (
    <header className="w-full bg-transparent transition-colors">
      <div className="mx-auto flex h-16 max-w-7xl items-center justify-between px-4 sm:px-6 lg:px-8">
        {/* Marca y Versión (Izquierda) */}
        <div className="flex items-center gap-3">
          <img
            src={branding.logoUrl}
            alt={branding.organizationName}
            className="h-8 w-auto object-contain dark:hidden"
          />
          <img
            src={branding.logoDarkUrl}
            alt={branding.organizationName}
            className="hidden h-8 w-auto object-contain dark:block"
          />
          <div className="h-6 w-px bg-slate-200 dark:bg-slate-700" />
          <div className="flex items-center gap-2">
            <span className="font-sans text-xl font-bold tracking-tight text-slate-900 dark:text-white">
              {branding.appName}
            </span>
            <Badge
              variant="default"
              className="bg-slate-100 px-2 py-0.5 text-[11px] font-medium text-slate-600 dark:bg-slate-800 dark:text-slate-300"
            >
              v1.0
            </Badge>
          </div>
        </div>

        {/* Perfil de Usuario, Selector de Permisos y Acciones (Derecha) */}
        <div className="flex items-center gap-3 sm:gap-4">
          {/* Selector de Perfil para Demos / Prueba de accesos */}
          {(config.apiMode !== 'http' || config.authMode !== 'keycloak') && (
            <div className="relative">
              <button
                type="button"
                onClick={() => setShowSwitch((open) => !open)}
                className="flex items-center gap-1.5 rounded-lg border border-slate-200 bg-slate-50 px-2.5 py-1.5 text-xs font-semibold text-slate-700 transition-colors hover:bg-slate-100 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
                title="Cambiar usuario para probar permisos de módulos"
              >
                <UserCheck size={14} className="text-blue-600 dark:text-blue-400" />
                <span className="hidden md:inline">Simular usuario</span>
              </button>

              {showSwitch && (
                <div className="absolute right-0 top-10 z-50 w-64 rounded-xl border border-slate-200 bg-white p-2 shadow-xl dark:border-slate-700 dark:bg-slate-800">
                  <p className="mb-2 px-2 text-[11px] font-bold uppercase tracking-wider text-slate-400">
                    Seleccionar perfil para probar accesos:
                  </p>
                  <div className="space-y-1">
                    {MOCK_IDENTITIES.map((identity) => {
                      const active = user?.sub === identity.sub
                      return (
                        <button
                          key={identity.sub}
                          type="button"
                          onClick={() => {
                            setMockIdentity(identity.sub)
                            setShowSwitch(false)
                          }}
                          className={`w-full rounded-lg px-3 py-2 text-left text-xs transition-colors ${
                            active
                              ? 'bg-brand-deep text-white font-bold'
                              : 'text-slate-700 hover:bg-slate-100 dark:text-slate-200 dark:hover:bg-slate-700'
                          }`}
                        >
                          <p className="font-semibold">{identity.nombre}</p>
                          <p
                            className={`text-[10px] ${active ? 'text-blue-200' : 'text-slate-500'}`}
                          >
                            {identity.department}
                          </p>
                        </button>
                      )
                    })}
                  </div>
                </div>
              )}
            </div>
          )}

          <div className="hidden text-right sm:block">
            <p className="font-sans text-xs font-semibold text-slate-900 dark:text-white">
              {userName}
            </p>
            <p className="text-[11px] font-normal text-slate-500 dark:text-slate-400">{userDept}</p>
          </div>

          <Avatar className="h-9 w-9 border border-slate-200 dark:border-slate-700">
            <AvatarFallback className="bg-brand-deep text-xs font-bold text-white">
              {initials || 'U'}
            </AvatarFallback>
          </Avatar>

          <div className="flex items-center gap-1 border-l border-slate-200 pl-3 dark:border-slate-800">
            <button
              type="button"
              onClick={toggleTheme}
              className="rounded-lg p-2 text-slate-600 transition-colors hover:bg-slate-100 hover:text-slate-900 dark:text-slate-400 dark:hover:bg-slate-800 dark:hover:text-white"
              title={isDark ? 'Modo claro' : 'Modo oscuro'}
              aria-label="Cambiar tema"
            >
              {isDark ? <Sun size={18} /> : <Moon size={18} />}
            </button>

            {config.apiMode === 'http' && config.authMode === 'keycloak' && (
              <button
                type="button"
                onClick={logout}
                className="rounded-lg p-2 text-slate-600 transition-colors hover:bg-slate-100 hover:text-slate-900 dark:text-slate-400 dark:hover:bg-slate-800 dark:hover:text-white"
                title="Cerrar sesión"
                aria-label="Cerrar sesión"
              >
                <LogOut size={18} />
              </button>
            )}
          </div>
        </div>
      </div>
    </header>
  )
}
