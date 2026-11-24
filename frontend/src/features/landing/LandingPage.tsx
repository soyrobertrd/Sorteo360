import { useEffect, useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import {
  ArrowRight,
  FileCheck2,
  Palette,
  Pause,
  Plus,
  Play,
  Presentation,
  QrCode,
  Scale,
  Sheet,
  WifiOff,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import { TableroGanadores } from '@/features/sorteo/components/TableroGanadores'
import type { EstadoTablero } from '@/features/sorteo/tablero'
import { CtaRegistro } from './CtaRegistro'
import { PlanesSeccion } from './PlanesSeccion'

/**
 * La página comercial de Sorteo360, en `/`. Anónima: "Crear cuenta" lleva al registro
 * (`/crear-cuenta`) y "Entrar" al login de quien ya tiene cuenta (`/app`).
 */

const FUNCIONES: { icono: ReactNode; titulo: string; texto: string }[] = [
  {
    icono: <Presentation size={22} />,
    titulo: 'Sorteo en pantalla grande',
    texto:
      'Ruleta o nombres pasando, a pantalla completa en el proyector del salón, controlado desde la consola del operador.',
  },
  {
    icono: <Sheet size={22} />,
    titulo: 'Padrón en minutos',
    texto:
      'Sube el listado en Excel, CSV o TXT, o conéctalo por API o webhook a tu sistema de personal. Cada fila con problema se informa sin frenar la carga.',
  },
  {
    icono: <QrCode size={22} />,
    titulo: 'Check-in con QR',
    texto:
      'Gafetes con QR y número de boleto. El escáner de la puerta sigue leyendo si se cae el wifi y envía todo cuando vuelve la red.',
  },
  {
    icono: <Scale size={22} />,
    titulo: 'Reglas que se cumplen solas',
    texto:
      'Solo participa quien llegó. Un premio por persona o uno por departamento en cada ronda, exclusiones y anulaciones con motivo: el sistema las aplica en cada giro.',
  },
  {
    icono: <FileCheck2 size={22} />,
    titulo: 'Acta y entrega con firma',
    texto:
      'Acta notarial en PDF, certificado para cada ganador y entrega del premio con firma y código de canje. Todo queda en la bitácora.',
  },
  {
    icono: <Palette size={22} />,
    titulo: 'Con la marca de tu empresa',
    texto:
      'Tus colores y tu logo en la plataforma, la pantalla del sorteo, los gafetes y los certificados.',
  },
]

/** El tablero de verdad, en miniatura, para la función principal del landing. Datos de muestra. */
const TABLERO_MUESTRA: EstadoTablero = {
  titulo: 'Rifa Electrónica',
  subtitulo: 'Tu empresa',
  ronda: 1,
  premioNombre: '10,000',
  colorPremio: '#01AEF0',
  navidad: false,
  principal: false,
  premioImagen: null,
  total: 83,
  quedan: 33,
  cifras: 7,
  lote: 0,
  // Nombres y áreas inventados: una página pública nunca muestra datos de un padrón real.
  filas: [
    ['0001042', 'ANA LUCÍA FERNÁNDEZ', 'RECURSOS HUMANOS'],
    ['0001187', 'CARLOS MÉNDEZ ROSARIO', 'TECNOLOGÍA'],
    ['0001233', 'MARÍA JOSÉ CASTILLO', 'MERCADEO'],
    ['0001356', 'LUIS ALBERTO GÓMEZ', 'FINANZAS'],
    ['0001479', 'PAOLA RAMÍREZ SOTO', 'OPERACIONES'],
    ['0001502', 'JOSÉ MIGUEL TORRES', 'VENTAS'],
    ['0001625', 'CAROLINA DÍAZ PEÑA', 'SERVICIO AL CLIENTE'],
    ['0001748', 'MIGUEL ÁNGEL REYES', 'LOGÍSTICA'],
    ['0001871', 'LAURA BEATRIZ NÚÑEZ', 'LEGAL'],
    ['0001994', 'PEDRO PABLO JIMÉNEZ', 'ADMINISTRACIÓN'],
  ].map(([numero, nombre, detalle]) => ({ id: numero, numero, nombre, detalle })),
}

/** El mismo tablero en modo premio principal: un ganador grande, para la tarjeta de la función. */
const PRINCIPAL_MUESTRA: EstadoTablero = {
  ...TABLERO_MUESTRA,
  ronda: 4,
  premioNombre: 'Viaje a Punta Cana',
  principal: true,
  total: 3,
  quedan: 2,
  filas: TABLERO_MUESTRA.filas.slice(0, 1),
}

const HECHOS = [
  ['25.000', 'participantes por sorteo'],
  ['10', 'ganadores por pantalla'],
  ['Sin internet', 'en el salón, si hace falta'],
  ['1 PDF', 'acta con cada ganador y su entrega'],
] as const

const PREGUNTAS = [
  [
    '¿Qué pasa si se cae el internet en el salón?',
    'Sorteo360 puede correr en una laptop dentro de la red del salón. La puerta, la consola y el proyector siguen funcionando, y el escáner guarda los check-in y los envía cuando vuelve la red.',
  ],
  [
    '¿Cómo cargo a los participantes?',
    'Con un Excel, CSV o TXT, a mano, o conectando tu sistema de personal por API o webhook. Cada fila con problema se informa sin frenar la carga.',
  ],
  [
    '¿Cómo sé que el sorteo es justo?',
    'Cada ganador lo elige el servidor al azar entre quienes cumplen las reglas, y queda en la bitácora con la hora y quién sorteó. Al cerrar, el acta en PDF lo lista todo para el notario.',
  ],
  [
    '¿Puedo sortear muchos premios iguales?',
    'Sí. Los premios iguales salen por lotes de hasta 10 en el tablero de ganadores, y los principales, con foto, uno a uno.',
  ],
  [
    '¿Se ve con la marca de mi empresa?',
    'Sí. Tus colores y tu logo aparecen en la plataforma, la pantalla del sorteo, los gafetes y los certificados.',
  ],
] as const

const PASOS = [
  ['Prepara', 'Crea el sorteo, importa los premios por ronda y carga a los participantes.'],
  ['Recibe', 'En la puerta se escanea el QR de cada invitado y entra al bombo.'],
  ['Sortea', 'Cada premio se gira en vivo frente a todos, con las reglas aplicadas.'],
  ['Entrega', 'El ganador firma al recibir y el acta se genera al cerrar el sorteo.'],
] as const

/**
 * La app de verdad en el héroe: el tablero de ganadores dentro de una ventana, sorteando un lote
 * nuevo cada 12 s. Corre solo más de 5 s, así que lleva pausa (WCAG 2.2.2) y arranca quieto si el
 * sistema pide menos movimiento.
 */
function VentanaApp() {
  const [lote, setLote] = useState(0)
  const [pausada, setPausada] = useState(
    () => window.matchMedia('(prefers-reduced-motion: reduce)').matches,
  )
  useEffect(() => {
    if (pausada) return
    const id = window.setInterval(() => setLote((n) => n + 1), 12000)
    return () => window.clearInterval(id)
  }, [pausada])
  // Cada lote rota el orden: los números vuelven a girar con otros ganadores.
  const filas = TABLERO_MUESTRA.filas.map((_, k, todas) => todas[(k + lote) % todas.length])
  return (
    <figure className="relative">
      <div className="overflow-hidden rounded-xl border border-border bg-card shadow-[0_24px_60px_-20px_rgb(15_23_42/0.25)]">
        <div className="flex items-center gap-3 border-b border-border px-4 py-2.5">
          <span className="flex gap-1.5" aria-hidden>
            <span className="h-2.5 w-2.5 rounded-full bg-slate-300 dark:bg-slate-600" />
            <span className="h-2.5 w-2.5 rounded-full bg-slate-300 dark:bg-slate-600" />
            <span className="h-2.5 w-2.5 rounded-full bg-slate-300 dark:bg-slate-600" />
          </span>
          <span className="mx-auto truncate rounded-md bg-muted px-3 py-0.5 text-[11px] text-muted-foreground">
            Pantalla del salón, en vivo
          </span>
          <button
            type="button"
            onClick={() => setPausada((v) => !v)}
            className="inline-flex shrink-0 items-center gap-1 rounded-md px-2 py-0.5 text-[11px] font-semibold text-muted-foreground hover:bg-muted hover:text-foreground"
          >
            {pausada ? <Play size={12} aria-hidden /> : <Pause size={12} aria-hidden />}
            {pausada ? 'Reanudar' : 'Pausar'}
          </button>
        </div>
        <TableroGanadores estado={{ ...TABLERO_MUESTRA, filas, lote }} logoUrl="" />
      </div>
      <figcaption className="sr-only">
        Vista de la pantalla del sorteo: números de empleado con sus ganadores.
      </figcaption>
    </figure>
  )
}

export function LandingPage() {
  return (
    <div className="min-h-dvh bg-background text-foreground">
      <header className="sticky top-0 z-20 border-b border-border bg-background/85 backdrop-blur">
        <div className="mx-auto flex max-w-6xl items-center justify-between gap-4 px-4 py-3 sm:px-6">
          <Link to="/" className="flex items-center" aria-label="Sorteo360, inicio">
            <img src="/logo-brand.svg" alt="Sorteo360" className="h-9 w-auto dark:hidden" />
            <img
              src="/logo-brand-dark.svg"
              alt="Sorteo360"
              className="hidden h-9 w-auto dark:block"
            />
          </Link>
          <nav
            aria-label="Secciones"
            className="hidden gap-6 text-sm font-medium text-muted-foreground md:flex"
          >
            <a href="#funciones" className="hover:text-foreground">
              Funciones
            </a>
            <a href="#como-funciona" className="hover:text-foreground">
              Cómo funciona
            </a>
            <a href="#planes" className="hover:text-foreground">
              Planes
            </a>
            <a href="#preguntas" className="hover:text-foreground">
              Preguntas
            </a>
          </nav>
          <div className="flex items-center gap-2">
            <Link
              to="/app"
              className="rounded-lg px-4 py-2 text-sm font-bold hover:bg-muted focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand"
            >
              Entrar
            </Link>
            <CtaRegistro
              className="rounded-lg bg-brand px-5 py-2 text-sm font-bold text-(--brand-primary-contrast) hover:bg-brand-deep focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand"
              registro="Crear cuenta"
              contacto="Solicitar demo"
            />
          </div>
        </div>
      </header>

      <main>
        <section className="mx-auto grid max-w-6xl items-center gap-12 px-4 py-16 sm:px-6 md:py-24 lg:grid-cols-[0.9fr_1.1fr]">
          <div>
            <p className="text-sm font-bold uppercase tracking-widest text-brand dark:text-brand-accent">
              Sorteos para fiestas y eventos corporativos
            </p>
            <h1 className="mt-4 text-4xl font-black leading-[1.05] tracking-tight text-balance lg:text-5xl">
              El sorteo de tu fiesta, en vivo y con acta.
            </h1>
            <p className="mt-5 max-w-xl text-lg text-pretty text-muted-foreground">
              Sorteo360 lleva la rifa de principio a fin: el listado de invitados, el check-in en la
              puerta, el giro en pantalla grande, la entrega de cada premio y el acta para el
              notario.
            </p>
            <div className="mt-8 flex flex-wrap gap-3">
              <CtaRegistro
                className="inline-flex items-center gap-2 rounded-lg bg-brand px-6 py-3 font-bold text-(--brand-primary-contrast) hover:bg-brand-deep focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand"
                registro={
                  <>
                    Crear cuenta gratis <ArrowRight size={18} />
                  </>
                }
                contacto={
                  <>
                    Solicitar una demo <ArrowRight size={18} />
                  </>
                }
              />
              <a
                href="#planes"
                className="inline-flex items-center rounded-lg border border-border px-6 py-3 font-bold hover:bg-muted"
              >
                Ver planes
              </a>
            </div>
          </div>
          <VentanaApp />
        </section>

        {/* Lo que la plataforma hace, en cifras del producto (no clientes inventados). */}
        <section aria-label="En cifras" className="border-y border-border bg-card">
          <dl className="mx-auto grid max-w-6xl grid-cols-2 px-4 sm:px-6 md:grid-cols-4 md:divide-x md:divide-border">
            {HECHOS.map(([cifra, texto]) => (
              <div key={texto} className="px-2 py-6 md:px-6">
                <dt className="text-2xl font-black tracking-tight tabular-nums">{cifra}</dt>
                <dd className="mt-1 text-sm text-muted-foreground">{texto}</dd>
              </div>
            ))}
          </dl>
        </section>

        <section id="funciones" className="scroll-mt-20 border-t border-border bg-card">
          <div className="mx-auto max-w-6xl px-4 py-16 sm:px-6 md:py-20">
            <h2 className="max-w-2xl text-3xl font-black text-balance">
              Todo lo que pasa entre la lista de invitados y el último premio
            </h2>
            {/* Rejilla con ritmo: la función central grande y en color, cuatro chicas a su lado y
                una franja ancha al final. Seis tarjetas iguales leen a plantilla. */}
            <ul className="mt-10 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              {FUNCIONES.map((f, i) => (
                <li
                  key={f.titulo}
                  className={cn(
                    'rounded-2xl border p-6',
                    i === 0
                      ? 'flex flex-col border-transparent bg-brand-deep text-white sm:col-span-2 lg:row-span-2 lg:p-8'
                      : i === FUNCIONES.length - 1
                        ? 'border-border bg-background sm:col-span-2 lg:col-span-4 lg:flex lg:items-center lg:gap-6'
                        : 'border-border bg-background',
                  )}
                >
                  <span
                    className={cn(
                      'inline-flex h-11 w-11 shrink-0 items-center justify-center rounded-xl',
                      i === 0
                        ? 'bg-white/10 text-white'
                        : 'bg-brand/10 text-brand dark:text-brand-accent',
                    )}
                  >
                    {f.icono}
                  </span>
                  {i === 0 && (
                    <TableroGanadores
                      estado={PRINCIPAL_MUESTRA}
                      logoUrl=""
                      className="mt-6 overflow-hidden rounded-xl shadow-2xl ring-1 ring-white/10"
                    />
                  )}
                  <div>
                    <h3
                      className={cn(
                        'font-bold',
                        i === 0 ? 'mt-6 text-2xl lg:text-3xl' : 'mt-4 text-lg lg:mt-4',
                        i === FUNCIONES.length - 1 && 'lg:mt-0',
                      )}
                    >
                      {f.titulo}
                    </h3>
                    <p
                      className={cn(
                        'mt-2 max-w-[60ch] text-sm leading-relaxed',
                        i === 0 ? 'text-white/75 lg:text-base' : 'text-muted-foreground',
                      )}
                    >
                      {f.texto}
                    </p>
                  </div>
                </li>
              ))}
            </ul>
          </div>
        </section>

        <section id="como-funciona" className="scroll-mt-20">
          <div className="mx-auto max-w-6xl px-4 py-16 sm:px-6 md:py-20">
            <h2 className="text-3xl font-black">Cómo funciona</h2>
            <ol className="mt-10 grid gap-6 md:grid-cols-4">
              {PASOS.map(([titulo, texto]) => (
                <li key={titulo} className="relative border-t-4 border-brand pt-5">
                  <h3 className="text-xl font-bold">{titulo}</h3>
                  <p className="mt-2 text-sm leading-relaxed text-muted-foreground">{texto}</p>
                </li>
              ))}
            </ol>
          </div>
        </section>

        <PlanesSeccion />

        <section id="salon" className="scroll-mt-20 px-4 py-16 sm:px-6 md:py-24">
          <div className="mx-auto flex max-w-6xl flex-col gap-6 rounded-3xl bg-brand-deep p-8 text-white md:flex-row md:items-center md:p-12">
            <span className="inline-flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl bg-white/10">
              <WifiOff size={28} />
            </span>
            <div className="flex-1">
              <h2 className="text-2xl font-black sm:text-3xl">El salón no necesita internet</h2>
              <p className="mt-2 max-w-2xl text-white/75">
                Sorteo360 corre en una laptop dentro de la red local del salón: la puerta, la
                consola y el proyector siguen funcionando aunque no haya conexión afuera.
              </p>
            </div>
            <CtaRegistro
              className="inline-flex items-center gap-2 self-start rounded-lg bg-white px-6 py-3 font-bold text-brand-deep hover:bg-white/90 md:self-center"
              registro={
                <>
                  Empezar gratis <ArrowRight size={18} />
                </>
              }
              contacto={
                <>
                  Escríbenos <ArrowRight size={18} />
                </>
              }
            />
          </div>
        </section>

        <section id="preguntas" className="scroll-mt-20 border-t border-border">
          <div className="mx-auto grid max-w-6xl gap-10 px-4 py-16 sm:px-6 md:py-20 lg:grid-cols-[1fr_1.6fr]">
            <h2 className="text-3xl font-black text-balance">Preguntas frecuentes</h2>
            <div className="divide-y divide-border border-y border-border">
              {PREGUNTAS.map(([pregunta, respuesta]) => (
                <details key={pregunta} className="group py-5">
                  <summary className="flex cursor-pointer list-none items-center justify-between gap-4 font-semibold [&::-webkit-details-marker]:hidden">
                    {pregunta}
                    <Plus
                      size={18}
                      aria-hidden
                      className="shrink-0 text-muted-foreground transition-transform group-open:rotate-45"
                    />
                  </summary>
                  <p className="mt-3 max-w-[65ch] text-sm leading-relaxed text-muted-foreground">
                    {respuesta}
                  </p>
                </details>
              ))}
            </div>
          </div>
        </section>
      </main>

      <footer className="border-t border-border">
        <div className="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-2 px-4 py-6 text-sm text-muted-foreground sm:px-6">
          <span>© {new Date().getFullYear()} Sorteo360</span>
          <nav aria-label="Legal" className="flex gap-4">
            <Link to="/terminos" className="hover:text-foreground">
              Términos de uso
            </Link>
            <Link to="/privacidad" className="hover:text-foreground">
              Privacidad
            </Link>
          </nav>
        </div>
      </footer>
    </div>
  )
}
