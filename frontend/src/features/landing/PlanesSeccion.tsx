import { useState } from 'react'
import { Check, Minus } from 'lucide-react'
import { cn } from '@/lib/utils'
import {
  lineasDelPlan,
  MESES_COBRADOS_AL_ANO,
  PLANES,
  precioTexto,
  type BillingPeriod,
} from '@/features/cuenta/planes'
import { CtaRegistro } from './CtaRegistro'

const PERIODOS: { valor: BillingPeriod; etiqueta: string }[] = [
  { valor: 'monthly', etiqueta: 'Mensual' },
  { valor: 'annual', etiqueta: 'Anual' },
]

/** Los cuatro planes con el selector mensual/anual. Cada botón lleva al registro con su plan. */
export function PlanesSeccion() {
  const [periodo, setPeriodo] = useState<BillingPeriod>('monthly')
  const mesesDeRegalo = 12 - MESES_COBRADOS_AL_ANO

  return (
    <section id="planes" className="scroll-mt-20 border-t border-border bg-card">
      <div className="mx-auto max-w-6xl px-4 py-16 sm:px-6 md:py-20">
        <div className="flex flex-col gap-6 md:flex-row md:items-end md:justify-between">
          <div>
            <h2 className="text-3xl font-black text-balance">Planes</h2>
            <p className="mt-2 max-w-xl text-muted-foreground">
              El plan se activa al confirmar la transferencia.
            </p>
          </div>
          <fieldset className="flex items-center gap-3">
            <legend className="sr-only">Forma de pago</legend>
            <div className="inline-flex rounded-full border border-border bg-background p-1">
              {PERIODOS.map((p) => (
                <label
                  key={p.valor}
                  className={cn(
                    'relative cursor-pointer rounded-full px-4 py-1.5 text-sm font-bold has-focus-visible:outline-2 has-focus-visible:outline-brand',
                    periodo === p.valor
                      ? 'bg-brand text-(--brand-primary-contrast)'
                      : 'text-muted-foreground hover:text-foreground',
                  )}
                >
                  <input
                    type="radio"
                    name="periodo"
                    value={p.valor}
                    checked={periodo === p.valor}
                    onChange={() => setPeriodo(p.valor)}
                    className="sr-only"
                  />
                  {p.etiqueta}
                </label>
              ))}
            </div>
            <span className="text-sm font-semibold text-brand dark:text-brand-accent">
              Anual: {mesesDeRegalo} meses de regalo
            </span>
          </fieldset>
        </div>

        {/* El plan Gratis existe (el registro arranca ahí) pero no se publicita: solo los de pago. */}
        <ul className="mt-10 grid gap-6 md:grid-cols-3">
          {PLANES.filter((plan) => plan.key !== 'free').map((plan) => (
            <li
              key={plan.key}
              className={cn(
                'relative flex flex-col rounded-2xl border bg-background p-6',
                plan.destacado ? 'border-brand shadow-lg' : 'border-border',
              )}
            >
              {plan.destacado && (
                <span className="absolute -top-3 left-6 rounded-full bg-brand px-3 py-0.5 text-xs font-bold text-(--brand-primary-contrast)">
                  Recomendado
                </span>
              )}
              <h3 className="text-xl font-black">{plan.nombre}</h3>
              <p className="mt-1 text-sm text-muted-foreground">{plan.resumen}</p>
              <p className="mt-4 text-2xl font-black">{precioTexto(plan, periodo)}</p>
              <ul className="mt-5 flex-1 space-y-2 text-sm">
                {lineasDelPlan(plan).map((l) => (
                  <li
                    key={l.texto}
                    className={cn('flex gap-2', !l.incluido && 'text-muted-foreground')}
                  >
                    {l.incluido ? (
                      <Check
                        size={16}
                        className="mt-0.5 shrink-0 text-brand dark:text-brand-accent"
                      />
                    ) : (
                      <Minus size={16} className="mt-0.5 shrink-0" />
                    )}
                    <span>
                      {!l.incluido && <span className="sr-only">No incluye: </span>}
                      {l.texto}
                    </span>
                  </li>
                ))}
              </ul>
              <CtaRegistro
                query={`?plan=${plan.key}&periodo=${periodo}`}
                asunto={`Plan ${plan.nombre} de Sorteo360`}
                registro={plan.key === 'free' ? 'Crear cuenta gratis' : `Elegir ${plan.nombre}`}
                contacto={`Quiero el plan ${plan.nombre}`}
                className={cn(
                  'mt-6 inline-flex justify-center rounded-lg px-5 py-2.5 text-sm font-bold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand',
                  plan.destacado || plan.key === 'free'
                    ? 'bg-brand text-(--brand-primary-contrast) hover:bg-brand-deep'
                    : 'border border-border hover:bg-muted',
                )}
              />
            </li>
          ))}
        </ul>
      </div>
    </section>
  )
}
