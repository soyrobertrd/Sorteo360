# Sorteo360

Plataforma de gestión de eventos, registro y sorteos corporativos en vivo con check-in QR,
importación de participantes (Excel/CSV/TXT), webhooks, ruleta en tiempo real, entrega de premios
y acta notarial en PDF.

La orquestación local usa Docker Compose. Para despliegue en servidor ver
[`docs/deploy-sorteo360.md`](docs/deploy-sorteo360.md) y `docker-compose.prod.yml`.

## Requisitos

- Docker y Docker Compose v2.24.4 o superior. Verificar con `docker compose version`.
- Opcional (solo compilación fuera de contenedores): .NET 10 SDK.

## Estructura del repositorio

```
.
├── docker-compose.yml            # Stack base local: infraestructura + servicios
├── docker-compose.override.yml   # Override local (se carga automáticamente en dev)
├── docker-compose.prod.yml       # Stack de producción con Caddy (HTTPS automático)
├── .env.example                  # Plantilla de variables para local
├── .env.prod.example             # Plantilla de variables para producción
├── Directory.Packages.props      # Versiones de paquetes NuGet centralizadas
├── Directory.Build.props         # Propiedades MSBuild comunes
├── Sorteo360.Platform.slnx   # Solución completa de backend (.NET 10)
├── infra/postgres/init.sh        # Bootstrap inicial: crea las DBs vacías por servicio
├── infra/keycloak/               # Realm sorteo360 importado al iniciar Keycloak en dev
├── infra/keycloak-themes/        # Tema de login Sorteo360 montado en Keycloak
├── infra/caddy/Caddyfile         # Reverse proxy HTTPS para producción
├── scripts/backup-postgres.sh    # Script de respaldo y limpieza para Postgres en prod
├── shared/                       # Chasis compartido (.NET Platform chassis)
├── events-service/               # Eventos, padrón de participantes, sorteos, actas PDF
├── convocatorias-service/        # Invitaciones, portal de registro público, check-in QR
├── notification-service/         # Puerta SMTP e historial de envíos
└── frontend/                     # SPA (Vite + React) servida por nginx
```

## Funcionalidades principales

- **Eventos (`events-service`):** creación en `planning`, inicio (`in_progress`), cierre y
  cancelación, imágenes de evento y switches públicos/privados de módulos.
- **Sorteos (`events-service`):** carga masiva de participantes desde Excel/CSV/TXT, integración
  por API/webhooks, sincronización de asistencia QR, ruleta por premio, anulación y
  reactivación de ganadores, entrega de premios con firma y acta notarial PDF.
- **Convocatorias y Check-in QR (`convocatorias-service`):** campañas de invitación, portal
  público de registro con QR y escaneo en puerta.
- **Notificaciones (`notification-service`):** envío SMTP (Mailpit en local, relay SMTP en
  producción) para correos al ganador e invitaciones.

## Arranque local con Docker Compose

```bash
cp .env.example .env
docker compose up -d --build
```

Para ver logs o reiniciar un servicio:

```bash
docker compose logs -f events-service
docker compose build events-service && docker compose up -d events-service
docker compose down -v   # borra volúmenes y re-ejecuta init.sh
```

### Perfiles (`COMPOSE_PROFILES`)

- La infraestructura (`postgres`, `keycloak`, `mailpit`) siempre arranca.
- Cada servicio pertenece a `[all, <servicio>]` (`events`, `convocatorias`, `notifications`, `frontend`).
- `COMPOSE_PROFILES=all,tools` incluye Adminer en `http://localhost:8081`.

## Mapa de servicios y puertos (local)

| Servicio | Dirección interna | Puerto host (`.env`) | Notas |
| --- | --- | --- | --- |
| `events-service` | `http://events-service:8080` | `EVENTS_PORT` (5170) | API de eventos y sorteos; OpenAPI/Scalar en `/scalar` |
| `convocatorias-service` | `http://convocatorias-service:8080` | `CONVOCATORIAS_PORT` (5185) | Campañas, registro público y check-in QR |
| `notification-service` | `http://notification-service:8080` | `NOTIFICATIONS_PORT` (5190) | Puerta SMTP e historial |
| `mailpit` | `mailpit:1025` (SMTP) | `MAILPIT_UI_PORT` (8025) | Bandeja web de pruebas en desarrollo |
| `frontend` | `http://frontend:8080` | `FRONTEND_PORT` (5175) | SPA React/nginx |
| `postgres` | `postgres:5432` | `POSTGRES_PORT` (5432) | PostgreSQL 17 con TLS obligatorio |
| `keycloak` | `http://keycloak:8080` | `KEYCLOAK_PORT` (8082) | Realm `sorteo360`, cliente `sorteo360-app` |
| `adminer` | `http://adminer:8080` | `ADMINER_PORT` (8081) | Solo perfil `tools` |

## Base de datos y aislamiento TLS

Cada servicio usa su propio login en PostgreSQL (`events_app`, `convocatorias_app`, `notifications_app`,
`keycloak_app`), provisionado automáticamente por `postgres-roles` (`infra/postgres-setup/roles.sh`).
Toda conexión requiere TLS (`infra/postgres-setup/certs.sh`). Conectar desde host:

```bash
psql "host=localhost port=5432 user=sorteo360 dbname=postgres sslmode=require"
```

Cada servicio aplica sus migraciones EF Core pendientes automáticamente al arrancar.

## Compilación y tests (en contenedor .NET 10)

```bash
docker run --rm -v "${PWD}:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet build Sorteo360.Platform.slnx

docker run --rm -v "${PWD}:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet test shared/Sorteo360.Platform.Tests.Unit
```

Verificar tema de login de Keycloak en desarrollo:

```bash
curl -s "http://localhost:8082/realms/sorteo360/protocol/openid-connect/auth?client_id=sorteo360-app&response_type=code&scope=openid&redirect_uri=http://localhost:5175/" | grep -o 'href="[^"]*\.css"'
```

Para despliegue en producción, HTTPS con Caddy y respaldos de PostgreSQL, consultar
[`docs/deploy-sorteo360.md`](docs/deploy-sorteo360.md).
