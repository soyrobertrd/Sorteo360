import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import {
  Armchair,
  ChevronDown,
  Eraser,
  LayoutGrid,
  List,
  Plus,
  RotateCcw,
  Search,
  Settings2,
  Wine,
} from 'lucide-react'
import { Card } from '@/components/ui/Card'
import { Tabs } from '@/components/ui/Tabs'
import { Button } from '@/components/ui/Button'
import { ActionMenu } from '@/components/ui/ActionMenu'
import { MenuItem, MenuSeparator } from '@/components/ui/MenuItem'
import { Modal } from '@/components/ui/Modal'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { WarningNote } from '@/components/ui/Feedback'
import { montajeLabel } from '@/lib/labels'
import type { MontajeEvento } from '@/api/types'
import { type Invitado } from '../../mockParticipants'
import {
  CRITERIO_MESAS_LABEL,
  MODO_MESAS_LABEL,
  agregarMesa,
  capacidadDe,
  asignaAsientos,
  reservaEnLibre,
  asientosFueraDeCapacidad,
  asignarAsiento,
  claveDe,
  companerosSinMesa,
  configInicial,
  distribucionDe,
  etiquetasDe,
  guardarConfigMesas,
  maxSillasPorMesa,
  MESA_PRINCIPAL,
  reasignarTodo,
  liberarAsientos,
  textoAsiento,
  useMesasDeEvento,
  useSentables,
  type Asiento,
  type ConfigMesas,
  hayVip,
} from '../../mesas'
import { AsientoResaltado, minus } from './texto'
import { Configuracion } from './Configuracion'
import {
  Leyenda,
  EsquemaFilas,
  EsquemaEscuela,
  EsquemaHerradura,
  EsquemaMesas,
  ListaPorUnidad,
} from './Esquemas'
import {
  LiberarAsientosModal,
  ElegirPersonaModal,
  AsignarMesaModal,
  SugerirCompanerosModal,
} from './Modales'
import { SectionHeader } from '@/components/ui/SectionHeader'
import { TextInput } from '@/components/ui/Field'

/**
 * La sección "Mesas y sillas" de un evento. Lo que muestra depende del montaje que se eligió
 * al crear el evento: mesas redondas, filas frente al escenario o, en un cóctel, nada que
 * asignar. Incluye la configuración, un esquema del salón y la lista por mesa o fila.
 */
export function MesasSillasCard({
  eventoId,
  montaje,
  invitados,
  listo,
}: {
  eventoId: string
  montaje: MontajeEvento | null | undefined
  invitados: Invitado[]
  /** La lista ya cargó: antes de eso no se libera ningún asiento. */
  listo: boolean
}) {
  const distribucion = distribucionDe(montaje)
  const et = etiquetasDe(montaje)
  const { config, configurado, asientos } = useMesasDeEvento(eventoId)
  const { registrados, sentables } = useSentables(eventoId, invitados, listo)
  const [borrador, setBorrador] = useState<ConfigMesas>(() =>
    configurado ? config : configInicial(montaje),
  )
  // Al cambiar el montaje (de mesas de 10 a escuela) la configuración guardada se ajusta al
  // tope del nuevo tipo de mesa, en vez de arrastrar las sillas de la distribución anterior.
  const maxSillas = maxSillasPorMesa(distribucion)
  useEffect(() => {
    if (configurado && config.sillasPorMesa > maxSillas) {
      const ajustada = { ...config, sillasPorMesa: maxSillas }
      guardarConfigMesas(eventoId, ajustada)
      setBorrador(ajustada)
    }
  }, [configurado, config, maxSillas, eventoId])
  const [editando, setEditando] = useState(false)
  const [menuAcciones, setMenuAcciones] = useState(false)
  const accionesRef = useRef<HTMLButtonElement>(null)
  const [vista, setVista] = useState<'esquema' | 'lista'>('esquema')
  const [porLiberar, setPorLiberar] = useState<string[] | null>(null)
  const [confirmarReasignar, setConfirmarReasignar] = useState(false)
  const [liberando, setLiberando] = useState(false)
  const [sillaElegida, setSillaElegida] = useState<Asiento | null>(null)
  const [moverA, setMoverA] = useState<Invitado | null>(null)
  const [sugerencia, setSugerencia] = useState<{ persona: Invitado; mesa: number } | null>(null)
  const [buscar, setBuscar] = useState('')
  const [resaltado, setResaltado] = useState<string | null>(null)

  const porClave = useMemo(
    () => new Map(registrados.map((i) => [claveDe(i.email), i])),
    [registrados],
  )
  const ocupante = (mesa: number, silla: number) => {
    const clave = Object.entries(asientos).find(
      ([, a]) => a.mesa === mesa && a.silla === silla,
    )?.[0]
    const registrado = clave ? (porClave.get(clave) ?? null) : null
    const manual = mesa === MESA_PRINCIPAL ? config.invitadosPrincipal?.[silla] : undefined
    // shortcut: el invitado escrito a mano se dibuja como un Invitado con solo el nombre;
    // si el servicio llega a guardar invitados sin registro, que tenga su propio tipo.
    return registrado ?? (manual ? ({ nombre: manual, empresa: '', email: '' } as Invitado) : null)
  }
  const nombreManualEn = (silla: number) => config.invitadosPrincipal?.[silla] ?? ''
  const guardarNombreManual = (silla: number, nombre: string | null) => {
    const invitadosPrincipal = { ...config.invitadosPrincipal }
    if (nombre) invitadosPrincipal[silla] = nombre
    else delete invitadosPrincipal[silla]
    guardarConfigMesas(eventoId, { ...config, invitadosPrincipal })
  }

  // Sin montaje no se sabe qué asignar; en un cóctel no hay asientos.
  if (!distribucion || distribucion === 'de_pie') {
    return (
      <div className="space-y-4">
        <Encabezado montaje={montaje} subtitulo={null} />
        {distribucion === 'de_pie' ? (
          <div className="flex items-center gap-3 rounded-2xl bg-slate-50 p-4 text-xs text-slate-600 dark:bg-slate-950/40 dark:text-slate-300">
            <Wine size={18} className="shrink-0 text-slate-400" />
            Montaje tipo cóctel: la gente está de pie y circula, así que no hay asientos que
            asignar.
          </div>
        ) : (
          <div>
            <WarningNote>
              <div className="flex flex-wrap items-center justify-between gap-2">
                <span>
                  Este evento no tiene montaje definido. Elígelo en la ficha del evento para saber
                  si habrá mesas, filas o nadie se sienta.
                </span>
                <Link
                  to={`/eventos/${eventoId}`}
                  className="font-bold text-brand underline dark:text-blue-300"
                >
                  Ir al evento
                </Link>
              </div>
            </WarningNote>
          </div>
        )}
      </div>
    )
  }

  const asigna = asignaAsientos(config) && configurado
  const soloReserva = reservaEnLibre(config)
  const sinMesa = registrados.filter((i) => !asientos[claveDe(i.email)])
  const capacidad = capacidadDe(config)
  const ocupadas = Object.keys(asientos).length

  // Buscar a alguien en el salón: hasta 5 coincidencias por nombre, correo o empresa.
  const termino = buscar.trim().toLowerCase()
  const coincidencias =
    termino.length < 2
      ? []
      : registrados
          .filter(
            (r) =>
              r.nombre.toLowerCase().includes(termino) ||
              r.email.toLowerCase().includes(termino) ||
              r.empresa.toLowerCase().includes(termino),
          )
          .slice(0, 5)

  const abrirConfiguracion = () => {
    setBorrador(configurado ? config : configInicial(montaje))
    setEditando(true)
  }

  const guardar = () => {
    // Con asiento libre, el salón asignable es solo la zona reservada al frente.
    const final =
      borrador.modo === 'libre' ? { ...borrador, mesas: borrador.reservadas ?? 0 } : borrador
    const fuera = asientosFueraDeCapacidad(final, asientos)
    if (fuera.length > 0 && porLiberar === null) {
      setPorLiberar(fuera)
      return
    }
    guardarConfigMesas(eventoId, final)
    setPorLiberar(null)
    setEditando(false)
  }

  const sentar = (persona: Invitado, asiento: Asiento) => {
    asignarAsiento(eventoId, persona, asiento)
    if (companerosSinMesa(persona, registrados, asientos).length > 0) {
      setSugerencia({ persona, mesa: asiento.mesa })
    }
  }

  const onSilla = (mesa: number, silla: number) => {
    const persona = ocupante(mesa, silla)
    // Un invitado escrito a mano no tiene registro que mover: se edita su nombre.
    if (persona && !persona.email) setSillaElegida({ mesa, silla })
    else if (persona) setMoverA(persona)
    else setSillaElegida({ mesa, silla })
  }

  return (
    <div className="space-y-4">
      <Encabezado
        montaje={montaje}
        subtitulo={
          !configurado
            ? null
            : soloReserva
              ? `Asiento libre · ${config.reservadas} ${minus(et.unidades)} reservadas al frente como área VIP`
              : asigna
                ? `${MODO_MESAS_LABEL[config.modo]} · ${config.mesas} ${minus(et.unidades)} de ${config.sillasPorMesa} ${minus(et.asientos)}${
                    config.modo === 'automatica'
                      ? ` · ${CRITERIO_MESAS_LABEL[config.criterio].toLowerCase()}`
                      : ''
                  }`
                : MODO_MESAS_LABEL.libre
        }
        actions={
          configurado ? (
            <div className="relative">
              <Button ref={accionesRef} onClick={() => setMenuAcciones((v) => !v)}>
                Acciones
                <ChevronDown
                  size={15}
                  className={`ml-1.5 transition-transform ${menuAcciones ? 'rotate-180' : ''}`}
                />
              </Button>
              <ActionMenu
                open={menuAcciones}
                onClose={() => setMenuAcciones(false)}
                anchorRef={accionesRef}
                className="w-52"
              >
                <MenuItem
                  onClick={() => {
                    setMenuAcciones(false)
                    abrirConfiguracion()
                  }}
                >
                  <Settings2 size={14} className="text-slate-500" /> Configurar
                </MenuItem>
                {asigna && ocupadas > 0 && (
                  <>
                    <MenuItem
                      onClick={() => {
                        setMenuAcciones(false)
                        setConfirmarReasignar(true)
                      }}
                    >
                      <RotateCcw size={14} className="text-slate-500" /> Volver a asignar
                    </MenuItem>
                    <MenuSeparator />
                    <MenuItem
                      tone="danger"
                      onClick={() => {
                        setMenuAcciones(false)
                        setLiberando(true)
                      }}
                    >
                      <Eraser size={14} /> Liberar asientos
                    </MenuItem>
                  </>
                )}
              </ActionMenu>
            </div>
          ) : undefined
        }
      />
      <Card className="space-y-5 rounded-3xl border border-slate-200/80 bg-white p-6 shadow-2xs dark:border-slate-800 dark:bg-slate-900">
        {editando && (
          <Modal
            title={configurado ? 'Configurar asientos' : 'Configurar asientos del evento'}
            onClose={() => setEditando(false)}
            width="sm:max-w-2xl"
          >
            <Configuracion
              montaje={montaje}
              borrador={borrador}
              setBorrador={setBorrador}
              et={et}
              onCancelar={() => setEditando(false)}
              onGuardar={guardar}
            />
          </Modal>
        )}

        {!configurado && (
          <div className="flex flex-col items-center gap-3 rounded-2xl border border-dashed border-slate-300 bg-slate-50/60 px-6 py-8 text-center dark:border-slate-700 dark:bg-slate-950/30">
            <Armchair size={28} className="text-slate-400" />
            <div className="max-w-md space-y-1">
              <p className="text-sm font-bold text-slate-800 dark:text-slate-100">
                Este evento todavía no tiene asientos configurados
              </p>
              <p className="text-xs text-slate-500">
                Elige si la gente se sienta donde quiera, si recibe su asiento al registrarse o si
                lo asignas a mano. También puedes reservar al frente un área VIP.
              </p>
            </div>
            <Button size="sm" onClick={abrirConfiguracion}>
              <Settings2 size={13} className="mr-1" /> Configurar asientos
            </Button>
          </div>
        )}

        {asigna && (
          <>
            {soloReserva && (
              <p className="rounded-xl bg-slate-50 px-3 py-2 text-xs text-slate-600 dark:bg-slate-950/40 dark:text-slate-300">
                Solo el área VIP tiene asientos: las autoridades y los VIP reciben el suyo al
                registrarse o al hacer check-in. El resto del salón se sienta donde quiera.
              </p>
            )}

            {config.modo === 'automatica' &&
              sinMesa.length > 0 &&
              sinMesa.length <= capacidad - ocupadas && (
                <p className="rounded-xl bg-blue-50 px-3 py-2 text-xs text-blue-900 dark:bg-blue-950/40 dark:text-blue-200">
                  {sinMesa.length}{' '}
                  {sinMesa.length === 1
                    ? 'registrado recibirá su asiento'
                    : 'registrados recibirán su asiento'}{' '}
                  al hacer check-in, entre los que queden libres.
                </p>
              )}

            {!soloReserva && sinMesa.length > capacidad - ocupadas && (
              <WarningNote>
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <span>
                    Hay {sinMesa.length} registrados sin asiento y solo{' '}
                    {Math.max(0, capacidad - ocupadas)} libres: faltan lugares en el salón.
                  </span>
                  <Button size="sm" variant="outline" onClick={() => agregarMesa(eventoId)}>
                    <Plus size={13} className="mr-1" /> Agregar {minus(et.unidad)}
                  </Button>
                </div>
              </WarningNote>
            )}

            <div className="space-y-2">
              <div className="relative max-w-md">
                <Search
                  size={14}
                  className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400"
                />
                <TextInput
                  type="search"
                  aria-label="Buscar a una persona en el salón"
                  value={buscar}
                  onChange={(e) => {
                    setBuscar(e.target.value)
                    setResaltado(null)
                  }}
                  placeholder="Buscar persona: nombre, correo o empresa…"
                  className="pl-8 pr-3"
                />
              </div>
              {termino.length >= 2 && (
                <div className="flex flex-wrap gap-2 text-[11px]">
                  {coincidencias.length === 0 && (
                    <span className="text-slate-500">Nadie registrado coincide.</span>
                  )}
                  {coincidencias.map((r) => {
                    const asiento = asientos[claveDe(r.email)]
                    const activo = resaltado === claveDe(r.email)
                    return (
                      <button
                        key={r.email}
                        type="button"
                        onClick={() => (asiento ? setResaltado(claveDe(r.email)) : setMoverA(r))}
                        className={`rounded-full border px-3 py-1 font-semibold transition-colors ${
                          activo
                            ? 'border-emerald-500 bg-emerald-50 text-emerald-800 dark:bg-emerald-950/50 dark:text-emerald-300'
                            : 'border-slate-200 bg-white text-slate-700 hover:border-brand dark:border-slate-700 dark:bg-slate-900 dark:text-slate-200'
                        }`}
                      >
                        {r.nombre} ·{' '}
                        {asiento ? (
                          <strong>{textoAsiento(asiento, config, et)}</strong>
                        ) : (
                          <span className="text-amber-700 dark:text-amber-300">
                            sin asiento, asignar
                          </span>
                        )}
                      </button>
                    )
                  })}
                </div>
              )}
            </div>

            <div className="flex flex-wrap items-center justify-between gap-3">
              <Leyenda reservadas={hayVip(config)} />
              <Tabs
                variant="segmented"
                size="sm"
                items={[
                  {
                    key: 'esquema' as const,
                    label: (
                      <>
                        <LayoutGrid size={12} /> Esquema
                      </>
                    ),
                  },
                  {
                    key: 'lista' as const,
                    label: (
                      <>
                        <List size={12} /> Lista
                      </>
                    ),
                  },
                ]}
                value={vista}
                onChange={setVista}
              />
            </div>

            <AsientoResaltado.Provider value={resaltado}>
              {vista === 'esquema' ? (
                distribucion === 'filas' ? (
                  <EsquemaFilas config={config} et={et} ocupante={ocupante} onSilla={onSilla} />
                ) : distribucion === 'herradura' ? (
                  <EsquemaHerradura config={config} et={et} ocupante={ocupante} onSilla={onSilla} />
                ) : distribucion === 'escuela' ? (
                  <EsquemaEscuela config={config} et={et} ocupante={ocupante} onSilla={onSilla} />
                ) : (
                  <EsquemaMesas config={config} et={et} ocupante={ocupante} onSilla={onSilla} />
                )
              ) : (
                <ListaPorUnidad
                  config={config}
                  et={et}
                  asientos={asientos}
                  ocupante={ocupante}
                  onLiberar={(persona) => asignarAsiento(eventoId, persona, null)}
                  onLibre={(mesa, silla) => setSillaElegida({ mesa, silla })}
                />
              )}
            </AsientoResaltado.Provider>
          </>
        )}

        {sillaElegida && (
          <ElegirPersonaModal
            titulo={textoAsiento(sillaElegida, config, et)}
            registrados={registrados}
            asientoDe={(i) => asientos[claveDe(i.email)]}
            config={config}
            et={et}
            onClose={() => setSillaElegida(null)}
            onElegir={(persona) => {
              if (nombreManualEn(sillaElegida.silla) && sillaElegida.mesa === MESA_PRINCIPAL) {
                guardarNombreManual(sillaElegida.silla, null)
              }
              sentar(persona, sillaElegida)
              setSillaElegida(null)
            }}
            nombreManual={
              sillaElegida.mesa === MESA_PRINCIPAL
                ? {
                    valor: nombreManualEn(sillaElegida.silla),
                    onGuardar: (nombre) => {
                      guardarNombreManual(sillaElegida.silla, nombre)
                      setSillaElegida(null)
                    },
                  }
                : undefined
            }
          />
        )}

        {moverA && (
          <AsignarMesaModal
            eventoId={eventoId}
            montaje={montaje}
            invitado={moverA}
            registrados={registrados}
            onClose={() => setMoverA(null)}
          />
        )}

        {sugerencia && (
          <SugerirCompanerosModal
            eventoId={eventoId}
            montaje={montaje}
            persona={sugerencia.persona}
            mesa={sugerencia.mesa}
            registrados={registrados}
            onClose={() => setSugerencia(null)}
          />
        )}

        {porLiberar && (
          <ConfirmDialog
            title={`Reducir ${minus(et.unidades)} o ${minus(et.asientos)}`}
            confirmLabel="Guardar y liberar"
            destructive
            onConfirm={guardar}
            onCancel={() => setPorLiberar(null)}
          >
            <p>
              Con la nueva capacidad {porLiberar.length === 1 ? 'queda' : 'quedan'} sin asiento:{' '}
              <strong>
                {porLiberar
                  .slice(0, 8)
                  .map((k) => porClave.get(k)?.nombre ?? k)
                  .join(', ')}
                {porLiberar.length > 8 ? ` y ${porLiberar.length - 8} más` : ''}
              </strong>
              .
            </p>
          </ConfirmDialog>
        )}

        {liberando && (
          <LiberarAsientosModal
            config={config}
            et={et}
            asientos={asientos}
            onClose={() => setLiberando(false)}
            onLiberar={(mesa) => {
              liberarAsientos(eventoId, mesa)
              setLiberando(false)
            }}
          />
        )}

        {confirmarReasignar && (
          <ConfirmDialog
            title="Volver a asignar todos los asientos"
            confirmLabel="Volver a asignar"
            onConfirm={() => {
              reasignarTodo(eventoId, sentables)
              setConfirmarReasignar(false)
            }}
            onCancel={() => setConfirmarReasignar(false)}
          >
            <p>
              Se borran los {ocupadas} asientos asignados y se vuelven a calcular con el criterio{' '}
              <strong>{CRITERIO_MESAS_LABEL[config.criterio].toLowerCase()}</strong>, incluidos los
              que se asignaron a mano. El área VIP se vuelve a llenar primero.
            </p>
          </ConfirmDialog>
        )}
      </Card>
    </div>
  )
}

function Encabezado({
  montaje,
  subtitulo,
  actions,
}: {
  montaje: MontajeEvento | null | undefined
  subtitulo: string | null
  actions?: ReactNode
}) {
  const dist = distribucionDe(montaje)
  return (
    <SectionHeader
      title={
        dist === 'filas'
          ? 'Filas y asientos'
          : dist === 'herradura'
            ? 'Asientos en U'
            : 'Mesas y sillas'
      }
      badge={
        montaje ? (
          <span className="rounded-full bg-slate-100 px-2.5 py-0.5 text-[11px] font-bold text-slate-600 dark:bg-slate-800 dark:text-slate-300">
            {montajeLabel[montaje]}
          </span>
        ) : undefined
      }
      description={subtitulo ?? 'Configura cómo se sienta la gente en este evento.'}
      actions={actions}
    />
  )
}
