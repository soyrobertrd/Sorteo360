import { useEffect, useMemo, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { ArrowLeft, Ban, Maximize2, MonitorUp, Play, Radio } from 'lucide-react'
import type { PrizeResponse } from '@/api/types'
import { useCan } from '@/auth/AuthProvider'
import { ROLES } from '@/auth/roles'
import { Button } from '@/components/ui/Button'
import { ErrorNote, Spinner } from '@/components/ui/Feedback'
import { Field, Select, TextInput } from '@/components/ui/Field'
import { useBranding } from '@/lib/branding'
import { camposDe } from '../camposVisibles'
import { useDrawPrizes, useRaffle } from '../hooks'
import { liveDraw } from '../raffleView'
import {
  abrirCanalProyector,
  rutaProyector,
  useProyectorConectado,
  type MensajeProyector,
} from '../proyeccion'
import {
  duracionLote,
  filaDe,
  barajar,
  gruposDePremios,
  guardarAjustes,
  leerAjustes,
  LOTES,
  MAX_POR_PANTALLA,
  pendientes,
  type AjustesTablero,
  type EstadoTablero,
  type FilaTablero,
  type Identificador,
} from '../tablero'
import { TableroGanadores } from './TableroGanadores'
import { VoidDrawModal } from './VoidDrawModal'

/**
 * La consola del tablero de ganadores: elige el grupo de premios, sortea un lote con un botón y
 * muestra el resultado aquí y en el proyector. Cada premio lo sortea el servicio, uno por uno; la
 * animación solo revela lo que él eligió.
 */
export function TableroConsola({ eventId }: { eventId: string }) {
  const branding = useBranding(eventId)
  const { data: sorteo, isLoading, error } = useRaffle(eventId)
  const drawPrizes = useDrawPrizes(eventId)
  const canDraw = useCan(ROLES.rafflesDraw)
  // Con el proyector abierto, esta ventana es el control remoto: no repite la pantalla del público.
  const control = useProyectorConectado(eventId)

  const [ajustes, setAjustesState] = useState<AjustesTablero>(() =>
    leerAjustes(eventId, branding.organizationName),
  )
  const [grupoElegido, setGrupoElegido] = useState<string | null>(null)
  const [filas, setFilas] = useState<FilaTablero[]>([])
  /** Los premios del último lote: desde aquí se anula a quien no se presentó. */
  const [delLote, setDelLote] = useState<PrizeResponse[]>([])
  const [anulando, setAnulando] = useState<PrizeResponse | null>(null)
  const [lote, setLote] = useState(0)
  const [ocupado, setOcupado] = useState(false)
  const [drawError, setDrawError] = useState<unknown>(null)
  const pantallaRef = useRef<HTMLDivElement>(null)
  const [enPantallaCompleta, setEnPantallaCompleta] = useState(false)
  useEffect(() => {
    const cambio = () => setEnPantallaCompleta(document.fullscreenElement === pantallaRef.current)
    document.addEventListener('fullscreenchange', cambio)
    return () => document.removeEventListener('fullscreenchange', cambio)
  }, [])

  const setAjustes = (cambio: Partial<AjustesTablero>) =>
    setAjustesState((a) => {
      const next = { ...a, ...cambio }
      guardarAjustes(eventId, next)
      return next
    })

  const grupos = useMemo(() => (sorteo ? gruposDePremios(sorteo) : []), [sorteo])
  const grupo =
    grupos.find((g) => g.clave === grupoElegido) ??
    grupos.find((g) => pendientes(g).length > 0) ??
    grupos[grupos.length - 1]
  const quedan = grupo ? pendientes(grupo).length : 0
  const premioImagen = grupo?.premios.find((p) => p.imageUrl)?.imageUrl ?? null
  // Un grupo con foto es premio principal: se sortea uno a uno, con la foto en pantalla.
  const principal = premioImagen !== null
  const enLote = Math.min(principal ? 1 : ajustes.porLote, MAX_POR_PANTALLA, quedan)

  const estado: EstadoTablero = {
    titulo: ajustes.titulo,
    subtitulo: ajustes.subtitulo,
    ronda: grupo?.ronda ?? null,
    premioNombre: grupo?.nombre ?? null,
    premioPatrocinador: grupo?.patrocinador ?? null,
    colorPremio: ajustes.colorPremio,
    navidad: ajustes.navidad,
    premioImagen,
    principal,
    total: grupo?.premios.length ?? 0,
    quedan,
    cifras: ajustes.cifras,
    filas,
    lote,
  }

  // El proyector recibe cada cambio, y al abrirse pide el estado actual con 'hola'.
  const canalRef = useRef<BroadcastChannel | null>(null)
  const estadoRef = useRef(estado)
  useEffect(() => {
    const canal = abrirCanalProyector(eventId)
    canalRef.current = canal
    if (!canal) return
    canal.onmessage = (e: MessageEvent<{ tipo: string }>) => {
      if (e.data.tipo === 'hola')
        canal.postMessage({
          tipo: 'tablero',
          tablero: estadoRef.current,
        } satisfies MensajeProyector)
    }
    return () => canal.close()
  }, [eventId])
  const claveEstado = JSON.stringify(estado)
  useEffect(() => {
    estadoRef.current = JSON.parse(claveEstado) as EstadoTablero
    canalRef.current?.postMessage({
      tipo: 'tablero',
      tablero: estadoRef.current,
    } satisfies MensajeProyector)
  }, [claveEstado])

  const sortearLote = async () => {
    if (!grupo || enLote === 0) return
    // Fija el grupo: si el lote lo completa, la pantalla no salta al siguiente con estos ganadores.
    setGrupoElegido(grupo.clave)
    setOcupado(true)
    setDrawError(null)
    const { drawn, error } = await drawPrizes(
      pendientes(grupo)
        .slice(0, enLote)
        .map((p) => p.id),
    )
    if (error) setDrawError(error)
    const nuevas = drawn.map((p) => filaDe(p, ajustes, camposDe(sorteo))).filter((f) => f !== null)
    setFilas(barajar(nuevas))
    setDelLote(drawn)
    setLote((n) => n + 1)
    window.setTimeout(
      () => setOcupado(false),
      nuevas.length ? duracionLote(nuevas.length, principal) : 0,
    )
  }

  const puedeSortear = sorteo?.state === 'in_progress' && canDraw.allowed && !ocupado && enLote > 0
  // En pantalla completa no hay botones: Enter o la barra espaciadora sortean el lote.
  const pantallaCompleta = () => {
    void pantallaRef.current?.requestFullscreen?.()
    pantallaRef.current?.focus()
  }

  // Como control remoto, Enter o la barra espaciadora sortean desde cualquier parte de la ventana
  // (menos dentro de un campo o un botón, que ya usan esas teclas).
  const sortearRef = useRef<() => void>(() => {})
  sortearRef.current = () => {
    if (puedeSortear) void sortearLote()
  }
  useEffect(() => {
    if (!control) return
    const alPulsar = (e: KeyboardEvent) => {
      if (e.key !== 'Enter' && e.key !== ' ') return
      const t = e.target as HTMLElement | null
      if (t?.closest('input, select, textarea, button, summary, [role="dialog"]')) return
      e.preventDefault()
      sortearRef.current()
    }
    window.addEventListener('keydown', alPulsar)
    return () => window.removeEventListener('keydown', alPulsar)
  }, [control])

  if (isLoading) return <Spinner />
  if (error != null || !sorteo) return <ErrorNote error={error} />

  const enCurso = sorteo.state === 'in_progress'

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <Link
          to={`/sorteo/sorteos/${eventId}`}
          className="inline-flex items-center gap-1.5 text-sm font-semibold text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft size={16} /> Volver al sorteo
        </Link>
        <div className="flex flex-wrap gap-2">
          <Button
            type="button"
            variant="outline"
            onClick={() =>
              window.open(rutaProyector(eventId), 'sorteo360-proyector', 'width=1366,height=768')
            }
          >
            <MonitorUp className="mr-1.5 h-4 w-4" />{' '}
            {control ? 'Traer el proyector' : 'Abrir proyector'}
          </Button>
          {!control && (
            <Button type="button" variant="outline" onClick={pantallaCompleta}>
              <Maximize2 className="mr-1.5 h-4 w-4" /> Pantalla completa
            </Button>
          )}
        </div>
      </div>

      {/* Lo de cada giro arriba y a mano: qué grupo y el botón. Los ajustes de la pantalla, que
          se fijan una vez antes de la fiesta, quedan plegados debajo. */}
      <section className="rounded-2xl border border-border bg-card p-4 shadow-sm sm:p-5">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-end">
          <div className="min-w-0 flex-1">
            <Field label="Grupo de premios">
              <Select
                value={grupo?.clave ?? ''}
                onChange={(e) => {
                  setGrupoElegido(e.target.value)
                  setFilas([])
                  setDelLote([])
                }}
              >
                {grupos.map((g) => (
                  <option key={g.clave} value={g.clave}>
                    Ronda {g.ronda} · {g.nombre}
                    {g.valor ? ` (${g.valor})` : ''} · quedan {pendientes(g).length} de{' '}
                    {g.premios.length}
                  </option>
                ))}
              </Select>
            </Field>
          </div>
          <div className="w-full lg:w-44">
            <Field label="Por lote">
              <Select
                value={principal ? 1 : ajustes.porLote}
                disabled={principal}
                title={principal ? 'Los premios con foto se sortean uno a uno.' : undefined}
                onChange={(e) => setAjustes({ porLote: Number(e.target.value) })}
              >
                {LOTES.map((n) => (
                  <option key={n} value={n}>
                    {n} premio{n === 1 ? '' : 's'}
                  </option>
                ))}
              </Select>
            </Field>
          </div>
          <Button
            type="button"
            size="lg"
            onClick={() => void sortearLote()}
            disabled={!puedeSortear}
            title={!canDraw.allowed ? canDraw.title : undefined}
            className="bg-brand text-(--brand-primary-contrast) hover:bg-brand-deep"
          >
            <Play className="mr-1.5 h-4 w-4" />
            {ocupado
              ? 'Sorteando…'
              : enLote === 0
                ? 'Grupo completo'
                : `Sortear ${enLote} premio${enLote === 1 ? '' : 's'}`}
          </Button>
        </div>
        <p className="mt-3 text-sm text-muted-foreground" aria-live="polite">
          {principal && quedan > 0 && 'Premio principal: uno a uno, con la foto. '}
          {!enCurso
            ? 'El sorteo tiene que estar iniciado.'
            : quedan === 0
              ? 'Este grupo está completo: elige el siguiente.'
              : `Quedan ${quedan} de ${grupo?.premios.length ?? 0} en este grupo.`}
        </p>

        <details className="group mt-4 border-t border-border pt-3">
          <summary className="cursor-pointer list-none text-sm font-semibold text-muted-foreground hover:text-foreground [&::-webkit-details-marker]:hidden">
            <span className="inline-block transition-transform group-open:rotate-90">›</span>{' '}
            Ajustes de la pantalla
          </summary>
          <div className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <Field label="Título (izquierda)">
              <TextInput
                value={ajustes.titulo}
                onChange={(e) => setAjustes({ titulo: e.target.value })}
              />
            </Field>
            <Field label="Título (derecha)">
              <TextInput
                value={ajustes.subtitulo}
                onChange={(e) => setAjustes({ subtitulo: e.target.value })}
              />
            </Field>
            <Field label="Número que se muestra">
              <Select
                value={ajustes.identificador}
                onChange={(e) => setAjustes({ identificador: e.target.value as Identificador })}
              >
                <option value="empleado">ID de empleado</option>
                <option value="boleto">Número de boleto</option>
              </Select>
            </Field>
            <Field label="Color del premio">
              <input
                type="color"
                value={ajustes.colorPremio}
                onChange={(e) => setAjustes({ colorPremio: e.target.value })}
                className="h-10.5 w-full cursor-pointer rounded-lg border border-slate-200 bg-white p-1 dark:border-slate-700 dark:bg-slate-900"
              />
            </Field>
            <Field label="Decoración">
              <label className="flex h-10.5 cursor-pointer items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={ajustes.navidad}
                  onChange={(e) => setAjustes({ navidad: e.target.checked })}
                />
                Navideña
              </label>
            </Field>
          </div>
        </details>
      </section>
      {drawError != null && <ErrorNote error={drawError} />}

      {control ? (
        <section
          className="flex flex-col gap-4 rounded-2xl border border-border bg-card p-4 shadow-sm sm:flex-row sm:items-center"
          aria-live="polite"
        >
          <div className="flex min-w-0 flex-1 items-start gap-3">
            <span className="mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-emerald-100 text-emerald-700 dark:bg-emerald-950/50 dark:text-emerald-300">
              <Radio className="h-4 w-4" />
            </span>
            <div className="min-w-0">
              <p className="font-bold text-foreground">Control remoto</p>
              <p className="text-sm text-muted-foreground">
                Los resultados se ven en la ventana del proyector. Desde aquí se sortea y se anula;
                Enter o la barra espaciadora también sortean.
              </p>
            </div>
          </div>
          <details className="group sm:w-72">
            <summary className="cursor-pointer list-none text-sm font-semibold text-muted-foreground hover:text-foreground [&::-webkit-details-marker]:hidden">
              <span className="inline-block transition-transform group-open:rotate-90">›</span> Ver
              lo que ve el público
            </summary>
            <div className="mt-2 overflow-hidden rounded-lg border border-border bg-white">
              <TableroGanadores estado={estado} logoUrl={branding.logoDarkUrl} />
            </div>
          </details>
        </section>
      ) : (
        <div
          ref={pantallaRef}
          tabIndex={-1}
          onKeyDown={(e) => {
            if (e.key !== 'Enter' && e.key !== ' ') return
            e.preventDefault()
            if (puedeSortear) void sortearLote()
          }}
          className="overflow-hidden rounded-2xl border border-border bg-white [&:fullscreen]:rounded-none [&:fullscreen]:border-0"
        >
          <TableroGanadores
            estado={estado}
            logoUrl={branding.logoDarkUrl}
            llenar={enPantallaCompleta}
          />
        </div>
      )}

      {/* Quien no se presenta se anula aquí: sale de la pantalla y su premio vuelve al grupo. */}
      {delLote.some((p) => liveDraw(p)) && (
        <section className="rounded-2xl border border-border bg-card p-4 shadow-sm">
          <h3 className="text-sm font-bold text-foreground">Ganadores de este lote</h3>
          <ul className="mt-2 divide-y divide-border">
            {delLote.map((p) => {
              const d = liveDraw(p)
              const fila = filas.find((f) => f.id === d?.id)
              if (!d || !fila) return null
              return (
                <li key={d.id} className="flex items-center justify-between gap-3 py-2 text-sm">
                  <span className="min-w-0 truncate">
                    <span className="font-mono tabular-nums text-muted-foreground">
                      {fila.numero}
                    </span>{' '}
                    <span className="font-semibold text-foreground">{fila.nombre}</span>
                    {fila.detalle && (
                      <span className="text-muted-foreground"> · {fila.detalle}</span>
                    )}
                  </span>
                  <Button
                    type="button"
                    size="sm"
                    variant="outline"
                    onClick={() => setAnulando(p)}
                    disabled={!canDraw.allowed || ocupado}
                    title={!canDraw.allowed ? canDraw.title : undefined}
                  >
                    <Ban className="mr-1 h-3.5 w-3.5" /> No se presentó
                  </Button>
                </li>
              )
            })}
          </ul>
        </section>
      )}

      {anulando && liveDraw(anulando) && (
        <VoidDrawModal
          eventId={eventId}
          prize={anulando}
          draw={liveDraw(anulando)!}
          initialCode="absent"
          onVoided={() => {
            const drawId = liveDraw(anulando)?.id
            setFilas((fs) => fs.filter((f) => f.id !== drawId))
            setDelLote((ps) => ps.filter((p) => p.id !== anulando.id))
            setAnulando(null)
          }}
          onClose={() => setAnulando(null)}
        />
      )}
    </div>
  )
}
