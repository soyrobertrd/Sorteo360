import Keycloak from 'keycloak-js'
import { config } from '@/lib/config'

let instance: Keycloak | null = null
let initPromise: Promise<boolean> | null = null

export function getKeycloak(): Keycloak {
  if (!instance) {
    instance = new Keycloak({
      url: config.keycloakUrl,
      realm: config.keycloakRealm,
      clientId: config.keycloakClientId,
    })
  }
  return instance
}

/**
 * Inicializa una sola vez (idempotente frente al doble montaje de StrictMode).
 * login-required: sin sesión válida redirige a la página de login de Keycloak.
 */
export function initKeycloak(): Promise<boolean> {
  if (!initPromise) {
    initPromise = getKeycloak().init({
      onLoad: 'login-required',
      // v1.0: el landing está oculto; quien entra por la raíz vuelve del login directo a /app.
      redirectUri: window.location.pathname === '/' ? `${window.location.origin}/app` : undefined,
      pkceMethod: 'S256',
      checkLoginIframe: false,
    })
  }
  return initPromise
}
