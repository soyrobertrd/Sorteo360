import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Gift, Trophy, Sparkles, Users, ArrowRight, Plus, Play } from 'lucide-react'
import { StatCard } from '@/components/ui/StatCard'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import type { RaffleListItem } from '@/api/types'
import { useCan } from '@/auth/AuthProvider'
import { ROLES } from '@/auth/roles'
import { FeatureGate } from '@/features/admin/FeatureGate'
import { formatDate } from '@/lib/format'
import { useRaffleList, useRaffleTotals } from './hooks'
import { EstadoSorteoBadge } from './components/EstadoSorteoBadge'
import { NuevoSorteoModal } from './components/NuevoSorteoModal'
import { IniciarSorteoModal } from './components/IniciarSorteoModal'

export function SorteoDashboardPage() {
  const { data } = useRaffleList()
  const { data: totals } = useRaffleTotals()
  const canWrite = useCan(ROLES.rafflesWrite)
  const canDraw = useCan(ROLES.rafflesDraw)
  const [showNuevoModal, setShowNuevoModal] = useState(false)
  const [sorteoParaEjecutar, setSorteoParaEjecutar] = useState<RaffleListItem | null>(null)

  const sorteos = data?.items ?? []
  const sorteosRecientes = sorteos.slice(0, 5)

  // Encontrar el sorteo en progreso, o si no el primer borrador
  const proximoSorteo =
    sorteos.find((s) => s.state === 'in_progress') || sorteos.find((s) => s.state === 'draft')

  const navigate = useNavigate()

  return (
    <div className="space-y-6">
      {/* Header institucional con botón "+ Nuevo Sorteo" en la esquina superior derecha */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 pb-2 border-b border-slate-200 dark:border-slate-800">
        <div>
          <h1 className="text-2xl sm:text-3xl font-black tracking-tight text-slate-900 dark:text-white">
            Módulo de Sorteos & Rifas
          </h1>
          <p className="mt-1 text-xs sm:text-sm text-slate-500 dark:text-slate-400">
            Gestión de rifas corporativas, carga de participantes, check-in QR y ejecución en vivo.
          </p>
        </div>

        <div className="flex items-center gap-3 shrink-0">
          <Button
            onClick={() => setShowNuevoModal(true)}
            disabled={!canWrite.allowed}
            title={canWrite.title}
            className="bg-blue-600 hover:bg-blue-700 text-white font-bold text-sm px-4 py-2.5 cursor-pointer shadow-sm"
          >
            <Plus className="w-4 h-4 mr-1.5 stroke-[2.5]" />
            Nuevo Sorteo
          </Button>
        </div>
      </div>

      {/* Banner Sorteo Listo Para Ejecutar */}
      {proximoSorteo && (
        <div className="relative overflow-hidden rounded-2xl bg-brand-deep p-5 sm:p-6 shadow-md">
          <div className="relative z-10 flex flex-col md:flex-row md:items-center md:justify-between gap-4">
            <div className="space-y-2">
              <div className="flex items-center gap-2 flex-wrap">
                <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-black uppercase tracking-wider bg-blue-100 text-blue-800 dark:bg-blue-950/70 dark:text-blue-300 border border-blue-200 dark:border-blue-800">
                  <Sparkles className="w-3.5 h-3.5 text-blue-600 dark:text-blue-400" />
                  Sorteo Listo Para Ejecutar
                </span>
                <EstadoSorteoBadge estado={proximoSorteo.state} />
              </div>

              <div>
                <h2 className="text-xl sm:text-2xl font-black text-white tracking-tight">
                  {proximoSorteo.eventName}
                </h2>
                <p className="text-xs sm:text-sm text-blue-100/90 mt-1 flex items-center gap-1.5 flex-wrap">
                  <span>
                    Fecha del evento:{' '}
                    <strong className="text-white font-semibold">
                      {formatDate(proximoSorteo.eventStartDate)}
                    </strong>
                  </span>
                  <span>•</span>
                  <strong className="text-white font-semibold">
                    {proximoSorteo.prizeCount} premio(s) configurado(s)
                  </strong>
                </p>
              </div>
            </div>

            <div className="flex items-center gap-3 shrink-0">
              <Link to={`/sorteo/sorteos/${proximoSorteo.eventId}`}>
                <Button
                  variant="outline"
                  className="border-blue-300/40 bg-white/10 hover:bg-white/20 text-white font-bold text-xs sm:text-sm cursor-pointer backdrop-blur-xs"
                >
                  Ver Detalles
                </Button>
              </Link>

              <Button
                onClick={() => setSorteoParaEjecutar(proximoSorteo)}
                disabled={!canDraw.allowed}
                title={canDraw.title}
                className="bg-blue-600 hover:bg-blue-700 text-white font-black text-xs sm:text-sm px-5 py-2.5 shadow-md shadow-blue-600/25 cursor-pointer flex items-center gap-1.5 transition-all hover:scale-[1.02] active:scale-[0.98]"
              >
                <Play className="w-4 h-4 fill-current" />
                Ejecutar en Vivo
              </Button>
            </div>
          </div>
        </div>
      )}

      {/* Stats Cards */}
      <div className="grid grid-cols-2 gap-3 sm:gap-4 lg:grid-cols-4">
        <StatCard
          label="Total Sorteos"
          value={totals?.raffles ?? 0}
          icon={Gift}
          hint={`${totals?.active ?? 0} en progreso`}
        />
        <StatCard
          label="Premios Registrados"
          value={totals?.prizes ?? 0}
          icon={Sparkles}
          hint={`${totals?.redeemed ?? 0} entregados`}
        />
        <StatCard label="Ganadores Totales" value={totals?.winners ?? 0} icon={Trophy} />
        <StatCard
          label="Participantes calificados"
          value={totals?.qualifiedParticipants ?? 0}
          icon={Users}
        />
      </div>

      {/* Sorteos recientes */}
      <div>
        {/* Tabla de Sorteos Recientes */}
        <div className="space-y-4">
          <div className="flex items-center justify-between">
            <h3 className="text-base font-bold text-slate-900 dark:text-slate-100">
              Sorteos Recientes
            </h3>
            <FeatureGate featureKey="sorteo.raffles">
              <Link
                to="/sorteo/sorteos"
                className="text-xs font-bold text-blue-600 dark:text-blue-400 flex items-center gap-1 hover:underline"
              >
                Ver todos ({data?.total ?? 0})
                <ArrowRight className="w-3.5 h-3.5" />
              </Link>
            </FeatureGate>
          </div>

          <div className="space-y-3">
            {sorteosRecientes.length === 0 ? (
              <Card className="p-8 text-center text-slate-500 bg-white dark:bg-card">
                <Gift className="w-10 h-10 mx-auto mb-2 text-slate-300" />
                <p>No hay sorteos configurados actualmente.</p>
              </Card>
            ) : (
              sorteosRecientes.map((sorteo) => {
                return (
                  <Card
                    key={sorteo.raffleId}
                    className="p-4 flex items-center justify-between hover:border-slate-300 dark:hover:border-slate-700 transition-colors bg-white dark:bg-card border border-slate-200 dark:border-slate-800"
                  >
                    <div className="flex items-center gap-3">
                      <div className="w-10 h-10 rounded-lg bg-slate-100 dark:bg-slate-800 text-blue-600 dark:text-blue-400 flex items-center justify-center shrink-0 border border-slate-200 dark:border-slate-700">
                        <Gift className="w-5 h-5" />
                      </div>
                      <div>
                        <h4 className="font-bold text-sm text-slate-900 dark:text-slate-100">
                          {sorteo.eventName}
                        </h4>
                        <p className="text-xs text-slate-500 dark:text-slate-400">
                          {formatDate(sorteo.eventStartDate)} • {sorteo.prizeCount} premio(s) •{' '}
                          {sorteo.drawnCount} ganador(es)
                        </p>
                      </div>
                    </div>

                    <div className="flex items-center gap-3">
                      <EstadoSorteoBadge estado={sorteo.state} />
                      <Link to={`/sorteo/sorteos/${sorteo.eventId}`}>
                        <Button variant="ghost" size="sm" className="font-semibold cursor-pointer">
                          Detalles
                        </Button>
                      </Link>
                    </div>
                  </Card>
                )
              })
            )}
          </div>
        </div>
      </div>

      {/* Modal Nuevo Sorteo */}
      {showNuevoModal && (
        <NuevoSorteoModal
          onClose={() => setShowNuevoModal(false)}
          onCreated={(eventId) => navigate(`/sorteo/sorteos/${eventId}`)}
        />
      )}

      {/* Modal Iniciar Sorteo desde el Dashboard */}
      {sorteoParaEjecutar && (
        <IniciarSorteoModal
          eventId={sorteoParaEjecutar.eventId}
          subtitulo={sorteoParaEjecutar.eventName}
          textoBotonAccion="Iniciar Sorteo en Vivo"
          onClose={() => setSorteoParaEjecutar(null)}
          onLaunch={(modoPantalla) => {
            const targetUrl = `/sorteo/sorteos/${sorteoParaEjecutar.eventId}/ejecutar`
            setSorteoParaEjecutar(null)
            if (modoPantalla === 'proyector') {
              window.open(targetUrl, '_blank', 'noopener,noreferrer,width=1366,height=768')
            } else {
              navigate(targetUrl)
            }
          }}
        />
      )}
    </div>
  )
}
