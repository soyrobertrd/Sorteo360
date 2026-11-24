import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Search, Calendar as CalendarIcon, ArrowRight } from 'lucide-react'
import { Card } from '@/components/ui/Card'
import { Spinner, ErrorNote, EmptyState } from '@/components/ui/Feedback'
import { formatDateRange } from '@/lib/format'
import { EVENTO_TYPES, eventoTypeLabel } from '@/lib/labels'
import { coverImageFor } from './coverImages'
import { usePublicEventos } from './hooks'
import { PublicNav } from './PublicNav'

export function PublicEventosListPage() {
  const [searchTerm, setSearchTerm] = useState('')
  const [selectedType, setSelectedType] = useState('')

  // La cartelera de convocatorias ya viene recortada a lo público y vigente: un evento
  // privado, cancelado o cerrado no llega hasta acá.
  const { data, isLoading, error } = usePublicEventos()
  const eventosVigentes = data ?? []

  const filteredEventos = eventosVigentes.filter((evento) => {
    const term = searchTerm.toLowerCase()
    const matchesSearch =
      !searchTerm ||
      evento.name.toLowerCase().includes(term) ||
      evento.cities.some(
        (c) => c.name.toLowerCase().includes(term) || c.country.toLowerCase().includes(term),
      )
    const matchesType = !selectedType || evento.type === selectedType
    return matchesSearch && matchesType
  })

  if (isLoading) return <Spinner label="Cargando eventos públicos..." />
  if (error) return <ErrorNote error={error} />

  return (
    <div className="space-y-6 pb-8">
      <PublicNav />

      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8 space-y-8">
        {/* Hero Banner */}
        <div className="relative overflow-hidden rounded-3xl bg-brand-deep p-8 sm:p-14 text-white shadow-xl">
          <img
            src="https://images.unsplash.com/photo-1540575467063-178a50c2df87?auto=format&fit=crop&w=1800&q=70"
            alt=""
            className="absolute inset-0 h-full w-full object-cover opacity-25"
          />
          <div className="absolute inset-0 bg-linear-to-r from-brand-deep via-brand-deep/95 to-brand-deep/60" />
          <div className="absolute -right-12 -top-12 h-64 w-64 rounded-full bg-blue-600/20 blur-3xl" />

          <div className="relative z-10 max-w-3xl space-y-5">
            <span className="inline-block rounded-full border border-blue-400/30 bg-blue-500/20 px-3 py-1 text-[11px] font-bold uppercase tracking-wider text-blue-200">
              Cartelera
            </span>
            <h1 className="font-sans text-3xl font-extrabold tracking-tight sm:text-5xl text-white">
              Próximos eventos
            </h1>
            <p className="font-body text-sm sm:text-base leading-relaxed text-blue-100/90 max-w-2xl">
              Consulta los eventos abiertos y regístrate en los que te interesen.
            </p>
          </div>
        </div>

        {/* Barra de Búsqueda y Filtros */}
        <Card className="p-4 sm:p-6 bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
            <div className="sm:col-span-2 relative">
              <Search
                size={18}
                className="absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400"
              />
              <input
                type="text"
                placeholder="Buscar por nombre de evento o ciudad..."
                aria-label="Buscar por nombre de evento o ciudad"
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                className="w-full rounded-xl border border-slate-200 bg-slate-50 py-2.5 pl-10 pr-4 text-xs sm:text-sm text-slate-900 focus:border-blue-600 focus:bg-white focus:outline-none dark:border-slate-700 dark:bg-slate-800 dark:text-white"
              />
            </div>

            <div>
              <select
                aria-label="Tipo de evento"
                value={selectedType}
                onChange={(e) => setSelectedType(e.target.value)}
                className="w-full rounded-xl border border-slate-200 bg-slate-50 px-3.5 py-2.5 text-xs sm:text-sm text-slate-900 focus:border-blue-600 focus:bg-white focus:outline-none dark:border-slate-700 dark:bg-slate-800 dark:text-white"
              >
                <option value="">Todos los tipos de eventos</option>
                {EVENTO_TYPES.map((t) => (
                  <option key={t} value={t}>
                    {eventoTypeLabel[t]}
                  </option>
                ))}
              </select>
            </div>
          </div>
        </Card>

        {/* Lista / Grid de Eventos */}
        {filteredEventos.length === 0 ? (
          <EmptyState>No hay eventos vigentes que coincidan con la búsqueda.</EmptyState>
        ) : (
          <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
            {filteredEventos.map((evento) => {
              const registroAbierto = evento.registrationOpen

              return (
                <Card
                  key={evento.id}
                  className="group flex flex-col justify-between overflow-hidden bg-white transition-all duration-200 hover:-translate-y-1 hover:shadow-xl dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 p-0"
                >
                  <Link to={`/publico/eventos/${evento.id}`} className="block">
                    <div className="relative h-40 w-full overflow-hidden">
                      <img
                        src={coverImageFor(evento.type)}
                        alt=""
                        className="h-full w-full object-cover transition-transform duration-300 group-hover:scale-105"
                      />
                      <div className="absolute inset-0 bg-linear-to-t from-black/60 via-black/0 to-black/0" />
                      <span className="absolute left-3 top-3 rounded-md bg-white/95 px-2.5 py-1 text-[11px] font-bold text-brand-deep shadow-sm dark:bg-slate-900/90 dark:text-blue-300">
                        {eventoTypeLabel[evento.type]}
                      </span>
                      {registroAbierto && (
                        <span className="absolute right-3 top-3 inline-flex items-center gap-1.5 rounded-full bg-emerald-500/95 px-2.5 py-1 text-[10px] font-extrabold uppercase tracking-wider text-white shadow-sm">
                          <span className="h-1.5 w-1.5 rounded-full bg-white animate-pulse" />
                          Registro abierto
                        </span>
                      )}
                    </div>
                  </Link>

                  <div className="flex flex-1 flex-col justify-between p-6">
                    <div className="space-y-4">
                      <h2 className="font-sans text-lg font-bold text-slate-900 group-hover:text-blue-600 transition-colors dark:text-white dark:group-hover:text-blue-400">
                        <Link to={`/publico/eventos/${evento.id}`}>{evento.name}</Link>
                      </h2>

                      <div className="space-y-2 text-xs text-slate-600 dark:text-slate-300 font-medium">
                        <div className="flex items-center gap-2">
                          <CalendarIcon size={15} className="text-slate-400 shrink-0" />
                          <span>{formatDateRange(evento.startDate, evento.endDate)}</span>
                        </div>
                      </div>

                      <div className="rounded-xl border border-slate-100 bg-slate-50 p-3 text-[11px] text-slate-600 dark:border-slate-800 dark:bg-slate-800/50 dark:text-slate-300">
                        <p className="font-semibold text-slate-700 dark:text-slate-200">
                          {registroAbierto
                            ? 'Registro de invitados habilitado.'
                            : 'Publicación oficial. Las inscripciones están cerradas.'}
                        </p>
                      </div>
                    </div>

                    <div className="mt-6 border-t border-slate-100 pt-4 dark:border-slate-800">
                      <Link
                        to={`/publico/eventos/${evento.id}`}
                        className="inline-flex w-full items-center justify-between font-sans text-xs font-bold text-brand-deep transition-colors group-hover:text-blue-600 dark:text-white dark:group-hover:text-blue-400"
                      >
                        <span>Ver detalle del evento</span>
                        <ArrowRight
                          size={15}
                          className="transition-transform group-hover:translate-x-1"
                        />
                      </Link>
                    </div>
                  </div>
                </Card>
              )
            })}
          </div>
        )}
      </div>
    </div>
  )
}
