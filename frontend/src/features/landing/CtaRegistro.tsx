import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { config } from '@/lib/config'

/**
 * El botón que lleva a tener cuenta. Con el registro encendido va a `/crear-cuenta`; apagado
 * (`VITE_SIGNUP_ENABLED=false`, mientras el servicio no lo tenga) abre un correo a
 * `VITE_CONTACT_EMAIL`, y sin correo no se muestra.
 */
export function CtaRegistro({
  className,
  registro,
  contacto,
  query = '',
  asunto = 'Quiero conocer Sorteo360',
}: {
  className: string
  /** El texto con el registro encendido. */
  registro: ReactNode
  /** El texto cuando se ofrece escribir. */
  contacto: ReactNode
  /** Lo que viaja a `/crear-cuenta` (`?plan=…`). */
  query?: string
  asunto?: string
}) {
  if (config.signupEnabled) {
    return (
      <Link to={`/crear-cuenta${query}`} className={className}>
        {registro}
      </Link>
    )
  }
  if (!config.contactEmail) return null
  return (
    <a
      href={`mailto:${config.contactEmail}?subject=${encodeURIComponent(asunto)}`}
      className={className}
    >
      {contacto}
    </a>
  )
}
