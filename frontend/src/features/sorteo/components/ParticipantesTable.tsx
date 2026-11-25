import { useDeferredValue, useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import QRCode from 'qrcode'
import { toast } from 'sonner'
import {
  CheckCircle2,
  ChevronDown,
  Clock,
  Download,
  FileSpreadsheet,
  IdCard,
  QrCode,
  RotateCcw,
  ScanLine,
  Search,
  ShieldBan,
  ShieldCheck,
  Trash2,
  UserCheck,
  UserPlus,
  Users,
  Webhook,
  Shuffle,
  Copy,
  Pencil,
  Ticket,
} from 'lucide-react'
import type {
  AttendeeOverrideDecision,
  RaffleAttendeeResponse,
  RaffleExclusionReason,
  RaffleParticipantInput,
  RaffleParticipantItem,
  RaffleResponse,
} from '@/api/types'
import { useCan } from '@/auth/AuthProvider'
import { ROLES } from '@/auth/roles'
import { Badge } from '@/components/ui/Badge'
import { ActionMenu } from '@/components/ui/ActionMenu'
import { Button } from '@/components/ui/Button'
import { MenuItem, MenuSeparator } from '@/components/ui/MenuItem'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { EmptyState, ErrorNote } from '@/components/ui/Feedback'
import { Field, TextInput } from '@/components/ui/Field'
import { Modal } from '@/components/ui/Modal'
import { Pagination } from '@/components/ui/Pagination'
import { PasarNoGanadoresModal } from './PasarNoGanadoresModal'
import { BadgesGeneratorModal } from '@/features/convocatorias/components/BadgesGeneratorModal'
import type { Invitado } from '@/features/convocatorias/mockParticipants'
import { useEvento } from '@/features/eventos/hooks'
import { ScannerCamera } from '@/features/scanner/ScannerCamera'
import { classifyCode } from '@/features/scanner/classifyCode'
import { useBranding } from '@/lib/branding'
import { formatDateTime } from '@/lib/format'
import { attendeeOverrideDecisionLabel, raffleExclusionReasonLabel } from '@/lib/labels'
import {
  useBulkRaffleCheckIn,
  useImportRaffleParticipants,
  useRaffleAttendees,
  useRaffleParticipants,
  useRemoveAttendeeOverride,
  useRemoveRaffleParticipant,
  useSetAttendeeOverride,
  useSetRaffleParticipantCheckIn,
} from '../hooks'
import { encolar, esSinConexion, leerCola, vaciarCola } from '../colaCheckin'
import { correoOSinCorreo, sinPuerta, UNOVERRIDABLE_EXCLUSIONS } from '../raffleView'
import { useEtiquetasPadron } from '../etiquetasPadron'
import { RowActionMenu } from '@/components/ui/RowActionMenu'
import { AttendeeOverrideModal } from './AttendeeOverrideModal'
import { CargarParticipantesModal } from './CargarParticipantesModal'
import { ConexionApiWebhookModal } from './ConexionApiWebhookModal'
import { DoorListStatus } from './DoorListStatus'

/** Filas que se dibujan de una vez; el resto, a pedido. */
/** Filas por página: con 25.000 participantes la tabla nunca dibuja más de esto. */
const FILAS_POR_PAGINA = 50

type FilterTab = 'todos' | 'calificados' | 'pendientes' | 'excluidos'

interface UnifiedParticipantRow {
  key: string
  participantId: string | null
  fullName: string
  email: string | null
  identityKey: string
  company: string | null
  department: string | null
  employeeCode: string | null
  phone: string | null
  qrCode: string | null
  ticketNumber: string | null
  source: RaffleParticipantItem['source']
  checkedIn: boolean
  checkedInAt: string | null
  qualified: boolean
  exclusionReason: RaffleExclusionReason | null
  manualDecision: AttendeeOverrideDecision | null
  manualNote: string | null
  attendeeObj: RaffleAttendeeResponse
}

function splitFullName(fullName: string): { firstName: string; lastName: string } {
  const parts = fullName.trim().split(/\s+/).filter(Boolean)
  if (parts.length <= 1) return { firstName: parts[0] ?? 'Participante', lastName: '' }
  return {
    firstName: parts[0],
    lastName: parts.slice(1).join(' '),
  }
}

/**
 * Quien ganó en un sorteo vinculado no entra al bombo aunque califique: el servicio lo dice en el
 * padrón y la fila lo muestra como excluido, con o sin la lista congelada.
 */
function conVinculado(
  row: UnifiedParticipantRow,
  p: RaffleParticipantItem | undefined,
): UnifiedParticipantRow {
  if (p?.exclusionReason !== 'won_in_linked_raffle' || row.manualDecision === 'excluded') return row
  return { ...row, qualified: false, exclusionReason: 'won_in_linked_raffle' }
}

function toUnifiedFromParticipant(
  p: RaffleParticipantItem,
  att?: RaffleAttendeeResponse,
): UnifiedParticipantRow {
  return conVinculado(filaDeParticipante(p, att), p)
}

function filaDeParticipante(
  p: RaffleParticipantItem,
  att?: RaffleAttendeeResponse,
): UnifiedParticipantRow {
  const { firstName, lastName } = splitFullName(p.fullName)
  const qualified = att ? att.qualified : p.qualified
  const manualDecision = att ? att.manualDecision : p.override
  const exclusionReason =
    att?.exclusionReason ?? (manualDecision === 'excluded' ? 'manually_excluded' : null)

  return {
    key: `p-${p.id}-${p.identityKey}`,
    participantId: p.id,
    fullName: p.fullName,
    email: p.email,
    identityKey: p.identityKey,
    company: p.company ?? att?.company ?? null,
    department: p.department ?? null,
    employeeCode: p.employeeCode ?? null,
    phone: p.phone ?? null,
    qrCode: p.qrCode,
    ticketNumber: p.ticketNumber ?? null,
    source: p.source,
    checkedIn: p.checkedIn,
    checkedInAt: p.checkedInAt,
    qualified,
    exclusionReason,
    manualDecision,
    manualNote: att?.manualNote ?? null,
    attendeeObj: att ?? {
      email: p.email,
      identityKey: p.identityKey,
      firstName,
      lastName,
      company: p.company,
      qualified,
      exclusionReason,
      manualDecision,
      overruledReason: null,
      manualNote: null,
      manualDecidedById: null,
      manualDecidedAt: null,
    },
  }
}

function toUnifiedFromAttendee(
  att: RaffleAttendeeResponse,
  p?: RaffleParticipantItem,
): UnifiedParticipantRow {
  const attName = `${att.firstName} ${att.lastName}`.trim() || att.email || 'Participante'
  return conVinculado(filaDeAsistente(att, attName, p), p)
}

function filaDeAsistente(
  att: RaffleAttendeeResponse,
  attName: string,
  p?: RaffleParticipantItem,
): UnifiedParticipantRow {
  return {
    key: `a-${att.identityKey}`,
    participantId: p?.id ?? null,
    fullName: p?.fullName ?? attName,
    email: att.email,
    identityKey: att.identityKey,
    company: p?.company ?? att.company,
    department: p?.department ?? null,
    employeeCode: p?.employeeCode ?? null,
    phone: p?.phone ?? null,
    qrCode: p?.qrCode ?? null,
    ticketNumber: p?.ticketNumber ?? null,
    source: p?.source ?? 'convocatoria',
    checkedIn: p ? p.checkedIn : true,
    checkedInAt: p?.checkedInAt ?? null,
    qualified: att.qualified,
    exclusionReason: att.exclusionReason,
    manualDecision: att.manualDecision,
    manualNote: att.manualNote,
    attendeeObj: att,
  }
}

function extractLookupCode(rawInput: string): string {
  const trimmed = rawInput.trim()
  if (!trimmed) return ''
  if (trimmed.startsWith('{')) {
    try {
      const parsed = JSON.parse(trimmed) as Record<string, unknown>
      for (const field of ['qrCode', 'code', 'employeeCode', 'email', 'id']) {
        const val = parsed[field]
        if (typeof val === 'string' && val.trim()) return val.trim()
      }
    } catch {
      // No es JSON válido; continúa con clasificación estándar.
    }
  }
  const classified = classifyCode(trimmed)
  if (classified.kind === 'ticket') return classified.qrCode
  if (classified.kind === 'reference') return classified.reference
  return trimmed
}

export function ParticipantesTable({ raffle }: { raffle: RaffleResponse }) {
  const eventId = raffle.eventId
  const etiquetas = useEtiquetasPadron(eventId)
  const canWrite = useCan(ROLES.rafflesWrite)
  const branding = useBranding(eventId)
  const { data: evento } = useEvento(eventId)
  const eventoNombre = evento?.name ?? 'Sorteo'

  const participantsQuery = useRaffleParticipants(eventId)
  const attendeesQuery = useRaffleAttendees(eventId)

  const setOverride = useSetAttendeeOverride(eventId)
  const removeOverride = useRemoveAttendeeOverride(eventId)
  const setParticipantCheckIn = useSetRaffleParticipantCheckIn(eventId)
  const bulkCheckIn = useBulkRaffleCheckIn(eventId)
  const removeParticipant = useRemoveRaffleParticipant(eventId)

  const [filterTab, setFilterTab] = useState<FilterTab>('todos')
  const [search, setSearch] = useState('')
  const [masAbierto, setMasAbierto] = useState(false)
  const masRef = useRef<HTMLButtonElement>(null)
  // Con 25.000 filas, escribir no espera al filtro y la tabla dibuja por páginas.
  const busqueda = useDeferredValue(search)
  const [pagina, setPagina] = useState(1)
  const [feedback, setFeedback] = useState<string | null>(null)
  const [actionError, setActionError] = useState<unknown>(null)

  const [showImportModal, setShowImportModal] = useState(false)
  const [showIntegrationModal, setShowIntegrationModal] = useState(false)
  const [showAddModal, setShowAddModal] = useState(false)
  const [showScannerModal, setShowScannerModal] = useState(false)
  const [showBadgesModal, setShowBadgesModal] = useState(false)
  const [showPasarModal, setShowPasarModal] = useState(false)
  const [editTarget, setEditTarget] = useState<UnifiedParticipantRow | null>(null)
  const copiar = (texto: string, que: string) =>
    navigator.clipboard
      .writeText(texto)
      .then(() => toast.success(`${que} copiado.`))
      .catch(() => toast.error('No se pudo copiar.'))
  const [qrModalRow, setQrModalRow] = useState<UnifiedParticipantRow | null>(null)
  const [removeTarget, setRemoveTarget] = useState<UnifiedParticipantRow | null>(null)
  const [overrideTarget, setOverrideTarget] = useState<{
    attendee: RaffleAttendeeResponse
    decision: AttendeeOverrideDecision
  } | null>(null)

  const enBorrador = raffle.state === 'draft'
  const editableList = canWrite.allowed && enBorrador
  const actionDisabledReason = !canWrite.allowed
    ? canWrite.title
    : !enBorrador
      ? 'La lista de participantes quedó congelada al iniciar el sorteo.'
      : undefined

  const unifiedRows = useMemo<UnifiedParticipantRow[]>(() => {
    const participants = participantsQuery.data ?? []
    const attendees = attendeesQuery.data?.attendees ?? []
    // La identidad es la identityKey: el correo, o el ID de quien no tiene correo.
    const participantByEmail = new Map<string, RaffleParticipantItem>()
    for (const p of participants) {
      participantByEmail.set(p.identityKey, p)
    }
    const attendeeByEmail = new Map<string, RaffleAttendeeResponse>()
    for (const a of attendees) {
      attendeeByEmail.set(a.identityKey, a)
    }

    const rows: UnifiedParticipantRow[] = []
    const seenEmails = new Set<string>()

    if (!enBorrador && attendees.length > 0) {
      for (const att of attendees) {
        const key = att.identityKey
        seenEmails.add(key)
        rows.push(toUnifiedFromAttendee(att, participantByEmail.get(key)))
      }
      for (const p of participants) {
        const key = p.identityKey
        if (seenEmails.has(key)) continue
        seenEmails.add(key)
        rows.push(toUnifiedFromParticipant(p, attendeeByEmail.get(key)))
      }
      return rows
    }

    for (const p of participants) {
      const key = p.identityKey
      seenEmails.add(key)
      rows.push(toUnifiedFromParticipant(p, attendeeByEmail.get(key)))
    }
    for (const att of attendees) {
      const key = att.identityKey
      if (seenEmails.has(key)) continue
      seenEmails.add(key)
      rows.push(toUnifiedFromAttendee(att, participantByEmail.get(key)))
    }
    return rows
  }, [participantsQuery.data, attendeesQuery.data?.attendees, enBorrador])

  const counts = useMemo(() => {
    let checkedIn = 0
    let qualified = 0
    let pending = 0
    let excluded = 0
    for (const r of unifiedRows) {
      if (r.checkedIn) checkedIn += 1
      if (r.qualified) qualified += 1
      if (!r.checkedIn) pending += 1
      if (
        !r.qualified &&
        (r.checkedIn || r.exclusionReason !== null || r.manualDecision === 'excluded')
      ) {
        excluded += 1
      }
    }
    return {
      total: unifiedRows.length,
      checkedIn,
      qualified,
      pending,
      excluded,
    }
  }, [unifiedRows])

  /** El texto en el que busca cada fila, armado una vez por padrón y no en cada tecla. */
  const textoDeBusqueda = useMemo(
    () =>
      new Map(
        unifiedRows.map((row) => [
          row.key,
          [
            row.fullName,
            row.email ?? '',
            row.company ?? '',
            row.department ?? '',
            row.employeeCode ?? '',
            row.qrCode ?? '',
            row.ticketNumber ?? '',
          ]
            .join(' ')
            .toLowerCase(),
        ]),
      ),
    [unifiedRows],
  )

  const filteredRows = useMemo(() => {
    const q = busqueda.trim().toLowerCase()
    return unifiedRows.filter((row) => {
      if (filterTab === 'calificados' && !row.qualified) return false
      if (filterTab === 'pendientes' && row.checkedIn) return false
      if (
        filterTab === 'excluidos' &&
        !(
          !row.qualified &&
          (row.checkedIn || row.exclusionReason !== null || row.manualDecision === 'excluded')
        )
      ) {
        return false
      }
      if (!q) return true
      return textoDeBusqueda.get(row.key)?.includes(q) ?? false
    })
  }, [unifiedRows, filterTab, busqueda, textoDeBusqueda])

  const paginas = Math.max(1, Math.ceil(filteredRows.length / FILAS_POR_PAGINA))
  // Un filtro o una baja pueden dejar la página fuera de rango: se queda en la última.
  const paginaActual = Math.min(pagina, paginas)
  const filasDePagina = filteredRows.slice(
    (paginaActual - 1) * FILAS_POR_PAGINA,
    paginaActual * FILAS_POR_PAGINA,
  )

  const badgeInvitados = useMemo<Invitado[]>(
    () =>
      unifiedRows.map((row) => ({
        id: row.qrCode ?? row.participantId ?? row.identityKey,
        eventoId: eventId,
        eventoNombre,
        nombre: row.fullName,
        boleto: row.ticketNumber,
        empresa:
          [...new Set([row.company, row.department].filter(Boolean))].join(' · ') ||
          branding.companyName,
        email: row.email ?? '',
        tipo: 'Invitado General',
        status: row.checkedIn ? 'CHECKED_IN' : 'REGISTERED',
        score: 100,
      })),
    [unifiedRows, eventId, eventoNombre, branding.companyName],
  )

  const handleToggleCheckIn = async (row: UnifiedParticipantRow) => {
    if (!editableList) return
    setActionError(null)
    setFeedback(null)
    try {
      const targetCode = row.participantId ?? row.qrCode ?? row.email ?? row.employeeCode ?? ''
      const nextState = !row.checkedIn
      await setParticipantCheckIn.mutateAsync({
        registrationIdOrCode: targetCode,
        checkedIn: nextState,
      })
      setFeedback(
        nextState
          ? `Check-in registrado para ${row.fullName}.`
          : `Se retiró el check-in de ${row.fullName}.`,
      )
    } catch (err) {
      setActionError(err)
    }
  }

  const allCheckedIn = unifiedRows.length > 0 && counts.checkedIn === unifiedRows.length
  const handleBulkCheckIn = async () => {
    if (!editableList || unifiedRows.length === 0) return
    setActionError(null)
    setFeedback(null)
    try {
      const target = !allCheckedIn
      await bulkCheckIn.mutateAsync(target)
      setFeedback(
        target
          ? `Check-in masivo aplicado (${unifiedRows.length} participantes habilitados).`
          : `Check-in retirado a los participantes (${unifiedRows.length} actualizados).`,
      )
    } catch (err) {
      setActionError(err)
    }
  }

  const loading = participantsQuery.isLoading && attendeesQuery.isLoading

  return (
    <div className="space-y-4">
      <DoorListStatus
        state={raffle.state}
        attendees={attendeesQuery.data}
        warnings={raffle.warnings ?? []}
      />

      {/* Una sola barra: filtros a la izquierda; búsqueda y acciones a la derecha. Las acciones
          de uso ocasional van en "Más", para que la vista no se llene de botones. */}
      <div className="flex flex-col gap-3 xl:flex-row xl:items-center xl:justify-between">
        <div
          role="tablist"
          aria-label="Filtrar participantes"
          className="flex flex-wrap items-center gap-1 rounded-xl bg-slate-100 p-1 dark:bg-slate-900/70"
        >
          {(
            [
              ['todos', 'Todos', counts.total],
              ['calificados', 'Califican', counts.qualified],
              ['pendientes', 'Sin check-in', counts.pending],
              ['excluidos', 'Excluidos', counts.excluded],
            ] as const
          )
            // Sin puerta todos están presentes: el filtro de check-in no dice nada.
            .filter(([key]) => !(sinPuerta(raffle) && key === 'pendientes'))
            .map(([key, label, n]) => {
              const active = filterTab === key
              return (
                <button
                  key={key}
                  type="button"
                  role="tab"
                  aria-selected={active}
                  onClick={() => {
                    setFilterTab(key)
                    setPagina(1)
                  }}
                  className={`inline-flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-xs font-semibold transition-colors cursor-pointer ${
                    active
                      ? 'bg-white text-slate-900 shadow-xs dark:bg-card dark:text-white'
                      : 'text-slate-600 hover:text-slate-900 dark:text-slate-400 dark:hover:text-slate-200'
                  }`}
                >
                  {label}
                  <span
                    className={`rounded-full px-1.5 py-px text-[10px] font-bold tabular-nums ${
                      active
                        ? 'bg-brand text-(--brand-primary-contrast)'
                        : 'bg-slate-200 text-slate-600 dark:bg-slate-800 dark:text-slate-300'
                    }`}
                  >
                    {n.toLocaleString('es-DO')}
                  </span>
                </button>
              )
            })}
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <div className="relative w-full sm:w-64">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-slate-400" />
            <input
              type="search"
              value={search}
              aria-label="Buscar participantes"
              onChange={(e) => {
                setSearch(e.target.value)
                setPagina(1)
              }}
              placeholder="Nombre, correo, área, código…"
              className="w-full rounded-lg border border-slate-200 bg-white py-2 pl-8 pr-3 text-xs text-slate-900 placeholder:text-slate-400 focus:outline-hidden focus:ring-2 focus:ring-brand/30 dark:border-slate-700 dark:bg-card dark:text-slate-100"
            />
          </div>
          {raffle.state !== 'completed' && (
            <>
              <Button
                type="button"
                size="sm"
                onClick={() => setShowImportModal(true)}
                disabled={!editableList}
                title={actionDisabledReason}
                className="text-xs font-semibold"
              >
                <FileSpreadsheet className="mr-1.5 h-3.5 w-3.5" />
                Cargar padrón
              </Button>
              <Button
                type="button"
                size="sm"
                variant="outline"
                onClick={() => setShowAddModal(true)}
                disabled={!editableList}
                title={actionDisabledReason}
                className="text-xs font-semibold"
              >
                <UserPlus className="mr-1.5 h-3.5 w-3.5" />
                Agregar
              </Button>
            </>
          )}
          <Button
            ref={masRef}
            type="button"
            size="sm"
            variant="outline"
            aria-haspopup="menu"
            aria-expanded={masAbierto}
            onClick={() => setMasAbierto((v) => !v)}
            className="text-xs font-semibold"
          >
            Más
            <ChevronDown className="ml-1 h-3.5 w-3.5" />
          </Button>
          <ActionMenu
            open={masAbierto}
            onClose={() => setMasAbierto(false)}
            anchorRef={masRef}
            className="w-60"
          >
            {raffle.state !== 'completed' && (
              <>
                {!sinPuerta(raffle) && (
                  <>
                    <MenuItem
                      disabled={!editableList}
                      onClick={() => {
                        setMasAbierto(false)
                        setShowScannerModal(true)
                      }}
                    >
                      <ScanLine className="h-3.5 w-3.5" /> Escáner de check-in QR
                    </MenuItem>
                    <MenuItem
                      disabled={!editableList || unifiedRows.length === 0 || bulkCheckIn.isPending}
                      onClick={() => {
                        setMasAbierto(false)
                        void handleBulkCheckIn()
                      }}
                    >
                      <UserCheck className="h-3.5 w-3.5" />
                      {allCheckedIn ? 'Quitar check-in a todos' : 'Check-in a todos'}
                    </MenuItem>
                  </>
                )}
                <MenuItem
                  disabled={!editableList}
                  onClick={() => {
                    setMasAbierto(false)
                    setShowIntegrationModal(true)
                  }}
                >
                  <Webhook className="h-3.5 w-3.5" /> Conexión API / Webhook
                </MenuItem>
                <MenuSeparator />
              </>
            )}
            <MenuItem
              disabled={unifiedRows.length === 0}
              onClick={() => {
                setMasAbierto(false)
                setShowBadgesModal(true)
              }}
            >
              <IdCard className="h-3.5 w-3.5" /> Gafetes con QR (PDF)
            </MenuItem>
            {canWrite.allowed && (
              <MenuItem
                disabled={(participantsQuery.data ?? []).length === 0}
                onClick={() => {
                  setMasAbierto(false)
                  setShowPasarModal(true)
                }}
              >
                <Shuffle className="h-3.5 w-3.5" /> Pasar no ganadores a otro sorteo
              </MenuItem>
            )}
          </ActionMenu>
        </div>
      </div>

      {feedback && (
        <div className="flex items-center justify-between gap-2 rounded-lg border border-emerald-200 bg-emerald-50 dark:border-emerald-900/60 dark:bg-emerald-950/30 px-3.5 py-2 text-xs font-semibold text-emerald-800 dark:text-emerald-300">
          <span className="flex items-center gap-2">
            <CheckCircle2 className="w-4 h-4 shrink-0" />
            {feedback}
          </span>
          <button
            type="button"
            onClick={() => setFeedback(null)}
            className="text-[11px] underline cursor-pointer"
          >
            Ocultar
          </button>
        </div>
      )}

      {actionError != null && <ErrorNote error={actionError} />}
      {(participantsQuery.error || attendeesQuery.error) != null && (
        <ErrorNote error={participantsQuery.error ?? attendeesQuery.error} />
      )}

      {/* Tabla de participantes */}
      <div className="rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-card overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full min-w-[640px] text-left border-collapse text-xs">
            <thead>
              <tr className="border-b border-slate-200 bg-slate-50 font-bold uppercase tracking-wider text-slate-600 dark:border-slate-800 dark:bg-slate-900/60 dark:text-slate-300">
                <th className="px-4 py-3">Participante</th>
                <th className="px-4 py-3">{etiquetas.department}</th>
                <th className="px-4 py-3">{sinPuerta(raffle) ? 'Boleto' : 'Check-in · Boleto'}</th>
                <th className="px-4 py-3">Elegibilidad</th>
                <th className="px-4 py-3 text-right">Acciones</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-slate-800/70">
              {loading ? (
                <tr>
                  <td colSpan={5} className="px-4 py-10 text-center text-slate-500">
                    Cargando participantes del sorteo...
                  </td>
                </tr>
              ) : filteredRows.length === 0 ? (
                <tr>
                  <td colSpan={5} className="px-4 py-10">
                    <EmptyState>
                      <div className="space-y-1 text-center">
                        <Users className="w-6 h-6 text-slate-400 mx-auto" />
                        <p className="font-semibold text-slate-700 dark:text-slate-200">
                          {unifiedRows.length === 0
                            ? 'Aún no hay participantes cargados en este sorteo.'
                            : 'Ningún participante coincide con el filtro actual.'}
                        </p>
                        {unifiedRows.length === 0 && editableList && (
                          <p className="text-xs text-slate-500">
                            Importe una lista desde Excel, CSV o TXT, conéctese por API/Webhook o
                            agregue empleados manualmente.
                          </p>
                        )}
                      </div>
                    </EmptyState>
                  </td>
                </tr>
              ) : (
                filasDePagina.map((row) => {
                  const unoverridable =
                    row.exclusionReason !== null &&
                    UNOVERRIDABLE_EXCLUSIONS.includes(row.exclusionReason)

                  return (
                    <tr
                      key={row.key}
                      className="hover:bg-slate-50/70 dark:hover:bg-slate-800/40 transition-colors"
                    >
                      <td className="px-4 py-3 align-top">
                        <div className="font-semibold text-slate-900 dark:text-slate-100">
                          {row.fullName}
                        </div>
                        {/* Debajo del nombre, solo el ID del empleado: el número tal cual. */}
                        {row.employeeCode && (
                          <div
                            className="font-mono text-[11px] text-slate-500 tabular-nums dark:text-slate-400"
                            title={etiquetas.employeeCode}
                          >
                            {row.employeeCode}
                          </div>
                        )}
                      </td>

                      <td className="px-4 py-3 align-top text-slate-700 dark:text-slate-300">
                        {/* Primero el área (la columna se llama como en el archivo); debajo, la
                            empresa, que en un padrón de empleados es la misma para todos. */}
                        <div className="font-medium">{row.department || '—'}</div>
                        {/* La empresa, solo si dice algo distinto del área. */}
                        {(row.company || branding.companyName) !== row.department && (
                          <div className="text-[11px] text-slate-500">
                            {row.company || branding.companyName}
                          </div>
                        )}
                      </td>

                      <td className="px-4 py-3 align-top">
                        <div className="flex flex-wrap items-center gap-2">
                          {sinPuerta(raffle) ? null : row.participantId || row.qrCode ? (
                            <button
                              type="button"
                              disabled={!editableList || setParticipantCheckIn.isPending}
                              onClick={() => handleToggleCheckIn(row)}
                              title={
                                actionDisabledReason ??
                                (row.checkedIn
                                  ? `Llegó ${row.checkedInAt ? formatDateTime(row.checkedInAt) : ''} · clic para retirar el check-in`
                                  : 'Clic para marcar el check-in')
                              }
                              className={`inline-flex items-center gap-1 whitespace-nowrap px-2 py-0.5 rounded-full text-[11px] font-bold border transition-colors ${
                                row.checkedIn
                                  ? 'bg-emerald-50 border-emerald-200 text-emerald-700 dark:bg-emerald-950/50 dark:border-emerald-800 dark:text-emerald-300'
                                  : 'bg-amber-50 border-amber-200 text-amber-800 dark:bg-amber-950/40 dark:border-amber-800 dark:text-amber-300'
                              } ${
                                editableList
                                  ? 'cursor-pointer hover:opacity-80'
                                  : 'cursor-default opacity-80'
                              }`}
                            >
                              {row.checkedIn ? (
                                <>
                                  <CheckCircle2 className="w-3 h-3" />
                                  Check-in
                                </>
                              ) : (
                                <>
                                  <Clock className="w-3 h-3" />
                                  Pendiente
                                </>
                              )}
                            </button>
                          ) : (
                            <Badge variant={row.checkedIn ? 'emerald' : 'amber'}>
                              {row.checkedIn ? 'Check-in' : 'Pendiente'}
                            </Badge>
                          )}

                          {row.ticketNumber && (
                            <span
                              className="font-mono text-[11px] font-semibold text-slate-600 tabular-nums dark:text-slate-300"
                              title="Número de boleto"
                            >
                              #{row.ticketNumber}
                            </span>
                          )}
                        </div>
                      </td>

                      <td className="px-4 py-3 align-top space-y-1">
                        {row.qualified ? (
                          <Badge variant="emerald">Califica</Badge>
                        ) : !row.checkedIn &&
                          !row.exclusionReason &&
                          row.manualDecision !== 'excluded' ? (
                          <Badge variant="slate">Sin check-in</Badge>
                        ) : (
                          <Badge variant="rose">Excluido</Badge>
                        )}

                        {row.manualDecision && (
                          <div
                            className="text-[11px] text-blue-600 dark:text-blue-400 font-semibold"
                            title={row.manualNote ?? undefined}
                          >
                            {attendeeOverrideDecisionLabel[row.manualDecision]}
                            {row.manualNote ? `: ${row.manualNote}` : ''}
                          </div>
                        )}

                        {!row.qualified && row.exclusionReason && (
                          <div className="text-[11px] text-slate-500">
                            {raffleExclusionReasonLabel[row.exclusionReason]}
                          </div>
                        )}
                      </td>

                      <td className="px-4 py-3 align-top text-right whitespace-nowrap">
                        <RowActionMenu label={`Opciones de ${row.fullName}`} menuClassName="w-60">
                          <MenuItem onClick={() => setQrModalRow(row)}>
                            <QrCode className="h-4 w-4" /> Ver QR
                          </MenuItem>
                          {row.employeeCode && (
                            <MenuItem
                              onClick={() => copiar(row.employeeCode!, etiquetas.employeeCode)}
                            >
                              <Copy className="h-4 w-4" /> Copiar {etiquetas.employeeCode}
                            </MenuItem>
                          )}
                          {row.ticketNumber && (
                            <MenuItem onClick={() => copiar(row.ticketNumber!, 'Número de boleto')}>
                              <Ticket className="h-4 w-4" /> Copiar número de boleto
                            </MenuItem>
                          )}
                          {editableList && row.participantId && row.employeeCode && (
                            <MenuItem onClick={() => setEditTarget(row)}>
                              <Pencil className="h-4 w-4" /> Editar datos
                            </MenuItem>
                          )}
                          {!sinPuerta(raffle) &&
                            editableList &&
                            (row.participantId || row.qrCode) && (
                              <MenuItem
                                disabled={setParticipantCheckIn.isPending}
                                onClick={() => handleToggleCheckIn(row)}
                              >
                                {row.checkedIn ? (
                                  <>
                                    <Clock className="h-4 w-4" /> Quitar check-in
                                  </>
                                ) : (
                                  <>
                                    <CheckCircle2 className="h-4 w-4" /> Marcar check-in
                                  </>
                                )}
                              </MenuItem>
                            )}
                          {editableList && !unoverridable && <MenuSeparator />}
                          {editableList && !unoverridable && (
                            <>
                              {row.manualDecision ? (
                                <MenuItem
                                  disabled={removeOverride.isPending}
                                  onClick={() => removeOverride.mutate(row.identityKey)}
                                >
                                  <RotateCcw className="h-4 w-4" /> Quitar ajuste manual
                                </MenuItem>
                              ) : row.qualified ? (
                                <MenuItem
                                  onClick={() =>
                                    setOverrideTarget({
                                      attendee: row.attendeeObj,
                                      decision: 'excluded',
                                    })
                                  }
                                >
                                  <ShieldBan className="h-4 w-4" /> Excluir del sorteo
                                </MenuItem>
                              ) : (
                                <MenuItem
                                  onClick={() =>
                                    setOverrideTarget({
                                      attendee: row.attendeeObj,
                                      decision: 'included',
                                    })
                                  }
                                >
                                  <ShieldCheck className="h-4 w-4" /> Incluir en el sorteo
                                </MenuItem>
                              )}
                            </>
                          )}
                          {editableList && row.participantId && (
                            <>
                              <MenuSeparator />
                              <MenuItem tone="danger" onClick={() => setRemoveTarget(row)}>
                                <Trash2 className="h-4 w-4" /> Quitar de la lista
                              </MenuItem>
                            </>
                          )}
                        </RowActionMenu>
                      </td>
                    </tr>
                  )
                })
              )}
            </tbody>
          </table>
        </div>
        {filteredRows.length > 0 && (
          <Pagination
            page={paginaActual}
            pageCount={paginas}
            onPageChange={setPagina}
            total={filteredRows.length}
            className="border-t border-slate-100 px-4 py-3 dark:border-slate-800"
          />
        )}
      </div>

      {/* Modal: Importación Excel / CSV / TXT */}
      {showImportModal && (
        <CargarParticipantesModal
          eventId={eventId}
          onClose={() => setShowImportModal(false)}
          onImported={(summary) => {
            setFeedback(
              `Importación completada: ${summary.imported} nuevos, ${summary.updated} actualizados (${summary.total} en total).`,
            )
            setShowImportModal(false)
          }}
        />
      )}

      {/* Modal: Conexión API & Webhook */}
      {showIntegrationModal && (
        <ConexionApiWebhookModal
          eventId={eventId}
          onClose={() => setShowIntegrationModal(false)}
          onSynced={(msg) => {
            setFeedback(msg)
            setShowIntegrationModal(false)
          }}
        />
      )}

      {/* Modal: Agregar participante manual */}
      {editTarget && (
        <AgregarParticipanteModal
          eventId={eventId}
          defaultCompany={branding.companyName}
          inicial={editTarget}
          onClose={() => setEditTarget(null)}
          onAdded={(name) => {
            setFeedback(`Se guardaron los datos de "${name}".`)
            setEditTarget(null)
          }}
        />
      )}

      {showAddModal && (
        <AgregarParticipanteModal
          eventId={eventId}
          defaultCompany={branding.companyName}
          onClose={() => setShowAddModal(false)}
          onAdded={(name) => {
            setFeedback(`Participante "${name}" agregado al padrón del sorteo.`)
            setShowAddModal(false)
          }}
        />
      )}

      {/* Modal: Escáner Check-in QR */}
      {showScannerModal && (
        <EscanerCheckinModal
          eventId={eventId}
          onClose={() => setShowScannerModal(false)}
          onCheckedIn={(participant) => {
            setFeedback(`Check-in QR confirmado: ${participant.fullName}.`)
          }}
        />
      )}

      {/* Modal: QR Individual */}
      {qrModalRow && (
        <QrIndividualModal
          row={qrModalRow}
          companyName={qrModalRow.company || branding.companyName}
          eventoNombre={eventoNombre}
          canToggleCheckIn={editableList && Boolean(qrModalRow.participantId || qrModalRow.qrCode)}
          pending={setParticipantCheckIn.isPending}
          onToggleCheckIn={async () => {
            await handleToggleCheckIn(qrModalRow)
            setQrModalRow((prev) => (prev ? { ...prev, checkedIn: !prev.checkedIn } : null))
          }}
          onClose={() => setQrModalRow(null)}
        />
      )}

      {showPasarModal && (
        <PasarNoGanadoresModal
          raffle={raffle}
          participantes={participantsQuery.data ?? []}
          onClose={() => setShowPasarModal(false)}
        />
      )}

      {/* Modal: Generador de Gafetes QR en PDF */}
      <BadgesGeneratorModal
        eventoNombre={eventoNombre}
        invitados={badgeInvitados}
        isOpen={showBadgesModal}
        onClose={() => setShowBadgesModal(false)}
      />

      {/* Modal: Ajuste manual (Incluir / Excluir) */}
      {overrideTarget && (
        <AttendeeOverrideModal
          attendee={overrideTarget.attendee}
          decision={overrideTarget.decision}
          pending={setOverride.isPending}
          error={setOverride.error}
          onConfirm={(note) => {
            setOverride.mutate(
              {
                identityKey: overrideTarget.attendee.identityKey,
                decision: overrideTarget.decision,
                note,
              },
              {
                onSuccess: () => {
                  setOverrideTarget(null)
                },
              },
            )
          }}
          onCancel={() => setOverrideTarget(null)}
        />
      )}

      {/* Confirmación para eliminar participante */}
      {removeTarget && (
        <ConfirmDialog
          title="Quitar participante"
          confirmLabel="Quitar"
          destructive
          pending={removeParticipant.isPending}
          error={removeParticipant.error}
          onConfirm={() => {
            removeParticipant.mutate(removeTarget.participantId ?? removeTarget.email ?? '', {
              onSuccess: () => {
                setFeedback(`Se quitó a ${removeTarget.fullName} del padrón del sorteo.`)
                setRemoveTarget(null)
              },
            })
          }}
          onCancel={() => setRemoveTarget(null)}
        >
          <p>
            ¿Seguro que desea quitar a <strong>{removeTarget.fullName}</strong>
            {removeTarget.email && ` (${removeTarget.email})`} de la lista de este sorteo?
          </p>
        </ConfirmDialog>
      )}
    </div>
  )
}

/**
 * Agrega un participante a mano o, con `inicial`, edita uno: la carga actualiza por el ID del
 * empleado, así que ese campo queda fijo al editar.
 */
function AgregarParticipanteModal({
  eventId,
  defaultCompany,
  inicial,
  onClose,
  onAdded,
}: {
  eventId: string
  defaultCompany: string
  inicial?: UnifiedParticipantRow
  onClose: () => void
  onAdded: (name: string) => void
}) {
  const importMutation = useImportRaffleParticipants(eventId)
  const etiquetas = useEtiquetasPadron(eventId)
  const [fullName, setFullName] = useState(inicial?.fullName ?? '')
  const [email, setEmail] = useState(inicial?.email ?? '')
  const [company, setCompany] = useState(inicial?.company ?? defaultCompany)
  const [department, setDepartment] = useState(inicial?.department ?? '')
  const [employeeCode, setEmployeeCode] = useState(inicial?.employeeCode ?? '')
  const [phone, setPhone] = useState(inicial?.phone ?? '')
  const [checkedIn, setCheckedIn] = useState(inicial?.checkedIn ?? true)

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    if (!fullName.trim()) return
    const participant: RaffleParticipantInput = {
      fullName: fullName.trim(),
      email: email.trim() || null,
      company: company.trim() || null,
      department: department.trim() || null,
      employeeCode: employeeCode.trim() || null,
      phone: phone.trim() || null,
      checkedIn,
    }
    await importMutation.mutateAsync({
      participants: [participant],
      replaceExisting: false,
      markCheckedIn: checkedIn,
      // Editar conserva el origen; una importación no puede declararse "convocatoria".
      source: inicial && inicial.source !== 'convocatoria' ? inicial.source : 'manual',
    })
    onAdded(participant.fullName)
  }

  return (
    <Modal
      title={inicial ? 'Editar participante' : 'Agregar Participante Manualmente'}
      onClose={onClose}
    >
      <form onSubmit={handleSubmit} className="space-y-4">
        <Field label="Nombre completo" required>
          <TextInput
            value={fullName}
            onChange={(e) => setFullName(e.target.value)}
            placeholder="Ej: Laura Méndez"
            required
          />
        </Field>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <Field label="Correo electrónico" hint="Opcional">
            <TextInput
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="laura.mendez@empresa.com"
            />
          </Field>
          <Field label={etiquetas.employeeCode}>
            <TextInput
              value={employeeCode}
              onChange={(e) => setEmployeeCode(e.target.value)}
              placeholder="EMP-0124"
              readOnly={inicial != null}
              title={
                inicial ? 'El ID identifica al participante: no se cambia al editar.' : undefined
              }
            />
          </Field>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <Field label="Empresa">
            <TextInput
              value={company}
              onChange={(e) => setCompany(e.target.value)}
              placeholder="Empresa cliente"
            />
          </Field>
          <Field label="Departamento / Área">
            <TextInput
              value={department}
              onChange={(e) => setDepartment(e.target.value)}
              placeholder="Recursos Humanos"
            />
          </Field>
        </div>

        <Field label="Teléfono / WhatsApp">
          <TextInput
            value={phone}
            onChange={(e) => setPhone(e.target.value)}
            placeholder="+1 809 555 0100"
          />
        </Field>

        <label className="flex items-center gap-2 text-xs font-medium text-slate-700 dark:text-slate-300 cursor-pointer">
          <input
            type="checkbox"
            checked={checkedIn}
            onChange={(e) => setCheckedIn(e.target.checked)}
            className="rounded border-slate-300 text-blue-600 focus:ring-blue-500"
          />
          Registrar con Check-in activo (entra directo a la tómbola)
        </label>

        {importMutation.error != null && <ErrorNote error={importMutation.error} />}

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="outline" onClick={onClose} className="cursor-pointer">
            Cancelar
          </Button>
          <Button
            type="submit"
            disabled={!fullName.trim() || importMutation.isPending}
            className="bg-blue-600 hover:bg-blue-700 text-white cursor-pointer"
          >
            {importMutation.isPending ? 'Guardando...' : 'Agregar Participante'}
          </Button>
        </div>
      </form>
    </Modal>
  )
}

function EscanerCheckinModal({
  eventId,
  onClose,
  onCheckedIn,
}: {
  eventId: string
  onClose: () => void
  onCheckedIn: (item: RaffleParticipantItem) => void
}) {
  const checkInMutation = useSetRaffleParticipantCheckIn(eventId)
  const [cameraActive, setCameraActive] = useState(true)
  const [manualInput, setManualInput] = useState('')
  const [recentScans, setRecentScans] = useState<
    Array<{
      code: string
      estado: 'ok' | 'error' | 'pendiente'
      name?: string
      message: string
      at: string
    }>
  >([])
  const [pendientes, setPendientes] = useState(() => leerCola(eventId).length)
  const [enviando, setEnviando] = useState(false)
  const { mutateAsync } = checkInMutation

  const anotar = (scan: Omit<(typeof recentScans)[number], 'at'>) =>
    setRecentScans((prev) => [
      {
        ...scan,
        at: new Date().toLocaleTimeString('es-DO', {
          hour: '2-digit',
          minute: '2-digit',
          second: '2-digit',
        }),
      },
      ...prev.slice(0, 5),
    ])

  // Al abrir y cada vez que vuelve la red, se mandan las lecturas guardadas sin conexión.
  useEffect(() => {
    let activo = true
    const vaciar = async () => {
      if (leerCola(eventId).length === 0) return
      setEnviando(true)
      const r = await vaciarCola(eventId, (codigo) =>
        mutateAsync({ registrationIdOrCode: codigo, checkedIn: true }),
      )
      if (!activo) return
      setEnviando(false)
      setPendientes(r.pendientes)
      if (r.enviados) toast.success(`Se enviaron ${r.enviados} check-in guardados sin conexión.`)
      if (r.rechazados.length)
        toast.error(`Sin participante para: ${r.rechazados.join(', ')}. Revise esos códigos.`)
    }
    void vaciar()
    window.addEventListener('online', vaciar)
    return () => {
      activo = false
      window.removeEventListener('online', vaciar)
    }
  }, [eventId, mutateAsync])

  const processCode = async (rawCode: string) => {
    const code = extractLookupCode(rawCode)
    if (!code) return
    try {
      const updated = await checkInMutation.mutateAsync({
        registrationIdOrCode: code,
        checkedIn: true,
      })
      anotar({
        code,
        estado: 'ok',
        name: updated.fullName,
        message: `Check-in confirmado (${updated.company || 'Participante'})`,
      })
      onCheckedIn(updated)
    } catch (error) {
      if (esSinConexion(error)) {
        setPendientes(encolar(eventId, code).length)
        anotar({
          code,
          estado: 'pendiente',
          message: 'Sin conexión: guardado aquí, se enviará solo al volver la red.',
        })
        return
      }
      anotar({
        code,
        estado: 'error',
        message: 'No se encontró un participante con ese código QR, código de empleado o correo.',
      })
    }
  }

  const handleManualSubmit = async (e: FormEvent) => {
    e.preventDefault()
    if (!manualInput.trim()) return
    const value = manualInput
    setManualInput('')
    await processCode(value)
  }

  return (
    <Modal title="Escáner de Check-in QR" onClose={onClose} wide>
      <div className="space-y-4">
        <div className="flex items-center justify-between gap-2">
          <p className="text-xs text-slate-600 dark:text-slate-300">
            Escanee el código QR del gafete con la cámara o utilice un lector USB / teclado manual.
          </p>
          <Button
            type="button"
            size="sm"
            variant="outline"
            onClick={() => setCameraActive((v) => !v)}
            className="text-xs cursor-pointer shrink-0"
          >
            {cameraActive ? 'Pausar cámara' : 'Activar cámara'}
          </Button>
        </div>

        {pendientes > 0 && (
          <div
            role="status"
            className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-xs font-bold text-amber-800 dark:border-amber-900/60 dark:bg-amber-950/30 dark:text-amber-300"
          >
            {enviando
              ? `Enviando ${pendientes} check-in guardados sin conexión…`
              : `${pendientes} check-in guardados sin conexión; se enviarán al volver la red.`}
          </div>
        )}

        <div className="rounded-xl overflow-hidden border border-slate-200 dark:border-slate-800 bg-black">
          <ScannerCamera onRead={processCode} paused={!cameraActive || checkInMutation.isPending} />
        </div>

        <form onSubmit={handleManualSubmit} className="flex flex-col sm:flex-row gap-2">
          <TextInput
            value={manualInput}
            onChange={(e) => setManualInput(e.target.value)}
            placeholder="Código QR (QR-...), código de empleado (EMP-...) o correo..."
            autoFocus
            className="flex-1"
          />
          <Button
            type="submit"
            disabled={!manualInput.trim() || checkInMutation.isPending}
            className="bg-blue-600 hover:bg-blue-700 text-white cursor-pointer shrink-0"
          >
            {checkInMutation.isPending ? 'Validando...' : 'Validar Check-in'}
          </Button>
        </form>

        {recentScans.length > 0 && (
          <div className="space-y-1.5">
            <div className="text-[11px] font-bold uppercase tracking-wider text-slate-500">
              Lecturas recientes
            </div>
            <div className="space-y-1.5 max-h-44 overflow-y-auto">
              {recentScans.map((scan, idx) => (
                <div
                  key={`${scan.code}-${idx}`}
                  className={`flex items-center justify-between gap-2 rounded-lg border px-3 py-2 text-xs ${
                    scan.estado === 'ok'
                      ? 'border-emerald-200 bg-emerald-50 text-emerald-800 dark:border-emerald-900/60 dark:bg-emerald-950/30 dark:text-emerald-300'
                      : scan.estado === 'pendiente'
                        ? 'border-amber-200 bg-amber-50 text-amber-800 dark:border-amber-900/60 dark:bg-amber-950/30 dark:text-amber-300'
                        : 'border-rose-200 bg-rose-50 text-rose-800 dark:border-rose-900/60 dark:bg-rose-950/30 dark:text-rose-300'
                  }`}
                >
                  <div>
                    <div className="font-bold">{scan.name ?? scan.code}</div>
                    <div className="text-[11px] opacity-90">{scan.message}</div>
                  </div>
                  <span className="text-[10px] font-mono shrink-0">{scan.at}</span>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>
    </Modal>
  )
}

function QrIndividualModal({
  row,
  companyName,
  eventoNombre,
  canToggleCheckIn,
  pending,
  onToggleCheckIn,
  onClose,
}: {
  row: UnifiedParticipantRow
  companyName: string
  eventoNombre: string
  canToggleCheckIn: boolean
  pending: boolean
  onToggleCheckIn: () => Promise<void>
  onClose: () => void
}) {
  const qrText = row.qrCode ?? row.employeeCode ?? row.email ?? row.identityKey
  const [qrDataUrl, setQrDataUrl] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    QRCode.toDataURL(qrText, { width: 280, margin: 2 })
      .then((url) => {
        if (!cancelled) setQrDataUrl(url)
      })
      .catch(() => {
        if (!cancelled) setQrDataUrl(null)
      })
    return () => {
      cancelled = true
    }
  }, [qrText])

  return (
    <Modal title="Pase & Código QR del Participante" onClose={onClose}>
      <div className="space-y-4 text-center">
        <div>
          <div className="text-[11px] font-bold uppercase tracking-wider text-blue-600 dark:text-blue-400">
            {eventoNombre}
          </div>
          <h4 className="text-lg font-black text-slate-900 dark:text-slate-100 mt-0.5">
            {row.fullName}
          </h4>
          <p className="text-xs text-slate-500">
            {companyName}
            {row.department && row.department !== companyName ? ` · ${row.department}` : ''}
          </p>
          <p className="text-xs text-slate-400">{correoOSinCorreo(row.email)}</p>
        </div>

        <div className="inline-flex flex-col items-center justify-center rounded-2xl border border-slate-200 dark:border-slate-700 bg-white p-4 shadow-xs">
          {qrDataUrl ? (
            <img
              src={qrDataUrl}
              alt={`Código QR de ${row.fullName}`}
              className="w-56 h-56 object-contain"
            />
          ) : (
            <div className="w-56 h-56 flex items-center justify-center text-xs text-slate-400">
              Generando QR...
            </div>
          )}
          <span className="mt-2 font-mono text-xs font-bold text-slate-700">{qrText}</span>
        </div>

        <div className="flex flex-wrap items-center justify-center gap-2 pt-2">
          {qrDataUrl && (
            <a
              href={qrDataUrl}
              download={`QR_${row.fullName.replace(/[^a-zA-Z0-9_-]/g, '_')}.png`}
              className="inline-flex items-center gap-1.5 px-3 py-2 rounded-lg border border-slate-200 dark:border-slate-700 text-xs font-bold text-slate-700 dark:text-slate-200 hover:bg-slate-50 dark:hover:bg-slate-800"
            >
              <Download className="w-3.5 h-3.5" />
              Descargar QR (PNG)
            </a>
          )}

          {canToggleCheckIn && (
            <Button
              type="button"
              size="sm"
              disabled={pending}
              onClick={onToggleCheckIn}
              className={
                row.checkedIn
                  ? 'bg-amber-600 hover:bg-amber-700 text-white cursor-pointer'
                  : 'bg-emerald-600 hover:bg-emerald-700 text-white cursor-pointer'
              }
            >
              {row.checkedIn ? 'Quitar Check-in' : 'Registrar Check-in'}
            </Button>
          )}
        </div>
      </div>
    </Modal>
  )
}
