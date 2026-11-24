import { useState } from 'react'
import {
  Check,
  Copy,
  Globe,
  Play,
  RefreshCw,
  Webhook,
  QrCode,
  CheckCircle2,
  AlertTriangle,
} from 'lucide-react'
import { Modal } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { Field, TextArea, TextInput, Select } from '@/components/ui/Field'
import { ErrorNote } from '@/components/ui/Feedback'
import { config } from '@/lib/config'
import type { RaffleParticipantInput } from '@/api/types'
import { useImportRaffleParticipants, useSetRaffleParticipantCheckIn } from '../hooks'

interface ConexionApiWebhookModalProps {
  eventId: string
  onClose: () => void
  onSynced?: (message: string) => void
}

type IntegrationTab = 'api' | 'webhook' | 'checkin'

interface ApiConnectionConfig {
  endpointUrl: string
  method: 'GET' | 'POST'
  authHeader: string
  dataPath: string
  markCheckedIn: boolean
  webhookSecret: string
}

const STORAGE_PREFIX = 'sorteo360.raffle.integrations.'

function readIntegrationConfig(eventId: string): ApiConnectionConfig {
  try {
    const raw = localStorage.getItem(`${STORAGE_PREFIX}${eventId}`)
    if (raw) return JSON.parse(raw) as ApiConnectionConfig
  } catch {
    // Fallback por defecto
  }
  return {
    endpointUrl: '',
    method: 'GET',
    authHeader: '',
    dataPath: '',
    markCheckedIn: true,
    webhookSecret: `whsec_${eventId.replace(/[^a-zA-Z0-9]/g, '').slice(0, 12) || 'sorteo360'}`,
  }
}

function saveIntegrationConfig(eventId: string, cfg: ApiConnectionConfig) {
  try {
    localStorage.setItem(`${STORAGE_PREFIX}${eventId}`, JSON.stringify(cfg))
  } catch {
    // Ignorar error de almacenamiento
  }
}

function extractByPath(payload: unknown, path: string): unknown {
  if (!path.trim()) return payload
  const parts = path
    .split('.')
    .map((p) => p.trim())
    .filter(Boolean)
  let current: unknown = payload
  for (const part of parts) {
    if (current && typeof current === 'object' && part in (current as Record<string, unknown>)) {
      current = (current as Record<string, unknown>)[part]
    } else {
      return undefined
    }
  }
  return current
}

function normalizeRecords(raw: unknown): RaffleParticipantInput[] {
  const candidates = Array.isArray(raw)
    ? raw
    : raw && typeof raw === 'object'
      ? ((raw as Record<string, unknown>).participants ??
        (raw as Record<string, unknown>).data ??
        (raw as Record<string, unknown>).items ??
        (raw as Record<string, unknown>).employees ??
        (raw as Record<string, unknown>).results)
      : null

  if (!Array.isArray(candidates)) return []

  return candidates
    .map((item): RaffleParticipantInput | null => {
      if (!item || typeof item !== 'object') return null
      const r = item as Record<string, unknown>
      const fullName = String(
        r.fullName ??
          r.name ??
          r.nombre ??
          ([r.firstName, r.lastName].filter(Boolean).join(' ') || ''),
      ).trim()
      if (!fullName) return null
      return {
        fullName,
        email: r.email ? String(r.email).trim() : r.correo ? String(r.correo).trim() : undefined,
        company: r.company
          ? String(r.company).trim()
          : r.department
            ? String(r.department).trim()
            : r.departamento
              ? String(r.departamento).trim()
              : undefined,
        department: r.department
          ? String(r.department).trim()
          : r.departamento
            ? String(r.departamento).trim()
            : undefined,
        employeeCode: r.employeeCode
          ? String(r.employeeCode).trim()
          : r.codigo
            ? String(r.codigo).trim()
            : r.id
              ? String(r.id).trim()
              : undefined,
        phone: r.phone
          ? String(r.phone).trim()
          : r.telefono
            ? String(r.telefono).trim()
            : undefined,
        qrCode: r.qrCode ? String(r.qrCode).trim() : r.qr ? String(r.qr).trim() : undefined,
        checkedIn: typeof r.checkedIn === 'boolean' ? r.checkedIn : undefined,
      }
    })
    .filter((x): x is RaffleParticipantInput => x !== null)
}

const SAMPLE_WEBHOOK_PAYLOAD = JSON.stringify(
  {
    source: 'rrhh-webhook',
    markCheckedIn: true,
    participants: [
      {
        fullName: 'Sofía Vargas',
        email: 'sofia.vargas@empresa.com',
        department: 'Mercadeo',
        employeeCode: 'EMP-901',
      },
      {
        fullName: 'Miguel Ángel Torres',
        email: 'miguel.torres@empresa.com',
        department: 'Contabilidad',
        employeeCode: 'EMP-902',
      },
    ],
  },
  null,
  2,
)

const SAMPLE_CHECKIN_WEBHOOK = JSON.stringify(
  {
    qrCode: 'QR-NAV-2026-0001',
    checkedIn: true,
  },
  null,
  2,
)

export function ConexionApiWebhookModal({
  eventId,
  onClose,
  onSynced,
}: ConexionApiWebhookModalProps) {
  const [tab, setTab] = useState<IntegrationTab>('api')
  const [cfg, setCfg] = useState<ApiConnectionConfig>(() => readIntegrationConfig(eventId))
  const [statusMessage, setStatusMessage] = useState<{ tone: 'ok' | 'err'; text: string } | null>(
    null,
  )
  const [fetchingApi, setFetchingApi] = useState(false)
  const [webhookBody, setWebhookBody] = useState(SAMPLE_WEBHOOK_PAYLOAD)
  const [checkinBody, setCheckinBody] = useState(SAMPLE_CHECKIN_WEBHOOK)
  const [copiedKey, setCopiedKey] = useState<string | null>(null)

  const importMutation = useImportRaffleParticipants(eventId)
  const checkInMutation = useSetRaffleParticipantCheckIn(eventId)

  const apiBase =
    config.eventosApiUrl ||
    (typeof window !== 'undefined' ? window.location.origin : 'http://localhost:5001')
  const participantsWebhookUrl = `${apiBase}/v1/events/${eventId}/raffle/webhooks/participants`
  const checkinWebhookUrl = `${apiBase}/v1/events/${eventId}/raffle/webhooks/check-in`

  const updateCfg = (patch: Partial<ApiConnectionConfig>) => {
    const next = { ...cfg, ...patch }
    setCfg(next)
    saveIntegrationConfig(eventId, next)
  }

  const copyText = (key: string, text: string) => {
    navigator.clipboard.writeText(text)
    setCopiedKey(key)
    setTimeout(() => setCopiedKey(null), 2000)
  }

  const handleSyncFromApi = async () => {
    setStatusMessage(null)
    if (!cfg.endpointUrl.trim()) {
      setStatusMessage({
        tone: 'err',
        text: 'Ingresa la URL de tu API externa o utiliza "Probar con datos demo HR API".',
      })
      return
    }
    setFetchingApi(true)
    try {
      const headers: Record<string, string> = { Accept: 'application/json' }
      if (cfg.authHeader.trim()) {
        headers.Authorization = cfg.authHeader.trim()
      }
      const res = await fetch(cfg.endpointUrl.trim(), {
        method: cfg.method,
        headers,
      })
      if (!res.ok) {
        throw new Error(`El endpoint devolvió HTTP ${res.status} (${res.statusText}).`)
      }
      const json: unknown = await res.json()
      const root = extractByPath(json, cfg.dataPath)
      const records = normalizeRecords(root)
      if (records.length === 0) {
        throw new Error(
          'No se encontraron registros válidos en la respuesta JSON (revisa la ruta de datos o campos nombre/name).',
        )
      }
      const imported = await importMutation.mutateAsync({
        participants: records,
        markCheckedIn: cfg.markCheckedIn,
        source: 'api',
      })
      const msg = `API sincronizada: ${imported.imported} nuevos, ${imported.updated} actualizados (Total: ${imported.total}).`
      setStatusMessage({ tone: 'ok', text: msg })
      onSynced?.(msg)
    } catch (err) {
      setStatusMessage({
        tone: 'err',
        text:
          err instanceof Error
            ? err.message
            : 'No se pudo consultar la API externa (verifica CORS o la URL).',
      })
    } finally {
      setFetchingApi(false)
    }
  }

  const handleDemoApiImport = async () => {
    setStatusMessage(null)
    const demoEmployees: RaffleParticipantInput[] = [
      {
        fullName: 'Valeria Castillo',
        email: 'valeria.castillo@empresa.com',
        department: 'Recursos Humanos',
        employeeCode: 'EMP-810',
      },
      {
        fullName: 'Andrés Mejía',
        email: 'andres.mejia@empresa.com',
        department: 'Tecnología',
        employeeCode: 'EMP-811',
      },
      {
        fullName: 'Gabriela Núñez',
        email: 'gabriela.nunez@empresa.com',
        department: 'Auditoría',
        employeeCode: 'EMP-812',
      },
    ]
    const res = await importMutation.mutateAsync({
      participants: demoEmployees,
      markCheckedIn: cfg.markCheckedIn,
      source: 'api',
    })
    const msg = `Sincronización HR API completada: ${res.imported} nuevos, ${res.updated} actualizados.`
    setStatusMessage({ tone: 'ok', text: msg })
    onSynced?.(msg)
  }

  const handleSimulateWebhook = async () => {
    setStatusMessage(null)
    try {
      const parsed = JSON.parse(webhookBody) as {
        participants?: unknown[]
        markCheckedIn?: boolean
        replaceExisting?: boolean
      }
      const list = normalizeRecords(parsed.participants ?? parsed)
      if (list.length === 0) {
        throw new Error('El JSON debe incluir un arreglo "participants" con al menos un nombre.')
      }
      const res = await importMutation.mutateAsync({
        participants: list,
        markCheckedIn: parsed.markCheckedIn ?? true,
        replaceExisting: Boolean(parsed.replaceExisting),
        source: 'webhook',
      })
      const msg = `Webhook procesado: ${res.imported} nuevos, ${res.updated} actualizados (Total: ${res.total}).`
      setStatusMessage({ tone: 'ok', text: msg })
      onSynced?.(msg)
    } catch (err) {
      setStatusMessage({
        tone: 'err',
        text: err instanceof Error ? err.message : 'JSON de Webhook inválido.',
      })
    }
  }

  const handleSimulateCheckinWebhook = async () => {
    setStatusMessage(null)
    try {
      const parsed = JSON.parse(checkinBody) as {
        qrCode?: string
        email?: string
        employeeCode?: string
        checkedIn?: boolean
      }
      const key = parsed.qrCode?.trim() || parsed.email?.trim() || parsed.employeeCode?.trim()
      if (!key) {
        throw new Error('Incluye "qrCode", "email" o "employeeCode" en el JSON del check-in.')
      }
      const updated = await checkInMutation.mutateAsync({
        registrationIdOrCode: key,
        checkedIn: parsed.checkedIn ?? true,
      })
      const msg = `Check-in vía Webhook aplicado a ${updated.fullName} (${updated.qrCode}) -> ${updated.checkedIn ? 'Presente / Califica' : 'Pendiente'}.`
      setStatusMessage({ tone: 'ok', text: msg })
      onSynced?.(msg)
    } catch (err) {
      setStatusMessage({
        tone: 'err',
        text:
          err instanceof Error
            ? err.message
            : 'No se encontró ningún participante con ese QR, correo o código.',
      })
    }
  }

  return (
    <Modal title="Conexión API & Webhook (Participantes y Check-in)" onClose={onClose} wide>
      <div className="space-y-4 text-xs">
        <div className="flex flex-wrap gap-1.5 rounded-xl bg-slate-100 p-1 dark:bg-slate-800">
          {[
            { id: 'api' as const, label: 'Sincronizar desde API Externa', icon: Globe },
            { id: 'webhook' as const, label: 'Webhook Participantes (POST)', icon: Webhook },
            { id: 'checkin' as const, label: 'Webhook / API Check-in QR', icon: QrCode },
          ].map((item) => (
            <button
              key={item.id}
              type="button"
              onClick={() => {
                setTab(item.id)
                setStatusMessage(null)
              }}
              className={`flex items-center gap-1.5 rounded-lg px-3 py-1.5 font-bold transition-colors cursor-pointer ${
                tab === item.id
                  ? 'bg-white text-indigo-700 shadow-2xs dark:bg-slate-900 dark:text-indigo-400'
                  : 'text-slate-600 hover:text-slate-900 dark:text-slate-300'
              }`}
            >
              <item.icon size={14} />
              <span>{item.label}</span>
            </button>
          ))}
        </div>

        {statusMessage && (
          <div
            className={`flex items-start gap-2 rounded-xl border p-3 font-semibold ${
              statusMessage.tone === 'ok'
                ? 'border-emerald-200 bg-emerald-50 text-emerald-800 dark:border-emerald-900/50 dark:bg-emerald-950/30 dark:text-emerald-300'
                : 'border-rose-200 bg-rose-50 text-rose-800 dark:border-rose-900/50 dark:bg-rose-950/30 dark:text-rose-300'
            }`}
          >
            {statusMessage.tone === 'ok' ? (
              <CheckCircle2 size={15} className="mt-0.5 shrink-0" />
            ) : (
              <AlertTriangle size={15} className="mt-0.5 shrink-0" />
            )}
            <span>{statusMessage.text}</span>
          </div>
        )}

        {tab === 'api' && (
          <div className="space-y-4">
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-4">
              <div className="sm:col-span-3">
                <Field label="URL de la API Externa (Nómina / RRHH / CRM)">
                  <TextInput
                    value={cfg.endpointUrl}
                    onChange={(e) => updateCfg({ endpointUrl: e.target.value })}
                    placeholder="https://api.tuempresa.com/v1/empleados"
                  />
                </Field>
              </div>
              <Field label="Método HTTP">
                <Select
                  value={cfg.method}
                  onChange={(e) => updateCfg({ method: e.target.value as 'GET' | 'POST' })}
                >
                  <option value="GET">GET</option>
                  <option value="POST">POST</option>
                </Select>
              </Field>
            </div>

            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <Field label="Header Authorization (Opcional)">
                <TextInput
                  value={cfg.authHeader}
                  onChange={(e) => updateCfg({ authHeader: e.target.value })}
                  placeholder="Bearer eyJhbGci..."
                />
              </Field>
              <Field label="Ruta JSON al arreglo (Opcional)">
                <TextInput
                  value={cfg.dataPath}
                  onChange={(e) => updateCfg({ dataPath: e.target.value })}
                  placeholder="Ej: data.employees (vacío = automático)"
                />
              </Field>
            </div>

            <label className="flex items-center gap-2 font-semibold text-slate-700 dark:text-slate-200 cursor-pointer">
              <input
                type="checkbox"
                checked={cfg.markCheckedIn}
                onChange={(e) => updateCfg({ markCheckedIn: e.target.checked })}
                className="h-4 w-4 rounded border-slate-300 text-indigo-600"
              />
              <span>Marcar empleados sincronizados con check-in inmediato</span>
            </label>

            <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-slate-200 bg-slate-50 p-3 dark:border-slate-800 dark:bg-slate-800/40">
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={handleDemoApiImport}
                disabled={importMutation.isPending}
                className="cursor-pointer"
              >
                <Play size={13} className="mr-1.5" /> Probar carga desde HR API Demo
              </Button>
              <Button
                type="button"
                onClick={handleSyncFromApi}
                disabled={fetchingApi || importMutation.isPending}
                className="bg-indigo-600 hover:bg-indigo-700 text-white font-bold cursor-pointer"
              >
                <RefreshCw size={13} className={`mr-1.5 ${fetchingApi ? 'animate-spin' : ''}`} />
                {fetchingApi ? 'Consultando API...' : 'Sincronizar ahora'}
              </Button>
            </div>
          </div>
        )}

        {tab === 'webhook' && (
          <div className="space-y-4">
            <div className="rounded-xl border border-slate-200 bg-slate-50 p-3 space-y-2 dark:border-slate-800 dark:bg-slate-800/40">
              <div className="flex items-center justify-between">
                <span className="font-bold text-slate-800 dark:text-slate-200">
                  Endpoint Receptor de Participantes (<code>POST</code>)
                </span>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => copyText('wh-part', participantsWebhookUrl)}
                  className="cursor-pointer text-[11px]"
                >
                  {copiedKey === 'wh-part' ? (
                    <>
                      <Check size={12} className="mr-1 text-emerald-600" /> Copiado
                    </>
                  ) : (
                    <>
                      <Copy size={12} className="mr-1" /> Copiar URL
                    </>
                  )}
                </Button>
              </div>
              <code className="block overflow-x-auto rounded-lg bg-slate-900 px-3 py-2 text-[11px] text-emerald-300">
                {participantsWebhookUrl}
              </code>
              <p className="text-[11px] text-slate-500">
                Header recomendado: <code>X-Webhook-Secret: {cfg.webhookSecret}</code>
              </p>
            </div>

            <Field label="Simulador en Vivo — Payload JSON del Webhook">
              <TextArea
                rows={8}
                value={webhookBody}
                onChange={(e) => setWebhookBody(e.target.value)}
                className="font-mono text-[11px]"
              />
            </Field>

            <div className="flex justify-end">
              <Button
                type="button"
                onClick={handleSimulateWebhook}
                disabled={importMutation.isPending}
                className="bg-indigo-600 hover:bg-indigo-700 text-white font-bold cursor-pointer"
              >
                <Play size={13} className="mr-1.5" /> Ejecutar Webhook de Participantes
              </Button>
            </div>
          </div>
        )}

        {tab === 'checkin' && (
          <div className="space-y-4">
            <div className="rounded-xl border border-slate-200 bg-slate-50 p-3 space-y-2 dark:border-slate-800 dark:bg-slate-800/40">
              <div className="flex items-center justify-between">
                <span className="font-bold text-slate-800 dark:text-slate-200">
                  Endpoint Webhook de Asistencia / Check-in QR (<code>POST</code>)
                </span>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => copyText('wh-chk', checkinWebhookUrl)}
                  className="cursor-pointer text-[11px]"
                >
                  {copiedKey === 'wh-chk' ? (
                    <>
                      <Check size={12} className="mr-1 text-emerald-600" /> Copiado
                    </>
                  ) : (
                    <>
                      <Copy size={12} className="mr-1" /> Copiar URL
                    </>
                  )}
                </Button>
              </div>
              <code className="block overflow-x-auto rounded-lg bg-slate-900 px-3 py-2 text-[11px] text-sky-300">
                {checkinWebhookUrl}
              </code>
              <p className="text-[11px] text-slate-500">
                Acepta <code>qrCode</code>, <code>email</code> o <code>employeeCode</code> para
                registrar el check-in desde torniquetes, tótems o sistemas externos de puerta.
              </p>
            </div>

            <Field label="Simulador de Evento Check-in (JSON)">
              <TextArea
                rows={6}
                value={checkinBody}
                onChange={(e) => setCheckinBody(e.target.value)}
                className="font-mono text-[11px]"
              />
            </Field>

            <div className="flex justify-end">
              <Button
                type="button"
                onClick={handleSimulateCheckinWebhook}
                disabled={checkInMutation.isPending}
                className="bg-emerald-600 hover:bg-emerald-700 text-white font-bold cursor-pointer"
              >
                <QrCode size={13} className="mr-1.5" /> Probar Check-in vía Webhook
              </Button>
            </div>
          </div>
        )}

        {(importMutation.error != null || checkInMutation.error != null) && (
          <ErrorNote error={importMutation.error ?? checkInMutation.error} />
        )}

        <div className="flex justify-end border-t border-slate-100 pt-3 dark:border-slate-800">
          <Button type="button" variant="outline" onClick={onClose} className="cursor-pointer">
            Cerrar
          </Button>
        </div>
      </div>
    </Modal>
  )
}
