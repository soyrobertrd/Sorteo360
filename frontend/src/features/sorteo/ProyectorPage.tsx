import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { Gift, Trophy, UserCheck } from 'lucide-react'
import { useBranding } from '@/lib/branding'
import { cssUrl } from '@/lib/safeUrl'
import { RuletaAnimacion } from './components/RuletaAnimacion'
import { TableroGanadores } from './components/TableroGanadores'
import { fireBurst, fireConfetti } from './confetti'
import { abrirCanalProyector, type MensajeProyector, type SenalProyector } from './proyeccion'
import { FotoPremio } from './components/FotoPremio'

/**
 * La pantalla del salón: solo lo que ve el público, sin un botón. La maneja la consola del
 * operador por `BroadcastChannel` (ver `proyeccion.ts`); se abre desde "Modo proyector" y se
 * arrastra a la segunda pantalla.
 */
export function ProyectorPage() {
  const { eventId = '' } = useParams<{ eventId: string }>()
  const branding = useBranding(eventId)
  const [estado, setEstado] = useState<MensajeProyector>({
    tipo: 'espera',
    premio: null,
    mostrarPremio: true,
  })
  const [presente, setPresente] = useState(false)
  const fondo = (() => {
    try {
      return localStorage.getItem('sorteo360_bg') ?? ''
    } catch {
      return ''
    }
  })()

  useEffect(() => {
    const canal = abrirCanalProyector(eventId)
    if (!canal) return
    canal.onmessage = (e: MessageEvent<MensajeProyector | SenalProyector>) => {
      const m = e.data
      // Una consola que se abre pregunta si hay proyector: se le contesta para que sea el control.
      if (m.tipo === 'ping') {
        canal.postMessage({ tipo: 'hola' } satisfies SenalProyector)
        return
      }
      if (m.tipo === 'hola' || m.tipo === 'adios') return
      if (m.tipo === 'presencia') {
        setPresente(m.confirmada)
        if (m.confirmada) fireBurst()
        return
      }
      if (m.tipo === 'ganador') setPresente(false)
      setEstado(m)
    }
    // Avisa a la consola que hay un proyector abierto, para que repita el estado actual.
    canal.postMessage({ tipo: 'hola' } satisfies SenalProyector)
    // Al cerrar la ventana la consola vuelve a mostrar la pantalla completa.
    const adios = () => canal.postMessage({ tipo: 'adios' } satisfies SenalProyector)
    window.addEventListener('pagehide', adios)
    return () => {
      window.removeEventListener('pagehide', adios)
      adios()
      canal.close()
    }
  }, [eventId])

  if (estado.tipo === 'tablero') {
    // De borde a borde: la franja arriba y el resto en blanco, sin bandas a los lados.
    return (
      <div className="fixed inset-0 select-none overflow-hidden bg-white">
        <TableroGanadores estado={estado.tablero} logoUrl={branding.logoDarkUrl} llenar />
      </div>
    )
  }

  const premio = estado.tipo === 'presencia' ? null : estado.premio
  const mostrarPremio =
    estado.tipo === 'ganador' || (estado.tipo !== 'presencia' && estado.mostrarPremio)

  return (
    <div className="fixed inset-0 flex select-none flex-col items-center justify-center overflow-hidden bg-brand-deep p-6 text-white">
      {fondo && (
        <div
          className="pointer-events-none fixed inset-0 bg-cover bg-center"
          style={{ backgroundImage: cssUrl(fondo) }}
        />
      )}
      <div className="pointer-events-none fixed inset-0 bg-brand-deep/60" />

      <img
        src={branding.logoDarkUrl}
        alt={branding.companyName}
        className="absolute left-8 top-6 z-10 h-10 w-auto max-w-56 object-contain"
      />

      <main className="relative z-10 flex w-full max-w-6xl flex-col items-center gap-8">
        {premio?.imageUrl && mostrarPremio && estado.tipo !== 'ganador' && (
          <FotoPremio
            src={premio.imageUrl}
            alt=""
            className="max-h-[28vh] max-w-[60vw] object-contain"
          />
        )}
        {premio && mostrarPremio && estado.tipo !== 'ganador' && (
          <h1 className="text-balance text-center text-5xl font-black tracking-tight drop-shadow-lg lg:text-7xl">
            {premio.name}
          </h1>
        )}

        {estado.tipo === 'espera' && (
          <p className="text-2xl font-bold text-white/90">
            {premio ? 'Preparando el próximo sorteo…' : branding.appName}
          </p>
        )}

        {estado.tipo === 'girando' && (
          <RuletaAnimacion
            participantes={estado.bombo}
            winner={estado.ganador}
            running
            modo={estado.modo}
            duracionSec={estado.duracionSec}
            onComplete={() => fireConfetti()}
          />
        )}

        {estado.tipo === 'ganador' && (
          <div className="w-full max-w-3xl overflow-hidden rounded-3xl border-4 border-brand bg-card text-card-foreground shadow-2xl">
            <div className="bg-brand px-8 py-8 text-center text-white">
              <div className="mx-auto flex h-24 w-24 items-center justify-center rounded-full bg-brand-deep ring-4 ring-white">
                <Trophy className="h-12 w-12 text-amber-300" />
              </div>
              <p className="mt-4 text-sm font-bold uppercase tracking-[0.3em]">¡Tenemos ganador!</p>
              <h2 className="mt-2 text-balance text-5xl font-black lg:text-7xl">
                {estado.visible?.nombre ?? estado.ganador.winnerName}
              </h2>
              {(estado.visible ? estado.visible.detalle : estado.ganador.winnerCompany) && (
                <p className="mt-3 text-2xl font-semibold">
                  {estado.visible ? estado.visible.detalle : estado.ganador.winnerCompany}
                </p>
              )}
              {estado.visible?.identificador && (
                <p className="mt-2 font-mono text-xl font-bold tabular-nums opacity-90">
                  {estado.visible.identificador}
                </p>
              )}
            </div>
            <div className="flex items-center justify-center gap-4 p-6">
              {estado.premio.imageUrl ? (
                <FotoPremio
                  src={estado.premio.imageUrl}
                  alt=""
                  className="h-20 w-20 object-contain"
                />
              ) : (
                <Gift className="h-10 w-10 text-brand dark:text-brand-accent" />
              )}
              <p className="text-3xl font-black text-foreground">{estado.premio.name}</p>
            </div>
            {presente && (
              <p className="flex items-center justify-center gap-2 bg-emerald-600 py-3 text-xl font-bold text-white">
                <UserCheck className="h-6 w-6" /> Presente en la sala
              </p>
            )}
          </div>
        )}
      </main>
    </div>
  )
}
