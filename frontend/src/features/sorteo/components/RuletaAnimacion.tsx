import { useState, useRef, useEffect, useCallback, useMemo } from 'react'
import type { EligibleParticipantResponse, RaffleVisualStyle } from '@/api/types'
import { getBrandColors, getBrandPalette, mixHex } from '@/lib/branding'

/**
 * La animación no elige a nadie: el ganador lo sortea el servicio, y la ruleta, el bombo o la
 * ráfaga de nombres van hacia él (spec raffles/02 §2). Tampoco hay azar en el movimiento, para que
 * nada aleatorio en la pantalla pueda confundirse con un sorteo.
 */
interface RuletaAnimacionProps {
  /** El bombo del premio, en el orden en que el servicio lo numera. */
  participantes: EligibleParticipantResponse[]
  /** El ganador que devolvió el servicio. La animación arranca cuando hay uno y `running`. */
  winner: EligibleParticipantResponse | null
  onComplete: () => void
  running: boolean
  modo?: RaffleVisualStyle
  duracionSec?: number
  /** Cuántos hay en el bombo: `participantes` puede ser solo una muestra (R14). */
  total?: number
}

// Las porciones y los bordes salen de la marca (getBrandPalette), así cambian con ella.
const BLANCO = '#ffffff' // el contraste fijo de los bordes y los textos sobre las porciones

// Audio sintetizado con Web Audio API (sin dependencias externas de red o archivos)
/**
 * Cuánto se amplifica todo el sonido de la ruleta. Pasa por un compresor, así que subirlo no
 * satura: los picos se aplanan en vez de distorsionar en los parlantes del salón.
 */
const VOLUMEN_RULETA = 3.5

class SoundEffects {
  private ctx: AudioContext | null = null
  private out: AudioNode | null = null

  /** La salida común: volumen general y compresor, en ese orden, hacia los parlantes. */
  private salida(ctx: AudioContext): AudioNode {
    if (!this.out) {
      const volumen = ctx.createGain()
      volumen.gain.value = VOLUMEN_RULETA
      const compresor = ctx.createDynamicsCompressor()
      compresor.threshold.value = -10
      compresor.knee.value = 6
      compresor.ratio.value = 8
      volumen.connect(compresor)
      compresor.connect(ctx.destination)
      this.out = volumen
    }
    return this.out
  }

  private getContext(): AudioContext | null {
    if (typeof window === 'undefined') return null
    if (!this.ctx) {
      const AudioCtx =
        window.AudioContext ||
        (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext
      if (AudioCtx) {
        this.ctx = new AudioCtx()
      }
    }
    if (this.ctx && this.ctx.state === 'suspended') {
      this.ctx.resume().catch(() => {})
    }
    return this.ctx
  }

  playTick(pitchModifier = 1) {
    try {
      const ctx = this.getContext()
      if (!ctx) return
      const osc = ctx.createOscillator()
      const gain = ctx.createGain()

      osc.type = 'triangle'
      osc.frequency.setValueAtTime(550 * pitchModifier, ctx.currentTime)
      osc.frequency.exponentialRampToValueAtTime(100, ctx.currentTime + 0.04)

      gain.gain.setValueAtTime(0.15, ctx.currentTime)
      gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.04)

      osc.connect(gain)
      gain.connect(this.salida(ctx))

      osc.start()
      osc.stop(ctx.currentTime + 0.04)
    } catch {
      // Audio no disponible
    }
  }

  playWin() {
    try {
      const ctx = this.getContext()
      if (!ctx) return
      const notes = [523.25, 659.25, 783.99, 1046.5] // Acorde Do Mayor
      notes.forEach((freq, idx) => {
        const osc = ctx.createOscillator()
        const gain = ctx.createGain()

        osc.type = 'sine'
        osc.frequency.setValueAtTime(freq, ctx.currentTime + idx * 0.08)

        gain.gain.setValueAtTime(0.2, ctx.currentTime + idx * 0.08)
        gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + idx * 0.08 + 0.4)

        osc.connect(gain)
        gain.connect(this.salida(ctx))

        osc.start(ctx.currentTime + idx * 0.08)
        osc.stop(ctx.currentTime + idx * 0.08 + 0.45)
      })
    } catch {
      // Audio no disponible
    }
  }
}

const sfx = new SoundEffects()

export function RuletaAnimacion({
  participantes: pool,
  winner,
  onComplete,
  running,
  modo: estilo = 'roulette',
  duracionSec = 6,
  total,
}: RuletaAnimacionProps) {
  const canvasRef = useRef<HTMLCanvasElement | null>(null)
  // Solo ruleta y nombres pasando: un sorteo guardado con la tómbola (que ya no se ofrece) gira
  // como ruleta.
  const modo = estilo === 'sequential_names' ? 'sequential_names' : 'roulette'

  const soundEnabled = true
  const [activeHighlight, setActiveHighlight] = useState<EligibleParticipantResponse | null>(null)
  const [needleBounce, setNeedleBounce] = useState(false)

  const animationFrameRef = useRef<number>(0)
  const currentAngleRef = useRef<number>(0)
  const isSpinningRef = useRef<boolean>(false)
  const lastTickIndexRef = useRef<number>(-1)

  // El winner siempre está entre lo que se ve, aunque la lista leída ya fuera vieja
  const participantes = useMemo(() => withWinner(pool, winner), [pool, winner])
  // Lista para la ruleta
  const displayedItems = useMemo(() => withWinner(pool.slice(0, 36), winner), [pool, winner])

  // Dibujar ruleta en Canvas con alta resolución
  const drawWheel = useCallback(
    (angle: number) => {
      const canvas = canvasRef.current
      if (!canvas) return
      const ctx = canvas.getContext('2d')
      if (!ctx) return

      const numSlices = displayedItems.length

      const dpr = window.devicePixelRatio || 1
      const isCompact = typeof window !== 'undefined' && window.innerHeight < 750
      const size = isCompact ? 390 : 470
      canvas.width = size * dpr
      canvas.height = size * dpr
      canvas.style.width = `${size}px`
      canvas.style.height = `${size}px`

      ctx.save()
      ctx.scale(dpr, dpr)
      ctx.clearRect(0, 0, size, size)

      const brand = getBrandColors()
      const sliceColors = getBrandPalette()

      const center = size / 2
      const radius = size / 2 - 16
      const sliceAngle = (2 * Math.PI) / numSlices

      const profundo = mixHex(brand.primary, '#000000', 0.45)

      // Anillo exterior en el tono oscuro de la marca, con una sombra que lo despega del fondo.
      ctx.save()
      ctx.shadowColor = 'rgba(0, 0, 0, 0.45)'
      ctx.shadowBlur = 24
      ctx.beginPath()
      ctx.arc(center, center, radius + 12, 0, 2 * Math.PI)
      ctx.fillStyle = profundo
      ctx.fill()
      ctx.restore()

      // Luces del borde: alternan blanco y el acento, con un brillo leve.
      const numPins = Math.max(numSlices, 16)
      for (let p = 0; p < numPins; p++) {
        const pinAngle = (p * 2 * Math.PI) / numPins + angle
        const px = center + (radius + 6) * Math.cos(pinAngle)
        const py = center + (radius + 6) * Math.sin(pinAngle)
        ctx.save()
        ctx.shadowColor = p % 2 === 0 ? BLANCO : brand.accent
        ctx.shadowBlur = 6
        ctx.beginPath()
        ctx.arc(px, py, 2.6, 0, 2 * Math.PI)
        ctx.fillStyle = p % 2 === 0 ? BLANCO : brand.accent
        ctx.fill()
        ctx.restore()
      }

      // Sin nombres, un disco liso: la ruleta conserva su tamaño en vez de colapsar
      if (numSlices === 0) {
        ctx.beginPath()
        ctx.arc(center, center, radius, 0, 2 * Math.PI)
        ctx.fillStyle = sliceColors[0]
        ctx.fill()
      }

      // Porciones de color plano, separadas por un filo fino.
      for (let i = 0; i < numSlices; i++) {
        const start = angle + i * sliceAngle
        const end = start + sliceAngle
        const base = sliceColors[i % sliceColors.length]

        ctx.beginPath()
        ctx.moveTo(center, center)
        ctx.arc(center, center, radius, start, end)
        ctx.closePath()

        ctx.fillStyle = base
        ctx.fill()

        ctx.lineWidth = 1
        ctx.strokeStyle = 'rgba(255, 255, 255, 0.55)'
        ctx.stroke()

        // El nombre, en blanco con una sombra corta para que se lea sobre cualquier tono.
        ctx.save()
        ctx.translate(center, center)
        ctx.rotate(start + sliceAngle / 2)
        ctx.textAlign = 'right'
        ctx.fillStyle = BLANCO
        ctx.shadowColor = 'rgba(0, 0, 0, 0.35)'
        ctx.shadowBlur = 3
        ctx.font = numSlices > 24 ? '600 11px Inter, sans-serif' : '700 13px Inter, sans-serif'

        const rawName = displayedItems[i].displayName
        const maxChars = numSlices > 20 ? 16 : 22
        const cleanName = rawName.length > maxChars ? `${rawName.substring(0, maxChars)}…` : rawName

        ctx.fillText(cleanName, radius - 22, 4)
        ctx.restore()
      }

      // Buje central en el color de la marca, con aro blanco y un punto al centro.
      ctx.save()
      ctx.shadowColor = 'rgba(0, 0, 0, 0.4)'
      ctx.shadowBlur = 12
      ctx.beginPath()
      ctx.arc(center, center, 36, 0, 2 * Math.PI)
      ctx.fillStyle = brand.primary
      ctx.fill()
      ctx.restore()
      ctx.lineWidth = 3
      ctx.strokeStyle = BLANCO
      ctx.stroke()
      ctx.beginPath()
      ctx.arc(center, center, 6, 0, 2 * Math.PI)
      ctx.fillStyle = BLANCO
      ctx.fill()
      // El buje va sin texto: el nombre de nadie compite con los participantes.

      ctx.restore()
    },
    [displayedItems],
  )

  // Girar la ruleta
  const startSpin = useCallback(() => {
    if (!winner || isSpinningRef.current) return

    isSpinningRef.current = true

    // Índice correspondiente en displayedItems
    const targetSliceIdx = displayedItems.findIndex((p) => p.attendeeId === winner.attendeeId)
    const sliceIndex = targetSliceIdx >= 0 ? targetSliceIdx : 0

    const numSlices = displayedItems.length
    const sliceAngle = (2 * Math.PI) / numSlices

    // La aguja apunta arriba (-PI/2)
    const targetCenterAngle = -Math.PI / 2 - (sliceIndex + 0.5) * sliceAngle

    const targetSeconds = Math.max(3, Math.min(30, duracionSec || 6))
    const fullSpins = Math.max(4, Math.floor(targetSeconds * 1.2))
    const startAngle = currentAngleRef.current
    const totalRotation =
      fullSpins * 2 * Math.PI + (targetCenterAngle - (startAngle % (2 * Math.PI)))

    const duration = targetSeconds * 1000
    const startTime = performance.now()

    const easeOutCubic = (t: number) => 1 - Math.pow(1 - t, 3)

    const animate = (time: number) => {
      const elapsed = time - startTime
      const progress = Math.min(elapsed / duration, 1)
      const easedProgress = easeOutCubic(progress)

      const currentAngle = startAngle + totalRotation * easedProgress
      currentAngleRef.current = currentAngle

      drawWheel(currentAngle)

      const normalizedAngle = ((currentAngle % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI)
      const pointerAngle = (3 * Math.PI) / 2
      const diff = (pointerAngle - normalizedAngle + 2 * Math.PI) % (2 * Math.PI)
      const currentActiveIdx = Math.floor(diff / sliceAngle) % numSlices

      if (currentActiveIdx !== lastTickIndexRef.current) {
        lastTickIndexRef.current = currentActiveIdx
        setActiveHighlight(displayedItems[currentActiveIdx])
        setNeedleBounce(true)
        setTimeout(() => setNeedleBounce(false), 50)

        if (soundEnabled) {
          sfx.playTick(1 + (1 - progress) * 0.5)
        }
      }

      if (progress < 1) {
        animationFrameRef.current = requestAnimationFrame(animate)
      } else {
        isSpinningRef.current = false
        setActiveHighlight(winner)
        if (soundEnabled) sfx.playWin()
        setTimeout(onComplete, 500)
      }
    }

    animationFrameRef.current = requestAnimationFrame(animate)
  }, [winner, displayedItems, drawWheel, onComplete, soundEnabled, duracionSec])

  // Modo Nombres Secuenciales (Ráfaga de nombres a alta velocidad con duración exacta)
  const startNombresSecuencialesSpin = useCallback(() => {
    if (!winner || isSpinningRef.current) return
    isSpinningRef.current = true

    const targetSeconds = Math.max(5, Math.min(30, duracionSec || 5))
    const duration = targetSeconds * 1000
    const startTime = performance.now()
    let lastTickTime = 0
    let tick = 0

    const animateSequential = (time: number) => {
      const elapsed = time - startTime
      const progress = Math.min(elapsed / duration, 1)

      // El intervalo entre cambios de nombres aumenta a medida que desacelera hacia el final
      const tickInterval = 30 + Math.pow(progress, 3) * 350

      if (time - lastTickTime > tickInterval) {
        lastTickTime = time
        tick++
        setActiveHighlight(participantes[tick % participantes.length])

        if (soundEnabled) {
          sfx.playTick(0.8 + progress * 0.6)
        }
      }

      if (progress < 1) {
        animationFrameRef.current = requestAnimationFrame(animateSequential)
      } else {
        isSpinningRef.current = false
        setActiveHighlight(winner)
        if (soundEnabled) sfx.playWin()
        setTimeout(onComplete, 400)
      }
    }

    animationFrameRef.current = requestAnimationFrame(animateSequential)
  }, [winner, participantes, soundEnabled, onComplete, duracionSec])

  useEffect(() => {
    if (running && !isSpinningRef.current) {
      if (modo === 'roulette') {
        startSpin()
      } else {
        startNombresSecuencialesSpin()
      }
    }
  }, [running]) // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (modo === 'roulette') {
      drawWheel(currentAngleRef.current)
    }
  }, [modo, drawWheel])

  useEffect(() => {
    return () => {
      if (animationFrameRef.current) cancelAnimationFrame(animationFrameRef.current)
      // Un giro cortado no sigue en curso: si el componente vuelve a montarse, arranca de nuevo
      isSpinningRef.current = false
    }
  }, [])

  return (
    <div className="w-full flex flex-col items-center select-none">
      {modo === 'roulette' ? (
        /* Vista Ruleta Circular Canvas */
        <div className="relative flex flex-col items-center">
          {/* Puntero Superior Sólido Blanco con Pin Navy */}
          <div
            className={`absolute top-0 z-20 -translate-y-2 transition-transform duration-75 ${
              needleBounce ? 'scale-110 -rotate-3' : 'scale-100 rotate-0'
            }`}
          >
            <div className="flex flex-col items-center">
              <div className="w-0 h-0 border-l-14 border-l-transparent border-r-14 border-r-transparent border-t-26 border-t-white" />
              <div className="w-3 h-3 rounded-full bg-slate-900 border-2 border-white -mt-1" />
            </div>
          </div>

          {/* Canvas de la ruleta */}
          <div className="p-3 rounded-full bg-slate-900 border-2 border-slate-700">
            <canvas ref={canvasRef} className="rounded-full block" />
          </div>
        </div>
      ) : (
        /* Vista Nombres Secuenciales Ráfaga */
        <div className="w-full max-w-2xl px-4 py-8 flex flex-col items-center justify-center text-center">
          <div className="min-h-40 flex flex-col items-center justify-center">
            <p className="text-3xl sm:text-5xl md:text-6xl font-black text-white tracking-tight drop-shadow-[0_4px_16px_rgba(0,0,0,0.8)] transition-all leading-tight">
              {activeHighlight?.displayName || 'Listo para sortear'}
            </p>
            {activeHighlight?.company ? (
              <p className="text-lg sm:text-2xl font-bold text-blue-300 drop-shadow-md mt-2">
                {activeHighlight.company}
              </p>
            ) : (
              <p className="text-sm font-semibold text-blue-200/80 mt-2">
                {(total ?? pool.length).toLocaleString('es-DO')} participantes elegibles en el pool
              </p>
            )}
          </div>
        </div>
      )}
    </div>
  )
}

/**
 * Los nombres a mostrar, con el ganador entre ellos. Puede faltar si no está entre los primeros que
 * caben, o si la lista leída quedó vieja antes del sorteo; entonces ocupa un lugar que depende de
 * su id, para que no caiga siempre en el mismo.
 */
function withWinner(
  list: EligibleParticipantResponse[],
  winner: EligibleParticipantResponse | null,
): EligibleParticipantResponse[] {
  if (!winner || list.some((p) => p.attendeeId === winner.attendeeId)) return list
  if (list.length === 0) return [winner]
  const slot = [...winner.attendeeId].reduce((sum, c) => sum + c.charCodeAt(0), 0) % list.length
  return list.map((p, i) => (i === slot ? winner : p))
}
