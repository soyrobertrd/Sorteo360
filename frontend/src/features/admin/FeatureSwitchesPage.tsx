import { useState } from 'react'
import { Link } from 'react-router-dom'
import { ArrowRight, Eye, EyeOff, ToggleRight } from 'lucide-react'
import { Breadcrumbs } from '@/components/ui/Breadcrumbs'
import { Callout } from '@/components/ui/Callout'
import { Card } from '@/components/ui/Card'
import { ErrorNote, Spinner } from '@/components/ui/Feedback'
import {
  CATALOGUE_BY_KEY,
  FEATURE_CATALOGUE,
  type SwitchableModuleKey,
  type SwitchKey,
} from './featureCatalogue'
import {
  ESTILOS_CLASICOS,
  estiloClasicoActivo,
  disabledFeatureCount,
  isOwnSwitchOff,
  lastChangeLabel,
  sidebarPreview,
} from './featureSwitches'
import { useFeatureSwitches, useSetFeatureSwitch } from './hooks'

/**
 * `/admin/feature-switches`: los módulos a la izquierda, las funciones del elegido en el centro y,
 * a la derecha, su barra lateral como la vería un usuario con todos los roles. Cada cambio se
 * guarda al instante y aplica a todos los usuarios.
 */
export function FeatureSwitchesPage() {
  const { stored, isEnabled, isLoading, error } = useFeatureSwitches()
  const setSwitch = useSetFeatureSwitch()
  const [selected, setSelected] = useState<SwitchableModuleKey>(FEATURE_CATALOGUE[0].key)
  const module = CATALOGUE_BY_KEY[selected]
  const moduleOn = isEnabled(selected)
  const ownOn = (key: SwitchKey) => !isOwnSwitchOff(stored, key)
  const flip = (key: SwitchKey) => setSwitch.set(key, !ownOn(key))
  const preview = sidebarPreview(stored, module)

  return (
    <div className="space-y-6">
      <Breadcrumbs
        items={[{ label: 'Administración', to: '/admin' }, { label: 'Interruptores de funciones' }]}
      />
      <div>
        <h1 className="flex items-center gap-2.5 font-sans text-2xl font-extrabold tracking-tight text-slate-900 sm:text-3xl dark:text-white">
          <ToggleRight className="h-7 w-7 text-brand-deep dark:text-blue-400" />
          <span>Interruptores de funciones</span>
        </h1>
        <p className="mt-1 font-body text-xs text-slate-500 sm:text-sm dark:text-slate-400">
          Apaga un módulo y desaparece del portal. Apaga una función y desaparece de la barra
          lateral de su módulo. Aplica a todos los usuarios, también a quien administra.
        </p>
      </div>

      {isLoading ? (
        <Spinner />
      ) : error ? (
        <ErrorNote error={error} />
      ) : (
        <>
          {setSwitch.error && <ErrorNote error={setSwitch.error} />}
          <div className="grid gap-4 lg:grid-cols-[240px_1fr_260px]">
            <Card className="p-2">
              <p className="px-3 pb-2 pt-1 text-[11px] font-bold uppercase tracking-wider text-slate-400">
                Módulos del portal
              </p>
              {FEATURE_CATALOGUE.map((m) => {
                const on = isEnabled(m.key)
                const off = disabledFeatureCount(stored, m)
                return (
                  <div
                    key={m.key}
                    className={`flex items-center gap-2.5 rounded-xl px-3 py-2 text-sm font-semibold ${
                      m.key === selected
                        ? 'bg-muted text-brand-deep dark:bg-blue-950/50 dark:text-blue-300'
                        : 'text-slate-600 hover:bg-slate-50 dark:text-slate-300 dark:hover:bg-slate-800'
                    }`}
                  >
                    <button
                      type="button"
                      onClick={() => setSelected(m.key)}
                      className="flex flex-1 cursor-pointer items-center gap-2.5 text-left"
                    >
                      <m.icon size={17} className={on ? '' : 'text-slate-300'} />
                      <span className="flex-1">
                        <span className={on ? '' : 'text-slate-400 line-through'}>{m.label}</span>
                        {off > 0 && (
                          <span className="block text-[11px] font-medium text-slate-400">
                            {off} {off === 1 ? 'función apagada' : 'funciones apagadas'}
                          </span>
                        )}
                      </span>
                    </button>
                    <SwitchControl
                      size="sm"
                      on={on}
                      onChange={() => flip(m.key)}
                      disabled={setSwitch.isPending(m.key)}
                      label={m.label}
                    />
                  </div>
                )
              })}
            </Card>

            <Card className="p-5">
              <div className="mb-4">
                <p className="font-sans text-lg font-extrabold text-brand-deep dark:text-white">
                  {module.label}
                </p>
                <p className="text-xs text-slate-500">
                  {lastChangeLabel(stored.get(module.key)) ?? 'Funciones de la barra lateral'}
                </p>
              </div>
              {!moduleOn && (
                <Callout tone="warning">
                  {module.label} está apagado: su tarjeta no aparece en el portal y sus rutas
                  muestran "Módulo desactivado". Sus funciones conservan su interruptor.
                </Callout>
              )}
              <div className="mt-3 grid gap-2 sm:grid-cols-2">
                {module.features.map((feature) => {
                  const key = feature.key as SwitchKey
                  const on = ownOn(key)
                  const cambio = lastChangeLabel(stored.get(key))
                  return (
                    <div
                      key={feature.key}
                      className={`flex items-center gap-3 rounded-xl border px-3 py-3 ${
                        on && moduleOn
                          ? 'border-slate-200 bg-white dark:border-slate-700 dark:bg-slate-900'
                          : 'border-dashed border-slate-300 bg-slate-50 dark:border-slate-700 dark:bg-slate-800/40'
                      }`}
                    >
                      <feature.icon size={18} className="text-slate-400" />
                      <div className="flex-1">
                        <p className="text-sm font-bold text-slate-800 dark:text-slate-100">
                          {feature.label}
                        </p>
                        {cambio && <p className="text-[11px] text-slate-400">{cambio}</p>}
                      </div>
                      <SwitchControl
                        on={on}
                        onChange={() => flip(key)}
                        disabled={setSwitch.isPending(key)}
                        label={`${module.label} › ${feature.label}`}
                      />
                    </div>
                  )
                })}
              </div>
            </Card>

            <Card className="p-4">
              <p className="mb-3 flex items-center gap-1.5 text-[11px] font-bold uppercase tracking-wider text-slate-400">
                <Eye size={13} /> Así lo ve un usuario con todos los roles
              </p>
              {preview.length > 0 ? (
                <div className="rounded-xl border border-slate-200 bg-white p-2 dark:border-slate-700 dark:bg-card">
                  <p className="px-2 pb-1.5 text-[10px] font-bold uppercase tracking-wider text-slate-400">
                    {module.label}
                  </p>
                  {preview.map((item, i) => (
                    <div
                      key={item.label}
                      className={`flex items-center gap-2 rounded-lg px-2 py-1.5 text-xs font-semibold ${
                        i === 0
                          ? 'bg-muted text-brand-deep dark:bg-blue-950/50 dark:text-blue-300'
                          : 'text-slate-600 dark:text-slate-300'
                      }`}
                    >
                      <item.icon size={14} />
                      <span className="flex-1">{item.label}</span>
                      {item.roleGated && (
                        <span className="rounded-full bg-slate-100 px-1.5 py-0.5 text-[9px] font-bold uppercase tracking-wider text-slate-500 dark:bg-slate-800 dark:text-slate-400">
                          según rol
                        </span>
                      )}
                    </div>
                  ))}
                </div>
              ) : (
                <div className="flex flex-col items-center gap-2 rounded-xl border border-dashed border-slate-300 p-6 text-center text-xs text-slate-400 dark:border-slate-700">
                  <EyeOff size={20} />
                  No aparece en el portal
                </div>
              )}
              <Link
                to={moduleOn ? module.route : '/'}
                className="mt-3 inline-flex items-center gap-1 text-xs font-bold text-brand-deep hover:underline dark:text-blue-300"
              >
                {moduleOn ? `Ir a ${module.label}` : 'Ver el portal'} <ArrowRight size={13} />
              </Link>
            </Card>
          </div>

          <Card className="p-5">
            <h2 className="text-sm font-bold text-slate-900 dark:text-white">
              Pantallas del sorteo
            </h2>
            <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">
              El tablero de ganadores está siempre disponible. La ruleta y los nombres pasando
              vienen desactivados; márcalos para ofrecerlos al iniciar un sorteo.
            </p>
            <div className="mt-4 flex flex-wrap gap-x-8 gap-y-3">
              {(
                [
                  [ESTILOS_CLASICOS.ruleta, 'Ruleta'],
                  [ESTILOS_CLASICOS.nombres, 'Nombres pasando'],
                ] as const
              ).map(([key, label]) => {
                const on = estiloClasicoActivo(stored, key)
                return (
                  <label
                    key={key}
                    className="flex cursor-pointer items-center gap-2.5 text-sm font-medium text-slate-800 dark:text-slate-100"
                  >
                    <input
                      type="checkbox"
                      checked={on}
                      disabled={setSwitch.isPending(key)}
                      onChange={() => setSwitch.set(key, !on)}
                      className="h-4 w-4 rounded border-slate-300 accent-brand-deep"
                    />
                    {label}
                  </label>
                )
              })}
            </div>
          </Card>
        </>
      )}
    </div>
  )
}

/**
 * El interruptor de cada módulo y cada función. Se deshabilita mientras su cambio espera respuesta,
 * para que un doble clic no mande dos órdenes contrarias.
 */
function SwitchControl({
  on,
  onChange,
  disabled,
  label,
  size = 'md',
}: {
  on: boolean
  onChange: () => void
  disabled: boolean
  label: string
  size?: 'sm' | 'md'
}) {
  const track = size === 'sm' ? 'h-5 w-9' : 'h-6 w-11'
  const knob = size === 'sm' ? 'h-4 w-4' : 'h-5 w-5'
  const shift = size === 'sm' ? 'translate-x-4' : 'translate-x-5'
  return (
    <button
      type="button"
      role="switch"
      aria-checked={on}
      aria-label={label}
      onClick={onChange}
      disabled={disabled}
      className={`relative inline-flex shrink-0 cursor-pointer items-center rounded-full p-0.5 transition-colors disabled:cursor-wait disabled:opacity-60 ${track} ${
        on ? 'bg-brand-deep dark:bg-blue-500' : 'bg-slate-300 dark:bg-slate-700'
      }`}
    >
      <span
        className={`rounded-full bg-white shadow transition-transform ${knob} ${on ? shift : 'translate-x-0'}`}
      />
    </button>
  )
}
