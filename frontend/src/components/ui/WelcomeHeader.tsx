import { useAuth } from '@/auth/AuthProvider'

interface WelcomeHeaderProps {
  subtitle?: string
}

export function WelcomeHeader({ subtitle }: WelcomeHeaderProps) {
  const { user } = useAuth()
  const userName = user?.nombre || 'Ana Coordinadora'

  const getGreeting = () => {
    const hour = new Date().getHours()
    if (hour < 12) return 'Buenos días'
    if (hour < 18) return 'Buenas tardes'
    return 'Buenas noches'
  }

  const defaultSubtitle = `Resumen general de la actividad de PFGTI360 Pulse.`

  return (
    <div className="mb-6">
      <h1 className="font-sans text-2xl font-extrabold tracking-tight text-brand-deep sm:text-3xl lg:text-4xl dark:text-white">
        {getGreeting()}, {userName}
      </h1>
      <p className="mt-1 font-body text-xs font-medium text-slate-500 sm:text-sm lg:text-base dark:text-slate-400">
        {subtitle || defaultSubtitle}
      </p>
    </div>
  )
}
