import { Link } from 'react-router-dom'

export interface BreadcrumbItem {
  label: string
  to?: string
}

export function Breadcrumbs({ items }: { items: BreadcrumbItem[] }) {
  const hasInicio = items.length > 0 && items[0].label.toLowerCase() === 'inicio'
  const fullItems: BreadcrumbItem[] = hasInicio
    ? items
    : [{ label: 'Inicio', to: '/app' }, ...items]

  return (
    <nav aria-label="Breadcrumb" className="mb-3">
      <ol className="flex flex-wrap items-center gap-1.5 text-xs text-slate-500 font-medium">
        {fullItems.map((item, index) => {
          const isLast = index === fullItems.length - 1
          return (
            <li key={index} className="flex items-center gap-1.5">
              {index > 0 && <span className="text-slate-400 font-normal">/</span>}
              {isLast || !item.to ? (
                <span className="font-bold text-slate-800 dark:text-white truncate max-w-55 sm:max-w-none">
                  {item.label}
                </span>
              ) : (
                <Link
                  to={item.to}
                  className="hover:text-brand-deep transition-colors dark:hover:text-blue-300"
                >
                  {item.label}
                </Link>
              )}
            </li>
          )
        })}
      </ol>
    </nav>
  )
}
