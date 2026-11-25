# Sorteo360: lo pendiente en bloques chicos

Armado el 2026-11-20 (viernes) para seguir en sesiones nuevas. La entrega a Banreservas es el
**miércoles 25 de noviembre**.

Cada bloque tiene entre 2 y 5 tareas y cabe en una sesión. Para cada uno se indica la prioridad, si
depende de otro bloque, los archivos que toca, cómo verificarlo y cuándo queda hecho.

## Estado al 2026-11-24 y por dónde seguir

**Leer esto primero.** Resume el estado de todas las sesiones hasta hoy; el detalle de cada cosa está
en el **Registro** al final.

**Hecho:** A1, A2, A3, B1–B5, D1, D3.1, D4, S1 (los pendientes 1, 3 y 6 de la auditoría siguen abiertos), la simulación para demos, el proyector como control
remoto y la limpieza de la revisión del 2026-11-21. El 2026-11-23 hubo un ensayo con el banco en
local, con un túnel de Cloudflare (sin staging). Todo sin commit: el árbol tiene ~3.600 cambios
sobre `6c77c011`, dentro de OneDrive. **No hacer commit sin la orden de Robert.**

**Próxima sesión, en este orden:**

1. **Decidir (Robert):** los pendientes 1 y 3 de la auditoría (convocatorias sin tenants; el QR
   que devuelve una inscripción repetida) y el 6 (cabeceras anti-iframe en Caddy/nginx).
2. **D3.2** (si Robert lo aprueba) y después el **guion C1 → C3** con Robert en el ambiente dev.
   Banreservas va sin check-in: C3.1 y C3.2 (gafetes y cola sin red) son opcionales.
3. **Despliegue:** Railway (`docs/deploy-paas-railway-fly.md`, sin ejecutar) o el VPS.
4. P2 (B6, B7, D2) y B5.5 después de la entrega.

**Documentos vivos:**

| Qué | Dónde |
| --- | --- |
| Este plan por bloques, con su Registro | `plans/bloques-pendientes.md` |
| Plan del producto, contratos, cuentas y planes | `plans/sorteo360.md` |
| Guion de pruebas, producción y día de la fiesta | `plans/pruebas-y-produccion.md` |
| Despliegue en VPS (Compose + Caddy) | `docs/deploy-sorteo360.md` |
| Despliegue en Railway / Fly.io (plan) | `docs/deploy-paas-railway-fly.md` |
| Auditoría de seguridad run-1 y cómo retomarla | `docs/security-audit-2026-11.md` |

**Avisos:**

- Banreservas: sin check-in, padrón desde una base de datos, patrocinadores opcionales pero
  guardados, dos reuniones de demo con ejecutivos esta semana.
- La revisión del 2026-11-21 dio `draw-batch` por bien protegido porque exige su rol. La auditoría
  del 2026-11-23 encontró después que no comprueba de qué cuenta es el evento (pendiente 2 de
  `docs/security-audit-2026-11.md`): manda la auditoría.
- La demo se levanta con `cd frontend && npx vite --port 5199 --strictPort` (modo mock), con Node
  22: `export PATH="/c/Users/Ro.Guzman/AppData/Roaming/nvm/v22.14.0:$PATH"`.

## Cómo usar este archivo

1. Al abrir una sesión, leer esto primero. Después leer `CLAUDE.md` y el `CLAUDE.md` del servicio
   que se vaya a tocar.
2. Tomar **un** bloque: el primero sin hacer con la prioridad más alta y sus dependencias listas.
3. Al terminar, marcar las tareas con `[x]` y agregar una línea al final, en el **Registro**:
   fecha, bloque y lo que quedó.
4. **No hacer commit, push ni despliegue** sin la orden explícita de Robert.
5. Puede haber otra sesión editando el árbol: correr `git status` antes de ediciones grandes.

Contexto que no hay que volver a leer entero: §8–§10 de `plans/sorteo360.md` (contratos),
`plans/agente-a-cuentas-planes-escala.md` §3 (contratos de cuentas y premios) y
`plans/pruebas-y-produccion.md` (guion, escala y producción).

### Verificación

- **Backend**: compilar y correr las pruebas en el contenedor `mcr.microsoft.com/dotnet/sdk:10.0`.
  Las de integración usan Testcontainers: montar el socket de Docker y pasar
  `TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal`.
  - Build: `dotnet build events-service/Sorteo360.Events.slnx --no-restore`.
  - Unitarias: `Events.Tests.Unit`, que estaba en 733/733.
  - Integración: `Events.Tests.Integration`.
  - Roles: `scripts/roles-parity.sh`, en un contenedor Alpine con `bash`, `jq` y `coreutils`.
- **Frontend**: Node 22, con `export PATH="/c/Users/Ro.Guzman/AppData/Roaming/nvm/v22.14.0:$PATH"`.
  En `frontend/` correr:
  - `npx tsc -p tsconfig.app.json --noEmit`
  - `npm run lint`: 0 errores.
  - `npm test`
  - `npm run build`

  Demo en modo mock: `npx vite --port 5199 --strictPort`.

---

## Prioridad P0: sin esto no se entrega el miércoles

### Bloque A1: la migración que falta y la verificación completa (backend, 3 tareas)

**Por qué:** A34 (la foto del premio) agregó `PrizeData.ImageStoredFileId` y su configuración en
`PrizeConfiguration`, pero ninguna migración crea `events.prize.image_stored_file_id`. El
`ModelSnapshot` solo tiene la del evento. Sin esa migración, el servicio falla contra Postgres al
leer o guardar premios.

- [x] A1.1 Generar la migración `AddPrizeImageStoredFileId` en
      `events-service/Sorteo360.Events.Infrastructure/Database/Migrations/`. Debe crear la
      columna, la FK a `stored_file` y el índice. Revisar que no meta columnas `xmin` espurias,
      que ya pasó en migraciones anteriores.
- [x] A1.2 Compilar la solución completa con `Sorteo360.Platform.slnx`: 0 errores.
- [x] A1.3 Correr `Events.Tests.Unit` completo, y de `Events.Tests.Integration` estas suites:
      `Database`, `AnonymousRoutesTests`, `OpenApiDocumentTests`, `AuthorizationTests`,
      `PrizeSponsorEndpointsTests` y `PrizeHandoverEndpointsTests`. Anotar las cifras.

**Hecho cuando:** la migración existe, `Database` (migraciones y constraints) pasa sobre
PostgreSQL real y las unitarias están en verde.

### Bloque A2: las 21 pruebas viejas de `RaffleEndpoints` (backend, 3 tareas, depende de A1)

**Por qué:** siguen asertando reglas anteriores a A4: `ConvocatoriasAttendanceSource`, exclusión
por `walk_in`, por `.do` y por `contact_unresolved`, y el acta dorada vieja. El CI no queda en
verde sin esto.

- [x] A2.1 Correr `events-service/Sorteo360.Events.Tests.Integration/RaffleEndpoints/` y listar
      las que fallan.
- [x] A2.2 Reescribirlas contra las reglas actuales:
      - `RosterAttendanceSource` (el padrón);
      - `exclusionReason` con `manually_excluded`, `not_checked_in`, `already_won` y
        `same_department`;
      - `requiresCheckIn`.

      Borrar las que solo probaban CRM o `.do`.
- [x] A2.3 Regenerar el acta dorada, si existe, con el renderer actual. Revisar el PDF a ojo antes
      de aceptarla: debe llevar el organizador, `sponsorName` y las reactivaciones.

**Hecho cuando:** `Events.Tests.Integration` corre completo en verde.

### Bloque B1: reescribir las pruebas de Playwright (frontend, 4 tareas)

**Por qué:** B18. `frontend/e2e` sigue probando el CRM y módulos quitados.

- [x] B1.1 Revisar cada spec de `frontend/e2e/`: `smoke`, `scanner`, `scanner-camera`,
      `interruptores-funciones`, `mesas-sillas`, `sorteo-acta`, `sorteo-entrega`,
      `sorteo-firmantes` y `sorteo-reglas`. Para cada uno decidir si sirve, se adapta o se
      borra; por ejemplo, ver si las mesas y sillas siguen existiendo.
- [x] B1.2 Adaptar `smoke.spec.ts` al landing (`/`), a `/app` y a las tarjetas actuales: Rifas en
      vivo, Agenda de eventos, Invitaciones y Marca y ajustes.
- [x] B1.3 Adaptar `interruptores-funciones.spec.ts` y `scanner.spec.ts`. Los interruptores salen
      de `GET /v1/public/feature-switches`; el escáner usa `participants/check-in` y la cola sin
      conexión.
- [x] B1.4 Escribir un e2e del recorrido de la entrega en modo mock: crear sorteo → importar un
      CSV con una fila mala → iniciar → girar → ganador → recargar y el ganador sigue → acta.

**Hecho cuando:** `npx playwright test` está en verde en modo mock y no queda ninguna referencia a
CRM, OPT, proveedores ni cotizaciones en `e2e/`.

### Bloque B2: datos de prueba para el ensayo (frontend, 2 tareas)

**Por qué:** B19. El guion y la prueba de escala usan `padron-25000.xlsx` y `premios-10000.csv`.

- [x] B2.1 Escribir un script, por ejemplo `frontend/scripts/datos-prueba.mjs`, que genere:
      - `padron-25000.xlsx`, con Nombre, Apellido, ID de empleado, correo, departamento y
        ubicación. Que traiga algunas filas malas a propósito: sin nombre, un correo inválido y
        un código repetido.
      - `premios-10000.csv`, con Ronda, Premio, Valor y Cantidad.
- [x] B2.2 Documentar cómo correrlo en `plans/pruebas-y-produccion.md` §3.2 y probar los dos
      archivos en el modo mock: que importen y cuánto tardan.

**Hecho cuando:** los dos archivos se generan con un solo comando y entran en el mock.

### Bloque A3: medir la escala (backend, 3 tareas, depende de A1 y B2)

**Por qué:** A31. Existe `RaffleScaleTests.cs`, pero no está registrado que se haya corrido.

- [x] A3.1 Correr `RaffleScaleTests` (`[Trait("Category","Scale")]`) y anotar cada métrica contra
      la tabla de `pruebas-y-produccion.md` §3.3. Por ejemplo, importar 25.000 participantes debe
      tardar menos de 10 s y cada giro menos de 300 ms.
- [x] A3.2 Si falta alguna medición, agregarla: el acta en PDF con 10.000 premios debe salir en
      menos de 30 s y sin quedarse sin memoria, y el check-in debe responder en menos de 100 ms.
- [x] A3.3 Arreglar lo que no llegue a la meta, o anotarlo como riesgo con su cifra.

**Hecho cuando:** hay una tabla con las cifras medidas en el **Registro**.

### Bloque C1: guion manual en el ambiente dev, parte 1 (Robert y una sesión, 3 tareas, depende de A1)

Ambiente: `cp .env.example .env && docker compose up -d --build`. Es la parte "Acceso y roles" de
`pruebas-y-produccion.md` §3.2.

- [ ] C1.1 Levantar todo y confirmar que `/_ready` responde en cada servicio, que Keycloak tiene
      el realm `sorteo360` y que Mailpit está en `:8025`.
- [ ] C1.2 Revisar el acceso:
      - el administrador ve los cuatro módulos;
      - el operador ve solo lo suyo;
      - la puerta entra directo al escáner y el proyector a su pantalla;
      - cerrar sesión vuelve al landing.
- [ ] C1.3 Abrir sin login `/`, `/terminos`, `/privacidad`, `/crear-cuenta` y `/activar/x`.
      Comprobar que `/app` sin sesión manda a Keycloak.

### Bloque C2: guion parte 2, padrón y premios (3 tareas, depende de C1 y B2)

- [ ] C2.1 Crear el sorteo con la marca de Banreservas y revisarlo en tema claro y oscuro.
- [ ] C2.2 Importar `padron-25000.xlsx`. Revisar la vista previa, el informe de filas con
      problemas, el tiempo de carga, las tandas de 200 filas y la búsqueda.
- [ ] C2.3 Importar `premios-10000.csv`. Revisar las rondas creadas, el agrupado, la foto del
      premio y que se puedan editar, mover y borrar premios.

### Bloque C3: guion parte 3, puerta y sorteo (4 tareas, depende de C2)

- [ ] C3.1 Gafetes: imprimir 10 y escanearlos con un celular. La cámara pide HTTPS, así que se
      usa Caddy con `tls internal`.
- [ ] C3.2 Cortar la red: escanear 20 personas sin wifi, volver a conectar y comprobar que la cola
      se envía sola, sin duplicados. Probar también dos puertas a la vez.
- [ ] C3.3 Sorteo:
      - iniciarlo, lo que congela la tómbola;
      - consola y proyector en dos pantallas;
      - girar, anular con motivo y reactivar;
      - refrescar la consola a mitad del sorteo;
      - entregar con firma y código.
- [ ] C3.4 Cierre: el acta en PDF midiendo cuánto tarda, el historial, la bitácora y el correo al
      ganador en Mailpit.

**Hecho cuando:** C1–C3 están marcados y lo que falló quedó en el **Registro** como un bloque
nuevo de arreglos.

### Bloque D1: seguridad del realm de producción (backend, 3 tareas)

- [x] D1.1 Documentar en `docs/deploy-sorteo360.md` §6, paso a paso, cómo borrar los usuarios demo
      (`operador`, `supervisor`, `admin`, `puerta` y `proyector`, todos con `localdev`) justo
      después del primer arranque y antes de abrir el DNS. Otra opción es un realm de producción
      sin esos usuarios.
- [x] D1.2 Confirmar que `.env.prod.example` trae todas las variables de
      `pruebas-y-produccion.md` §4.2, con `VITE_SIGNUP_ENABLED=false`.
- [x] D1.3 Correr `scripts/roles-parity.sh` y `scripts/check.sh`. Compose y backend pasaron el
      2026-11-23; falta `scripts/check.sh --frontend` (ver el Registro).

### Bloque S1: seguridad antes de desplegar (backend, 4 tareas)

Detalle, archivos y pruebas de cada uno en `docs/security-audit-2026-11.md`.

- [x] S1.1 Cerrar `/v1/accounts/signup` y `/activate` en el backend cuando el registro está
      apagado. Hecho el 2026-11-24: `Accounts__SignupEnabled` (compose lo toma de
      `SIGNUP_ENABLED`, por omisión `false`) y, apagado, todo POST a `/v1/accounts/*` da 404
      (`SignupGateTests`). Si algún día se enciende, `account_admin` sigue viendo todos los
      tenants en convocatorias: ahí habrá que quitarle esos roles.
- [x] S1.2 Actualizar `System.Security.Cryptography.Pkcs` (10.0.0 tiene avisos conocidos) en
      `Directory.Packages.props`. Hecho el 2026-11-24: 10.0.15.
- [x] S1.3 Corregir los 4 confirmados, cada uno con su prueba de regresión: HTML sin escapar en
      los correos de activación y de solicitud de plan; `proxyHopCount: 2`; xlsx con columnas
      hasta XFD; `email` duplicado en el JSON del registro público. Hecho el 2026-11-24 (ver
      Registro).
- [x] S1.4 Agregar el filtro de cuenta a las tablas hijas del evento (premios, imágenes, sorteos
      por código, branding) y correr las pruebas de integración de los pendientes 1–5 y 7.
      Hecho el 2026-11-24 para 2, 4, 5 y 7 (`EventChildTenantIsolationTests`). **Siguen abiertos
      el 1 (convocatorias sin tenants) y el 3 (QR de otra persona)**: no son filtros de EF, son
      decisiones de diseño; Banreservas no usa convocatorias.
- [ ] S1.5 Corregir después de las pruebas los pendientes que siguen abiertos (anotado el
      2026-11-24, al hacer público el repo): el 1 (convocatorias sin tenants), el 3 (una
      inscripción repetida devuelve el QR de otra persona) y el 6 (el SPA se puede meter en un
      iframe: falta `X-Frame-Options`/`frame-ancestors` en Caddy y nginx).

### Bloque D4: CI (2 tareas)

- [x] D4.1 `.github/workflows/ci.yml` solo corre en push a `main` y en PR, y `development` es la
      rama principal. Agregar `development` a `on.push.branches`.
- [x] D4.2 Agregar un job con `npx playwright install --with-deps chromium` y `npm run test:e2e`
      (modo mock). Las pruebas de integración necesitan Docker en el runner: decidir aparte.

### Bloque D3: arreglos antes del guion C1 (2 tareas, encontrados el 2026-11-23)

- [x] D3.1 Reparar `frontend/node_modules`, que quedó a medio borrar. Antes, cerrar en el editor el
      servidor de pruebas de Playwright y el explorador de Vitest, porque tienen abierto
      `lightningcss.win32-x64-msvc.node` y `npm ci` falla con EPERM. Después correr
      `bash scripts/check.sh --frontend` (con Node 22 en el `PATH`) y marcar D1.3.
- [ ] D3.2 **Decisión de Robert pendiente:** agregar a los 5 usuarios demo de
      `infra/keycloak/sorteo360-realm.json` el atributo
      `"account_id": ["00000000-0000-0000-0000-000000000001"]`. Sin él, `events-service` responde
      `403 ACCOUNT_NOT_ACTIVE` (`RequireAccountClaimMiddleware`) y C1 falla. Confirmar además que
      Keycloak 26 no descarta el atributo por no estar en el perfil de usuario
      (`unmanagedAttributePolicy`), y que el token lo trae.

---

## Prioridad P1: mejora la entrega; si no llega, se entrega sin esto

### Bloque B3: reglas y patrocinador en texto (frontend, 3 tareas)

B3 del plan. El contrato está en `plans/sorteo360.md` §8.1, "Contrato adicional".

- [x] B3.1 En la creación y edición de premios, el patrocinador como texto libre: `sponsorName`
      en `POST` y `PATCH` de premios, en http y en el mock. Hoy `frontend/src` no usa
      `sponsorName`.
- [x] B3.2 Las reglas en la configuración del sorteo: `onePrizePerPerson`,
      `onePrizePerDepartmentPerRound` y `requiresCheckIn`, por `PATCH /v1/events/{id}/raffle`.
- [x] B3.3 Mostrar `exclusionReason` en la tabla de participantes con textos en `labels.ts`.

### Bloque B4: detalles del tablero y la ruleta (frontend, 3 tareas)

- [x] B4.1 Anular a un ganador ausente desde el tablero. Hoy solo se puede en la pestaña
      Ganadores.
- [x] B4.2 La ruleta clásica debe pedir `GET …/prizes/{prizeId}/eligible?limit=36`, que ya existe
      (R14).
- [x] B4.3 El tablero debe leer `winnerTicketNumber`, `winnerEmployeeCode` y `winnerDepartment`
      del servidor (A33) en vez de lo guardado en el navegador, y usar
      `POST …/prizes/draw-batch` para los lotes.

### Bloque B5: campos visibles del participante (frontend, 4 tareas)

B16. El contrato está en `plans/sorteo360.md` §10. El backend (A29) ya está hecho.

- [x] B5.1 Selector de `displayFields` (nombre, identificador y detalle) en la configuración del
      sorteo, con `PUT …/raffle/display-fields`. Solo se puede editar en borrador.
- [x] B5.2 Una función pura con pruebas que, a partir del participante y `displayFields`, arme el
      texto. Si el campo elegido está vacío no se muestra nada: nunca "—".
- [x] B5.3 Aplicarla en la consola, el proyector, la ruleta, los nombres pasando, la tarjeta del
      ganador y el historial. **No** aplicarla al acta ni al certificado.
- [ ] B5.5 (P2) Aplicar los campos visibles también a la ruleta y los nombres pasando (hoy usan
      el `displayName` del bombo) y al historial de ganadores.
- [x] B5.4 Agregar Nombre, Apellido y Ubicación al mapeo del padrón en `participantFile.ts`, con
      los alias `nombres`, `apellidos`, `ubicacion`, `sucursal`, `localidad` y `oficina`, y sus
      pruebas.

---

## Prioridad P2: después del miércoles (el registro sale apagado)

### Bloque B6: uso del plan (frontend, 4 tareas)

B14.

- [ ] B6.1 Leer `GET /v1/account` y mostrar el uso, por ejemplo "18 de 25 participantes" y "1 de
      1 sorteo este mes".
- [ ] B6.2 Agregar a `labels.ts` los mensajes de `PLAN_PARTICIPANT_LIMIT`,
      `PLAN_ACTIVE_RAFFLE_LIMIT`, `PLAN_MONTHLY_RAFFLE_LIMIT`, `PLAN_USER_LIMIT`,
      `PLAN_FEATURE_NOT_INCLUDED`, `PLAN_ROUND_LIMIT`, `PLAN_PRIZE_LIMIT` y
      `ACCOUNT_NOT_ACTIVE`. Usar `extensions.remaining` y `resetsAt`.
- [ ] B6.3 Avisar en la vista previa de la importación cuando el lote pasa el tope.
- [ ] B6.4 Ocultar el botón y la ruta `/proyector/:eventId` cuando `features.projector` es
      `false`. El mock debe aplicar los mismos topes.

### Bloque B7: "Mi plan" y el superadministrador (frontend, 3 tareas)

B15. El contrato está en `plans/sorteo360.md` §9.5 y los endpoints en `PlanRequestEndpoints.cs`.

- [ ] B7.1 La pantalla "Mi plan": el plan actual, pedir otro plan con
      `POST /v1/account/plan-requests`, los datos de transferencia, el estado de la solicitud y
      cancelarla.
- [ ] B7.2 La pantalla del `platform-admin`: listar las solicitudes pendientes y aprobarlas (con
      el período pagado) o rechazarlas (con motivo).
- [ ] B7.3 Hacer lo mismo en el mock, con pruebas.

### Bloque D2: limpieza (2 tareas)

- [ ] D2.1 `docs/specs/*/BUILD-ORDER.md` vienen de PGTI: hablan de Dokploy, SendGrid, cotizaciones
      y CRM. Ponerles un aviso de "histórico, no es trabajo pendiente" o borrar las casillas que
      ya no aplican.
- [ ] D2.2 Actualizar las imágenes base del `frontend/Dockerfile` (el editor marca
      vulnerabilidades) y volver a construir la imagen.

---

## Lo que decide Robert (no es de ninguna sesión)

Estas decisiones bloquean el calendario de `pruebas-y-produccion.md` §6:

1. ~~El servidor y el dominio~~: resuelto. Robert confirmó (2026-11-20) que estarán listos para
   staging el lunes 23. No volver a señalarlo como bloqueo.
2. El proveedor SMTP y el remitente, con SPF, DKIM y DMARC.
3. Si el día de la fiesta corre en la nube o en modo salón, y la fecha, la hora y el lugar.
4. Los datos de Banreservas: el padrón, los premios, el logo y los colores, y quiénes son los
   operadores, la puerta y el notario.
5. Quién de Banreservas recibe la capacitación de 1 hora, el lunes o el martes.
6. Los datos legales (`frontend/src/features/legal/legalDatos.ts`) y la revisión de un abogado.
7. El nombre del repo nuevo. El remote sigue apuntando a `mitur-git/pgti360`.
8. Más adelante: los precios de los planes y los datos bancarios.
9. La fuente Freight Sans en `.woff2`, que tiene que mandar el cliente.

## Orden sugerido

Actualizado el 2026-11-23. A1–A3, B1–B5 y D1.1–D1.2 ya están hechos.

| Sesión | Backend | Frontend / infra |
| --- | --- | --- |
| 1 | S1.1 y S1.2 | D3.1 (y cerrar D1.3) |
| 2 | S1.3 y S1.4 | D4 |
| 3 | C1 → C3, con Robert, en el ambiente dev (antes, D3.2) | — |
| 4 | Arreglos del guion | Railway (`docs/deploy-paas-railway-fly.md`) |
| Después del miércoles | D2 | B5.5, B6, B7 |

Backend y frontend pueden correr en paralelo porque no comparten carpetas: el backend toca los
servicios, `shared/`, `infra/` y `docs/`, y el frontend solo `frontend/`.

## Registro

- 2026-11-20: se creó este archivo a partir de la revisión de `docs/` y `plans/`. Al revisar se
  encontró que la migración de la foto del premio no existe (Bloque A1).
- 2026-11-20: Bloque A1 hecho. Migración `20261120175332_AddPrizeImageStoredFileId` (columna,
  FK `fk_prize_image_stored_file` Restrict e índice; sin `xmin` espurios). Para compilar hubo que
  arreglar 3 errores que ya estaban: `RaffleService.DrawBatchAsync` y `RaffleEndpoints.GetRaffle`
  usaban `RaffleDetail` como si fuera `RaffleWithRounds` (faltaba `.Raffle`), y
  `BatchDrawAndBoardTests` usaba el nombre viejo de parámetro `draws:`. La unitaria destapó un bug
  real: `EligibleResponse.From` no aplicaba el tope `MaxLimit` (100) si el pedido pasaba del tamaño
  del bombo; corregido. Platform.slnx: 0 errores. Unitarias 737/737. Integración (Database,
  AnonymousRoutes, OpenApiDocument, Authorization, PrizeSponsor, PrizeHandover): 143/143 sobre
  PostgreSQL real. Se corrió con el SDK local (10.0.404) y Docker Desktop, no con el contenedor sdk.
- 2026-11-20: Bloque A2 hecho en `RaffleEndpoints` (419/419, sin `Scale`). Fallaban 20, no 21. Se
  adaptaron al padrón y a la exclusión manual: `RaffleStartEndpoints`, `RaffleAttendeeOverride`,
  `RaffleAttendees`, `RaffleReadiness`, `RaffleCrmGate` y `RaffleCrmOutage`. Se borraron
  `ConvocatoriasAttendanceTests`, `ConvocatoriasAttendanceOutageTests`, `ConvocatoriasCheckInStub`,
  `ConvocatoriasCheckInApiDbFactory` y los casos `.do`/walk-in/sin contacto. Nuevo
  `RosterAttendanceTests` (6): `requiresCheckIn` true/false, congelado, `not_checked_in`,
  `manually_excluded`, `already_won` y `same_department`. El acta dorada (que son marcas de texto,
  no un archivo) ahora usa marca del evento, `sponsorName` y una reactivación; el PDF se revisó
  a ojo y lleva Organizador, "patrocinado por Brugal & Co." y "Reactivaciones tras anulación".
  **Pendiente para que `Events.Tests.Integration` quede en verde (730/735):** 4 pruebas fuera del
  sorteo fallan por el filtro de cuenta (tenancy): `ActorStampingTests.CreateEvent_Stamps…`,
  `EventConcurrencyTests.CloseVsCancel…` y las dos `EventRepositoryTests.UpdateAsync_…`. Leen la
  base con un scope sin cuenta y el filtro esconde el evento. **Riesgo para la entrega:**
  `RaffleService.MaxAttendees = 5000` sigue en pie, así que arrancar con un padrón de 25.000
  devolvería `ATTENDANCE_TOO_MANY_ROWS` (lo mide A3).
- 2026-11-20: arregladas las 4 pruebas de tenancy. Fallaban porque fuera de una petición
  `ICurrentAccount.AccountId` es `Guid.Empty` (R6, falla cerrado) y el filtro de cuenta escondía
  el evento. Los arreglos están solo en las pruebas: `ActorStampingTests` y `EventConcurrencyTests`
  leen con `IgnoreQueryFilters()`, y `EventRepositoryTests` arma su contexto con la cuenta por
  omisión. `Events.Tests.Integration` completo: **735/735** en verde (sin `Scale`). A2 queda
  hecho. El tope `MaxAttendees = 5000` sigue sin cambiar.
- 2026-11-20: el tope de asistentes por sorteo (`RaffleService.MaxAttendees`) sube de 5000 a
  25.000, para que entre el padrón de Banreservas. También se cambiaron el mensaje de
  `ATTENDANCE_TOO_MANY_ROWS`, las descripciones de OpenAPI y las pruebas del borde (25.000 entra,
  25.001 no). El tope del webhook (5.000 por petición) es otra cosa y no se tocó. Unitarias:
  737/737. Integración del borde y OpenAPI: 46/46.
- 2026-11-20: Bloque B1 hecho (solo `frontend/e2e/`). `npx playwright test` en modo mock:
  **34/34** en verde (2,7 min); `npm run lint` 0 errores (3 avisos que ya estaban en `src/`);
  `npm test` 176/176. En `e2e/` no queda CRM, OPT, proveedores ni cotizaciones.
  - **Casi sin cambios:** `sorteo-entrega` (solo pasa por el menú Acciones y cambia un cargo de
    ministro por uno neutro).
  - **Adaptados:** `smoke` (landing `/` → Entrar → `/app` con Rifas en vivo, Agenda de eventos,
    Invitaciones y Marca y ajustes; barra lateral y menús sobre Rifas en vivo);
    `interruptores-funciones` (apaga Rifas en vivo › Historial de Ganadores, 404 para quien no
    administra, sobrevive a la recarga); `scanner` (check-in del padrón con `QR-NAV-…`, conteo
    51/51, la puerta entra desde `/app`, y la cola sin conexión del escáner del sorteo se manda
    sola al abrirlo); `scanner-camera` (arreglada una carrera: el código seguía en cuadro al
    cerrar la lista); `sorteo-acta`, `sorteo-firmantes`, `sorteo-reglas` (menú Acciones, pestaña
    Configuración, botón "Siguiente premio" y confirmar presencia). Lo común quedó en
    `e2e/sorteo.helpers.ts`.
  - **Borrado:** `mesas-sillas` (Invitaciones: la siembra ya no trae registrados ni los de
    `expedia.com`, y "Agregar mesa" ya no existe; las mesas quedan sin e2e).
  - **Nuevo:** `sorteo-recorrido` (crear sorteo → premios por CSV → padrón CSV con una fila sin
    nombre → iniciar → girar → recargar y el ganador sigue → completar → abrir el acta).
  - **Hallazgos para otro bloque:** el mock no arma el PDF del acta (`getFileContentUrl` devuelve
    un marcador de placehold.co), así que el e2e solo comprueba que el acta se abre. La cola sin
    conexión no se puede disparar en modo mock (el mock no usa la red): se prueba el envío de una
    cola ya guardada, no el encolado; eso queda para C3.2.
- 2026-11-20: Bloque B2 hecho. Comando: `cd frontend && node scripts/datos-prueba.mjs` (semilla
  fija; deja `padron-25000.xlsx`, `premios-10000.csv`, `padron-25.csv` y `premios-10.csv` en
  `frontend/datos-prueba/`, agregada a `frontend/.gitignore`). El padrón no entraba tal cual:
  "ID de empleado" y "Ubicación" no eran alias de `participantFile.ts`; se agregaron (`id de
  empleado` → código, `ubicacion` → empresa) con su prueba unitaria. Probado en el mock
  (`npx vite --port 5199 --strictPort`, sorteo `ev-0001`): padrón 24.970 importados y 50 filas
  marcadas (sin nombre, correo inválido y código repetido), lectura 1,7 s e importación 3,4 s;
  premios 10.000 unidades agrupadas en 117 líneas y 16 rondas nuevas (20 en total), lectura 0,1 s
  e importación 1,0 s. Lint: 0 errores (3 avisos que ya estaban). Unitarias: 177/177.
- 2026-11-20: Bloque A3 hecho. `RaffleScaleTests` nunca había corrido: el cuerpo de crear el evento
  ya no era válido, el ETag se reenviaba sin `W/` y no había realm, así que cada giro pagaba ~0,6 s
  de reintentos contra un Keycloak inexistente. Se reescribió para medir las 7 filas de §3.3
  (aserciones blandas; imprime cada cifra al medirla; `SCALE_DOOR_SECONDS=600` da los 10 min de
  las puertas, que por omisión corren 60 s). Comando:
  `dotnet test events-service/Sorteo360.Events.Tests.Integration --filter Category=Scale`, en el
  contenedor `sdk:10.0` con Testcontainers. **Cifras de la laptop** (Docker Desktop, 7,6 GB, build
  Debug, base en otro contenedor detrás del proxy de puertos): la misma lectura sin cambios varió
  entre 0,3 s y 1,7 s de una corrida a otra. **Hay que repetir la medición en staging** antes del
  miércoles.

  | Operación | Meta | 1.ª corrida (antes) | Corrida final | ¿Pasa? |
  | --- | --- | --- | --- | --- |
  | Importar 25.000 participantes (una petición) | < 10 s | 18,0 s | 11,7 s | no |
  | Listar el padrón | < 1,5 s | 1,88 s | 1,41 s | sí |
  | Leer un sorteo con 10.000 premios | < 1,5 s | 2,32 s | 1,50 s | no (en el borde) |
  | Cada giro (p50 / p95 de 100) | < 300 ms | 1.656 / 3.624 ms | 570 / 916 ms | no |
  | Check-in por QR o boleto (p50 / p95 de 200) | < 100 ms | 52 / 144 ms | 38 / 50 ms | sí |
  | Acta en PDF con 10.000 premios | < 30 s, sin quedarse sin memoria | 218 s | 64,7 s (21,1 s aislada) | no; memoria sí (pico 1,1 GB) |
  | 5 puertas escaneando a la vez (60 s) | sin errores 5xx | 0 en 2.956 | 0 en 4.171 (p95 113 ms) | sí |

  También se anotó, fuera de la tabla: iniciar (congela 25.000) 8,8 s; lote de 10 giros p50 2,3 s.

  **Lo que se cambió:**
  - **Giro:** `RaffleService.DrawAsync` ya no carga las rondas con los 10.000 premios antes y
    después de girar. Antes valida contra el sorteo solo (el repositorio da el mismo
    `PRIZE_NOT_FOUND`, bajo el bloqueo) y después lee solo ese premio con
    `IRaffleRepository.GetPrizeWithDrawsAsync`, que es nuevo. Con eso también se aligeran la
    anulación y la entrega, que usan la misma relectura.
  - **Acta:** el registro cronológico y el anexo se parten en tablas de 500 filas, cada una con su
    fila de títulos. MigraDoc pagina una tabla grande en tiempo más que lineal: 25.000 filas en una
    sola tabla tardaban 40 s; partidas en tablas de 100, 9 s. El anexo va en su propia sección
    (sigue empezando en página nueva y con la numeración de corrido). El PDF se revisó a ojo: el
    corte se ve limpio y no hay hojas en blanco. `NotarialRecordContent.RoundsOf` dejaba de buscar los
    giros de cada premio entre todos (10⁸ pasos con 10.000 de cada lado).
  - **Correos de los ganadores:** se mandaban dentro de `POST …/complete`, uno por ganador, y con
    10.000 eran 161 s del cierre. Ahora el endpoint los encola en `RaffleWinnerNotificationWorker`
    (canal en memoria, con la cuenta de quien completó) y salen después de responder. **Cambia el
    contrato:** la respuesta de `complete` ya no trae `winnerNotificationStatus`; se ve al releer el
    sorteo (en la pantalla aparece "—" hasta recargar). Si el proceso se cae a mitad, lo ya mandado
    queda con estado y el resto en nulo, como antes. `NotifyWinnersAsync` pasó a ser pública en
    `IRaffleService` y también dejó de ser cuadrática. Se actualizaron `events-service/CLAUDE.md`,
    `events-service/docs/raffles.md` y `docs/specs/raffles/03-evidence.md`.
  - **Pruebas:** `RaffleTestApi.CompleteAsync` espera los correos cuando sale bien. Las dos pruebas
    que leían el estado del correo en la respuesta ahora lo leen del sorteo releído. En las
    unitarias, `CompleteAsync_NotifiesNobody_WhenTheWriteCommits` es nueva y la de los correos pasa a
    `NotifyWinnersAsync`.

  **Verificación:** `Events.Tests.Integration` completo (sin `Scale`) **735/735**;
  `Events.Tests.Unit` **738/738**; build 0 errores. Todo en el contenedor `sdk:10.0`.

  **Riesgos que quedan (con su cifra de la laptop):**
  - **Giro: 570 ms p50 contra 300.** Lo que queda es leer los 25.000 congelados bajo el bloqueo en
    cada giro (~0,75 s perfilado, casi todo traer las filas). El frontend espera la respuesta antes
    de arrancar la animación, así que es tiempo muerto después del clic. Arreglo posible: guardar
    en memoria el bombo congelado (invalidándolo al reactivar o reabrir), o traer solo las columnas
    del bombo y después la fila del ganador. No se hizo ahora porque toca el corazón del sorteo
    notarial.
  - **Acta: 65 s en la corrida completa contra 30, aunque aislada dio 21 s.** La diferencia es la
    carga de la laptop en esa corrida. **El proxy corta a los 60 s:** `frontend/nginx.conf` no fija
    `proxy_read_timeout` (vale 60 s) y Caddy va delante en producción. Hay que subirlo a 300 s
    para `/v1`. Es de `frontend/` e `infra/`, fuera de esta sesión.
  - **Importar 25.000: 11,7 s contra 10.** Está cerca de la meta. El frontend ya manda en tandas de
    200 filas (B2), así que no hay una petición de 25.000 desde la pantalla.
  - **Leer el sorteo: 1,50 s, justo en el borde.** El proyector la repite y el ETag con 304 la
    abarata cuando no cambió.
  - **Keycloak en cada giro y lectura:** `KeycloakRaffleOperatorDirectory` pide el nombre del
    operador sin caché. Con Keycloak lento o caído, cada giro paga los reintentos (~0,6 s medidos).
    Arreglo posible: caché de los nombres resueltos para las respuestas (no para el acta).
- 2026-11-20: Bloques P1 hechos: B3, B4 y B5. Datos nuevos de Robert: Banreservas sin check-in
  (padrón desde una base de datos, todos participan); patrocinadores opcionales pero guardados en la
  base; dos reuniones de demo con ejecutivos la semana del 23.
  - **B3 patrocinador:** campo "Patrocinador (opcional)" al agregar premio y columna
    **Patrocinador** en la importación de premios (`premioFile.ts`, alias `patrocinador`,
    `patrocinado por`, `sponsor`, `auspiciador`). **Backend:** `prizes/import` ahora acepta
    `sponsorName` por fila (request, DTO, servicio y validador de 200) y lo persiste; antes solo lo
    aceptaban el alta y la edición. Prueba de integración
    `ImportPrizes_PersistsTheOptionalSponsorName`. Se ve en la lista de premios ("Patrocina: …"),
    separa grupos y sale en el tablero como "Cortesía de …" (el acta ya lo imprimía). B3.2 y B3.3
    ya existían; se corrigió el texto de la regla a "un premio por departamento en cada ronda".
  - **B4:** "No se presentó" en la consola del tablero (anula con `absent` y saca la fila del
    proyector); el tablero sortea por `draw-batch` (cae premio por premio si el servicio no lo
    tiene; un 409 parcial muestra los que salieron); la ruleta pide `?limit=36` y muestra el total
    real; el detalle del sorteo pide `?limit=1` porque solo usa el conteo.
  - **B5:** `camposVisibles.ts` (puro, con pruebas) + tarjeta "Datos del ganador en pantalla" en
    Configuración (solo borrador, con vista previa). Aplicado en el tablero, la tarjeta del ganador
    y el proyector. **Falta:** la ruleta/nombres pasando (siguen con el nombre del bombo) y el
    historial. **Backend:** `PrizeDrawSummary` suma `winnerLocation`, `winnerFirstName` y
    `winnerLastName` desde el padrón. Padrón: "Ubicación/Sucursal/Oficina/Localidad/Sede" van a
    `location` (ya no a empresa); sin columna de departamento, la ubicación hace de área.
  - **Pedido de Robert:** en el tablero, la columna del premio pasa de 22 % a 36 % y los ganadores
    quedan más a la derecha, como la plantilla de Banreservas.
  - Verificación: frontend tsc 0 errores, lint 0 errores (3 avisos viejos), `npm test` 183/183.
    Backend build 0 errores, unitarias 738/738, integración (Review/Security, OpenAPI,
    PrizeSponsor, BatchDraw, Database) 99/99; integración completa sin `Scale`: 736/736.
- 2026-11-20: **Modo proyector como control remoto** (pedido de Robert: la ventana del proyector
  repetía la misma pantalla que la consola). Ahora el proyector (`/proyector/:eventId`) solo
  muestra resultados y la consola detecta que está abierto por `BroadcastChannel`
  (`useProyectorConectado`: la consola manda `ping`, el proyector contesta `hola` y avisa `adios`
  al cerrarse). Con proyector: el tablero no se repite en la consola (queda "Control remoto" con
  Sortear, "No se presentó", "Ver lo que ve el público" plegado, y Enter/espacio sortean desde
  cualquier parte); en la ruleta clásica el título se achica, la ruleta queda como vista previa
  chica y hay botón "Proyector" en la barra. Al cerrar el proyector la consola vuelve sola a la
  pantalla completa. Probado en modo mock con dos ventanas (capturas). Lint 0 errores, `npm test`
  183/183, Playwright 34/34 (se ajustó `sorteo-reglas` al nuevo texto de la regla).
- 2026-11-21: **Simulación para demos** (pedido de Robert): Premios → Agregar → "Simular 100
  empleados y 52 premios" (solo en borrador, con confirmación). Carga 100 empleados de prueba
  (todos presentes, con nombre/apellido, ID, departamento y ubicación), 50 premios en una ronda
  nueva (25 bonos 5,000, 15 de 10,000, 7 canastas con patrocinador, 3 tablets) y 2 premios
  principales con foto dibujada en el navegador (sin internet) en otra ronda al final, que el
  tablero sortea uno a uno. Usa las rutas reales (padrón, `prizes/import`, foto), así que sirve en
  modo demo y contra el servidor. `features/sorteo/simulacion.ts`. Probado en mock hasta sortear un
  principal en el tablero.
- 2026-11-21: **Revisión de fallos** (barrido de pantallas en modo mock, accesibilidad, código
  muerto, infraestructura). Arreglado: enlace roto "Casos de éxito" del portal público (404) y
  `proxy_read_timeout 300s` en `/v1/` de `frontend/nginx.conf` (el acta grande pasaba de 60 s).
  Pendiente, sin tocar: ver el resumen de la sesión (código muerto en `components/ui` y
  `features/publico`, scripts heredados con CRM/MiTur, CI que no corre en `development`, campos
  sin etiqueta). Playwright 34/34 (una prueba de interruptores falló una vez con la laptop cargada
  y pasó sola al repetir).
- 2026-11-21: **Corrección de lo hallado en la revisión.**
  - Scripts: `postgres-isolation.sh` sin la base/login del CRM y con `POSTGRES_USER` por omisión
    `sorteo360`; `postgres-certs.test.sh` con imagen/volúmenes `sorteo360` y la guía nueva;
    `certs.sh` (CA "Sorteo360 PostgreSQL CA" para entornos nuevos) apunta a
    `docs/deploy-sorteo360.md` §7, sección nueva sobre el certificado interno y qué hacer si su
    renovación falla. Fuera las referencias PGTI/MiTur de `infra/postgres-setup` y `roles-parity.sh`.
  - Código muerto: borrados 18 archivos sin uso (12 de `components/ui`, 3 de `features/publico`,
    `lib/dates.ts` con su prueba, `lib/search.ts`), 17 funciones exportadas sin uso (y
    `lib/format.test.ts`, que solo probaba dos de ellas) y las dependencias `pdfjs-dist` y
    `recharts`. `tsc --noUnusedLocals` limpio.
  - Accesibilidad: nombre en el botón de menú del celular (`aria-expanded`), en las flechas del
    calendario público y etiqueta en 20 campos (buscadores, filtros de tipo y fecha, registros por
    página, enlaces del logo). Re-barrido de las 40 pantallas: 0 controles sin nombre, 0 páginas
    rotas, 0 errores de JS.
  - Backend: 0 avisos de compilación (tipo de contenido nulo en `IUploadPolicy`, pruebas de
    notificaciones con `ShouldNotBeNull()` y el constructor nuevo de Testcontainers). Arreglada
    también `CheckInRealmTests` de convocatorias, que fallaba desde que existe `account_admin`.
  - Prueba inestable: `interruptores-funciones` recargaba antes de que el cambio optimista se
    guardara; ahora espera el estado guardado (6/6 seguidas).
  - Verificación: frontend tsc/lint 0 errores, `npm test` 174/174, build OK, Playwright 34/34.
    Backend: Platform 169, Events 738, Notifications 217, Convocatorias 540 (unitarias); integración
    de archivos/imágenes 81/81 y de envíos 16/16.
- 2026-11-23: D1.1 y D1.2 hechos (D1.3 espera a Docker). **D1.1:** `docs/deploy-sorteo360.md`
  §6.1 nueva: borrar los 5 usuarios demo con `kcadm.sh` dentro del contenedor (sin dominio ni
  consola), con búsqueda exacta, comprobación y limpieza de la sesión; §3 apunta ahí. No se hizo
  un realm de producción aparte: el realm se importa una vez y borrarlos es un paso. **D1.2:** en
  `.env.prod.example` están todas las variables de §4.2 y las que exige `docker-compose.prod.yml`.
  El registro sale apagado con `SIGNUP_ENABLED=false`, que el compose pasa como
  `VITE_SIGNUP_ENABLED` (se aclaró en el ejemplo y en la tabla de §4.2). Cambios: perfiles por
  omisión `events,notifications,frontend` (Banreservas); SMTP sin SendGrid por omisión (proveedor
  por decidir); `SENDGRID_EVENT_WEBHOOK_ENABLED=false`, porque con `true` y otro proveedor cada
  envío queda esperando noticias de entrega que no llegan; nota de que
  `CONVOCATORIAS_SERVICE_CLIENT_SECRET` es obligatoria aunque convocatorias esté apagado.
  Los comandos de `kcadm` no se probaron: Docker estaba apagado. Probarlos en staging o en C1.
- 2026-11-23: D1.3 a medias. `roles-parity.sh` OK (24 roles, Alpine); `check.sh --compose` OK
  (22 + 12); `--backend` OK (Platform 169, Events 738, Convocatorias 540, Notifications 217).
  `--frontend` falló en `npm ci` con EPERM: el servidor de pruebas de Playwright y el explorador
  de Vitest del editor tenían abierto `lightningcss.win32-x64-msvc.node`, y `node_modules` quedó a
  medio borrar. **Hallazgo para C1:** los 5 usuarios demo del realm no tienen el atributo
  `account_id`, así que `events-service` les respondería `403 ACCOUNT_NOT_ACTIVE`. Ambos quedan
  en el Bloque D3. No se borró ningún usuario de Keycloak (no había contenedores corriendo) ni se
  hizo commit.
  **Próxima sesión, en este orden:** D3.1 → D3.2 (si Robert lo aprueba) → C1 → C3, con Robert, en
  el ambiente dev. En C1 probar también los comandos `kcadm` de `docs/deploy-sorteo360.md` §6.1,
  pero sobre una copia: borran los usuarios demo que C1 necesita.
- 2026-11-24: D3.1 y D1.3 hechos. El 2026-11-23 hubo un ensayo con el equipo del banco, en local
  con un túnel de Cloudflare. Antes de `npm ci` se detuvieron 3 servidores de prueba de Playwright,
  el explorador de Vitest y 2 servidores de Vite (`npm run dev` y `:5199`), que tenían abierto
  `node_modules`. `npm ci`: 295 paquetes, sin EPERM (`npm audit` marca 21 vulnerabilidades: 2
  críticas y 7 altas, sin revisar). `bash scripts/check.sh --frontend`: **OK**. Build en 1 min 3 s,
  lint con 0 errores y 2 avisos que ya estaban, Prettier limpio, `npm test` 178/178. Hay que volver
  a levantar Vite (`npx vite --port 5199 --strictPort`) si el túnel lo necesita. Sigue **S1**.
- 2026-11-24: Bloque D4 hecho, sin commit. `ci.yml` corre también en push a `development` y tiene
  un job `e2e` nuevo (`npm ci`, `npx playwright install --with-deps chromium`,
  `npm run test:e2e` en modo mock, que levanta Vite solo en `:5199`). No se probó en un runner:
  se verá en el primer push. Las pruebas de integración con Docker siguen sin decidir.
- 2026-11-25: **Exclusión en lote** hecha, sin commit, en la rama `feat/exclusion-en-lote`
  (`plans/exclusion-en-lote.md`, pasos 0–6). Paso 0: ya no hay correos `@sorteo.local`; la
  identidad es `identity_key` (correo, o `id:` + ID) y la migración `RemoveSyntheticEmails` la
  arma sobre los datos viejos (probada sobre una copia de la base local: 14.073 filas, todas a
  `id:`, ninguna `legacy:`; también se probó el `Down`). Una fila sin correo ni ID se rechaza
  (`missing_email_and_id`). El aviso al ganador sin correo queda en `no_recipient` (no se creó
  `skipped_no_email`). Endpoint `POST …/attendees/overrides/bulk-exclude`, pantalla «Excluir en
  lote» y e2e nuevo. El mock pasó a `sorteo360.mock.state.v4`. Falla previa, no de este bloque:
  `RaffleDrawnSetupEndpointsTests.UpdateRaffle_Returns204WithTheNewRules…(False, False)`.
