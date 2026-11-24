import { Link, useNavigate } from 'react-router-dom'
import { Compass, ArrowLeft } from 'lucide-react'
import { Button } from '@/components/ui/Button'

export function NotFoundPage() {
  const navigate = useNavigate()

  return (
    <div className="flex min-h-[70vh] flex-col items-center justify-center gap-5 text-center px-4">
      <div className="flex h-16 w-16 items-center justify-center rounded-2xl bg-blue-50 text-brand-deep border border-blue-200/80 dark:bg-blue-950/50 dark:text-blue-300 dark:border-blue-900">
        <Compass size={30} />
      </div>
      <div className="space-y-1.5">
        <h1 className="font-sans text-3xl font-black text-slate-900 dark:text-white">404</h1>
        <p className="font-sans text-base font-bold text-slate-700 dark:text-slate-300">
          Esta página no existe o fue movida
        </p>
        <p className="max-w-sm text-xs text-slate-500 dark:text-slate-400">
          Verifica el enlace o vuelve al panel principal de Sorteo360.
        </p>
      </div>
      <div className="flex items-center gap-3">
        <Button variant="outline" onClick={() => navigate(-1)} className="cursor-pointer font-bold">
          <ArrowLeft size={15} className="mr-1.5" /> Volver
        </Button>
        <Link to="/eventos">
          <Button className="cursor-pointer bg-brand-deep font-bold text-white hover:bg-brand-deep/90">
            Ir al panel principal
          </Button>
        </Link>
      </div>
    </div>
  )
}
