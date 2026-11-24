# Sorteo360 — Frontend

La aplicación web de Sorteo360: sorteos y rifas en vivo con padrón de participantes, check-in con
QR y certificados. Los módulos de eventos y convocatorias existen pero se encienden por
interruptor (Administración → Interruptores).

## Stack

React 18 + TypeScript + Vite 6 + Tailwind CSS v4 + TanStack Query. Los colores de la marca son
dos variables (`--brand-primary`, `--brand-accent` en `src/index.css`) que se cambian desde
Configuración → Marca; las utilidades `bg-brand`, `text-brand-deep`, `border-brand-accent`, etc.
los siguen.

## Ejecutar

Node 20 o 22.

```bash
npm ci
npm run dev                                   # contra los servicios reales (modo http)
VITE_API_MODE=mock VITE_AUTH_MODE=local npm run dev   # demo sin backend ni Keycloak
```

En modo `mock` los datos viven en el navegador (`localStorage`, clave
`sorteo360.mock.state.v3`): sobreviven a una recarga, no a otro equipo ni a borrar los datos del
navegador.

## Verificar

```bash
npm run build && npm run lint && npm test   # tipos, lint y pruebas unitarias (Vitest)
npm run test:e2e                            # pruebas de pantalla (Playwright, modo mock)
```

## Variables de entorno

Se hornean al construir la imagen (`VITE_*`):

| Variable | Qué es |
| --- | --- |
| `VITE_API_MODE` | `http` (servicios reales) o `mock` (demo en el navegador) |
| `VITE_AUTH_MODE` | `keycloak` o `local` (usuarios de demostración) |
| `VITE_EVENTOS_API_URL` | Vacía: la app llama `/v1` en su mismo origen y nginx/Vite lo reenvía |
| `VITE_KEYCLOAK_URL`, `VITE_KEYCLOAK_REALM`, `VITE_KEYCLOAK_CLIENT_ID` | Keycloak visto desde el navegador (realm `sorteo360`, cliente `sorteo360-app`) |

El plan de trabajo y la arquitectura están en `../plans/sorteo360.md`.
