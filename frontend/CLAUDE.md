# frontend — CLAUDE.md

> La SPA de Sorteo360. Plan, arquitectura y reparto de trabajo: `../plans/sorteo360.md`.
> Operación local y variables: `README.md` (esta carpeta).

## Qué es

React + TypeScript + Vite + Tailwind v4 + TanStack Query. El producto es el **sorteo** (padrón
de participantes por Excel/CSV/TXT, API o webhook; check-in con QR; sorteo en vivo con ruleta o
nombres pasando; ganadores, entrega, acta y certificados). **Eventos** y **convocatorias**
existen pero dependen de sus interruptores (`features/admin/featureCatalogue.ts`); apagados, no se
ofrecen en ninguna pantalla.

Dos modos: `http` contra los servicios reales y `mock` sin backend, con los datos en
`localStorage` (`sorteo360.mock.state.v4`).

## Reglas

- **Contratos:** `src/api/types.ts` replica los DTO JSON de los servicios por nombre exacto
  (camelCase). Un cambio de contrato toca a la vez ese archivo, el mock (`src/api/mock/`) y el
  cliente HTTP.
- **Puerto de datos:** toda llamada pasa por `ApiClient` (`src/api/client.ts`). Nunca `fetch` desde
  un componente: el método va a la interfaz y a **las dos** implementaciones.
- **El mock replica las reglas del negocio**, no solo las formas, y usa los mismos `code` de error
  que el servicio con el texto que `labels.ts` les da. Una regla nueva del backend tiene que
  llegar al mock o la demo miente.
- **Colores:** nunca un hex en una clase ni en un estilo. La marca es `--brand-primary` y
  `--brand-accent` (`src/index.css`), personalizables en Configuración → Marca; las utilidades son
  `bg-brand`, `bg-brand-deep`, `text-brand-accent`, etc. Lo que pinta fuera de CSS (canvas,
  confeti, gráficos) usa `getBrandColors()` de `src/lib/branding.ts`.
- **Nombres:** el producto se llama Sorteo360 y el cliente se nombra con
  `useBranding().organizationName`. Nada del sistema anterior (ministerio, PGTI) en pantallas,
  textos, claves ni comentarios.
- **`Button` no fija `type`:** dentro de un `<form>` envía. Todo botón que no sea el de enviar
  lleva `type="button"`.
- **Un 400 con `errors` es un error de campo:** el mensaje va en el input (`errorDeCampo` + la
  prop `error` de `Field`); el banner solo dice que algo falló.
- **Idioma:** la interfaz es 100 % en español; los códigos de máquina (estados, enums) van en
  inglés y se traducen solo en `src/lib/labels.ts`.
- **Avisos:** nunca `alert()` ni `confirm()` del navegador; los avisos son `toast` (sonner) y las
  confirmaciones `ConfirmDialog`.
- **Auth:** en `http` el login es Keycloak (`src/auth/`), cliente público `sorteo360-app`, PKCE.
  El token llega al cliente HTTP por `setTokenProvider`; nunca se lee en un componente. Los
  permisos se consultan con `useCan(ROLES.x)`; `src/auth/roles.ts` replica el realm.
- **Docker:** el servicio `frontend` del compose raíz sirve el build con nginx y reenvía `/v1` a
  events-service; `/v1/public/` y `/v1/convocatorias/` van a convocatorias-service y
  `/v1/notifications` a notification-service. Son **dos listas** (`nginx.conf` y el proxy de Vite)
  y tienen que coincidir. En nginx cada destino va por una variable `set` más `$request_uri`,
  nunca un hostname literal en `proxy_pass`.

## Verificación mínima

`npm run build && npm run lint && npm test` (Node 20 o 22). Las pantallas las cubren las pruebas
de Playwright (`npm run test:e2e`, modo mock).
