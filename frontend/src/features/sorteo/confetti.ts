/**
 * Confetti utility & Celebration Sound — dispara una explosión de confetti y sonido triunfal al revelar ganador.
 * Usa canvas-confetti y Web Audio API (100% nativo, sin dependencias externas de audio ni archivos).
 */
import confetti from 'canvas-confetti'
import { getBrandPalette } from '@/lib/branding'

/** Generador de efectos de audio para celebración y fanfarria triunfal */
class CelebrationAudio {
  private ctx: AudioContext | null = null

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

  playFanfare() {
    try {
      const ctx = this.getContext()
      if (!ctx) return

      const now = ctx.currentTime

      // Fanfarria triunfal estilo evento institucional (Do, Mi, Sol, Do alto, Mi alto)
      const fanfareSequence = [
        { freq: 523.25, time: 0.0, dur: 0.12, type: 'triangle' as OscillatorType, vol: 0.25 }, // C5
        { freq: 659.25, time: 0.12, dur: 0.12, type: 'triangle' as OscillatorType, vol: 0.25 }, // E5
        { freq: 783.99, time: 0.24, dur: 0.14, type: 'triangle' as OscillatorType, vol: 0.28 }, // G5
        { freq: 1046.5, time: 0.38, dur: 0.55, type: 'triangle' as OscillatorType, vol: 0.35 }, // C6 (Larga)
        { freq: 1318.51, time: 0.52, dur: 0.65, type: 'sine' as OscillatorType, vol: 0.28 }, // E6 brillante
      ]

      fanfareSequence.forEach((note) => {
        const osc = ctx.createOscillator()
        const gain = ctx.createGain()

        osc.type = note.type
        osc.frequency.setValueAtTime(note.freq, now + note.time)

        gain.gain.setValueAtTime(0.001, now + note.time)
        gain.gain.exponentialRampToValueAtTime(note.vol, now + note.time + 0.02)
        gain.gain.exponentialRampToValueAtTime(0.001, now + note.time + note.dur)

        osc.connect(gain)
        gain.connect(ctx.destination)

        osc.start(now + note.time)
        osc.stop(now + note.time + note.dur + 0.05)
      })

      // Efecto secundario de campana / brillo de celebración (chime)
      const chimes = [1567.98, 2093.0, 2637.02, 3135.96]
      chimes.forEach((f, idx) => {
        const osc = ctx.createOscillator()
        const gain = ctx.createGain()

        osc.type = 'sine'
        osc.frequency.setValueAtTime(f, now + 0.4 + idx * 0.06)

        gain.gain.setValueAtTime(0.12, now + 0.4 + idx * 0.06)
        gain.gain.exponentialRampToValueAtTime(0.001, now + 0.4 + idx * 0.06 + 0.4)

        osc.connect(gain)
        gain.connect(ctx.destination)

        osc.start(now + 0.4 + idx * 0.06)
        osc.stop(now + 0.4 + idx * 0.06 + 0.45)
      })
    } catch {
      // Audio no disponible
    }
  }
}

const celebrationSound = new CelebrationAudio()

// canvas-confetti solo entiende hex: el blanco y el dorado son los brillos de cualquier marca.
const BRILLOS = ['#ffffff', '#fcd34d']

/** Explosión grande al revelar al ganador: varias olas durante ~6 s, desde el centro y los lados. */
export function fireConfetti() {
  celebrationSound.playFanfare()

  const colors = [...getBrandPalette(), ...BRILLOS]
  const comun = { colors, shapes: ['square', 'circle', 'star'] as confetti.Shape[], scalar: 1.1 }

  // Primera ola: explosión central a todo lo ancho.
  confetti({ ...comun, particleCount: 220, spread: 120, startVelocity: 55, origin: { y: 0.6 } })
  // Dos cañones desde abajo a los lados, un instante después.
  window.setTimeout(() => {
    confetti({
      ...comun,
      particleCount: 120,
      angle: 60,
      spread: 70,
      startVelocity: 65,
      origin: { x: 0, y: 0.9 },
    })
    confetti({
      ...comun,
      particleCount: 120,
      angle: 120,
      spread: 70,
      startVelocity: 65,
      origin: { x: 1, y: 0.9 },
    })
  }, 350)
  // Lluvia desde arriba.
  window.setTimeout(() => {
    confetti({
      ...comun,
      particleCount: 160,
      spread: 160,
      startVelocity: 30,
      gravity: 0.8,
      origin: { y: 0 },
    })
  }, 900)

  // Chorro continuo desde los costados.
  const end = Date.now() + 6000
  function frame() {
    confetti({ ...comun, particleCount: 7, angle: 60, spread: 60, origin: { x: 0, y: 0.7 } })
    confetti({ ...comun, particleCount: 7, angle: 120, spread: 60, origin: { x: 1, y: 0.7 } })
    if (Date.now() < end) requestAnimationFrame(frame)
  }
  frame()
}

/** Una ráfaga más corta: al confirmar que el ganador está en la sala. */
export function fireBurst() {
  celebrationSound.playFanfare()
  const colors = [...getBrandPalette(), ...BRILLOS]
  confetti({ colors, particleCount: 160, spread: 100, startVelocity: 50, origin: { y: 0.65 } })
  window.setTimeout(() => {
    confetti({ colors, particleCount: 80, angle: 60, spread: 65, origin: { x: 0, y: 0.8 } })
    confetti({ colors, particleCount: 80, angle: 120, spread: 65, origin: { x: 1, y: 0.8 } })
  }, 250)
}
export { celebrationSound }
