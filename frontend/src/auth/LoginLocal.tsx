import { useId, useState, type FormEvent } from 'react'
import { Eye, EyeOff, Lock, User } from 'lucide-react'
import { useBranding } from '@/lib/branding'
import { CLAVE_DEMO, signInMock } from './mockIdentity'

/**
 * El login del modo demostración, con el mismo diseño que el tema de Keycloak
 * (`infra/keycloak-themes/sorteo360`): formulario a la izquierda, panel de marca a la derecha.
 * Entra con los usuarios del realm: `admin`, `supervisor` y `sorteo`.
 */
export function LoginLocal() {
  const branding = useBranding()
  const id = useId()
  const [usuario, setUsuario] = useState('')
  const [clave, setClave] = useState('')
  const [verClave, setVerClave] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const entrar = (e: FormEvent) => {
    e.preventDefault()
    if (!signInMock(usuario, clave)) setError('Usuario o contraseña incorrectos.')
  }

  const campo =
    'h-[3.25rem] w-full rounded-xl border border-slate-300 bg-white pl-11 pr-4 text-base text-slate-900 outline-none transition-colors focus:border-brand focus:ring-2 focus:ring-brand/20'

  return (
    <div className="grid min-h-screen bg-white lg:grid-cols-[1fr_1.05fr]">
      <div className="mx-auto flex w-full max-w-[34rem] flex-col px-6 py-10 sm:px-10">
        <img src={branding.logoUrl} alt={branding.appName} className="h-11 w-auto self-start" />

        <form onSubmit={entrar} className="my-auto space-y-6 py-10" noValidate>
          <div>
            <h1 className="text-4xl font-semibold tracking-tight text-slate-900">
              ¡Hola de nuevo!
            </h1>
            <p className="mt-3.5 max-w-md text-[17px] leading-relaxed text-slate-500">
              Inicia sesión para preparar tus sorteos y correr la tómbola en vivo.
            </p>
          </div>

          <div className="space-y-2">
            <label htmlFor={`${id}-u`} className="block text-[15px] font-medium text-slate-900">
              Usuario o correo
            </label>
            <div className="relative">
              <User className="pointer-events-none absolute left-4 top-1/2 h-[18px] w-[18px] -translate-y-1/2 text-slate-400" />
              <input
                id={`${id}-u`}
                autoFocus
                autoComplete="username"
                value={usuario}
                onChange={(e) => {
                  setUsuario(e.target.value)
                  setError(null)
                }}
                aria-invalid={error != null}
                className={campo}
              />
            </div>
          </div>

          <div className="space-y-2">
            <label htmlFor={`${id}-c`} className="block text-[15px] font-medium text-slate-900">
              Contraseña
            </label>
            <div className="relative">
              <Lock className="pointer-events-none absolute left-4 top-1/2 h-[18px] w-[18px] -translate-y-1/2 text-slate-400" />
              <input
                id={`${id}-c`}
                type={verClave ? 'text' : 'password'}
                autoComplete="current-password"
                value={clave}
                onChange={(e) => {
                  setClave(e.target.value)
                  setError(null)
                }}
                aria-invalid={error != null}
                aria-describedby={error ? `${id}-e` : undefined}
                className={`${campo} pr-12`}
              />
              <button
                type="button"
                onClick={() => setVerClave((v) => !v)}
                aria-label={verClave ? 'Ocultar la contraseña' : 'Mostrar la contraseña'}
                className="absolute right-2 top-1/2 -translate-y-1/2 rounded-lg p-2 text-slate-500 hover:text-slate-900"
              >
                {verClave ? <EyeOff size={18} /> : <Eye size={18} />}
              </button>
            </div>
            {error && (
              <p id={`${id}-e`} role="alert" className="text-sm text-rose-600">
                {error}
              </p>
            )}
          </div>

          <button
            type="submit"
            className="h-[3.25rem] w-full rounded-xl bg-brand text-base font-semibold text-white transition-colors hover:bg-brand-deep focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand"
          >
            Iniciar sesión
          </button>

          {import.meta.env.DEV && (
            <p className="text-center text-xs text-slate-400">
              Demostración: admin, supervisor o sorteo · contraseña {CLAVE_DEMO}
            </p>
          )}
        </form>
      </div>

      {/* El panel de marca: el mismo degradado y los mismos textos que el login de Keycloak. */}
      <div className="relative hidden flex-col justify-between overflow-hidden bg-gradient-to-br from-brand-deep via-brand to-brand-deep px-[clamp(3rem,7vw,7rem)] py-16 text-white lg:flex">
        <div
          aria-hidden="true"
          className="pointer-events-none absolute -right-40 -top-40 h-[36rem] w-[36rem] rounded-full bg-white/15 blur-3xl"
        />
        <span />
        <p className="relative text-[clamp(2.25rem,3.6vw,3.5rem)] font-semibold leading-[1.12] tracking-tight">
          Cada premio,
          <br />
          en vivo y con acta.
        </p>
        <p className="relative max-w-[34rem] border-t border-white/20 pt-6 text-[17px] leading-relaxed text-white/80">
          Padrón, rondas y premios listos para la tómbola. Los ganadores salen al instante y el acta
          se arma sola.
        </p>
      </div>
    </div>
  )
}
