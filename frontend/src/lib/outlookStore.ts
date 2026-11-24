/**
 * Estado global y persistente de la vinculación de cuenta de Microsoft Outlook vía Microsoft Graph API.
 * Compartido entre todos los módulos de la plataforma (CRM, Convocatorias, Eventos, Notificaciones).
 */

export interface OutlookAccountInfo {
  isConnected: boolean
  email: string
  displayName: string
  tenantId: string
  scopes: string[]
  connectedAt?: string
}

const OUTLOOK_STORAGE_KEY = 'sorteo360_outlook_account'

const defaultAccount: OutlookAccountInfo = {
  isConnected: true,
  email: 'eventos@empresa.com',
  displayName: 'Coordinación de Eventos Corporativos',
  tenantId: 'empresa-tenant-guid-2026',
  scopes: ['Mail.Send', 'Mail.ReadWrite', 'User.Read', 'offline_access'],
  connectedAt: '2026-08-01T10:00:00Z',
}

export function getOutlookAccount(): OutlookAccountInfo {
  try {
    const raw = localStorage.getItem(OUTLOOK_STORAGE_KEY)
    if (raw) return JSON.parse(raw)
  } catch {
    // Fallback default
  }
  return defaultAccount
}

export function saveOutlookAccount(info: OutlookAccountInfo): void {
  try {
    localStorage.setItem(OUTLOOK_STORAGE_KEY, JSON.stringify(info))
  } catch {
    // localStorage Error fallback
  }
}

export function disconnectOutlookAccount(): OutlookAccountInfo {
  const disconnected: OutlookAccountInfo = {
    isConnected: false,
    email: '',
    displayName: '',
    tenantId: '',
    scopes: [],
  }
  saveOutlookAccount(disconnected)
  return disconnected
}
