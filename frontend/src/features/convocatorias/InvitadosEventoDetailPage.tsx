import { toast } from 'sonner'
import { useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import {
  CalendarDays,
  ExternalLink,
  Languages,
  Mail,
  MapPin,
  Plus,
  QrCode,
  Search,
  UserCheck,
  Users,
  UserX,
} from 'lucide-react'
import { Breadcrumbs } from '@/components/ui/Breadcrumbs'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { EmptyState, ErrorNote, Spinner } from '@/components/ui/Feedback'
import { DataTable } from '@/components/ui/DataTable'
import { StatCard } from '@/components/ui/StatCard'
import { ExportButtons } from '@/components/ui/ExportButtons'
import { useEvento } from '@/features/eventos/hooks'
import { formatDateRange } from '@/lib/format'
import { eventoStatusLabel, idiomaLabel } from '@/lib/labels'
import type { ReportColumn } from '@/lib/exportUtils'
import { EstadoParticipanteBadge } from './components/EstadoParticipanteBadge'
import { BadgesGeneratorModal } from './components/BadgesGeneratorModal'
import {
  ESTADOS_INSCRITOS,
  ESTADO_INVITACION_LABEL,
  PARTICIPANT_STATUS_LABEL,
  PARTICIPANT_TYPES,
  type Invitado,
  type ParticipantStatus,
  type ParticipantType,
} from './mockParticipants'
import {
  descargarCsvParticipantes,
  useInvitadosDeEventos,
  useParticipantesDeEventos,
} from './participants'
import { useEventTracking } from './tracking'
import {
  asignaAsientos,
  claveDe,
  distribucionDe,
  etiquetasDe,
  puedeSentarse,
  textoAsiento,
  useMesasDeEvento,
  useSentables,
} from './mesas'
import { AsignarMesaModal } from './components/asientos/Modales'
import { RowActionMenu } from '@/components/ui/RowActionMenu'
import { MenuItem } from '@/components/ui/MenuItem'
import { Select, TextInput } from '@/components/ui/Field'
import { MenuSeparator } from '@/components/ui/MenuItem'
import { AgregarInvitadoModal, QuitadosDeLaLista } from './components/AgregarInvitadoModal'
import {
  enviarInvitacion,
  invitacionEnviada,
  quitarDeListaBlanca,
  useAjustesListaBlanca,
} from './listaBlanca'
import { rasgosLocalesPorCorreo } from './mockParticipants'

const DISTRIBUCION_CUPOS: Record<ParticipantType, number> = {
  Buyer: 0.3,
  Supplier: 0.3,
  Prensa: 0.1,
  Influencer: 0.05,
  VIP: 0.05,
  Autoridad: 0.03,
  Staff: 0.05,
  Expositor: 0.05,
  Patrocinador: 0.05,
  'Invitado General': 0.02,
}

const CUENTA_PARA_CUPO: ParticipantStatus[] = ['RSVP_YES', 'REGISTERED', 'CONFIRMED', 'CHECKED_IN']

/** `embebido`: dentro de la página del evento en Convocatorias, sin migas ni cabecera propia. */
export function InvitadosEventoDetailPage({ embebido = false }: { embebido?: boolean } = {}) {
  const { id = '' } = useParams()
  const eventoQuery = useEvento(id)
  const trackingQuery = useEventTracking(id)

  const [searchTerm, setSearchTerm] = useState('')
  const [filtroTipo, setFiltroTipo] = useState<ParticipantType | ''>('')
  const [filtroStatus, setFiltroStatus] = useState<ParticipantStatus | ''>('')
  const [showBadgesModal, setShowBadgesModal] = useState(false)
  const [filtroMesa, setFiltroMesa] = useState<'' | 'sin' | 'con'>('')
  const [asignarPara, setAsignarPara] = useState<Invitado | null>(null)
  const { config: configMesas, asientos } = useMesasDeEvento(id)
  // Solo hay asientos en un montaje con mesas o filas, y si alguien los configuró.
  const montaje = eventoQuery.data?.seatingLayout
  const et = etiquetasDe(montaje)
  const dist = distribucionDe(montaje)
  const asignaMesas = asignaAsientos(configMesas) && Boolean(dist) && dist !== 'de_pie'

  const participantesQuery = useParticipantesDeEventos([id])

  const evento = eventoQuery.data
  const eventosList = useMemo(() => (evento ? [evento] : []), [evento])

  const { invitados } = useInvitadosDeEventos(eventosList)
  // Mantiene las sillas al día: quien deja de estar registrado suelta la suya.
  useSentables(id, invitados, !participantesQuery.isLoading && participantesQuery.error == null)

  // Un evento privado solo deja registrarse a su lista: los de las campañas, menos los que se
  // quitaron, más los agregados a mano. Por eso en privado la tabla es esa lista.
  const privado = evento?.visibility === 'private'
  const ajustes = useAjustesListaBlanca(id)
  const [agregando, setAgregando] = useState(false)
  const lista = useMemo<(Invitado & { manual?: boolean })[]>(() => {
    if (!privado) return invitados
    const quitados = new Set(ajustes.quitados.map((q) => q.toLowerCase()))
    const vistos = new Set(invitados.map((i) => i.email.toLowerCase()))
    const manuales = ajustes.agregados
      .filter((a) => !vistos.has(a.email.toLowerCase()))
      .map((a) => ({
        id: `manual-${a.email}`,
        eventoId: id,
        eventoNombre: evento?.name ?? '',
        nombre: a.nombre,
        empresa: a.empresa ?? '',
        email: a.email,
        status: 'INVITED' as ParticipantStatus,
        fechaInvitado: invitacionEnviada(ajustes, a.email)?.slice(0, 10) ?? null,
        manual: true,
        ...rasgosLocalesPorCorreo(a.email),
      }))
    return [...invitados.filter((i) => !quitados.has(i.email.toLowerCase())), ...manuales]
  }, [privado, invitados, ajustes, id, evento?.name])

  const filtrados = useMemo(
    () =>
      lista.filter((i) => {
        const matchesSearch =
          !searchTerm ||
          i.nombre.toLowerCase().includes(searchTerm.toLowerCase()) ||
          i.empresa.toLowerCase().includes(searchTerm.toLowerCase()) ||
          i.email.toLowerCase().includes(searchTerm.toLowerCase())
        const matchesTipo = !filtroTipo || i.tipo === filtroTipo
        const matchesStatus =
          !filtroStatus ||
          i.status === filtroStatus ||
          (filtroStatus === 'NO_ASISTIRE' &&
            (i.status === 'RSVP_NO' || i.status === 'NO_SHOW' || i.status === 'CANCELLED')) ||
          (filtroStatus === 'REGISTERED' &&
            (i.status === 'RSVP_YES' || i.status === 'CONFIRMED' || i.status === 'WAITLIST')) ||
          (filtroStatus === 'CHECKED_IN' &&
            (i.status === 'RAFFLE_ELIGIBLE' || i.status === 'RAFFLE_WINNER'))
        const tieneMesa = Boolean(asientos[claveDe(i.email)])
        const matchesMesa =
          !asignaMesas ||
          !filtroMesa ||
          (filtroMesa === 'con' ? tieneMesa : puedeSentarse(i) && !tieneMesa)
        return matchesSearch && matchesTipo && matchesStatus && matchesMesa
      }),
    [lista, searchTerm, filtroTipo, filtroStatus, filtroMesa, asientos, asignaMesas],
  )

  const capacidadPorSegmento = useMemo(() => {
    if (!evento?.invitedCount) return null
    return PARTICIPANT_TYPES.map((tipo) => {
      const delTipo = invitados.filter((i) => i.tipo === tipo)
      const cuposAsignados = Math.round(evento.invitedCount! * DISTRIBUCION_CUPOS[tipo])
      const registrados = delTipo.filter((i) => CUENTA_PARA_CUPO.includes(i.status)).length
      const enEspera = delTipo.filter((i) => i.status === 'WAITLIST')
      return {
        tipo,
        cuposAsignados,
        registrados,
        disponibles: Math.max(0, cuposAsignados - registrados),
        enListaEspera: enEspera.length,
        primerEnEsperaId: enEspera[0]?.id,
      }
    }).filter((row) => row.cuposAsignados > 0)
  }, [evento, invitados])

  const columns: ReportColumn<Invitado>[] = [
    { header: 'Nombre', accessor: (i) => i.nombre },
    { header: 'Empresa', accessor: (i) => i.empresa },
    { header: 'Email', accessor: (i) => i.email },
    { header: 'Tipo de participante', accessor: (i) => i.tipo },
    { header: 'Fecha de invitado', accessor: (i) => i.fechaInvitado || '—' },
    { header: 'Estado', accessor: (i) => PARTICIPANT_STATUS_LABEL[i.status] },
    { header: 'Fecha de registrado', accessor: (i) => i.fechaRegistro || '—' },
  ]

  const exportarCsv = () => {
    const registered = !filtroStatus
      ? undefined
      : ESTADOS_INSCRITOS.includes(filtroStatus)
        ? true
        : filtroStatus === 'INVITED'
          ? false
          : undefined
    descargarCsvParticipantes(id, {
      search: searchTerm.trim() || undefined,
      registered,
    }).catch((e: unknown) =>
      toast.error(
        e instanceof Error ? e.message : 'No se pudo exportar la lista de participantes.',
      ),
    )
  }

  if (eventoQuery.isLoading || participantesQuery.isLoading) {
    return <Spinner label="Cargando invitados del evento…" />
  }

  if (eventoQuery.error != null || !evento) {
    return <ErrorNote error={eventoQuery.error ?? new Error('Evento no encontrado')} />
  }

  const sede = evento.cities[0]
  const resumen = trackingQuery.data
  const cifra = (n: number | null | undefined) => (n == null ? '—' : n.toLocaleString('es-DO'))

  return (
    <div className={embebido ? 'space-y-6' : 'mx-auto max-w-7xl space-y-6 pb-12'}>
      {!embebido && (
        <Breadcrumbs
          items={[
            { label: 'Invitaciones', to: '/convocatorias' },
            { label: 'Invitados', to: '/convocatorias/invitados' },
            { label: evento.name },
          ]}
        />
      )}

      {/* Header del Evento */}
      {!embebido && (
        <Card className="rounded-3xl border border-slate-200/80 bg-white p-6 shadow-2xs dark:border-slate-800 dark:bg-slate-900">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div className="min-w-0 space-y-2">
              <div className="flex flex-wrap items-center gap-2">
                <span className="rounded-md bg-slate-100 px-2.5 py-0.5 text-[11px] font-extrabold text-slate-700 dark:bg-slate-800 dark:text-slate-300">
                  {eventoStatusLabel[evento.status]}
                </span>
                {resumen && (
                  <span
                    className={`rounded-md px-2.5 py-0.5 text-[11px] font-extrabold ${
                      resumen.registrationWindow.open
                        ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300'
                        : 'bg-rose-100 text-rose-800 dark:bg-rose-950 dark:text-rose-300'
                    }`}
                  >
                    {resumen.registrationWindow.open ? 'Registro abierto' : 'Registro cerrado'}
                  </span>
                )}
              </div>
              <h1 className="font-sans text-2xl font-extrabold tracking-tight text-slate-900 sm:text-3xl dark:text-white">
                {evento.name}
              </h1>
              <div className="flex flex-wrap gap-x-5 gap-y-1.5 text-xs text-slate-600 dark:text-slate-300">
                <span className="flex items-center gap-1.5">
                  <CalendarDays size={14} className="shrink-0 text-slate-400" />
                  {formatDateRange(evento.startDate, evento.endDate)}
                </span>
                {sede && (
                  <span className="flex items-center gap-1.5">
                    <MapPin size={14} className="shrink-0 text-slate-400" />
                    {sede.name}, {sede.country}
                  </span>
                )}
                {evento.language && (
                  <span className="flex items-center gap-1.5">
                    <Languages size={14} className="shrink-0 text-slate-400" />
                    {idiomaLabel[evento.language]}
                  </span>
                )}
              </div>
            </div>

            <div className="flex flex-wrap items-center gap-2.5">
              <Link to={`/eventos/${evento.id}`}>
                <Button
                  type="button"
                  variant="outline"
                  className="cursor-pointer text-xs font-bold"
                >
                  <ExternalLink size={14} className="mr-1.5" /> Ficha del evento
                </Button>
              </Link>
              <Link to={`/convocatorias/checkin?eventoId=${evento.id}`}>
                <Button
                  type="button"
                  variant="outline"
                  className="cursor-pointer text-xs font-bold"
                >
                  <QrCode size={14} className="mr-1.5" /> Ir a Check-in
                </Button>
              </Link>
              <Button
                type="button"
                onClick={() => setShowBadgesModal(true)}
                disabled={filtrados.length === 0}
                className="bg-blue-600 hover:bg-blue-700 text-white font-bold text-xs py-2 px-4 rounded-xl flex items-center gap-2 cursor-pointer shadow-md shadow-blue-600/20"
              >
                <QrCode className="w-4 h-4" />
                <span>Generar Gafetes QR ({filtrados.length})</span>
              </Button>
            </div>
          </div>
        </Card>
      )}

      {/* Tarjetas de Métricas de Invitados */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard
          icon={Users}
          label="Capacidad"
          value={cifra(evento.invitedCount)}
          iconClassName="border-blue-200 bg-blue-50 text-blue-700 dark:border-blue-900 dark:bg-blue-950/50 dark:text-blue-300"
        />
        <StatCard
          icon={Mail}
          label="Invitaciones Enviadas"
          value={cifra(invitados.length)}
          iconClassName="border-violet-200 bg-violet-50 text-violet-700 dark:border-violet-900 dark:bg-violet-950/50 dark:text-violet-300"
        />
        <StatCard
          icon={UserCheck}
          label="Registrados"
          value={cifra(invitados.filter((i) => CUENTA_PARA_CUPO.includes(i.status)).length)}
          iconClassName="border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900 dark:bg-emerald-950/50 dark:text-emerald-300"
        />
        <StatCard
          icon={UserX}
          label="Sin Registrar"
          value={cifra(
            invitados.filter(
              (i) =>
                i.status === 'INVITED' || i.status === 'NO_ASISTIRE' || i.status === 'CANCELLED',
            ).length,
          )}
          iconClassName="border-rose-200 bg-rose-50 text-rose-700 dark:border-rose-900 dark:bg-rose-950/50 dark:text-rose-300"
        />
      </div>

      {/* Filtros de la Lista */}
      <Card className="p-4 bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 rounded-2xl">
        <div
          className={`grid grid-cols-1 gap-3.5 ${asignaMesas ? 'sm:grid-cols-4' : 'sm:grid-cols-3'}`}
        >
          <div className="relative">
            <Search
              size={16}
              className="absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400"
            />
            <TextInput
              type="text"
              placeholder="Buscar por nombre, empresa o correo..."
              aria-label="Buscar por nombre, empresa o correo"
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="pl-9 pr-3"
            />
          </div>
          <Select
            value={filtroTipo}
            onChange={(e) => setFiltroTipo(e.target.value as ParticipantType | '')}
          >
            <option value="">Todos los tipos de participante</option>
            {PARTICIPANT_TYPES.map((t) => (
              <option key={t} value={t}>
                {t}
              </option>
            ))}
          </Select>
          <Select
            value={filtroStatus}
            onChange={(e) => setFiltroStatus(e.target.value as ParticipantStatus | '')}
          >
            <option value="">Todos los estados</option>
            {Object.entries(ESTADO_INVITACION_LABEL).map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </Select>
          {asignaMesas && (
            <Select
              value={filtroMesa}
              onChange={(e) => setFiltroMesa(e.target.value as '' | 'sin' | 'con')}
              aria-label="Filtrar por mesa"
            >
              <option value="">Todos los asientos</option>
              <option value="sin">Registrados sin asiento</option>
              <option value="con">Con asiento asignado</option>
            </Select>
          )}
        </div>
      </Card>

      {/* Capacidad por segmento */}
      {capacidadPorSegmento && capacidadPorSegmento.length > 0 && (
        <Card className="bg-white p-6 dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 rounded-2xl shadow-2xs">
          <h2 className="mb-1 font-sans text-base font-bold text-slate-900 dark:text-white">
            Capacidad y asignación por segmento
          </h2>
          <p className="mb-4 text-xs text-slate-500 dark:text-slate-400">
            Capacidad total del evento: {evento.invitedCount?.toLocaleString('es-DO')} personas
          </p>
          <DataTable
            dense
            rows={capacidadPorSegmento}
            getRowKey={(row) => row.tipo}
            columns={[
              {
                key: 'segmento',
                header: 'Segmento',
                cellClassName: 'font-bold text-slate-800 dark:text-slate-200',
                cell: (row) => row.tipo,
              },
              {
                key: 'cupos',
                header: 'Cupos asignados',
                align: 'center',
                cell: (row) => row.cuposAsignados,
              },
              {
                key: 'registrados',
                header: 'Registrados',
                align: 'center',
                cellClassName: 'font-bold text-emerald-600 dark:text-emerald-400',
                cell: (row) => row.registrados,
              },
              {
                key: 'disponibles',
                header: 'Disponibles',
                align: 'center',
                cell: (row) => row.disponibles,
              },
              {
                key: 'espera',
                header: 'En lista de espera',
                align: 'center',
                cellClassName: 'font-bold text-amber-600',
                cell: (row) => row.enListaEspera || '—',
              },
            ]}
          />
        </Card>
      )}

      {/* Lista de Invitados */}
      <Card className="bg-white p-6 dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 rounded-2xl shadow-2xs">
        <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
          <div>
            <h2 className="font-sans text-base font-bold text-slate-900 dark:text-white">
              Lista de invitados al evento
            </h2>
            <span className="text-xs font-bold text-slate-400">{filtrados.length} invitados</span>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            {privado && (
              <Button type="button" onClick={() => setAgregando(true)}>
                <Plus size={14} /> Agregar invitado
              </Button>
            )}
            <ExportButtons
              filename={`invitados-${evento.id}`}
              title={`Reporte de invitados - ${evento.name}`}
              sheetName="Invitados"
              columns={columns}
              rows={filtrados}
              onExcel={exportarCsv}
            />
          </div>
        </div>

        {filtrados.length === 0 ? (
          <EmptyState>No hay invitados que coincidan con los filtros para este evento.</EmptyState>
        ) : (
          <DataTable
            rows={filtrados}
            getRowKey={(i) => i.id}
            stickyHeader
            columns={[
              {
                key: 'nombre',
                header: 'Nombre',
                cell: (i) => (
                  <div>
                    <p className="font-bold text-brand dark:text-blue-400">{i.nombre}</p>
                    <p className="text-[11px] text-slate-500">
                      {i.empresa}
                      {i.manual && (
                        <span className="ml-1 rounded-full bg-amber-50 px-2 py-0.5 text-[10px] font-bold text-amber-800 dark:bg-amber-950/50 dark:text-amber-300">
                          Agregado a mano
                        </span>
                      )}
                    </p>
                  </div>
                ),
              },
              {
                key: 'email',
                header: 'Correo',
                cell: (i) => (
                  <span className="font-mono text-[11px] text-slate-700 dark:text-slate-300">
                    {i.email}
                  </span>
                ),
              },
              {
                key: 'tipo',
                header: 'Tipo',
                cell: (i) => (
                  <span className="font-semibold text-slate-700 dark:text-slate-300">{i.tipo}</span>
                ),
              },
              {
                key: 'fechaInvitado',
                header: 'Fecha invitado',
                align: 'center',
                cell: (i) => (
                  <span className="text-slate-600 dark:text-slate-300 font-medium">
                    {i.fechaInvitado || '—'}
                  </span>
                ),
              },
              {
                key: 'estado',
                header: 'Estado',
                align: 'center',
                cell: (i) => <EstadoParticipanteBadge status={i.status} />,
              },
              {
                key: 'fechaRegistro',
                header: 'Fecha registrado',
                align: 'right',
                cell: (i) =>
                  i.fechaRegistro ? (
                    <span className="text-emerald-700 dark:text-emerald-400 font-bold">
                      {i.fechaRegistro}
                    </span>
                  ) : (
                    <span className="text-slate-400">—</span>
                  ),
              },
              ...(asignaMesas
                ? [
                    {
                      key: 'mesa',
                      header: `${et.unidad} / ${et.asiento}`,
                      align: 'center' as const,
                      cell: (i: Invitado) => {
                        const asiento = asientos[claveDe(i.email)]
                        if (asiento) return textoAsiento(asiento, configMesas, et)
                        return puedeSentarse(i) ? (
                          <span className="text-xs font-semibold text-amber-700">Sin asiento</span>
                        ) : (
                          <span className="text-slate-400">—</span>
                        )
                      },
                    },
                  ]
                : []),
              ...(asignaMesas || privado
                ? [
                    {
                      key: 'acciones',
                      header: 'Acciones',
                      align: 'right' as const,
                      cell: (i: Invitado & { manual?: boolean }) => {
                        const sienta = asignaMesas && puedeSentarse(i)
                        if (!sienta && !privado) return null
                        return (
                          <RowActionMenu label="Opciones del invitado">
                            {sienta && (
                              <MenuItem onClick={() => setAsignarPara(i)}>
                                {asientos[claveDe(i.email)] ? 'Cambiar asiento' : 'Asignar asiento'}
                              </MenuItem>
                            )}
                            {sienta && privado && <MenuSeparator />}
                            {privado && (
                              <MenuItem onClick={() => enviarInvitacion(id, i.email)}>
                                {invitacionEnviada(ajustes, i.email)
                                  ? 'Reenviar invitación'
                                  : 'Enviar invitación'}
                              </MenuItem>
                            )}
                            {privado && (
                              <MenuItem
                                tone="danger"
                                onClick={() =>
                                  quitarDeListaBlanca(id, {
                                    email: i.email,
                                    nombre: i.nombre,
                                    fuente: i.manual ? 'manual' : 'convocatoria',
                                  })
                                }
                              >
                                Quitar de los invitados
                              </MenuItem>
                            )}
                          </RowActionMenu>
                        )
                      },
                    },
                  ]
                : []),
            ]}
          />
        )}
        {privado && (
          <QuitadosDeLaLista
            eventoId={id}
            quitados={ajustes.quitados}
            nombrePorCorreo={new Map(invitados.map((i) => [i.email.toLowerCase(), i.nombre]))}
          />
        )}
      </Card>

      {asignarPara && (
        <AsignarMesaModal
          eventoId={id}
          montaje={montaje}
          invitado={asignarPara}
          registrados={invitados}
          onClose={() => setAsignarPara(null)}
        />
      )}

      {agregando && (
        <AgregarInvitadoModal
          eventoId={id}
          yaInvitados={lista.map((i) => i.email)}
          onClose={() => setAgregando(false)}
        />
      )}

      {showBadgesModal && (
        <BadgesGeneratorModal
          isOpen={showBadgesModal}
          onClose={() => setShowBadgesModal(false)}
          eventoNombre={evento.name}
          invitados={filtrados}
        />
      )}
    </div>
  )
}
