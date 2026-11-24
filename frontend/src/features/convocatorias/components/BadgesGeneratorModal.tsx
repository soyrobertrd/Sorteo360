import { toast } from 'sonner'
import { getBrandPalette, hexToRgb } from '@/lib/branding'
import { useState } from 'react'
import { Download, Mail, QrCode, CheckCircle2 } from 'lucide-react'
import jsPDF from 'jspdf'
import { Modal } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import type { Invitado } from '../mockParticipants'

interface BadgesGeneratorModalProps {
  eventoNombre: string
  invitados: Invitado[]
  isOpen: boolean
  onClose: () => void
}

// Cada tipo de invitado toma un tono de la marca, en este orden; los que no están, el primero.
const TIPOS_CON_COLOR = [
  'Invitado General',
  'VIP',
  'Prensa',
  'Autoridad',
  'Patrocinador',
  'Expositor',
  'Staff',
  'Influencer',
]

function colorDelTipo(tipo: string): [number, number, number] {
  const paleta = getBrandPalette()
  const i = Math.max(0, TIPOS_CON_COLOR.indexOf(tipo))
  return hexToRgb(paleta[i % paleta.length])
}

export function BadgesGeneratorModal({
  eventoNombre,
  invitados,
  isOpen,
  onClose,
}: BadgesGeneratorModalProps) {
  const [generandoPdf, setGenerandoPdf] = useState(false)
  const [enviandoCorreos, setEnviandoCorreos] = useState(false)
  const [envioExitoso, setEnvioExitoso] = useState(false)

  // Generar PDF Masivo de Badges / Gafetes con QR (Formato Estándar A4 con múltiples badges por hoja o tamaño credencial)
  const handleDescargarBadgesPDF = () => {
    setGenerandoPdf(true)
    try {
      const doc = new jsPDF({
        orientation: 'portrait',
        unit: 'mm',
        format: 'a4',
      })

      // Dimensiones de Badge: 85mm x 120mm (2 columnas x 2 filas por hoja A4)
      const badgeW = 88
      const badgeH = 125
      const marginX = 12
      const marginY = 15
      const gapX = 10
      const gapY = 12

      invitados.forEach((inv, index) => {
        const itemOnPage = index % 4
        if (index > 0 && itemOnPage === 0) {
          doc.addPage()
        }

        const col = itemOnPage % 2
        const row = Math.floor(itemOnPage / 2)
        const x = marginX + col * (badgeW + gapX)
        const y = marginY + row * (badgeH + gapY)

        const header = colorDelTipo(inv.tipo)

        // Borde exterior y fondo del gafete
        doc.setDrawColor(203, 213, 225) // slate-300
        doc.setFillColor(255, 255, 255)
        doc.roundedRect(x, y, badgeW, badgeH, 4, 4, 'FD')

        // Cabecera Institucional Marca Blanca
        doc.setFillColor(15, 23, 42) // slate-900
        doc.rect(x, y, badgeW, 20, 'F')

        doc.setTextColor(255, 255, 255)
        doc.setFontSize(8)
        doc.setFont('helvetica', 'bold')
        doc.text('ACREDITACIÓN OFICIAL', x + badgeW / 2, y + 8, { align: 'center' })
        doc.setFontSize(6.5)
        doc.setFont('helvetica', 'normal')
        doc.text('CONTROL DE ACCESO & QR', x + badgeW / 2, y + 13, { align: 'center' })

        // Franja de Tipo de Participante
        doc.setFillColor(header[0], header[1], header[2])
        doc.rect(x, y + 20, badgeW, 8, 'F')
        doc.setTextColor(255, 255, 255)
        doc.setFontSize(8.5)
        doc.setFont('helvetica', 'bold')
        doc.text(inv.tipo.toUpperCase(), x + badgeW / 2, y + 25.5, { align: 'center' })

        // Nombre del Participante
        doc.setTextColor(15, 23, 42)
        doc.setFontSize(11)
        doc.setFont('helvetica', 'bold')
        const splitNombre = doc.splitTextToSize(inv.nombre, badgeW - 10)
        doc.text(splitNombre, x + badgeW / 2, y + 37, { align: 'center' })

        // Empresa / Cargo
        doc.setTextColor(71, 85, 105)
        doc.setFontSize(8.5)
        doc.setFont('helvetica', 'normal')
        const splitEmpresa = doc.splitTextToSize(inv.empresa || 'Invitado Oficial', badgeW - 10)
        doc.text(splitEmpresa, x + badgeW / 2, y + 46, { align: 'center' })

        // Marco del Código QR
        const qrSize = 38
        const qrX = x + (badgeW - qrSize) / 2
        const qrY = y + 54

        doc.setFillColor(248, 250, 252) // slate-50
        doc.setDrawColor(226, 232, 240)
        doc.roundedRect(qrX - 2, qrY - 2, qrSize + 4, qrSize + 4, 2, 2, 'FD')

        // Generar QR simulado / trazado vectorial estilizado
        doc.setFillColor(15, 23, 42)
        doc.rect(qrX + 2, qrY + 2, 8, 8, 'F')
        doc.rect(qrX + qrSize - 10, qrY + 2, 8, 8, 'F')
        doc.rect(qrX + 2, qrY + qrSize - 10, 8, 8, 'F')
        doc.rect(qrX + 13, qrY + 13, 12, 12, 'F')
        doc.rect(qrX + 15, qrY + 4, 4, 6, 'F')
        doc.rect(qrX + 27, qrY + 15, 6, 12, 'F')
        doc.rect(qrX + 5, qrY + 14, 4, 8, 'F')

        // Código de Acceso / ID
        doc.setFontSize(7)
        doc.setFont('courier', 'bold')
        doc.setTextColor(100, 116, 139)
        doc.text(
          inv.boleto ? `BOLETO ${inv.boleto}` : `ID: ${inv.id.toUpperCase()}`,
          x + badgeW / 2,
          y + 101,
          { align: 'center' },
        )

        // Pie del Badge con Nombre del Evento
        doc.setFontSize(6.5)
        doc.setFont('helvetica', 'italic')
        doc.setTextColor(148, 163, 184)
        const splitEvento = doc.splitTextToSize(eventoNombre, badgeW - 8)
        doc.text(splitEvento, x + badgeW / 2, y + 109, { align: 'center' })

        // Línea punteada de corte
        doc.setDrawColor(226, 232, 240)
        doc.setLineDashPattern([2, 2], 0)
        doc.rect(x - 1, y - 1, badgeW + 2, badgeH + 2)
        doc.setLineDashPattern([], 0)
      })

      doc.save(`Gafetes_Acceso_${eventoNombre.replace(/\s+/g, '_')}.pdf`)
    } catch (err) {
      console.error('Error generando badges PDF:', err)
      toast.error('Ocurrió un error al compilar los gafetes en PDF.')
    } finally {
      setGenerandoPdf(false)
    }
  }

  // Simulación de Envío de Pases Digitales con QR por Correo
  const handleEnviarBadgesCorreo = () => {
    setEnviandoCorreos(true)
    setTimeout(() => {
      setEnviandoCorreos(false)
      setEnvioExitoso(true)
      setTimeout(() => setEnvioExitoso(false), 5000)
    }, 1500)
  }

  if (!isOpen) return null

  return (
    <Modal title="Generación y Emisión de Badges / Gafetes con QR" onClose={onClose}>
      <div className="space-y-5 text-xs">
        {/* Cabecera Informativa */}
        <div className="bg-slate-50 dark:bg-slate-800/80 p-4 rounded-2xl border border-slate-200 dark:border-slate-700 flex items-start gap-3">
          <div className="w-10 h-10 rounded-xl bg-blue-600/10 dark:bg-blue-500/20 text-blue-600 dark:text-blue-400 flex items-center justify-center shrink-0">
            <QrCode className="w-5 h-5" />
          </div>
          <div className="space-y-1">
            <h4 className="font-bold text-slate-900 dark:text-white text-sm">
              Acreditación Oficial: {eventoNombre}
            </h4>
            <p className="text-slate-500 dark:text-slate-400 leading-relaxed">
              Genera planchas listas para imprimir en acreditación física o envía el pase digital
              con código QR directamente a la bandeja de entrada de los participantes.
            </p>
          </div>
        </div>

        {/* Resumen de Participantes Seleccionados */}
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-2.5 text-center">
          <div className="p-3 rounded-xl bg-slate-50 dark:bg-slate-800/50 border border-slate-200 dark:border-slate-700">
            <span className="text-[10px] font-bold uppercase text-slate-400 block">
              Total Gafetes
            </span>
            <span className="text-lg font-black text-slate-900 dark:text-white">
              {invitados.length}
            </span>
          </div>
          <div className="p-3 rounded-xl bg-blue-50 dark:bg-blue-950/40 border border-blue-200 dark:border-blue-900">
            <span className="text-[10px] font-bold uppercase text-blue-500 block">
              Compradores / Buyers
            </span>
            <span className="text-lg font-black text-blue-700 dark:text-blue-300">
              {invitados.filter((i) => i.tipo === 'Buyer').length}
            </span>
          </div>
          <div className="p-3 rounded-xl bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-900">
            <span className="text-[10px] font-bold uppercase text-emerald-500 block">
              Proveedores
            </span>
            <span className="text-lg font-black text-emerald-700 dark:text-emerald-300">
              {invitados.filter((i) => i.tipo === 'Supplier').length}
            </span>
          </div>
          <div className="p-3 rounded-xl bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-900">
            <span className="text-[10px] font-bold uppercase text-amber-500 block">
              VIP / Prensa
            </span>
            <span className="text-lg font-black text-amber-700 dark:text-amber-300">
              {invitados.filter((i) => i.tipo === 'VIP' || i.tipo === 'Prensa').length}
            </span>
          </div>
        </div>

        {/* Muestra Previa del Gafete */}
        <div className="p-4 rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-100 dark:bg-slate-950 flex flex-col items-center">
          <span className="text-[10px] font-bold uppercase tracking-wider text-slate-400 mb-3">
            Muestra del Diseño de Credencial / Badge
          </span>

          <div className="w-56 rounded-2xl overflow-hidden shadow-xl border border-slate-300 dark:border-slate-700 bg-white text-slate-900 flex flex-col items-center select-none">
            {/* Header Credencial */}
            <div className="w-full bg-slate-900 py-2.5 px-3 text-center text-white">
              <p className="text-[8px] font-bold uppercase tracking-wider">Acreditación Oficial</p>
              <p className="text-[6.5px] text-slate-400">Control de Acceso QR</p>
            </div>

            {/* Franja de Categoría */}
            <div className="w-full bg-blue-600 py-1 text-center text-white text-[9px] font-black uppercase tracking-wider">
              {invitados[0]?.tipo || 'Invitado VIP'}
            </div>

            {/* Datos */}
            <div className="p-3.5 text-center flex flex-col items-center space-y-2">
              <div>
                <p className="text-xs font-black text-slate-900">
                  {invitados[0]?.nombre || 'Nombre del Asistente'}
                </p>
                <p className="text-[10px] text-slate-500 font-medium">
                  {invitados[0]?.empresa || 'Empresa Internacional'}
                </p>
              </div>

              {/* QR Mock */}
              <div className="p-2 bg-slate-50 rounded-xl border border-slate-200">
                <QrCode className="w-16 h-16 text-slate-900" />
              </div>

              <p className="font-mono text-[9px] text-slate-400">
                ID: {invitados[0]?.id || 'EVT-INV-001'}
              </p>
            </div>
          </div>
        </div>

        {/* Alerta de Éxito al Enviar por Correo */}
        {envioExitoso && (
          <div className="p-3.5 rounded-xl bg-emerald-50 dark:bg-emerald-950/50 border border-emerald-200 dark:border-emerald-800 text-emerald-800 dark:text-emerald-300 flex items-center gap-2">
            <CheckCircle2 className="w-4 h-4 text-emerald-600 shrink-0" />
            <p className="text-xs">
              ¡Se han enviado <strong>{invitados.length} pases digitales con QR</strong> a las
              direcciones de correo corporativo registradas!
            </p>
          </div>
        )}

        {/* Botones de Acción */}
        <div className="flex flex-col sm:flex-row gap-2.5 pt-2 border-t border-slate-100 dark:border-slate-800">
          <Button
            type="button"
            onClick={handleDescargarBadgesPDF}
            disabled={generandoPdf || invitados.length === 0}
            className="flex-1 bg-blue-600 hover:bg-blue-700 text-white font-bold py-2.5 rounded-xl flex items-center justify-center gap-1.5 cursor-pointer shadow-md shadow-blue-600/20"
          >
            <Download className="w-4 h-4" />
            <span>{generandoPdf ? 'Generando PDF...' : 'Descargar Plancha PDF de Gafetes'}</span>
          </Button>

          <Button
            type="button"
            variant="outline"
            onClick={handleEnviarBadgesCorreo}
            disabled={enviandoCorreos || invitados.length === 0}
            className="flex-1 font-bold py-2.5 rounded-xl flex items-center justify-center gap-1.5 cursor-pointer"
          >
            <Mail className="w-4 h-4" />
            <span>
              {enviandoCorreos ? 'Despachando correos...' : 'Enviar Pase Digital por Correo'}
            </span>
          </Button>
        </div>
      </div>
    </Modal>
  )
}
