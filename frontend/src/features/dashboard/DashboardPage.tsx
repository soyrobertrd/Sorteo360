import { Link } from 'react-router-dom'
import { DataTable } from '@/components/ui/DataTable'
import { Clock, CalendarDays, CheckCircle2, Ban, ArrowRight, Plus } from 'lucide-react'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { StatCard } from '@/components/ui/StatCard'
import { Tooltip } from '@/components/ui/Tooltip'
import { WelcomeHeader } from '@/components/ui/WelcomeHeader'
import { useEventosList } from '@/features/eventos/hooks'
import { Spinner, ErrorNote } from '@/components/ui/Feedback'
import { formatDateRange } from '@/lib/format'
import { eventoTypeLabel } from '@/lib/labels'
import { EventoFormModal } from '@/features/eventos/components/EventoFormModal'
import { useState } from 'react'
import { useCreateEvento } from '@/features/eventos/hooks'
import { useNavigate } from 'react-router-dom'
import { api } from '@/api'
import { useCan } from '@/auth/AuthProvider'
import { ROLES } from '@/auth/roles'
import { FeatureGate } from '@/features/admin/FeatureGate'

export function DashboardPage() {
  const navigate = useNavigate()
  const canCrear = useCan(ROLES.eventsWrite)
  const [showCreateModal, setShowCreateModal] = useState(false)
  const createEvento = useCreateEvento()

  const { data, isLoading, error } = useEventosList({ page: 1, pageSize: 100 })

  if (isLoading) return <Spinner />
  if (error) return <ErrorNote error={error} />

  const eventos = data?.items ?? []
  const enPlanificacion = eventos.filter((e) => e.status === 'planning')
  const enProgreso = eventos.filter((e) => e.status === 'in_progress')
  const cancelados = eventos.filter((e) => e.status === 'cancelled')

  const enUnaSemana = new Date()
  enUnaSemana.setDate(enUnaSemana.getDate() + 7)
  const porCerrarEstaSemana = eventos.filter((e) => {
    if (e.status !== 'in_progress') return false
    const fin = new Date(e.endDate)
    return fin >= new Date() && fin <= enUnaSemana
  })

  const stats = [
    {
      label: 'En planificación',
      value: String(enPlanificacion.length),
      icon: Clock,
      color: 'amber' as const,
      to: '/eventos/todos?estado=planning',
    },
    {
      label: 'En progreso',
      value: String(enProgreso.length),
      icon: CalendarDays,
      color: 'blue' as const,
      to: '/eventos/todos?estado=in_progress',
    },
    {
      label: 'Próximos a cerrar (7 días)',
      value: String(porCerrarEstaSemana.length),
      icon: CheckCircle2,
      color: 'teal' as const,
      to: '/eventos/todos?estado=in_progress',
    },
    {
      label: 'Eventos cancelados',
      value: String(cancelados.length),
      icon: Ban,
      color: 'rose' as const,
      to: '/eventos/todos?estado=cancelled',
    },
  ]

  const activos = eventos
    .filter((e) => e.status === 'planning' || e.status === 'in_progress')
    .sort((a, b) => a.startDate.localeCompare(b.startDate))

  const proximosEventos = activos.slice(0, 3)

  return (
    <div className="space-y-6">
      {/* Encabezado del Módulo y Botón de Acción Nuevo Evento */}
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <WelcomeHeader subtitle="Módulo Eventos • Eventos corporativos, celebraciones y actividades especiales" />
        </div>

        <Tooltip content={canCrear.allowed ? '' : canCrear.title}>
          <Button
            type="button"
            variant="default"
            size="default"
            onClick={() => setShowCreateModal(true)}
            disabled={!canCrear.allowed}
            className="mb-6 font-normal shrink-0 cursor-pointer"
          >
            <Plus className="mr-1.5 h-4 w-4" />
            Nuevo evento
          </Button>
        </Tooltip>
      </div>

      {/* Fila de 4 Tarjetas de Métricas KPI — cada una abre la lista filtrada por ese estado */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {stats.map((stat) => (
          <StatCard
            key={stat.label}
            icon={stat.icon}
            label={stat.label}
            value={stat.value}
            to={stat.to}
            color={stat.color}
            showArrow={false}
            layout="horizontal"
          />
        ))}
      </div>

      {/* Card: los próximos 3 eventos que siguen en curso o en planificación */}
      <Card className="bg-white p-6 rounded-2xl border border-slate-200/80 shadow-2xs dark:bg-slate-900 dark:border-slate-800">
        <div className="mb-4 flex items-center justify-between">
          <h2 className="font-sans text-base font-bold text-slate-900 dark:text-white">
            Próximos eventos
          </h2>
          <FeatureGate featureKey="events.events">
            <Link
              to="/eventos/todos"
              className="inline-flex items-center gap-1 font-sans text-xs font-bold text-blue-600 hover:underline dark:text-blue-400"
            >
              <span>Ver todos</span>
              <ArrowRight size={13} />
            </Link>
          </FeatureGate>
        </div>

        <DataTable
          dense
          rows={proximosEventos}
          getRowKey={(evento) => evento.id}
          empty="Sin eventos próximos."
          columns={[
            {
              key: 'evento',
              header: 'EVENTO',
              cell: (evento) => (
                <>
                  <Link
                    to={`/eventos/${evento.id}`}
                    className="font-sans text-sm font-bold text-slate-900 group-hover:text-blue-600 dark:text-white dark:group-hover:text-blue-400"
                  >
                    {evento.name}
                  </Link>
                  <p className="font-body text-xs text-slate-500 dark:text-slate-400">
                    {eventoTypeLabel[evento.type]}
                  </p>
                </>
              ),
            },
            {
              key: 'periodo',
              header: 'PERÍODO',
              cellClassName:
                'whitespace-nowrap font-body text-xs text-slate-600 dark:text-slate-300',
              cell: (evento) => formatDateRange(evento.startDate, evento.endDate),
            },
            {
              key: 'estado',
              header: 'ESTADO',
              align: 'right',
              cellClassName: 'whitespace-nowrap',
              cell: (evento) => (
                <span
                  className={`inline-block rounded-md border px-2.5 py-1 font-sans text-[10px] font-extrabold tracking-wider uppercase ${
                    evento.status === 'in_progress'
                      ? 'bg-blue-100 text-blue-800 border-blue-200 dark:bg-blue-950/80 dark:text-blue-300'
                      : 'bg-amber-100 text-amber-800 border-amber-200 dark:bg-amber-950/80 dark:text-amber-300'
                  }`}
                >
                  {evento.status === 'in_progress'
                    ? 'EN PROGRESO'
                    : evento.status === 'planning'
                      ? 'PLANIFICACIÓN'
                      : evento.status === 'closed'
                        ? 'CERRADO'
                        : 'CANCELADO'}
                </span>
              ),
            },
          ]}
        />
      </Card>

      {/* Modal para Crear Evento */}
      {showCreateModal && (
        <EventoFormModal
          onClose={() => {
            setShowCreateModal(false)
            createEvento.reset()
          }}
          onCreate={async (body, files) => {
            try {
              const id = await createEvento.mutateAsync({ body })
              if (files.coverFile) {
                await api.uploadEventoImage(id, files.coverFile, 'Cover')
              }
              await Promise.all(
                files.galleryFiles.map((file) => api.uploadEventoImage(id, file, 'Gallery')),
              )
              setShowCreateModal(false)
              navigate(`/eventos/${id}`)
            } catch (err) {
              console.error(err)
            }
          }}
          pending={createEvento.isPending}
          error={createEvento.error}
        />
      )}
    </div>
  )
}
