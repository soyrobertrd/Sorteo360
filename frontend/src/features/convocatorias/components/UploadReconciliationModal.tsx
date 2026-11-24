import { useState } from 'react'
import { FileSpreadsheet } from 'lucide-react'
import type { RecipientUploadResponse } from '@/api/types'
import { Button } from '@/components/ui/Button'
import { ErrorNote } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'

interface UploadReconciliationModalProps {
  /** La carga ya analizada por el servicio (`POST .../recipient-uploads`), sin confirmar. */
  upload: RecipientUploadResponse | null
  isConfirming: boolean
  error: unknown
  onClose: () => void
  onConfirm: () => void
}

export function UploadReconciliationModal({
  upload,
  isConfirming,
  error,
  onClose,
  onConfirm,
}: UploadReconciliationModalProps) {
  const [step, setStep] = useState<1 | 2>(1)

  if (!upload) return null
  const fileName = upload.originalFileName

  // Las seis categorías de la especificación 05-recipient-uploads.md, tal como las contó el servicio.
  const metrics = {
    matched: upload.counts.matched, // Coinciden con CRM
    unmatched: upload.counts.unmatched, // Fila limpia sin match en CRM (se crearán con consentSource verbal_upload)
    suppressed: upload.counts.suppressed, // Excluidos por supresión en CRM (opted_out / do_not_invite / no_email)
    alreadyRecipient: upload.counts.alreadyARecipient, // Ya eran destinatarios de esta campaña
    duplicateInFile: upload.counts.duplicateInFile, // Duplicados en el propio archivo
    malformed: upload.counts.malformed, // Correo inválido
  }

  const totalProcesados = upload.rowCount

  const destinatariosEfectivos = upload.confirmableCount

  return (
    <Modal
      open
      onClose={onClose}
      title={`Reconciliación y Previsualización de Subida — ${fileName}`}
      width="max-w-3xl"
    >
      <div className="space-y-4 text-xs">
        {/* Cabecera de Etapas */}
        <div className="flex items-center justify-between border-b border-slate-100 pb-3 dark:border-slate-800">
          <div className="flex items-center gap-2">
            <FileSpreadsheet size={20} className="text-emerald-600" />
            <div>
              <p className="font-extrabold text-slate-900 dark:text-white">{fileName}</p>
              <p className="text-[11px] text-slate-400">
                {totalProcesados} filas detectadas y parseadas en streaming UTF-8
              </p>
            </div>
          </div>
          <span className="rounded-full bg-blue-100 px-3 py-1 text-[11px] font-black text-blue-900 dark:bg-blue-950 dark:text-blue-300">
            {step === 1 ? 'Paso 1 de 2: Análisis & Buckets' : 'Paso 2 de 2: Confirmación'}
          </span>
        </div>

        {step === 1 && (
          <div className="space-y-4">
            <p className="text-slate-600 dark:text-slate-300 leading-relaxed">
              El archivo ha sido analizado. A continuación se desglosa el destino de cada fila de
              acuerdo con las reglas de reconciliación de CRM y supresión:
            </p>

            {/* Grid de Buckets */}
            <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
              <div className="rounded-2xl border border-emerald-200 bg-emerald-50/70 p-3 dark:border-emerald-900/40 dark:bg-emerald-950/30">
                <p className="text-[10px] font-extrabold text-emerald-800 uppercase tracking-wider dark:text-emerald-300">
                  Coinciden con CRM
                </p>
                <p className="text-2xl font-black text-emerald-700 dark:text-emerald-400 mt-1">
                  {metrics.matched}
                </p>
                <p className="text-[10px] text-emerald-600 dark:text-emerald-400 mt-0.5">
                  Se vinculan al contacto existente.
                </p>
              </div>

              <div className="rounded-2xl border border-blue-200 bg-blue-50/70 p-3 dark:border-blue-900/40 dark:bg-blue-950/30">
                <p className="text-[10px] font-extrabold text-blue-800 uppercase tracking-wider dark:text-blue-300">
                  Nuevos sin match
                </p>
                <p className="text-2xl font-black text-blue-700 dark:text-blue-400 mt-1">
                  {metrics.unmatched}
                </p>
                <p className="text-[10px] text-blue-600 dark:text-blue-400 mt-0.5">
                  Se crearán con consentSource: verbal_upload.
                </p>
              </div>

              <div className="rounded-2xl border border-rose-200 bg-rose-50/70 p-3 dark:border-rose-900/40 dark:bg-rose-950/30">
                <p className="text-[10px] font-extrabold text-rose-800 uppercase tracking-wider dark:text-rose-300">
                  Excluidos Supresión
                </p>
                <p className="text-2xl font-black text-rose-700 dark:text-rose-400 mt-1">
                  {metrics.suppressed}
                </p>
                <p className="text-[10px] text-rose-600 dark:text-rose-400 mt-0.5">
                  Dados de baja / No invitar. Van a exclusión.
                </p>
              </div>

              <div className="rounded-2xl border border-slate-200 bg-slate-50 p-3 dark:border-slate-800 dark:bg-slate-900">
                <p className="text-[10px] font-extrabold text-slate-500 uppercase tracking-wider">
                  Ya en la campaña
                </p>
                <p className="text-xl font-bold text-slate-700 dark:text-slate-300 mt-1">
                  {metrics.alreadyRecipient}
                </p>
                <p className="text-[10px] text-slate-400 mt-0.5">Ignorados sin duplicar.</p>
              </div>

              <div className="rounded-2xl border border-slate-200 bg-slate-50 p-3 dark:border-slate-800 dark:bg-slate-900">
                <p className="text-[10px] font-extrabold text-slate-500 uppercase tracking-wider">
                  Duplicados en archivo
                </p>
                <p className="text-xl font-bold text-slate-700 dark:text-slate-300 mt-1">
                  {metrics.duplicateInFile}
                </p>
                <p className="text-[10px] text-slate-400 mt-0.5">Se toma solo la 1ra aparición.</p>
              </div>

              <div className="rounded-2xl border border-amber-200 bg-amber-50/70 p-3 dark:border-amber-900/40 dark:bg-amber-950/30">
                <p className="text-[10px] font-extrabold text-amber-800 uppercase tracking-wider dark:text-amber-300">
                  Mal formados
                </p>
                <p className="text-xl font-bold text-amber-700 dark:text-amber-400 mt-1">
                  {metrics.malformed}
                </p>
                <p className="text-[10px] text-amber-600 dark:text-amber-400 mt-0.5">
                  Sintaxis inválida.
                </p>
              </div>
            </div>

            <div className="flex justify-between pt-3 border-t border-slate-100 dark:border-slate-800">
              <Button type="button" variant="outline" onClick={onClose} className="cursor-pointer">
                Cancelar
              </Button>
              <Button
                type="button"
                onClick={() => setStep(2)}
                className="bg-brand-deep text-white font-bold cursor-pointer"
              >
                Revisar Resumen y Confirmar &rarr;
              </Button>
            </div>
          </div>
        )}

        {step === 2 && (
          <div className="space-y-4">
            {error != null && <ErrorNote error={error} />}
            <div className="rounded-2xl border border-blue-200 bg-blue-50/60 p-4 dark:border-blue-900/40 dark:bg-blue-950/40 text-blue-900 dark:text-blue-300 space-y-2">
              <h4 className="font-extrabold text-sm">Resumen Final de Confirmación</h4>
              <p className="leading-relaxed">
                Al hacer clic en <strong>Confirmar Importación</strong>:
              </p>
              <ul className="list-disc list-inside space-y-1 text-[11px] font-medium">
                <li>
                  Se crearán <strong>{metrics.unmatched}</strong> contactos nuevos en el CRM stamped
                  con `consentSource: verbal_upload`.
                </li>
                <li>
                  Se incorporarán <strong>{destinatariosEfectivos}</strong> destinatarios a la lista
                  de esta campaña.
                </li>
                <li>
                  Los <strong>{metrics.suppressed}</strong> contactos suprimidos quedarán
                  registrados en la tabla de exclusiones.
                </li>
              </ul>
            </div>

            <div className="flex justify-between pt-3 border-t border-slate-100 dark:border-slate-800">
              <Button
                type="button"
                variant="outline"
                onClick={() => setStep(1)}
                className="cursor-pointer"
              >
                &larr; Volver
              </Button>
              <Button
                type="button"
                onClick={onConfirm}
                disabled={isConfirming}
                className="bg-emerald-700 hover:bg-emerald-800 text-white font-bold cursor-pointer"
              >
                {isConfirming
                  ? 'Importando y vinculando…'
                  : `Confirmar Importación (${destinatariosEfectivos} Destinatarios)`}
              </Button>
            </div>
          </div>
        )}
      </div>
    </Modal>
  )
}
