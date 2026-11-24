import type { ReactNode } from 'react'

/**
 * El marco navideño del tablero: una guirnalda de pino con luces que cuelga en tramos bajo la
 * franja de arriba y baja por los dos costados, con bolas grandes colgando en las esquinas de
 * arriba. Solo decora: no recibe clics y los lectores de pantalla la saltan. Medidas en `cqw`,
 * como el resto del tablero (16:9: 100cqw de ancho por 56,25 de alto; la franja mide 9).
 */

const DORADO = 'oklch(0.8 0.14 85)'
const ROJO = 'oklch(0.55 0.21 25)'
const AZUL = 'oklch(0.62 0.15 235)'
const PINO = ['oklch(0.36 0.09 155)', 'oklch(0.45 0.11 150)', 'oklch(0.52 0.12 145)']
const LUZ = 'oklch(0.93 0.12 90)'

/** Azar fijo: la guirnalda se dibuja igual en la consola y en el proyector. */
const azar = (i: number) => {
  const x = Math.sin(i * 12.9898) * 43758.5453
  return x - Math.floor(x)
}

/** Ramitas de pino alrededor de un punto: lo que da volumen a la guirnalda. */
function Ramillete({
  x,
  y,
  semilla,
  largo,
}: {
  x: number
  y: number
  semilla: number
  largo: number
}) {
  return (
    <g>
      {Array.from({ length: 11 }, (_, k) => {
        const a = azar(semilla * 7 + k) * Math.PI * 2
        const l = largo * (0.6 + azar(semilla * 13 + k) * 0.5)
        return (
          <line
            key={k}
            x1={x}
            y1={y}
            x2={x + Math.cos(a) * l}
            y2={y + Math.sin(a) * l}
            stroke={PINO[(semilla + k) % PINO.length]}
            strokeWidth={largo * 0.28}
            strokeLinecap="round"
          />
        )
      })}
    </g>
  )
}

/** Una bola de Navidad con su tapita, brillo y reflejo. */
function Bola({ x, y, r, color }: { x: number; y: number; r: number; color: string }) {
  return (
    <g transform={`translate(${x} ${y})`}>
      <rect
        x={-r * 0.3}
        y={-r * 1.2}
        width={r * 0.6}
        height={r * 0.32}
        rx={r * 0.08}
        fill={DORADO}
      />
      <circle r={r} fill={color} />
      <circle r={r} fill="url(#brillo-bola)" />
      <path
        d={`M ${-r * 0.75} ${-r * 0.05} Q 0 ${r * 0.35} ${r * 0.75} ${-r * 0.05}`}
        stroke="white"
        strokeOpacity={0.35}
        strokeWidth={r * 0.08}
        fill="none"
      />
      <ellipse
        cx={-r * 0.35}
        cy={-r * 0.4}
        rx={r * 0.22}
        ry={r * 0.13}
        fill="white"
        opacity={0.6}
      />
    </g>
  )
}

/** Un moño rojo, para los puntos de donde cuelga la guirnalda. */
function Mono({ x, y, s }: { x: number; y: number; s: number }) {
  return (
    <g transform={`translate(${x} ${y}) scale(${s})`}>
      <path d="M0 0 C -14 -10, -22 4, -6 4 Z" fill={ROJO} />
      <path d="M0 0 C 14 -10, 22 4, 6 4 Z" fill={ROJO} />
      <path d="M-2 2 L -9 16 L -4 14 L -1 18 Z" fill={ROJO} />
      <path d="M2 2 L 9 16 L 4 14 L 1 18 Z" fill={ROJO} />
      <circle r={3.4} fill="oklch(0.45 0.19 25)" />
    </g>
  )
}

/** Puntos a lo largo de una curva cuadrática, para poner ramilletes y luces. */
function puntos(x0: number, y0: number, cx: number, cy: number, x1: number, y1: number, n: number) {
  return Array.from({ length: n + 1 }, (_, i) => {
    const t = i / n
    const u = 1 - t
    return {
      x: u * u * x0 + 2 * u * t * cx + t * t * x1,
      y: u * u * y0 + 2 * u * t * cy + t * t * y1,
    }
  })
}

/** La guirnalda de arriba: cinco tramos que cuelgan entre moños, con bolas en las puntas. */
function GuirnaldaSuperior() {
  // viewBox de 1000 × 120: la misma proporción 100:12 que su caja, sin deformar.
  const anclas = [0, 200, 400, 600, 800, 1000]
  const ramilletes: ReactNode[] = []
  const luces: ReactNode[] = []
  anclas.slice(0, -1).forEach((x0, s) => {
    const x1 = anclas[s + 1]
    const tramo = puntos(x0, 4, (x0 + x1) / 2, 50, x1, 4, 34)
    // La cuerda de pino oscura debajo da cuerpo; los ramilletes, el borde con agujas.
    ramilletes.push(
      <path
        key={`c${s}`}
        d={`M ${x0} 4 Q ${(x0 + x1) / 2} 50 ${x1} 4`}
        stroke={PINO[0]}
        strokeWidth={14}
        strokeLinecap="round"
        fill="none"
      />,
    )
    tramo.forEach((p, i) => {
      const id = s * 100 + i
      ramilletes.push(<Ramillete key={`r${id}`} x={p.x} y={p.y} semilla={id} largo={12} />)
      if (i % 3 === 1)
        luces.push(<circle key={`l${id}`} cx={p.x} cy={p.y + 3} r={2.6} fill={LUZ} />)
    })
  })
  // Las bolas grandes van en los extremos, donde no tapan el contenido.
  // Dentro de las primeras y últimas 9cqw: el texto del premio empieza más adentro.
  const colgantes = [
    { x: 42, largo: 96, r: 26, color: ROJO },
    { x: 86, largo: 58, r: 19, color: DORADO },
    { x: 914, largo: 58, r: 19, color: AZUL },
    { x: 958, largo: 96, r: 26, color: DORADO },
  ]
  return (
    <svg viewBox="0 0 1000 120" className="h-full w-full overflow-visible" aria-hidden>
      {colgantes.map((c) => (
        <g key={c.x}>
          <line x1={c.x} y1={18} x2={c.x} y2={c.largo - c.r} stroke={DORADO} strokeWidth={1.4} />
          <Bola x={c.x} y={c.largo} r={c.r} color={c.color} />
        </g>
      ))}
      {ramilletes}
      {luces}
      {anclas.map((x) => (
        <Mono key={x} x={x} y={6} s={0.9} />
      ))}
    </svg>
  )
}

/** Una guirnalda que baja por un costado, con luces y bolas pequeñas cada tanto. */
function GuirnaldaLateral({ lado }: { lado: 'izquierda' | 'derecha' }) {
  // viewBox de 30 × 480: la proporción de su caja (2,8 × 47cqw: el alto bajo la franja).
  const semilla = lado === 'izquierda' ? 1000 : 2000
  const pasos = Array.from({ length: 60 }, (_, i) => i)
  return (
    <svg
      viewBox="0 0 30 480"
      preserveAspectRatio="xMidYMin slice"
      className="h-full w-full overflow-visible"
      aria-hidden
    >
      <line
        x1={15}
        y1={0}
        x2={15}
        y2={480}
        stroke={PINO[0]}
        strokeWidth={9}
        strokeLinecap="round"
      />
      {pasos.map((i) => (
        <Ramillete
          key={i}
          x={15 + Math.sin(i * 0.9) * 2}
          y={i * 8 + 4}
          semilla={semilla + i}
          largo={10}
        />
      ))}
      {pasos
        .filter((i) => i % 3 === 1)
        .map((i) => (
          <circle key={`l${i}`} cx={15 + Math.sin(i) * 4} cy={i * 8 + 6} r={2.4} fill={LUZ} />
        ))}
      {[90, 210, 330, 440].map((y, k) => (
        <Bola
          key={y}
          x={lado === 'izquierda' ? 21 : 9}
          y={y}
          r={9}
          color={[ROJO, DORADO, AZUL, ROJO][k]}
        />
      ))}
    </svg>
  )
}

export function DecoracionNavidena() {
  return (
    <div className="pointer-events-none absolute inset-0 z-20" aria-hidden>
      <svg width="0" height="0" className="absolute">
        <defs>
          <radialGradient id="brillo-bola" cx="35%" cy="30%" r="75%">
            <stop offset="0%" stopColor="white" stopOpacity="0.4" />
            <stop offset="55%" stopColor="white" stopOpacity="0" />
            <stop offset="100%" stopColor="black" stopOpacity="0.3" />
          </radialGradient>
        </defs>
      </svg>
      {/* Pegada al borde de abajo de la franja (9cqw), a todo el ancho. */}
      <div className="absolute inset-x-0 top-[8.4cqw] h-[12cqw]">
        <GuirnaldaSuperior />
      </div>
      <div className="absolute bottom-0 left-0 top-[9cqw] w-[2.8cqw]">
        <GuirnaldaLateral lado="izquierda" />
      </div>
      <div className="absolute bottom-0 right-0 top-[9cqw] w-[2.8cqw]">
        <GuirnaldaLateral lado="derecha" />
      </div>
    </div>
  )
}
