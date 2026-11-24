import { useEffect, useState } from 'react'
import { FileDropzone } from '@/components/ui/FileDropzone'
import { Building2, CheckCircle2, RotateCcw } from 'lucide-react'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { Field, TextInput } from '@/components/ui/Field'
import { setRaffleBranding, useBranding } from '@/lib/branding'

const MAX_LOGO_BYTES = 2 * 1024 * 1024

interface PersonalizacionMarcaCardProps {
  eventId: string
  canEdit: boolean
}

/**
 * Marca personalizada por sorteo (nombre de empresa y logo propio) para distinguir
 * eventos de clientes sin alterar la marca blanca global.
 */
export function PersonalizacionMarcaCard({ eventId, canEdit }: PersonalizacionMarcaCardProps) {
  const branding = useBranding(eventId)
  const [companyName, setCompanyName] = useState(
    branding.hasCustomEventBranding ? branding.companyName : '',
  )
  const [logoUrl, setLogoUrl] = useState(branding.hasCustomEventBranding ? branding.logoUrl : '')
  const [fileError, setFileError] = useState<string | null>(null)
  const [savedMsg, setSavedMsg] = useState<string | null>(null)

  useEffect(() => {
    setCompanyName(branding.hasCustomEventBranding ? branding.companyName : '')
    setLogoUrl(branding.hasCustomEventBranding ? branding.logoUrl : '')
  }, [branding.hasCustomEventBranding, branding.companyName, branding.logoUrl])

  const handleLogoFile = (file: File) => {
    if (file.size > MAX_LOGO_BYTES) {
      setFileError('El archivo del logo excede el máximo permitido de 2 MB.')
      return
    }
    setFileError(null)
    const reader = new FileReader()
    reader.onload = () => {
      if (typeof reader.result === 'string') {
        setLogoUrl(reader.result)
      }
    }
    reader.readAsDataURL(file)
  }

  const handleSave = () => {
    setFileError(null)
    const cleanCompany = companyName.trim()
    const cleanLogo = logoUrl.trim()
    if (!cleanCompany && !cleanLogo) {
      setRaffleBranding(eventId, {})
      setSavedMsg('Se restauró la marca global para este sorteo.')
      return
    }
    setRaffleBranding(eventId, {
      companyName: cleanCompany || undefined,
      logoUrl: cleanLogo || undefined,
    })
    setSavedMsg('Marca personalizada guardada para este sorteo.')
  }

  const handleRestoreGlobal = () => {
    setFileError(null)
    setRaffleBranding(eventId, {})
    setCompanyName('')
    setLogoUrl('')
    setSavedMsg('Marca restaurada a la configuración global.')
  }

  const previewLogo = logoUrl.trim() || branding.logoUrl

  return (
    <Card className="p-6 bg-white dark:bg-card border border-slate-200 dark:border-slate-800 space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2 border-b border-slate-100 dark:border-slate-800 pb-3">
        <div className="flex items-center gap-2">
          <Building2 className="w-5 h-5 text-blue-600 dark:text-blue-400" />
          <div>
            <h3 className="text-base font-bold text-slate-900 dark:text-white">
              Personalización / Marca del Sorteo (Logo y Empresa)
            </h3>
            <p className="text-xs text-slate-500">
              Si este sorteo corresponde a un cliente específico, su nombre y logo aparecerán en el
              proyector, encabezado y certificados.
            </p>
          </div>
        </div>

        {branding.hasCustomEventBranding && (
          <span className="px-2.5 py-1 rounded-full text-[11px] font-bold bg-blue-50 text-blue-700 border border-blue-200 dark:bg-blue-950/40 dark:text-blue-300 dark:border-blue-800">
            Marca personalizada activa
          </span>
        )}
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4 items-start">
        <div className="md:col-span-2 space-y-3">
          <Field label="Nombre de la empresa / cliente">
            <TextInput
              value={companyName}
              onChange={(e) => {
                setSavedMsg(null)
                setCompanyName(e.target.value)
              }}
              disabled={!canEdit}
              placeholder={branding.organizationName}
            />
          </Field>

          <Field
            label="URL o archivo de logo propio"
            hint="Acepta URL HTTPS, ruta relativa o carga local PNG/SVG/JPG (máx. 2 MB)"
          >
            <div className="flex flex-col sm:flex-row gap-2">
              <TextInput
                aria-label="Enlace del logo"
                value={logoUrl}
                onChange={(e) => {
                  setSavedMsg(null)
                  setLogoUrl(e.target.value)
                }}
                disabled={!canEdit}
                placeholder="/logo-brand.svg o https://..."
                className="flex-1"
              />
              <FileDropzone
                compact
                accept="image/*"
                disabled={!canEdit}
                onFile={handleLogoFile}
                title="Logo"
                hint="PNG, JPG o SVG"
              />
            </div>
          </Field>
        </div>

        <div className="rounded-xl border border-slate-200 dark:border-slate-800 bg-slate-50 dark:bg-slate-900/50 p-4 flex flex-col items-center justify-center text-center gap-2 min-h-[120px]">
          <img
            src={previewLogo}
            alt={companyName.trim() || branding.companyName}
            className="max-h-14 max-w-[160px] object-contain"
          />
          <div className="text-xs font-bold text-slate-800 dark:text-slate-200">
            {companyName.trim() || branding.companyName}
          </div>
          <div className="text-[10px] text-slate-400 uppercase tracking-wider">Vista previa</div>
        </div>
      </div>

      {fileError && <p className="text-xs font-semibold text-rose-600">{fileError}</p>}
      {savedMsg && (
        <p className="flex items-center gap-1.5 text-xs font-semibold text-emerald-700 dark:text-emerald-400">
          <CheckCircle2 className="w-3.5 h-3.5" />
          {savedMsg}
        </p>
      )}

      {canEdit && (
        <div className="flex flex-wrap items-center justify-end gap-2 pt-1">
          {branding.hasCustomEventBranding && (
            <Button
              type="button"
              size="sm"
              variant="outline"
              onClick={handleRestoreGlobal}
              className="text-xs font-bold cursor-pointer"
            >
              <RotateCcw className="w-3.5 h-3.5 mr-1.5" />
              Restaurar global
            </Button>
          )}
          <Button
            type="button"
            size="sm"
            onClick={handleSave}
            className="text-xs font-bold bg-blue-600 hover:bg-blue-700 text-white cursor-pointer"
          >
            Guardar marca del sorteo
          </Button>
        </div>
      )}
    </Card>
  )
}
