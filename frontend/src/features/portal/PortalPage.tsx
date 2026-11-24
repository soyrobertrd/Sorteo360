import { Link, Navigate } from 'react-router-dom'
import { Calendar, FileText, Sparkles, ArrowRight, Lock, Settings2 } from 'lucide-react'
import { Card } from '@/components/ui/Card'
import { useAuth } from '@/auth/AuthProvider'
import { ADMIN_ROLES, landsOnScanner } from '@/auth/roles'
import { useModulosVisibles } from './modulosVisibles'
import { useBranding } from '@/lib/branding'

interface ModuleCardProps {
  title: string
  description: string
  icon: React.ElementType
  colorScheme:
    | 'blue'
    | 'emerald'
    | 'purple'
    | 'amber'
    | 'rose'
    | 'cyan'
    | 'orange'
    | 'teal'
    | 'indigo'
    | 'slate'
  to: string
  comingSoon?: boolean
}

function ModuleCard({
  title,
  description,
  icon: Icon,
  colorScheme,
  to,
  comingSoon,
}: ModuleCardProps) {
  const colorStyles = {
    blue: 'border-blue-200/80 bg-blue-50/60 text-blue-600 dark:border-blue-900/50 dark:bg-blue-950/30 dark:text-blue-400',
    emerald:
      'border-emerald-200/80 bg-emerald-50/60 text-emerald-600 dark:border-emerald-900/50 dark:bg-emerald-950/30 dark:text-emerald-400',
    purple:
      'border-purple-200/80 bg-purple-50/60 text-purple-600 dark:border-purple-900/50 dark:bg-purple-950/30 dark:text-purple-400',
    amber:
      'border-amber-200/80 bg-amber-50/60 text-amber-600 dark:border-amber-900/50 dark:bg-amber-950/30 dark:text-amber-400',
    rose: 'border-rose-200/80 bg-rose-50/60 text-rose-600 dark:border-rose-900/50 dark:bg-rose-950/30 dark:text-rose-400',
    cyan: 'border-cyan-200/80 bg-cyan-50/60 text-cyan-600 dark:border-cyan-900/50 dark:bg-cyan-950/30 dark:text-cyan-400',
    orange:
      'border-orange-200/80 bg-orange-50/60 text-orange-600 dark:border-orange-900/50 dark:bg-orange-950/30 dark:text-orange-400',
    teal: 'border-teal-200/80 bg-teal-50/60 text-teal-600 dark:border-teal-900/50 dark:bg-teal-950/30 dark:text-teal-400',
    indigo:
      'border-indigo-200/80 bg-indigo-50/60 text-indigo-600 dark:border-indigo-900/50 dark:bg-indigo-950/30 dark:text-indigo-400',
    slate:
      'border-slate-200/80 bg-slate-50/60 text-brand-deep dark:border-slate-700 dark:bg-slate-800/40 dark:text-blue-300',
  }

  return (
    <Card
      className={`group relative flex flex-col justify-between px-4.75 py-3.75 lg:px-5.75 lg:py-4.75 transition-all duration-200 hover:-translate-y-1 hover:border-slate-300 hover:shadow-lg dark:hover:border-slate-700 ${comingSoon ? 'opacity-80' : ''}`}
    >
      {comingSoon && (
        <span className="absolute top-3 right-3 rounded-full bg-slate-100 px-2 py-0.5 text-[10px] font-bold uppercase tracking-wider text-slate-500 dark:bg-slate-800 dark:text-slate-400">
          Próximamente
        </span>
      )}
      <div>
        {/* Contenedor del ícono idéntico al prototipo */}
        <div
          className={`mb-3 flex h-12 w-12 items-center justify-center rounded-2xl border ${colorStyles[colorScheme]} transition-transform duration-200 group-hover:scale-105 sm:mb-4 sm:h-14 sm:w-14`}
        >
          <Icon size={26} className="stroke-[1.8]" />
        </div>

        {/* Título del módulo */}
        <h2 className="mb-2 font-sans text-lg font-extrabold tracking-tight text-brand-deep sm:text-xl dark:text-white">
          {title}
        </h2>

        {/* Descripción funcional */}
        <p className="mb-3 font-body text-xs sm:mb-4 sm:text-sm leading-relaxed text-slate-600 dark:text-slate-400">
          {description}
        </p>
      </div>

      {/* Enlace de acción */}
      <div>
        {comingSoon ? (
          <span className="inline-flex items-center gap-2 font-sans text-xs font-bold text-slate-400 sm:text-sm cursor-not-allowed">
            <span>En desarrollo</span>
          </span>
        ) : (
          <Link
            to={to}
            className="inline-flex items-center gap-2 font-sans text-xs font-bold text-slate-900 sm:text-sm transition-colors group-hover:text-brand-deep dark:text-white dark:group-hover:text-blue-400"
          >
            <span>Acceder al módulo</span>
            <ArrowRight
              size={16}
              className="stroke-[2.5] transition-transform duration-200 group-hover:translate-x-1"
            />
          </Link>
        )}
      </div>
    </Card>
  )
}

export function PortalPage() {
  const { hasRole, user } = useAuth()
  const { modulos, listo } = useModulosVisibles()
  const isLoading = !listo
  const branding = useBranding()
  if (user && landsOnScanner(user.roles)) return <Navigate to="/scanner" replace />

  const allModules = [
    {
      title: 'Rifas en vivo',
      description:
        'Arma la tómbola con tu padrón, recibe a los invitados con QR y gira cada premio en pantalla grande, con acta y entrega firmada.',
      icon: Sparkles,
      colorScheme: 'amber' as const,
      to: '/sorteo',
      module: 'sorteo' as const,
      comingSoon: false,
    },
    {
      title: 'Agenda de eventos',
      description:
        'Las fiestas y actividades de la empresa en un calendario: fecha, lugar y estado de cada una.',
      icon: Calendar,
      colorScheme: 'blue' as const,
      to: '/eventos',
      module: 'events' as const,
      comingSoon: false,
    },
    {
      title: 'Invitaciones',
      description:
        'Envía la invitación por correo, recibe las confirmaciones en un formulario y sigue quién viene.',
      icon: FileText,
      colorScheme: 'purple' as const,
      to: '/convocatorias',
      module: 'convocatorias' as const,
      comingSoon: false,
    },
  ]

  // Filtra los módulos únicamente a los que el usuario tiene acceso y que no están apagados.
  const visibleModules = allModules.filter((mod) => modulos.includes(mod.module))
  // Con un solo módulo a la vista se entra directo a él: el portal sería un clic de más.
  if (listo && visibleModules.length === 1) {
    const unico = visibleModules[0]
    return <Navigate to={unico.module === 'sorteo' ? '/sorteo/sorteos' : unico.to} replace />
  }

  return (
    <div className="py-4 sm:py-6 lg:py-8">
      {/* Sección Hero / Título Principal */}
      <div className="mb-6 sm:mb-8 lg:mb-10 -mt-3">
        <p className="mb-2 text-xs font-bold uppercase tracking-wider text-indigo-600 dark:text-indigo-400">
          {branding.organizationName}
        </p>
        <h1 className="max-w-4xl font-sans text-[18px] font-extrabold tracking-tight text-brand-deep sm:text-[30px] lg:text-[42px] leading-tight sm:leading-tight lg:leading-tight dark:text-white">
          {branding.appName}
        </h1>
        <p className="mt-2 max-w-2xl text-sm text-slate-600 dark:text-slate-400">
          {branding.appSubtitle}
        </p>
      </div>

      {/* Grid de tarjetas filtradas por acceso del usuario */}
      {visibleModules.length > 0 || isLoading ? (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 lg:gap-6">
          {visibleModules.map((mod) => (
            <ModuleCard
              key={mod.title}
              title={mod.title}
              description={mod.description}
              icon={mod.icon}
              colorScheme={mod.colorScheme}
              to={mod.to}
              comingSoon={mod.comingSoon}
            />
          ))}
          {/* Administración: con cualquiera de los roles de sus herramientas */}
          {ADMIN_ROLES.some((role) => hasRole(role)) && (
            <ModuleCard
              title="Marca y ajustes"
              description="Tus colores y logos, y qué módulos ve tu equipo en la plataforma."
              icon={Settings2}
              colorScheme="slate"
              to="/admin"
            />
          )}
        </div>
      ) : (
        <Card className="p-8 text-center">
          <Lock size={36} className="mx-auto mb-3 text-slate-400" />
          <h3 className="text-lg font-bold text-slate-800 dark:text-slate-200">
            Sin acceso a módulos
          </h3>
          <p className="mt-1 text-sm text-slate-500">
            Tu cuenta no tiene permisos asignados para acceder a ningún módulo en este momento.
          </p>
        </Card>
      )}
    </div>
  )
}
