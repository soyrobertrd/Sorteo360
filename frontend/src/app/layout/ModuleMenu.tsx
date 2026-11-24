import { NavLink } from 'react-router-dom'
import { useAuth } from '@/auth/AuthProvider'
import { useFeatureSwitches } from '@/features/admin/hooks'
import type { SidebarEntry, SidebarMenu } from '@/features/admin/sidebarMenus'
import { SidebarBackLink, SidebarLink, SidebarSectionLabel } from './SidebarNav'
import { useSoloUnModulo } from '@/features/portal/modulosVisibles'

/** Las entradas que este usuario ve: las de su rol y con su interruptor encendido. */
function useVisibleEntries(menu: SidebarMenu): readonly SidebarEntry[] {
  const { hasRole } = useAuth()
  const { isEnabled } = useFeatureSwitches()
  return menu.entries.filter(
    (entry) => (!entry.role || hasRole(entry.role)) && (!entry.key || isEnabled(entry.key)),
  )
}

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

const MOBILE_LINK =
  'flex items-center gap-3 rounded-xl px-3 py-2 text-sm font-semibold text-slate-600 dark:text-slate-300'

/** El mismo menú en el desplegable móvil; cada enlace lo cierra al navegar. */
export function MobileMenuNav({ menu, onNavigate }: { menu: SidebarMenu; onNavigate: () => void }) {
  const entries = useVisibleEntries(menu)
  return (
    <>
      <span className="text-[11px] font-extrabold text-blue-600 uppercase tracking-wider px-3">
        {menu.heading}
      </span>
      {[menu.dashboard, ...entries].map((entry) => (
        <NavLink
          key={entry.to}
          to={entry.to}
          end={entry === menu.dashboard}
          onClick={onNavigate}
          className={MOBILE_LINK}
        >
          <entry.icon size={17} />
          {entry.label}
        </NavLink>
      ))}
    </>
  )
}
