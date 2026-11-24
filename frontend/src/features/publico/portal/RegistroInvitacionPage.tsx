import { useState, type FormEvent } from 'react'
import { CargoSelect } from '@/components/ui/CargoSelect'
import { Link } from 'react-router-dom'
import { useMutation, useQuery } from '@tanstack/react-query'
import { api, ApiError } from '@/api'
import type { InvitationPrefill, PublicEventoDetail, PublicRegistrationResponse } from '@/api/types'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { Callout } from '@/components/ui/Callout'
import { Spinner } from '@/components/ui/Feedback'
import { Field, TextInput } from '@/components/ui/Field'
import { apiErrorCodeLabel } from '@/lib/labels'
import { PortalEvent, PortalNotice, PortalPage, TicketQr } from './PortalParts'
import { fieldError, hasCode, portalErrorMessage, useFragmentParam } from './portal'

/**
 * `/publico/registro#t=<token>`: el enlace de una invitación. Resuelve el token, precarga los
 * datos si el evento es privado e inscribe (spec convocatorias/06 §1, §2.2).
 */
export function RegistroInvitacionPage() {
  const token = useFragmentParam('t')
  const resolved = useQuery({
    queryKey: ['publico', 'invitacion', token],
    queryFn: () => api.resolveRegistration(token!),
    enabled: Boolean(token),
    // Un 404, un 409 o un 429 no se arreglan repitiendo, y cada intento cuenta para el límite.
    retry: false,
  })
  const [registro, setRegistro] = useState<PublicRegistrationResponse | null>(null)
  const [cerrado, setCerrado] = useState(false)

  let content
  if (!token) {
    content = (
      <PortalNotice tone="danger" title="Enlace incompleto">
        {apiErrorCodeLabel.PUBLIC_REGISTRATION_NOT_FOUND}
      </PortalNotice>
    )
  } else if (resolved.isLoading) {
    content = <Spinner />
  } else if (cerrado || hasCode(resolved.error, 'REGISTRATIONS_CLOSED')) {
    content = (
      <PortalNotice tone="warning" title="Inscripciones cerradas">
        {apiErrorCodeLabel.REGISTRATIONS_CLOSED}
      </PortalNotice>
    )
  } else if (resolved.error || !resolved.data) {
    content = (
      <PortalNotice tone="danger" title="No pudimos abrir tu invitación">
        {portalErrorMessage(resolved.error)}
      </PortalNotice>
    )
  } else if (registro) {
    content = <Confirmacion registro={registro} />
  } else {
    content = (
      <>
        <PortalEvent event={resolved.data.event} />
        <RegistroForm
          event={resolved.data.event}
          token={token}
          prefill={resolved.data.prefill ?? null}
          onRegistered={setRegistro}
          onClosed={() => setCerrado(true)}
        />
      </>
    )
  }

  return <PortalPage>{content}</PortalPage>
}

function RegistroForm({
  event,
  token,
  prefill,
  onRegistered,
  onClosed,
}: {
  event: PublicEventoDetail
  token: string
  prefill: InvitationPrefill | null
  onRegistered: (r: PublicRegistrationResponse) => void
  onClosed: () => void
}) {
  const [form, setForm] = useState({
    firstName: prefill?.firstName ?? '',
    lastName: prefill?.lastName ?? '',
    email: prefill?.email ?? '',
    phone: '',
    company: '',
    jobTitle: '',
    country: '',
  })
  const set = (key: keyof typeof form) => (e: { target: { value: string } }) =>
    setForm((f) => ({ ...f, [key]: e.target.value }))

  const mutation = useMutation({
    mutationFn: () =>
      api.register({
        eventId: event.id,
        token,
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        email: form.email.trim(),
        // Los opcionales vacíos no viajan: el servicio los guarda nulos.
        phone: form.phone.trim() || null,
        company: form.company.trim() || null,
        jobTitle: form.jobTitle.trim() || null,
        country: form.country.trim() || null,
      }),
    onSuccess: onRegistered,
    // Alguien cerró la ventana mientras la persona llenaba el formulario (spec 06 §7.9).
    onError: (error) => hasCode(error, 'REGISTRATIONS_CLOSED') && onClosed(),
  })

  const submit = (e: FormEvent) => {
    e.preventDefault()
    mutation.mutate()
  }
  const err = mutation.error
  // Un 400 de campo se muestra sobre cada input; el aviso de abajo solo dice que algo falta.
  const esDeCampo = err instanceof ApiError && Object.keys(err.fieldErrors ?? {}).length > 0

  return (
    <Card className="p-6">
      <form onSubmit={submit} className="space-y-4">
        <h2 className="font-sans text-base font-bold text-slate-900 dark:text-white">
          Completa tus datos para inscribirte
        </h2>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <Field label="Nombre" required error={fieldError(err, 'FirstName')}>
            <TextInput required value={form.firstName} onChange={set('firstName')} />
          </Field>
          <Field label="Apellido" required error={fieldError(err, 'LastName')}>
            <TextInput required value={form.lastName} onChange={set('lastName')} />
          </Field>
          <Field label="Correo electrónico" required error={fieldError(err, 'Email')}>
            <TextInput type="email" required value={form.email} onChange={set('email')} />
          </Field>
          <Field label="Teléfono" error={fieldError(err, 'Phone')}>
            <TextInput type="tel" value={form.phone} onChange={set('phone')} />
          </Field>
          <Field label="Empresa" error={fieldError(err, 'Company')}>
            <TextInput value={form.company} onChange={set('company')} />
          </Field>
          <Field label="Cargo" error={fieldError(err, 'JobTitle')}>
            <CargoSelect
              value={form.jobTitle}
              onChange={(jobTitle) => set('jobTitle')({ target: { value: jobTitle } })}
              aria-label="Cargo"
            />
          </Field>
          <Field label="País" error={fieldError(err, 'Country')}>
            <TextInput value={form.country} onChange={set('country')} />
          </Field>
        </div>
        {err && (
          <Callout tone="danger">
            {esDeCampo ? 'Revisa los datos marcados.' : portalErrorMessage(err)}
          </Callout>
        )}
        <div className="flex justify-end">
          <Button type="submit" disabled={mutation.isPending}>
            {mutation.isPending ? 'Enviando…' : 'Confirmar inscripción'}
          </Button>
        </div>
      </form>
    </Card>
  )
}

function Confirmacion({ registro }: { registro: PublicRegistrationResponse }) {
  return (
    <>
      <PortalEvent event={registro.event} />
      <Card className="space-y-4 p-6 text-center">
        <Callout tone="success" title="¡Inscripción confirmada!">
          Te llegará una confirmación por correo. Presenta este código QR en la entrada.
        </Callout>
        <TicketQr src={api.ticketQrUrl(registro.qrCode)} />
        <p className="text-sm text-slate-600 dark:text-slate-300">
          Referencia:{' '}
          <span className="font-mono font-bold text-slate-900 dark:text-white">
            {registro.reference}
          </span>
        </p>
        <Link
          to={`/publico/entrada/${encodeURIComponent(registro.qrCode)}`}
          className="inline-block text-sm font-bold text-blue-700 hover:underline dark:text-blue-400"
        >
          Ver mi entrada
        </Link>
      </Card>
    </>
  )
}
