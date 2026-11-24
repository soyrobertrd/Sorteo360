import { Check } from 'lucide-react'
import { Button } from '@/components/ui/Button'
import { RadioCards } from '@/components/ui/RadioCards'
import { Field, Select, TextInput } from '@/components/ui/Field'
import type { MontajeEvento } from '@/api/types'
import { PARTICIPANT_TYPES } from '../../mockParticipants'
import {
  CATEGORIAS_PROTOCOLO,
  CRITERIO_MESAS_LABEL,
  MODO_MESAS_LABEL,
  distribucionDe,
  esReservada,
  type ConfigMesas,
  type CriterioMesas,
  type Etiquetas,
  type ModoMesas,
  maxSillasPorMesa,
  unidadesVip,
} from '../../mesas'
import { minus } from './texto'
import { Fragment, useState, type ReactNode } from 'react'

/** Un paso de la configuración, con su título y para qué sirve. */
function Paso({
  titulo,
  descripcion,
  extra,
  children,
}: {
  titulo: string
  descripcion?: string
  extra?: ReactNode
  children: ReactNode
}) {
  return (
    <section>
      <div className="mb-3 flex flex-wrap items-start justify-between gap-2">
        <div className="flex items-start gap-2.5">
          <div>
            <h4 className="text-sm font-bold text-slate-800 dark:text-slate-100">{titulo}</h4>
            {descripcion && <p className="mt-0.5 text-[11px] text-slate-500">{descripcion}</p>}
          </div>
        </div>
        {extra}
      </div>
      {children}
    </section>
  )
}

const MODO_DESCRIPCION: Record<ModoMesas, string> = {
  libre: 'Cada quien elige su asiento; puedes reservar el frente.',
  automatica: 'Cada registrado recibe su asiento al inscribirse.',
  manual: 'Alguien asigna cada asiento después.',
}

export function Configuracion({
  montaje,
  borrador,
  setBorrador,
  et,
  onCancelar,
  onGuardar,
}: {
  montaje: MontajeEvento | null | undefined
  borrador: ConfigMesas
  setBorrador: (c: ConfigMesas) => void
  et: Etiquetas
  onCancelar: () => void
  onGuardar: () => void
}) {
  // Un campo vacío guarda 0 y se muestra vacío, para que se pueda borrar y escribir el número.
  const numero = (v: string, min: number) => (v === '' ? 0 : Math.max(min, Number(v) || min))
  const vacio = (n: number | undefined) => (n ? n : '')
  const dist = distribucionDe(montaje)
  const maxSillas = maxSillasPorMesa(dist)
  const sillas = (v: string) => Math.min(maxSillas, numero(v, 1))
  const [paso, setPaso] = useState(1)
  const vip = unidadesVip(borrador)
  const alternarVip = (mesa: number) =>
    setBorrador({
      ...borrador,
      vip: vip.includes(mesa)
        ? vip.filter((m) => m !== mesa)
        : [...vip, mesa].sort((a, b) => a - b),
    })
  const unidades = Array.from({ length: borrador.mesas }, (_, i) => i + 1)
  const conferencia = montaje === 'Conference'
  // Los campos de Distribución van parejos: 2, 3 o 2 y 2; nunca uno suelto en la última fila.
  const camposDistribucion =
    2 + Number(conferencia) + Number(dist === 'filas') + Number(borrador.modo === 'automatica')
  const columnasDe = (n: number) => (n === 3 ? 'sm:grid-cols-3' : n > 1 ? 'sm:grid-cols-2' : '')
  // Con 5 campos la mesa principal ocupa su propia fila y quedan 2 y 2 debajo.
  const principalAncho =
    borrador.modo !== 'libre' && camposDistribucion === 5 ? 'sm:col-span-2' : undefined
  const pasos =
    borrador.modo === 'libre'
      ? ['Modo', 'Distribución']
      : ['Modo', 'Distribución', 'Área VIP', 'Nombres']
  const actual = Math.min(paso, pasos.length)
  const ultimo = actual === pasos.length
  const distribucionLista =
    borrador.modo === 'libre'
      ? borrador.sillasPorMesa > 0
      : borrador.mesas > 0 && borrador.sillasPorMesa > 0
  const puedeSeguir = actual !== 2 || distribucionLista
  return (
    <div className="flex flex-col gap-4 text-xs">
      <ol
        className="flex items-start gap-2 border-b border-slate-200 pb-3 dark:border-slate-800"
        aria-label="Pasos de la configuración"
      >
        {pasos.map((nombre, i) => {
          const n = i + 1
          const estado = n === actual ? 'actual' : n < actual ? 'hecho' : 'pendiente'
          return (
            <Fragment key={nombre}>
              {i > 0 && (
                <li
                  aria-hidden
                  className={`mt-3 min-w-4 flex-1 border-t-2 border-dashed ${
                    n <= actual
                      ? 'border-brand dark:border-blue-400'
                      : 'border-slate-300 dark:border-slate-700'
                  }`}
                />
              )}
              <li>
                <button
                  type="button"
                  aria-current={estado === 'actual' ? 'step' : undefined}
                  disabled={n > actual && !distribucionLista && actual <= 2}
                  onClick={() => setPaso(n)}
                  className={`flex cursor-pointer flex-col items-center gap-1 whitespace-nowrap text-[11px] font-bold disabled:cursor-not-allowed disabled:opacity-50 ${
                    estado === 'pendiente' ? 'text-slate-500' : 'text-brand dark:text-blue-300'
                  }`}
                >
                  <span
                    className={`flex h-6 w-6 shrink-0 items-center justify-center rounded-full text-[11px] ${
                      estado === 'pendiente'
                        ? 'border-2 border-slate-300 text-slate-500 dark:border-slate-700'
                        : estado === 'actual'
                          ? 'bg-brand text-white ring-4 ring-blue-100 dark:bg-blue-500 dark:ring-blue-950'
                          : 'bg-brand text-white dark:bg-blue-500'
                    }`}
                  >
                    {estado === 'hecho' ? <Check size={12} strokeWidth={3} /> : n}
                  </span>
                  <span>{nombre}</span>
                </button>
              </li>
            </Fragment>
          )
        })}
      </ol>

      {actual === 1 && (
        <Paso titulo="Modo de asignación">
          <RadioCards
            name="modo-mesas"
            legend="¿Cómo se sienta la gente?"
            value={borrador.modo}
            onChange={(modo) => setBorrador({ ...borrador, modo })}
            options={(Object.keys(MODO_MESAS_LABEL) as ModoMesas[]).map((m) => ({
              value: m,
              label: MODO_MESAS_LABEL[m],
              description: MODO_DESCRIPCION[m],
            }))}
          />
        </Paso>
      )}

      {actual === 2 && (
        <Paso
          titulo="Distribución del salón"
          descripcion={
            Number.isFinite(maxSillas)
              ? `Este montaje admite hasta ${maxSillas} ${minus(et.asientos)} por ${minus(et.unidad)}.`
              : undefined
          }
        >
          <div className="space-y-3">
            {borrador.modo === 'libre' && (
              <div className={`grid grid-cols-1 gap-3 ${columnasDe(2 + Number(conferencia))}`}>
                {conferencia && (
                  <div className={principalAncho}>
                    <Field
                      label="Personas en la mesa principal"
                      hint="Sobre el escenario, de frente al público. Se asignan a mano, de antemano."
                    >
                      <TextInput
                        type="number"
                        min={0}
                        max={30}
                        value={vacio(borrador.mesaPrincipal)}
                        onChange={(e) =>
                          setBorrador({
                            ...borrador,
                            mesaPrincipal: Math.min(30, numero(e.target.value, 0)),
                          })
                        }
                      />
                    </Field>
                  </div>
                )}
                <Field
                  label={`${et.unidades} reservadas al frente`}
                  hint={`Para ${CATEGORIAS_PROTOCOLO.join(' y ')}; 0 = sin reserva`}
                >
                  <TextInput
                    type="number"
                    min={0}
                    value={vacio(borrador.reservadas)}
                    onChange={(e) => {
                      const reservadas = numero(e.target.value, 0)
                      // En asiento libre el salón asignable es solo la zona reservada.
                      setBorrador({ ...borrador, reservadas, mesas: reservadas })
                    }}
                  />
                </Field>
                <Field label={`${et.asientos} por ${minus(et.unidad)}`}>
                  <TextInput
                    type="number"
                    min={1}
                    max={Number.isFinite(maxSillas) ? maxSillas : undefined}
                    value={vacio(borrador.sillasPorMesa)}
                    onChange={(e) =>
                      setBorrador({ ...borrador, sillasPorMesa: sillas(e.target.value) })
                    }
                  />
                </Field>
              </div>
            )}

            {borrador.modo !== 'libre' && (
              <div className={`grid grid-cols-1 gap-3 ${columnasDe(camposDistribucion)}`}>
                {conferencia && (
                  <div className={principalAncho}>
                    <Field
                      label="Personas en la mesa principal"
                      hint="Sobre el escenario, de frente al público. Se asignan a mano, de antemano."
                    >
                      <TextInput
                        type="number"
                        min={0}
                        max={30}
                        value={vacio(borrador.mesaPrincipal)}
                        onChange={(e) =>
                          setBorrador({
                            ...borrador,
                            mesaPrincipal: Math.min(30, numero(e.target.value, 0)),
                          })
                        }
                      />
                    </Field>
                  </div>
                )}
                <Field label={`Cantidad de ${minus(et.unidades)}`}>
                  <TextInput
                    type="number"
                    min={1}
                    value={vacio(borrador.mesas)}
                    onChange={(e) => setBorrador({ ...borrador, mesas: numero(e.target.value, 1) })}
                  />
                </Field>
                <Field label={`${et.asientos} por ${minus(et.unidad)}`}>
                  <TextInput
                    type="number"
                    min={1}
                    max={Number.isFinite(maxSillas) ? maxSillas : undefined}
                    value={vacio(borrador.sillasPorMesa)}
                    onChange={(e) =>
                      setBorrador({ ...borrador, sillasPorMesa: sillas(e.target.value) })
                    }
                  />
                </Field>
                {dist === 'filas' && (
                  <Field label="Bloques por fila" hint="Solo visual: pasillos para ubicar rápido">
                    <Select
                      value={borrador.columnas ?? 1}
                      onChange={(e) =>
                        setBorrador({ ...borrador, columnas: Number(e.target.value) })
                      }
                    >
                      {[1, 2, 3].map((n) => (
                        <option key={n} value={n}>
                          {n === 1 ? '1 bloque (sin pasillo)' : `${n} bloques`}
                        </option>
                      ))}
                    </Select>
                  </Field>
                )}
                {borrador.modo === 'automatica' && (
                  <Field label={`Cómo llenar las ${minus(et.unidades)}`}>
                    <Select
                      value={borrador.criterio}
                      onChange={(e) =>
                        setBorrador({ ...borrador, criterio: e.target.value as CriterioMesas })
                      }
                    >
                      {(Object.keys(CRITERIO_MESAS_LABEL) as CriterioMesas[]).map((c) => (
                        <option key={c} value={c}>
                          {CRITERIO_MESAS_LABEL[c]}
                        </option>
                      ))}
                    </Select>
                  </Field>
                )}
              </div>
            )}
          </div>
        </Paso>
      )}

      {borrador.modo !== 'libre' && (
        <>
          {actual === 3 && (
            <fieldset>
              <div className="mb-3 flex flex-wrap items-start justify-between gap-2">
                <div className="flex items-start gap-2.5">
                  <div>
                    <legend className="text-sm font-bold text-slate-800 dark:text-slate-100">
                      Área VIP
                    </legend>
                    <p className="mt-0.5 text-[11px] text-slate-500">
                      Elige las {minus(et.unidades)} para {CATEGORIAS_PROTOCOLO.join(' y ')}. El
                      reparto automático las llena primero con ellos.
                    </p>
                  </div>
                </div>
                <div className="flex items-center gap-3">
                  <span className="rounded-full bg-amber-100 px-2.5 py-0.5 text-[11px] font-bold text-amber-800 dark:bg-amber-900/40 dark:text-amber-200">
                    {vip.length} de {borrador.mesas} VIP
                  </span>
                  <button
                    type="button"
                    onClick={() => setBorrador({ ...borrador, vip: unidades })}
                    className="cursor-pointer text-[11px] font-bold text-blue-700 hover:underline dark:text-blue-400"
                  >
                    Todas
                  </button>
                  <button
                    type="button"
                    onClick={() => setBorrador({ ...borrador, vip: [] })}
                    className="cursor-pointer text-[11px] font-bold text-slate-500 hover:underline"
                  >
                    Ninguna
                  </button>
                </div>
              </div>
              <div className="grid max-h-[45vh] grid-cols-2 gap-2 overflow-y-auto pr-1 sm:grid-cols-3">
                {unidades.map((mesa) => {
                  const activa = vip.includes(mesa)
                  return (
                    <button
                      key={mesa}
                      type="button"
                      aria-pressed={activa}
                      onClick={() => alternarVip(mesa)}
                      className={`flex cursor-pointer items-center gap-2 rounded-lg border px-3 py-2 text-left transition-colors focus-visible:outline-2 focus-visible:outline-blue-600 ${
                        activa
                          ? 'border-amber-400 bg-amber-50 dark:border-amber-600 dark:bg-amber-950/30'
                          : 'border-slate-200 bg-white hover:border-slate-300 dark:border-slate-700 dark:bg-slate-900'
                      }`}
                    >
                      <span
                        className={`flex h-4 w-4 shrink-0 items-center justify-center rounded border ${
                          activa
                            ? 'border-amber-500 bg-amber-500 text-white'
                            : 'border-slate-300 dark:border-slate-600'
                        }`}
                      >
                        {activa && <Check size={11} strokeWidth={3} />}
                      </span>
                      <span className="min-w-0">
                        <span className="block truncate font-bold text-slate-800 dark:text-slate-100">
                          {borrador.nombres?.[mesa] || `${et.unidad} ${mesa}`}
                        </span>
                        <span className="block text-[10px] text-slate-500">
                          {borrador.sillasPorMesa} {minus(et.asientos)}
                        </span>
                      </span>
                    </button>
                  )
                })}
              </div>
            </fieldset>
          )}

          {actual === 4 && (
            <section>
              <h4 className="text-sm font-bold text-slate-800 dark:text-slate-100">
                Nombres de {minus(et.unidades)}
                {borrador.modo === 'automatica' &&
                  borrador.criterio === 'categoria' &&
                  ' y categoría de cada una'}{' '}
                (opcional)
              </h4>
              <div className="mt-3 grid max-h-[45vh] overflow-y-auto pr-1 grid-cols-1 gap-2 sm:grid-cols-2">
                {unidades.map((mesa) => (
                  <div key={mesa} className="space-y-1.5">
                    <TextInput
                      aria-label={`Nombre de la ${minus(et.unidad)} ${mesa}`}
                      placeholder={`${et.unidad} ${mesa}${esReservada(borrador, mesa) ? ' · VIP' : ''}`}
                      value={borrador.nombres?.[mesa] ?? ''}
                      onChange={(e) =>
                        setBorrador({
                          ...borrador,
                          nombres: { ...borrador.nombres, [mesa]: e.target.value },
                        })
                      }
                    />
                    {borrador.modo === 'automatica' && borrador.criterio === 'categoria' && (
                      <Select
                        aria-label={`Categoría de la ${minus(et.unidad)} ${mesa}`}
                        value={borrador.categorias[mesa] ?? ''}
                        onChange={(e) => {
                          const categorias = { ...borrador.categorias }
                          if (e.target.value) categorias[mesa] = e.target.value
                          else delete categorias[mesa]
                          setBorrador({ ...borrador, categorias })
                        }}
                      >
                        <option value="">General</option>
                        {PARTICIPANT_TYPES.map((t) => (
                          <option key={t} value={t}>
                            {t}
                          </option>
                        ))}
                      </Select>
                    )}
                  </div>
                ))}
              </div>
            </section>
          )}
        </>
      )}

      <div className="flex flex-wrap items-center justify-between gap-2 border-t border-slate-200 pt-3 dark:border-slate-800">
        <Button size="sm" variant="ghost" onClick={onCancelar}>
          Cancelar
        </Button>
        <div className="flex items-center gap-2">
          {actual > 1 && (
            <Button size="sm" variant="outline" onClick={() => setPaso(actual - 1)}>
              Anterior
            </Button>
          )}
          {ultimo ? (
            <Button size="sm" disabled={!distribucionLista} onClick={onGuardar}>
              Guardar
            </Button>
          ) : (
            <Button size="sm" disabled={!puedeSeguir} onClick={() => setPaso(actual + 1)}>
              Siguiente
            </Button>
          )}
        </div>
      </div>
    </div>
  )
}
