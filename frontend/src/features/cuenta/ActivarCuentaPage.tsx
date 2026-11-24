import { useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation } from '@tanstack/react-query'
import { CheckCircle2 } from 'lucide-react'
import { api } from '@/api'
import { ApiError } from '@/api/client'
import { Button } from '@/components/ui/Button'
import { ErrorNote } from '@/components/ui/Feedback'
import { Field, TextInput } from '@/components/ui/Field'
import { CuentaLayout } from './CuentaLayout'
import { errorDeCampo } from './errores'
import { planPorClave } from './planes'

/**
 * `/activar/:token`, anónima: el enlace del correo. Aquí se elige la contraseña; va directo al
 * servicio de identidad y el token se quema solo si la acepta.
 */
export function ActivarCuentaPage() {
  const { token = '' } = useParams()
  const [password, setPassword] = useState('')
  const [verla, setVerla] = useState(false)
  const activar = useMutation({
    mutationFn: () => api.activateAccount({ token, password }),
  })

  const enviar = (e: FormEvent) => {
    e.preventDefault()
    activar.mutate()
  }

  if (activar.isSuccess) {
    const pedido = activar.data.requestedPlan
    return (
      <CuentaLayout titulo="Cuenta activada">
        <div className="space-y-4 text-sm">
          <CheckCircle2 size={40} className="text-brand dark:text-brand-accent" aria-hidden />
          <p>
            La cuenta de <strong>{activar.data.companyName}</strong> está lista. Entra con{' '}
            <strong>{activar.data.email}</strong> y la contraseña que acabas de elegir.
          </p>
          {pedido && (
            <p className="text-muted-foreground">
              Empiezas en Gratis. El plan {planPorClave(pedido).nombre} se activa al confirmar la
              transferencia; los datos están en "Mi plan".
            </p>
          )}
          <Link
            to="/app"
            className="inline-flex w-full justify-center rounded-full bg-brand px-5 py-2.5 font-bold text-(--brand-primary-contrast) hover:bg-brand-deep"
          >
            Entrar
          </Link>
        </div>
      </CuentaLayout>
    )
  }

  const err = activar.error
  const vencido = err instanceof ApiError && err.code === 'ACTIVATION_TOKEN_INVALID'
  return (
    <CuentaLayout titulo="Activa tu cuenta">
      <form onSubmit={enviar} noValidate className="space-y-4">
        {/* Una sola vez y con "Mostrar": pedirla dos veces suma trabajo y choca con el gestor de
            contraseñas del navegador, que la sugiere y la guarda. */}
        <p id="reglas-clave" className="text-sm text-muted-foreground">
          Elige la contraseña con la que vas a entrar: mínimo 10 caracteres, con letras y números.
        </p>
        <Field label="Contraseña" required error={errorDeCampo(err, 'password')}>
          <TextInput
            type={verla ? 'text' : 'password'}
            name="new-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="new-password"
            minLength={10}
            pattern="(?=.*\d)(?=.*[A-Za-zÁÉÍÓÚÑáéíóúñ]).{10,}"
            aria-describedby="reglas-clave"
            enterKeyHint="done"
            required
          />
        </Field>
        <label className="flex items-center gap-2 text-sm text-muted-foreground">
          <input type="checkbox" checked={verla} onChange={(e) => setVerla(e.target.checked)} />
          Mostrar la contraseña
        </label>
        {err != null && !errorDeCampo(err, 'password') && <ErrorNote error={err} />}
        {vencido && (
          <Link
            to="/crear-cuenta"
            className="block text-center text-sm font-bold text-brand underline dark:text-brand-accent"
          >
            Pedir un enlace nuevo
          </Link>
        )}
        <Button type="submit" className="w-full" disabled={activar.isPending || !password}>
          {activar.isPending ? 'Activando…' : 'Activar cuenta'}
        </Button>
      </form>
    </CuentaLayout>
  )
}
