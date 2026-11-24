import { useEffect, useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useMutation } from '@tanstack/react-query'
import { MailCheck } from 'lucide-react'
import { api } from '@/api'
import type { SignupRequest, SignupResponse } from '@/api/types'
import { Button } from '@/components/ui/Button'
import { ErrorNote } from '@/components/ui/Feedback'
import { Field, Select, TextInput } from '@/components/ui/Field'
import { CuentaLayout } from './CuentaLayout'
import { errorDeCampo } from './errores'
import { PLANES, planPorClave, precioTexto, type BillingPeriod, type PlanKey } from './planes'

const ESPERA_REENVIO_S = 60

/**
 * `/crear-cuenta`, anónima. Pide empresa, nombre y correo (la contraseña se pone al activar) y
 * manda el enlace de activación. Llega con `?plan=` y `?periodo=` desde los planes del landing.
 */
export function CrearCuentaPage() {
  const [params] = useSearchParams()
  const [form, setForm] = useState<SignupRequest>({
    companyName: '',
    fullName: '',
    email: '',
    plan: planPorClave(params.get('plan')).key,
    billingPeriod: params.get('periodo') === 'annual' ? 'annual' : 'monthly',
    acceptTerms: false,
  })
  const registro = useMutation({ mutationFn: (body: SignupRequest) => api.signup(body) })
  const plan = planPorClave(form.plan)
  const set = <K extends keyof SignupRequest>(k: K, v: SignupRequest[K]) =>
    setForm((f) => ({ ...f, [k]: v }))

  const enviar = (e: FormEvent) => {
    e.preventDefault()
    registro.mutate(form)
  }

  if (registro.isSuccess) {
    return (
      <RevisaTuCorreo
        email={form.email}
        primera={registro.data}
        planDePago={plan.key !== 'free' ? plan.nombre : null}
      />
    )
  }

  const err = registro.error
  return (
    <CuentaLayout titulo="Crea tu cuenta">
      <form onSubmit={enviar} noValidate className="space-y-4">
        <Field label="Empresa" required error={errorDeCampo(err, 'companyName')}>
          <TextInput
            value={form.companyName}
            onChange={(e) => set('companyName', e.target.value)}
            autoComplete="organization"
            required
          />
        </Field>
        <Field label="Tu nombre" required error={errorDeCampo(err, 'fullName')}>
          <TextInput
            value={form.fullName}
            onChange={(e) => set('fullName', e.target.value)}
            autoComplete="name"
            required
          />
        </Field>
        <Field
          label="Correo de trabajo"
          required
          error={errorDeCampo(err, 'email')}
          hint="Ahí llega el enlace para activar la cuenta."
        >
          <TextInput
            type="email"
            value={form.email}
            onChange={(e) => set('email', e.target.value)}
            autoComplete="email"
            spellCheck={false}
            inputMode="email"
            required
          />
        </Field>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Plan">
            <Select value={form.plan} onChange={(e) => set('plan', e.target.value as PlanKey)}>
              {PLANES.map((p) => (
                <option key={p.key} value={p.key}>
                  {p.nombre}
                </option>
              ))}
            </Select>
          </Field>
          {plan.key !== 'free' && (
            <Field label="Pago">
              <Select
                value={form.billingPeriod}
                onChange={(e) => set('billingPeriod', e.target.value as BillingPeriod)}
              >
                <option value="monthly">Mensual</option>
                <option value="annual">Anual (2 meses de regalo)</option>
              </Select>
            </Field>
          )}
        </div>
        {plan.key !== 'free' && (
          <p className="rounded-xl bg-muted px-3 py-2 text-xs text-muted-foreground">
            {plan.nombre}: {precioTexto(plan, form.billingPeriod)}. La cuenta empieza en Gratis y el
            plan se activa cuando confirmemos la transferencia.
          </p>
        )}
        <Field label="Términos" error={errorDeCampo(err, 'acceptTerms')}>
          <span className="flex items-start gap-2 text-sm">
            <input
              type="checkbox"
              checked={form.acceptTerms}
              onChange={(e) => set('acceptTerms', e.target.checked)}
              className="mt-0.5"
            />
            <span>
              Acepto los{' '}
              <Link to="/terminos" target="_blank" className="font-semibold underline">
                términos de uso
              </Link>{' '}
              y la{' '}
              <Link to="/privacidad" target="_blank" className="font-semibold underline">
                política de privacidad
              </Link>
              .
            </span>
          </span>
        </Field>
        {err != null && !errorDeCampo(err, 'email') && <ErrorNote error={err} />}
        <Button type="submit" className="w-full" disabled={registro.isPending}>
          {registro.isPending ? 'Creando…' : 'Crear cuenta'}
        </Button>
        <p className="text-center text-sm text-muted-foreground">
          ¿Ya tienes cuenta?{' '}
          <Link to="/app" className="font-bold text-brand underline dark:text-brand-accent">
            Entrar
          </Link>
        </p>
      </form>
    </CuentaLayout>
  )
}

function RevisaTuCorreo({
  email,
  primera,
  planDePago,
}: {
  email: string
  primera: SignupResponse
  planDePago: string | null
}) {
  const [espera, setEspera] = useState(ESPERA_REENVIO_S)
  const reenvio = useMutation({
    mutationFn: () => api.resendActivation(email),
    onSuccess: () => setEspera(ESPERA_REENVIO_S),
  })
  useEffect(() => {
    if (espera <= 0) return
    const id = window.setTimeout(() => setEspera((s) => s - 1), 1000)
    return () => window.clearTimeout(id)
  }, [espera])
  const demo = (reenvio.data ?? primera).demoActivationPath

  return (
    <CuentaLayout titulo="Revisa tu correo">
      <div className="space-y-4 text-sm">
        <MailCheck size={40} className="text-brand dark:text-brand-accent" aria-hidden />
        <p>
          Si <strong>{email}</strong> no tiene cuenta todavía, te mandamos un enlace para activarla.
          Vence en 24 horas. Al abrirlo eliges tu contraseña.
        </p>
        {planDePago && (
          <p className="text-muted-foreground">
            Tu solicitud del plan {planDePago} queda pendiente; al activar verás cómo hacer la
            transferencia.
          </p>
        )}
        {demo && (
          <p className="rounded-xl border border-dashed border-border px-3 py-2">
            Modo demo, sin correo:{' '}
            <Link to={demo} className="font-bold text-brand underline dark:text-brand-accent">
              abrir el enlace de activación
            </Link>
          </p>
        )}
        {reenvio.error != null && <ErrorNote error={reenvio.error} />}
        <Button
          type="button"
          variant="outline"
          className="w-full"
          disabled={espera > 0 || reenvio.isPending}
          onClick={() => reenvio.mutate()}
        >
          {espera > 0 ? `Reenviar el correo en ${espera} s` : 'Reenviar el correo'}
        </Button>
      </div>
    </CuentaLayout>
  )
}
