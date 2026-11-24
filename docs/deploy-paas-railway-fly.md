# Despliegue de prueba en PaaS: Railway y Fly.io

> **Estado (2026-11-23):** plan sin ejecutar. No hay cambios de código todavía.
> El despliegue de producción vigente sigue siendo el de `docs/deploy-sorteo360.md` (VPS + Compose + Caddy).
> Este documento recoge lo que se analizó para retomarlo en otra sesión.

## Decisión y contexto

- Vercel y Cloudways se descartaron. Vercel solo sirve estáticos y funciones serverless (Node/Python),
  y su plan Hobby no permite uso comercial. Cloudways es hosting PHP sin Docker. Ninguno corre los
  contenedores .NET 10 ni Keycloak, y migrar el backend a TS o Laravel es reescribir 3 servicios (semanas).
- **Se mantiene el stack .NET y se cambia solo el hosting.** El hosting elegido es Railway. Fly.io se
  prueba en paralelo para comparar.
- Para demos solo de interfaz no hace falta backend. El frontend tiene un modo `mock` (el default, sin
  `VITE_API_MODE`) con login local y datos en `localStorage`. Un `npm run build` en ese modo se publica
  gratis como sitio estático (Cloudflare Pages o Netlify).

## Estrategia de trabajo: dos copias

1. Aplicar primero los **cambios comunes** (abajo) en el repo original, porque valen para las dos plataformas.
2. Copiar fuera de OneDrive y sin dependencias:
   ```powershell
   $src = "C:\Users\Ro.Guzman\OneDrive - sectur.gov.do\Escritorio\Pav - Banreservas\Sorteo360"
   robocopy $src C:\dev\Sorteo360-railway /E /XD node_modules bin obj .vs /NFL /NDL
   robocopy $src C:\dev\Sorteo360-fly     /E /XD node_modules bin obj .vs /NFL /NDL
   ```
3. En cada copia, crear una rama: `git switch -c deploy/railway` o `git switch -c deploy/fly`. Lo que funcione
   vuelve al repo con cherry-pick.

No se usa `git worktree` porque `development` tiene muchos cambios sin commit (el recorte del fork) y un
worktree parte del último commit.

## Cambios comunes (repo original, antes de copiar)

Todo lo que sigue asume Docker Compose y falla en un PaaS, donde cada servicio se despliega por separado,
sin bind mounts y con otro DNS interno.

### 1. nginx del frontend: resolver y hosts por variable

`frontend/nginx.conf` tiene dos supuestos de Docker:

- `resolver 127.0.0.11 valid=10s ipv6=off;`: es el DNS embebido de Docker. Railway y Fly usan otro DNS, y
  su red privada es IPv6, así que `ipv6=off` también rompe.
- Hosts fijos en cada `set $... http://events-service:8080;` (y `convocatorias-service`, `notification-service`).

Propuesta:
- Mover el archivo a `frontend/nginx/templates/default.conf.template`. La imagen `nginx-unprivileged`
  ejecuta envsubst sobre `/etc/nginx/templates/*.template` al arrancar, y solo sustituye variables
  **definidas** en el entorno, así que `$request_uri` y `$host` quedan intactas.
- Variables: `NGINX_RESOLVER`, `EVENTS_UPSTREAM`, `CONVOCATORIAS_UPSTREAM`, `NOTIFICATIONS_UPSTREAM`.
- Defaults para Compose (`127.0.0.11 ipv6=off`, `http://events-service:8080`, …) en el Dockerfile,
  para que el stack local y el de VPS no cambien.
- Mantener la regla de las dos listas: las rutas de `nginx.conf` y las de `vite.config.ts` tienen que coincidir.

### 2. Keycloak: imagen propia en vez de volúmenes

En `docker-compose.prod.yml`, Keycloak recibe el realm (`./infra/keycloak/`) y los temas
(`./infra/keycloak-themes/`) por bind mount. Los PaaS no tienen bind mounts.

Propuesta: crear `infra/keycloak/Dockerfile` con `FROM quay.io/keycloak/keycloak:26.6` que haga `COPY` del
realm a `/opt/keycloak/data/import/` y de los temas a `/opt/keycloak/themes/`, con
`CMD ["start", "--import-realm"]`. Compose puede seguir usando la imagen base con volúmenes.

### 3. Postgres: conexión configurable, sin CA propia

En producción, las cadenas de conexión usan `SSL Mode=VerifyFull;Root Certificate=/etc/postgresql-ca/ca.crt`
contra una CA generada por `postgres-certs`, y `postgres-roles` (`infra/postgres-setup/roles.sh`) crea
`events_app`, `convocatorias_app`, `notifications_app` y `keycloak_app`.

En un PaaS:
- `ConnectionStrings__Postgres` se define entera como variable de entorno por servicio (ya funciona así;
  no hace falta código). Sobre la red privada: `SSL Mode=Disable` o `Prefer`.
- `KC_DB_URL` sin `sslrootcert`.
- Bases y roles se crean **una sola vez a mano** con `psql`: replicar `infra/postgres/init.sh` (4 `CREATE DATABASE`)
  y `roles.sh`. Las migraciones de EF Core se siguen aplicando solas al arrancar cada servicio.

### 4. Archivos de events-service: volumen persistente

`FileStore__Provider=filesystem` y `FileStore__Filesystem__RootPath=/var/lib/sorteo360/files`. Sin volumen,
los archivos subidos se pierden en cada redeploy. Hay que montar un volumen de Railway o de Fly en esa ruta.
Ojo con los permisos: el contenedor corre con un usuario no-root.

## Variables por servicio (de `docker-compose.prod.yml`)

Sustituir `postgres`, `keycloak`, `events-service`, etc. por el host interno de cada plataforma:
`<servicio>.railway.internal` en Railway, `<app>.internal` en Fly.

| Servicio | Variables a ajustar |
| --- | --- |
| events-service | `ConnectionStrings__Postgres`, `Keycloak__Authority` (interno), `Keycloak__AdditionalValidIssuer` (`https://<auth público>/realms/sorteo360`), `ServiceAuth__ClientSecret`, `Services__Convocatorias__BaseUrl`, `Services__Keycloak__BaseUrl`, `Services__Notifications__BaseUrl`, `App__BaseUrl` (dominio público de la app), `FileStore__*` |
| convocatorias-service | `ConnectionStrings__Postgres`, `Keycloak__Authority`, `Keycloak__AdditionalValidIssuer`, `ServiceAuth__ClientSecret`, `Services__Events__BaseUrl`, `Services__Notifications__BaseUrl`, `Portal__BaseUrl` |
| notification-service | `ConnectionStrings__Postgres`, `Keycloak__*`, `Smtp__*` (relay real, o Mailpit como servicio aparte para pruebas) |
| keycloak | `KC_DB_URL`, `KC_DB_USERNAME/PASSWORD`, `KC_HOSTNAME=https://<auth público>`, `KC_HTTP_ENABLED=true`, `KC_PROXY_HEADERS=xforwarded`, `KC_BOOTSTRAP_ADMIN_*`, `EVENTS_SERVICE_CLIENT_SECRET`, `CONVOCATORIAS_SERVICE_CLIENT_SECRET` |
| frontend | **Build args** (se hornean en el bundle): `VITE_API_MODE=http`, `VITE_KEYCLOAK_URL=https://<auth público>`, `VITE_SIGNUP_ENABLED=false`. En runtime, las variables de nginx del cambio 1 |

Comunes a los tres servicios .NET: `ASPNETCORE_ENVIRONMENT=Production`, `ASPNETCORE_HTTP_PORTS=8080`.

**Keycloak después del primer arranque:** el realm JSON solo se importa la primera vez (ADR-0016). Los
`redirectUris` y `webOrigins` del cliente `sorteo360-app` con el dominio del PaaS se agregan en la consola de
Keycloak. El JSON usa `${APP_DOMAIN}`, así que también se puede definir `APP_DOMAIN` en Keycloak antes del primer arranque.

Solo dos servicios necesitan dominio público: **frontend** y **keycloak**. Los servicios .NET y Postgres van
solo por la red privada. Caddy no se despliega, porque el PaaS pone el TLS.

## Railway

- Un proyecto con: plugin de Postgres, keycloak, events-service, convocatorias-service, notification-service y frontend.
- Servicios .NET: root directory = raíz del repo y `RAILWAY_DOCKERFILE_PATH=events-service/Dockerfile` (y su
  equivalente en cada uno). El contexto debe ser la raíz para que resuelva el `ProjectReference` a `shared/`.
- `NGINX_RESOLVER`: verificar qué DNS expone la red privada de Railway (pendiente).
- Kestrel tiene que escuchar también en IPv6 para la red privada. Verificar que `ASPNETCORE_HTTP_PORTS=8080`
  enlaza en `[::]`.
- Costo: crédito de trial único, después Hobby ~$5/mes + uso. Keycloak y 3 servicios .NET encendidos
  probablemente pasen de $5 (no verificado).

## Fly.io

- Una app por servicio, cada una con su `fly.toml`. Despliegue desde la raíz:
  `fly deploy --config events-service/fly.toml --dockerfile events-service/Dockerfile`.
- Red privada: `<app>.internal` por IPv6 (6PN), con DNS en `fdaa::3`. En nginx: `NGINX_RESOLVER="[fdaa::3]"` sin `ipv6=off`.
- Postgres: Managed Postgres es caro para una prueba. Las alternativas son Postgres propio en una máquina con
  volumen, o uno externo (Neon/Supabase, que exigen TLS: `SSL Mode=Require`).
- **No activar auto-stop en Keycloak**: tarda ~30–60 s en despertar y rompe el login de una demo. Puede
  usarse en los servicios .NET si se acepta la latencia del primer request.
- No hay plan gratis para cuentas nuevas (pago por uso y tarjeta obligatoria).

## Pendiente de verificar

- [ ] Precios y límites actuales de Railway y Fly (lo de arriba es de memoria).
- [ ] DNS y soporte IPv4/IPv6 de la red privada de Railway.
- [ ] Que Kestrel escuche en IPv6 con la configuración actual.
- [ ] Que el `iss` del token (dominio público) y `Keycloak__Authority` (interno) convivan, como ya pasa en el VPS con `AdditionalValidIssuer`.
- [ ] Permisos del volumen de archivos con el usuario no-root.
- [ ] Healthchecks del PaaS apuntando a `/_alive` (los servicios .NET y nginx lo exponen).

## Orden sugerido para la próxima sesión

1. Cambios comunes 1–4 en el repo original, y verificar que `docker compose up` local sigue funcionando.
2. Copias y ramas.
3. Railway primero (la opción elegida), y después Fly.io.
4. Anotar aquí lo que se descubra y pasar a `docs/deploy-sorteo360.md` lo que quede como camino oficial.
