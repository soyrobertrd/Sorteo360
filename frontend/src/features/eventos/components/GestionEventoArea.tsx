import { Link } from 'react-router-dom'
import { Mail, ExternalLink, Eye, UserCheck, MailX, UserX, Globe, Loader2 } from 'lucide-react'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { CopyButton } from '@/components/ui/CopyButton'
import { ErrorNote } from '@/components/ui/Feedback'
import { useConvocatoria } from '../useConvocatoria'
import { useEventTracking } from '@/features/convocatorias/tracking'
import { CerrarConvocatoriaDialog } from './CerrarConvocatoriaDialog'
import type { EventoDetail } from '@/api/types'
import { TextInput } from '@/components/ui/Field'

export function GestionEventoArea({ evento }: { evento: EventoDetail }) {
  const convocatoria = useConvocatoria(evento)
  const { publicUrl, iniciada, registroAbierto, abiertaSinIniciar, guardando } = convocatoria
  // Solo lo que el servicio sabe. Lo que todavía no sirve (aperturas, bajas) va con guion:
  // un número inventado al lado de uno real no se distingue.
  const { data: resumen } = useEventTracking(evento.id)
  const cifra = (n: number | undefined) => (n == null ? '—' : n.toLocaleString())
  const metrics = {
    enviados: cifra(resumen?.people.invited),
    registrados: cifra(resumen?.registrations.total),
    abiertos: '—',
    rechazados: cifra(resumen?.people.failed),
    bajas: '—',
  }

  return (
    <Card className="bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 rounded-2xl shadow-2xs overflow-hidden">
      <div className="p-6 space-y-6">
        {/* BLOQUE 1: Link Público del Evento (Registro de Invitados) */}
        <div className="rounded-2xl border border-slate-200/80 bg-slate-50/50 p-5 dark:border-slate-800 dark:bg-slate-800/30 space-y-4">
          <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200/60 pb-3 dark:border-slate-800">
            <div className="flex items-center gap-2">
              <Globe size={18} className="text-brand-deep dark:text-blue-400" />
              <h3 className="font-sans text-sm font-extrabold text-brand-deep dark:text-white uppercase tracking-wider">
                Link público del evento (Registro de Invitados)
              </h3>
            </div>

            <Button
              type="button"
              onClick={convocatoria.toggle}
              disabled={convocatoria.disabled}
              title={convocatoria.title}
              className={`font-bold text-xs sm:text-sm px-4 py-2.5 rounded-xl shadow-2xs transition-colors cursor-pointer ${
                registroAbierto
                  ? 'bg-rose-700 hover:bg-rose-800 text-white'
                  : 'bg-emerald-600 hover:bg-emerald-700 text-white'
              }`}
            >
              {guardando && <Loader2 size={15} className="mr-1.5 animate-spin" />}
              {registroAbierto ? 'Cerrar convocatoria' : 'Abrir convocatoria'}
            </Button>
          </div>

          <div className="flex flex-wrap items-center justify-between gap-4">
            <div className="flex flex-1 flex-wrap items-center gap-2 min-w-70">
              <TextInput
                type="text"
                readOnly
                aria-label="Enlace público del evento"
                value={publicUrl}
                className="flex-1 font-mono"
              />
              <CopyButton value={publicUrl} label="Copiar" copiedLabel="¡Copiado!" />
              <a
                href={publicUrl}
                target="_blank"
                rel="noreferrer"
                className="inline-flex items-center gap-1.5 rounded-xl border border-slate-200 bg-white px-3.5 py-2 text-xs font-semibold text-slate-700 transition-colors hover:bg-slate-50 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-200"
              >
                <span>Ver formulario de registro</span>
                <ExternalLink size={13} />
              </a>
            </div>
          </div>

          {/* La marca del evento se guarda aparte, y ese guardado valida la OPT contra el CRM:
              puede fallar, y el botón que se queda quieto no lo explica. */}
          {convocatoria.errors.map((error, i) => (
            <ErrorNote key={i} error={error} />
          ))}

          <p className="text-[11px] italic text-slate-500 dark:text-slate-400">
            {registroAbierto
              ? 'La convocatoria está abierta: el formulario de registro del evento acepta inscripciones. Haz clic en "Cerrar convocatoria" cuando ya no se acepten más registros.'
              : abiertaSinIniciar
                ? 'La convocatoria no se ha abierto, pero el formulario público ya acepta inscripciones: el registro de este evento quedó abierto al crearlo.'
                : 'Haz clic en "Abrir convocatoria" para habilitar el formulario de registro del evento.'}
          </p>
        </div>

        {/* BLOQUE 2: Resumen de Campaña de Convocatoria. No existe hasta que se inicia. */}
        {iniciada && (
          <div className="rounded-2xl border border-slate-200/80 bg-slate-50/50 p-5 dark:border-slate-800 dark:bg-slate-800/30 space-y-5">
            <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200/60 pb-3.5 dark:border-slate-800">
              <div className="flex items-center gap-2.5">
                <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-brand-deep text-white shadow-2xs">
                  <Mail size={20} />
                </div>
                <div>
                  <h3 className="font-sans text-base font-extrabold text-brand-deep dark:text-white">
                    Resumen de campaña de convocatoria
                  </h3>
                  <p className="text-xs text-slate-500 dark:text-slate-400">
                    Métricas de difusión y audiencia para <strong>{evento.name}</strong>
                  </p>
                </div>
              </div>
            </div>

            <>
              {/* Grid de 5 Métricas con Paleta Corporativa Limpia */}
              <div className="grid grid-cols-2 gap-3 sm:grid-cols-5">
                <div className="rounded-xl bg-white p-4 border border-slate-200/80 dark:bg-slate-800 dark:border-slate-700 shadow-2xs relative overflow-hidden">
                  <div className="flex items-center gap-1.5 text-slate-400 mb-1.5">
                    <Mail size={15} />
                    <span className="text-[11px] font-bold uppercase tracking-wider text-slate-400">
                      Enviados
                    </span>
                  </div>
                  <p className="font-sans text-2xl font-black text-brand-deep dark:text-white">
                    {metrics.enviados}
                  </p>
                  <div className="absolute bottom-0 left-0 right-0 h-1 bg-brand-deep" />
                </div>

                <div className="rounded-xl bg-white p-4 border border-slate-200/80 dark:bg-slate-800 dark:border-slate-700 shadow-2xs relative overflow-hidden">
                  <div className="flex items-center gap-1.5 text-slate-400 mb-1.5">
                    <UserCheck size={15} />
                    <span className="text-[11px] font-bold uppercase tracking-wider text-slate-400">
                      Registrados
                    </span>
                  </div>
                  <p className="font-sans text-2xl font-black text-brand-deep dark:text-white">
                    {metrics.registrados}
                  </p>
                  <div className="absolute bottom-0 left-0 right-0 h-1 bg-brand-deep" />
                </div>

                <div className="rounded-xl bg-white p-4 border border-slate-200/80 dark:bg-slate-800 dark:border-slate-700 shadow-2xs relative overflow-hidden">
                  <div className="flex items-center gap-1.5 text-slate-400 mb-1.5">
                    <Eye size={15} />
                    <span className="text-[11px] font-bold uppercase tracking-wider text-slate-400">
                      Abiertos
                    </span>
                  </div>
                  <p className="font-sans text-2xl font-black text-brand-deep dark:text-white">
                    {metrics.abiertos}
                  </p>
                  <div className="absolute bottom-0 left-0 right-0 h-1 bg-brand-deep" />
                </div>

                <div className="rounded-xl bg-white p-4 border border-slate-200/80 dark:bg-slate-800 dark:border-slate-700 shadow-2xs relative overflow-hidden">
                  <div className="flex items-center gap-1.5 text-slate-400 mb-1.5">
                    <MailX size={15} />
                    <span className="text-[11px] font-bold uppercase tracking-wider text-slate-400">
                      Rechazados
                    </span>
                  </div>
                  <p className="font-sans text-2xl font-black text-brand-deep dark:text-white">
                    {metrics.rechazados}
                  </p>
                  <div className="absolute bottom-0 left-0 right-0 h-1 bg-brand-deep" />
                </div>

                <div className="col-span-2 sm:col-span-1 rounded-xl bg-white p-4 border border-slate-200/80 dark:bg-slate-800 dark:border-slate-700 shadow-2xs relative overflow-hidden">
                  <div className="flex items-center gap-1.5 text-slate-400 mb-1.5">
                    <UserX size={15} />
                    <span className="text-[11px] font-bold uppercase tracking-wider text-slate-400">
                      Bajas
                    </span>
                  </div>
                  <p className="font-sans text-2xl font-black text-brand-deep dark:text-white">
                    {metrics.bajas}
                  </p>
                  <div className="absolute bottom-0 left-0 right-0 h-1 bg-brand-deep" />
                </div>
              </div>

              <div className="flex justify-end pt-1">
                <Link
                  to={`/convocatorias/eventos/${evento.id}`}
                  className="inline-flex items-center gap-2 rounded-xl bg-brand-deep hover:bg-brand-deep/90 text-white font-bold text-xs sm:text-sm px-4 py-2.5 shadow-2xs transition-colors cursor-pointer"
                >
                  <span>Gestionar campañas</span>
                  <ExternalLink size={15} />
                </Link>
              </div>
            </>
          </div>
        )}
      </div>

      <CerrarConvocatoriaDialog convocatoria={convocatoria} eventoNombre={evento.name} />
    </Card>
  )
}
