import { useRef, useState } from 'react'
import { Award, Download, Printer } from 'lucide-react'
import { Modal } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { Field, TextInput } from '@/components/ui/Field'
import { jsPDF } from 'jspdf'
import type { CertificateSigner, PrizeDrawSummary, PrizeResponse } from '@/api/types'
import { useBranding, getBrandColors, hexToRgb } from '@/lib/branding'
import {
  CERTIFICATE_SIGNER_MAX_LENGTH,
  firmantesDelCertificado,
  type Firmante,
} from '../certificateSigners'

interface CertificadoPremioModalProps {
  draw: PrizeDrawSummary
  premio: PrizeResponse
  /** Los firmantes guardados en el sorteo, de los que parte el certificado. */
  signers: CertificateSigner[]
  sorteoNombre: string
  eventId?: string
  onClose: () => void
}

/**
 * El recuerdo del ganador, para la foto: decorativo, no es evidencia y no da fe de nada. Sin sello,
 * sin enlace de validación y sin el código de canje, que es del correo del ganador y no de un PDF
 * que se publica en redes (spec raffles/03 §6). Una línea sin nombre queda en blanco para firmar a mano.
 */
export function CertificadoPremioModal({
  draw,
  premio,
  signers,
  sorteoNombre,
  eventId,
  onClose,
}: CertificadoPremioModalProps) {
  const branding = useBranding(eventId)
  const printRef = useRef<HTMLDivElement | null>(null)
  const ganador = { nombre: draw.winnerName, empresa: draw.winnerCompany }
  const [firmantes, setFirmantes] = useState<Firmante[]>(() => firmantesDelCertificado(signers))
  const cambiarFirmante = (i: number, cambio: Partial<Firmante>) =>
    setFirmantes((actual) => actual.map((f, j) => (j === i ? { ...f, ...cambio } : f)))

  const handlePrint = () => {
    window.print()
  }

  const handleDownloadPDF = () => {
    const doc = new jsPDF({
      orientation: 'landscape',
      unit: 'mm',
      format: 'a4',
    })

    // El color principal es el de la marca; los grises y el dorado son de imprenta, fijos.
    const primaryColor = hexToRgb(getBrandColors().primary)
    const goldColor: [number, number, number] = [202, 138, 4]
    const slateDark: [number, number, number] = [30, 41, 59]
    const slateLight: [number, number, number] = [100, 116, 139]

    // Marco exterior con doble filete decorativo dorado
    doc.setDrawColor(primaryColor[0], primaryColor[1], primaryColor[2])
    doc.setLineWidth(1.5)
    doc.rect(10, 10, 277, 190)

    doc.setDrawColor(goldColor[0], goldColor[1], goldColor[2])
    doc.setLineWidth(0.6)
    doc.rect(12.5, 12.5, 272, 185)

    // Cabecera de Marca Blanca / Empresa Anfitriona
    doc.setFont('helvetica', 'bold')
    doc.setFontSize(9)
    doc.setTextColor(slateLight[0], slateLight[1], slateLight[2])
    doc.text('RECONOCIMIENTO OFICIAL', 148.5, 25, { align: 'center' })

    doc.setFontSize(18)
    doc.setTextColor(primaryColor[0], primaryColor[1], primaryColor[2])
    doc.text(branding.companyName.toUpperCase(), 148.5, 33, { align: 'center' })

    doc.setFontSize(10)
    doc.setFont('helvetica', 'normal')
    doc.setTextColor(slateLight[0], slateLight[1], slateLight[2])
    doc.text(`${branding.appName} · ${branding.appSubtitle}`.toUpperCase(), 148.5, 39, {
      align: 'center',
    })

    // Título Principal
    doc.setFont('helvetica', 'bold')
    doc.setFontSize(22)
    doc.setTextColor(goldColor[0], goldColor[1], goldColor[2])
    doc.text('CERTIFICADO OFICIAL DE PREMIO', 148.5, 54, { align: 'center' })

    // Otorgado a
    doc.setFont('helvetica', 'normal')
    doc.setFontSize(11)
    doc.setTextColor(slateLight[0], slateLight[1], slateLight[2])
    doc.text(`${branding.companyName} hace constar que:`, 148.5, 66, { align: 'center' })

    // Nombre del Ganador
    doc.setFont('helvetica', 'bold')
    doc.setFontSize(20)
    doc.setTextColor(primaryColor[0], primaryColor[1], primaryColor[2])
    doc.text(ganador.nombre.toUpperCase(), 148.5, 78, { align: 'center' })

    // Empresa / Departamento
    doc.setFont('helvetica', 'normal')
    doc.setFontSize(11)
    doc.setTextColor(slateDark[0], slateDark[1], slateDark[2])
    // Sin departamento no se imprime la línea: la empresa que entrega el premio ya va arriba.
    if (ganador.empresa) {
      doc.text(`Área / Departamento: ${ganador.empresa}`, 148.5, 85, { align: 'center' })
    }

    // Descripción del Premio
    doc.setFontSize(11)
    doc.text(`Ha resultado acreedor(a) del premio en "${sorteoNombre}":`, 148.5, 96, {
      align: 'center',
    })

    // Caja destacada del premio
    doc.setFillColor(248, 250, 252)
    doc.setDrawColor(primaryColor[0], primaryColor[1], primaryColor[2])
    doc.setLineWidth(0.8)
    doc.roundedRect(48.5, 102, 200, 22, 3, 3, 'FD')

    doc.setFont('helvetica', 'bold')
    doc.setFontSize(14)
    doc.setTextColor(primaryColor[0], primaryColor[1], primaryColor[2])
    doc.text(premio.name, 148.5, 112, { align: 'center' })

    if (premio.value) {
      doc.setFont('helvetica', 'normal')
      doc.setFontSize(9)
      doc.setTextColor(slateLight[0], slateLight[1], slateLight[2])
      doc.text(`Valor Estimado: ${premio.value}`, 148.5, 119, { align: 'center' })
    }

    // Firmas
    const firmasY = 148
    doc.setDrawColor(148, 163, 184)
    doc.setLineWidth(0.4)

    firmantes.forEach((firmante, i) => {
      const centro = i === 0 ? 77.5 : 219.5
      doc.line(centro - 32.5, firmasY, centro + 32.5, firmasY)
      doc.setFont('helvetica', 'bold')
      doc.setFontSize(8.5)
      doc.setTextColor(slateDark[0], slateDark[1], slateDark[2])
      doc.text(firmante.nombre.trim(), centro, firmasY + 5, { align: 'center' })
      doc.setFont('helvetica', 'normal')
      doc.setFontSize(7)
      doc.setTextColor(slateLight[0], slateLight[1], slateLight[2])
      doc.text(firmante.cargo.trim(), centro, firmasY + 9, { align: 'center' })
    })

    doc.save(`Certificado_Premio_${ganador.nombre.replace(/[^a-zA-Z0-9]/g, '_')}.pdf`)
  }

  return (
    <Modal title={`Certificado Oficial de Premio · ${branding.companyName}`} onClose={onClose} wide>
      <div className="space-y-6">
        {/* Acciones de cabecera */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 bg-amber-50 dark:bg-amber-950/30 p-3.5 rounded-xl border border-amber-200 dark:border-amber-800">
          <div className="flex items-center gap-2.5 text-xs text-amber-900 dark:text-amber-200 font-semibold">
            <Award className="w-5 h-5 text-amber-600 dark:text-amber-400 shrink-0" />
            <div>
              <p className="font-bold">Certificado Oficial Imprimible de Premio Ganador</p>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <Button
              size="sm"
              onClick={handleDownloadPDF}
              className="bg-amber-600 hover:bg-amber-700 text-white text-xs font-bold cursor-pointer"
            >
              <Download className="w-3.5 h-3.5 mr-1.5" />
              Descargar Certificado PDF
            </Button>
            <Button
              size="sm"
              variant="outline"
              onClick={handlePrint}
              className="text-xs font-bold cursor-pointer"
            >
              <Printer className="w-3.5 h-3.5 mr-1.5" />
              Imprimir
            </Button>
          </div>
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          {(['primer', 'segundo'] as const).map((orden, i) => (
            <div key={orden} className="space-y-2">
              <Field label={`Nombre del ${orden} firmante`}>
                <TextInput
                  value={firmantes[i].nombre}
                  maxLength={CERTIFICATE_SIGNER_MAX_LENGTH}
                  onChange={(e) => cambiarFirmante(i, { nombre: e.target.value })}
                  placeholder="Nombre y apellido"
                />
              </Field>
              <Field label={`Cargo del ${orden} firmante`}>
                <TextInput
                  value={firmantes[i].cargo}
                  maxLength={CERTIFICATE_SIGNER_MAX_LENGTH}
                  onChange={(e) => cambiarFirmante(i, { cargo: e.target.value })}
                  placeholder="Ej: Directora de Talento Humano"
                />
              </Field>
            </div>
          ))}
        </div>

        {/* Vista previa del Certificado */}
        <div
          ref={printRef}
          className="bg-white text-slate-900 p-8 sm:p-12 rounded-2xl border-4 border-brand-deep shadow-sm relative overflow-hidden"
        >
          <div className="border-2 border-amber-500 p-6 sm:p-8 rounded-xl text-center relative space-y-6">
            <div>
              {branding.eventLogoUrl && (
                <img
                  src={branding.eventLogoUrl}
                  alt={branding.companyName}
                  className="mx-auto mb-2 h-10 w-auto object-contain"
                />
              )}
              <span className="text-xs tracking-[0.25em] uppercase font-bold text-slate-500 block mb-1">
                {sorteoNombre}
              </span>
              <h2 className="text-xl sm:text-2xl font-black uppercase text-brand-deep tracking-tight">
                {branding.companyName}
              </h2>
              <p className="text-xs text-slate-500 mt-0.5">
                {branding.appName} · {branding.appSubtitle}
              </p>
            </div>

            <div>
              <span className="text-xs font-bold text-amber-600 uppercase tracking-widest block mb-1">
                Certificado Oficial de Premio
              </span>
              <p className="text-xs text-slate-600">
                Se otorga el presente reconocimiento y constancia a:
              </p>
              <h3 className="text-2xl sm:text-3xl font-black text-brand-deep tracking-tight my-2">
                {ganador.nombre}
              </h3>
              {ganador.empresa && (
                <p className="text-xs font-semibold text-slate-600">{ganador.empresa}</p>
              )}
            </div>

            <div className="max-w-md mx-auto p-4 bg-slate-50 border border-slate-200 rounded-xl">
              <span className="text-[11px] font-bold text-slate-500 block uppercase">
                Premio Ganado
              </span>
              <p className="text-lg font-black text-brand-deep mt-0.5">{premio.name}</p>
              {premio.value && <p className="text-xs text-slate-600 mt-1">Valor: {premio.value}</p>}
            </div>

            <div className="grid grid-cols-2 gap-8 pt-8 max-w-lg mx-auto text-center text-xs">
              {firmantes.map((firmante, i) => (
                <div key={i} className="space-y-1">
                  <div className="border-b border-slate-300 pb-8" />
                  <p className="min-h-4 font-bold text-slate-800">{firmante.nombre}</p>
                  <p className="min-h-3.5 text-[10px] text-slate-400">{firmante.cargo}</p>
                </div>
              ))}
            </div>
          </div>
        </div>

        <div className="flex justify-end gap-2 pt-2 border-t border-slate-200 dark:border-slate-800">
          <Button variant="outline" onClick={onClose}>
            Cerrar
          </Button>
          <Button
            onClick={handleDownloadPDF}
            className="bg-amber-600 hover:bg-amber-700 text-white font-bold cursor-pointer"
          >
            <Download className="w-4 h-4 mr-1.5" />
            Descargar PDF
          </Button>
        </div>
      </div>
    </Modal>
  )
}
