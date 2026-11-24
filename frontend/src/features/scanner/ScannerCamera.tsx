import { useEffect, useRef, useState } from 'react'
import type { BarcodeDetector } from 'barcode-detector/ponyfill'
import zxingWasmUrl from 'zxing-wasm/reader/zxing_reader.wasm?url'
import { CameraOff, Loader2 } from 'lucide-react'
import { freshCodes } from './repeatGuard'

/** Cada cuánto se mira un fotograma. Más seguido solo gasta batería: la cámara repite igual. */
const DETECT_EVERY_MS = 200

/** Cuánto queda en verde el visor tras leer un código. */
const FLASH_MS = 600

type CameraState = 'starting' | 'running' | 'denied' | 'unavailable'

type QrDetector = Pick<BarcodeDetector, 'detect'>

let detector: Promise<QrDetector> | null = null

/**
 * El `BarcodeDetector` nativo donde el navegador lo tiene con QR (Android Chrome), y si no el de
 * zxing-wasm (iOS Safari), que se descarga solo entonces. El `.wasm` sale de este mismo origen, no
 * de un CDN (spec convocatorias/10 §6). Se arma una vez por carga de la página.
 */
function qrDetector(): Promise<QrDetector> {
  detector ??= (async () => {
    const Native = (globalThis as { BarcodeDetector?: typeof BarcodeDetector }).BarcodeDetector
    if (Native && (await Native.getSupportedFormats()).includes('qr_code')) {
      return new Native({ formats: ['qr_code'] })
    }
    const { BarcodeDetector: Ponyfill, prepareZXingModule } =
      await import('barcode-detector/ponyfill')
    prepareZXingModule({
      overrides: {
        locateFile: (path: string, prefix: string) =>
          path.endsWith('.wasm') ? zxingWasmUrl : prefix + path,
      },
    })
    return new Ponyfill({ formats: ['qr_code'] })
  })()
  // Un fallo (el `.wasm` no llegó) se reintenta la próxima vez que se abra la cámara
  detector.catch(() => (detector = null))
  return detector
}

/**
 * La cámara trasera, leyendo QR mientras está montada. A `onRead` llega cada código una sola vez
 * mientras siga a la vista (`freshCodes`). En pausa la cámara sigue abierta pero no lee: así no hay
 * que volver a pedirla, que en iOS puede volver a preguntar el permiso.
 */
export function ScannerCamera({
  onRead,
  paused = false,
}: {
  onRead: (text: string) => void
  paused?: boolean
}) {
  const videoRef = useRef<HTMLVideoElement>(null)
  const onReadRef = useRef(onRead)
  const pausedRef = useRef(paused)
  useEffect(() => {
    onReadRef.current = onRead
    pausedRef.current = paused
  }, [onRead, paused])
  const [state, setState] = useState<CameraState>('starting')
  const [flashing, setFlashing] = useState(false)

  useEffect(() => {
    let stopped = false
    let stream: MediaStream | null = null
    let timer: ReturnType<typeof setTimeout> | undefined
    let flashTimer: ReturnType<typeof setTimeout> | undefined
    const lastSeen = new Map<string, number>()

    // Ante cualquier fallo la cámara se suelta: nunca queda encendida con un aviso de que no anda
    const fail = (next: CameraState) => {
      stream?.getTracks().forEach((t) => t.stop())
      if (!stopped) setState(next)
    }

    async function start() {
      if (!navigator.mediaDevices?.getUserMedia) return fail('unavailable')
      try {
        stream = await navigator.mediaDevices.getUserMedia({
          video: { facingMode: 'environment' },
          audio: false,
        })
      } catch (e) {
        return fail(
          e instanceof DOMException && e.name === 'NotAllowedError' ? 'denied' : 'unavailable',
        )
      }
      const video = videoRef.current
      if (stopped || !video) return fail('unavailable')
      video.srcObject = stream
      let qr: QrDetector
      try {
        await video.play()
        qr = await qrDetector()
      } catch {
        return fail('unavailable')
      }
      if (stopped) return
      setState('running')

      const tick = async () => {
        if (stopped) return
        if (!pausedRef.current && video.readyState >= HTMLMediaElement.HAVE_CURRENT_DATA) {
          try {
            const codes = (await qr.detect(video)).map((c) => c.rawValue).filter(Boolean)
            const fresh = freshCodes(lastSeen, codes, Date.now())
            if (fresh.length > 0 && !stopped) {
              // El visor confirma la lectura en la cámara misma; el resultado sale en la tarjeta
              setFlashing(true)
              clearTimeout(flashTimer)
              flashTimer = setTimeout(() => setFlashing(false), FLASH_MS)
            }
            for (const code of fresh) {
              if (!stopped) onReadRef.current(code)
            }
          } catch {
            // Un fotograma que no se pudo leer: se prueba con el siguiente
          }
        }
        if (!stopped) timer = setTimeout(tick, DETECT_EVERY_MS)
      }
      void tick()
    }

    void start()
    return () => {
      stopped = true
      clearTimeout(timer)
      clearTimeout(flashTimer)
      stream?.getTracks().forEach((t) => t.stop())
    }
  }, [])

  return (
    <div className="relative mx-auto w-full aspect-square max-w-72 rounded-3xl overflow-hidden bg-slate-900 shadow-lg shadow-blue-900/10">
      <video
        ref={videoRef}
        muted
        playsInline
        aria-label="Cámara"
        className={`h-full w-full object-cover ${state === 'running' ? '' : 'invisible'}`}
      />
      {state === 'running' && <Viewfinder flashing={flashing} />}
      {state !== 'running' && (
        <div className="absolute inset-0 flex flex-col items-center justify-center gap-2 p-6 text-center text-xs font-semibold text-slate-200">
          {state === 'starting' ? (
            <>
              <Loader2 className="h-6 w-6 animate-spin" />
              Abriendo la cámara…
            </>
          ) : (
            <>
              <CameraOff className="h-7 w-7 text-slate-400" />
              {state === 'denied'
                ? 'Sin permiso para usar la cámara. Actívelo en el navegador, o escriba el código abajo.'
                : 'La cámara no está disponible en este dispositivo. Escriba el código abajo o use un lector.'}
            </>
          )}
        </div>
      )}
    </div>
  )
}

/**
 * La guía para apuntar, encima del video: el marco, la línea y las esquinas del visor anterior. Es
 * solo para el ojo: el lector mira el cuadro entero, así que un código fuera del marco también se lee.
 */
function Viewfinder({ flashing }: { flashing: boolean }) {
  const corner = `absolute w-5 h-5 transition-colors duration-150 ${
    flashing ? 'border-emerald-400' : 'border-blue-600'
  }`
  return (
    <div
      aria-hidden="true"
      className={`absolute inset-0 pointer-events-none rounded-3xl transition-shadow duration-150 ${
        flashing ? 'shadow-[inset_0_0_0_4px_var(--color-emerald-400)]' : ''
      }`}
    >
      <div
        className={`absolute inset-4 border rounded-2xl transition-colors duration-150 ${
          flashing ? 'border-emerald-300' : 'border-blue-200'
        }`}
      />
      <div className="absolute inset-x-8 top-1/2 h-0.5 -translate-y-1/2 bg-linear-to-r from-transparent via-blue-500 to-transparent motion-safe:animate-pulse" />
      <div className={`${corner} top-5 left-5 border-t-2 border-l-2 rounded-tl-lg`} />
      <div className={`${corner} top-5 right-5 border-t-2 border-r-2 rounded-tr-lg`} />
      <div className={`${corner} bottom-5 left-5 border-b-2 border-l-2 rounded-bl-lg`} />
      <div className={`${corner} bottom-5 right-5 border-b-2 border-r-2 rounded-br-lg`} />
    </div>
  )
}
