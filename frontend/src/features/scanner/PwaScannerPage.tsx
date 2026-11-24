import { toast } from 'sonner'
import { useEffect, useRef, useState, type FormEvent } from 'react'
import { QrCode, UserCheck, Download, ChevronRight, RefreshCw } from 'lucide-react'
import type { CheckInResponse, DoorEventResponse } from '@/api/types'
import { Button } from '@/components/ui/Button'
import { useCan } from '@/auth/AuthProvider'
import { ROLES } from '@/auth/roles'
import { classifyCode } from './classifyCode'
import { readStoredEventId, rememberedEvent, storeEventId } from './doorEvent'
import { DoorListSheet } from './DoorListSheet'
import { EventPicker } from './EventPicker'
import { useCheckIn, useDoorEvents, useDoorTotals } from './hooks'
import { ReadResult } from './ReadResult'
import { formatTime } from '@/lib/format'
import { useBranding } from '@/lib/branding'
import { describeRead, type Read, type ReadOutcome } from './readOutcome'
import { ScannerCamera } from './ScannerCamera'

// El evento beforeinstallprompt no viene tipado en lib.dom.
interface BeforeInstallPromptEvent extends Event {
  prompt: () => Promise<void>
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>
}

/** Cuántas lecturas recientes guarda el terminal. */
const RECENT_READS = 8

/** Una entrada que no contesta (el wifi de la puerta) no deja la tarjeta girando para siempre. */
const CHECK_IN_TIMEOUT_MS = 10_000

function withTimeout<T>(promise: Promise<T>): Promise<T> {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(
      () =>
        reject({
          message:
            'La entrada no respondió a tiempo. Revise la conexión y vuelva a leerla, o busque a la persona en la lista.',
        }),
      CHECK_IN_TIMEOUT_MS,
    )
    promise.then(resolve, reject).finally(() => clearTimeout(timer))
  })
}

/**
 * Con teclado físico (un portátil, un lector USB o Bluetooth que escribe como teclado) la caja de
 * texto tiene el foco siempre. En un teléfono no: abriría el teclado encima de la cámara.
 */
const hasKeyboard = () => window.matchMedia?.('(pointer: fine)').matches ?? false

/**
 * El terminal de la puerta (spec convocatorias/10): lee un código por la cámara o el teclado y dice
 * qué es. Una entrada hace check-in en el evento de este terminal, un código de premio se consulta
 * (solo con `raffles:draw`), y una referencia abre la lista de la puerta sin hacer check-in a nadie.
 */
export function PwaScannerPage() {
  const branding = useBranding()
  const doorEvents = useDoorEvents()
  const [storedId, setStoredId] = useState(readStoredEventId)
  const [eligiendo, setEligiendo] = useState(false)
  const evento = doorEvents.data ? rememberedEvent(doorEvents.data, storedId) : null
  const eventId = evento?.id ?? ''

  const prizeDesk = useCan(ROLES.rafflesDraw).allowed
  const checkIn = useCheckIn(eventId)
  const totals = useDoorTotals(eventId)

  const [texto, setTexto] = useState('')
  const [reads, setReads] = useState<Read[]>([])
  const nextReadId = useRef(0)
  const inputRef = useRef<HTMLInputElement>(null)
  const [lista, setLista] = useState<{ search: string } | null>(null)

  // Soporte para instalación PWA (beforeinstallprompt)
  const [deferredPrompt, setDeferredPrompt] = useState<BeforeInstallPromptEvent | null>(null)
  const [isStandalone, setIsStandalone] = useState(false)

  useEffect(() => {
    const isPwa =
      window.matchMedia('(display-mode: standalone)').matches ||
      (window.navigator as Navigator & { standalone?: boolean }).standalone
    setIsStandalone(!!isPwa)

    const handler = (e: Event) => {
      e.preventDefault()
      setDeferredPrompt(e as BeforeInstallPromptEvent)
    }
    window.addEventListener('beforeinstallprompt', handler)
    return () => window.removeEventListener('beforeinstallprompt', handler)
  }, [])

  const handleInstallClick = async () => {
    if (!deferredPrompt) {
      toast.info(
        'Para instalar la aplicación en iOS pulsa Compartir -> "Agregar a inicio", o en Android usa el menú del navegador -> "Instalar app".',
      )
      return
    }
    deferredPrompt.prompt()
    const { outcome } = await deferredPrompt.userChoice
    if (outcome === 'accepted') setDeferredPrompt(null)
  }

  const addRead = (outcome: ReadOutcome) => {
    const id = ++nextReadId.current
    setReads((prev) => [{ id, at: Date.now(), outcome }, ...prev].slice(0, RECENT_READS))
    return id
  }

  const settleRead = (id: number, outcome: ReadOutcome) =>
    setReads((prev) => prev.map((r) => (r.id === id ? { ...r, outcome } : r)))

  const handleCode = (text: string) => {
    const code = classifyCode(text)
    switch (code.kind) {
      case 'ticket': {
        const id = addRead({ kind: 'checking' })
        withTimeout(checkIn.mutateAsync(code.qrCode)).then(
          (result) => settleRead(id, { kind: 'checkedIn', checkIn: result }),
          (error: unknown) => settleRead(id, { kind: 'ticketRefused', error }),
        )
        return
      }
      case 'prize':
        addRead(prizeDesk ? { kind: 'prize', code: code.code } : { kind: 'prizeRefused' })
        return
      case 'reference':
        addRead({ kind: 'reference', reference: code.reference })
        setLista({ search: code.reference })
        return
      case 'unrecognised':
        addRead({ kind: 'unrecognised' })
    }
  }

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault()
    if (!texto.trim()) return
    handleCode(texto)
    setTexto('')
  }

  const handlePick = (ev: DoorEventResponse) => {
    storeEventId(ev.id)
    setStoredId(ev.id)
    setEligiendo(false)
  }

  const ultima = reads[0]
  const pidiendoEvento = eligiendo || (!doorEvents.isLoading && evento === null)
  const terminalAbierto = evento !== null && !pidiendoEvento

  // Al abrir el terminal y al cerrar la lista, el foco vuelve a la caja (después de que la lista
  // devuelva el suyo al botón que la abrió)
  useEffect(() => {
    if (terminalAbierto && !lista && hasKeyboard()) inputRef.current?.focus()
  }, [terminalAbierto, lista])

  return (
    <div className="min-h-screen bg-slate-50 text-slate-900 flex flex-col items-center pb-6 font-sans">
      <header className="w-full max-w-md px-4 py-3 flex items-center justify-between gap-2 border-b border-slate-200/80 bg-white/95 backdrop-blur-md sticky top-0 z-30 shadow-xs">
        <div className="flex items-center gap-2.5 min-w-0">
          <div className="w-8 h-8 rounded-xl bg-brand-deep flex items-center justify-center text-white shadow-sm shrink-0">
            <QrCode className="w-4 h-4" />
          </div>
          <div className="min-w-0">
            <h1 className="font-black text-sm text-slate-900 tracking-tight">
              {branding.appName} Scanner
            </h1>
            {/* El nombre del evento siempre a la vista */}
            <p className="text-[11px] font-semibold text-blue-700 truncate">
              {evento ? evento.name : 'Sin evento elegido'}
            </p>
          </div>
        </div>
        <div className="flex items-center gap-2 shrink-0">
          {evento && !eligiendo && (
            <button
              type="button"
              onClick={() => setEligiendo(true)}
              className="flex items-center gap-1 text-[11px] font-bold bg-slate-100 hover:bg-slate-200 text-slate-700 px-2.5 py-1.5 rounded-xl cursor-pointer"
            >
              <RefreshCw className="w-3.5 h-3.5" />
              Cambiar evento
            </button>
          )}
          {!isStandalone && (
            <button
              type="button"
              onClick={handleInstallClick}
              title="Instalar App en tu dispositivo"
              aria-label="Instalar"
              className="flex items-center gap-1 text-[11px] font-bold bg-blue-50 hover:bg-blue-100 text-blue-700 border border-blue-200 px-2 py-1.5 rounded-xl cursor-pointer"
            >
              <Download className="w-3.5 h-3.5" />
            </button>
          )}
        </div>
      </header>

      <main className="w-full max-w-md px-4 py-3 flex-1 flex flex-col gap-3">
        {pidiendoEvento ? (
          <EventPicker
            events={doorEvents.data}
            isLoading={doorEvents.isLoading}
            error={doorEvents.error}
            currentId={evento?.id ?? null}
            onPick={handlePick}
            onCancel={evento ? () => setEligiendo(false) : undefined}
          />
        ) : evento ? (
          <>
            {/* Con la lista abierta la cámara no lee lo que queda detrás de ella */}
            <ScannerCamera onRead={handleCode} paused={lista !== null} />

            <form onSubmit={handleSubmit} className="flex gap-2">
              <input
                ref={inputRef}
                type="text"
                value={texto}
                onChange={(e) => setTexto(e.target.value)}
                placeholder="Código o referencia"
                aria-label="Código"
                autoComplete="off"
                autoCapitalize="off"
                spellCheck={false}
                className="flex-1 min-w-0 bg-white border border-slate-300 rounded-xl px-3.5 py-2.5 text-xs text-slate-900 placeholder:text-slate-400 focus:outline-none focus:border-blue-500 shadow-xs"
              />
              <Button
                type="submit"
                className="bg-brand-deep hover:bg-blue-900 text-white font-bold text-xs px-4 py-2.5 rounded-xl shrink-0 cursor-pointer shadow-xs"
              >
                Validar
              </Button>
            </form>

            {ultima && (
              <ReadResult
                key={ultima.id}
                outcome={ultima.outcome}
                onOpenDoorList={(search) => setLista({ search })}
              />
            )}

            {reads.length > 1 && (
              <section className="bg-white rounded-2xl border border-slate-200/80 p-3 shadow-xs">
                <h2 className="text-[10px] font-black uppercase tracking-wider text-slate-500 mb-1.5">
                  Lecturas anteriores en este terminal
                </h2>
                <ul className="divide-y divide-slate-100">
                  {reads.slice(1).map((r) => {
                    const { title, subject } = describeRead(r.outcome)
                    return (
                      <li key={r.id} className="py-1.5 flex items-center gap-2 text-[11px]">
                        <span className="text-slate-400 tabular-nums shrink-0">
                          {formatTime(r.at)}
                        </span>
                        <span className="text-slate-700 truncate">
                          {subject ? `${title}: ${subject}` : title}
                        </span>
                      </li>
                    )
                  })}
                </ul>
              </section>
            )}
          </>
        ) : null}
      </main>

      {evento && (
        <footer className="w-full max-w-md px-4 pt-2">
          <div className="bg-white rounded-2xl p-3 border border-slate-200/80 shadow-xs flex items-center justify-between text-xs text-slate-600">
            <div className="flex items-center gap-1.5">
              <UserCheck className="w-4 h-4 text-blue-600" />
              <span>
                Entradas:{' '}
                <strong className="text-slate-900">{totals.data?.checkedIn ?? '—'}</strong>/
                {totals.data?.registered ?? '—'}
              </span>
            </div>
            <button
              type="button"
              onClick={() => setLista({ search: '' })}
              className="text-[11px] text-blue-600 font-bold hover:underline cursor-pointer flex items-center gap-0.5"
            >
              Lista de la puerta <ChevronRight className="w-3 h-3" />
            </button>
          </div>
        </footer>
      )}

      {lista && evento && (
        <DoorListSheet
          eventId={evento.id}
          eventName={evento.name}
          initialSearch={lista.search}
          onCheckedIn={(c: CheckInResponse) => addRead({ kind: 'checkedIn', checkIn: c })}
          onClose={() => setLista(null)}
        />
      )}
    </div>
  )
}

export default PwaScannerPage
