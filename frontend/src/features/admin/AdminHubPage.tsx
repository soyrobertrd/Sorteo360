import { Link } from 'react-router-dom'
import { ArrowRight, Palette, Settings2, ToggleRight, type LucideIcon } from 'lucide-react'
import { useAuth } from '@/auth/AuthProvider'
import { ROLES, type FunctionRole } from '@/auth/roles'
import { Breadcrumbs } from '@/components/ui/Breadcrumbs'
import { Card } from '@/components/ui/Card'

/** Una herramienta de Administración, que solo ve quien tiene su rol. */
interface Herramienta {
  to: string
  title: string
  description: string
  icon: LucideIcon
  role?: FunctionRole
}

const HERRAMIENTAS: readonly Herramienta[] = [
  {
    to: '/admin/feature-switches',
    title: 'Interruptores de funciones',
    description:
      'Habilitar o mantener ocultos módulos como Eventos y Convocatorias y sus vistas laterales.',
    icon: ToggleRight,
    role: ROLES.featureSwitchesWrite,
  },
  {
    to: '/configuracion?tab=marca',
    title: 'Marca Blanca & Logos',
    description:
      'Configurar el nombre de la aplicación, organización anfitriona y los logotipos claros y oscuros.',
    icon: Palette,
  },
]

/** `/admin`: las herramientas de Administración, cada una con su propio rol. */
export function AdminHubPage() {
  const { hasRole } = useAuth()
  const visibles = HERRAMIENTAS.filter((h) => !h.role || hasRole(h.role))
  return (
    <div className="space-y-6">
      <Breadcrumbs items={[{ label: 'Administración' }]} />
      <div>
        <h1 className="flex items-center gap-2.5 font-sans text-2xl font-extrabold tracking-tight text-slate-900 sm:text-3xl dark:text-white">
          <Settings2 className="h-7 w-7 text-brand-deep dark:text-blue-400" />
          <span>Administración</span>
        </h1>
        <p className="mt-1 font-body text-xs text-slate-500 sm:text-sm dark:text-slate-400">
          Herramientas para configurar la plataforma.
        </p>
      </div>
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {visibles.map((h) => (
          <Card
            key={h.to}
            className="relative flex flex-col justify-between p-5 transition-all hover:-translate-y-1 hover:shadow-lg"
          >
            <div>
              <div className="mb-3 flex h-12 w-12 items-center justify-center rounded-2xl border border-slate-200 bg-muted text-brand-deep dark:border-slate-700 dark:bg-blue-950/50 dark:text-blue-300">
                <h.icon size={24} />
              </div>
              <h2 className="mb-1 font-sans text-lg font-extrabold text-brand-deep dark:text-white">
                {h.title}
              </h2>
              <p className="mb-4 text-sm text-slate-600 dark:text-slate-400">{h.description}</p>
            </div>
            <Link
              to={h.to}
              aria-label={`Abrir ${h.title}`}
              className="inline-flex items-center gap-2 text-sm font-bold text-slate-900 hover:text-brand-deep dark:text-white"
            >
              Abrir <ArrowRight size={16} />
            </Link>
          </Card>
        ))}
      </div>
    </div>
  )
}
