import { useSyncExternalStore } from 'react'

export interface GlobalBranding {
  appName: string
  appSubtitle: string
  organizationName: string
  logoUrl: string
  logoDarkUrl: string
  /** Color principal de la marca, en hex (#rrggbb): barra, botones, enlaces. */
  primaryColor: string
  /** Color de acento, en hex (#rrggbb): resaltados, gráficos, insignias. */
  accentColor: string
}

export interface RaffleBranding {
  companyName?: string
  logoUrl?: string
}

interface BrandingStoreState {
  global: GlobalBranding
  byEvent: Record<string, RaffleBranding>
}

const STORAGE_KEY = 'sorteo360.branding.v1'

export const DEFAULT_GLOBAL_BRANDING: GlobalBranding = {
  appName: 'Sorteo360',
  appSubtitle: 'Plataforma de Rifas y Check-in QR',
  organizationName: 'Empresa Anfitriona',
  logoUrl: '/logo-brand.svg',
  logoDarkUrl: '/logo-brand-dark.svg',
  // Los colores del logo del cliente.
  primaryColor: '#0d3b7d',
  accentColor: '#1ea2dc',
}

const HEX_COLOR = /^#[0-9a-f]{6}$/i

/** El color si es un hex #rrggbb válido; si no, el de respaldo (lo guardado puede venir alterado). */
export function safeHexColor(value: unknown, fallback: string): string {
  return typeof value === 'string' && HEX_COLOR.test(value.trim())
    ? value.trim().toLowerCase()
    : fallback
}

/** Los dos colores de marca ya validados, para lo que pinta fuera de CSS (canvas, confeti). */
export function getBrandColors(): { primary: string; accent: string } {
  const g = readStore().global
  return {
    primary: safeHexColor(g.primaryColor, DEFAULT_GLOBAL_BRANDING.primaryColor),
    accent: safeHexColor(g.accentColor, DEFAULT_GLOBAL_BRANDING.accentColor),
  }
}

/** Escribe los dos colores de marca en <html>; src/index.css deriva de ellos toda la paleta. */
function applyBrandColors(global: GlobalBranding) {
  if (typeof document === 'undefined') return
  const root = document.documentElement.style
  root.setProperty(
    '--brand-primary',
    safeHexColor(global.primaryColor, DEFAULT_GLOBAL_BRANDING.primaryColor),
  )
  root.setProperty(
    '--brand-accent',
    safeHexColor(global.accentColor, DEFAULT_GLOBAL_BRANDING.accentColor),
  )
}

let cachedRaw: string | null = null
let cachedState: BrandingStoreState = { global: { ...DEFAULT_GLOBAL_BRANDING }, byEvent: {} }
const listeners = new Set<() => void>()

function readStore(): BrandingStoreState {
  if (typeof window === 'undefined') return cachedState
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY)
    if (!raw) {
      if (cachedRaw !== null) {
        cachedRaw = null
        cachedState = { global: { ...DEFAULT_GLOBAL_BRANDING }, byEvent: {} }
      }
      return cachedState
    }
    if (raw === cachedRaw) return cachedState
    const parsed = JSON.parse(raw) as Partial<BrandingStoreState>
    cachedRaw = raw
    cachedState = {
      global: {
        ...DEFAULT_GLOBAL_BRANDING,
        ...(parsed.global ?? {}),
      },
      byEvent: { ...(parsed.byEvent ?? {}) },
    }
    return cachedState
  } catch {
    return cachedState
  }
}

function writeStore(next: BrandingStoreState) {
  cachedState = next
  if (typeof window !== 'undefined') {
    try {
      const raw = JSON.stringify(next)
      cachedRaw = raw
      window.localStorage.setItem(STORAGE_KEY, raw)
    } catch {
      // Ignora cuota excedida en entornos restringidos.
    }
  }
  applyBrandColors(next.global)
  for (const listener of listeners) listener()
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener)
  const onStorage = (ev: StorageEvent) => {
    if (ev.key === STORAGE_KEY) listener()
  }
  if (typeof window !== 'undefined') {
    window.addEventListener('storage', onStorage)
  }
  return () => {
    listeners.delete(listener)
    if (typeof window !== 'undefined') {
      window.removeEventListener('storage', onStorage)
    }
  }
}

function getSnapshot(): BrandingStoreState {
  return readStore()
}

export function updateGlobalBranding(patch: Partial<GlobalBranding>) {
  const cur = readStore()
  writeStore({
    ...cur,
    global: {
      ...cur.global,
      ...patch,
    },
  })
}

export function resetGlobalBranding() {
  const cur = readStore()
  writeStore({
    ...cur,
    global: { ...DEFAULT_GLOBAL_BRANDING },
  })
}

export function setRaffleBranding(eventId: string, branding: RaffleBranding) {
  const cur = readStore()
  const cleanCompany = branding.companyName?.trim() || undefined
  const cleanLogo = branding.logoUrl?.trim() || undefined
  const nextByEvent = { ...cur.byEvent }
  if (!cleanCompany && !cleanLogo) {
    delete nextByEvent[eventId]
  } else {
    nextByEvent[eventId] = {
      companyName: cleanCompany,
      logoUrl: cleanLogo,
    }
  }
  writeStore({
    ...cur,
    byEvent: nextByEvent,
  })
}

export interface ResolvedBranding extends GlobalBranding {
  companyName: string
  eventLogoUrl?: string
  hasCustomEventBranding: boolean
}

export function getBranding(eventId?: string): ResolvedBranding {
  const state = readStore()
  const ev = eventId ? state.byEvent[eventId] : undefined
  const hasCustomEventBranding = Boolean(ev?.companyName || ev?.logoUrl)
  return {
    ...state.global,
    logoUrl: ev?.logoUrl || state.global.logoUrl,
    logoDarkUrl: ev?.logoUrl || state.global.logoDarkUrl,
    companyName: ev?.companyName || state.global.organizationName || state.global.appName,
    eventLogoUrl: ev?.logoUrl,
    hasCustomEventBranding,
  }
}

export function useBranding(eventId?: string): ResolvedBranding {
  const state = useSyncExternalStore(subscribe, getSnapshot, getSnapshot)
  const ev = eventId ? state.byEvent[eventId] : undefined
  const hasCustomEventBranding = Boolean(ev?.companyName || ev?.logoUrl)
  return {
    ...state.global,
    logoUrl: ev?.logoUrl || state.global.logoUrl,
    logoDarkUrl: ev?.logoUrl || state.global.logoDarkUrl,
    companyName: ev?.companyName || state.global.organizationName || state.global.appName,
    eventLogoUrl: ev?.logoUrl,
    hasCustomEventBranding,
  }
}

// Los colores se aplican al cargar la app y cuando otra pestaña los cambia, haya o no un
// componente suscrito: la paleta es de toda la página.
applyBrandColors(readStore().global)
if (typeof window !== 'undefined') {
  window.addEventListener('storage', (ev) => {
    if (ev.key === STORAGE_KEY) applyBrandColors(readStore().global)
  })
}

/** Mezcla dos colores hex (#rrggbb): `t` es la proporción del segundo, de 0 a 1. */
export function mixHex(a: string, b: string, t: number): string {
  const parse = (h: string) => [1, 3, 5].map((i) => parseInt(h.slice(i, i + 2), 16))
  const [x, y] = [parse(a), parse(b)]
  return `#${x
    .map((v, i) =>
      Math.round(v + (y[i] - v) * t)
        .toString(16)
        .padStart(2, '0'),
    )
    .join('')}`
}

/**
 * La paleta de lo que se pinta fuera de CSS (canvas, confeti, gráficos): los dos colores de
 * marca y tonos derivados de ellos, así todo cambia cuando cambia la marca.
 */
export function getBrandPalette(): string[] {
  const { primary, accent } = getBrandColors()
  return [
    primary,
    accent,
    mixHex(primary, '#000000', 0.3),
    mixHex(accent, '#000000', 0.3),
    mixHex(primary, '#ffffff', 0.25),
    mixHex(accent, '#ffffff', 0.25),
    mixHex(primary, accent, 0.5),
    mixHex(primary, '#000000', 0.5),
  ]
}

/** Un hex #rrggbb como [r, g, b], para jsPDF. */
export function hexToRgb(hex: string): [number, number, number] {
  return [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16)) as [number, number, number]
}
