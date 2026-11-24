import type { ReactNode } from 'react'
import { NavLink } from 'react-router-dom'
import { ArrowLeft, type LucideIcon } from 'lucide-react'

/** El texto que se pliega con la barra: se encoge en vez de desmontarse, para animar. */
const etiquetaClass = (collapsed: boolean) =>
  `truncate overflow-hidden whitespace-nowrap transition-all duration-300 ease-in-out ${
    collapsed ? 'max-w-0 opacity-0' : 'max-w-xs opacity-100'
  }`

const FOCO =
  'outline-none focus-visible:ring-2 focus-visible:ring-brand-deep/40 dark:focus-visible:ring-blue-400/50'

/**
 * Las clases de una entrada de la barra lateral: una sola definición del estado activo para
 * todos los módulos. Activa lleva un fondo gris neutro; plegada, un cuadro de 40 px.
 */
function sidebarLinkClass(isActive: boolean, collapsed: boolean, sub = false): string {
  const base = `group relative flex items-center rounded-lg transition-colors duration-200 ${FOCO}`
  if (sub) {
    return `${base} py-1.5 text-xs font-medium ${collapsed ? 'justify-center px-0' : 'gap-2.5 px-2.5'} ${
      isActive
        ? 'text-slate-900 font-semibold dark:text-white'
        : 'text-slate-500 hover:bg-slate-50 hover:text-slate-900 dark:text-slate-400 dark:hover:bg-slate-800 dark:hover:text-white'
    }`
  }
  return `${base} h-10 text-[15px] font-medium ${collapsed ? 'mx-auto w-10 justify-center px-0' : 'gap-3 px-2.5'} ${
    isActive
      ? 'bg-slate-100 text-slate-900 dark:bg-slate-800 dark:text-white'
      : 'text-slate-700 hover:bg-slate-50 hover:text-slate-900 dark:text-slate-300 dark:hover:bg-slate-800/60 dark:hover:text-white'
  }`
}

/** El icono de una entrada: gris mientras descansa, de la marca cuando está activa. */
export function SidebarIcon({
  icon: Icon,
  active,
  size = 18,
}: {
  icon: LucideIcon
  active: boolean
  size?: number
}) {
  return (
    <Icon
      size={size}
      className={`shrink-0 stroke-[1.8] transition-colors duration-200 ${
        active
          ? 'text-brand-deep dark:text-blue-300'
          : 'text-slate-400 group-hover:text-slate-600 dark:text-slate-500 dark:group-hover:text-slate-300'
      }`}
    />
  )
}

/** El nombre de una entrada con la barra plegada: aparece al pasar el ratón o al enfocar. */
export function SidebarTooltip({ children }: { children: ReactNode }) {
  return (
    <span
      aria-hidden="true"
      className="pointer-events-none absolute left-full top-1/2 z-50 ml-3 -translate-y-1/2 whitespace-nowrap rounded-md bg-slate-900 px-2.5 py-1 text-xs font-medium text-white opacity-0 shadow-lg transition-opacity duration-150 group-hover:opacity-100 group-focus-visible:opacity-100 dark:bg-slate-700"
    >
      {children}
    </span>
  )
}

interface SidebarLinkProps {
  to: string
  /** Activa solo en la ruta exacta, como el `end` de NavLink. */
  end?: boolean
  icon: LucideIcon
  label: string
  collapsed: boolean
  /** Entrada de un submenú: más pequeña y sin fondo al activarse. */
  sub?: boolean
}

/** Una entrada de la barra lateral. Plegada muestra solo el icono y el nombre en un globo. */
export function SidebarLink({
  to,
  end,
  icon: Icon,
  label,
  collapsed,
  sub = false,
}: SidebarLinkProps) {
  return (
    <NavLink
      to={to}
      end={end}
      aria-label={collapsed ? label : undefined}
      className={({ isActive }) => sidebarLinkClass(isActive, collapsed, sub)}
    >
      {({ isActive }) => (
        <>
          <SidebarIcon icon={Icon} active={isActive} size={sub ? 16 : 18} />
          <span className={etiquetaClass(collapsed)}>{label}</span>
          {collapsed && <SidebarTooltip>{label}</SidebarTooltip>}
        </>
      )}
    </NavLink>
  )
}

/** El rótulo de un grupo. Plegada la barra deja una raya, para que los grupos sigan separados. */
export function SidebarSectionLabel({
  collapsed,
  children,
}: {
  collapsed: boolean
  children: ReactNode
}) {
  return (
    <div className="mb-1.5">
      <p
        className={`overflow-hidden whitespace-nowrap px-2.5 text-[13px] font-medium text-slate-500 transition-all duration-300 ease-in-out dark:text-slate-400 ${
          collapsed ? 'max-h-0 opacity-0' : 'max-h-6 opacity-100'
        }`}
      >
        {children}
      </p>
      <div
        aria-hidden="true"
        className={`mx-auto h-px w-6 bg-slate-200 transition-opacity duration-300 dark:bg-slate-700 ${
          collapsed ? 'opacity-100' : 'opacity-0'
        }`}
      />
    </div>
  )
}

/** La salida al portal, al pie de cada módulo. */
export function SidebarBackLink({ collapsed }: { collapsed: boolean }) {
  return (
    <NavLink
      to="/app"
      aria-label={collapsed ? 'Volver al Portal' : undefined}
      className={`group relative flex h-10 items-center rounded-lg text-sm font-medium text-slate-500 transition-colors duration-200 hover:bg-slate-50 hover:text-slate-900 dark:text-slate-400 dark:hover:bg-slate-800 dark:hover:text-white ${FOCO} ${
        collapsed ? 'mx-auto w-10 justify-center px-0' : 'gap-3 px-2.5'
      }`}
    >
      <ArrowLeft
        size={18}
        className="shrink-0 stroke-[1.8] text-slate-400 transition-transform duration-200 group-hover:-translate-x-0.5"
      />
      <span className={etiquetaClass(collapsed)}>Volver al Portal</span>
      {collapsed && <SidebarTooltip>Volver al Portal</SidebarTooltip>}
    </NavLink>
  )
}
