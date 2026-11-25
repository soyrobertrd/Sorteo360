import { toast } from 'sonner'
import { cn } from '@/lib/utils'
import { errorMessage } from '@/lib/errorMessage'
import { useRef, useState } from 'react'
import { useParams, useNavigate, useSearchParams } from 'react-router-dom'
import {
  CalendarDays,
  Circle,
  Gift,
  Play,
  CheckCircle2,
  FileText,
  Download,
  ShieldCheck,
  MoreVertical,
  ChevronDown,
  Smartphone,
  Ban,
  Award,
  PackageCheck,
  Undo2,
  QrCode,
  UserMinus,
} from 'lucide-react'
import { Tabs } from '@/components/ui/Tabs'
import { Pagination } from '@/components/ui/Pagination'
import { ApiError } from '@/api'
import type {
  PrizeDrawSummary,
  PrizeHandoverSummary,
  PrizeResponse,
  RaffleRoundResponse,
} from '@/api/types'
import { Breadcrumbs } from '@/components/ui/Breadcrumbs'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { ActionMenu } from '@/components/ui/ActionMenu'
import { MenuItem, MenuSeparator } from '@/components/ui/MenuItem'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { ErrorNote, Spinner, WarningNote } from '@/components/ui/Feedback'
import { useCan } from '@/auth/AuthProvider'
import { ROLES } from '@/auth/roles'
import { useEvento } from '@/features/eventos/hooks'
import { formatDate, formatDateTime } from '@/lib/format'
import { fieldError } from '@/lib/fieldErrors'
import {
  apiErrorCodeLabel,
  prizeDrawVoidCodeLabel,
  prizeStateLabel,
  raffleBlockingReasonLabel,
} from '@/lib/labels'
import {
  useCreatePrizes,
  useDeletePrize,
  usePrizeImage,
  useDeleteRaffleRound,
  usePrizeEligible,
  useRaffle,
  useRaffleParticipants,
  useReopenRaffle,
  useSaveRaffleRound,
  useUpdateRaffle,
} from './hooks'
import type { RaffleChanges } from './raffleRequest'
import {
  downloadNotarialRecord,
  hasAnyDraw,
  liveDraw,
  liveHandover,
  prizesOf,
  sinPuerta,
} from './raffleView'
import { ActaNotarialCard } from './components/ActaNotarialCard'
import { AnuladosCard } from './components/AnuladosCard'
import { BitacoraSorteoCard } from './components/BitacoraSorteoCard'
import { CertificadoPremioModal } from './components/CertificadoPremioModal'
import { CompletarSorteoModal } from './components/CompletarSorteoModal'
import { EntregaPremioModal } from './components/EntregaPremioModal'
import { EscanerCanjeModal } from './components/EscanerCanjeModal'
import { EstadoSorteoBadge } from './components/EstadoSorteoBadge'
import { FirmantesCertificadoCard } from './components/FirmantesCertificadoCard'
import { RondaPremios } from './components/RondaPremios'
import { rutaProyector } from './proyeccion'
import { ViewModeToggle, type ViewMode } from '@/components/ui/ViewModeToggle'
import { ConfigurarPremiosModal } from './components/ConfigurarPremiosModal'
import { CamposVisiblesCard } from './components/CamposVisiblesCard'
import { SorteosVinculadosCard } from './components/SorteosVinculadosCard'
import { PRINCIPALES, SIMULACION, useCargarSimulacion } from './simulacion'
import { ImportarPremiosModal } from './components/ImportarPremiosModal'
import { ConfigurarRondaModal } from './components/ConfigurarRondaModal'
import { IniciarSorteoModal } from './components/IniciarSorteoModal'
import { ParticipantesTable } from './components/ParticipantesTable'
import { ExcluirEnLoteModal } from './components/ExcluirEnLoteModal'
import { RevertirEntregaModal } from './components/RevertirEntregaModal'
import { VoidDrawModal } from './components/VoidDrawModal'
import { descargarBlob } from '@/lib/download'
import { useBranding } from '@/lib/branding'
import { PersonalizacionMarcaCard } from './components/PersonalizacionMarcaCard'

const menuItemClass =
  'w-full flex items-center gap-2.5 px-4 py-2 text-xs text-slate-700 dark:text-slate-200 hover:bg-slate-50 dark:hover:bg-slate-800 font-semibold cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed'

const PESTANAS = ['premios', 'participantes', 'ganadores', 'reglas'] as const

const GANADORES_POR_PAGINA = 50
type Pestana = (typeof PESTANAS)[number]

/** La pestaña viene en `?tab=`, así el historial puede enlazar directo a los ganadores. */
function esPestana(valor: string | null): valor is Pestana {
  return PESTANAS.some((p) => p === valor)
}

export function SorteoDetailPage() {
  const { eventId = '' } = useParams<{ eventId: string }>()
  const navigate = useNavigate()
  const { data: sorteo, isLoading, error } = useRaffle(eventId)
  const { data: evento } = useEvento(eventId)
  const canWrite = useCan(ROLES.rafflesWrite)
  const [excluirEnLote, setExcluirEnLote] = useState(false)
  const canDraw = useCan(ROLES.rafflesDraw)
  const updateRaffle = useUpdateRaffle(eventId)
  const saveRound = useSaveRaffleRound(eventId)
  const deleteRound = useDeleteRaffleRound(eventId)
  const createPrizes = useCreatePrizes(eventId)
  const deletePrize = useDeletePrize(eventId)
  const prizeImage = usePrizeImage(eventId)
  // La lista (tabla) por omisión: más compacta con muchos premios; la cuadrícula es la alternativa.
  const [vistaPremios, setVistaPremios] = useState<ViewMode>('list')
  const reopenRaffle = useReopenRaffle(eventId)

  const [searchParams, setSearchParams] = useSearchParams()
  const solicitada = searchParams.get('tab')
  const activeTab: Pestana = esPestana(solicitada) ? solicitada : 'premios'
  const setActiveTab = (tab: Pestana) =>
    setSearchParams(
      (prev) => {
        const next = new URLSearchParams(prev)
        next.set('tab', tab)
        return next
      },
      { replace: true },
    )
  const [showAddPremioModal, setShowAddPremioModal] = useState(false)
  const [showImportarPremios, setShowImportarPremios] = useState(false)
  const [showSimulacion, setShowSimulacion] = useState(false)
  const cargarSimulacion = useCargarSimulacion(eventId)
  const [selectedRondaIdForPremio, setSelectedRondaIdForPremio] = useState<string | undefined>()

  const [showRondaModal, setShowRondaModal] = useState(false)
  const [editingRonda, setEditingRonda] = useState<RaffleRoundResponse | null>(null)

  const [showIniciarModal, setShowIniciarModal] = useState(false)
  const [showSecondaryMenu, setShowSecondaryMenu] = useState(false)
  const accionesRef = useRef<HTMLButtonElement>(null)
  const [showAccionesPremios, setShowAccionesPremios] = useState(false)
  const [showReopen, setShowReopen] = useState(false)
  const [showCompletar, setShowCompletar] = useState(false)
  const [actaError, setActaError] = useState<unknown>(null)
  const [voiding, setVoiding] = useState<{
    prize: PrizeResponse
    draw: PrizeDrawSummary
  } | null>(null)
  const [showEscaner, setShowEscaner] = useState(false)
  const [menuDrawId, setMenuDrawId] = useState<string | null>(null)
  const [paginaGanadores, setPaginaGanadores] = useState(1)
  const [revirtiendo, setRevirtiendo] = useState<{
    prize: PrizeResponse
    draw: PrizeDrawSummary
    handover: PrizeHandoverSummary
  } | null>(null)
  const [certificado, setCertificado] = useState<{
    prize: PrizeResponse
    draw: PrizeDrawSummary
  } | null>(null)

  // La entrega se abre por `?entrega={prizeId}`: el escáner enlaza a ella (Decisión 9)
  const entregaId = searchParams.get('entrega')
  const setEntrega = (prizeId: string | null) =>
    setSearchParams(
      (prev) => {
        const next = new URLSearchParams(prev)
        if (prizeId) next.set('entrega', prizeId)
        else next.delete('entrega')
        return next
      },
      { replace: true },
    )

  // El bombo del próximo premio por sortear: solo existe en curso (spec raffles/02 §5)
  const nextPrize =
    sorteo?.state === 'in_progress'
      ? prizesOf(sorteo).find((p) => p.state === 'undrawn')
      : undefined
  // Aquí solo se usa el total: una muestra de 1 basta.
  const { data: nextPrizePool } = usePrizeEligible(eventId, nextPrize?.id, false, 1)

  const nombreEvento = evento?.name ?? 'Sorteo'
  const { data: participantesData } = useRaffleParticipants(eventId)
  const branding = useBranding(eventId)

  if (isLoading) return <Spinner />

  if (!sorteo) {
    const noExiste = error instanceof ApiError && error.code === 'RAFFLE_NOT_FOUND'
    return (
      <div className="p-8 text-center bg-white dark:bg-card rounded-xl border border-slate-200 dark:border-slate-800 space-y-4">
        {noExiste || error == null ? (
          <h2 className="text-xl font-bold text-slate-900 dark:text-slate-100">
            Sorteo no encontrado
          </h2>
        ) : (
          <ErrorNote error={error} />
        )}
        <Button
          onClick={() => navigate('/sorteo/sorteos')}
          className="bg-blue-600 hover:bg-blue-700 text-white"
        >
          Volver a Sorteos
        </Button>
      </div>
    )
  }

  const premios = prizesOf(sorteo)
  const totalPremios = premios.length
  const premiosSorteados = premios.filter((p) => p.state !== 'undrawn').length
  const premiosEntregados = premios.filter((p) => p.state === 'redeemed').length
  const totalParticipantes = participantesData?.length ?? 0
  const conCheckIn = participantesData?.filter((p) => p.checkedIn).length ?? 0
  // Lo que conviene tener listo antes de la noche; cada paso lleva a la pestaña donde se resuelve.
  const preparacion: { texto: string; listo: boolean; pestana: Pestana }[] = [
    { texto: 'Rondas creadas', listo: sorteo.rounds.length > 0, pestana: 'premios' },
    { texto: 'Premios cargados', listo: totalPremios > 0, pestana: 'premios' },
    { texto: 'Participantes cargados', listo: totalParticipantes > 0, pestana: 'participantes' },
    ...(sinPuerta(sorteo)
      ? []
      : [
          {
            texto: 'Participantes con check-in',
            listo: conCheckIn > 0,
            pestana: 'participantes' as const,
          },
        ]),
    {
      texto: 'Firmantes del certificado',
      listo: sorteo.certificateSigners.length > 0,
      pestana: 'reglas',
    },
    {
      texto: 'Instrucciones de retiro',
      listo: Boolean(sorteo.pickupInstructions),
      pestana: 'reglas',
    },
  ]
  const ganadores = premios.flatMap((premio) => {
    const draw = liveDraw(premio)
    return draw ? [{ premio, draw }] : []
  })
  // Vivos y anulados: una anulación no borra nada, queda en el historial del premio
  const prizeDraws = premios.flatMap((premio) => premio.draws.map((draw) => ({ premio, draw })))
  const paginasGanadores = Math.max(1, Math.ceil(prizeDraws.length / GANADORES_POR_PAGINA))
  const paginaGanadoresActual = Math.min(paginaGanadores, paginasGanadores)
  const ganadoresDePagina = prizeDraws.slice(
    (paginaGanadoresActual - 1) * GANADORES_POR_PAGINA,
    paginaGanadoresActual * GANADORES_POR_PAGINA,
  )
  const voidTitle = !canDraw.allowed
    ? canDraw.title
    : 'Anular este ganador: el premio vuelve a quedar por sortear'

  // Armar el sorteo: rondas, premios, reglas y autoridades. Agregar sigue en curso; un completado
  // no admite cambios.
  const canSetup = canWrite.allowed && sorteo.state !== 'completed'
  const setupTitle = !canWrite.allowed
    ? canWrite.title
    : sorteo.state === 'completed'
      ? apiErrorCodeLabel.RAFFLE_COMPLETED
      : undefined
  const bloqueos = sorteo.blockingReasons ?? []

  // Los firmantes del certificado no atestiguan nada: los frena el evento cerrado, no el sorteo.
  const eventoCerrado = evento?.status === 'closed' || evento?.status === 'cancelled'
  const firmantesTitle = !canWrite.allowed
    ? canWrite.title
    : eventoCerrado
      ? apiErrorCodeLabel.EVENT_NOT_EDITABLE
      : undefined

  const saveChanges = (changes: RaffleChanges) => updateRaffle.mutate(changes)

  // Exportar resultados oficiales a CSV
  const handleExportarCSV = () => {
    const headers = [
      'Premio',
      'Ronda',
      'Valor Estimado',
      'Ganador(a)',
      'Empresa',
      'Codigo de Canje',
      'Fecha Sorteado',
      'Un premio por persona',
      'Un premio por empresa',
      'Estado Entrega',
      'Receptor de Entrega',
    ]

    const csv = (texto: string) => `"${texto.replace(/"/g, '""')}"`
    const rows = sorteo.rounds.flatMap((ronda) =>
      ronda.prizes.map((p) => {
        const draw = liveDraw(p)
        const entrega = draw?.handovers.find((h) => h.reversedAt === null)
        return [
          csv(p.name),
          csv(ronda.name),
          csv(p.value || 'N/D'),
          csv(draw?.winnerName || 'Sin ganador'),
          csv(draw?.winnerCompany || 'N/D'),
          csv(draw?.redemptionCode || 'N/D'),
          csv(draw ? new Date(draw.drawnAt).toLocaleString() : 'Pendiente'),
          csv(draw ? (draw.onePrizePerPerson ? 'Sí' : 'No') : 'N/D'),
          csv(draw ? (draw.onePrizePerCompany ? 'Sí' : 'No') : 'N/D'),
          csv(prizeStateLabel[p.state]),
          csv(entrega?.receiverName || 'N/D'),
        ].join(',')
      }),
    )

    const csvContent = '﻿' + [headers.join(','), ...rows].join('\n')
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' })
    descargarBlob(blob, `Sorteo_${nombreEvento.replace(/[^a-zA-Z0-9]/g, '_')}_Resultados.csv`)
  }

  // Determinar texto dinámico del botón de acción
  const textoBotonAccion = (() => {
    if (totalPremios === 0) return 'Iniciar Sorteo'
    if (premiosSorteados === 0) return 'Iniciar Sorteo'
    if (premiosSorteados < totalPremios) {
      // Buscar la ronda del próximo premio pendiente
      const nextPrize = premios.find((p) => p.state === 'undrawn')
      const rondaDelProximo = nextPrize
        ? sorteo.rounds.find((r) => r.id === nextPrize.roundId)
        : null

      if (rondaDelProximo && rondaDelProximo.number > 1) {
        // Verificar si es el primer premio de esa ronda siguiente
        const sorteadosEnEsaRonda = rondaDelProximo.prizes.filter(
          (p) => p.state !== 'undrawn',
        ).length
        if (sorteadosEnEsaRonda === 0) {
          return `Iniciar ${rondaDelProximo.name || `Ronda ${rondaDelProximo.number}`}`
        }
      }
      return 'Continuar Sorteo'
    }
    return 'Iniciar Sorteo'
  })()

  const iniciarDeshabilitado =
    !canDraw.allowed ||
    sorteo.state === 'completed' ||
    (sorteo.state === 'draft' && sorteo.isReadyToStart !== true)
  const iniciarTitle = !canDraw.allowed
    ? canDraw.title
    : sorteo.state === 'completed'
      ? 'El sorteo ya está completado.'
      : bloqueos.length > 0
        ? bloqueos.map((b) => raffleBlockingReasonLabel[b]).join(' ')
        : 'Configurar e iniciar sorteo en vivo'

  // Reabrir corrige un `start` apurado; desde el primer sorteo de premio, cada corrección es una
  // anulación (spec raffles/02 §6).
  const reopenDisabled = !canDraw.allowed || hasAnyDraw(sorteo)
  const reopenTitle = !canDraw.allowed
    ? canDraw.title
    : hasAnyDraw(sorteo)
      ? apiErrorCodeLabel.RAFFLE_HAS_DRAWS
      : 'Volver a borrador y descartar la lista congelada'

  // Completar cierra la noche: se ofrece en curso, y como acción principal cuando ya no queda nada
  // por sortear (spec raffles/03 §4.4).
  const completarPrincipal = sorteo.state === 'in_progress' && premiosSorteados === totalPremios
  const completarTitle = canDraw.allowed
    ? 'Cerrar el sorteo, generar el acta y avisar por correo a los ganadores'
    : canDraw.title
  const abrirCompletar = () => {
    setShowSecondaryMenu(false)
    setShowCompletar(true)
  }

  const actaVigente = sorteo.notarialRecords[0]
  const descargarActa = () => {
    setShowSecondaryMenu(false)
    setActaError(null)
    if (actaVigente) downloadNotarialRecord(actaVigente).catch(setActaError)
  }

  const handleOpenAddPremio = (rondaId?: string) => {
    createPrizes.reset()
    setSelectedRondaIdForPremio(rondaId || sorteo.rounds[0]?.id)
    setShowAddPremioModal(true)
  }

  const handleOpenAddRonda = () => {
    saveRound.reset()
    setEditingRonda(null)
    setShowRondaModal(true)
  }

  const handleOpenEditRonda = (ronda: RaffleRoundResponse) => {
    saveRound.reset()
    setEditingRonda(ronda)
    setShowRondaModal(true)
  }

  const handleSaveRonda = (nombre: string, descripcion?: string) => {
    saveRound.mutate(
      { roundId: editingRonda?.id, body: { name: nombre, description: descripcion ?? null } },
      {
        onSuccess: () => {
          setShowRondaModal(false)
          setEditingRonda(null)
        },
      },
    )
  }

  const errorDeArmado = deleteRound.error ?? deletePrize.error

  return (
    <div className="space-y-6">
      <Breadcrumbs
        items={[
          { label: 'Inicio', to: '/app' },
          { label: 'Rifas en vivo', to: '/sorteo' },
          { label: 'Todos los sorteos', to: '/sorteo/sorteos' },
          { label: nombreEvento },
        ]}
      />

      {/* Encabezado: el evento, su estado y la acción principal, sobre la franja de la marca */}
      <section className="relative z-20 rounded-3xl border border-slate-200 bg-card shadow-sm dark:border-slate-800">
        <div className="relative rounded-t-3xl bg-brand-deep px-5 py-6 text-white sm:px-8">
          <div className="relative flex flex-col gap-5 lg:flex-row lg:items-center lg:justify-between">
            <div className="flex min-w-0 items-center gap-4">
              <div className="flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl bg-white p-1.5 shadow-sm">
                {branding.logoUrl ? (
                  <img
                    src={branding.logoUrl}
                    alt={branding.companyName}
                    className="h-full w-full object-contain"
                  />
                ) : (
                  <Gift className="h-7 w-7 text-brand" />
                )}
              </div>
              <div className="min-w-0">
                <p className="text-[11px] font-bold uppercase tracking-[0.18em] text-white/70">
                  {branding.companyName}
                </p>
                <h1 className="line-clamp-2 text-xl font-black leading-tight tracking-tight [overflow-wrap:anywhere] sm:truncate sm:text-3xl">
                  {nombreEvento}
                </h1>
                <div className="mt-1.5 flex flex-wrap items-center gap-2 text-xs text-white/80">
                  <EstadoSorteoBadge estado={sorteo.state} />
                  {evento && (
                    <span className="inline-flex items-center gap-1">
                      <CalendarDays className="h-3.5 w-3.5" />
                      {formatDate(evento.startDate)}
                    </span>
                  )}
                </div>
              </div>
            </div>

            <div className="flex flex-wrap items-center gap-2">
              {/* Acciones del sorteo: el mismo menú flotante que el de los tres puntos. Solo lo
                  deshabilitado lleva globo, para decir por qué. */}
              <Button
                ref={accionesRef}
                variant="outline"
                size="sm"
                onClick={() => setShowSecondaryMenu((v) => !v)}
                aria-haspopup="menu"
                aria-expanded={showSecondaryMenu}
                className="cursor-pointer rounded-xl border-white bg-white px-4 py-2 text-sm font-semibold text-brand shadow-lg hover:bg-white/90 flex items-center gap-1.5"
              >
                <span>Acciones</span>
                <ChevronDown
                  className={cn(
                    'ml-0.5 h-3.5 w-3.5 opacity-70 transition-transform',
                    showSecondaryMenu && 'rotate-180',
                  )}
                />
              </Button>
              <ActionMenu
                open={showSecondaryMenu}
                onClose={() => setShowSecondaryMenu(false)}
                anchorRef={accionesRef}
                className="w-60"
              >
                {completarPrincipal ? (
                  <MenuItem
                    disabled={!canDraw.allowed}
                    title={canDraw.allowed ? undefined : completarTitle}
                    onClick={abrirCompletar}
                  >
                    <CheckCircle2 className="h-4 w-4" /> Completar sorteo
                  </MenuItem>
                ) : (
                  <MenuItem
                    disabled={iniciarDeshabilitado}
                    title={iniciarDeshabilitado ? iniciarTitle : undefined}
                    onClick={() => {
                      setShowSecondaryMenu(false)
                      setShowIniciarModal(true)
                    }}
                  >
                    <Play className="h-4 w-4" /> {textoBotonAccion}
                  </MenuItem>
                )}
                <MenuItem
                  onClick={() => {
                    setShowSecondaryMenu(false)
                    setShowEscaner(true)
                  }}
                >
                  <QrCode className="h-4 w-4" /> Canjear QR
                </MenuItem>
                {!sinPuerta(sorteo) && (
                  <MenuItem
                    onClick={() => {
                      setShowSecondaryMenu(false)
                      window.open('/scanner', '_blank')
                    }}
                  >
                    <Smartphone className="h-4 w-4" /> Terminal escáner QR
                  </MenuItem>
                )}
                <MenuSeparator />
                <MenuItem
                  disabled={!actaVigente}
                  title={actaVigente ? undefined : 'Se genera al completar el sorteo.'}
                  onClick={descargarActa}
                >
                  <FileText className="h-4 w-4" /> Descargar acta notarial
                </MenuItem>
                <MenuItem
                  onClick={() => {
                    setShowSecondaryMenu(false)
                    handleExportarCSV()
                  }}
                >
                  <Download className="h-4 w-4" /> Exportar resultados (CSV)
                </MenuItem>
                {sorteo.state === 'in_progress' && (
                  <>
                    <MenuSeparator />
                    {!completarPrincipal && (
                      <MenuItem
                        disabled={!canDraw.allowed}
                        title={canDraw.allowed ? undefined : completarTitle}
                        onClick={abrirCompletar}
                      >
                        <CheckCircle2 className="h-4 w-4" /> Completar sorteo
                      </MenuItem>
                    )}
                    <MenuItem
                      tone="danger"
                      disabled={reopenDisabled}
                      title={reopenDisabled ? reopenTitle : undefined}
                      onClick={() => {
                        setShowSecondaryMenu(false)
                        reopenRaffle.reset()
                        setShowReopen(true)
                      }}
                    >
                      <Undo2 className="h-4 w-4" /> Reabrir sorteo
                    </MenuItem>
                  </>
                )}
              </ActionMenu>
            </div>
          </div>
        </div>

        {/* Indicadores: el avance de la noche de un vistazo */}
        <dl className="grid grid-cols-3 divide-slate-100 lg:grid-cols-4 lg:divide-x dark:divide-slate-800">
          <div className="col-span-3 p-4 sm:p-5 lg:col-span-1">
            <dt className="text-[10px] font-bold uppercase tracking-wide text-muted-foreground sm:text-[11px] sm:tracking-wider">
              Premios sorteados
            </dt>
            <dd className="mt-1 text-xl font-black sm:text-2xl text-foreground">
              {premiosSorteados}
              <span className="text-base font-bold text-muted-foreground"> / {totalPremios}</span>
            </dd>
            <div
              className="mt-2 h-2 overflow-hidden rounded-full bg-muted"
              role="progressbar"
              aria-label="Premios sorteados"
              aria-valuemin={0}
              aria-valuemax={totalPremios}
              aria-valuenow={premiosSorteados}
            >
              <div
                className="h-full rounded-full bg-brand-accent transition-all"
                style={{ width: `${totalPremios ? (premiosSorteados / totalPremios) * 100 : 0}%` }}
              />
            </div>
          </div>
          <div className="min-w-0 p-4 sm:p-5">
            <dt className="text-[10px] font-bold uppercase tracking-wide text-muted-foreground sm:text-[11px] sm:tracking-wider">
              Participantes
            </dt>
            <dd className="mt-1 text-xl font-black sm:text-2xl text-foreground">
              {totalParticipantes}
            </dd>
            <p className="text-xs text-muted-foreground">
              {sinPuerta(sorteo) ? 'Todos participan' : `${conCheckIn} con check-in`}
            </p>
          </div>
          <div
            className="min-w-0 p-4 sm:p-5"
            title={
              nextPrize
                ? `Para el próximo premio por sortear: ${nextPrize.name}`
                : 'Solo con el sorteo en curso y un premio por sortear.'
            }
          >
            <dt className="text-[10px] font-bold uppercase tracking-wide text-muted-foreground sm:text-[11px] sm:tracking-wider">
              En la tómbola
            </dt>
            <dd className="mt-1 text-xl font-black sm:text-2xl text-brand dark:text-brand-accent">
              {nextPrize && nextPrizePool ? nextPrizePool.count : '—'}
            </dd>
            <p className="text-xs text-muted-foreground">para el próximo premio</p>
          </div>
          <div className="min-w-0 p-4 sm:p-5">
            <dt className="text-[10px] font-bold uppercase tracking-wide text-muted-foreground sm:text-[11px] sm:tracking-wider">
              Entregados
            </dt>
            <dd className="mt-1 text-xl font-black sm:text-2xl text-emerald-600 dark:text-emerald-400">
              {premiosEntregados}
            </dd>
            <p className="text-xs text-muted-foreground">de {ganadores.length} ganadores</p>
          </div>
        </dl>

        {/* Navegación de la ficha */}
        <div className="border-t border-slate-100 px-6 dark:border-slate-800">
          <Tabs
            value={activeTab}
            onChange={setActiveTab}
            variant="underline"
            items={[
              { key: 'premios', label: `Premios (${totalPremios})` },
              { key: 'participantes', label: `Participantes (${totalParticipantes})` },
              { key: 'ganadores', label: `Ganadores (${ganadores.length})` },
              { key: 'reglas', label: 'Configuración' },
            ]}
          />
        </div>
      </section>

      {actaError != null && <ErrorNote error={actaError} />}

      {sorteo.state === 'draft' && bloqueos.length > 0 && (
        <WarningNote>
          <p className="font-bold">El sorteo todavía no puede iniciarse:</p>
          <ul className="list-disc pl-5">
            {bloqueos.map((b) => (
              <li key={b}>{raffleBlockingReasonLabel[b]}</li>
            ))}
          </ul>
        </WarningNote>
      )}

      {/* El panel lateral (próximo premio y preparación) solo acompaña a Premios: en las demás
          pestañas la tabla usa todo el ancho y no queda apretada. */}
      <div
        className={cn(
          'grid gap-6',
          activeTab === 'premios' && 'xl:grid-cols-[minmax(0,1fr)_300px]',
        )}
      >
        <div className="min-w-0 space-y-6">
          {/* Tab 1: Rondas & Premios */}
          {activeTab === 'premios' && (
            <div className="space-y-6">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div>
                  <h3 className="text-base font-bold text-slate-900 dark:text-slate-100">
                    Rondas y premios
                  </h3>
                  <p className="text-xs text-slate-500">
                    {sorteo.rounds.length} ronda(s) · {prizesOf(sorteo).length} premio(s)
                  </p>
                </div>

                <div className="flex items-center gap-2">
                  <ViewModeToggle value={vistaPremios} onChange={setVistaPremios} size="sm" />
                  <div className="relative">
                    <Button
                      size="sm"
                      onClick={() => setShowAccionesPremios((v) => !v)}
                      disabled={!canSetup}
                      title={setupTitle}
                      aria-expanded={showAccionesPremios}
                      className="text-xs font-bold bg-brand hover:bg-brand/90 text-white cursor-pointer"
                    >
                      Agregar
                      <ChevronDown className="w-3 h-3 ml-1 opacity-80" />
                    </Button>
                    {showAccionesPremios && (
                      <>
                        <div
                          className="fixed inset-0 z-20 cursor-default"
                          onClick={() => setShowAccionesPremios(false)}
                        />
                        <div className="absolute right-0 z-50 mt-1.5 w-max min-w-40 rounded-2xl border border-slate-200 bg-white py-2 text-left shadow-2xl dark:border-slate-700 dark:bg-slate-900">
                          <button
                            type="button"
                            onClick={() => {
                              setShowAccionesPremios(false)
                              handleOpenAddRonda()
                            }}
                            className="w-full flex items-center whitespace-nowrap px-4 py-2.5 text-xs text-slate-700 dark:text-slate-200 hover:bg-slate-50 dark:hover:bg-slate-800 font-semibold cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
                          >
                            Nueva ronda
                          </button>
                          <button
                            type="button"
                            disabled={sorteo.rounds.length === 0}
                            title={
                              sorteo.rounds.length === 0 ? 'Primero cree una ronda.' : undefined
                            }
                            onClick={() => {
                              setShowAccionesPremios(false)
                              handleOpenAddPremio()
                            }}
                            className="w-full flex items-center whitespace-nowrap px-4 py-2.5 text-xs text-slate-700 dark:text-slate-200 hover:bg-slate-50 dark:hover:bg-slate-800 font-semibold cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
                          >
                            Agregar premio
                          </button>
                          <button
                            type="button"
                            onClick={() => {
                              setShowAccionesPremios(false)
                              setShowImportarPremios(true)
                            }}
                            className="w-full flex items-center whitespace-nowrap px-4 py-2.5 text-xs text-slate-700 dark:text-slate-200 hover:bg-slate-50 dark:hover:bg-slate-800 font-semibold cursor-pointer"
                          >
                            Importar premios (Excel/CSV)
                          </button>
                          <div className="my-1 border-t border-slate-100 dark:border-slate-800" />
                          <button
                            type="button"
                            disabled={sorteo.state !== 'draft'}
                            title={
                              sorteo.state !== 'draft'
                                ? 'Solo en un sorteo en borrador.'
                                : 'Datos de prueba para ensayar o enseñar el sorteo.'
                            }
                            onClick={() => {
                              setShowAccionesPremios(false)
                              setShowSimulacion(true)
                            }}
                            className="w-full flex items-center whitespace-nowrap px-4 py-2.5 text-xs text-slate-700 dark:text-slate-200 hover:bg-slate-50 dark:hover:bg-slate-800 font-semibold cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
                          >
                            Simular {SIMULACION.empleados} empleados y{' '}
                            {SIMULACION.premios + SIMULACION.principales} premios
                          </button>
                        </div>
                      </>
                    )}
                  </div>
                </div>
              </div>

              {errorDeArmado != null && <ErrorNote error={errorDeArmado} />}

              {sorteo.rounds.length === 0 && (
                <div className="rounded-xl border border-dashed border-slate-300 dark:border-slate-700 py-10 text-center text-xs text-slate-500">
                  Este sorteo todavía no tiene rondas. Cree la primera para agregarle premios.
                </div>
              )}

              {/* Rondas como acordeón: se abren la primera y las que tienen premios por sortear */}
              <div className="space-y-4">
                {sorteo.rounds.map((ronda, i) => (
                  <RondaPremios
                    key={ronda.id}
                    ronda={ronda}
                    vista={vistaPremios}
                    inicialmenteAbierta={i === 0 || ronda.prizes.some((p) => p.state === 'undrawn')}
                    canSetup={canSetup}
                    setupTitle={setupTitle}
                    onEditar={() => handleOpenEditRonda(ronda)}
                    onEliminar={() => deleteRound.mutate(ronda.id)}
                    onAgregarPremio={() => handleOpenAddPremio(ronda.id)}
                    onEliminarPremio={(premio) => deletePrize.mutate(premio.id)}
                    // La foto no cambia el sorteo: se puede poner también con el sorteo en curso.
                    onImagenPremio={
                      !canSetup
                        ? undefined
                        : (premio, foto) => {
                            // Los premios iguales de la ronda comparten la foto.
                            const prizeIds = ronda.prizes
                              .filter((p) => p.name === premio.name && p.value === premio.value)
                              .map((p) => p.id)
                            prizeImage.mutate(
                              { prizeIds, foto },
                              {
                                onSuccess: () =>
                                  toast.success(
                                    foto
                                      ? `Foto puesta en ${prizeIds.length} premio(s).`
                                      : `Foto quitada de ${prizeIds.length} premio(s).`,
                                  ),
                                onError: (err) => toast.error(errorMessage(err)),
                              },
                            )
                          }
                    }
                  />
                ))}
              </div>
            </div>
          )}

          {/* Tab 2: Participantes */}
          {activeTab === 'participantes' && (
            <div className="space-y-3">
              {canWrite.allowed && (
                <div className="flex justify-end">
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    disabled={sorteo.state !== 'draft'}
                    title={
                      sorteo.state !== 'draft'
                        ? 'El bombo ya está congelado: solo se excluye en lote con el sorteo en borrador.'
                        : undefined
                    }
                    onClick={() => setExcluirEnLote(true)}
                  >
                    <UserMinus className="mr-1 h-4 w-4" /> Excluir en lote
                  </Button>
                </div>
              )}
              <ParticipantesTable raffle={sorteo} />
              {excluirEnLote && (
                <ExcluirEnLoteModal
                  eventId={sorteo.eventId}
                  onClose={() => setExcluirEnLote(false)}
                />
              )}
            </div>
          )}

          {/* Tab 3: Ganadores */}
          {activeTab === 'ganadores' && (
            <div className="space-y-4">
              <AnuladosCard raffle={sorteo} />
              <div className="rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-card pb-24">
                <table className="w-full text-left border-collapse text-xs">
                  <thead>
                    <tr className="border-b border-slate-200 bg-slate-50 font-bold uppercase tracking-wider text-slate-600 dark:border-slate-800 dark:bg-slate-900/60 dark:text-slate-300">
                      <th className="px-4 py-3">Ganador</th>
                      <th className="px-4 py-3">Empresa</th>
                      <th className="px-4 py-3">Premio Obtenido</th>
                      <th className="px-4 py-3">Ronda</th>
                      <th className="px-4 py-3">Fecha y Hora</th>
                      <th className="px-4 py-3">Estado Entrega</th>
                      <th className="px-4 py-3">Código / ID</th>
                      <th className="px-4 py-3 text-right">Acciones</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                    {prizeDraws.length === 0 ? (
                      <tr>
                        <td colSpan={8} className="px-4 py-8 text-center text-slate-400">
                          Aún no se han sorteado premios en este sorteo.
                        </td>
                      </tr>
                    ) : (
                      ganadoresDePagina.map(({ premio, draw }) => {
                        const ronda = sorteo.rounds.find((r) => r.id === premio.roundId)
                        const entrega = draw.void === null ? liveHandover(draw) : undefined
                        const voidable =
                          draw.void === null &&
                          premio.state === 'drawn' &&
                          sorteo.state === 'in_progress'
                        return (
                          <tr
                            key={draw.id}
                            className={`hover:bg-slate-50/60 dark:hover:bg-slate-800/40 ${
                              draw.void ? 'opacity-60' : ''
                            }`}
                          >
                            <td
                              className={`px-4 py-3 font-bold text-slate-900 dark:text-slate-100 ${
                                draw.void ? 'line-through' : ''
                              }`}
                            >
                              {draw.winnerName}
                            </td>
                            <td className="px-4 py-3 text-slate-600 dark:text-slate-400">
                              {draw.winnerCompany ?? '—'}
                            </td>
                            <td className="px-4 py-3 font-semibold text-blue-600 dark:text-blue-400">
                              {premio.name}
                              {premio.value && (
                                <span className="block text-[10px] text-slate-500 font-normal">
                                  Valor: {premio.value}
                                </span>
                              )}
                            </td>
                            <td className="px-4 py-3 text-slate-600 dark:text-slate-400">
                              {ronda?.name ?? '—'}
                            </td>
                            <td className="px-4 py-3 text-slate-500">
                              {formatDateTime(draw.drawnAt)}
                            </td>
                            <td className="px-4 py-3">
                              {draw.void ? (
                                <span
                                  className="inline-flex items-center gap-1 text-rose-700 dark:text-rose-400 font-bold"
                                  title={draw.void.note ?? undefined}
                                >
                                  <Ban size={13} />
                                  Anulado: {prizeDrawVoidCodeLabel[draw.void.code]}
                                </span>
                              ) : entrega ? (
                                <span className="text-emerald-700 dark:text-emerald-400 font-bold">
                                  <span className="inline-flex items-center gap-1">
                                    <CheckCircle2 size={13} />
                                    {prizeStateLabel.redeemed}
                                  </span>
                                  <span className="block text-[10px] font-normal text-slate-500 dark:text-slate-400">
                                    {entrega.receiverIsWinner
                                      ? `Retiró: ${entrega.receiverName}`
                                      : `Retiró en su nombre: ${entrega.receiverName}`}
                                  </span>
                                </span>
                              ) : (
                                <span className="text-slate-500 dark:text-slate-400 font-semibold">
                                  {prizeStateLabel[premio.state]}
                                </span>
                              )}
                            </td>
                            <td className="px-4 py-3 font-mono text-slate-600 dark:text-slate-400">
                              {draw.winnerEmployeeCode || draw.winnerTicketNumber || '—'}
                            </td>
                            <td className="px-4 py-3 text-right whitespace-nowrap">
                              {draw.void === null && (
                                <div className="relative inline-block text-left">
                                  <button
                                    type="button"
                                    onClick={() =>
                                      setMenuDrawId(menuDrawId === draw.id ? null : draw.id)
                                    }
                                    className="p-1.5 rounded-lg text-slate-500 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
                                    title="Acciones"
                                    aria-label={`Acciones de ${draw.winnerName}`}
                                  >
                                    <MoreVertical className="w-4 h-4" />
                                  </button>
                                  {menuDrawId === draw.id && (
                                    <>
                                      <div
                                        className="fixed inset-0 z-20 cursor-default"
                                        onClick={() => setMenuDrawId(null)}
                                      />
                                      <div className="absolute right-0 mt-1 w-52 rounded-xl bg-white dark:bg-card shadow-2xl border border-slate-200 dark:border-slate-800 py-1.5 z-30 text-left">
                                        {voidable && (
                                          <button
                                            type="button"
                                            disabled={!canDraw.allowed}
                                            title={voidTitle}
                                            onClick={() => {
                                              setMenuDrawId(null)
                                              setVoiding({ prize: premio, draw })
                                            }}
                                            className={menuItemClass}
                                          >
                                            <Ban className="w-4 h-4 text-rose-600 dark:text-rose-400" />
                                            Anular ganador
                                          </button>
                                        )}
                                        {entrega ? (
                                          <button
                                            type="button"
                                            disabled={!canDraw.allowed}
                                            title={
                                              canDraw.allowed
                                                ? 'La entrega queda en el historial con este motivo'
                                                : canDraw.title
                                            }
                                            onClick={() => {
                                              setMenuDrawId(null)
                                              setRevirtiendo({
                                                prize: premio,
                                                draw,
                                                handover: entrega,
                                              })
                                            }}
                                            className={menuItemClass}
                                          >
                                            <Undo2 className="w-4 h-4 text-rose-600 dark:text-rose-400" />
                                            Revertir entrega
                                          </button>
                                        ) : (
                                          <button
                                            type="button"
                                            disabled={!canDraw.allowed}
                                            title={canDraw.allowed ? undefined : canDraw.title}
                                            onClick={() => {
                                              setMenuDrawId(null)
                                              setEntrega(premio.id)
                                            }}
                                            className={menuItemClass}
                                          >
                                            <PackageCheck className="w-4 h-4 text-emerald-600 dark:text-emerald-400" />
                                            Registrar entrega
                                          </button>
                                        )}
                                        <button
                                          type="button"
                                          onClick={() => {
                                            setMenuDrawId(null)
                                            setCertificado({ prize: premio, draw })
                                          }}
                                          className={menuItemClass}
                                        >
                                          <Award className="w-4 h-4 text-amber-600 dark:text-amber-400" />
                                          Certificado
                                        </button>
                                      </div>
                                    </>
                                  )}
                                </div>
                              )}
                            </td>
                          </tr>
                        )
                      })
                    )}
                  </tbody>
                </table>
                {prizeDraws.length > 0 && (
                  <Pagination
                    page={paginaGanadoresActual}
                    pageCount={paginasGanadores}
                    onPageChange={setPaginaGanadores}
                    total={prizeDraws.length}
                    className="border-t border-slate-100 px-4 py-3 dark:border-slate-800"
                  />
                )}
              </div>
            </div>
          )}

          {/* Tab 4: Reglas & Auditoría */}
          {activeTab === 'reglas' && (
            <div className="space-y-4">
              {updateRaffle.error != null && <ErrorNote error={updateRaffle.error} />}
              <PersonalizacionMarcaCard eventId={eventId} canEdit={canSetup} />
              <CamposVisiblesCard
                eventId={eventId}
                sorteo={sorteo}
                canEdit={canSetup}
                title={setupTitle}
              />
              <SorteosVinculadosCard
                sorteo={sorteo}
                canEdit={canSetup}
                title={setupTitle}
                onSave={saveChanges}
              />
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                {/* Card Reglas de Elegibilidad */}
                <Card className="p-6 bg-white dark:bg-card border border-slate-200 dark:border-slate-800 space-y-4">
                  <div className="flex items-center gap-2 border-b border-slate-100 dark:border-slate-800 pb-3">
                    <ShieldCheck className="w-5 h-5 text-blue-600 dark:text-blue-400" />
                    <h3 className="text-base font-bold text-slate-900 dark:text-white">
                      Reglas de Transparencia & Anti-Duplicados
                    </h3>
                  </div>

                  <div className="space-y-4">
                    <label className="flex items-start gap-3 cursor-pointer" title={setupTitle}>
                      <input
                        type="checkbox"
                        checked={sorteo.requiresCheckIn !== false}
                        disabled={!canSetup || sorteo.state !== 'draft'}
                        onChange={(e) => saveChanges({ requiresCheckIn: e.target.checked })}
                        className="mt-0.5 rounded border-slate-300 text-blue-600 focus:ring-blue-600"
                      />
                      <div>
                        <span className="text-sm font-bold text-slate-900 dark:text-white block">
                          Solo participan quienes llegaron (check-in)
                        </span>
                        <span className="text-xs text-slate-500 block">
                          Apagado, entra a la tómbola todo el padrón aunque no haya pasado por la
                          puerta. Se decide antes de iniciar: al iniciar, la tómbola se congela.
                        </span>
                      </div>
                    </label>

                    {/* Un premio por persona no es un interruptor: el servicio lo aplica siempre. */}
                    <div className="flex items-start gap-3">
                      <ShieldCheck className="mt-0.5 h-4 w-4 shrink-0 text-emerald-600" />
                      <div>
                        <span className="text-sm font-bold text-slate-900 dark:text-white block">
                          Un solo premio por persona (siempre)
                        </span>
                        <span className="text-xs text-slate-500 block">
                          Quien gana queda guardado como ganador y fuera de los premios siguientes
                          de la ronda y del sorteo. No se puede desactivar.
                        </span>
                      </div>
                    </div>

                    <label className="flex items-start gap-3 cursor-pointer" title={setupTitle}>
                      <input
                        type="checkbox"
                        checked={sorteo.onePrizePerCompany}
                        disabled={!canSetup}
                        onChange={(e) => saveChanges({ onePrizePerCompany: e.target.checked })}
                        className="mt-0.5 rounded border-slate-300 text-blue-600 focus:ring-blue-600"
                      />
                      <div>
                        <span className="text-sm font-bold text-slate-900 dark:text-white block">
                          Un solo premio por departamento en cada ronda
                        </span>
                        <span className="text-xs text-slate-500 block">
                          Dentro de una ronda, dos personas del mismo departamento no ganan. Sin
                          departamento en el padrón, se agrupa por empresa o ubicación.
                        </span>
                      </div>
                    </label>

                    {sorteo.state === 'in_progress' && (
                      <p className="text-xs text-slate-500 dark:text-slate-400">
                        Un cambio aplica desde el próximo premio sorteado; los ya sorteados
                        conservan sus reglas.
                      </p>
                    )}

                    <label className="flex items-start gap-3 cursor-pointer" title={setupTitle}>
                      <input
                        type="checkbox"
                        checked={sorteo.requiresPresenceConfirmation}
                        disabled={!canSetup}
                        onChange={(e) =>
                          saveChanges({ requiresPresenceConfirmation: e.target.checked })
                        }
                        className="mt-0.5 rounded border-slate-300 text-blue-600 focus:ring-blue-600"
                      />
                      <div>
                        <span className="text-sm font-bold text-slate-900 dark:text-white block">
                          Temporizador de Confirmación de Presencia en Sala (60s)
                        </span>
                        <span className="text-xs text-slate-500 block">
                          Muestra un contador de 60 segundos en la pantalla del ganador para
                          confirmar que se encuentra presente.
                        </span>
                      </div>
                    </label>
                  </div>
                </Card>

                {/* Card Configuración de Testigos y Notario para Acta */}
                <Card className="p-6 bg-white dark:bg-card border border-slate-200 dark:border-slate-800 space-y-4">
                  <div className="flex items-center gap-2 border-b border-slate-100 dark:border-slate-800 pb-3">
                    <FileText className="w-5 h-5 text-blue-600 dark:text-blue-400" />
                    <h3 className="text-base font-bold text-slate-900 dark:text-white">
                      Autoridades y Testigos del Acta
                    </h3>
                  </div>

                  <p className="text-xs text-slate-500">
                    Se guardan al salir de cada campo. Los seis datos se exigen para completar el
                    sorteo.
                  </p>

                  <div className="space-y-3">
                    <Autoridad
                      etiqueta="Notario / Operador Responsable"
                      placeholderNombre="Ej: Lic. Manuel Rodríguez (Notario Público)"
                      nombre={sorteo.notaryName}
                      documento={sorteo.notaryIdentityDocument}
                      disabled={!canSetup}
                      title={setupTitle}
                      errorNombre={fieldError(updateRaffle.error, 'NotaryName')}
                      errorDocumento={fieldError(updateRaffle.error, 'NotaryIdentityDocument')}
                      onSaveNombre={(v) => saveChanges({ notaryName: v })}
                      onSaveDocumento={(v) => saveChanges({ notaryIdentityDocument: v })}
                    />
                    <Autoridad
                      etiqueta="Testigo 1 (Comité Organizador)"
                      placeholderNombre="Ej: Licda. Carolina Ortiz (Dirección de Promoción)"
                      nombre={sorteo.witnessOneName}
                      documento={sorteo.witnessOneIdentityDocument}
                      disabled={!canSetup}
                      title={setupTitle}
                      errorNombre={fieldError(updateRaffle.error, 'WitnessOneName')}
                      errorDocumento={fieldError(updateRaffle.error, 'WitnessOneIdentityDocument')}
                      onSaveNombre={(v) => saveChanges({ witnessOneName: v })}
                      onSaveDocumento={(v) => saveChanges({ witnessOneIdentityDocument: v })}
                    />
                    <Autoridad
                      etiqueta="Testigo 2 (Auditor / Supervisor)"
                      placeholderNombre="Ej: Ing. David Ramos (Auditoría Técnica)"
                      nombre={sorteo.witnessTwoName}
                      documento={sorteo.witnessTwoIdentityDocument}
                      disabled={!canSetup}
                      title={setupTitle}
                      errorNombre={fieldError(updateRaffle.error, 'WitnessTwoName')}
                      errorDocumento={fieldError(updateRaffle.error, 'WitnessTwoIdentityDocument')}
                      onSaveNombre={(v) => saveChanges({ witnessTwoName: v })}
                      onSaveDocumento={(v) => saveChanges({ witnessTwoIdentityDocument: v })}
                    />
                  </div>
                </Card>

                {/* Card Instrucciones de Retiro: van en el correo de cada ganador */}
                <Card className="p-6 bg-white dark:bg-card border border-slate-200 dark:border-slate-800 space-y-4">
                  <div className="flex items-center gap-2 border-b border-slate-100 dark:border-slate-800 pb-3">
                    <Gift className="w-5 h-5 text-blue-600 dark:text-blue-400" />
                    <h3 className="text-base font-bold text-slate-900 dark:text-white">
                      Instrucciones de retiro de premios
                    </h3>
                  </div>
                  <p className="text-xs text-slate-500">
                    Dónde y hasta cuándo se retiran los premios. Van en el correo de cada ganador al
                    completar el sorteo, y se exigen para completarlo. Se guardan al salir del
                    campo.
                  </p>
                  <CampoAlSalir
                    key={`retiro:${sorteo.pickupInstructions ?? ''}`}
                    multiline
                    value={sorteo.pickupInstructions}
                    placeholder="Ej: Mesa de entrega de premios en el salón principal, hasta las 22:00"
                    disabled={!canSetup}
                    title={setupTitle}
                    onSave={(v) => saveChanges({ pickupInstructions: v })}
                  />
                  {fieldError(updateRaffle.error, 'PickupInstructions') && (
                    <p className="text-[11px] font-medium text-rose-600 dark:text-rose-400">
                      {fieldError(updateRaffle.error, 'PickupInstructions')}
                    </p>
                  )}
                </Card>

                <FirmantesCertificadoCard
                  eventId={eventId}
                  signers={sorteo.certificateSigners}
                  disabled={!canWrite.allowed || eventoCerrado}
                  title={firmantesTitle}
                />

                <ActaNotarialCard eventId={eventId} raffle={sorteo} />
              </div>
              <BitacoraSorteoCard raffle={sorteo} nombre={nombreEvento} />
            </div>
          )}
        </div>

        {/* Panel de control: lo próximo que hay que hacer y qué falta preparar */}
        {activeTab === 'premios' && (
          <aside className="space-y-4 xl:sticky xl:top-20 xl:self-start">
            <section className="rounded-2xl border border-slate-200 bg-card p-5 shadow-sm dark:border-slate-800">
              <h2 className="text-[11px] font-bold uppercase tracking-wider text-muted-foreground">
                {sorteo.state === 'completed'
                  ? 'Sorteo completado'
                  : nextPrize
                    ? 'Próximo premio'
                    : sorteo.state === 'draft'
                      ? 'Antes de empezar'
                      : 'Todo sorteado'}
              </h2>
              {nextPrize ? (
                <div className="mt-3 space-y-3">
                  <div className="flex items-start gap-3">
                    <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-brand/10 text-brand dark:text-brand-accent">
                      <Gift className="h-5 w-5" />
                    </div>
                    <div className="min-w-0">
                      <p className="font-black leading-snug text-foreground">{nextPrize.name}</p>
                      {nextPrize.value && (
                        <p className="text-sm font-bold text-brand dark:text-brand-accent">
                          {nextPrize.value}
                        </p>
                      )}
                      <p className="text-xs text-muted-foreground">
                        {nextPrizePool
                          ? `${nextPrizePool.count} personas en la tómbola`
                          : 'Leyendo la tómbola…'}
                      </p>
                    </div>
                  </div>
                  <Button
                    onClick={() => setShowIniciarModal(true)}
                    disabled={iniciarDeshabilitado}
                    title={iniciarTitle}
                    className="w-full cursor-pointer rounded-xl bg-brand py-2.5 text-sm font-black text-white hover:bg-brand/90 disabled:cursor-not-allowed disabled:opacity-60"
                  >
                    <Play className="mr-1.5 h-4 w-4 fill-current" /> Sortear en vivo
                  </Button>
                </div>
              ) : sorteo.state === 'completed' ? (
                <p className="mt-2 text-sm text-muted-foreground">
                  El acta está generada y los ganadores quedaron registrados.
                </p>
              ) : sorteo.state === 'draft' ? (
                <p className="mt-2 text-sm text-muted-foreground">
                  Completa la lista y pulsa <strong>{textoBotonAccion}</strong> para congelar la
                  tómbola.
                </p>
              ) : (
                <Button
                  onClick={abrirCompletar}
                  disabled={!canDraw.allowed}
                  title={completarTitle}
                  className="mt-3 w-full cursor-pointer rounded-xl bg-emerald-600 py-2.5 text-sm font-black text-white hover:bg-emerald-700 disabled:cursor-not-allowed disabled:opacity-60"
                >
                  <CheckCircle2 className="mr-1.5 h-4 w-4" /> Completar sorteo
                </Button>
              )}
            </section>

            <section className="rounded-2xl border border-slate-200 bg-card p-5 shadow-sm dark:border-slate-800">
              <h2 className="text-[11px] font-bold uppercase tracking-wider text-muted-foreground">
                Preparación
              </h2>
              <ul className="mt-3 space-y-1">
                {preparacion.map((paso) => (
                  <li key={paso.texto}>
                    <button
                      type="button"
                      onClick={() => setActiveTab(paso.pestana)}
                      className="flex w-full cursor-pointer items-center gap-2.5 rounded-lg px-2 py-1.5 text-left text-sm hover:bg-muted"
                    >
                      {paso.listo ? (
                        <CheckCircle2 className="h-4 w-4 shrink-0 text-emerald-600 dark:text-emerald-400" />
                      ) : (
                        <Circle className="h-4 w-4 shrink-0 text-slate-300 dark:text-slate-600" />
                      )}
                      <span className={paso.listo ? 'text-foreground' : 'text-muted-foreground'}>
                        {paso.texto}
                      </span>
                    </button>
                  </li>
                ))}
              </ul>
            </section>

            <section className="rounded-2xl border border-slate-200 bg-card p-2 shadow-sm dark:border-slate-800">
              {!sinPuerta(sorteo) && (
                <button
                  type="button"
                  onClick={() => window.open('/scanner', '_blank')}
                  className="flex w-full cursor-pointer items-center gap-2.5 rounded-xl px-3 py-2.5 text-sm font-semibold text-foreground hover:bg-muted"
                >
                  <Smartphone className="h-4 w-4 text-brand dark:text-brand-accent" /> Escáner de la
                  puerta
                </button>
              )}
              <button
                type="button"
                onClick={handleExportarCSV}
                className="flex w-full cursor-pointer items-center gap-2.5 rounded-xl px-3 py-2.5 text-sm font-semibold text-foreground hover:bg-muted"
              >
                <Download className="h-4 w-4 text-brand dark:text-brand-accent" /> Exportar
                resultados (CSV)
              </button>
              <button
                type="button"
                disabled={!actaVigente}
                title={actaVigente ? undefined : 'Se genera al completar el sorteo.'}
                onClick={descargarActa}
                className="flex w-full cursor-pointer items-center gap-2.5 rounded-xl px-3 py-2.5 text-sm font-semibold text-foreground hover:bg-muted disabled:cursor-not-allowed disabled:opacity-50"
              >
                <FileText className="h-4 w-4 text-brand dark:text-brand-accent" /> Descargar acta
              </button>
            </section>
          </aside>
        )}
      </div>

      {/* Modal Configurar Ronda */}
      {showRondaModal && (
        <ConfigurarRondaModal
          numeroRonda={editingRonda ? editingRonda.number : sorteo.rounds.length + 1}
          initialNombre={editingRonda?.name}
          initialDescripcion={editingRonda?.description ?? undefined}
          saving={saveRound.isPending}
          error={saveRound.error}
          onClose={() => setShowRondaModal(false)}
          onSave={handleSaveRonda}
        />
      )}

      {/* Modal Agregar Premio */}
      {showAddPremioModal && (
        <ConfigurarPremiosModal
          rondas={sorteo.rounds}
          defaultRondaId={selectedRondaIdForPremio}
          saving={createPrizes.isPending}
          error={createPrizes.error}
          onClose={() => setShowAddPremioModal(false)}
          onAdd={(body, foto) =>
            createPrizes.mutate(body, {
              onSuccess: (creados) => {
                setShowAddPremioModal(false)
                if (foto)
                  prizeImage.mutate(
                    { prizeIds: creados.map((p) => p.id), foto },
                    { onError: (err) => toast.error(errorMessage(err)) },
                  )
              },
            })
          }
        />
      )}

      {showImportarPremios && (
        <ImportarPremiosModal
          eventId={eventId}
          rondas={sorteo.rounds}
          onClose={() => setShowImportarPremios(false)}
          onImported={(n) => {
            setShowImportarPremios(false)
            toast.success(`${n} premio(s) importados.`)
          }}
        />
      )}

      {/* Modal Iniciar Sorteo (Selector de Tómbola/Ruleta y Pantalla/Proyector) */}
      {showIniciarModal && (
        <IniciarSorteoModal
          eventId={eventId}
          subtitulo={nombreEvento}
          textoBotonAccion={textoBotonAccion}
          onClose={() => setShowIniciarModal(false)}
          onLaunch={(modoPantalla) => {
            setShowIniciarModal(false)
            const targetUrl = `/sorteo/sorteos/${eventId}/ejecutar`
            // Con proyector, la pantalla del público se abre aparte (para arrastrarla a la segunda
            // pantalla) y esta pestaña pasa a ser la consola del operador.
            if (modoPantalla === 'proyector') {
              window.open(rutaProyector(eventId), 'sorteo360-proyector', 'width=1366,height=768')
            }
            navigate(targetUrl)
          }}
        />
      )}

      {voiding && (
        <VoidDrawModal
          eventId={eventId}
          prize={voiding.prize}
          draw={voiding.draw}
          onVoided={() => setVoiding(null)}
          onClose={() => setVoiding(null)}
        />
      )}

      {showEscaner && (
        <EscanerCanjeModal
          raffle={sorteo}
          onEntregar={(prizeId) => {
            setShowEscaner(false)
            setEntrega(prizeId)
          }}
          onClose={() => setShowEscaner(false)}
        />
      )}

      {entregaId && (
        <EntregaPremioModal
          eventId={eventId}
          prize={premios.find((p) => p.id === entregaId)}
          onClose={() => setEntrega(null)}
        />
      )}

      {revirtiendo && (
        <RevertirEntregaModal
          eventId={eventId}
          prize={revirtiendo.prize}
          draw={revirtiendo.draw}
          handover={revirtiendo.handover}
          onClose={() => setRevirtiendo(null)}
        />
      )}

      {certificado && (
        <CertificadoPremioModal
          premio={certificado.prize}
          draw={certificado.draw}
          signers={sorteo.certificateSigners}
          sorteoNombre={nombreEvento}
          eventId={eventId}
          onClose={() => setCertificado(null)}
        />
      )}

      {showCompletar && (
        <CompletarSorteoModal
          eventId={eventId}
          raffle={sorteo}
          onClose={() => setShowCompletar(false)}
        />
      )}

      {showSimulacion && (
        <ConfirmDialog
          title="Cargar datos de simulación"
          confirmLabel="Cargar simulación"
          pending={cargarSimulacion.isPending}
          error={cargarSimulacion.error}
          onCancel={() => {
            setShowSimulacion(false)
            cargarSimulacion.reset()
          }}
          onConfirm={() =>
            cargarSimulacion.mutate(undefined, {
              onSuccess: () => {
                setShowSimulacion(false)
                toast.success(
                  `Simulación cargada: ${SIMULACION.empleados} empleados, ${SIMULACION.premios} premios y ${SIMULACION.principales} premios principales.`,
                )
              },
            })
          }
        >
          <p>
            Se agregan al sorteo <strong>{SIMULACION.empleados} empleados de prueba</strong>, todos
            presentes, y <strong>{SIMULACION.premios} premios</strong> en una ronda nueva (bonos,
            canastas y tablets) más <strong>{SIMULACION.principales} premios principales</strong>{' '}
            con foto en otra ronda al final ({PRINCIPALES.join(' y ')}), que el tablero sortea uno a
            uno.
          </p>
          <p className="mt-2 text-xs text-slate-500">
            Es para ensayar o enseñar: antes del sorteo real, quita el padrón y los premios de
            prueba, o usa un sorteo aparte.
          </p>
        </ConfirmDialog>
      )}
      {showReopen && (
        <ConfirmDialog
          title="Reabrir el sorteo"
          confirmLabel="Reabrir sorteo"
          destructive
          pending={reopenRaffle.isPending}
          error={reopenRaffle.error}
          onConfirm={() =>
            reopenRaffle.mutate(undefined, { onSuccess: () => setShowReopen(false) })
          }
          onCancel={() => setShowReopen(false)}
        >
          <p>
            El sorteo vuelve a borrador y se descarta la lista congelada al iniciarlo. Los ajustes a
            mano se conservan, y al iniciar de nuevo se vuelve a leer la puerta.
          </p>
        </ConfirmDialog>
      )}
    </div>
  )
}

/** Un firmante del acta: nombre y documento, cada uno guardado al salir de su campo. */
function Autoridad({
  etiqueta,
  placeholderNombre,
  nombre,
  documento,
  disabled,
  title,
  errorNombre,
  errorDocumento,
  onSaveNombre,
  onSaveDocumento,
}: {
  etiqueta: string
  placeholderNombre: string
  nombre: string | null
  documento: string | null
  disabled: boolean
  title?: string
  errorNombre?: string
  errorDocumento?: string
  onSaveNombre: (value: string) => void
  onSaveDocumento: (value: string) => void
}) {
  return (
    <div>
      <label className="text-xs font-bold text-slate-700 dark:text-slate-300 block mb-1">
        {etiqueta}
      </label>
      <div className="grid grid-cols-1 sm:grid-cols-[2fr_1fr] gap-2">
        {/* La clave es lo guardado: si cambia en el servicio, el borrador vuelve a partir de ahí. */}
        <CampoAlSalir
          key={`nombre:${nombre ?? ''}`}
          value={nombre}
          placeholder={placeholderNombre}
          disabled={disabled}
          title={title}
          onSave={onSaveNombre}
        />
        <CampoAlSalir
          key={`documento:${documento ?? ''}`}
          value={documento}
          placeholder="Documento de identidad"
          disabled={disabled}
          title={title}
          onSave={onSaveDocumento}
        />
      </div>
      {[errorNombre, errorDocumento].filter(Boolean).map((mensaje) => (
        <p key={mensaje} className="mt-1 text-[11px] font-medium text-rose-600 dark:text-rose-400">
          {mensaje}
        </p>
      ))}
    </div>
  )
}

/** Guarda al perder el foco, y solo si cambió: nunca una petición por tecla. */
function CampoAlSalir({
  value,
  placeholder,
  disabled,
  title,
  multiline = false,
  onSave,
}: {
  value: string | null
  placeholder: string
  disabled: boolean
  title?: string
  multiline?: boolean
  onSave: (value: string) => void
}) {
  const [draft, setDraft] = useState(value ?? '')
  const props = {
    value: draft,
    disabled,
    title,
    placeholder,
    onBlur: () => {
      if (draft.trim() !== (value ?? '')) onSave(draft)
    },
    className:
      'w-full text-xs rounded-lg border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-800 p-2 text-slate-900 dark:text-white disabled:opacity-60',
  }
  return multiline ? (
    <textarea rows={3} {...props} onChange={(e) => setDraft(e.target.value)} />
  ) : (
    <input type="text" {...props} onChange={(e) => setDraft(e.target.value)} />
  )
}
