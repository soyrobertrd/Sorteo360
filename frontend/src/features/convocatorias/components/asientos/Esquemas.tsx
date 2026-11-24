import { useContext, useEffect, useRef, useState, type CSSProperties, type ReactNode } from 'react'
import { X } from 'lucide-react'
import { createPortal } from 'react-dom'
import { type Invitado } from '../../mockParticipants'
import {
  sillasDe,
  unidadesDe,
  MESA_PRINCIPAL,
  esReservada,
  nombreDeMesa,
  textoAsiento,
  type Asiento,
  type ConfigMesas,
  type Etiquetas,
  claveDe,
} from '../../mesas'
import { AsientoResaltado, minus } from './texto'

export function Leyenda({ reservadas }: { reservadas: boolean }) {
  const item = (clase: string, texto: string) => (
    <span className="flex items-center gap-1.5">
      <span className={`h-3 w-3 rounded ${clase}`} /> {texto}
    </span>
  )
  return (
    <div className="flex flex-wrap items-center gap-3 text-[11px] font-semibold text-slate-500">
      {item('bg-brand dark:bg-blue-500', 'Ocupado')}
      {item('border border-slate-300 bg-white dark:border-slate-600 dark:bg-slate-900', 'Libre')}
      {reservadas && item('border-2 border-amber-400 bg-amber-50', 'Área VIP')}
    </div>
  )
}

const claseAsiento = (ocupado: boolean, protocolo: boolean) =>
  ocupado
    ? protocolo
      ? 'bg-amber-500 text-white border-amber-500'
      : 'bg-brand text-white border-brand dark:bg-blue-500 dark:border-blue-500'
    : protocolo
      ? 'border-2 border-amber-400 bg-amber-50 text-amber-700 dark:bg-amber-950/40'
      : 'border border-slate-300 bg-white text-slate-400 hover:border-brand dark:border-slate-600 dark:bg-slate-900'

type Ocupante = (mesa: number, silla: number) => Invitado | null

/** La franja oscura de arriba de cada esquema: hacia dónde mira el público. */
export function Escenario({ texto, children }: { texto: string; children?: ReactNode }) {
  return (
    <div className="mx-auto mb-5 flex w-3/4 flex-col items-center gap-2 rounded-b-3xl bg-slate-800 px-4 py-2 text-white dark:bg-slate-700">
      <span className="text-[10px] font-black uppercase tracking-[0.3em]">{texto}</span>
      {children}
    </div>
  )
}

/**
 * Un asiento de cualquier esquema: su color dice si está ocupado, libre o en el área VIP, y al
 * pasar el mouse muestra quién lo tiene. En una mesa redonda va redondo y posicionado.
 */
export function BotonAsiento({
  mesa,
  silla,
  protocolo,
  config,
  et,
  ocupante,
  onSilla,
  redondo = false,
  style,
}: {
  mesa: number
  silla: number
  protocolo: boolean
  config: ConfigMesas
  et: Etiquetas
  ocupante: Ocupante
  onSilla: (mesa: number, silla: number) => void
  redondo?: boolean
  style?: CSSProperties
}) {
  const persona = ocupante(mesa, silla)
  const texto = textoAsiento({ mesa, silla }, config, et)
  const resaltado = useContext(AsientoResaltado)
  const encontrado = Boolean(persona && resaltado === claveDe(persona.email))
  const ref = useRef<HTMLButtonElement>(null)
  useEffect(() => {
    if (encontrado) ref.current?.scrollIntoView({ block: 'nearest', inline: 'center' })
  }, [encontrado])
  return (
    <button
      ref={ref}
      type="button"
      onClick={() => onSilla(mesa, silla)}
      title={
        persona
          ? `${texto} · ${persona.nombre}${persona.empresa ? ` (${persona.empresa})` : ''}`
          : `${texto} · libre`
      }
      aria-label={`${texto}${persona ? `, ${persona.nombre}` : ', libre'}`}
      style={style}
      className={`flex items-center justify-center font-bold transition-colors ${
        redondo ? 'absolute h-7 w-7 rounded-full text-[10px]' : 'h-8 w-8 rounded-md text-[11px]'
      } ${claseAsiento(Boolean(persona), protocolo)} ${
        encontrado ? 'z-10 scale-125 ring-4 ring-emerald-400 ring-offset-1' : ''
      }`}
    >
      {silla}
    </button>
  )
}

/**
 * Auditorio y conferencia: el escenario arriba, con la mesa principal de frente al público si la
 * hay, y las filas debajo. Cada fila se dibuja en 1 a 3 bloques con pasillo, solo para ubicar
 * rápido la silla: el asiento sigue siendo fila y número.
 */
export function EsquemaFilas({
  config,
  et,
  ocupante,
  onSilla,
}: {
  config: ConfigMesas
  et: Etiquetas
  ocupante: Ocupante
  onSilla: (mesa: number, silla: number) => void
}) {
  const filas = Array.from({ length: config.mesas }, (_, i) => i + 1)
  const n = config.sillasPorMesa
  const columnas = Math.min(3, Math.max(1, config.columnas ?? 1))
  const porBloque = Math.ceil(n / columnas)
  const bloques = Array.from({ length: columnas }, (_, b) =>
    Array.from({ length: n }, (_, j) => j + 1).slice(b * porBloque, (b + 1) * porBloque),
  ).filter((bloque) => bloque.length > 0)
  const principal = Array.from({ length: config.mesaPrincipal ?? 0 }, (_, j) => j + 1)
  const boton = (mesa: number, silla: number, protocolo: boolean) => (
    <BotonAsiento key={silla} {...{ mesa, silla, protocolo, config, et, ocupante, onSilla }} />
  )
  return (
    <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-slate-50/60 p-4 dark:border-slate-800 dark:bg-slate-950/30">
      <div className="flex min-w-fit flex-col items-center space-y-1.5">
        <Escenario texto="Escenario">
          {principal.length > 0 && (
            <div className="flex flex-col items-center gap-1 pb-1">
              <div className="flex gap-1">
                {principal.map((silla) => boton(MESA_PRINCIPAL, silla, true))}
              </div>
              <div className="h-1.5 w-full rounded-full bg-amber-400" />
              <span className="flex items-center gap-1 text-[10px] font-bold text-amber-300">
                Mesa principal · de frente al público
              </span>
            </div>
          )}
        </Escenario>
        {filas.map((fila) => {
          const protocolo = esReservada(config, fila)
          return (
            <div key={fila} className="flex items-center gap-2">
              <span
                className={`w-28 shrink-0 truncate text-right text-[11px] font-bold ${
                  protocolo ? 'text-amber-700 dark:text-amber-300' : 'text-slate-500'
                }`}
                title={nombreDeMesa(config, fila, et)}
              >
                {nombreDeMesa(config, fila, et)}
              </span>
              {/* Bloques del mismo ancho para que el pasillo caiga en el centro del escenario. */}
              <div className="flex gap-5">
                {bloques.map((bloque, b) => (
                  <div
                    key={b}
                    style={{ width: porBloque * 36 - 4 }}
                    className={`flex gap-1 ${
                      b === 0 && bloques.length > 1
                        ? 'justify-end'
                        : b === bloques.length - 1 && bloques.length > 1
                          ? 'justify-start'
                          : 'justify-center'
                    }`}
                  >
                    {bloque.map((silla) => boton(fila, silla, protocolo))}
                  </div>
                ))}
              </div>
              {/* Mismo ancho que el nombre de la fila, para que el conjunto quede centrado. */}
              <span aria-hidden className="w-28 shrink-0" />
            </div>
          )
        })}
      </div>
    </div>
  )
}

/**
 * Tipo escuela (talleres): mesas de 2 a 4 personas, todas de frente a quien expone.
 */
export function EsquemaEscuela({
  config,
  et,
  ocupante,
  onSilla,
}: {
  config: ConfigMesas
  et: Etiquetas
  ocupante: Ocupante
  onSilla: (mesa: number, silla: number) => void
}) {
  return (
    <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-slate-50/60 p-4 dark:border-slate-800 dark:bg-slate-950/30">
      <Escenario texto="Expositor · Taller" />
      <div className="flex flex-wrap justify-center gap-x-10 gap-y-5">
        {Array.from({ length: config.mesas }, (_, i) => i + 1).map((mesa) => {
          const protocolo = esReservada(config, mesa)
          return (
            <div key={mesa} className="flex flex-col items-center gap-1">
              <div
                className={`h-3 w-full min-w-16 rounded-sm ${protocolo ? 'bg-amber-300' : 'bg-slate-300 dark:bg-slate-600'}`}
              />
              <div className="flex gap-1">
                {Array.from({ length: config.sillasPorMesa }, (_, j) => j + 1).map((silla) => (
                  <BotonAsiento
                    key={silla}
                    {...{ mesa, silla, protocolo, config, et, ocupante, onSilla }}
                  />
                ))}
              </div>
              <OcupantesMesa
                mesa={mesa}
                titulo={nombreDeMesa(config, mesa, et)}
                sillas={config.sillasPorMesa}
                ocupante={ocupante}
                et={et}
                botonClass="rounded px-1.5 text-[10px] font-bold text-slate-500 hover:bg-slate-200 hover:text-slate-800 dark:hover:bg-slate-800"
              >
                {nombreDeMesa(config, mesa, et)}
              </OcupantesMesa>
            </div>
          )
        })}
      </div>
    </div>
  )
}

/**
 * Herradura: cada mesa en U con sus asientos por los dos lados y el fondo; la abertura mira al
 * escenario, donde están los presentadores.
 */
export function EsquemaHerradura({
  config,
  et,
  ocupante,
  onSilla,
}: {
  config: ConfigMesas
  et: Etiquetas
  ocupante: Ocupante
  onSilla: (mesa: number, silla: number) => void
}) {
  const n = config.sillasPorMesa
  const lado = Math.floor(n / 3)
  const asiento = (mesa: number, silla: number, protocolo: boolean) => (
    <BotonAsiento key={silla} {...{ mesa, silla, protocolo, config, et, ocupante, onSilla }} />
  )
  return (
    <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-slate-50/60 p-4 dark:border-slate-800 dark:bg-slate-950/30">
      <Escenario texto="Escenario · Presentadores" />
      <div className="flex flex-wrap justify-center gap-10">
        {Array.from({ length: config.mesas }, (_, i) => i + 1).map((mesa) => {
          const protocolo = esReservada(config, mesa)
          const izquierda = Array.from({ length: lado }, (_, j) => j + 1)
          const fondo = Array.from({ length: n - 2 * lado }, (_, j) => lado + j + 1)
          const derecha = Array.from({ length: lado }, (_, j) => n - lado + j + 1).reverse()
          return (
            <div key={mesa} className="flex flex-col items-center gap-2">
              <div className="flex items-end gap-1">
                <div className="flex flex-col gap-1">
                  {izquierda.map((s) => asiento(mesa, s, protocolo))}
                </div>
                <div
                  className={`flex min-h-32 flex-col items-center justify-end gap-1 rounded-b-xl border-x-4 border-b-4 px-2 pb-1 ${
                    protocolo ? 'border-amber-300' : 'border-slate-300 dark:border-slate-600'
                  }`}
                >
                  <div className="flex gap-1">{fondo.map((s) => asiento(mesa, s, protocolo))}</div>
                </div>
                <div className="flex flex-col gap-1">
                  {derecha.map((s) => asiento(mesa, s, protocolo))}
                </div>
              </div>
              <OcupantesMesa
                mesa={mesa}
                titulo={nombreDeMesa(config, mesa, et)}
                sillas={n}
                ocupante={ocupante}
                et={et}
                botonClass="rounded px-1.5 text-[11px] font-bold text-slate-600 hover:bg-slate-200 hover:text-slate-900 dark:text-slate-300 dark:hover:bg-slate-800"
              >
                {nombreDeMesa(config, mesa, et)}
              </OcupantesMesa>
            </div>
          )
        })}
      </div>
    </div>
  )
}

/**
 * Envuelve el centro o el nombre de una mesa: al pasar el mouse muestra quién está sentado en
 * ella, y al hacer clic el panel se queda abierto hasta cerrarlo o hacer clic fuera.
 */
export function OcupantesMesa({
  mesa,
  titulo,
  sillas,
  ocupante,
  et,
  className = 'relative',
  botonClass,
  children,
}: {
  mesa: number
  titulo: string
  sillas: number
  ocupante: Ocupante
  et: Etiquetas
  className?: string
  botonClass: string
  children: ReactNode
}) {
  const [fijo, setFijo] = useState(false)
  const [encima, setEncima] = useState(false)
  const ref = useRef<HTMLDivElement>(null)
  const panel = useRef<HTMLDivElement>(null)
  const [pos, setPos] = useState<{ left: number; top: number } | null>(null)
  const abierto = fijo || encima
  // El panel va en un portal con posición fija: flota sobre el salón y no le agrega scroll.
  useEffect(() => {
    if (!abierto) return
    const ubicar = () => {
      const r = ref.current?.getBoundingClientRect()
      if (!r) return
      const ancho = 256
      const left = Math.min(
        Math.max(8, r.left + r.width / 2 - ancho / 2),
        window.innerWidth - ancho - 8,
      )
      setPos({ left, top: r.bottom + 8 })
    }
    ubicar()
    window.addEventListener('scroll', ubicar, true)
    window.addEventListener('resize', ubicar)
    return () => {
      window.removeEventListener('scroll', ubicar, true)
      window.removeEventListener('resize', ubicar)
    }
  }, [abierto])
  useEffect(() => {
    if (!fijo) return
    const fuera = (e: MouseEvent) => {
      const t = e.target as Node
      if (!ref.current?.contains(t) && !panel.current?.contains(t)) setFijo(false)
    }
    const tecla = (e: KeyboardEvent) => e.key === 'Escape' && setFijo(false)
    document.addEventListener('mousedown', fuera)
    document.addEventListener('keydown', tecla)
    return () => {
      document.removeEventListener('mousedown', fuera)
      document.removeEventListener('keydown', tecla)
    }
  }, [fijo])
  const sentados = Array.from({ length: sillas }, (_, j) => ({
    silla: j + 1,
    persona: ocupante(mesa, j + 1),
  })).filter((s) => s.persona)
  return (
    <div
      ref={ref}
      className={className}
      onMouseEnter={() => setEncima(true)}
      onMouseLeave={() => setEncima(false)}
    >
      <button
        type="button"
        aria-expanded={abierto}
        aria-label={`Ver quién está en ${titulo}`}
        onClick={() => setFijo((f) => !f)}
        className={`cursor-pointer ${botonClass}`}
      >
        {children}
      </button>
      {abierto &&
        pos &&
        createPortal(
          <div
            ref={panel}
            role="dialog"
            aria-label={`Personas en ${titulo}`}
            style={{ left: pos.left, top: pos.top }}
            onMouseEnter={() => setEncima(true)}
            onMouseLeave={() => setEncima(false)}
            className="fixed z-[60] w-64 rounded-xl border border-slate-200 bg-white p-3 text-left shadow-xl dark:border-slate-700 dark:bg-slate-900"
          >
            <div className="mb-2 flex items-center justify-between gap-2">
              <p className="truncate text-xs font-black text-slate-900 dark:text-white">{titulo}</p>
              <span className="shrink-0 text-[10px] font-bold text-slate-500">
                {sentados.length}/{sillas}
              </span>
              {fijo && (
                <button
                  type="button"
                  aria-label="Cerrar"
                  onClick={() => setFijo(false)}
                  className="cursor-pointer text-slate-400 hover:text-slate-700"
                >
                  <X size={13} />
                </button>
              )}
            </div>
            {sentados.length === 0 ? (
              <p className="text-[11px] text-slate-500">Nadie sentado todavía.</p>
            ) : (
              <ul className="max-h-60 space-y-1.5 overflow-y-auto">
                {sentados.map(({ silla, persona }) => (
                  <li key={silla} className="flex items-start gap-2 text-[11px]">
                    <span className="flex h-5 w-5 shrink-0 items-center justify-center rounded bg-brand text-[10px] font-bold text-white dark:bg-blue-500">
                      {silla}
                    </span>
                    <span className="min-w-0">
                      <span className="block truncate font-bold text-slate-800 dark:text-slate-100">
                        {persona!.nombre}
                      </span>
                      {persona!.empresa && (
                        <span className="block truncate text-slate-500">{persona!.empresa}</span>
                      )}
                    </span>
                  </li>
                ))}
              </ul>
            )}
            {sentados.length < sillas && (
              <p className="mt-2 border-t border-slate-100 pt-2 text-[10px] text-slate-500 dark:border-slate-800">
                {sillas - sentados.length} {minus(et.asientos)} libres
              </p>
            )}
          </div>,
          document.body,
        )}
    </div>
  )
}

/** Banquete o cena: mesas redondas con sus sillas alrededor. */
export function EsquemaMesas({
  config,
  et,
  ocupante,
  onSilla,
}: {
  config: ConfigMesas
  et: Etiquetas
  ocupante: Ocupante
  onSilla: (mesa: number, silla: number) => void
}) {
  const mesas = Array.from({ length: config.mesas }, (_, i) => i + 1)
  const n = config.sillasPorMesa
  return (
    <div className="rounded-2xl border border-slate-200 bg-slate-50/60 p-4 dark:border-slate-800 dark:bg-slate-950/30">
      <Escenario texto="Escenario" />
      <div className="grid grid-cols-2 justify-items-center gap-6 sm:grid-cols-3 lg:grid-cols-5">
        {mesas.map((mesa) => {
          const protocolo = esReservada(config, mesa)
          const usadas = Array.from({ length: n }, (_, j) => ocupante(mesa, j + 1)).filter(
            Boolean,
          ).length
          return (
            <div key={mesa} className="relative h-48 w-48">
              <OcupantesMesa
                mesa={mesa}
                titulo={nombreDeMesa(config, mesa, et)}
                sillas={n}
                ocupante={ocupante}
                et={et}
                className="absolute inset-10"
                botonClass={`flex h-full w-full flex-col items-center justify-center rounded-full border-2 text-center transition-shadow hover:shadow-md focus-visible:outline-2 focus-visible:outline-blue-600 ${
                  protocolo
                    ? 'border-amber-400 bg-amber-50 dark:bg-amber-950/40'
                    : 'border-slate-300 bg-white dark:border-slate-600 dark:bg-slate-900'
                }`}
              >
                <span className="max-w-[90px] truncate text-[10px] font-black text-slate-800 dark:text-slate-100">
                  {nombreDeMesa(config, mesa, et)}
                </span>
                <span className="text-[10px] font-bold text-slate-500">
                  {usadas}/{n}
                </span>
              </OcupantesMesa>
              {Array.from({ length: n }, (_, j) => j + 1).map((silla) => {
                const angulo = (2 * Math.PI * (silla - 1)) / n - Math.PI / 2
                return (
                  <BotonAsiento
                    key={silla}
                    {...{ mesa, silla, protocolo, config, et, ocupante, onSilla }}
                    redondo
                    style={{
                      left: `calc(50% + ${Math.cos(angulo) * 78}px - 14px)`,
                      top: `calc(50% + ${Math.sin(angulo) * 78}px - 14px)`,
                    }}
                  />
                )
              })}
            </div>
          )
        })}
      </div>
    </div>
  )
}

/** La lista por mesa o fila: ocupación, barra y, al abrirla, quién va en cada asiento. */
export function ListaPorUnidad({
  config,
  et,
  asientos,
  ocupante,
  onLiberar,
  onLibre,
}: {
  config: ConfigMesas
  et: Etiquetas
  asientos: Record<string, Asiento>
  ocupante: Ocupante
  onLiberar: (persona: Invitado) => void
  onLibre: (mesa: number, silla: number) => void
}) {
  const [abiertas, setAbiertas] = useState<Set<number>>(new Set())
  const alternar = (mesa: number) =>
    setAbiertas((prev) => {
      const next = new Set(prev)
      if (next.has(mesa)) next.delete(mesa)
      else next.add(mesa)
      return next
    })
  return (
    <div className="grid grid-cols-1 items-start gap-3 sm:grid-cols-2 lg:grid-cols-4">
      {unidadesDe(config).map((mesa) => {
        const usadas = Object.values(asientos).filter((a) => a.mesa === mesa).length
        const total = sillasDe(config, mesa)
        const llena = usadas >= total
        const protocolo = esReservada(config, mesa)
        const abierta = abiertas.has(mesa)
        return (
          <div
            key={mesa}
            className={`rounded-2xl border p-3 text-xs ${
              protocolo
                ? 'border-amber-300 bg-amber-50/50 dark:border-amber-800 dark:bg-amber-950/20'
                : llena
                  ? 'border-emerald-200 bg-emerald-50/50 dark:border-emerald-900 dark:bg-emerald-950/20'
                  : 'border-slate-200 dark:border-slate-800'
            }`}
          >
            <button
              type="button"
              onClick={() => alternar(mesa)}
              aria-expanded={abierta}
              className="flex w-full items-center justify-between gap-2 text-left"
            >
              <span className="min-w-0">
                <span className="flex items-center gap-1 truncate font-bold text-slate-800 dark:text-slate-200">
                  {nombreDeMesa(config, mesa, et)}
                </span>
                {config.criterio === 'categoria' && config.categorias[mesa] && (
                  <span className="text-[10px] font-semibold text-amber-700 dark:text-amber-300">
                    {config.categorias[mesa]}
                  </span>
                )}
              </span>
              <span className="shrink-0 font-bold text-slate-600 dark:text-slate-300">
                {usadas}/{total}
              </span>
            </button>
            <div className="mt-2 h-1.5 overflow-hidden rounded-full bg-slate-100 dark:bg-slate-800">
              <div
                className={`h-full rounded-full ${
                  protocolo ? 'bg-amber-500' : llena ? 'bg-emerald-500' : 'bg-blue-500'
                }`}
                style={{ width: `${(usadas / Math.max(1, total)) * 100}%` }}
              />
            </div>
            {abierta && (
              <ul className="mt-2 space-y-1">
                {Array.from({ length: total }, (_, j) => j + 1).map((silla) => {
                  const persona = ocupante(mesa, silla)
                  return (
                    <li key={silla} className="flex items-center justify-between gap-2">
                      <span className="w-16 shrink-0 text-slate-400">
                        {et.asiento} {silla}
                      </span>
                      {persona ? (
                        <>
                          <span className="min-w-0 flex-1 truncate font-semibold text-slate-700 dark:text-slate-300">
                            {persona.nombre}
                          </span>
                          <button
                            type="button"
                            aria-label={`Liberar ${textoAsiento({ mesa, silla }, config, et)}`}
                            onClick={() => onLiberar(persona)}
                            className="text-slate-400 hover:text-rose-600"
                          >
                            <X size={13} />
                          </button>
                        </>
                      ) : (
                        <button
                          type="button"
                          onClick={() => onLibre(mesa, silla)}
                          className="flex-1 text-left text-blue-600 hover:underline dark:text-blue-400"
                        >
                          Libre · asignar
                        </button>
                      )}
                    </li>
                  )
                })}
              </ul>
            )}
          </div>
        )
      })}
    </div>
  )
}
