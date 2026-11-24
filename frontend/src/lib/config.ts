export type ApiMode = 'mock' | 'http'
export type AuthMode = 'local' | 'keycloak'

const rawApiMode = import.meta.env.VITE_API_MODE
const resolvedApiMode = (rawApiMode ?? 'mock') as ApiMode
const resolvedAuthMode = (import.meta.env.VITE_AUTH_MODE ??
  (resolvedApiMode === 'http' ? 'keycloak' : 'local')) as AuthMode

export const config = {
  apiMode: resolvedApiMode,
  authMode: resolvedAuthMode,
  // Vacío = mismo origen: NGINX / Vite proxy reenvía /v1 a eventos-service evitando CORS.
  // En QA / Producción, definir VITE_EVENTOS_API_URL para consumir el backend directamente.
  eventosApiUrl: (import.meta.env.VITE_EVENTOS_API_URL ?? '').replace(/\/$/, ''),
  // URL de Keycloak vista desde el NAVEGADOR (cuando authMode === 'keycloak').
  keycloakUrl: (import.meta.env.VITE_KEYCLOAK_URL ?? 'http://localhost:8082').replace(/\/$/, ''),
  keycloakRealm: import.meta.env.VITE_KEYCLOAK_REALM ?? 'sorteo360',
  keycloakClientId: import.meta.env.VITE_KEYCLOAK_CLIENT_ID ?? 'sorteo360-app',
  // El registro de cuentas (`/crear-cuenta`): apagado mientras el servicio no lo tenga (A25).
  // Apagado salvo que se pida: el registro público abre después de las fiestas de Banreservas.
  // El modo demostración (sin Keycloak) pide login como producción; las pruebas e2e lo saltan.
  mockLogin: import.meta.env.VITE_MOCK_LOGIN !== 'false',
  signupEnabled: import.meta.env.VITE_SIGNUP_ENABLED === 'true',
  // Sin registro, los planes del landing ofrecen escribir a este correo.
  contactEmail: import.meta.env.VITE_CONTACT_EMAIL ?? '',
}
