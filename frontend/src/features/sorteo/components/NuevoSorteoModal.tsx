import { useState, useMemo, type FormEvent } from 'react'
import { Calendar, Sparkles, Link2 } from 'lucide-react'
import { useQueryClient } from '@tanstack/react-query'
import { api } from '@/api'
import { toUpdateRaffleRequest } from '../raffleRequest'
import { Modal } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { Field, TextArea, TextInput } from '@/components/ui/Field'
import { ErrorNote } from '@/components/ui/Feedback'
import { useEventosList } from '@/features/eventos/hooks'
import { useFeatureSwitches } from '@/features/admin/hooks'
import { setRaffleBranding, useBranding } from '@/lib/branding'
import { formatDate } from '@/lib/format'
import { raffleKeys, useAllRaffles, useCreateRaffle, useRaffleList } from '../hooks'
import { EmpresaAnfitriona } from './EmpresaAnfitriona'

interface NuevoSorteoModalProps {
  onClose: () => void
  /** El sorteo se direcciona por su evento, así que eso es lo que devuelve. */
  onCreated: (eventId: string) => void
}

export function NuevoSorteoModal({ onClose, onCreated }: NuevoSorteoModalProps) {
  const queryClient = useQueryClient()
  const globalBrand = useBranding()
  // Vincular un sorteo a un evento existente solo tiene sentido con el módulo de eventos encendido.
  const { isEnabled } = useFeatureSwitches()
  const conEventos = isEnabled('events') && isEnabled('events.events')
  const { data: eventosData } = useEventosList({ page: 1, pageSize: 100 }, conEventos)
  const { data: rafflesData } = useRaffleList()
  const createRaffle = useCreateRaffle()
  const { data: todosLosSorteos } = useAllRaffles()
  // Sorteos vinculados: el nuevo no repite ganadores con el elegido ni con los de su grupo.
  const [vincularCon, setVincularCon] = useState('')

  const eventosDisponibles = useMemo(() => {
    const conSorteo = new Set((rafflesData?.items ?? []).map((r) => r.eventId))
    return (eventosData?.items ?? []).filter(
      (e) => e.status !== 'closed' && e.status !== 'cancelled' && !conSorteo.has(e.id),
    )
  }, [eventosData, rafflesData])

  const [modoElegido, setMode] = useState<'nuevo' | 'existente'>('nuevo')
  const mode = conEventos ? modoElegido : 'nuevo'
  const [nombreEvento, setNombreEvento] = useState('')
  const [companyName, setCompanyName] = useState(globalBrand.organizationName)
  const [companyLogoUrl, setCompanyLogoUrl] = useState('')
  const [fecha, setFecha] = useState(() => new Date().toISOString().slice(0, 10))
  const [ciudad, setCiudad] = useState('Santo Domingo')
  const [pais, setPais] = useState('República Dominicana')
  const [descripcion, setDescripcion] = useState('')
  const [eventoIdElegido, setEventoIdElegido] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [submitError, setSubmitError] = useState<unknown>(null)

  const eventoId = eventoIdElegido || eventosDisponibles[0]?.id || ''
  const selectedEv = eventosDisponibles.find((e) => e.id === eventoId)

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setSubmitError(null)

    if (mode === 'existente') {
      if (!eventoId) return
      if (companyName.trim() || companyLogoUrl.trim()) {
        setRaffleBranding(eventoId, {
          companyName: companyName.trim() || undefined,
          logoUrl: companyLogoUrl.trim() || undefined,
        })
      }
      setSubmitting(true)
      try {
        await createRaffle.mutateAsync(eventoId)
        if (vincularCon) {
          const creado = await api.getRaffle(eventoId)
          await api.updateRaffle(
            eventoId,
            toUpdateRaffleRequest(creado, { linkedEventId: vincularCon }),
          )
        }
        onCreated(eventoId)
      } catch (err) {
        setSubmitError(err)
      } finally {
        setSubmitting(false)
      }
      return
    }

    if (!nombreEvento.trim()) {
      setSubmitError({ message: 'El nombre del sorteo o celebración es obligatorio.' })
      return
    }

    setSubmitting(true)
    try {
      const createdEventId = await api.createEvento({
        name: nombreEvento.trim(),
        type: 'Corporate',
        startDate: fecha,
        endDate: fecha,
        city: ciudad.trim() || 'Santo Domingo',
        country: pais.trim() || 'República Dominicana',
        timeZone: 'America/Santo_Domingo',
        language: 'es',
        visibility: 'public',
        invitedCount: 250,
        estimatedBudget: null,
        description: descripcion.trim() || null,
        seatingLayout: null,
      })

      // Activa automáticamente el evento para permitir check-in y ejecución directa en el sorteo.
      await api.iniciarEvento(createdEventId).catch(() => {})
      await api.createRaffle(createdEventId)
      // Un sorteo creado solo, sin evento, no tiene puerta: el padrón llega por archivo, API o
      // webhook y todos participan (A35).
      const creado = await api.getRaffle(createdEventId)
      await api.updateRaffle(
        createdEventId,
        toUpdateRaffleRequest(creado, {
          requiresCheckIn: false,
          linkedEventId: vincularCon || null,
        }),
      )

      setRaffleBranding(createdEventId, {
        companyName: companyName.trim() || undefined,
        logoUrl: companyLogoUrl.trim() || undefined,
      })

      await Promise.all([
        queryClient.invalidateQueries({ queryKey: raffleKeys.all }),
        queryClient.invalidateQueries({ queryKey: ['eventos'] }),
      ])
      onCreated(createdEventId)
    } catch (err) {
      setSubmitError(err)
    } finally {
      setSubmitting(false)
    }
  }

  const busy = submitting || createRaffle.isPending
  const errorToShow = submitError ?? createRaffle.error

  return (
    <Modal title="Nuevo Sorteo / Rifa Corporativa" onClose={onClose} wide>
      <form onSubmit={handleSubmit} className="space-y-4 text-xs">
        {conEventos && (
          <div className="flex gap-1.5 rounded-xl bg-slate-100 p-1 dark:bg-slate-800">
            <button
              type="button"
              onClick={() => setMode('nuevo')}
              className={`flex flex-1 items-center justify-center gap-1.5 rounded-lg px-3 py-2 font-bold transition-colors cursor-pointer ${
                mode === 'nuevo'
                  ? 'bg-white text-indigo-700 shadow-2xs dark:bg-slate-900 dark:text-indigo-400'
                  : 'text-slate-600 hover:text-slate-900 dark:text-slate-300'
              }`}
            >
              <Sparkles size={14} /> Crear nuevo evento y sorteo
            </button>
            <button
              type="button"
              onClick={() => setMode('existente')}
              className={`flex flex-1 items-center justify-center gap-1.5 rounded-lg px-3 py-2 font-bold transition-colors cursor-pointer ${
                mode === 'existente'
                  ? 'bg-white text-indigo-700 shadow-2xs dark:bg-slate-900 dark:text-indigo-400'
                  : 'text-slate-600 hover:text-slate-900 dark:text-slate-300'
              }`}
            >
              <Link2 size={14} /> Vincular a evento existente ({eventosDisponibles.length})
            </button>
          </div>
        )}

        {mode === 'nuevo' ? (
          <div className="space-y-3.5">
            <Field label="Nombre del Sorteo / Celebración" required>
              <TextInput
                value={nombreEvento}
                onChange={(e) => setNombreEvento(e.target.value)}
                placeholder="Ej. Fiesta de Navidad Empleados 2026"
                required
              />
            </Field>

            <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
              <Field label="Fecha del Evento" required>
                <TextInput
                  type="date"
                  value={fecha}
                  onChange={(e) => setFecha(e.target.value)}
                  required
                />
              </Field>
              <Field label="Ciudad / Salón">
                <TextInput
                  value={ciudad}
                  onChange={(e) => setCiudad(e.target.value)}
                  placeholder="Santo Domingo"
                />
              </Field>
              <Field label="País">
                <TextInput
                  value={pais}
                  onChange={(e) => setPais(e.target.value)}
                  placeholder="República Dominicana"
                />
              </Field>
            </div>

            <Field label="Descripción (Opcional)">
              <TextArea
                rows={2}
                value={descripcion}
                onChange={(e) => setDescripcion(e.target.value)}
                placeholder="Notas sobre la dinámica o premios del evento..."
              />
            </Field>
          </div>
        ) : (
          <div className="space-y-2 rounded-xl border border-slate-200 bg-slate-50 p-3.5 dark:border-slate-700 dark:bg-slate-800/60">
            <label className="block text-xs font-bold text-slate-800 dark:text-slate-200">
              Seleccionar Evento Disponible
            </label>
            {eventosDisponibles.length === 0 ? (
              <p className="text-xs text-slate-500">
                No hay eventos disponibles sin sorteo. Puedes crear uno nuevo directamente en la
                otra pestaña.
              </p>
            ) : (
              <select
                value={eventoId}
                onChange={(e) => setEventoIdElegido(e.target.value)}
                className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 transition-colors focus:border-blue-600 focus:outline-none dark:border-slate-700 dark:bg-slate-900 dark:text-white cursor-pointer"
              >
                {eventosDisponibles.map((ev) => (
                  <option key={ev.id} value={ev.id}>
                    {ev.name}
                  </option>
                ))}
              </select>
            )}
            {selectedEv && (
              <div className="flex items-center gap-2 rounded-lg border border-blue-100 bg-blue-50/70 px-3 py-2 text-xs text-blue-900 dark:border-blue-900/50 dark:bg-blue-950/40 dark:text-blue-300">
                <Calendar className="h-3.5 w-3.5 shrink-0" />
                <span>
                  Fecha: <strong>{formatDate(selectedEv.startDate)}</strong>
                </span>
              </div>
            )}
          </div>
        )}

        <Field
          label="Vincular con otro sorteo (opcional)"
          hint="Los sorteos vinculados no repiten ganadores: quien gane en uno no entra a los demás."
        >
          <select
            aria-label="Vincular con otro sorteo"
            value={vincularCon}
            onChange={(e) => setVincularCon(e.target.value)}
            className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 dark:border-slate-700 dark:bg-slate-900 dark:text-white"
          >
            <option value="">Sin vincular</option>
            {(todosLosSorteos ?? []).map((s) => (
              <option key={s.eventId} value={s.eventId}>
                {s.eventName} · {formatDate(s.eventStartDate)}
              </option>
            ))}
          </select>
        </Field>

        <EmpresaAnfitriona
          nombre={companyName}
          logo={companyLogoUrl}
          onNombre={setCompanyName}
          onLogo={setCompanyLogoUrl}
        />

        {errorToShow != null && <ErrorNote error={errorToShow} />}

        <div className="flex justify-end gap-2 border-t border-slate-100 pt-3 dark:border-slate-800">
          <Button type="button" variant="outline" onClick={onClose} className="cursor-pointer">
            Cancelar
          </Button>
          <Button
            type="submit"
            disabled={busy || (mode === 'existente' && !eventoId)}
            className="bg-brand-deep hover:bg-brand-deep/90 text-white font-bold cursor-pointer"
          >
            {busy ? 'Creando sorteo...' : 'Crear Sorteo'}
          </Button>
        </div>
      </form>
    </Modal>
  )
}
