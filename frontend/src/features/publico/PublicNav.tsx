import { NavLink } from 'react-router-dom'
import { List, CalendarDays } from 'lucide-react'

/**
 * Navegación propia del portal público: las pestañas de la cartelera.
 */
const TABS = [
  { to: '/publico/eventos', label: 'Próximos eventos', icon: List },
  { to: '/publico/calendario', label: 'Calendario', icon: CalendarDays },
]

export function PublicNav() {
  return (
    <div className="mx-auto max-w-7xl px-4 pt-6 sm:px-6 lg:px-8">
      <nav className="flex w-fit items-center gap-1 rounded-xl border border-slate-200/80 bg-white p-1 dark:border-slate-800 dark:bg-slate-900">
        {TABS.map(({ to, label, icon: Icon }) => (
          <NavLink
            key={to}
            to={to}
            className={({ isActive }) =>
              `flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-xs font-bold transition-colors ${
                isActive
                  ? 'bg-brand-deep text-white dark:bg-blue-600'
                  : 'text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800'
              }`
            }
          >
            <Icon size={14} />
            {label}
          </NavLink>
        ))}
      </nav>
    </div>
  )
}
