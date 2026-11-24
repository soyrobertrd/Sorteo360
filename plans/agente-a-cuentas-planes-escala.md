# Agente A: cuentas, planes y escala (25.000 participantes, 10.000 premios)

Encargo de Robert del 2026-11-19, para el Agente A (backend).

- **Lógica de fondo:** `plans/sorteo360.md` §9 (cuentas y planes) y §10 (campos visibles). Si una
  regla de este documento choca con aquellas, manda este documento, que es más nuevo.
- **Frontend:** el Agente B ya hizo su parte en modo mock y llama a los contratos de §3 tal como
  están escritos aquí.
- **Sin commit ni push** salvo que Robert lo pida explícitamente.

## 0. Hallazgos de la revisión de código (2026-11-19): primero esto

La revisión (`/code-review` contra `origin/development`) encontró 10 defectos en el backend. Van
**antes** que cualquier tarea de §4, en este orden. Cada uno se cierra con su test de regresión
(que falle antes del arreglo y pase después) y una línea en el Handoff de `plans/sorteo360.md`.

### Bloquean la entrega del miércoles (Banreservas)

- [x] **R1. La regla "un premio por empresa" usa el campo equivocado en el sorteo real.**
  `events-service/.../Raffles/RosterAttendanceSource.cs:41` arma `Attendee.Company` como *Company
  antes que Department*, pero el DTO del padrón (`ResolveExclusionReason`) usa *Department antes
  que Company*. En Banreservas todos tienen Company = "Banreservas": con la regla encendida, tras el
  primer ganador `EligiblePool` saca a todos y el sorteo se agota, mientras la pantalla los muestra
  calificados.
  **Arreglo:** una sola función que diga cuál es la "empresa" del participante para la regla
  (Department si viene, si no Company), usada por los dos caminos.
  **Test:** 10 participantes con la misma Company y Departments distintos, regla encendida: se
  sortean 10 premios sin `RAFFLE_POOL_EXHAUSTED`, y el DTO coincide con la tómbola.

- [x] **R2. "Participar sin estar presente" no tiene efecto (A35).**
  `RaffleParticipantWebhookEndpoints.cs:549`: `requireCheckIn` se calcula y no se usa;
  `Rules.RequireCheckIn` de `UpdateRaffleRequest` se acepta y no se mapea; `RaffleRulesResponse`
  nunca se construye.
  **Arreglo:** el contrato es el de §4 A35: un `requiresCheckIn` **en el nivel superior** del PATCH y
  de `RaffleResponse` (el frontend ya lo manda así), guardado y aplicado al congelar la tómbola y
  al calcular `not_checked_in`. Quitar el `Rules` anidado que nadie usa.
  **Test:** con `requiresCheckIn=false`, un participante sin check-in queda calificado y entra a la
  tómbola al iniciar; con `true`, no.

- [x] **R3. Sin configuración de Keycloak, cualquiera entra como administrador total.**
  `shared/.../Auth/KeycloakAuthenticationExtensions.cs:50` registra un handler "LocalDev" que
  autentica toda petición, incluso anónima, con `central_admin`, `platform:staff` y todos los
  permisos; `docker-compose.yml` arranca en `Development` por omisión.
  **Arreglo:** volver a fallar al arrancar si falta `Keycloak__Authority`, salvo que se pida el modo
  local de forma explícita (`Auth__Mode=LocalDev`) **y** el entorno sea `Development`; nunca en
  `Production`. El handler local no autentica peticiones sin cabecera.
  **Test:** arranque sin authority ni flag → excepción; flag en Production → excepción; petición
  anónima a un endpoint protegido en modo local → 401.

### Aislamiento entre cuentas (antes de abrir el registro a otras empresas)

- [x] **R4. El padrón no verifica que el evento sea de la cuenta.**
  `RaffleParticipantWebhookEndpoints.cs:138`: lista, QR, `badges.pdf`, auditoría, check-in, check-in
  masivo y borrar no comprueban la cuenta, y `RaffleParticipants` no tiene filtro global (solo
  `Events`).
  **Arreglo:** filtro global por cuenta también en `RaffleParticipants` y en todo lo que cuelga del
  evento (premios, sorteos, entregas, actas), o resolver primero el evento por el repositorio
  filtrado y responder 404 si no es de la cuenta.
  **Test:** un usuario de la cuenta B pide cada una de esas rutas con un evento de A → 404, sin
  cambios en la base.

- [x] **R5. El webhook de participantes está atado a Banreservas y su clave sirve para todos.**
  `RaffleParticipantRepository.cs:61`: el webhook es anónimo, `CurrentAccountId` cae en la cuenta
  por omisión y el filtro de `Events` esconde los eventos de otras cuentas (siempre
  `EVENT_NOT_FOUND`); el check-in por webhook no pasa por ese filtro y sí cruza cuentas; la clave
  global `RaffleWebhooks:ApiKey` / `Webhooks:Secret` autentica para cualquier evento.
  **Arreglo:** clave de webhook **por evento** (o por cuenta), de la que sale la cuenta; importar y
  check-in resuelven la cuenta desde la clave, no desde el usuario. La clave global queda solo como
  compatibilidad para Banreservas o se elimina.
  **Test:** la clave de A no importa ni hace check-in en un evento de B; el webhook de una cuenta
  nueva funciona en su propio evento.

- [x] **R6. Un token sin `account_id` se convierte en Banreservas.**
  `Web/Auth/CurrentAccountAccessor.cs:25`: claim ausente, vacío o mal formado → cuenta por omisión
  en plan Avanzado.
  **Arreglo:** fallar cerrado con 403 (`ACCOUNT_NOT_ACTIVE`, o un código nuevo
  `ACCOUNT_CLAIM_MISSING` que se suma a `frontend/src/lib/labels.ts`). Los usuarios existentes de
  Banreservas reciben el atributo `account_id` (realm o consola) antes del despliegue; documentarlo
  en `docs/deploy-sorteo360.md`.
  **Test:** token sin el claim → 403; con un GUID inexistente → 403.

- [x] **R7. La marca es una sola fila para todas las cuentas.**
  `Infrastructure/Database/BrandingRepository.cs:41` y el acta (`RaffleRepository.cs:1023`) leen la
  marca "global".
  **Arreglo:** `account_id` en la marca (lo que ya pedía A24), lectura y escritura filtradas por
  cuenta; el acta toma el nombre de la organización de la cuenta del evento. Migración: la fila
  actual pasa a Banreservas.
  **Test:** B cambia su marca; la de A y su acta no cambian.

### Registro y planes

- [x] **R8. La activación puede dejar una cuenta imposible de activar.**
  `Core/Accounts/AccountOnboardingService.cs:174`: crea el usuario en Keycloak antes de la
  transacción y sin compensación; `CompleteActivationAsync` no bloquea la fila; el aprovisionador
  inventa un `Guid.NewGuid()` si falta `Location` y se traga los errores al asignar roles.
  **Arreglo:**
  - bloquear la activación (`SELECT … FOR UPDATE`, o estado `Activating` con concurrencia
    optimista) para que un doble clic no corra dos veces;
  - si Keycloak responde "ya existe" con el correo de esta activación, reutilizar ese usuario en
    vez de 409;
  - si algo falla después de crear el usuario, borrarlo o dejar la activación reintentable;
  - sin `Location`, buscar el usuario por correo; nunca inventar un id;
  - si falla la asignación del rol, la activación falla.
  **Test:** doble activación simultánea → una cuenta activa y un usuario; fallo simulado de la base
  tras crear el usuario → el reintento termina bien; fallo al asignar el rol → la activación falla.

- [x] **R9. El tope mensual del plan se puede pasar con dos pedidos a la vez.**
  `Web/RaffleEndpoints/RaffleEndpoints.cs:248`: `EnsureCanCreateRaffleAsync` y
  `RecordRaffleCreatedAsync` van separados y sin bloqueo; el contador se lee y se escribe (pierde
  incrementos); la carrera del primer día del mes choca con la clave primaria; el resultado de
  `Record` se ignora.
  **Arreglo:** chequear y contar en la misma transacción que crea el sorteo, con
  `INSERT … ON CONFLICT (account_id, month) DO UPDATE SET raffles_created = raffles_created + 1
  WHERE raffles_created < @tope RETURNING raffles_created`; sin fila devuelta →
  `PLAN_MONTHLY_RAFFLE_LIMIT` y no se crea el sorteo. Igual para los demás topes que cuentan
  (sorteos activos, usuarios).
  **Test:** cuenta Gratis en 0/1, dos creaciones en paralelo → una entra, la otra 409, contador en 1.

### Menor

- [x] **R10. Reincorporar a un anulado ignora el estado del evento.**
  `Core/Raffles/RaffleService.cs:1077`: `ReinstateAttendeeAsync` lee el estado del evento y no lo
  valida.
  **Arreglo:** el mismo chequeo que `SetDisplayFieldsAsync` (`EVENT_NOT_EDITABLE` si está cancelado
  o cerrado).
  **Test:** evento cancelado con sorteo en curso → reincorporar devuelve el error y no cambia la
  tómbola.

- [x] **R11. Archivo vacío `0.26.0` en la raíz del repo**, residuo de una redirección de shell.
  Borrarlo.

### Auditoría de seguridad enfocada (2026-11-19)

Alcance: alta y activación de cuentas, Keycloak, archivos, rutas anónimas y frontend anónimo.
Bien: el token de activación es aleatorio de 32 bytes y se guarda solo su SHA-256; la contraseña
va directo a Keycloak y no aparece en logs; el realm no permite autorregistro y exige TLS fuera de
la red local; el frontend no inserta HTML sin escapar en ningún lado; `/files/{id}/content` manda
`nosniff`.

- [x] **S1. El alta de cuentas no tiene límite: sirve para bombardear con correos a cualquiera
  (media).** `Core/Accounts/AccountOnboardingService.cs:39-90`: `SignupAsync` es anónima, crea una
  activación pendiente nueva y manda el correo **cada vez**; el límite de 1 por minuto y 5 por día
  solo existe en `ResendActivationAsync`. Cualquiera puede llenar el buzón de un tercero con
  correos de Sorteo360, inflar la tabla de activaciones y quemar la reputación del remitente.
  **Arreglo:** si ya hay una activación pendiente para ese correo, `SignupAsync` la reutiliza y
  aplica los mismos contadores que el reenvío (sin error visible: responde 200 y no manda nada si
  se pasó del límite); además, `AddRateLimiter` por IP en `/v1/accounts/*` (por ejemplo 10 por
  minuto y 50 por hora).
  **Test:** 10 altas seguidas con el mismo correo → 1 correo enviado y 1 activación pendiente;
  la IP número 11 en un minuto → 429.

- [x] **S2. El reenvío revela si un correo tiene un alta pendiente (baja).**
  `AccountOnboardingService.cs:110-116`: un correo con alta pendiente dentro del minuto responde
  `ResendRateLimited` (429) y uno sin alta responde 200, así que se puede saber quién se registró.
  **Arreglo:** responder siempre 200 y no reenviar en silencio cuando aplica el límite.
  **Test:** reenvío a un correo pendiente dentro del minuto y a uno inexistente → misma respuesta.

- [x] **S3. Contraseñas sin política ni bloqueo, y el cliente público acepta usuario y contraseña
  directo (media).** `infra/keycloak/sorteo360-realm.json:364`: `sorteo360-app` tiene
  `directAccessGrantsEnabled: true`, y el realm no define `passwordPolicy` ni
  `bruteForceProtected`. Cualquiera con acceso a `AUTH_DOMAIN` puede probar contraseñas contra el
  endpoint de token sin pasar por la pantalla y sin bloqueo; además, la activación acepta
  cualquier contraseña porque Keycloak no tiene política que rechazar (`PASSWORD_POLICY_REJECTED`
  nunca ocurre, aunque el frontend pida 10 caracteres con letras y números).
  **Arreglo:** en el realm (y en la consola del Keycloak ya importado, ADR-0016):
  `directAccessGrantsEnabled: false` en `sorteo360-app` (el frontend usa el flujo estándar con
  PKCE); `passwordPolicy: "length(10) and digits(1) and notUsername and notEmail"`;
  `bruteForceProtected: true` con espera progresiva. Mover las pruebas locales que usen la
  contraseña directa a un cliente aparte, solo de desarrollo.
  **Test:** `roles-parity.sh` sigue en verde; activar con `abc` → 400 en `password`.

- [x] **S4. Los archivos se leen sin mirar la cuenta (media, depende de conocer el id).**
  `Web/FileEndpoints/FileEndpoints.cs:56-72`: `GET /v1/files/{id}` y `/content` solo exigen estar
  autenticado. Entre los archivos están las **firmas de los ganadores** al recibir su premio, las
  **actas notariales** (con cédulas del notario y testigos) y las imágenes. Los ids son GUID
  aleatorios, que es el único freno: un id filtrado (en un PDF, un log o un enlace compartido) le
  abre el archivo a cualquier usuario de cualquier cuenta. `GET /v1/files/missing` además lista
  archivos de todas las cuentas.
  **Arreglo:** guardar `account_id` en el archivo (o resolverlo por su dueño: evento, premio,
  entrega, acta) y filtrar en `GetByIdAsync`, `OpenContentAsync` y `ListMissingAsync`; 404 si no es
  de la cuenta. Va con R4.
  **Test:** un usuario de la cuenta B pide el contenido de una firma de A → 404.

- [x] **S5. La marca pública anónima es siempre la de Banreservas (FYI, va con R7).**
  `BrandingEndpoints.cs:41` es anónima; con la marca por cuenta, la ruta pública tiene que
  resolver la cuenta por el evento (portal de registro, escáner) y no devolver la de Banreservas
  por omisión.

Sin hallazgos nuevos en el frontend: las rutas anónimas (`/`, `/crear-cuenta`, `/activar`,
`/terminos`, `/privacidad`) no muestran datos de nadie, el `mailto:` codifica el asunto, las URLs
de imágenes van solo a `<img src>` y el token de activación no se guarda en el navegador.

### Repaso de la lógica del servicio de sorteos (QA y arquitectura, 2026-11-19)

Lo que está bien y no se toca: el azar sale de `RandomNumberGenerator.GetInt32` sobre el bombo
ordenado por id (reproducible para el acta); cada sorteo corre bajo el bloqueo del sorteo y relee
todo adentro; el código de canje es aleatorio criptográfico con reintento ante colisión; la firma
del webhook compara en tiempo constante y tiene ventana de 5 minutos. Lo que falla con la escala de
Banreservas:

- [x] **R12. El bombo se calcula en O(asistentes × sorteos) en cada giro (P0).**
  `Core/Raffles/EligiblePool.cs` llama a `RaffleAttendee.IsVoidedAndNotReinstated(raffleDraws)` por
  cada asistente, y ese método recorre **todos** los sorteos. Con 25.000 asistentes y miles de
  sorteos son cientos de millones de vueltas por giro, **dentro del bloqueo del sorteo**: hacia el
  final de la fiesta cada giro tardaría segundos y los lotes de 10 del tablero, decenas. Lo mismo
  `IsCurrentlyReinstated` donde se arme el DTO de asistentes.
  **Arreglo:** en `EligiblePool.For`, una pasada previa por los sorteos que arme
  `Dictionary<Guid, DateTimeOffset>` con la última anulación por asistente, y que cada asistente se
  consulte ahí (O(asistentes + sorteos)). Mismo cambio en el armado del DTO de asistentes.
  **Test:** unitario de rendimiento: 25.000 asistentes y 10.000 sorteos (500 anulados) → `For` en
  menos de 200 ms; y los tests de reglas de `EligiblePool` siguen en verde.

- [x] **R13. Cada giro relee los 25.000 asistentes y todos los sorteos (P1).**
  `RaffleRepository.DrawAsync` (líneas 774-775) carga todo bajo el bloqueo y, después,
  `LoadPrizeWithDrawsAsync` vuelve a leer. Con R12 resuelto es aceptable (~100 ms); el paso
  siguiente es el sorteo por lote de A33 (una sola lectura para hasta 10 premios).
  **Test:** el de escala de A31: cada giro < 300 ms con 25.000 / 10.000.

- [x] **R14. `GET …/prizes/{id}/eligible` devuelve el bombo entero (P1).**
  La ruleta del frontend usa como máximo 36 nombres y el total; hoy cada giro de la consola clásica
  baja 25.000 nombres (~1,5 MB).
  **Arreglo:** `?limit=` opcional (máximo 100) que devuelve `count` completo y una muestra de
  `participants` elegida al azar entre los elegibles (que no revele al ganador, porque todavía no
  existe). El frontend pasa `limit=36` cuando esté.

- [x] **R15. La relectura del sorteo completo después de cada cambio (P1).**
  `GET …/raffle` con 10.000 premios y sus sorteos pesa varios MB, y el frontend la relee tras cada
  giro, anulación o entrega. El frontend ya relee **una vez por lote** en el tablero (Agente B,
  `useDrawPrizes`). Del lado del servicio: compresión de respuesta (A31) y `ETag` con
  `If-None-Match` sobre la versión del sorteo para responder 304 cuando no cambió.

### Lo que la revisión no cubrió (revisar al cerrar R1–R10)

`PlanExpirationWorker`, `PlanRequestService`, las migraciones nuevas y los cambios de convocatorias y
notificaciones más allá del renombrado. Pasar `/code-review` de nuevo sobre esos archivos antes de
pedir el despliegue a staging.

## 1. Orden de trabajo

| Prioridad | Tareas | Por qué |
| --- | --- | --- |
| **P0, antes del miércoles (Banreservas)** | A30 importación de premios en bloque · A31 escala 25.000 / 10.000 · A32 Eventos y Convocatorias visibles para el administrador · A33 tablero de ganadores · A34 foto del premio · A35 participar sin estar presente | Banreservas usa un padrón y una lista de premios grandes; el frontend ya llama a `prizes/import` |
| P1, producto | A24 cuentas · A25 registro y activación · A26 topes · A27 `GET /v1/account` | Lo que pide el landing |
| P2 | A28 solicitudes de plan · A29 campos visibles | Dependen de P1 |

P1 y P2 no pueden romper la entrega: Banreservas queda como una cuenta **Avanzado** sin topes, y
con la migración de A24 todo lo existente pasa a esa cuenta.

## 2. Decisiones fijadas por Robert (2026-11-19)

- **Gratis:**
  - 25 participantes por sorteo, 1 sorteo **creado** al mes (los borrados cuentan), **1 ronda**,
    **10 premios** por sorteo y 1 usuario.
  - Sin pantalla de proyector, sin API/webhook y sin marca propia.
- **Planes de pago:** Básico, Pro y Avanzado (claves `basic`, `pro`, `advanced`). Los topes suben
  de plan en plan; están en la tabla de abajo.
- **Forma de pago:**
  - Mensual o **anual con descuento**: el anual cobra 10 meses (2 de regalo).
  - Se paga por **transferencia**: el plan de pago queda como **solicitud** hasta que el
    superadministrador confirma la transferencia.
- **Contraseña:** se elige **al activar**, no al registrarse.
- **El administrador de la cuenta ve todos los módulos, también Eventos y Convocatorias.**
- **Los campos que la rifa muestra se eligen por sorteo** (§10 de `plans/sorteo360.md`): nombre o
  nombre y apellido, ID o boleto, departamento o ubicación.

**Catálogo** (en código, no en base de datos; `null` = sin límite):

| Plan | Participantes por sorteo | Sorteos | Rondas por sorteo | Premios por sorteo | Usuarios | Proyector | API/webhook | Marca propia |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `free` | 25 | 1 creado al mes | 1 | 10 | 1 | no | no | no |
| `basic` | 250 | 3 activos | 5 | 100 | 3 | sí | no | sí |
| `pro` | 1.000 | 10 activos | 20 | 1.000 | 10 | sí | sí | sí |
| `advanced` | null | null | null | null | null | sí | sí | sí |

- Los topes y precios de los planes de pago son una propuesta; Robert todavía fija los precios.
- La copia del catálogo que usa el frontend está en `frontend/src/features/cuenta/planes.ts`. Si
  cambias un número, cámbialo en los dos lados.

## 3. Contratos que el frontend ya llama

### 3.1 Cuentas (anónimas)

Van bajo **`/v1/accounts`**, no `/v1/public`. El proxy (`frontend/nginx.conf` y
`frontend/vite.config.ts`) manda todo `/v1/public/` a convocatorias-service, y estas rutas son de
events-service. Las tres llevan `AllowAnonymous` y límite de frecuencia por IP.

```
POST /v1/accounts/signup
  { companyName, fullName, email, plan: "free"|"basic"|"pro"|"advanced",
    billingPeriod: "monthly"|"annual", acceptTerms: true }
  → 200 {}                    // igual exista o no el correo; nunca revela cuentas
  → 400 errors.{companyName|fullName|email|acceptTerms}

POST /v1/accounts/signup/resend   { email }
  → 200 {}                    // 1 por minuto y 5 por día por correo; igual si no existe

POST /v1/accounts/activate        { token, password }
  → 200 { email, companyName, requestedPlan: null | "basic"|"pro"|"advanced" }
  → 410 code ACTIVATION_TOKEN_INVALID   // vencido (24 h) o usado
  → 409 code ACCOUNT_EMAIL_TAKEN
  → 400 errors.password                 // política del realm; el token NO se quema
```

- `SignupResponse.demoActivationPath` existe **solo en el mock**. El servicio real nunca lo manda.
- El token es aleatorio de 128 bits. En la base se guarda solo su hash, y el correo lleva
  `/activar/{token}`.
- La activación crea el usuario en Keycloak por la Admin API, con una cuenta de servicio:
  - la contraseña queda como credencial definitiva (`temporary: false`);
  - el usuario recibe el atributo `account_id` y el rol compuesto de **administrador de cuenta**.
- La contraseña **no se guarda, no se registra en logs ni entra en la bitácora**. Revisa también
  el log de la petición y de las excepciones.
- Un plan de pago pedido al registrarse:
  - la cuenta queda en `free`;
  - se crea un `PlanRequest` en estado `Pending` (A28) con el `billingPeriod` elegido.

### 3.2 Premios en bloque (A30, P0)

```
POST /v1/events/{eventId}/raffle/prizes/import
  { prizes: [ { roundNumber: int>=1, name, value: string|null, quantity: int>=1 } ] }
  → 200 { created, roundsCreated }
```

- **Todo o nada**, en una transacción.
- Crea las rondas que falten por número, con el nombre `Ronda N`, desde max+1 hasta el número más
  alto del archivo.
- Un `roundNumber` menor que ya no existe (la ronda se borró) va a la primera ronda.
- Mismo permiso que `createPrizes` (`raffles:write`). Solo con el sorteo en borrador.
- `quantity` es la cantidad de unidades de esa línea (el archivo trae una columna Cantidad: "Ronda 1 · Bono · 10,000 · 25" son 25 bonos; un premio de 100 puede venir en 4 líneas de 25, una por ronda). Sin tope de 50 por línea, a diferencia de `createPrizes`.
- Tope de 1 a 10.000 unidades (suma de `quantity`) por petición. Si se pasa: 400 en `prizes`.
- Hoy el frontend llama a este endpoint y, si responde 404 o 405, cae al camino lento (una llamada
  a `createPrizes` por grupo). Ese respaldo se borra cuando esto esté publicado: está en
  `frontend/src/features/sorteo/hooks.ts`, en `useImportPrizes`.
- Con A26 este endpoint hace cumplir los topes del plan: `PLAN_ROUND_LIMIT` y `PLAN_PRIZE_LIMIT`,
  con `extensions.remaining`.

### 3.3 Códigos de error nuevos

Ya están en `frontend/src/api/types.ts` y `frontend/src/lib/labels.ts`; usa estos nombres exactos.

| `code` | HTTP | Cuándo |
| --- | --- | --- |
| `PLAN_PARTICIPANT_LIMIT` | 409 | El padrón pasaría el tope: en la importación, el alta manual, la API y el webhook. Lleva `extensions.remaining` |
| `PLAN_ACTIVE_RAFFLE_LIMIT` | 409 | Crear un sorteo con los activos al tope (planes de pago) |
| `PLAN_MONTHLY_RAFFLE_LIMIT` | 409 | Crear un sorteo con el cupo del mes usado (Gratis). Lleva `extensions.resetsAt` |
| `PLAN_ROUND_LIMIT` | 409 | Crear una ronda por encima del tope, también dentro de `prizes/import` |
| `PLAN_PRIZE_LIMIT` | 409 | Crear premios por encima del tope, en `createPrizes` y en `prizes/import`. Lleva `extensions.remaining` |
| `PLAN_USER_LIMIT` | 409 | Invitar un usuario por encima del tope |
| `PLAN_FEATURE_NOT_INCLUDED` | 403 | API/webhook o marca propia fuera del plan |
| `PLAN_REQUEST_PENDING` | 409 | Ya hay una solicitud de plan pendiente |
| `ACCOUNT_NOT_ACTIVE` | 403 | Cuenta sin activar o suspendida, en todo lo autenticado |
| `ACCOUNT_EMAIL_TAKEN` | 409 | En la activación |
| `ACTIVATION_TOKEN_INVALID` | 410 | En la activación |

## 4. Tareas

### A30. Premios en bloque (P0)

- El contrato es §3.2.
- **Inserción:** en una sola pasada (`AddRange` y un solo `SaveChanges`, o `COPY` si 10.000 tarda
  más de 2 s). Los `position` y `number` se calculan en memoria.
- **Tests de integración:**
  - 10.000 premios en 3 rondas nuevas;
  - un error a mitad del lote no deja nada guardado;
  - un `roundNumber` borrado va a la primera ronda;
  - con el sorteo iniciado, se rechaza.

### A31. Escala: 25.000 participantes y 10.000 premios (P0)

Objetivo, medido con PostgreSQL real (Testcontainers):

| Operación | Tope |
| --- | --- |
| Importar 25.000 participantes en una petición (JSON de unos 6 a 8 MB) | < 10 s |
| Listar el padrón (`GET .../raffle/participants`) | < 1,5 s |
| Leer el sorteo con 10.000 premios (`GET .../raffle`) | < 1,5 s |
| Cada giro (draw) | < 300 ms |
| Check-in por QR o boleto | < 100 ms |
| Acta en PDF con 10.000 premios | < 30 s, sin agotar memoria |

**Qué revisar y arreglar:**

1. **Tamaño de la petición.**
   - `MaxRequestBodySize` sale de `UploadLimits` (`DomainServicesConfiguration.cs:160-167`), y
     esos son los límites de archivos.
   - La importación JSON de 25.000 filas necesita su propio límite, al menos 16 MB, aplicado solo
     a esa ruta (`RequestSizeLimit`). Si no, Kestrel corta con un 413 sin `code`.
2. **Importación de participantes** (`RaffleParticipantRepository.cs`).
   - Ya genera los boletos contra un `HashSet` en memoria (línea 87), bien.
   - La búsqueda de duplicados por correo, código o QR tiene que ir por diccionarios, nunca por
     `FirstOrDefault` dentro del `foreach` (eso es O(n²)). Verificarlo.
   - Usar un solo `SaveChanges` por lote y `AutoDetectChangesEnabled = false` durante el lote.
   - Si no alcanza el tope de 10 s, usar `COPY` (Npgsql `BeginBinaryImport`) a una tabla temporal y
     hacer el `MERGE` en SQL.
3. **Índices** en `raffle_participant`:
   - `(event_id, lower(email))`
   - `(event_id, lower(employee_code))`
   - `(event_id, qr_code)`
   - ya existe `ux_raffle_participant_event_ticket_number`
   - en `prize`: `(round_id, position)`
   - en `prize_draw`: `(prize_id)` y `(event_id, attendee_id)`

   El check-in (líneas 325-345) hoy carga candidatos y filtra en memoria; con los índices tiene
   que ser una sola consulta por igualdad.
4. **El giro no carga la tómbola entera.**
   - Elegir al ganador con `ORDER BY random() LIMIT 1`, o con un índice aleatorio sobre un
     `COUNT`, entre los elegibles. La elegibilidad (llegó, no ganó todavía, no excluido) se
     resuelve en SQL.
   - Hay que mantener la reproducibilidad del acta: si hoy se guarda una semilla o el tamaño del
     pool, se sigue guardando.
   - La regla "un premio por persona/empresa" va con un `NOT EXISTS` contra `prize_draw`.
5. **`GET .../raffle` con 10.000 premios.**
   - No usar `Include` en cascada que multiplique filas (rondas × premios × draws); usar
     `AsSplitQuery()` y `AsNoTracking()`.
   - La respuesta pesa varios MB: activar la compresión de respuesta (Brotli/Gzip) para
     `application/json` en events-service, o en Caddy si ya comprime.
6. **Listado del padrón:** proyección directa a DTO con `AsNoTracking`, sin cargar entidades.
   - Acepta `?page=&pageSize=&search=&filter=`, con 25.000 completos como máximo si no viene
     `pageSize`.
   - Mantener la lista completa como respuesta por omisión: el frontend la usa en el modo sin
     internet y ya pagina al dibujar (200 filas por tanda).
7. **Acta en PDF:**
   - Generarla por streaming (sin el documento entero en memoria) y paginada.
   - Con más de 2.000 ganadores, el detalle va en tabla compacta.
   - Probar con 10.000 premios.
8. **Webhook/API de participantes:** el mismo camino en bloque que la importación, y como máximo
   5.000 filas por petición, con un 413 que lleve `code`.
9. **Pruebas de carga:** un test de integración marcado `[Trait("Category","Scale")]` que siembra
   25.000 participantes y 10.000 premios y comprueba los tiempos de la tabla.
   - No corre en el CI normal.
   - Documentar cómo se corre en `events-service/CLAUDE.md`.

### A32. Eventos y Convocatorias visibles para el administrador (P0)

- Robert decidió que el administrador **ve** Eventos y Convocatorias.
- En el servicio no hay siembra que los apague (`feature_switch` empieza vacío, y vacío quiere
  decir encendido); el mock ya se cambió igual.
- **Verificar:**
  - que no haya filas `events` o `convocatorias` con `enabled=false` en la base de producción ni
    en `infra/`;
  - que el rol compuesto del administrador (`central_admin` hoy, administrador de cuenta con A24)
    tenga los permisos de los dos módulos.
- Si `docs/roles.md` o el realm dicen lo contrario, alinearlos y correr `scripts/roles-parity.sh`.

### A33. Tablero de ganadores (P0, Banreservas)

El formato de la fiesta de Banreservas: un grupo de premios iguales ("83 premios de 10,000") se
sortea por lotes de hasta 10 y se muestra como lista de número + nombre + área. El frontend ya lo
tiene (`features/sorteo/tablero.ts`, `components/TableroGanadores.tsx`, `TableroConsola.tsx`) y
hoy sortea cada premio del lote con `drawPrize`, uno tras otro.

1. **`PrizeDrawSummary` suma `winnerTicketNumber`, `winnerEmployeeCode` y `winnerDepartment`**
   (del participante del padrón que corresponde al asistente ganador). Sin ellos el tablero muestra
   guiones en el número. Lo mismo en `EligibleParticipantResponse` si es barato.
2. **`visualStyle: 'winner_board'`**: sumarlo al enum y a `ck_raffle_visual_style` (migración).
   Hoy el frontend guarda "tablero" en el navegador y manda `sequential_names` (shortcut marcado
   en `tablero.ts`).
3. **Ajustes del tablero en el sorteo**: `boardTitle`, `boardSubtitle`, `boardIdentifier`
   (`employee_code` | `ticket`), `boardBatchSize` (1-10, **máximo 10 por pantalla**, decisión de
   Robert). Hoy viven en `localStorage` (`sorteo360.tablero.{eventId}`).
4. **Sorteo por lote**: `POST /v1/events/{id}/raffle/prizes/draw-batch { prizeIds: [≤10] }`,
   todo en una transacción, con las mismas reglas que `drawPrize` aplicadas premio a premio (un
   ganador por persona/empresa dentro del lote también). Responde los premios sorteados en orden.
   Si el bombo se agota a mitad, sortea los que pudo y devuelve `RAFFLE_POOL_EXHAUSTED` con
   `extensions.drawn`. El frontend cambia a este endpoint cuando exista.

### A34. Foto del premio (P0)

- `PrizeResponse.imageUrl` (nulo sin foto).
- `POST /v1/events/{id}/raffle/prizes/image` multipart: `file` + `prizeIds` (repetido). Una sola
  imagen guardada, ligada a todos esos premios (los iguales del grupo). JPG, PNG o WebP, máximo
  5 MB; reducirla a 1280 px de lado mayor al guardar **conservando la transparencia** (las fotos de
  producto llegan como PNG recortado: guardar PNG/WebP, nunca convertir a JPEG). Responde `{ imageUrl }`.
- `POST /v1/events/{id}/raffle/prizes/image/remove { prizeIds }`.
- Se puede cambiar con el sorteo en borrador o en curso (no altera nada del sorteo).
- La URL la sirve events-service con caché larga (el id del archivo cambia si cambia la foto).
- El acta no la incluye.

### A35. Participar sin estar presente (P0)

- `RaffleResponse.requiresCheckIn` y el mismo campo en `UpdateRaffleRequest` (por omisión `true`,
  migración con `true` para lo existente). El frontend ya lo manda en el PATCH.
- Con `false`, la tómbola al iniciar es **todo el padrón activo** (sin los dados de baja ni los
  excluidos a mano), haya o no check-in. Solo editable en borrador.
- El acta lo dice: "Participaron todos los colaboradores del padrón" vs. "Participaron los
  presentes".
- **Sorteo sin evento = sin puerta** (Robert, 2026-11-19): el sorteo que se crea solo (sin vincular
  a un evento existente) nace con `requiresCheckIn=false`; el frontend ya lo pide al crearlo. En
  ese sorteo el padrón entra por archivo, API o webhook y **todos quedan presentes**: la
  importación, el alta manual, la API y el webhook marcan `checkedIn=true` sin importar lo que
  mande el cliente, y no hay escáner ni check-in que exponer. El frontend esconde escáner,
  check-in y el filtro "Sin check-in" cuando `requiresCheckIn` es `false`.

### A24. Cuentas (P1)

- Tabla `account`: `id`, `name`, `plan`, `status` (`PendingActivation` | `Active` | `Suspended`),
  `paid_until`, `billing_period` y `created_at`.
- Agregar `account_id` en `event` y en la marca.
- Filtro global de EF Core por el claim `account_id`. Un id de otra cuenta responde **404**.
- Migración: todo lo existente pasa a la cuenta "Banreservas" en `advanced`.
- Contador mensual de sorteos creados (`account_monthly_usage`: `account_id`, `month` y
  `raffles_created`), que se suma al crear un sorteo y nunca se resta.
- El realm suma el mapper del atributo `account_id` al token y el rol compuesto **administrador de
  cuenta**, que trae todos los permisos de la plataforma: Sorteo, Eventos, Convocatorias, marca,
  usuarios y plan.

### A25. Registro y activación (P1)

- Contratos: §3.1.
- Correo por notification-service, con la plantilla "Activa tu cuenta Sorteo360".
- Límite de frecuencia por IP y por correo.
- Tests:
  - el signup no revela si el correo existe;
  - un token vencido devuelve 410;
  - una contraseña rechazada no quema el token;
  - el segundo uso del token devuelve 410.

### A26. Topes del plan (P1)

- Topes en los cuatro caminos de participantes, en sorteos (por mes en Gratis, activos en los de
  pago), rondas, premios (`createPrizes` y `prizes/import`) y usuarios. Códigos en §3.3.
- **Una importación que se pasa se rechaza entera** y dice cuántos caben.
- Bajar de plan no borra nada.
- Un sorteo ya iniciado no se corta.

### A27. `GET /v1/account` (P1)

```json
{
  "plan": "free",
  "status": "Active",
  "billingPeriod": null,
  "paidUntil": null,
  "limits": {
    "participantsPerRaffle": 25,
    "raffles": { "max": 1, "per": "month" },
    "roundsPerRaffle": 1,
    "prizesPerRaffle": 10,
    "users": 1
  },
  "usage": { "rafflesThisMonth": 1, "activeRaffles": 1, "users": 1 },
  "resetsAt": "2026-12-01T04:00:00Z",
  "features": { "projector": false, "api": false, "branding": false },
  "pendingRequest": null
}
```

- `null` en un tope quiere decir sin límite.
- **El proyector no pasa por el servidor** (`BroadcastChannel`), así que el bloqueo vive en el
  frontend y se guía por `features.projector`.

### A28. Solicitudes de plan y pago por transferencia (P2)

- `PlanRequest`: `plan`, `billingPeriod`, `amount` (anual = mensual × 10), `transferReference?`,
  `status` (`Pending` | `Approved` | `Rejected` | `Cancelled`), quién y cuándo, y el motivo si se
  rechaza. Una sola pendiente por cuenta.
- Endpoints:
  - `POST /v1/account/plan-requests` y `DELETE /v1/account/plan-requests/{id}` (cancelar), con el
    rol de administrador de cuenta;
  - `GET /v1/admin/plan-requests?status=Pending` y
    `POST /v1/admin/plan-requests/{id}/approve|reject`, con el rol `platform-admin`, que va fuera
    de las cuentas.
- Aprobar fija el plan y `paid_until` (+1 mes o +12 meses) en la misma transacción.
- Un trabajo diario:
  - avisa 7 días antes de que venza `paid_until`;
  - al vencer, baja la cuenta a `free`.
- Correos: al cliente cuando la solicitud se abre, se aprueba o se rechaza; al superadministrador
  cuando entra una nueva.
- Los datos bancarios se guardan en configuración de la plataforma (variables de entorno), nunca
  en el código.

### A29. Campos visibles del participante (P2)

- Contrato en `plans/sorteo360.md` §10.
- El participante suma `firstName`, `lastName` y `location`; el sorteo suma
  `displayFields {name, identifier, detail}`.
- `PUT /v1/events/{id}/raffle/display-fields`, solo con el sorteo en borrador.
- El acta y el certificado ignoran esta elección: siempre llevan el nombre completo, el boleto y
  el ID.

## 5. Para terminar cada tarea

- Build en 0 errores, más las unitarias y las de integración de lo que tocaste (Testcontainers).
- Migración generada, y probada aplicándola sobre una base que ya tenga los datos de Banreservas.
- `scripts/roles-parity.sh` en verde si tocaste roles.
- Una entrada en el "Handoff" de `plans/sorteo360.md` que diga qué quedó, cómo se verificó y qué
  falta.
