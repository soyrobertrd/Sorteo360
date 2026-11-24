import { useState } from 'react'
import { PenLine } from 'lucide-react'
import type { CertificateSigner } from '@/api/types'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Field, TextInput } from '@/components/ui/Field'
import { ErrorNote } from '@/components/ui/Feedback'
import { fieldError } from '@/lib/fieldErrors'
import {
  CERTIFICATE_SIGNER_MAX_LENGTH,
  firmantesDelCertificado,
  lineaEnBlanco,
  toSetCertificateSignersRequest,
  type Firmante,
} from '../certificateSigners'
import { useSetCertificateSigners } from '../hooks'

interface FirmantesCertificadoCardProps {
  eventId: string
  signers: CertificateSigner[]
  disabled: boolean
  title?: string
}

const ORDEN = ['primer', 'segundo'] as const

/**
 * Los firmantes que el certificado de premio propone por omisión. No atestiguan nada, así que se
 * cambian en cualquier estado del sorteo, completado incluido. Un firmante es nombre y cargo, así
 * que se guardan juntos con un botón y no al salir de cada campo.
 */
export function FirmantesCertificadoCard({
  eventId,
  signers,
  disabled,
  title,
}: FirmantesCertificadoCardProps) {
  const [firmantes, setFirmantes] = useState<Firmante[]>(() => firmantesDelCertificado(signers))
  const guardar = useSetCertificateSigners(eventId)

  const cambiar = (i: number, cambio: Partial<Firmante>) => {
    guardar.reset()
    setFirmantes((actual) => actual.map((f, j) => (j === i ? { ...f, ...cambio } : f)))
  }

  // Una línea en blanco no viaja, así que el servicio numera solo las que se mandaron: el error de
  // su posición en el cuerpo es el de esta línea en pantalla.
  const enviadas = firmantes.flatMap((f, i) => (lineaEnBlanco(f) ? [] : [i]))
  const errorDe = (i: number, campo: 'Name' | 'Title') => {
    const posicion = enviadas.indexOf(i)
    return posicion < 0 ? undefined : fieldError(guardar.error, `Signers[${posicion}].${campo}`)
  }

  return (
    <Card
      role="region"
      aria-label="Firmantes del certificado"
      className="p-6 bg-white dark:bg-card border border-slate-200 dark:border-slate-800 space-y-4"
    >
      <div className="flex items-center gap-2 border-b border-slate-100 dark:border-slate-800 pb-3">
        <PenLine className="w-5 h-5 text-blue-600 dark:text-blue-400" />
        <h3 className="text-base font-bold text-slate-900 dark:text-white">
          Firmantes del certificado
        </h3>
      </div>
      <p className="text-xs text-slate-500">
        Los nombres y cargos que el certificado de premio propone en sus dos líneas de firma. Se
        pueden cambiar en el certificado antes de descargarlo, y lo que se imprime no se guarda.
      </p>

      {guardar.error != null && <ErrorNote error={guardar.error} />}

      <div className="grid gap-4 sm:grid-cols-2" title={title}>
        {ORDEN.map((orden, i) => (
          <div key={orden} className="space-y-2">
            <Field label={`Nombre del ${orden} firmante`} error={errorDe(i, 'Name')}>
              <TextInput
                value={firmantes[i].nombre}
                maxLength={CERTIFICATE_SIGNER_MAX_LENGTH}
                disabled={disabled}
                onChange={(e) => cambiar(i, { nombre: e.target.value })}
                placeholder="Nombre y apellido"
              />
            </Field>
            <Field label={`Cargo del ${orden} firmante`} error={errorDe(i, 'Title')}>
              <TextInput
                value={firmantes[i].cargo}
                maxLength={CERTIFICATE_SIGNER_MAX_LENGTH}
                disabled={disabled}
                onChange={(e) => cambiar(i, { cargo: e.target.value })}
                placeholder="Ej: Directora de Talento Humano"
              />
            </Field>
          </div>
        ))}
      </div>

      <div className="flex items-center justify-end gap-3">
        {guardar.isSuccess && (
          <span className="text-xs font-medium text-emerald-600 dark:text-emerald-400">
            Firmantes guardados.
          </span>
        )}
        <Button
          type="button"
          size="sm"
          disabled={disabled || guardar.isPending}
          title={title}
          onClick={() => {
            const pedido = toSetCertificateSignersRequest(firmantes)
            // Lo guardado, recortado y sin huecos: un único firmante en la segunda línea pasa a la
            // primera, que es donde el certificado lo imprime
            guardar.mutate(pedido, {
              onSuccess: () => setFirmantes(firmantesDelCertificado(pedido.signers)),
            })
          }}
        >
          Guardar firmantes
        </Button>
      </div>
    </Card>
  )
}
