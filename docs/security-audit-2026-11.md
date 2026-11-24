# Auditoría de seguridad — noviembre 2026 (run-1)

> **Actualización 2026-11-24:** los 4 confirmados y los pendientes 2, 4, 5 y 7 ya están corregidos
> (S1.3 y S1.4 en `plans/bloques-pendientes.md`). Siguen abiertos el 1, el 3 y el 6 (tarea S1.5).

Estado al 2026-11-23, para retomar en la próxima sesión. Es una revisión **parcial** (perfil `quick`): no afirma cobertura completa.

- **Informe completo y evidencias:** fuera del repo, en `C:\Users\Ro.Guzman\security-audit-skill\sorteo360\run-1\` (`REPORT.md` en español, `FINDINGS-DETAIL.md`, `NEEDS-VALIDATION.md`, `findings.json`, `coverage-ledger.json`, `agents/sandbox-runner/artifacts/`).
- **Código revisado:** el árbol de trabajo sobre el commit `6c77c011` (muchos cambios sin commitear).
- **Sin revisar:** `crm-service/` y lo que solo existe en HEAD.

## Resultado

De 12 candidatos: **4 confirmados, 7 pendientes de validación y 1 descartado.**

### Confirmados (con prueba en sandbox y verificador independiente)

| Severidad | Hallazgo | Dónde | Corrección |
|---|---|---|---|
| Media | El nombre y la empresa del registro anónimo se insertan como HTML sin escapar en el email de activación. `PlanRequestNotifier` repite el mismo patrón en el correo al administrador | `events-service/Sorteo360.Events.Infrastructure/Notifications/AccountOnboardingNotifier.cs:48-52`, `PlanRequestNotifier.cs:65` | Pasar los valores como placeholders en `SharedData`, igual que `RaffleWinnerNotifier` |
| Media | events-service cuenta un salto de proxy y en prod hay dos (Caddy → nginx), así que el límite de alta de cuentas (10/min) queda global. Solo afecta a usuarios legítimos con `SIGNUP_ENABLED=true` | `events-service/Sorteo360.Events.Web/Program.cs:41` | `AddProxyHeaders(proxyHopCount: 2)` y un test |
| Media | Un xlsx de 1,6 KB con una hoja que llega hasta la columna XFD ocupa el parser más de 20 s por subida | `convocatorias-service/Sorteo360.Convocatorias.Infrastructure/Uploads/SpreadsheetParser.cs:67-74` | Rechazar hojas con más de unas 50 columnas antes de MiniExcel, usar `IgnoreEmptyRows = true` y aplicar `MaxDataRows` dentro del bucle |
| Baja | Repetir la clave `email` en el JSON salta el límite por correo del registro público: el limitador lee la primera y el binder la última | `convocatorias-service/Sorteo360.Convocatorias.Web/RateLimiting/PublicRateLimitPartitions.cs:152` | `AllowDuplicateProperties = false` en `ConfigureHttpJsonOptions` |

### Pendientes de validación (necesitan Postgres; el test exacto de cada uno está en `NEEDS-VALIDATION.md`)

1. **convocatorias-service no tiene tenants, solo comprueba roles.** Un `account_admin` (que el registro anónimo concede) puede listar, exportar `participants.csv`, lanzar campañas y registrar entradas de cualquier tenant.
2. **`prizes/draw-batch` y `prizes/image[/remove]` no comprueban de qué cuenta es el evento.** Permiten sortear y cambiar fotos en la rifa de otro tenant. Causa: `RaffleService.cs:1161`, `:1000` y `:1066` usan `LoadAsync` sin `GetEventStatusAsync`.
3. **Una inscripción pública repetida devuelve el QR de acceso de otra persona** a quien conozca su email. Es un fallo de diseño: la spec 06 §4.1 lo pide.
4. **`GET prize-draws/by-code/{code}` busca el código en todos los tenants.** Causa: `RaffleRepository.cs:1113`.
5. **`DELETE events/{id}/images/{imageId}` permite borrar imágenes de eventos de otro tenant.** Causa: `EventImageRepository.cs:96`.
6. **El SPA se puede meter en un iframe**: no hay X-Frame-Options ni frame-ancestors en Caddy ni en nginx.
7. **Otro tenant puede ocupar primero la clave de branding de un evento** y dejar al dueño con 409.

Los pendientes 2, 4, 5 y 7 tienen la misma causa: las tablas hijas no tienen filtro de cuenta en EF. Un filtro que pase por el evento, o un `account_id` desnormalizado, los cierra todos.

### Descartado

**"Keycloak de producción importa usuarios de demo con `localdev`."** Keycloak 26.6 aborta el import (`invalidPasswordMinLengthMessage`) porque la política `length(10)` del realm rechaza `localdev`. Termina con código 1 sin crear usuarios. Para operaciones esto implica dos cosas:
- **Volumen nuevo:** un despliegue con este realm no arranca Keycloak.
- **Producción ya en marcha:** su realm no salió de este archivo tal como está hoy. Hay que revisar en la consola qué usuarios tiene.

## Próximos pasos, en orden

1. ~~**Cerrar `/v1/accounts/signup` y `/activate` en el backend.**~~ Hecho el 2026-11-24 (`Accounts__SignupEnabled`, 404 apagado, `SignupGateTests`). `SIGNUP_ENABLED=false` solo oculta las páginas del SPA, y esas rutas son la puerta para el pendiente 1 y para el confirmado del xlsx. La otra opción es quitar los roles de convocatorias del compuesto `account_admin`.
2. ~~**Actualizar `System.Security.Cryptography.Pkcs 10.0.0`.**~~ Hecho el 2026-11-24: fijado a 10.0.15 en `Directory.Packages.props` (GHSA-846f-f764-76mx y GHSA-2fmv-rv2c-59x7).
3. ~~**Corregir los cuatro confirmados.**~~ Hecho el 2026-11-24, cada uno con su prueba: los correos de activación y de plan pasan los valores en `SharedData` (`OnboardingAndPlanNotifierEscapingTests`); `proxyHopCount: 2` (`ProxyHeadersTests`); xlsx de más de 50 columnas da `400 UPLOAD_TOO_MANY_COLUMNS` antes de MiniExcel (`SpreadsheetParserTests`); JSON con clave repetida da 400 en convocatorias (`PublicRateLimitingTests`).
4. **Pendientes 2, 4, 5 y 7: hechos el 2026-11-24** (`EventChildTenantIsolationTests`). El filtro pasa por el evento: `draw-batch` y la foto de premios leen el evento primero, `by-code` hace join con `Events`, `EventImageData` tiene filtro por `Events.Any`, y el branding de evento da 404 si el evento no es de la cuenta. No se puso filtro global en `Raffles`: los webhooks anónimos leen la rifa sin cuenta. **Siguen abiertos el 1 (convocatorias sin tenants), el 3 (QR de otra persona) y el 6 (iframe).**
5. **Revisar en la consola de Keycloak de producción los usuarios del realm `sorteo360`.**
6. **Lanzar una auditoría `standard`** sobre las seis áreas que no se revisaron:
   - subida y entrega de archivos en events, y los PDFs (acta notarial y credenciales);
   - límites de importación masiva y del PDF de credenciales;
   - solicitudes de plan y la excepción de platform-admin en `/v1/admin`;
   - workers en segundo plano sin usuario en la petición (`RaffleWinnerNotificationWorker`, `PlanExpirationWorker`, `CampaignHandoffWorker`, el drain de notificaciones);
   - llamadas de convocatorias a events con la cuenta de servicio, que podrían llevar datos de eventos de otro tenant a un correo de campaña;
   - alcance del historial de notification-service (`HistoryScope.ForCaller`).

## Endurecimiento (no son hallazgos)

**Despliegue y borde**
- La consola de Keycloak (`/admin` y `/realms/master`) es pública en AUTH_DOMAIN.
- `.gitignore` no cubre `.env.prod`.
- Ni Caddy ni nginx envían cabeceras de seguridad (CSP, HSTS, Referrer-Policy, nosniff).
- Las imágenes `caddy:2-alpine` y `keycloak:26.6` no están fijadas por digest.
- El realm de prod mantiene el redirect `http://localhost:*` y no exige PKCE.
- El ADR-0016 está desactualizado: dice que prod no monta el realm.
- `docker-compose.salon.yml` define `NOTIFICATIONS_TRANSPORT`, pero el servicio no la lee.

**Webhooks de rifas**
- El chequeo de plan y de existencia del evento va antes que el de la clave.
- La ruta por evento no tiene HMAC ni ventana anti-replay.
- La respuesta de importación devuelve el roster completo con datos personales.
- Queda un fallback muerto a una clave global.
- La UI propone un `whsec_<eventId>` predecible.

**events**
- `proyector` lee datos personales (es intencional).
- `DrawBatchAsync` responde 404 después del commit.
- Los feature-switches anónimos exponen `ChangedById` y `ChangedByName`.
- La validación del email de registro es solo `Contains('@')`.

**convocatorias**
- El 200 frente al 201 del registro público revela si un email está inscrito.
- Las rutas internas no tienen rate limit.
- El check-in de `event_staff` no está limitado por evento.

**notification-service**
- No se valida la URL de List-Unsubscribe.
- El asunto solo elimina CR/LF.

**Frontend**
- El modal de webhooks guarda un header Authorization en localStorage y el logout no lo borra.
- `http.ts` no codifica los segmentos de ruta.
- El branding en localStorage se comparte entre cuentas.

## Cómo retomar

- **Repetir las pruebas confirmadas.** El arnés está compilado en `...\run-1\agents\sandbox-runner\scratch\src\harness`, sobre una copia del código del 2026-11-23. Si el código cambió, primero hay que volver a copiar los proyectos. Para correrlo, sin red:

  ```bash
  SC="C:/Users/Ro.Guzman/security-audit-skill/sorteo360/run-1/agents/sandbox-runner/scratch"
  MSYS_NO_PATHCONV=1 docker run --rm --network none --memory 3g --cpus 4 --pids-limit 1024 \
    -v "$SC/src:/src" -w /src/harness -e HOME=/src/.home -e DOTNET_CLI_HOME=/src/.home \
    -e NUGET_PACKAGES=/src/.nuget mcr.microsoft.com/dotnet/sdk:10.0 \
    dotnet bin/Release/net10.0/Harness.dll all
  ```

  Después de corregir, los confirmados deben dar:
  - **5 (email):** el HTML sale escapado (`&lt;a`).
  - **6 (proxies):** con la configuración nueva, la IP del cliente.
  - **7 (`email` duplicado):** el cuerpo se rechaza.
  - **8 (xlsx):** el rechazo llega en menos de 1 s.
- **Próxima auditoría.** Lanzar `/security-audit` con perfil `standard` en `...\security-audit-skill\sorteo360\run-2`. La corrida leerá el ledger y los hallazgos de run-1 como corrida previa.
- **Validadores en Windows.** Los validadores del skill no leen archivos en Windows porque Node no tiene `O_NOFOLLOW`. Usa el envoltorio guardado junto a la evidencia, que llama a `validateDocument` sin cambios: `node ...\run-1\validate-windows.cjs findings|ledger <archivo.json>`.
- **Disco.** Las imágenes `mcr.microsoft.com/dotnet/sdk:10.0` y `quay.io/keycloak/keycloak:26.6` siguen descargadas, y la caché de NuGet está en el scratch. Se pueden borrar cuando ya no hagan falta.
