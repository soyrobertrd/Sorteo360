# Despliegue de Sorteo360 en servidor de producción

Guía operativa para levantar Sorteo360 en un servidor Linux con Docker Compose v2, HTTPS
automático mediante Caddy, perfiles mínimos según el alcance del evento y respaldo automático
de PostgreSQL.

## 1. Requisitos previos en el servidor

- Linux x64/arm64 con Docker Engine 26+ y `docker compose` v2.24+.
- Registros DNS apuntando al servidor para:
  - `APP_DOMAIN` (ej. `sorteo.empresa.com`)
  - `AUTH_DOMAIN` (ej. `auth.sorteo.empresa.com`)
- Puertos `80` y `443` abiertos hacia Caddy (PostgreSQL, Keycloak y las APIs no publican
  puertos externos).

## 2. Configuración inicial

1. Clonar el repositorio y ubicarse en su raíz.
2. Copiar la plantilla y generar contraseñas seguras (`openssl rand -hex 32` para bases de datos,
   `openssl rand -base64 32` para secretos):

```bash
cp .env.prod.example .env.prod
chmod 600 .env.prod
nano .env.prod
```

3. Elegir el perfil mínimo en `COMPOSE_PROFILES`:
   - `events,frontend` → eventos, padrón Excel/CSV/TXT/webhook, ruleta, premios y acta PDF.
   - `events,convocatorias,notifications,frontend` → incluye portal de registro QR e invitaciones/correos.

## 3. Arranque y verificación

```bash
docker compose -f docker-compose.prod.yml --env-file .env.prod up -d --build
```

Comprobar estado de servicios y migraciones:

```bash
docker compose -f docker-compose.prod.yml ps
docker compose -f docker-compose.prod.yml logs --tail=50 events-service keycloak caddy
```

Al arrancar por primera vez:
- `postgres-certs` genera la CA interna de PostgreSQL.
- `postgres` y `postgres-roles` crean bases y usuarios aislados (`events_app`, `convocatorias_app`, `notifications_app`, `keycloak_app`).
- `keycloak` importa `infra/keycloak/sorteo360-realm.json` la primera vez únicamente. El realm trae 5 usuarios demo con la contraseña `localdev`: **borrarlos antes de abrir el DNS** con el paso a paso de §6.1.
- `caddy` obtiene los certificados TLS de Let's Encrypt automáticamente.

## 4. Copias de seguridad de PostgreSQL

Ejecutar manualmente o mediante `cron`:

```bash
chmod +x scripts/backup-postgres.sh
./scripts/backup-postgres.sh /var/backups/sorteo360 14
```

Para restaurar desde un backup:

```bash
gunzip -c /var/backups/sorteo360/sorteo360-pg-YYYYMMDD-HHMMSS.sql.gz | \
  docker compose -f docker-compose.prod.yml exec -T postgres psql -U "${POSTGRES_USER:-sorteo360}" -d postgres
```

## 5. Modo salón / red local sin internet (`A22`)

Si el evento ocurre en un salón cerrado sin salida a internet, se despliega el stack completo
en una laptop o mini-PC local usando `docker-compose.salon.yml` + `infra/caddy/Caddyfile.salon`:

### Antes del evento (con acceso a internet)

1. Pre-descargar imágenes base y construir las imágenes de producción en la laptop/mini-PC:
   ```bash
   cp .env.prod.example .env.prod
   # Definir APP_DOMAIN=sorteo.local, AUTH_DOMAIN=auth.sorteo.local,
   # COMPOSE_PROFILES=events,frontend y contraseñas fuertes.
   docker compose -f docker-compose.prod.yml -f docker-compose.salon.yml --env-file .env.prod pull postgres keycloak caddy
   docker compose -f docker-compose.prod.yml -f docker-compose.salon.yml --env-file .env.prod build
   ```
2. Levantar y cargar el padrón de participantes y premios con antelación; comprobar el modo
   proyector (`/proyector/:eventId`, sincronizado en la misma laptop por `BroadcastChannel`).
3. Tomar un respaldo previo de PostgreSQL antes de desconectar internet:
   ```bash
   ./scripts/backup-postgres.sh ./backups/pre-evento 30
   ```

### Durante el evento (sin internet)

1. Conectar la laptop/mini-PC al router local del salón con IP fija (ej. `192.168.10.10`) y
   apuntar `sorteo.local` y `auth.sorteo.local` a esa IP (DNS del router o `/etc/hosts` en los
   equipos de puerta/operación).
2. Iniciar el stack offline (Caddy levanta con `tls internal`, sin contactar Let's Encrypt, y
   desactiva el envío SMTP externo):
   ```bash
   docker compose -f docker-compose.prod.yml -f docker-compose.salon.yml --env-file .env.prod up -d --no-build
   ```
3. Para usar la cámara QR en tablets/celulares de puerta con HTTPS local, exportar e instalar el
   certificado raíz de Caddy en esos dispositivos:
   ```bash
   docker compose -f docker-compose.prod.yml cp caddy:/data/caddy/pki/authorities/local/root.crt ./caddy-local-ca.crt
   ```

### Al cerrar el evento

Tomar un respaldo inmediato de PostgreSQL (incluyendo ganadores, entregas y acta PDF) antes de
apagar la máquina:

```bash
./scripts/backup-postgres.sh ./backups/post-evento 30
```

## 6. Seguridad de cuentas y Keycloak antes de pasar a producción (`R6`, `S3`)

### 6.1 Borrar los usuarios demo (primer arranque, antes de abrir el DNS)

`infra/keycloak/sorteo360-realm.json` siembra los tres usuarios de la v1.0, `admin`, `supervisor` y
`sorteo` (rol `proyector`: corre la tómbola sin configurar), todos con la contraseña `localdev` y roles reales. Cualquiera que llegue al dominio
podría entrar con ellos, así que se borran **justo después del primer `up` y antes de apuntar el
DNS final** al servidor. Se hace desde dentro del contenedor con `kcadm.sh`, sin necesitar el
dominio ni la consola web:

1. Esperar a que Keycloak esté `healthy`:
   ```bash
   docker compose -f docker-compose.prod.yml ps keycloak
   ```
2. Iniciar sesión con el administrador inicial. Usa las variables `KC_BOOTSTRAP_ADMIN_*` que el
   contenedor ya tiene, así la contraseña no queda en el historial de la terminal. Este paso va
   **antes** de cambiar o borrar ese administrador (§4.4 de `plans/pruebas-y-produccion.md`, punto
   4.1); si ya se cambió, usar la contraseña nueva en `--password`.
   ```bash
   docker compose -f docker-compose.prod.yml exec keycloak sh -c \
     '/opt/keycloak/bin/kcadm.sh config credentials --config /tmp/kcadm.config \
        --server http://localhost:8080 --realm master \
        --user "$KC_BOOTSTRAP_ADMIN_USERNAME" --password "$KC_BOOTSTRAP_ADMIN_PASSWORD"'
   ```
3. Borrar los usuarios demo (los tres de hoy y los dos que sembraban versiones anteriores). `exact=true` evita que la búsqueda por prefijo encuentre otro usuario
   (por ejemplo, `admin` también encontraría `admin.banreservas`):
   ```bash
   docker compose -f docker-compose.prod.yml exec keycloak sh -c '
     for u in operador supervisor admin puerta proyector sorteo; do
       id=$(/opt/keycloak/bin/kcadm.sh get users --config /tmp/kcadm.config -r sorteo360 \
              -q username=$u -q exact=true --fields id --format csv --noquotes)
       if [ -n "$id" ]; then
         /opt/keycloak/bin/kcadm.sh delete users/$id --config /tmp/kcadm.config -r sorteo360 \
           && echo "borrado: $u"
       else
         echo "no existe: $u"
       fi
     done'
   ```
4. Comprobar que no queda ninguno. La lista no debe mostrar ninguno de los 5:
   ```bash
   docker compose -f docker-compose.prod.yml exec keycloak \
     /opt/keycloak/bin/kcadm.sh get users --config /tmp/kcadm.config -r sorteo360 \
       --fields username --format csv --noquotes
   ```
5. Borrar la sesión de `kcadm`:
   ```bash
   docker compose -f docker-compose.prod.yml exec keycloak rm -f /tmp/kcadm.config
   ```
6. Seguir con el resto de §4.4, punto 4, de `plans/pruebas-y-produccion.md`: el administrador
   nominal, las URLs del cliente, la política de contraseñas, la fuerza bruta, el SMTP del realm,
   los tiempos de sesión y los usuarios reales (cada uno con el atributo `account_id` de abajo).

Lo mismo se puede hacer a mano en `https://${AUTH_DOMAIN}/admin` → realm `sorteo360` → **Users**
→ cada usuario → **Delete**, pero eso ya exige el dominio abierto o un túnel SSH.

El realm se importa **una sola vez** (ADR-0016): un `docker compose down` sin `-v` no los vuelve a
crear, y un `down -v` borra la base y obliga a repetir esta sección.

- **Atributo `account_id` obligatorio (`R6`)**: el servicio `events-service` falla cerrado (`403 ACCOUNT_NOT_ACTIVE`) para cualquier usuario autenticado cuyo token no incluya un claim `account_id` válido correspondiente a una cuenta activa en `events.account`.
  - Antes del despliegue, asignar el atributo de usuario `account_id = 00000000-0000-0000-0000-000000000001` en la consola de Keycloak (realm `sorteo360`) a todos los usuarios existentes de Banreservas (administradores, operadores, puerta y proyector).
  - Las cuentas nuevas creadas mediante `/crear-cuenta` + `/activar/:token` reciben este atributo automáticamente.
- **Política de contraseñas y bloqueo por fuerza bruta (`S3`)**: en un realm `sorteo360` ya importado, confirmar en la consola de Keycloak que:
  - El cliente `sorteo360-app` tiene **Direct access grants = OFF** (solo Standard Flow + PKCE).
  - **Authentication → Policies → Password Policy** incluye `length(10) and digits(1) and notUsername and notEmail`.
  - **Realm Settings → Security Defenses → Brute Force Detection** está activado (`failureFactor = 5`, espera progresiva).

## 7. Certificado interno de PostgreSQL

El contenedor `postgres-certs` (`infra/postgres-setup/certs.sh`) corre en cada arranque, antes que
PostgreSQL. La primera vez crea una CA propia del entorno (diez años) y un certificado para
`DNS:postgres` (dos años); después solo renueva el certificado del servidor cuando le quedan menos
de 45 días. La CA no se reemplaza nunca: los servicios confían en ella.

Para que no venza entre despliegues, conviene correrlo una vez al mes desde cron:

```bash
docker compose -f docker-compose.prod.yml --env-file .env.prod run --rm postgres-certs
```

### Si la renovación del certificado de postgres falla

El contenedor sale con error cuando, al terminar, el certificado vence en menos de 45 días.

1. Leer el motivo: `docker compose -f docker-compose.prod.yml logs postgres-certs`.
2. Revisar que los volúmenes `postgres-tls` y `postgres-ca` existan y que el disco no esté lleno.
3. Volver a correr el contenedor (comando de arriba) y reiniciar `postgres` para que tome el
   certificado nuevo: `docker compose -f docker-compose.prod.yml restart postgres`.
4. Si la CA se perdió (volumen borrado), el contenedor crea una nueva: hay que reiniciar también
   `events-service`, `convocatorias-service`, `notification-service` y `keycloak` para que
   confíen en ella.
