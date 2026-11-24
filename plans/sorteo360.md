# Sorteo360: arquitectura recomendada y plan

> ⚠ No borrar este archivo. El 2026-11-18 alguien borró `plans/` entera, este incluido (no
> estaba en git), y se recreó. Borrar archivos viejos de `plans/` está bien; este no.

Sistema de rifas para la fiesta de Navidad de los empleados del cliente. Sale de PGTI360 hacia un
**repositorio nuevo**. Marca: Sorteo360, colores del logo del cliente (azul marino `#0d3b7d`,
azul cielo `#1ea2dc`), personalizables en Configuración → Marca.

**No hacer commit ni push** sin que Robert lo pida explícitamente.

## 1. Punto de partida

| Pieza de PGTI360                                                  | Qué aporta                                                                                                                                      | Destino                      |
| ----------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------- |
| Motor del sorteo (`events-service/.../Raffles`)                   | Bombo congelado, rondas, premios, sorteo con `RandomNumberGenerator`, anulación, entrega con firma, código de retiro, acta en PDF, certificados | **Se conserva entero**       |
| Lector de hojas (`convocatorias-service/.../Uploads`)             | Excel/CSV con límites y protección contra zip bombs                                                                                             | Se reutiliza para el padrón  |
| QR (`QrImageRenderer`), escáner PWA                               | Gafete y lectura con cámara                                                                                                                     | Se conservan                 |
| Chasis `shared/Sorteo360.Platform`                            | Errores con `code`, auth, archivos, health, Serilog                                                                                             | Se integra al proyecto nuevo |
| Eventos                                                           | Fecha, lugar, información                                                                                                                       | Solo lo básico, oculto       |
| Convocatorias                                                     | Campañas, portal de registro                                                                                                                    | Oculto; hoy depende del CRM  |
| CRM, proveedores, cotizaciones, pagos, viáticos, FirmaGOB, Ferias | Gestión del ministerio                                                                                                                          | **Fuera**                    |

## 2. Opciones de arquitectura

|                                                | A. Microservicios recortados | **B. Monolito modular (recomendada)** | C. Solo frontend |
| ---------------------------------------------- | ---------------------------- | ------------------------------------- | ---------------- |
| Esfuerzo                                       | Alto                         | Medio                                 | Bajo             |
| API y webhook                                  | Sí                           | Sí                                    | No               |
| Varias pantallas (puerta, operador, proyector) | Sí                           | Sí                                    | No               |
| Operación                                      | 4 contenedores + Keycloak    | 1 API + Postgres + SPA                | Hosting estático |

Recomendación: **B** después de la entrega del miércoles (ver §7).

## 3. Arquitectura recomendada (después del miércoles)

Una API .NET (`Sorteo360.Api`) con módulos Events, Participants, CheckIn, Raffles (el motor
actual), Branding, Integrations y Notifications (outbox); una base PostgreSQL; la SPA; OIDC con
Keycloak o Entra ID. SignalR para el proyector. Estructura `src/` (Api, Core, Infrastructure),
`tests/`, `web/`, `deploy/`, `docs/`.

Participante: `employee_code` como clave natural (upsert por evento + código), `qr_token` de 128
bits, cédula opcional cifrada, `department`/`branch` para reglas.

Reglas configurables por sorteo, escritas en el acta: debe tener check-in, excluido del padrón,
exclusión/inclusión manual, un premio por persona, un premio por departamento por ronda, ausente
al ser llamado → anular y re-sortear. Fuera: CRM, `walk_in`, DO Travel Awards y **dominio `.do`**
(dejaría fuera a todos los empleados dominicanos).

## 4. Mejoras

Imprescindibles: branding en el servidor; pantalla de proyector separada; vista previa de la
carga con errores por fila; check-in con cola sin conexión; QR aleatorio sin datos personales;
webhook/API con clave + HMAC + idempotencia; ensayo general.
Recomendadas: sorteo verificable (hash del bombo y cadena de sorteos); Ley 172-13 (cifrado,
bitácora, purga); modo contingencia (exportar bombo y acta); SSO; roles simples.
Después: multi-cliente, invitaciones sobre el padrón, WhatsApp/SMS.

## 5. Migración al repo nuevo

Historial nuevo (`git init`); renombrar `Sorteo360.*` → `Sorteo360.*` en la misma pasada;
migraciones desde cero; portar los tests del sorteo primero; CI mínimo. Confirmar con el
ministerio que se puede reutilizar el código.

## 6. Respuestas (Robert, 2026-11-18)

- **Entrega: miércoles 25-nov.** Jueves 26: primer sorteo grabado. Fiesta sin fecha.
- Alojamiento: servidor de Robert por ahora. Padrón: no se sabe cómo llega (archivo, API y webhook).
- Tamaño desconocido; probable ofrecerlo a otros clientes. Login: usuarios nuevos creados por Robert.

## 7. Entrega del miércoles

Se entrega sobre el árbol actual. Dos caminos:

- **Red de seguridad (Agente B):** la SPA de punta a punta en modo `mock` con usuarios locales
  (`VITE_API_MODE=mock`, `VITE_AUTH_MODE=local`).
- **Camino real (Agente A):** backend con el padrón persistido, sin CRM; SPA en modo `http`.

## 8. Reparto en dos agentes

|              | Agente A: backend                                                                                                                                                 | Agente B: frontend (esta sesión)                         |
| ------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------- |
| Carpetas     | `events-service/`, `convocatorias-service/`, `notification-service/`, `shared/`, `infra/`, `docker-compose*.yml`, `.env.example`, `docs/`, `scripts/`, `.github/` | `frontend/`                                              |
| Verificación | build + Tests.Unit en contenedor `mcr.microsoft.com/dotnet/sdk:10.0`                                                                                              | `npm run build && npm run lint && npm test` (Node 20/22) |

### 8.1 Contrato compartido

**Se adopta el contrato que ya está implementado en los dos lados** (endpoint
`RaffleParticipantWebhookEndpoints.cs` en events-service, hoy con un store en memoria, y
`listRaffleParticipants`/`importRaffleParticipants`/... en `frontend/src/api`). Reescribirlo a
otra forma cuesta días que no hay. A lo persiste sin cambiar rutas ni formas.

Todo bajo `/v1/events/{eventId}/raffle`, JSON camelCase, errores ProblemDetails con `code`.

```
RaffleParticipantInput { fullName*, email?, company?, department?, employeeCode?, phone?, qrCode?, checkedIn? }
RaffleParticipantItem  { id, fullName, email, company, department, employeeCode, phone, qrCode,
                         checkedIn, checkedInAt, source: 'convocatoria'|'excel'|'csv'|'txt'|'manual'|'api'|'webhook',
                         qualified, override: 'included'|'excluded'|null }
ImportRequest  { participants: RaffleParticipantInput[], replaceExisting?, markCheckedIn?, source? }
ImportResponse { imported, updated, total, participants: RaffleParticipantItem[] }

GET    participants                       → RaffleParticipantItem[]
POST   participants/import                ImportRequest → ImportResponse          (raffles:write)
POST   participants/check-in              { code | qrCode | email | employeeCode | registrationIdOrCode, checkedIn } → RaffleParticipantItem
POST   participants/check-in-bulk         { checkedIn } → 204
POST   participants/remove                { email } → 204   (también DELETE participants?email=)
POST   webhooks/participants              anónimo con clave → ImportResponse
POST   webhooks/check-in                  anónimo con clave → RaffleParticipantItem
```

Identidad del participante: `employeeCode` si viene; si no, `email`. El archivo (Excel/CSV/TXT) lo
lee el navegador y manda JSON a `import`: la vista previa y los errores por fila son del
frontend.

**Contrato adicional ya implementado en el backend (listo para B3 y modo `http`):**

- **Patrocinador como texto (`B3`)**:
  - `POST /v1/events/{eventId}/raffle/prizes` y `PATCH /v1/events/{eventId}/raffle/prizes/{prizeId}` aceptan `sponsorName?: string | null` (texto libre, sin requerir `sponsorCompanyId` ni `sponsorContactId`).
  - `PrizeResponse.sponsor` devuelve `{ companyId: string | null, companyName: string | null, contactId: string | null, contactName: string | null, logo: PrizeSponsorLogoResponse | null }` (o `null` si no hay patrocinador; con patrocinador en texto viene `companyId: null` y `companyName: "<sponsorName>"`).
  - `PUT|DELETE /v1/events/{eventId}/raffle/prizes/{prizeId}/sponsor/logo` funciona también con patrocinador en texto (sin `companyId`).
- **Reglas del sorteo (`B3`)**:
  - `PATCH /v1/events/{eventId}/raffle` acepta los campos planos (`onePrizePerPerson`, `onePrizePerCompany`, `onePrizePerDepartmentPerRound`) y/o el objeto `rules: { requireCheckIn?, onePrizePerPerson?, onePrizePerDepartmentPerRound? }`. En persistencia, `onePrizePerDepartmentPerRound` se almacena sobre `one_prize_per_company` y `requireCheckIn` es siempre `true`.
  - `RaffleResponse` devuelve `onePrizePerPerson`, `onePrizePerCompany`, `onePrizePerDepartmentPerRound` y `rules: { requireCheckIn: true, onePrizePerPerson, onePrizePerDepartmentPerRound }`.
- **Motivos de exclusión sin CRM**: `RaffleParticipantDto.exclusionReason` devuelve `'manually_excluded' | 'not_checked_in' | 'already_won' | 'same_department' | null`; `ticketNumber` en formato `000-000`.
- **Webhook**: clave en `X-Webhook-Key` / `X-Api-Key` / `Authorization: Bearer <key>` + firma HMAC-SHA256 opcional en `X-Webhook-Signature` / `X-Callback-Signature` (`t=<epoch>,v1=<hex>`, ventana de 5 min).
- **Branding**: `GET|PUT /v1/branding` (GET anónimo) y `GET|PUT /v1/events/{eventId}/raffle/branding`.
- **Bitácora y reactivación**: `drawnByName`, `voidedByName`, `redeemedByName` en `PrizeDrawSummary`/`Void`/`Handover`; `POST /v1/events/{eventId}/raffle/attendees/{attendeeId}/reinstate` y `RaffleResponse.reinstatedAttendeeIds`.

### 8.2 Agente A: backend

Prioridad miércoles: A0 → A5.

- [x] A0. Compilar en contenedor SDK. Revisar `RaffleParticipantWebhookEndpoints.cs` (ya persistente
      en Postgres; arreglados constructores primarios en `EventsDocumentTransformer` y
      `KeycloakAuthenticationExtensions`).
- [x] A1. Padrón persistido en Postgres (`events.raffle_participant`, `RaffleParticipant`, EF,
      migración `20261118170732_AddRaffleParticipantRosterAndSponsorName`) detrás de §8.1.
- [x] A2. Upsert por `employeeCode`/`email`; `replaceExisting` transaccional en
      `RaffleParticipantRepository.ImportAsync`.
- [x] A3. Webhook con clave por evento/global en tiempo constante (`RaffleWebhookSignature`) + HMAC-SHA256
      (`t=<epoch>,v1=<hex>`, tolerancia 5 min); `AnonymousRoutesTests` y `OpenApiDocumentTests` verdes.
- [x] A4. QR PNG (`GET participants/{code}/qr`) y gafetes PDF (`GET participants/badges.pdf`) con
      `RaffleBadgeRenderer`.
- [x] A5. `RosterAttendanceSource : IAttendanceSource` sobre el padrón; reglas sin CRM obligatorio ni `.do`
      ni exclusión de `WalkIn`; patrocinador en texto (`sponsorName` en premios/logo/acta); tests unitarios.
- [x] A6. Fuera CRM del backend (events y convocatorias); quitar `Services__Crm__BaseUrl`.
- [x] A7. Fuera proveedores, cotizaciones, aprobadores, FirmaGOB, evaluaciones, relevo de tokens.
- [x] A8. Branding en el servidor.
- [x] A9. Realm con roles simples y sin usuarios del ministerio; quitar `CRM_SERVICE_CLIENT_SECRET`.
- [x] A10. Despliegue en el servidor de Robert (HTTPS, backup).

Tareas nuevas (2026-11-18, pedidas por Robert; el frontend ya va por delante en todas):

- [x] A11. **Tipos de evento genéricos.** `EventType` pasa a `Corporate`, `Christmas`, `Wedding`,
      `Birthday`, `Concert`, `Graduation`, `Conference`, `Other` (antes `Roadshow`, `Feria`,
      `Tradeshow`, `Webinar`, `RuedaDePrensa`). Migración que traduce las filas existentes
      (`Roadshow`/`Feria`/`Tradeshow` → `Corporate`, `Webinar` → `Conference`, `RuedaDePrensa` →
      `Other`) y validadores. El frontend ya manda los valores nuevos (`frontend/src/api/types.ts`).
- [x] A12. **Fuera MiTur / PGTI de todo el backend y la infraestructura.** Namespaces C#
      `Sorteo360.*` → `Sorteo360.*` (1,353 archivos `.cs` incluyendo `ModelSnapshot` y
      migraciones), textos de correos/PDF/acta ("Ministerio de Turismo", "MITUR"), dominios
      `mitur.gob.do`, prefijo de referencias `MITUR-` → `REF-`, red `sorteo360-net`, imágenes
      `${REGISTRY:-sorteo360}`, `POSTGRES_USER=sorteo360`, comentarios con tickets `PGTI-*` en
      compose/`.env`, tema de login `sorteo360.css` y raíz (`CLAUDE.md`, `README.md`,
      `GLOSSARY*.md`, `events-service/CLAUDE.md`). _(Los nombres de carpeta/archivo `.csproj`/`.slnx`
      conservan el prefijo en disco para no romper rutas de Dockerfiles/CI)._
- [x] A13. **Realm `sorteo360` y cliente `sorteo360-app`.** El frontend ya los usa por omisión
      (`frontend/src/lib/config.ts`, `.env.*`); hoy el realm se llama `pgti360` y en modo `http` el
      login falla hasta renombrarlo. Renombrar también `pgti360-realm.json`, el tema de login
      `pgti360` y las variables `Keycloak__Authority` en ambos compose. Va junto con A9.
- [x] A14. **Interruptores de funciones fuera del CRM.** Vivían en crm-service (ADR-0039), que ya
      no existe: `GET/PUT /v1/feature-switches` en events-service, más una lectura **anónima**
      `GET /v1/public/feature-switches` (o equivalente) que el portal público usa para saber si
      `convocatorias` está encendido. Sin ella el portal público queda oculto en modo `http` (el
      frontend falla cerrado). Agregar la ruta a nginx/Vite es de B.
- [x] A15. **Fuera notificaciones y tareas del CRM.** Quitar `/v1/tasks`, los avisos de tareas y la
      bandeja; el frontend ya no tiene campana ni módulo de tareas. notification-service se queda
      solo para el correo al ganador y las invitaciones.
- [x] A16. **Acta y correo al ganador con el nombre del cliente.** El acta en PDF
      (`MigraDocNotarialRecordRenderer`) y la plantilla del correo al ganador deben tomar la
      empresa u organizador del evento (branding por evento, o el global de A8), no un texto fijo.
- [x] A17. **Sorteo sin tómbola.** `ball_tombola` sigue en el contrato pero el frontend ya no lo
      ofrece; un sorteo guardado con él se muestra como ruleta. Migración que lo pasa a `roulette`
      y validador que deje de aceptarlo.
- [x] A18. **Crear sorteo sin evento visible.** Si el módulo de eventos está apagado, el frontend
      ya no deja vincular un sorteo a un evento existente: crea el evento mínimo en el mismo paso.
      Hoy ese alta exige `optOfficeId` (CRM): debe dejar de exigirlo (va con A6).
- [x] A19. **Reactivar a un anulado** (pedido de Robert, 2026-11-18). Si pasan los 60 s sin
      confirmar la presencia, el frontend anula solo el sorteo con `code: 'absent'` (ruta de
      anulación existente); el bombo ya deja fuera a todo anulado en los premios siguientes
      (spec raffles/02 §1). Implementado:
      `POST /v1/events/{eventId}/raffle/attendees/{attendeeId}/reinstate` (`raffles:write`, solo
      `in_progress`, `409 RAFFLE_ATTENDEE_NOT_VOIDED` si no tiene anulaciones) devolviendo
      `RaffleResponse` con `reinstatedAttendeeIds: string[]`; el bombo excluye anulados salvo
      reactivados (una nueva anulación vuelve a excluir); el acta PDF lista las reactivaciones.
- [x] A20. **Número de boleto.** `RaffleParticipantItem.ticketNumber`: seis dígitos al azar con
      guion (`482-917`), con generador criptográfico (`TicketNumberGenerator`), único por sorteo
      (índice único `ux_raffle_participant_event_ticket_number`), asignado al crear el participante
      (archivo, API o webhook) y a los existentes en migración `20261119130955_AddRaffleParticipantTicketNumber`.
      Se imprime en los gafetes PDF (`Boleto #482-917`).
- [x] A21. **Quién lo hizo, para la bitácora.** `PrizeDrawSummary.drawnByName`,
      `PrizeDrawVoidSummary.voidedByName` y `PrizeHandoverSummary.redeemedByName` resueltos vía
      `IRaffleOperatorDirectory` (degradación limpia a `null` si Keycloak no responde) en todas las
      respuestas de `RaffleEndpoints` y `PrizeDrawEndpoints`.
- [x] A22. **Sin internet en el salón.** Añadidos `docker-compose.salon.yml` e
      `infra/caddy/Caddyfile.salon` (`tls internal`, sin SMTP externo) y guía completa de
      instalación previa, operación sin internet en laptop/mini-PC y respaldos Postgres antes/después
      en `docs/deploy-sorteo360.md` §5.
- [x] A23. **Fuera OPT del backend.** El frontend ya no manda `optOfficeId`/`optContactId` ni lee
      `opt`/`optContact`. Quitado de `events-service` `IOptDirectory`, `OptLookup`, `OptErrors`,
      `OptRefResponse`, `CachedOptDirectory`, `CrmOptClient`, el filtro `OptOfficeId` y las columnas
      `opt_office_id`/`opt_contact_id` (migración `20261119153205_DropEventOptColumns`); y de
      `convocatorias-service` toda la carpeta `SupplierIntake/`, los endpoints/validadores/tests de
      carga de proveedores, las consultas de OPT/empresas en `Crm/`, el rol `supplier-intake:manage`
      (realm, `docs/roles.md` y políticas) y sus tablas (migración `20261119155557_DropSupplierIntakeTables`).

### 8.3 Agente B: frontend

Prioridad miércoles: B0 → B5.

- [x] B0. Build, lint y tests en verde (2026-11-18: 297 tests, 0 errores de lint).
- [x] B1. Padrón en el sorteo. La carga lee Excel/CSV/TXT con `features/sorteo/participantFile.ts`
      (puro, con tests): Nombre + Apellido, alias en español, CSV con comillas y BOM, filas sin
      nombre, correos inválidos y códigos repetidos informados por fila con descarga CSV. Tabla,
      check-in, exclusión y modal API/webhook ya existían. Probado en el navegador (modo mock).
- [x] B2. Check-in: el escáner del sorteo usa `participants/check-in`; sin red, la lectura queda en
      una cola local (`features/sorteo/colaCheckin.ts`, con tests) y se manda sola al volver la red.
      Gafetes con QR y número de boleto.
- [ ] B3. Reglas del sorteo y patrocinador en texto (cuando A defina el contrato).
- [x] B4. Pantalla de proyector `/proyector/:eventId`, sin controles, alimentada por la consola con
      `BroadcastChannel` (misma laptop, sin red).
- [x] B5a. Lanzamiento en vivo en el modal de 3 pasos de Ferias (`components/ui/StepperModal.tsx`):
      formato, pantalla y presentación. Solo ruleta y nombres pasando; la tómbola se quitó de la
      animación (un sorteo guardado con ella gira como ruleta). e2e del sorteo actualizados.
- [ ] B5. Modo de entrega: usuarios locales de Robert, roles simples, recorrido completo probado
      (evento → Excel → check-in → sorteo → ganador → acta).
- [x] B6. Fuera CRM y OPT del frontend: 85 métodos de CRM/proveedores y los 4 de OPT de
      `ApiClient`, `http.ts` y el mock; `features/opt` borrado; `opt`/`optContact`/`optOfficeId`
      fuera de los tipos de evento y de todas las pantallas (filtros, columnas, formulario de
      evento, configuración); 118 declaraciones sin uso fuera de `types.ts`, `seed.ts` y
      `labels.ts`. Semilla del mock: 12 fiestas navideñas; la clave pasa a
      `sorteo360.mock.state.v2` (descarta lo guardado con eventos del sistema anterior).
- [x] B10. Fuera cotizaciones del frontend: 18 métodos de `ApiClient` (http y mock), hooks,
      `cotizacionEstado`, `montoFinal`, `guardarMontoFinal`, el candado de venue del formulario de
      evento, validadores de proveedores/banco sin uso y `lib/money.ts`. Cerrar un evento en el mock
      ya no exige cotizaciones.
- [x] B11. Importar premios desde Excel/CSV (`features/sorteo/premioFile.ts`, con tests;
      `ImportarPremiosModal`): vista previa de las primeras filas, cada columna se asigna a
      Ronda / Premio / Valor (RD$), detecta encabezados, crea las rondas que falten y agrupa filas
      seguidas iguales en un solo `quantity`.
- [x] B7. Marca: sin MITUR/PGTI/Ministerio/Roadshow en `frontend/` (pantallas, claves de
      `localStorage` `sorteo360.*`, dominios, comentarios, README, CLAUDE.md); tipos de evento
      genéricos; colores solo por tokens (`bg-card`, `bg-muted`, `text-foreground`, `bg-brand*`) y
      `getBrandPalette()`/`hexToRgb()` para canvas, confeti y PDF; se quitó el panel de
      estadísticas falsas del dashboard de eventos.
- [x] B8. Premios por ronda en Lista (tabla) o Cuadrícula; centro de la ruleta sin texto y ruleta
      estilizada; certificado sin "departamento" inventado; campana y módulo de tareas fuera;
      portal público solo con convocatorias encendido (falla cerrado); sin vincular a evento
      existente si eventos está apagado; adjuntar archivos con `FileDropzone` (explorador y
      arrastrar y soltar); entrada directa al módulo cuando solo hay uno; `toast` en vez de
      `alert()`; foco atrapado en los modales; `xlsx` 0.20.3 (CDN de SheetJS, sin las CVE de 0.18.5).
- [x] B9. El mock se carga con `import()` solo en modo `mock` (`iniciarApi()` en `main.tsx`).

## 9. Registro, cuentas y planes (Sorteo360 como producto)

> **Encargo vigente del Agente A:** `plans/agente-a-cuentas-planes-escala.md` (catálogo con rondas y
> premios por plan, pago mensual/anual, rutas `/v1/accounts/*`, `prizes/import`, escala 25.000/10.000).
> Manda sobre lo que diga esta sección si chocan.

Pedido de Robert (2026-11-19): el landing (`/`) no entra directo a la plataforma. Lleva a un
**registro que activa al usuario**, con un **plan gratis de hasta 25 empleados** y **3 planes de
pago**. Esta sección es la lógica; nada está implementado. No toca la entrega de Banreservas del
miércoles: Banreservas queda como una cuenta más, sin límite.

### 9.1 Conceptos

- **Cuenta** (`Account`): la empresa cliente. Todo lo demás (eventos, sorteos, participantes,
  marca, usuarios) pertenece a una cuenta. Es el "multi-cliente" que §4 dejaba para después.
- **Usuario**: vive en Keycloak, como hoy. Lleva el atributo `account_id`, que viaja en el token
  como claim. Un usuario pertenece a una sola cuenta.
- **Plan**: un catálogo fijo en código (no en base de datos), con sus límites. La cuenta guarda
  qué plan tiene y desde cuándo.
- **Empleado** = **participante en el padrón de un sorteo**. "Hasta 25 empleados" es el tope de
  personas que entran a **un** sorteo, sin contar a los dados de baja (decidido por Robert,
  2026-11-19).

### 9.2 Planes (nombres fijados; topes de pago y precios, propuesta)

| Plan        | Participantes por sorteo | Sorteos                  | Usuarios | Modo proyector | Extras                                         |
| ----------- | ------------------------ | ------------------------ | -------- | -------------- | ---------------------------------------------- |
| Gratis      | **25**                   | **1 creado al mes**      | **1**    | **No**         | Marca de agua "Sorteo360" en el acta           |
| Básico      | 250                      | 3 activos a la vez       | 3        | Sí             | Su marca, sin marca de agua                    |
| Pro         | 1,000                    | 10 activos a la vez      | 10       | Sí             | + API/webhook, modo salón sin internet         |
| Avanzado    | Sin límite               | Sin límite               | Sin lím. | Sí             | + SSO, soporte; contrato (Banreservas va aquí) |

Fijado por Robert (2026-11-19): Gratis completo y los nombres Básico, Pro y Avanzado. Los topes de
los planes de pago y los precios siguen siendo propuesta. Claves en código: `free`, `basic`, `pro`,
`advanced`.

- **"1 sorteo al mes"** (Gratis) = sorteos **creados** en el mes calendario de la cuenta (zona
  `America/Santo_Domingo`). Uno borrado **sigue contando**: si no, crear-borrar-crear daría
  sorteos ilimitados. El cupo vuelve el día 1 a las 00:00.
- **"Sorteo activo"** (planes de pago) = en borrador o en curso; uno completado no cuenta.
- **Sin modo proyector** (Gratis): el sorteo se gira solo en la consola del operador; no se ofrece
  "Abrir pantalla del proyector" ni abre `/proyector/:eventId`. El proyector corre en el navegador
  (`BroadcastChannel`, B4) y no llama al servidor, así que el backend no lo puede bloquear: el
  servidor publica `features.projector` en `GET /v1/account` y el frontend lo respeta. Es una
  barrera comercial, no de seguridad; si hace falta que sea dura, la pantalla tendría que pedir
  un permiso al servidor al abrir (no se propone).

### 9.3 Registro y activación

1. `/crear-cuenta` (anónima): empresa, nombre, correo y aceptar términos. **No pide contraseña.**
   Con `?plan=basic|pro|advanced` llega con el plan elegido; sin él, Gratis.
2. `POST /v1/public/signup`: valida, crea la cuenta en estado `PendingActivation` y guarda un
   token de activación (aleatorio de 128 bits; en la base solo su hash; vence en 24 h). Manda el
   correo por notification-service con el enlace `/activar/{token}`. Responde lo mismo exista o no
   el correo (no revela quién tiene cuenta).
3. `/activar/:token` (anónima) pide **la contraseña, dos veces** (decidido por Robert,
   2026-11-19). `POST /v1/public/activate {token, password}`: si el token es válido y no venció,
   crea el usuario en Keycloak (Admin API, cuenta de servicio) con esa contraseña como credencial
   definitiva, el rol compuesto **administrador de la cuenta** y el atributo `account_id`; pasa la
   cuenta a `Active` y quema el token. La contraseña va directo a Keycloak en esa misma petición:
   **no se guarda ni aparece en logs ni en la bitácora**. La política (largo mínimo, etc.) es la
   del realm; si Keycloak la rechaza, responde 400 con el error en el campo `password` y el token
   **no** se quema. Después el usuario entra por el login de Keycloak normal.
4. Reenviar activación: `POST /v1/public/signup/resend {email}`, como máximo 1 por minuto y 5 por
   día por correo.
5. Un plan de pago elegido en el registro deja la cuenta activa en **Gratis** y abre una
   **solicitud de plan** (§9.5); el plan sube solo cuando se confirma la transferencia.

**El administrador de la cuenta ve todos los módulos** (decidido por Robert, 2026-11-19): el rol
compuesto que recibe al activar incluye todos los permisos de la plataforma dentro de su cuenta
(sorteos, participantes, check-in, premios y entrega, marca, usuarios, plan). Los interruptores de
módulos (eventos y convocatorias apagados) siguen mandando: "todos" son todos los encendidos. Los
usuarios que él invite (planes de pago) reciben roles más chicos (operador, puerta), como hoy.

### 9.4 Reglas que hace cumplir el backend

Todas devuelven ProblemDetails con `code` (regla de plataforma):

| Regla                                                          | Dónde                                              | `code`                          | HTTP |
| -------------------------------------------------------------- | -------------------------------------------------- | ------------------------------- | ---- |
| Padrón de un sorteo no pasa el tope del plan                   | importar, alta manual, API y webhook de participantes | `PLAN_PARTICIPANT_LIMIT`        | 409  |
| Sorteos activos no pasan el tope (planes de pago)              | crear sorteo/evento                                | `PLAN_ACTIVE_RAFFLE_LIMIT`      | 409  |
| Sorteos creados en el mes no pasan el tope (Gratis)            | crear sorteo/evento                                | `PLAN_MONTHLY_RAFFLE_LIMIT`     | 409  |
| Usuarios no pasan el tope                                      | invitar usuario                                    | `PLAN_USER_LIMIT`               | 409  |
| Función fuera del plan (API/webhook, marca)                    | endpoint de la función                             | `PLAN_FEATURE_NOT_INCLUDED`     | 403  |
| Proyector fuera del plan (Gratis)                              | solo frontend, por `features.projector` (§9.2)     | —                               | —    |
| Cuenta no activada o suspendida                                | todo lo autenticado                                | `ACCOUNT_NOT_ACTIVE`            | 403  |
| Correo ya registrado (solo en activación, nunca en signup)     | activate                                           | `ACCOUNT_EMAIL_TAKEN`           | 409  |
| Token de activación vencido o usado                            | activate                                           | `ACTIVATION_TOKEN_INVALID`      | 410  |
| Contraseña rechazada por la política del realm                 | activate                                           | `errors.password`               | 400  |
| Ya hay una solicitud de plan pendiente                         | pedir plan                                         | `PLAN_REQUEST_PENDING`          | 409  |

- Una importación que pasaría el tope **no entra a medias**: se rechaza entera y el error dice
  cuántos caben (`extensions.remaining`), para que la vista previa lo muestre antes de enviar.
- `PLAN_MONTHLY_RAFFLE_LIMIT` trae `extensions.resetsAt` (el día 1 siguiente) para que la pantalla
  diga "Tu próximo sorteo gratis se habilita el 1 de diciembre".
- El conteo mensual se guarda en la cuenta al crear (contador por mes), no se recalcula sobre los
  sorteos vivos, porque los borrados también cuentan.
- Bajar de plan no borra nada: lo que ya existe sigue, pero no se puede agregar más por encima del
  tope nuevo.
- Un sorteo ya iniciado no se corta por el plan: la tómbola está congelada.
- **Aislamiento**: toda consulta filtra por el `account_id` del token (filtro global de EF Core).
  Un id de otra cuenta responde 404, nunca 403. Las rutas públicas (portal de registro de
  invitados, consulta de premios) resuelven la cuenta por el evento.

### 9.5 Pagos: solicitud hasta confirmar la transferencia

Decidido por Robert (2026-11-19): un plan de pago **se registra como solicitud** y no se activa
hasta confirmar el pago por transferencia. Sin pasarela.

- **`PlanRequest`**: cuenta, plan pedido, estado (`Pending` → `Approved` | `Rejected` |
  `Cancelled`), fecha, quién la pidió, referencia de la transferencia (texto, opcional) y, al
  decidir, quién, cuándo y el motivo si se rechaza.
- **Quién la abre**: el registro con plan (§9.3, paso 5) o el administrador de la cuenta desde
  "Mi plan" (`POST /v1/account/plan-requests {plan, transferReference?}`). Una sola pendiente por
  cuenta (`PLAN_REQUEST_PENDING`); el administrador puede cancelarla.
- **Qué ve el cliente**: los datos bancarios para transferir (configuración de la plataforma), el
  monto del plan y el estado de su solicitud. Mientras está pendiente sigue en su plan actual.
- **Quién la confirma**: el superadministrador (rol `platform-admin`, fuera de las cuentas) lista
  las pendientes, revisa la transferencia y aprueba o rechaza
  (`POST /v1/admin/plan-requests/{id}/approve|reject`). Aprobar cambia el plan de la cuenta en la
  misma transacción y guarda el período pagado (desde/hasta).
- **Correos** (notification-service): al cliente cuando se abre, se aprueba o se rechaza; al
  superadministrador cuando entra una nueva.
- **Vencimiento**: al terminar el período pagado sin otra solicitud aprobada, la cuenta baja a
  Gratis (no se borra nada, §9.4). Aviso al cliente 7 días antes.

### 9.6 Dónde vive

Recomendado: un módulo `Accounts` en **events-service** (dueño de sorteos y participantes, que es
donde se cuentan los topes) con tablas `account`, `account_activation` y `account_id` en
`event` (los sorteos y participantes cuelgan del evento). Un servicio nuevo
obligaría a una llamada síncrona en cada importación para conocer el plan. Branding pasa de global
a por cuenta.

### 9.7 Tareas

Agente A (backend):

- [x] A24. `Account` + catálogo de planes + `account_id` en evento y marca, filtro global por
      cuenta, migración (las filas existentes van a una cuenta "Banreservas" en Avanzado).
- [x] A25. Registro y activación (`signup`, `activate` con contraseña, `resend`) con Keycloak Admin
      API, correo por notification-service y límites de frecuencia; claim `account_id` y rol
      compuesto de administrador de cuenta (todos los permisos) en el realm.
- [x] A26. Topes del plan (§9.4) en participantes (los cuatro caminos), sorteos y usuarios, con
      tests de integración.
- [x] A27. `GET /v1/account` (plan, uso, topes, `resetsAt` y `features`: `projector`, `api`,
      `branding`) para que el frontend muestre "18 de 25" y "1 de 1 este mes".
- [x] A28. Solicitudes de plan (§9.5): `PlanRequest`, pedir/cancelar desde la cuenta, listar,
      aprobar y rechazar como `platform-admin`, vencimiento del período pagado y correos.
- [x] A29. Campos visibles del participante (§10): `displayFields` por sorteo y `firstName`,
      `lastName` y `location` en el participante (importación, alta, API y webhook).

Agente B (frontend), después de A24-A27:

- [x] B12. Landing: sección de planes con selector mensual/anual y botones al registro
      (`features/landing/PlanesSeccion.tsx`, catálogo en `features/cuenta/planes.ts`).
- [x] B13. `/crear-cuenta` (sin contraseña), `/activar/:token` (contraseña dos veces), reenviar
      activación, `/terminos` y `/privacidad` (rutas anónimas; mock completo, http contra
      `/v1/accounts/*`). Textos legales en borrador hasta completar `features/legal/legalDatos.ts`.
- [ ] B14. Uso del plan en la plataforma ("18 de 25 participantes", "1 de 1 sorteo este mes"),
      aviso antes de importar por encima del tope, mensajes de los `code` nuevos en `labels.ts` y
      ocultar el modo proyector (botón y ruta) cuando `features.projector` es `false`. El mock
      replica los mismos topes.
- [ ] B15. "Mi plan": pedir un plan, datos de transferencia y estado de la solicitud; pantalla del
      superadministrador para aprobar o rechazar.
- [ ] B16. Campos visibles del participante (§10): elegirlos en el sorteo y aplicarlos en la
      consola, el proyector, el ganador y el historial; Nombre, Apellido y Ubicación en el mapeo
      del padrón.

### 9.8 Lo que Robert tiene que decidir

1. Precios de Básico, Pro y Avanzado (los topes, incluidas rondas y premios, son propuesta).
2. Datos bancarios para la transferencia.
3. Correo remitente y datos del titular para los textos legales (razón social, RNC, domicilio,
   correos), más la revisión de un abogado.

Ya decidido (2026-11-19): 25 participantes por sorteo; Gratis con 1 sorteo al mes, 1 usuario y
sin proyector; contraseña al activar; planes Básico, Pro y Avanzado; pago por transferencia como
solicitud; pago mensual o anual con descuento (anual = 10 meses); Gratis con 1 ronda y 10
premios, topes que suben por plan; el administrador ve todos los módulos, también Eventos y
Convocatorias; campos visibles del participante configurables.

## 10. Campos visibles del participante en la rifa

Pedido de Robert (2026-11-19): qué datos del participante se muestran en la rifa se elige por
sorteo. Tres huecos:

| Hueco         | Opciones                                          | Por omisión      |
| ------------- | ------------------------------------------------- | ---------------- |
| Nombre        | Nombre completo · Nombre y apellido · Solo nombre | Nombre completo  |
| Identificador | Número de boleto · ID de empleado · Ninguno       | Número de boleto |
| Detalle       | Departamento · Ubicación · Empresa · Ninguno      | Departamento     |

- **Dato**: el participante gana `firstName`, `lastName` y `location` (opcionales). `fullName`
  sigue obligatorio: si el archivo trae nombre y apellido separados y no el completo, se arma
  `firstName + " " + lastName`. Si faltan los separados, "Nombre y apellido" y "Solo nombre" caen
  a `fullName`. ID de empleado es `employeeCode`, que ya existe.
- **Configuración**: `displayFields {name, identifier, detail}` en el sorteo
  (`PUT /v1/events/{id}/raffle/display-fields`), editable hasta que el sorteo se inicia; después
  queda fija, como la tómbola.
- **Dónde se aplica**: consola del operador, pantalla del proyector, animación (ruleta y nombres
  pasando), tarjeta del ganador e historial de ganadores.
- **Dónde no**: el **acta notarial** y el certificado siempre llevan nombre completo, boleto e ID
  de empleado si lo hay, para identificar al ganador sin ambigüedad. El check-in sigue buscando
  por todos los campos.
- **Importación**: el mapeo de columnas del padrón suma Nombre, Apellido y Ubicación (alias:
  `nombres`, `apellidos`, `ubicacion`, `sucursal`, `localidad`, `oficina`).
- Si el hueco elegido viene vacío en un participante, no se muestra para él (nada de "—" en la
  pantalla grande).

## Handoff

- 2026-11-19, Agente A (cierre de **A33**, **A34**, **R13–R15**, **A31 parcial** y realm de producción):
    - **A33 (Tablero de ganadores)**: `PrizeDrawSummary` expone `winnerTicketNumber`, `winnerEmployeeCode` y `winnerDepartment` poblados desde `events.raffle_participant` en `RaffleRepository.ListDrawsAsync`; nuevo endpoint `POST /v1/events/{eventId}/raffle/prizes/draw-batch` (`DrawBatchPrizesRequest`, máx. 10 premios en una transacción y una sola lectura de asistentes/sorteos — cierra también **R13**; devuelve `409 RAFFLE_POOL_EXHAUSTED` con `extensions.drawn` si el bombo se agota a mitad).
    - **A34 (Foto del premio)**: `PrizeData.ImageStoredFileId` (`image_stored_file_id` + FK/índice en `PrizeConfiguration`), `PrizeResponse.imageUrl` (`/v1/files/{id}/content`), `POST /v1/events/{eventId}/raffle/prizes/image` (multipart `file` + `prizeIds`, máx. 5 MB, preserva PNG/WebP) y `POST /v1/events/{eventId}/raffle/prizes/image/remove` (permitidos en `draft` e `in_progress`).
    - **R14**: `GET .../prizes/{prizeId}/eligible?limit=` (máx. 100) devuelve `count` completo y muestra aleatoria criptográfica de `participants`.
    - **R15**: Compresión Brotli/Gzip (`AddResponseCompression` / `UseResponseCompression`) en `events-service/Program.cs` y `ETag`/`If-None-Match` (304 Not Modified) en `GET /v1/events/{eventId}/raffle`.
    - **A31 (Escala)**: `AutoDetectChangesEnabled = false` en `RaffleParticipantRepository.ImportAsync`, `RequestSizeLimit(20 MB)` en `participants/import`, tope de 5.000 filas con `413 WEBHOOK_BATCH_TOO_LARGE` en `webhooks/participants`.
    - **Realm producción (`pruebas-y-produccion.md` §1.1)**: `infra/keycloak/sorteo360-realm.json` suma `https://${APP_DOMAIN}/*` a `redirectUris`, `https://${APP_DOMAIN}` a `webOrigins` y `post.logout.redirect.uris`.
    - **Pendiente al levantar Docker Desktop**: generar migración EF Core para `prize.image_stored_file_id` (`dotnet ef migrations add AddPrizeImageStoredFileId`), compilar/correr `Events.Tests.Unit` y `Events.Tests.Integration` en contenedor `mcr.microsoft.com/dotnet/sdk:10.0`, y añadir el benchmark `[Trait("Category","Scale")]` (25.000 / 10.000).

- 2026-11-19, Agente A (cierre de hallazgos **R1–R12**, **S1–S5** y P0 **A30**, **A32**, **A35**):
    - **Bloqueantes de entrega (`R1`, `R2`/`A35`, `R3`)**:
        - `R1`: `RaffleParticipant.RuleGroup` (`Department` antes que `Company`) compartido entre `RosterAttendanceSource` y `RaffleParticipantWebhookEndpoints.ResolveExclusionReason`.
        - `R2`/`A35`: `requires_check_in` (default `true`) en `events.raffle`, `Raffle`, `UpdateRaffleRequest` (nivel superior, sin `Rules` anidado) y `RaffleResponse`; `RosterAttendanceSource` y `CachedAttendance` incluyen todo el padrón activo cuando es `false` y `ResolveExclusionReason` omite `not_checked_in`.
        - `R3`: `KeycloakAuthenticationExtensions` vuelve a fallar al arrancar sin `Keycloak__Authority`/`Audience`, permite `Auth__Mode=LocalDev` solo en `Development` y nunca autentica peticiones anónimas sin cabecera.
    - **Aislamiento entre cuentas (`R4`, `R5`, `R6`, `R7`, `S4`, `S5`)**:
        - `R4`: `RaffleParticipantRepository` valida pertenencia del evento a la cuenta actual (404 si no pertenece) en listado, importación, check-in, check-in masivo, borrado, QR, gafetes y auditoría.
        - `R5`: `events.event.webhook_secret` por evento (autogenerado al crear + backfill en migración); `VerifyAndReadWebhookBodyAsync` resuelve cuenta y clave desde el evento y valida la función `api` del plan.
        - `R6`: `CurrentAccountAccessor` ya no cae a Banreservas; `RequireAccountClaimMiddleware` + `DatabaseAccountAccessChecker` responden `403 ACCOUNT_NOT_ACTIVE` ante claim ausente/inválido o cuenta inexistente/inactiva; documentado en `docs/deploy-sorteo360.md` §6.
        - `R7`/`S5`: filtro global por `account_id` en `BrandingSettingData`, `BrandingRepository` y `RaffleRepository.ReadRecordEventAsync` por cuenta; `GET /v1/branding` anónimo devuelve valores neutros salvo `?eventId=`.
        - `S4`: `events.stored_file.account_id` con filtro global en `EventsDbContext` y estampado automático en `SaveChangesAsync` (404 entre cuentas).
    - **Registro, planes y rendimiento (`R8`, `R9`, `R10`, `R11`, `R12`, `S1`, `S2`, `S3`, `A30`, `A32`)**:
        - `R8`: `AccountOnboardingRepository.ActivateLockedAsync` bloquea `events.account_activation` con `SELECT ... FOR UPDATE`; `KeycloakAccountProvisioner` reutiliza usuario en `409` si coincide `account_id`, busca por correo si falta `Location` (sin `Guid.NewGuid()`) y falla la activación si falla la asignación del rol.
        - `R9`: `RaffleRepository.AddAsync` bloquea la cuenta y chequea/incrementa `account_monthly_usage` con `INSERT ... ON CONFLICT DO UPDATE ... WHERE raffles_created < @tope RETURNING` en la misma transacción.
        - `R10`: `ReinstateAttendeeAsync` rechaza eventos cerrados/cancelados (`EVENT_NOT_EDITABLE`). `R11`: borrado archivo vacío `0.26.0`.
        - `S1`/`S2`: `SignupAsync` reutiliza activación pendiente con los mismos límites en silencio + `AddRateLimiter` por IP (10/min) en `/v1/accounts/*`; `ResendActivationAsync` responde 200 en silencio al aplicar límite.
        - `S3`: `infra/keycloak/sorteo360-realm.json` con `directAccessGrantsEnabled: false` en `sorteo360-app`, `passwordPolicy` (`length(10) and digits(1) and notUsername and notEmail`) y `bruteForceProtected: true`; validación alineada en `AccountOnboardingService`.
        - `R12`: `EligiblePool.For` y `NotarialRecordContent.ReinstatementsOf` optimizados a O(asistentes + sorteos) (25.000 asistentes / 10.000 sorteos en < 200 ms).
        - `A30`: `POST /v1/events/{eventId}/raffle/prizes/import` (1–10.000 premios en una transacción, crea `Ronda N`, `PLAN_ROUND_LIMIT` y `PLAN_PRIZE_LIMIT`). `A32`: `account_admin` en `docs/roles.md` y paridad verificada.
    - **Migración EF Core**: `events-service/Sorteo360.Events.Infrastructure/Database/Migrations/20261120013500_AddSecurityIsolationAndRaffleSettings.cs`.
    - **Verificado en contenedor `mcr.microsoft.com/dotnet/sdk:10.0` + PostgreSQL 17 (Testcontainers) + Alpine**:
        - `dotnet build events-service/Sorteo360.Events.slnx --no-restore` → **0 errores**
        - `Events.Tests.Unit` (incluyendo `ReviewFindingsRegressionTests` y `AccountOnboardingServiceTests`) → **733/733**
        - `Events.Tests.Integration`: `AnonymousRoutesTests` **1/1**, `OpenApiDocumentTests` **15/15**, `AuthorizationTests` **31/31**, `ReviewAndSecurityRegressionTests` **7/7**, `Database` **48/48**
        - `scripts/roles-parity.sh` → **OK — 24 roles**; `scripts/roles-parity.test.sh` → **22/22**.
    - **Pendiente siguiente**: A33 (completar `winnerTicketNumber`/`winnerEmployeeCode`/`winnerDepartment` y `draw-batch`), A34 (foto del premio), A31 (escala 25.000/10.000 y benchmark `[Trait("Category","Scale")]`), R13–R15.

- 2026-11-19, Agente A (cierre §9.7: A24, A25, A26 y A27 — **todas las tareas A0–A29 completadas**):
    - **Cuentas, catálogo de planes y aislamiento (`A24`)**: `PlanCatalog` alineado con `frontend/src/features/cuenta/planes.ts` (`MaxParticipantsPerRaffle`, `MaxActiveRaffles`, `MaxMonthlyRaffles`, `MaxRoundsPerRaffle`, `MaxPrizesPerRaffle`, `MaxUsers`, `ProjectorEnabled`, `ApiEnabled`, `CustomBrandingEnabled` y reinicio mensual en `America/Santo_Domingo`); `ICurrentAccount`/`CurrentAccountAccessor` (claim `account_id` con respaldo `Banreservas` `00000000-0000-0000-0000-000000000001`); columna `account_id` en `events.event` y `events.branding_setting` + filtro global EF Core por cuenta en `EventsDbContext` (`EventData`) y estampado automático en `EventRepository.CreateAsync`.
    - **Registro y activación (`A25`)**: `POST /v1/accounts/signup`, `POST /v1/accounts/signup/resend` (1/min y 5/día, `429 ACTIVATION_RESEND_RATE_LIMITED`) y `POST /v1/accounts/activate` (`410 ACTIVATION_TOKEN_INVALID`, `400 errors.password` sin quemar token, `409 ACCOUNT_EMAIL_TAKEN`), rutas anónimas alineadas exactamente con `frontend/src/api/http.ts`. Token de 128 bits almacenado solo como hash SHA-256 en `events.account_activation` (24 h); `KeycloakAccountProvisioner` crea el usuario en Keycloak Admin API con contraseña directa (nunca guardada ni logueada), atributo `account_id` y rol compuesto `account_admin`; al activar con plan pago abre `PlanRequest` en `Pending`; `AccountOnboardingNotifier` envía el enlace `/activar/{token}`. Añadidos el rol compuesto `account_admin` y el protocol mapper `account_id` en `infra/keycloak/sorteo360-realm.json` y `docs/roles.md`.
    - **Topes del plan (`A26`)**: `IPlanLimitGuard`/`PlanLimitGuard` + tabla `events.account_monthly_usage` (cuenta sorteos creados en el mes incluso si se borran): `PLAN_PARTICIPANT_LIMIT` (409 con `extensions.limit`/`remaining`, rechazo completo del lote en importación y webhook), `PLAN_MONTHLY_RAFFLE_LIMIT` (409 con `extensions.resetsAt`), `PLAN_ACTIVE_RAFFLE_LIMIT` (409), y `PLAN_FEATURE_NOT_INCLUDED` (403 en `PUT /v1/branding` cuando `!CustomBrandingEnabled`).
    - **Resumen de cuenta (`A27`)**: `GET /v1/account` devolviendo plan, uso (`activeRaffles`, `monthlyRafflesCreated`, `users`), topes, `resetsAt` y `features: { projector, api, branding }`.
    - **Migración EF Core**: `events-service/Sorteo360.Events.Infrastructure/Database/Migrations/20261120000800_AddAccountTenancyAndOnboarding.cs`.
    - **Verificado en contenedor `mcr.microsoft.com/dotnet/sdk:10.0` + Testcontainers/PostgreSQL**:
        - `dotnet build events-service/Sorteo360.Events.slnx --no-restore` → **0 errores**
        - `Events.Tests.Unit` (incluyendo `AccountOnboardingServiceTests`) → **723/723**
        - `Events.Tests.Integration`: `AnonymousRoutesTests` **1/1**, `OpenApiDocumentTests` **15/15**, `AuthorizationTests` **31/31**, `Database` migraciones/constraints sobre PostgreSQL real **48/48**.

- 2026-11-19, Agente A (A28 y A29):
    - **Solicitudes de plan por transferencia (`A28`, §9.5)**:
        - Dominio `Sorteo360.Events.Core.Accounts`: `PlanCatalog` (`free`, `basic`, `pro`, `advanced`), `Account` (cuenta por defecto `Banreservas` en `advanced` sembrada en migración), `PlanRequest` (`Pending` → `Approved` | `Rejected` | `Cancelled`), `PlanRequestErrors` (`PLAN_REQUEST_PENDING`, `PLAN_REQUEST_INVALID_PLAN`, `PLAN_REQUEST_NOT_PENDING`, `PLAN_REQUEST_REJECTION_REASON_REQUIRED`, `PLAN_REQUEST_INVALID_PERIOD`) y `PlanRequestService`.
        - Persistencia e infra: tablas `events.account` y `events.plan_request` con índice único parcial `ux_plan_request_account_pending` (`status = 'Pending'`), transacción atómica al aprobar (actualiza `PlanRequest` y sube `Account.Plan` + período pagado juntos), `PlanRequestNotifier` (avisos al cliente, alerta al superadmin y aviso 7 días antes de vencer vía `INotificationClient`, no bloqueante si está offline) y `PlanExpirationWorker` (baja cuentas vencidas a `free` sin borrar datos).
        - Endpoints (`PlanRequestEndpoints.cs`): `GET|POST /v1/account/plan-requests`, `POST /v1/account/plan-requests/{id}/cancel`, y bajo rol `platform-admin` (añadido a `AuthorizationPolicies.cs`, `infra/keycloak/sorteo360-realm.json` y `docs/roles.md`): `GET /v1/admin/plan-requests?status=`, `POST /v1/admin/plan-requests/{id}/approve` (`{ paidPeriodStart?, paidPeriodEnd? }`, default 30 días) y `POST /v1/admin/plan-requests/{id}/reject` (`{ reason }`).
    - **Campos visibles del participante (`A29`, §10)**:
        - `RaffleDisplayFields` (`name`: `full_name` | `first_and_last` | `first_name`; `identifier`: `ticket_number` | `employee_code` | `none`; `detail`: `department` | `location` | `company` | `none`) persistido en `events.raffle` (`display_name_mode`, `display_identifier_mode`, `display_detail_mode`), expuesto en `RaffleResponse.displayFields` y editable solo en borrador vía `PUT /v1/events/{eventId}/raffle/display-fields` (`raffles:write`, `409 RAFFLE_NOT_IN_DRAFT` una vez iniciado).
        - Participante (`events.raffle_participant`, `RaffleParticipant`, `RaffleParticipantInput`, `RaffleParticipantDto`, importación y webhook): nuevos campos opcionales `firstName`, `lastName` y `location` (alias en webhook: `firstName`/`first_name`/`nombres`, `lastName`/`last_name`/`apellido`/`apellidos`, `location`/`ubicacion`/`sucursal`/`localidad`/`oficina`); si falta `fullName` y llegan `firstName`/`lastName`, arma `fullName = firstName + " " + lastName`.
    - **Migración EF Core**: `events-service/Sorteo360.Events.Infrastructure/Database/Migrations/20261119181749_AddDisplayFieldsAndPlanRequests.cs`.
    - **Verificado en contenedor `mcr.microsoft.com/dotnet/sdk:10.0` + Testcontainers/PostgreSQL**:
        - `dotnet build events-service/Sorteo360.Events.slnx --no-restore` → **0 errores**
        - `Events.Tests.Unit` (incluyendo `PlanRequestServiceTests` y `RaffleDisplayFieldsTests`) → **716/716**
        - `Events.Tests.Integration`: `AnonymousRoutesTests` **1/1**, `OpenApiDocumentTests` **15/15**, `AuthorizationTests` **31/31**, `Database` migraciones/constraints sobre PostgreSQL real **48/48**.

- 2026-11-19, Agente A (A23 + pruebas de integración con PostgreSQL/Testcontainers):
    - **Fuera OPT y carga de proveedores (`A23`)**:
        - `events-service`: eliminados `OptDirectory/` (`IOptDirectory`, `OptLookup`, `OptOfficeInfo`), `OptErrors`, `CachedOptDirectory`, `CrmOptClient`, `CrmOptResponses`, `ICrmOptClient`, `OptDirectorySettings`, `OptRefResponse`, los campos `OptOfficeId`/`OptContactId` en `Event`, `EventData`, DTOs, requests, validadores y endpoints, y los tests exclusivos de OPT (`OptLookupTests`, `OptDirectory*Tests`). Generada migración `events-service/Sorteo360.Events.Infrastructure/Database/Migrations/20261119153205_DropEventOptColumns.cs` (borra `ix_event_opt_office`, `opt_contact_id` y `opt_office_id`).
        - `convocatorias-service`: eliminados `Core/SupplierIntake/`, `Web/SupplierIntakeEndpoints/`, `PublicSupplierIntake*`, validadores y tests de `SupplierIntake`, `CrmOptOffice`, `CrmOptStaff`, `CrmNamedCompany`, `ICrmCompanyDirectory`, `CrmCompanyDirectory`, `CrmCompanyContracts`, `IServiceSupplierDirectory`, `ServiceSupplierDirectory`, métodos OPT/empresa de `ICrmContactsClient`/`CrmContactsClient`/`DisabledCrmContactsClient`/`ICrmContactDirectory`/`CrmContactDirectory`, y el rol `supplier-intake:manage` de `AuthorizationPolicies.cs`, `infra/keycloak/sorteo360-realm.json` y `docs/roles.md`. Generada migración `convocatorias-service/Sorteo360.Convocatorias.Infrastructure/Database/Migrations/20261119155557_DropSupplierIntakeTables.cs` (borra `convocatorias.supplier_intake_send` y `convocatorias.supplier_intake_link`).
    - **Verificado en contenedor `mcr.microsoft.com/dotnet/sdk:10.0` + Testcontainers/PostgreSQL (socket Docker montado con `TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal`)**:
        - Build: `events-service/Sorteo360.Events.slnx` (**0 errores**), `convocatorias-service/Sorteo360.Convocatorias.slnx` (**0 errores**).
        - Unitarias: `Events.Tests.Unit` **702/702**, `Convocatorias.Tests.Unit` **540/540**.
        - Integración con PostgreSQL real (Testcontainers):
            - `Events.Tests.Integration`: corrida completa inicial **649/733**; tras actualizar `EventType.Corporate` en `FileEndpoints`, tipo histórico en `OneCityPerEventMigrationTests`/`RoadshowCityTimeZoneBackfillTests`, tabla `raffle_participant` en `RaffleConstraintTests` y `RosterAttendanceSource` en `AttendanceSourceRegistrationTests`, pasaron en verde `EventEndpointsTests` (**29/29**), `FileEndpoints` (**68/68**), `Database` migraciones y constraints (**48/48**), `AttendanceSourceRegistrationTests` (**1/1**), `PrizeHandoverEndpointsTests` (**36/36**), `PrizeDrawLookupEndpointsTests` (**13/13**) y `PrizeSponsorEndpointsTests` (**14/14**). Solo quedan 21 tests heredados en `RaffleEndpoints` que aún asertan reglas pre-A4 (`ConvocatoriasAttendanceSource`, exclusión de `walk_in`/`.do`/`contact_unresolved` y acta dorada previa a A4).
            - `Convocatorias.Tests.Integration`: `MigrationsTests` **25/25** (incluye `DropSupplierIntakeTables`).
        - Paridad de roles (Alpine): `scripts/roles-parity.sh` (**OK — 22 roles**), `scripts/roles-parity.test.sh` (**22 ok, 0 fallos**).

- 2026-11-19, Agente A (cierre §8.2: A20, A21 y A22):
    - **Número de boleto (`A20`)**: Añadido `TicketNumberGenerator` (`RandomNumberGenerator.GetInt32(0, 1_000_000)`, formato `000-000`, hasta 100 reintentos ante colisión), columna `ticket_number` (`varchar(7)`) + índice único `ux_raffle_participant_event_ticket_number` en `events.raffle_participant`, asignación automática al importar/crear participantes (y búsqueda por boleto en check-in), campo `ticketNumber` en `RaffleParticipantDto`, impresión `Boleto #000-000` en `RaffleBadgeRenderer`, y migración `events-service/Sorteo360.Events.Infrastructure/Database/Migrations/20261119130955_AddRaffleParticipantTicketNumber.cs` con backfill SQL único por evento para filas existentes.
    - **Operadores en bitácora (`A21`)**: Añadidos `DrawnByName` en `PrizeDrawSummary`, `VoidedByName` en `PrizeDrawVoidSummary` y `RedeemedByName` en `PrizeHandoverSummary`, resueltos en vivo vía `IRaffleOperatorDirectory` en `RaffleResponse.FromAsync` y `PrizeDrawEndpoints` (sin llamadas cuando no hay sorteos y degradando a `null` si Keycloak no contesta).
    - **Modo salón sin internet (`A22`)**: Creados `docker-compose.salon.yml` e `infra/caddy/Caddyfile.salon` (`tls internal`, sin SMTP externo) y ampliada la sección §5 de `docs/deploy-sorteo360.md` con el procedimiento previo (con internet), operación en red local del salón y respaldos Postgres pre/post evento.
    - **Verificado en contenedor `mcr.microsoft.com/dotnet/sdk:10.0`**:
        - `dotnet build events-service/Sorteo360.Events.slnx --no-restore` → **0 errores**
        - `Events.Tests.Unit` (`TicketNumberGeneratorTests`, `PrizeDrawSummaryOperatorNameTests`, `RaffleBadgeRendererTests`, `PrizeDrawShapeTests`, `TemplateSeedIsNotModelManagedTests`, `StoredFileReferencesTests`) → **25/25**
        - `Events.Tests.Integration` (`AnonymousRoutesTests`, `OpenApiDocumentTests`, `AuthorizationTests`) → **47/47**
    - **Estado de §8.2 (Agente A)**: Todas las tareas **A0 → A22** completadas y marcadas `[x]`. Solo queda fuera de contenedor sin socket Docker la corrida de tests de integración con Testcontainers/PostgreSQL.

- 2026-11-19, Agente A (ola 3: A19, A13+A9, A12, A7 y A10):
    - **Reactivar a un anulado (`A19`)**: Nuevo endpoint `POST /v1/events/{eventId}/raffle/attendees/{attendeeId}/reinstate` (`raffles:write`, solo `in_progress`, `409 RAFFLE_ATTENDEE_NOT_VOIDED` si no tiene anulaciones). Columnas `reinstated_at` / `reinstated_by_id` en `events.raffle_attendee`, `RaffleResponse.reinstatedAttendeeIds`, `EligiblePool.For` reincorpora reactivados (y una nueva anulación limpia la reactivación y vuelve a excluir), y el acta PDF (`NotarialRecordContent` + `MigraDocNotarialRecordRenderer`) lista las reactivaciones. Tests unitarios añadidos en `EligiblePoolTests`, `RaffleServiceTests`, `NotarialRecordContentTests` y `MigraDocNotarialRecordRendererTests`.
    - **Realm `sorteo360`, roles simples y tema de login (`A13`, `A9`)**: Añadido rol compuesto `proyector` (`raffles:draw`, `platform:staff`) y usuario `proyector@sorteo360.local` en `infra/keycloak/sorteo360-realm.json` y `docs/roles.md`; retirados los 5 roles de cotizaciones/proveedores; tema de login renombrado a `infra/keycloak-themes/sorteo360/login/resources/css/sorteo360.css` (`theme.properties` y `README.md` sin referencias a MiTur/PGTI).
    - **Retiro de proveedores, cotizaciones, FirmaGOB y relevo de tokens (`A7`)**: Eliminados endpoints, dominio, infraestructura, `TokenRelay`, `SignatureWorker`, validadores y tests obsoletos en `events-service/` y `shared/`; simplificados `EventService.CloseAsync` y `EventRepository.CancelAsync`/`CloseAsync`; actualizado `StoredFileReferences.cs`. Generada migración `events-service/Sorteo360.Events.Infrastructure/Database/Migrations/20261118211258_AddAttendeeReinstatementAndDropSupplierQuoteTables.cs` (borra las 14 tablas retiradas y agrega `reinstated_at`/`reinstated_by_id` a `events.raffle_attendee`, sin columnas `xmin` espurias).
    - **Limpieza MiTur/PGTI y namespaces (`A12`)**: Renombrados los namespaces C# `Sorteo360.*` → `Sorteo360.*` en 1,353 archivos `.cs` (incluidos `ModelSnapshot` y migraciones); limpiados dominios `mitur.gob.do`, plantillas heredadas, `docker-compose.yml` (`/var/lib/sorteo360/files` + `events-service/Dockerfile`), `docker-compose.override.yml`, `.env.example`, `infra/postgres-setup/roles.sh`, `CLAUDE.md`, `README.md`, `GLOSSARY.md`, `GLOSSARY-MAP.md`, `events-service/CLAUDE.md` y `shared/CLAUDE.md`; eliminados `events-service/docs/{quotes,signatures}.md` y colecciones Bruno de `Quotes`/`Signatures`.
    - **Despliegue de producción (`A10`)**: Creados `docker-compose.prod.yml` (perfiles mínimos, Keycloak en `start --import-realm`, sin puertos de desarrollo expuestos), `infra/caddy/Caddyfile` (HTTPS automático con Let's Encrypt y opción `tls internal` para salón offline), `.env.prod.example`, `scripts/backup-postgres.sh` y runbook `docs/deploy-sorteo360.md`.
    - **Verificado en contenedor**:
        - `dotnet build Sorteo360.Platform.slnx --no-restore` → **0 errores**
        - `Platform.Tests.Unit` → **169/169**
        - `Events.Tests.Unit` → **703/704** en pasada completa + `StoredFileReferencesTests` **1/1** tras ajustar `StoredFileReferences.cs` (**704/704**)
        - `Convocatorias.Tests.Unit` → **554/554**
        - `Notifications.Tests.Unit` → **217/217**
        - `Events.Tests.Integration` (`AnonymousRoutesTests`, `OpenApiDocumentTests`, `AuthorizationTests`) → **47/47**
        - `scripts/roles-parity.sh` (23 roles OK) y `scripts/roles-parity.test.sh` (**22 ok, 0 fallos**) en contenedor Alpine con `bash`, `jq` y `coreutils`.
    - **Pendiente**: `A20` (número de boleto), `A21` (nombres de operador en `PrizeDrawSummary`/`Void`/`Handover` para bitácora), `A22` (ensayo completo offline en laptop/mini-PC del salón) y tests de integración con Testcontainers/Postgres.

- 2026-11-18, Agente A (ola 2: A6, A8, A9, A11–A18 y §8.1):
    - **Desacoplamiento CRM & eventos sin oficina (`A6`, `A18`)**: Añadidos `DisabledCrmClient` (`events-service`) y `DisabledCrmContactsClient` (`convocatorias-service`) cuando falta `Services:Crm:BaseUrl`; retirado `Services__Crm__BaseUrl`, `CRM_SERVICE_CLIENT_SECRET`, `CRM_DB_PASSWORD` de `docker-compose.yml`/`.env.example`. `Event.OptOfficeId` admite `Guid.Empty`, `EventService.ValidateOptAsync` permite omitir `optOfficeId` cuando no viene `optContactId`, y `CreateEventRequestValidator`/`UpdateEventRequestValidator`/`EventEndpoints` ya permiten crear eventos mínimos sin oficina del CRM y con valores por defecto de ciudad/país (`Santo Domingo`, `República Dominicana`).
    - **Branding, organizador y sin tómbola (`A8`, `A16`, `A17`)**: `GET/PUT /v1/branding` (con lectura anónima) y `GET/PUT /v1/events/{eventId}/raffle/branding` en `events.branding_setting`. El acta PDF (`MigraDocNotarialRecordRenderer` + `NotarialRecordFacts` + `RaffleRepository`) y el correo al ganador (`PrizeWonTemplateSeed` `{{organizador}}` + `RaffleWinnerNotifier`) toman el nombre de empresa u organizador configurado (con respaldo `Sorteo360`). `ball_tombola` quedó fuera de `RaffleVisualStyle.All` y mapeado a `roulette` en lectura y migración.
    - **Interruptores de funciones y tareas (`A14`, `A15`)**: Movidos fuera del eliminado CRM a `events.feature_switch` vía `FeatureSwitchEndpoints` (`GET /v1/feature-switches` y `GET /v1/public/feature-switches` anónimos devolviendo array JSON, y `PUT /v1/feature-switches/{key}` bajo política `feature-switches:write`); sin endpoints `/v1/tasks` en `notification-service`.
    - **Tipos genéricos, prefijo `REF-`, infra y Realm `sorteo360` (`A9`, `A11`, `A12`, `A13`)**: `EventType` actualizado a `Corporate`, `Christmas`, `Wedding`, `Birthday`, `Concert`, `Graduation`, `Conference`, `Other` (con constantes legadas mapeadas y `ck_event_type`). Migración `events-service/Sorteo360.Events.Infrastructure/Database/Migrations/20261118191349_AddBrandingFeatureSwitchesAndGenericEventTypes.cs` (sin columnas `xmin` espurias, convierte `Roadshow`/`Feria`/`Tradeshow`→`Corporate`, `Webinar`→`Conference`, `ball_tombola`→`roulette` y `events.prize.won`→`{{organizador}}`). `RegistrationCodes` usa prefijo `REF-`; plantillas de `TemplateSeed.cs` actualizadas a `Sorteo360`; compose/.env usan `name: sorteo360`, `sorteo360-net`, `${REGISTRY:-sorteo360}` y `POSTGRES_USER=sorteo360`; realm renombrado en `infra/keycloak/sorteo360-realm.json` (`sorteo360`, `sorteo360-app`, roles limpios sin CRM ni `collaborator`), tema en `infra/keycloak-themes/sorteo360/` con `sorteo360-logo.svg`, y `docs/roles.md` sincronizado. _(Nota `A12`: el renombrado masivo de namespaces/ensamblados/carpetas `Sorteo360._`→`Sorteo360._` se mantiene para la pasada de repositorio nuevo de §5)._
    - **Auditoría y motivos de exclusión (§8.1)**: `RaffleParticipantDto` expone `exclusionReason` (`manually_excluded`, `not_checked_in`, `already_won`, `same_department`) y `GET /v1/events/{eventId}/raffle/audit` exporta `drumHash`, `drawChainHash` y bitácora.
    - **Verificado en contenedor `mcr.microsoft.com/dotnet/sdk:10.0`**: build completo de `Sorteo360.Platform.slnx` (0 errores), `Platform.Tests.Unit` (169/169), `Events.Tests.Unit` (1,384/1,384), `Notifications.Tests.Unit` (217/217), `Convocatorias.Tests.Unit` (554/554), `AnonymousRoutesTests` (2/2), `OpenApiDocumentTests` (15/15), `scripts/roles-parity.sh` (27 roles OK) y `scripts/roles-parity.test.sh` (22/22). Tests de integración con Testcontainers/Postgres siguen sin ejecutarse dentro del contenedor sin socket Docker montado.
    - **Pendiente**: `A7` (retiro completo opcional de proveedores/cotizaciones/FirmaGOB), `A10` (despliegue HTTPS/backup), `A19` (`POST .../attendees/{attendeeId}/reinstate` + reactivaciones en `RaffleResponse`/bombo/acta) y renombrado masivo de ensamblados `Sorteo360.*` → `Sorteo360.*`.

- 2026-11-18, Agente A (prioridad miércoles A0–A5): Completado A0→A5 sobre `events-service/` y `shared/`.
  Padrón `events.raffle_participant` persistido en Postgres (migración `20261118170732_AddRaffleParticipantRosterAndSponsorName.cs`,
  sin columnas `xmin` espurias) con upsert por `employeeCode`/`email`, `replaceExisting`, check-in
  individual/bulk, eliminación, QR PNG (`GET .../participants/{code}/qr`) y gafetes PDF (`GET .../participants/badges.pdf`).
  Webhooks protegidos por clave en tiempo constante (`RaffleWebhooks:Events:{eventId}:ApiKey` /
  `RaffleWebhooks:ApiKey` / `Webhooks:Secret`) y firma HMAC opcional (`X-Webhook-Signature` /
  `X-Callback-Signature` con `t=<epoch>,v1=<hex>`, ventana de 5 min). `RosterAttendanceSource` lee del
  padrón; `Qualification` y `EnsureCanIncludeAsync` permiten participantes sin CRM, `WalkIn` y `.do`;
  premios/logos/acta aceptan `sponsorName` libre (`PrizeSponsorResponse.CompanyId` pasa a `Guid?`).
  Verificado en contenedor `mcr.microsoft.com/dotnet/sdk:10.0`: build de `events-service/Sorteo360.Events.slnx`
  (0 errores), `Platform.Tests.Unit` (169/169), `Events.Tests.Unit` (1,360/1,360), `AnonymousRoutesTests`
  (2/2) y `OpenApiDocumentTests` (15/15). Tests de integración con Testcontainers/Postgres no ejecutados
  dentro del contenedor sin socket Docker montado. Queda pendiente: `A6`–`A10`.

- 2026-11-18, Agente B (sesión 2): B0 y B1 hechos; lanzamiento en 3 pasos sin tómbola.
  Recorrido probado en el navegador en modo mock: cargar Excel con filas malas → vista previa
  con errores → importar → iniciar → girar → ganador → recargar y el ganador sigue (el mock
  persiste en `localStorage`, clave `sorteo360.mock.state.v1`). build, lint (0 errores) y
  306 tests en verde. Aviso de "DO Travel Awards" oculto en pantalla. Pendiente: B2, B3, B5
  (usuarios), B6, B7. Riesgo: el paquete `xlsx@0.18.5` de npm tiene CVEs conocidas al leer
  archivos manipulados; aquí solo lee archivos que sube el propio operador. Actualizar a la
  0.20.x del CDN de SheetJS si se va a leer archivos de terceros.

- 2026-11-18, sesión 1 (antes de repartir): Fase 0 y limpieza. Fuera `crm-service/`,
  `.scratch/`, Dokploy, CONTRIBUTING, JULES_REPORT, `.githooks/`, docs/ADRs/scripts del
  ministerio; CRM fuera de compose, init.sh, roles.sh, `.env.example`, solución, CI y
  `check.sh`. Frontend sin crm, proveedores, viatico, reportes, carga de proveedores, casos de
  éxito, tareas (página), OPT (páginas), cotizaciones/pagos del evento, e2e de esas áreas,
  assets del ministerio, rutas nginx/Vite al CRM. Colores de marca personalizables.
- `Services__Crm__BaseUrl` y `CRM_SERVICE_CLIENT_SECRET` siguen en compose a propósito (A6, A9).
- `0.26.0` en la raíz: otro proceso lo tiene abierto, no se pudo borrar.
- Hay más de un actor editando el árbol: revisar `git status` antes de editar.
