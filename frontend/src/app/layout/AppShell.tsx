import { useMemo, useState, useEffect, useRef } from 'react'
import { useTheme } from '@/lib/theme'
import { Link, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { useClickOutside } from '@/lib/useClickOutside'
import {
  Calendar,
  Search,
  Moon,
  Sun,
  PanelLeft,
  ChevronDown,
  MoreHorizontal,
  Settings,
  LogOut,
  Trophy,
  X,
} from 'lucide-react'

import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { useAuth } from '@/auth/AuthProvider'
import { compositeRoleLabel } from '@/auth/roles'
import { useEventosList } from '@/features/eventos/hooks'
import { useRaffleList } from '@/features/sorteo/hooks'
import { useFeatureSwitches } from '@/features/admin/hooks'
import { menuForPath } from '@/features/admin/sidebarMenus'
import { useBranding } from '@/lib/branding'
import { MobileMenuNav, SidebarMenuNav } from './ModuleMenu'
import { SidebarTooltip } from './SidebarNav'

const esMac = typeof navigator !== 'undefined' && /Mac|iP(hone|ad)/.test(navigator.platform)

interface Resultado {
  clave: string
  ruta: string
  nombre: string
  tipo: 'sorteo' | 'evento'
}

export function AppShell() {
  const { user, logout, canAccessModule } = useAuth()
  const location = useLocation()
  const navigate = useNavigate()
  const branding = useBranding()
  const [theme, toggleTheme] = useTheme()
  const isDark = theme === 'dark'
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false)
  const [collapsed, setCollapsed] = useState(false)
  const [isOffline, setIsOffline] = useState(!navigator.onLine)
  const [userMenuOpen, setUserMenuOpen] = useState(false)
  const [sidebarUserOpen, setSidebarUserOpen] = useState(false)
  const [searchOpen, setSearchOpen] = useState(false)
  const [searchTerm, setSearchTerm] = useState('')

  const userMenuRef = useRef<HTMLDivElement>(null)
  const sidebarUserRef = useRef<HTMLDivElement>(null)
  const searchRef = useRef<HTMLDivElement>(null)
  const searchInputRef = useRef<HTMLInputElement>(null)

  useClickOutside(userMenuRef, () => setUserMenuOpen(false), userMenuOpen)
  useClickOutside(sidebarUserRef, () => setSidebarUserOpen(false), sidebarUserOpen)
  useClickOutside(searchRef, () => setSearchOpen(false), searchOpen)

  // La búsqueda encuentra sorteos y, con el módulo de Eventos encendido, también eventos.
  const { isEnabled } = useFeatureSwitches()
  const buscaSorteos = canAccessModule('sorteo')
  const buscaEventos =
    canAccessModule('events') && isEnabled('events') && isEnabled('events.events')
  const { data: rafflesData } = useRaffleList(buscaSorteos)
  const { data: eventosData } = useEventosList({ page: 1, pageSize: 100 }, buscaEventos)

  const resultados = useMemo<Resultado[]>(() => {
    const term = searchTerm.trim().toLowerCase()
    if (term.length < 2) return []
    const sorteos: Resultado[] = (rafflesData?.items ?? [])
      .filter((r) => r.eventName.toLowerCase().includes(term))
      .slice(0, 5)
      .map((r) => ({
        clave: `s-${r.raffleId}`,
        ruta: `/sorteo/sorteos/${r.eventId}`,
        nombre: r.eventName,
        tipo: 'sorteo',
      }))
    const eventos: Resultado[] = buscaEventos
      ? (eventosData?.items ?? [])
          .filter((e) => e.name.toLowerCase().includes(term))
          .slice(0, 5)
          .map((e) => ({
            clave: `e-${e.id}`,
            ruta: `/eventos/${e.id}`,
            nombre: e.name,
            tipo: 'evento',
          }))
      : []
    return [...sorteos, ...eventos]
  }, [searchTerm, rafflesData, eventosData, buscaEventos])

  const goToSearchResult = (path: string) => {
    navigate(path)
    setSearchOpen(false)
    setSearchTerm('')
  }

  const abrirBusqueda = () => {
    setCollapsed(false)
    // El campo aparece al desplegarse la barra: se enfoca en el cuadro siguiente.
    requestAnimationFrame(() => searchInputRef.current?.focus())
  }

  // Ctrl+K (⌘K en Mac) lleva a la búsqueda desde cualquier pantalla.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key.toLowerCase() === 'k' && (e.metaKey || e.ctrlKey)) {
        e.preventDefault()
        abrirBusqueda()
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])

  const menu = menuForPath(location.pathname)

  useEffect(() => {
    const handleOnline = () => setIsOffline(false)
    const handleOffline = () => setIsOffline(true)
    window.addEventListener('online', handleOnline)
    window.addEventListener('offline', handleOffline)
    return () => {
      window.removeEventListener('online', handleOnline)
      window.removeEventListener('offline', handleOffline)
    }
  }, [])

  const userName = user?.nombre || 'Administrador'
  const userDept = user?.department || compositeRoleLabel(user?.roles || []) || 'Sorteo360'
  const initials = userName
    .split(' ')
    .map((n) => n[0])
    .join('')
    .substring(0, 2)
    .toUpperCase()

  /** Lo que ofrece el menú del usuario, igual en la barra lateral y en la cabecera móvil. */
  const opcionesUsuario = (cerrar: () => void) => (
    <>
      <div className="flex items-center gap-3 border-b border-slate-100 px-3.5 pb-3 pt-1 dark:border-slate-800">
        <Avatar className="h-9 w-9 shrink-0">
          <AvatarFallback className="bg-brand-deep text-xs font-bold text-white">
            {initials}
          </AvatarFallback>
        </Avatar>
        <div className="min-w-0">
          <p className="truncate text-sm font-semibold text-slate-900 dark:text-white">
            {userName}
          </p>
          <p className="truncate text-xs text-slate-500">{userDept}</p>
        </div>
      </div>
      <div className="p-1.5">
        <Link
          to="/configuracion"
          onClick={cerrar}
          className="flex w-full items-center gap-3 rounded-lg px-2.5 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100 dark:text-slate-200 dark:hover:bg-slate-800"
        >
          <Settings size={16} className="text-slate-400" /> Configuración
        </Link>
        <button
          type="button"
          onClick={toggleTheme}
          className="flex w-full items-center gap-3 rounded-lg px-2.5 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100 dark:text-slate-200 dark:hover:bg-slate-800"
        >
          {isDark ? (
            <Sun size={16} className="text-slate-400" />
          ) : (
            <Moon size={16} className="text-slate-400" />
          )}
          {isDark ? 'Modo claro' : 'Modo oscuro'}
        </button>
      </div>
      <div className="border-t border-slate-100 p-1.5 dark:border-slate-800">
        <button
          type="button"
          onClick={() => {
            cerrar()
            logout()
          }}
          className="flex w-full items-center gap-3 rounded-lg px-2.5 py-2 text-sm font-medium text-rose-600 hover:bg-rose-50 dark:hover:bg-rose-950/40"
        >
          <LogOut size={16} /> Cerrar sesión
        </button>
      </div>
    </>
  )

  return (
    <div className="flex h-screen w-full overflow-hidden bg-background text-foreground transition-colors dark:bg-card dark:text-foreground">
      <aside
        className={`hidden h-screen shrink-0 flex-col border-r border-slate-200/80 bg-white transition-[width] duration-300 ease-in-out lg:flex dark:border-slate-800 dark:bg-card ${
          collapsed ? 'w-20' : 'w-64'
        }`}
      >
        {/* La marca, a la altura de la cabecera */}
        <div
          className={`flex h-16 shrink-0 items-center overflow-hidden px-4 ${
            collapsed ? 'justify-center' : 'justify-start'
          }`}
        >
          <img
            src={branding.logoUrl}
            alt={branding.organizationName}
            className={`w-auto shrink-0 object-contain dark:hidden ${
              collapsed ? 'h-7 max-w-[48px]' : 'h-8 max-w-[190px]'
            }`}
          />
          <img
            src={branding.logoDarkUrl}
            alt={branding.organizationName}
            className={`hidden w-auto shrink-0 object-contain dark:block ${
              collapsed ? 'h-7 max-w-[48px]' : 'h-8 max-w-[190px]'
            }`}
          />
        </div>

        {/* La búsqueda */}
        <div className="relative shrink-0 px-3 pb-3 pt-1" ref={searchRef}>
          {collapsed ? (
            <button
              type="button"
              onClick={abrirBusqueda}
              aria-label="Buscar"
              className="group relative mx-auto flex h-10 w-10 items-center justify-center rounded-lg bg-slate-100 text-slate-500 hover:text-slate-900 dark:bg-slate-800 dark:text-slate-400 dark:hover:text-white"
            >
              <Search size={18} className="stroke-[1.8]" />
              <SidebarTooltip>Buscar</SidebarTooltip>
            </button>
          ) : (
            <>
              <Search
                size={16}
                className="pointer-events-none absolute left-6 top-1/2 -translate-y-1/2 text-slate-400"
              />
              <input
                ref={searchInputRef}
                type="search"
                value={searchTerm}
                onChange={(e) => {
                  setSearchTerm(e.target.value)
                  setSearchOpen(true)
                }}
                onFocus={() => setSearchOpen(true)}
                onKeyDown={(e) => {
                  if (e.key === 'Escape') {
                    setSearchOpen(false)
                    e.currentTarget.blur()
                  }
                }}
                placeholder="Buscar"
                aria-label="Buscar sorteos y eventos"
                className="h-10 w-full rounded-lg border border-transparent bg-slate-100 pl-9 pr-14 text-sm text-slate-800 placeholder:text-slate-500 focus:border-slate-300 focus:bg-white focus:outline-none dark:bg-slate-800 dark:text-white dark:focus:border-slate-600 dark:focus:bg-slate-900 [&::-webkit-search-cancel-button]:hidden"
              />
              {searchTerm ? (
                <button
                  type="button"
                  onClick={() => setSearchTerm('')}
                  aria-label="Borrar la búsqueda"
                  className="absolute right-5 top-1/2 -translate-y-1/2 rounded p-1 text-slate-400 hover:text-slate-700 dark:hover:text-slate-200"
                >
                  <X size={14} />
                </button>
              ) : (
                <kbd className="pointer-events-none absolute right-5 top-1/2 -translate-y-1/2 rounded-md border border-slate-200 bg-white px-1.5 py-0.5 font-sans text-[11px] font-medium text-slate-500 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-400">
                  {esMac ? '⌘K' : 'Ctrl K'}
                </kbd>
              )}

              {searchOpen && searchTerm.trim().length >= 2 && (
                <div className="absolute left-3 right-3 top-full z-50 mt-1 rounded-xl border border-slate-200 bg-white p-1.5 shadow-xl dark:border-slate-800 dark:bg-slate-900">
                  {resultados.length === 0 ? (
                    <p className="px-3 py-4 text-center text-xs text-slate-400">
                      Sin resultados para “{searchTerm}”.
                    </p>
                  ) : (
                    <ul className="max-h-80 overflow-y-auto">
                      {resultados.map((r) => (
                        <li key={r.clave}>
                          <button
                            type="button"
                            onClick={() => goToSearchResult(r.ruta)}
                            className="flex w-full items-center gap-2.5 rounded-lg px-2.5 py-2 text-left text-sm text-slate-700 hover:bg-slate-100 dark:text-slate-200 dark:hover:bg-slate-800"
                          >
                            {r.tipo === 'sorteo' ? (
                              <Trophy size={15} className="shrink-0 text-slate-400" />
                            ) : (
                              <Calendar size={15} className="shrink-0 text-slate-400" />
                            )}
                            <span className="truncate">{r.nombre}</span>
                          </button>
                        </li>
                      ))}
                    </ul>
                  )}
                </div>
              )}
            </>
          )}
        </div>

        {/* El menú. Plegada, sin recorte: los globos con el nombre salen por la derecha. */}
        <div
          className={`flex-1 px-3 py-2 ${collapsed ? 'overflow-visible' : 'overflow-y-auto scrollbar-thin'}`}
        >
          <SidebarMenuNav menu={menu} collapsed={collapsed} />
        </div>

        {/* El usuario, con su menú hacia arriba */}
        <div
          className="relative shrink-0 border-t border-slate-100 p-3 dark:border-slate-800"
          ref={sidebarUserRef}
        >
          <button
            type="button"
            onClick={() => setSidebarUserOpen((v) => !v)}
            aria-expanded={sidebarUserOpen}
            aria-label={collapsed ? `Menú de ${userName}` : undefined}
            className={`group relative flex w-full items-center rounded-lg p-1.5 text-left transition-colors hover:bg-slate-50 dark:hover:bg-slate-800 ${
              collapsed ? 'justify-center' : 'gap-3'
            }`}
          >
            <Avatar className="h-9 w-9 shrink-0">
              <AvatarFallback className="bg-brand-deep text-xs font-bold text-white">
                {initials}
              </AvatarFallback>
            </Avatar>
            {!collapsed && (
              <>
                <span className="min-w-0 flex-1">
                  <span className="block truncate text-sm font-semibold text-slate-900 dark:text-white">
                    {userName}
                  </span>
                  <span className="block truncate text-xs text-slate-500">{userDept}</span>
                </span>
                <MoreHorizontal size={18} className="shrink-0 text-slate-400" />
              </>
            )}
            {collapsed && <SidebarTooltip>{userName}</SidebarTooltip>}
          </button>

          {sidebarUserOpen && (
            <div
              className={`absolute bottom-full z-50 mb-2 w-64 rounded-xl border border-slate-200 bg-white pt-2 shadow-xl dark:border-slate-800 dark:bg-slate-900 ${
                collapsed ? 'left-3' : 'left-3 right-3 w-auto'
              }`}
            >
              {opcionesUsuario(() => setSidebarUserOpen(false))}
            </div>
          )}
        </div>
      </aside>

      {/* Contenedor Principal con Scroll Independiente */}
      <div className="relative flex flex-1 flex-col h-screen overflow-y-auto min-w-0">
        <header className="flex h-16 shrink-0 items-center justify-between border-b border-slate-200/80 bg-white px-4 sm:px-6 lg:px-8 dark:border-slate-800 dark:bg-slate-900">
          <div className="flex items-center gap-3">
            {/* Toggle para Escritorio (Ocultar / Expandir Sidebar) */}
            <button
              type="button"
              onClick={() => setCollapsed((prev) => !prev)}
              className="hidden lg:flex items-center justify-center rounded-xl p-2 text-slate-600 transition-colors hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800"
              title={collapsed ? 'Expandir barra lateral' : 'Ocultar barra lateral'}
            >
              <PanelLeft size={19} className="stroke-2" />
            </button>

            {/* Toggle para Móvil */}
            <button
              type="button"
              onClick={() => setMobileMenuOpen((prev) => !prev)}
              aria-label={mobileMenuOpen ? 'Cerrar el menú' : 'Abrir el menú'}
              aria-expanded={mobileMenuOpen}
              className="flex items-center justify-center rounded-xl p-2 text-slate-600 hover:bg-slate-100 lg:hidden dark:text-slate-300 dark:hover:bg-slate-800"
            >
              <PanelLeft size={19} />
            </button>
          </div>

          {/* Lado derecho: tema y, en pantallas sin barra lateral, el menú de usuario */}
          <div className="flex items-center gap-2 sm:gap-3">
            <button
              type="button"
              onClick={toggleTheme}
              className="rounded-lg p-2 text-slate-600 transition-colors hover:bg-slate-100 dark:text-slate-400 dark:hover:bg-slate-800 dark:hover:text-white cursor-pointer"
              title={isDark ? 'Modo claro' : 'Modo oscuro'}
            >
              {isDark ? <Sun size={18} /> : <Moon size={18} />}
            </button>

            <div
              className="relative ml-1 border-l border-slate-200/80 pl-2 lg:hidden dark:border-slate-800"
              ref={userMenuRef}
            >
              <button
                type="button"
                onClick={() => setUserMenuOpen((prev) => !prev)}
                className="flex items-center gap-1 rounded-full p-0.5 transition-colors hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
                title={`${userName} (${userDept})`}
              >
                <Avatar className="h-8 w-8">
                  <AvatarFallback className="bg-brand-deep text-xs font-extrabold text-white">
                    {initials}
                  </AvatarFallback>
                </Avatar>
                <ChevronDown size={14} className="text-slate-400 shrink-0" />
              </button>

              {userMenuOpen && (
                <div className="absolute right-0 z-50 mt-2 w-64 rounded-xl border border-slate-200 bg-white pt-2 shadow-xl dark:border-slate-800 dark:bg-slate-900">
                  {opcionesUsuario(() => setUserMenuOpen(false))}
                </div>
              )}
            </div>
          </div>
        </header>

        {/* Menú Móvil Desplegable */}
        {mobileMenuOpen && (
          <div className="border-b border-slate-200 bg-white py-3 lg:hidden dark:border-slate-800 dark:bg-slate-900">
            <nav className="flex flex-col gap-1 px-4">
              <MobileMenuNav menu={menu} onNavigate={() => setMobileMenuOpen(false)} />
            </nav>
          </div>
        )}

        {/* Banner Indicativo de Modo Sin Conexión (Offline / PWA) */}
        {isOffline && (
          <div className="bg-amber-500 px-4 py-2 text-center text-xs font-bold text-slate-950 shadow-xs flex items-center justify-center gap-2">
            <span>
              Modo Sin Conexión activo — Se está utilizando la caché local de {branding.appName}.
              Las modificaciones se sincronizarán al reconectar.
            </span>
          </div>
        )}

        {/* Contenido Principal */}
        <main className="flex-1 px-4 py-6 sm:px-6 lg:px-8">
          <div className="mx-auto max-w-7xl">
            <Outlet />
          </div>
        </main>

        {/* Footer Marca Blanca */}
        <footer className="border-t border-slate-200/80 bg-white/80 py-3 px-4 text-center text-xs text-slate-500 dark:border-slate-800 dark:bg-slate-900/60 dark:text-slate-400">
          {branding.appName} · {branding.organizationName}
        </footer>
      </div>
    </div>
  )
}
