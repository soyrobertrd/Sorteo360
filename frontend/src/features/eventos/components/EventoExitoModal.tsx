import { CheckCircle2, ArrowRight, Calendar, MapPin, Sparkles } from 'lucide-react'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { Badge } from '@/components/ui/Badge'
import { formatDate } from '@/lib/format'

interface EventoExitoModalProps {
  eventoNombre: string
  eventoTipo?: string
  ciudad?: string
  pais?: string
  fecha?: string
  createdId?: string
  isEdit?: boolean
  onClose: () => void
  onNavigateToDetail?: (id: string) => void
}

export function EventoExitoModal({
  eventoNombre,
  eventoTipo = 'Corporate',
  ciudad = 'Nueva York',
  pais = 'Estados Unidos',
  fecha,
  createdId,
  isEdit = false,
  onClose,
  onNavigateToDetail,
}: EventoExitoModalProps) {
  const formattedFecha = fecha ? formatDate(fecha) : 'Próximamente'

  return (
    <Modal onClose={onClose}>
      <div className="space-y-5 p-2 sm:p-4 text-center">
        {/* Ícono animado de éxito con sombra y degradado */}
        <div className="relative mx-auto flex h-16 w-16 items-center justify-center rounded-2xl bg-linear-to-tr from-emerald-600 to-teal-500 text-white shadow-lg shadow-emerald-500/20 dark:shadow-emerald-950/40">
          <CheckCircle2 size={36} className="stroke-[2.2]" />
          <div className="absolute -top-1 -right-1 flex h-5 w-5 items-center justify-center rounded-full bg-amber-400 text-amber-950 font-black text-[10px] shadow-xs">
            <Sparkles size={12} />
          </div>
        </div>

        {/* Encabezado y Mensaje Principal */}
        <div className="space-y-1.5">
          <h3 className="font-sans text-xl font-extrabold text-slate-900 dark:text-white">
            {isEdit ? 'Evento actualizado exitosamente' : 'Evento publicado exitosamente'}
          </h3>
          <p className="text-xs text-slate-600 dark:text-slate-400 max-w-sm mx-auto leading-relaxed">
            {isEdit
              ? 'Los cambios del evento han sido guardados correctamente en la plataforma.'
              : 'Se ha creado el perfil inicial del evento en el sistema y ya puedes cargar sus participantes y preparar el sorteo.'}
          </p>
        </div>

        {/* Ficha resumida del evento creado con medidas optimizadas */}
        <div className="mx-auto max-w-sm rounded-2xl border border-slate-200/80 bg-slate-50/70 p-4 text-left space-y-2.5 dark:border-slate-800 dark:bg-slate-900/60 shadow-2xs">
          <div className="flex items-center justify-between gap-2 border-b border-slate-200/60 pb-2 dark:border-slate-800">
            <Badge className="bg-emerald-100 text-brand-deep dark:bg-emerald-950 dark:text-emerald-300 font-extrabold text-[10px] uppercase tracking-wider">
              {eventoTipo}
            </Badge>
            <span className="text-[10px] font-extrabold text-slate-400 font-mono">
              ID: {createdId ? createdId.slice(0, 8) : 'EV-2026'}
            </span>
          </div>

          <h4 className="text-xs font-black text-slate-900 dark:text-white line-clamp-2 leading-tight">
            {eventoNombre}
          </h4>

          <div className="grid grid-cols-2 gap-2 pt-1 text-[11px] text-slate-600 dark:text-slate-400 font-medium">
            <div className="flex items-center gap-1.5 truncate">
              <MapPin size={13} className="text-slate-400 shrink-0" />
              <span className="truncate">
                {ciudad}, {pais}
              </span>
            </div>
            <div className="flex items-center gap-1.5 truncate">
              <Calendar size={13} className="text-slate-400 shrink-0" />
              <span className="truncate">{formattedFecha}</span>
            </div>
          </div>
        </div>

        {/* Botones de acción limpia con jerarquía primaria y secundaria */}
        <div className="pt-2 flex flex-col sm:flex-row items-center justify-center gap-2.5">
          {createdId && onNavigateToDetail ? (
            <Button
              type="button"
              onClick={() => onNavigateToDetail(createdId)}
              className="w-full sm:w-auto bg-brand-deep text-white hover:bg-brand-deep/90 font-bold text-xs px-5 py-2.5 rounded-xl shadow-xs cursor-pointer flex items-center justify-center gap-1.5"
            >
              <span>Ver perfil del evento</span>
              <ArrowRight size={14} />
            </Button>
          ) : null}

          <Button
            type="button"
            variant="outline"
            onClick={onClose}
            className="w-full sm:w-auto font-bold text-xs px-5 py-2.5 rounded-xl cursor-pointer"
          >
            Volver a la lista
          </Button>
        </div>
      </div>
    </Modal>
  )
}
