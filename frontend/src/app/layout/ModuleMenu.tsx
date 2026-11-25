import { NavLink } from 'react-router-dom'
import { ArrowLeft } from 'lucide-react'
import type { SidebarMenu } from '@/features/admin/sidebarMenus'
import { SidebarBackLink, SidebarLink, SidebarSectionLabel } from './SidebarNav'
import { useSoloUnModulo } from '@/features/portal/modulosVisibles'
import { useMobileEntries, useVisibleEntries } from './useModuleMenu'

/** La barra lateral de escritorio del menú abierto, con el regreso al portal al final. */
export function SidebarMenuNav({ menu, collapsed }: { menu: SidebarMenu; collapsed: boolean }) {
  const entries = useVisibleEntries(menu)
  const soloUnModulo = useSoloUnModulo()
  return (
    <div>
      <SidebarSectionLabel collapsed={collapsed}>{menu.heading}</SidebarSectionLabel>
      <nav className="space-y-0.5">
        <SidebarLink
          to={menu.dashboard.to}
          end
          icon={menu.dashboard.icon}
          label={menu.dashboard.label}
          collapsed={collapsed}
        />
        {entries.map((entry) => (
          <SidebarLink
            key={entry.to}
            to={entry.to}
            icon={entry.icon}
            label={entry.label}
            collapsed={collapsed}
          />
        ))}

        {!soloUnModulo && (
          <div className="mt-3 border-t border-slate-100 pt-3 dark:border-slate-800">
            <SidebarBackLink collapsed={collapsed} />
          </div>
        )}
      </nav>
    </div>
  )
}

const MOBILE_LINK = 'flex min-h-11 items-center gap-3 rounded-xl px-3 py-2 text-sm font-semibold'

/** El menú en el desplegable móvil; cada enlace lo cierra al navegar. */
export function MobileMenuNav({ menu, onNavigate }: { menu: SidebarMenu; onNavigate: () => void }) {
  const items = useMobileEntries(menu)
  const soloUnModulo = useSoloUnModulo()
  return (
    <>
      <span className="px-3 text-[11px] font-extrabold uppercase tracking-wider text-blue-600">
        {menu.heading}
      </span>
      {items.map((entry) => (
        <NavLink
          key={entry.to}
          to={entry.to}
          end={entry === menu.dashboard}
          onClick={onNavigate}
          className={({ isActive }) =>
            `${MOBILE_LINK} ${
              isActive
                ? 'bg-slate-100 text-slate-900 dark:bg-slate-800 dark:text-white'
                : 'text-slate-600 active:bg-slate-50 dark:text-slate-300 dark:active:bg-slate-800/60'
            }`
          }
        >
          <entry.icon size={18} />
          {entry.label}
        </NavLink>
      ))}
      {!soloUnModulo && (
        <div className="mt-1 border-t border-slate-100 pt-1 dark:border-slate-800">
          <NavLink
            to="/app"
            onClick={onNavigate}
            className={`${MOBILE_LINK} text-slate-500 dark:text-slate-400`}
          >
            <ArrowLeft size={18} />
            Volver al Portal
          </NavLink>
        </div>
      )}
    </>
  )
}

/** La barra inferior del teléfono: los destinos del módulo al alcance del pulgar. */
export function MobileTabBar({ menu }: { menu: SidebarMenu }) {
  const items = useMobileEntries(menu)
  return (
    <nav
      aria-label={menu.heading}
      className="fixed inset-x-0 bottom-0 z-40 flex border-t border-slate-200/80 bg-white/95 backdrop-blur-md lg:hidden dark:border-slate-800 dark:bg-slate-900/95"
    >
      {items.map((entry) => (
        <NavLink
          key={entry.to}
          to={entry.to}
          end={entry === menu.dashboard}
          className={({ isActive }) =>
            `flex min-h-16 min-w-0 flex-1 flex-col items-center justify-center gap-1 px-1 text-[11px] font-semibold outline-none transition-colors focus-visible:bg-slate-100 dark:focus-visible:bg-slate-800 ${
              isActive ? 'text-brand-deep dark:text-blue-300' : 'text-slate-500 dark:text-slate-400'
            }`
          }
        >
          {({ isActive }) => (
            <>
              <span
                className={`flex h-7 w-14 items-center justify-center rounded-full transition-colors duration-200 ${
                  isActive ? 'bg-slate-100 dark:bg-slate-800' : ''
                }`}
              >
                <entry.icon size={20} className="stroke-[1.9]" />
              </span>
              <span className="max-w-full truncate">{entry.label}</span>
            </>
          )}
        </NavLink>
      ))}
    </nav>
  )
}
