# Plan: exclusión en lote de un sorteo (ganadores de otro sorteo o archivo importado)

Estado: **hecho (2026-11-25), sin commit** · Redactado el 2026-11-24 · Rama de trabajo: `feat/exclusion-en-lote`
(sale de `development`).

## Qué se quiere

En el sorteo B, antes de arrancarlo, el operador saca del bombo a un grupo de personas en una sola
acción. Hay dos fuentes:

1. **Ganadores de otro sorteo**: elige el sorteo A (otro evento de la misma cuenta) y todos los
   ganadores **vivos** de A quedan excluidos en B. Un ganador anulado no se llevó nada y no cuenta.
2. **Archivo importado**: un Excel/CSV/TXT con emails o códigos de empleado.

Cada persona excluida se puede volver a meter una por una con el "quitar exclusión" que ya existe.
Regla de negocio: **quien ganó en un sorteo de la fiesta no gana en otro.**

### Identidad: email **o** ID, sin correos inventados

**Decisión de Robert (2026-11-24): el sistema no inventa correos.** Hoy el padrón le da a quien llega
sin correo uno interno (`…@sorteo.local`, `RaffleParticipant.SyntheticEmailDomain`,
`RaffleParticipantRepository.cs:~405-420`). Esa función se elimina en el **paso 0**. Después del
paso 0:

- Un participante tiene **email**, **ID** (`employee_code`, "ID de empleado"; el lector de archivos
  lo toma de las columnas `id`, `codigo`, `cedula`, etc., `participantFile.ts:64-80`) o los dos. Una
  fila sin ninguno de los dos se **rechaza** al cargar el padrón y se informa en el resultado del
  import.
- La clave de una persona dentro de un evento es `identity_key`: el email normalizado si tiene, y
  si no, `id:` + el ID recortado y en minúsculas. Es una clave interna: nunca se muestra, nunca se
  usa como dirección y no es un correo.
- Entre eventos, una persona se reconoce si coincide **el email o el ID**: con uno basta.
- Quien no tiene correo no recibe correos (por ejemplo, el aviso al ganador): queda como "sin
  correo", no falla.

Dentro del evento destino, la exclusión se guarda como `AttendeeOverride` sobre la `identity_key`
del asistente.

Fuera de alcance: varios sorteos por evento (ver "Después" al final), excluir automáticamente sin que
el operador lo pida, y cambiar exclusiones con el sorteo ya arrancado.

## Lo que ya existe (no reconstruirlo)

- **Exclusión manual de a uno**: `AttendeeOverride` (`events-service/Sorteo360.Events.Core/Raffles/AttendeeOverride.cs`),
  decisión `excluded`, identidad = **email normalizado**. Endpoints `POST/DELETE
  /v1/events/{eventId}/raffle/attendees/overrides` (`RaffleEndpoints.cs:142-160`). Servicio
  `RaffleService.SetOverrideAsync` / `RemoveOverrideAsync` (`RaffleService.cs:1085-1118`).
- Las decisiones solo se escriben con el sorteo en **borrador** (`LoadDraftForWriteAsync`; si no,
  `409 RAFFLE_NOT_DRAFT`). Al arrancar, el bombo se congela.
- `SetOverrideAsync` exige que la persona esté en la lista de la puerta del evento
  (`FindAtTheDoorAsync`). La versión en lote **no falla** por eso: la salta y la informa.
- El bombo ya respeta las exclusiones: `Qualification.Classify` → motivo `manually_excluded`.
- El padrón del evento es `raffle_participant`, con índices únicos por `(event_id, email_normalized)`
  y `(event_id, employee_code)`, así que un código de empleado se traduce a email dentro del evento.
- El frontend ya lee archivos de participantes: `frontend/src/features/sorteo/participantFile.ts`
  (+ `participantFile.test.ts`), usado por `CargarParticipantesModal.tsx`.
- La tabla `ParticipantesTable.tsx` ya muestra `manually_excluded` y el quitar de a uno.

## Reglas para todos los agentes

- Leer primero `CLAUDE.md` (raíz), `events-service/CLAUDE.md` y `frontend/CLAUDE.md`. Mandan.
- Nombres de dominio en inglés; textos de UI, logs y comentarios en español (ADR-0008).
- Todo error lleva su `code` de máquina (ProblemDetails del chassis).
- Frontend: toda llamada pasa por `ApiClient`, con **las dos** implementaciones (http y mock), y el
  mock replica las reglas y los `code` de error.
- Multi-tenant: el sorteo fuente tiene que ser de la **misma cuenta** que el destino. Usar los
  filtros de cuenta existentes (S1.4). Un evento de otra cuenta responde `404`, igual que uno que
  no existe.
- **No hacer commit ni push** salvo que Robert lo pida. Al terminar cada paso, dejar el resumen y la
  salida de las pruebas.

## Contrato (fijo: backend y frontend trabajan contra esto)

```
POST /v1/events/{eventId}/raffle/attendees/overrides/bulk-exclude
Autorización: la misma política que POST .../attendees/overrides
```

Request (exactamente una de las dos fuentes):

```json
{
  "sourceEventId": "guid | null",
  "identifiers": ["ana@banco.com", "E-10234"],
  "note": "texto opcional | null"
}
```

- `sourceEventId`: excluye a los ganadores vivos del sorteo de ese evento.
- `identifiers`: emails o IDs; hasta 5000, se recortan y se descartan los vacíos y los repetidos.
  Si el valor contiene `@` es un email; si no, es un ID (`employee_code`)
  del padrón del evento destino.
- `note`: si viene vacía, el servidor pone `Ganador de «{nombre del evento A}»` o
  `Importado en lote`.

Response `200`:

```json
{
  "excluded": 12,
  "alreadyExcluded": 2,
  "notInRoster": ["x@y.com", "E-999"],
  "invalid": ["no-es-nada"],
  "unidentifiable": ["María Pérez"]
}
```

- `excluded`: exclusiones nuevas, o decisiones `included` que pasaron a `excluded`.
- `alreadyExcluded`: ya tenían `excluded`; no se tocan.
- `notInRoster`: no están en la puerta o el padrón del evento destino.
- `invalid`: no parece email y no hay ningún participante con ese ID.
- `unidentifiable`: solo con `sourceEventId`. Son los nombres de los ganadores de la fuente que no
  tienen email ni ID (solo pasa con filas viejas `legacy:` del paso 0), así que no se pueden buscar en el destino. El operador los excluye a
  mano. Con `identifiers` siempre es `[]`.

Errores:

| Status | code | Cuándo |
|---|---|---|
| 400 | `RAFFLE_BULK_EXCLUDE_SOURCE_REQUIRED` | ninguna fuente o las dos a la vez |
| 400 | `RAFFLE_BULK_EXCLUDE_TOO_MANY` | más de 5000 identificadores |
| 400 | `RAFFLE_BULK_EXCLUDE_SAME_EVENT` | `sourceEventId` igual a `eventId` |
| 404 | `RAFFLE_NOT_FOUND` | el destino o la fuente no tienen sorteo (o son de otra cuenta) |
| 409 | `RAFFLE_NOT_DRAFT` | el destino ya arrancó |

Todo o nada: las exclusiones se escriben en **una transacción**. Un fallo no deja medio lote escrito.

---

## Pasos

Orden: **0** primero (cambia la identidad de la que dependen todos). Después **1 → 2 → 3** y
**4 → 5**, y **6** al final. Con el contrato fijo, 4–5 pueden correr en paralelo con 1–3.

### Paso 0 — Quitar los correos inventados (`@sorteo.local`)

**Agente:** backend .NET + frontend (pueden ser dos agentes, en este orden). **Depende de:** nada.
Es el paso más delicado: el email es hoy la identidad del asistente en unos 43 archivos del backend
(`grep -rl EmailNormalized events-service --include=*.cs`). Inventariar esos archivos **antes** de
tocar nada.

Backend:

1. **Migración EF** (la única autoridad del esquema):
   - `raffle_participant.email_normalized` pasa a admitir null.
   - Columna nueva `identity_key` (NOT NULL) en `raffle_participant`, `raffle_attendee` y
     `raffle_attendee_override`. Los índices únicos `(event_id, email_normalized)` y
     `(raffle_id, email_normalized)` pasan a ser sobre `identity_key`. El índice por
     `employee_code` se queda.
   - Datos existentes: las filas con correo `@sorteo.local` pasan a `email_normalized = NULL`, y su
     `identity_key` es `id:{employee_code}`. Las filas viejas sin código (las del correo al azar)
     reciben `legacy:{id de la fila}`: es el único caso en que se conserva una clave sin email ni
     ID, solo para no perder datos ya cargados. Las demás filas: `identity_key = email_normalized`.
2. **Dominio:** `RaffleParticipant`, `Attendee`, `RaffleAttendee` y `AttendeeOverride` llevan
   `IdentityKey` y un `Email` opcional. Borrar `SyntheticEmailDomain` e `IsSyntheticEmail`, y el
   generador de `RaffleParticipantRepository.cs:~405-420`. Una sola función
   `PersonIdentity.KeyFor(email, employeeCode)` arma la clave: no repetir la regla.
3. **Import y webhook de participantes:** una fila sin email ni ID se rechaza con su número de fila
   y el motivo `missing_email_and_id` en el resultado del import. No falla el lote entero.
4. Cambiar los usos de `EmailNormalized` **como identidad** por `IdentityKey`: el bombo
   (`Qualification`, `EligiblePool`), los overrides, el check-in, el congelado al arrancar y el
   acta. Los usos **como dirección** (el aviso al ganador en `RaffleWinnerNotifier`, el gafete en
   `RaffleBadgeRenderer`) leen `Email` y, si es null, no envían ni imprimen: el aviso queda en
   estado `skipped_no_email`.
5. La fuente de convocatorias (`ConvocatoriasAttendanceSource`) siempre trae email: su
   `identity_key` es el email. No se toca su contrato.
6. **API:** los DTO de participantes y asistentes devuelven `email: string | null`. El override de
   a uno (`POST/DELETE …/attendees/overrides`) acepta `identityKey` además de `email`, porque
   alguien sin correo también se tiene que poder excluir e incluir a mano.

Frontend:

7. `src/api/types.ts`: `email` pasa a `string | null` y aparece `identityKey`. Quitar del mock
   (`mockClient.ts:~3655-3675`, `seed.ts:~2509`) toda generación de `@sorteo.local`, y en
   `raffleView.ts:~132` el helper que los ocultaba pasa a mostrar "Sin correo" cuando es null.
8. La carga del padrón (`participantFile.ts`, `CargarParticipantesModal.tsx`) muestra en la vista
   previa las filas que se van a rechazar por no tener ni email ni ID.

**Decisión abierta para Robert:** la opción "generar código" del import (`participantFile.ts:~246`)
le pone un ID automático a quien no trae ninguno. No es un correo, pero también es un dato
inventado, y no sirve para reconocer a la persona en otro evento. Hasta que Robert decida, se
mantiene.

**Listo cuando:**
- `grep -rn "sorteo.local" events-service frontend/src` no encuentra nada fuera de la migración.
- La suite completa del backend y `npm run build && npm run lint && npm test && npm run test:e2e`
  pasan.
- Prueba nueva: un padrón con una persona solo con ID, otra solo con email y una sin nada → se
  cargan 2 y se rechaza 1. La persona solo con ID entra al bombo, puede ganar y en el acta sale sin
  correo.
- La migración se probó sobre una copia de una base con datos `@sorteo.local`.

### Paso 1 — Backend Core: el caso de uso

**Agente:** backend .NET. **Depende de:** nada.

1. `Core/DTOs/BulkExcludeAttendeesDto.cs`: `EventId`, `SourceEventId?`, `Identifiers`
   (`IReadOnlyList<string>?`), `Note?`, `DecidedById`.
2. `Core/Raffles/BulkExcludeOutcome.cs`: record con `Excluded`, `AlreadyExcluded`, `NotInRoster`,
   `Invalid` y `Unidentifiable`.
3. `IRaffleService.BulkExcludeAsync(dto, ct)` → `Result<BulkExcludeOutcome, BaseError>`, y su
   implementación en `RaffleService`:
   - Validar las fuentes y el tope (errores 400 del contrato).
   - `LoadDraftForWriteAsync(dto.EventId)`, igual que `SetOverrideAsync`.
   - **Fuente sorteo:** cargar el sorteo del evento fuente con el filtro de cuenta y tomar sus
     sorteos de premio **vivos** (`PrizeDraw.IsLive`). De cada ganador sacar su **email** y su **ID**,
     cruzando la `identity_key` del asistente ganador con el padrón (`raffle_participant`) del
     evento **fuente**, en una sola consulta. El ganador que no
     tenga ninguno de los dos va a `Unidentifiable`, con su nombre. Un sorteo fuente sin ganadores
     da `excluded = 0`, no es error.
   - **Fuente identificadores:** clasificar cada valor como email o ID (regla de "Identidad").
   - **Resolver en el destino**, con la misma función para las dos fuentes: buscar cada
     email o ID en el padrón del evento **destino** (`IRaffleParticipantRepository`, una sola
     consulta por lote con `email_normalized IN (…) OR lower(employee_code) IN (…)`, nunca N+1) y
     quedarse con la `identity_key` de cada participante encontrado.
     Lo que no aparece va a `NotInRoster` (o a `Invalid` si era un ID con formato imposible).
   - Leer **una vez** la lista de la puerta del destino (la misma fuente que `FindAtTheDoorAsync`,
     respetando `RequiresCheckIn`): el que está en el padrón pero no en la puerta también va a
     `NotInRoster`.
   - Leer los overrides existentes del sorteo y separar entre `AlreadyExcluded` y por escribir.
4. Repositorio: `UpsertOverridesAsync(raffleId, IReadOnlyList<AttendeeOverride>, ct)` en una sola
   transacción, junto a `UpsertOverrideAsync`, que ya existe.
5. Errores nuevos con sus `code`, junto a los demás del sorteo (buscar dónde vive `RAFFLE_NOT_DRAFT`).

**Listo cuando:** compila, y las pruebas unitarias de la clasificación (presentes, no presentes,
inválidos, ya excluidos, dedupe y mayúsculas) pasan en `Sorteo360.Events.Tests.Unit`.

### Paso 2 — Backend Web: el endpoint

**Agente:** backend .NET. **Depende de:** paso 1.

1. `Web/RaffleEndpoints/BulkExcludeAttendeesRequest.cs` y `BulkExcludeAttendeesResponse.cs` (camelCase, como el contrato).
2. Registrar `group.MapPost("attendees/overrides/bulk-exclude", BulkExcludeAttendees)` en
   `RaffleEndpoints.cs`, al lado de `SetAttendeeOverride`: misma política, `WithName`,
   `WithSummary` en `RaffleEndpointsDescriptions` y los `Produces` del contrato.
3. `DecidedById` sale de `ICurrentUser`, igual que en `SetAttendeeOverride` (`RaffleEndpoints.cs:~410`).
4. Agregar el request a Bruno: `events-service/requests/bruno/Raffles/Bulk Exclude Attendees.bru`.

**Listo cuando:** el endpoint aparece en `/openapi/v1.json` con el contrato exacto.

### Paso 3 — Backend: pruebas de integración

**Agente:** backend .NET. **Depende de:** paso 2.

En `Sorteo360.Events.Tests.Integration/RaffleEndpoints/`, con las factories existentes
(`RaffleAttendanceApiDbFactory`, ver `events-service/docs/raffles.md`):

1. A completo con 2 ganadores vivos y 1 anulado; B en borrador con los 3 en la puerta → el
   bulk-exclude desde A excluye exactamente a los 2 vivos, y `GET …/raffle/eligible` de B ya no los trae.
2. Repetir la llamada → `excluded = 0`, `alreadyExcluded = 2` (es idempotente).
3. Un ganador de A que no está en la puerta de B → aparece en `notInRoster`, y no hay error.
4. Identificadores mezclados (email en mayúsculas, código de empleado, basura) → conteos correctos.
5. B arrancado → `409 RAFFLE_NOT_DRAFT`, y no se escribió nada.
6. Fuente de otra cuenta → `404`. Las dos fuentes o ninguna → `400` con su code. 5001 ids → `400`.
7. Un `included` previo pasa a `excluded` y se cuenta en `excluded`.
8. **Sin email, con ID:** una persona con el ID `E-55` y sin correo en A y en B (cada evento le da
   su correo interno) gana en A → el bulk-exclude desde A la excluye en B por el ID.
9. **Sin email ni ID:** un ganador de A con una fila vieja `legacy:` → sale en `unidentifiable` con
   su nombre y no se excluye a nadie por error en B.
10. **Importar IDs:** un archivo con `E-55` (sin email) excluye a esa persona en B.

**Listo cuando:** `dotnet test events-service/Sorteo360.Events.Tests.Integration --filter BulkExclude`
pasa y la suite completa del sorteo sigue en verde. Verificación de plataforma: `scripts/check.sh`.

### Paso 4 — Frontend: contrato, cliente HTTP y mock

**Agente:** frontend. **Depende de:** el contrato (no del backend terminado).

1. `src/api/types.ts`: `BulkExcludeAttendeesRequest` y `BulkExcludeAttendeesResponse`, con los nombres exactos del contrato.
2. `src/api/client.ts`: `bulkExcludeAttendees(eventId, body)`, junto a `setAttendeeOverride` (línea ~273).
3. `src/api/http.ts`: la implementación (`POST …/attendees/overrides/bulk-exclude`).
4. `src/api/mock/mockClient.ts`: la misma regla que el backend. Solo en borrador; si no,
   `RAFFLE_NOT_DRAFT`. Excluye a los ganadores vivos de la fuente reconociéndolos por **email
   o ID** (ver "Identidad"), devuelve los cinco grupos y usa los mismos codes.
5. `src/lib/labels.ts`: textos en español de los codes nuevos.
6. Hook `useBulkExcludeAttendees(eventId)` en `src/features/sorteo/hooks.ts`, que invalida el
   sorteo, la lista de asistentes y el bombo.

**Listo cuando:** `npm run build && npm run lint && npm test`, con una prueba unitaria del mock
(los cinco grupos, una persona sin correo excluida por ID y `RAFFLE_NOT_DRAFT`).

### Paso 5 — Frontend: la pantalla

**Agente:** frontend. **Depende de:** paso 4.

1. Componente nuevo `src/features/sorteo/components/ExcluirEnLoteModal.tsx`, con dos pestañas:
   - **"Ganadores de otro sorteo"**: un selector con los sorteos de `useRaffleList()` menos el
     actual, mostrando el nombre, la fecha y cuántos ganadores tiene. Confirmar con `ConfirmDialog`.
   - **"Importar archivo"**: reusar `participantFile.ts` para leer Excel/CSV/TXT. De cada fila se
     envía el email si tiene y, si no, el ID (las mismas columnas `id`/`codigo`/`cedula` que ya
     reconoce el padrón). Una fila sin ninguno de los dos se descarta y se cuenta en la vista previa.
     Se acepta también un TXT con un valor por línea. Mostrar una vista previa (cantidad y primeras
     filas) antes de enviar.
   - Campo opcional de nota.
2. Resultado: `toast` con "12 excluidos · 2 ya estaban". Si hay `notInRoster`, `invalid` o
   `unidentifiable`, una lista desplegable dentro del modal para que el operador vea a quién no
   encontró. Los `unidentifiable` llevan el aviso "sin email ni ID: exclúyelos a mano en la tabla".
   Quien no tiene correo se muestra con su nombre y su ID.
3. Botón **"Excluir en lote"** en la sección de participantes de `SorteoDetailPage.tsx`:
   - visible solo con el sorteo en borrador y el permiso de las exclusiones de a uno
     (`useCan(ROLES.…)`, el mismo que usa `ParticipantesTable`);
   - fuera de borrador, deshabilitado con un `title` que explique que el bombo ya está congelado.
4. Las reglas de `frontend/CLAUDE.md`: `type="button"`, nada de hex, nada de `alert`/`confirm`, todo en español.
5. Prueba e2e en modo mock (`npm run test:e2e`): desde el sorteo A completo, excluir sus ganadores
   en B y ver que aparecen como "Excluido manualmente".

**Listo cuando:** build, lint, unitarias y el e2e nuevo pasan.

### Paso 6 — Documentación

**Agente:** cualquiera. **Depende de:** pasos 2 y 5.

1. `docs/specs/raffles/01-qualification.md` §4.2: agregar la exclusión en lote, sus dos fuentes y
   la regla "solo en borrador; los que no están en la puerta se informan, no fallan".
2. `events-service/docs/raffles.md`: un bullet sobre `BulkExcludeAsync` (una lectura de la puerta,
   una transacción, la fuente filtrada por cuenta).
3. `GLOSSARY.md`: el término "exclusión en lote".
4. `plans/bloques-pendientes.md`: registrar el bloque como hecho, con fecha.

### Verificación final (quien integra)

```bash
scripts/check.sh                       # backend y compose
cd frontend && npm run build && npm run lint && npm test && npm run test:e2e
```

Prueba manual en el stack local (`docker compose up -d`):

1. Sorteo A: correr 3 premios y anular 1.
2. Sorteo B: "Excluir en lote" → "Ganadores de otro sorteo" → A. Tienen que quedar excluidos los 2 vivos.
3. Arrancar B y comprobar que esos 2 no salen.
4. Importar un CSV con un código de empleado y un email que no existe → el resumen lo informa.

---

## Después (no es parte de este plan)

**Varios sorteos por evento** (Evento → N sorteos → N rondas → N premios × cantidad), con exclusión
automática entre los sorteos de la fiesta y un solo padrón y check-in. Exige quitar el índice único de
`raffle.event_id`, pasar las rutas a `/v1/events/{eventId}/raffles/{raffleId}/…`, bloquear la fila
del evento al sortear (`EventRowLock`) y llevar el acta y el proyector a nivel de sorteo. Va en su
propio plan cuando se decida. La exclusión en lote sigue sirviendo ahí para listas externas.
