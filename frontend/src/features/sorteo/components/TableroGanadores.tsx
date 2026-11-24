import { useEffect, useState, type CSSProperties } from 'react'
import { cn } from '@/lib/utils'
import { DecoracionNavidena } from './DecoracionNavidena'
import {
  GIRO_INICIAL_MS,
  GIRO_PRINCIPAL_MS,
  PASO_FILA_MS,
  type EstadoTablero,
  type FilaTablero,
} from '../tablero'
import { FotoPremio } from './FotoPremio'

const sinMovimiento = () =>
  typeof window !== 'undefined' && window.matchMedia('(prefers-reduced-motion: reduce)').matches

/**
 * El número de una fila como contador mecánico: gira con cifras al azar y se detiene en el suyo.
 * Con "menos movimiento" aparece quieto.
 */
function NumeroRodante({
  valor,
  detieneEn,
  lote,
  grande = false,
}: {
  valor: string
  detieneEn: number
  lote: number
  grande?: boolean
}) {
  const [mostrado, setMostrado] = useState(valor)
  useEffect(() => {
    if (sinMovimiento()) {
      setMostrado(valor)
      return
    }
    const azar = () => valor.replace(/\d/g, () => String(Math.floor(Math.random() * 10)))
    const id = window.setInterval(() => setMostrado(azar()), 70)
    const fin = window.setTimeout(() => {
      window.clearInterval(id)
      setMostrado(valor)
    }, detieneEn)
    return () => {
      window.clearInterval(id)
      window.clearTimeout(fin)
    }
  }, [valor, detieneEn, lote])

  // Un ID largo (cédula, "EMP-0124") encoge cada casilla para caber en el mismo ancho que 7 cifras.
  const ancho = (base: number, total: number) => `${Math.min(base, total / mostrado.length)}cqw`

  if (grande) {
    // El contador del premio principal: casillas grandes con cifras finas, como en la plantilla.
    return (
      <span className="inline-flex rounded-[2.2cqw] bg-brand-deep p-[1.1cqw] shadow-[0_0.8cqw_2cqw_rgb(0_0_0/0.25)]">
        <span className="flex overflow-hidden rounded-[1.2cqw] bg-linear-to-b from-black/45 via-black/20 to-black/45">
          {[...mostrado].map((c, i) => (
            <span
              key={i}
              style={{ width: ancho(5.2, 48), fontSize: ancho(6.4, 59) }}
              className="border-l border-white/10 py-[0.6cqw] text-center font-extralight leading-none text-white tabular-nums first:border-l-0"
            >
              {c}
            </span>
          ))}
        </span>
      </span>
    )
  }
  return (
    <span className="flex h-full items-center justify-center rounded-[0.6cqw] bg-brand-deep px-[1cqw] shadow-[inset_0_-0.3cqw_0_rgb(0_0_0/0.25)]">
      <span className="flex gap-[0.3cqw] rounded-[0.4cqw] bg-black/25 px-[0.5cqw] py-[0.15cqw]">
        {[...mostrado].map((c, i) => (
          <span
            key={i}
            style={{ width: ancho(1.55, 12), fontSize: ancho(2.15, 16.6) }}
            className="text-center font-mono font-bold leading-none text-white tabular-nums"
          >
            {c}
          </span>
        ))}
      </span>
    </span>
  )
}

function Fila({ fila, orden, lote }: { fila: FilaTablero; orden: number; lote: number }) {
  const detieneEn = GIRO_INICIAL_MS + orden * PASO_FILA_MS
  const visible = useAparece(detieneEn, lote)

  return (
    <li className="grid h-[3.3cqw] grid-cols-[17cqw_1fr] items-center gap-[2cqw]">
      <NumeroRodante valor={fila.numero} detieneEn={detieneEn} lote={lote} />
      <div
        className={cn(
          'min-w-0 transition-all duration-500',
          visible ? 'translate-x-0 opacity-100' : '-translate-x-[1cqw] opacity-0',
        )}
      >
        <p className="truncate text-[1.55cqw] font-black leading-tight text-brand-deep">
          {fila.nombre}
        </p>
        {fila.detalle && (
          <p className="truncate text-[1.05cqw] font-semibold leading-tight text-brand-deep/70">
            {fila.detalle}
          </p>
        )}
      </div>
    </li>
  )
}

/** Si ya pasó `ms` desde que empezó el lote: el nombre aparece cuando su número se detiene. */
function useAparece(ms: number, lote: number): boolean {
  const [visible, setVisible] = useState(false)
  useEffect(() => {
    setVisible(false)
    const t = window.setTimeout(() => setVisible(true), sinMovimiento() ? 0 : ms)
    return () => window.clearTimeout(t)
  }, [ms, lote])
  return visible
}

/**
 * El premio principal, como la plantilla: la foto del producto, el contador grande montado sobre
 * su borde inferior, y debajo el nombre del ganador y su área, centrados.
 */
function PremioPrincipal({
  imagen,
  nombrePremio,
  fila,
  lote,
}: {
  imagen: string | null
  nombrePremio: string
  fila: FilaTablero | undefined
  lote: number
}) {
  const visible = useAparece(GIRO_PRINCIPAL_MS, lote)
  return (
    <div className="flex min-h-0 flex-col items-center justify-center text-center">
      {imagen && (
        // PNG transparente: sin marco ni sombra, y con aire hasta el contador.
        <FotoPremio
          src={imagen}
          alt={nombrePremio}
          className="mb-[2cqw] max-h-[17cqw] max-w-[36cqw] object-contain"
        />
      )}
      <div aria-live="polite">
        {fila ? (
          <NumeroRodante valor={fila.numero} detieneEn={GIRO_PRINCIPAL_MS} lote={lote} grande />
        ) : (
          <NumeroRodante valor={'-'.repeat(7)} detieneEn={0} lote={lote} grande />
        )}
      </div>
      <div
        className={cn(
          'mt-[1.4cqw] w-full transition-all duration-700',
          fila && visible ? 'translate-y-0 opacity-100' : 'translate-y-[1cqw] opacity-0',
        )}
      >
        <p className="truncate text-[3.2cqw] font-black leading-tight tracking-tight text-brand-deep">
          {fila?.nombre}
        </p>
        {fila?.detalle && (
          <p className="mt-[0.6cqw] truncate text-[2cqw] font-bold leading-tight tracking-tight text-slate-500">
            {fila.detalle}
          </p>
        )}
      </div>
    </div>
  )
}

/**
 * La pantalla del tablero, con medidas relativas a su ancho: se ve igual en la vista previa de la
 * consola y en el proyector. En la vista previa es 16:9; con `llenar` ocupa toda la pantalla, de
 * borde a borde, con la franja arriba y el resto en blanco. Colores y logo: los de la marca.
 */
export function TableroGanadores({
  estado,
  logoUrl,
  llenar = false,
  className,
}: {
  estado: EstadoTablero
  logoUrl: string
  llenar?: boolean
  className?: string
}) {
  const ronda = estado.ronda
  const tablero = (
    <div
      className={cn(
        '[container-type:inline-size]',
        llenar ? 'w-[min(100vw,177.78dvh)]' : className,
      )}
    >
      <div className="relative flex aspect-video w-full flex-col overflow-hidden bg-white text-slate-900">
        {estado.navidad && <DecoracionNavidena />}
        {/* Títulos a los lados del logo, hacia el centro; el logo, en blanco y del alto de la franja. */}
        <header className="grid h-[9cqw] shrink-0 grid-cols-[1fr_auto_1fr] items-center gap-[3cqw] bg-brand-deep px-[3cqw] text-white">
          <p className="truncate text-right text-[2.5cqw] font-black leading-none">
            {estado.titulo}
          </p>
          <span className="flex h-full items-center py-[1.2cqw]">
            {logoUrl && (
              <img
                src={logoUrl}
                alt=""
                className="h-full w-auto max-w-[24cqw] object-contain brightness-0 invert"
              />
            )}
          </span>
          <p className="truncate text-left text-[2.5cqw] font-black leading-none">
            {estado.subtitulo}
          </p>
        </header>

        <div
          className={cn(
            // Como la plantilla de Banreservas: el nombre del premio tiene una columna ancha a la
            // izquierda y los ganadores (número, nombre y área) quedan más a la derecha.
            'grid min-h-0 flex-1 grid-cols-[36%_1fr] gap-[4cqw] px-[3cqw] pb-[1.5cqw]',
            // La guirnalda cuelga ~4cqw bajo la franja: el contenido baja para no quedar detrás.
            estado.navidad ? 'pt-[5cqw]' : 'pt-[2.5cqw]',
          )}
        >
          <aside className="flex min-h-0 flex-col items-end text-right">
            {ronda != null && (
              <p className="text-[1.5cqw] font-semibold text-slate-700">Ronda No. {ronda}</p>
            )}
            {estado.total > 0 && (
              <p className="mt-[0.4cqw] text-[2.1cqw] text-slate-500">
                {estado.total} premio(s) de:
              </p>
            )}
            {/* La tipografía de la plantilla de Banreservas: 56 px sobre 1490 de ancho = 3,75cqw. */}
            <p
              className="mt-[0.6cqw] break-words font-premio text-[3.75cqw] font-extralight leading-[0.93] text-(--tablero-premio)"
              style={{ '--tablero-premio': estado.colorPremio } as CSSProperties}
            >
              {estado.premioNombre ?? ''}
            </p>
            {estado.premioPatrocinador && (
              <p className="mt-[0.8cqw] text-[1.5cqw] font-semibold text-slate-500">
                Cortesía de {estado.premioPatrocinador}
              </p>
            )}
            {estado.total > 0 && (
              <p className="mt-[1.2cqw] text-[1.9cqw] text-slate-500">
                {estado.quedan === 0
                  ? 'Grupo completo.'
                  : estado.quedan === 1
                    ? 'Queda 1 premio.'
                    : `Quedan ${estado.quedan} premios.`}
              </p>
            )}
          </aside>

          {estado.principal ? (
            <PremioPrincipal
              imagen={estado.premioImagen}
              nombrePremio={estado.premioNombre ?? ''}
              fila={estado.filas[0]}
              lote={estado.lote}
            />
          ) : (
            <ol className="flex flex-col gap-[0.7cqw]" aria-live="polite">
              {estado.filas.map((f, i) => (
                <Fila key={`${estado.lote}-${f.id}`} fila={f} orden={i} lote={estado.lote} />
              ))}
            </ol>
          )}
        </div>
      </div>
    </div>
  )
  if (!llenar) return tablero
  // Pantalla completa de cualquier proporción: el tablero 16:9 arriba y al centro, la franja de
  // color continúa de borde a borde y el resto queda en blanco, sin bandas a los lados.
  return (
    <div
      className={cn('flex h-full w-full justify-center overflow-hidden bg-white', className)}
      style={{
        background:
          'linear-gradient(to bottom, var(--color-brand-deep) 0 calc(min(100vw, 177.78dvh) * 0.09), white 0)',
      }}
    >
      {tablero}
    </div>
  )
}
