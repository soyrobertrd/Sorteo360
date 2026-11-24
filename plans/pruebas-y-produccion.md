# Sorteo360: pruebas en dev y puesta en producción

Plan del 2026-11-19 (jueves), con la entrega a Banreservas el **miércoles 25 de noviembre**.

Es para Robert, el Agente A (backend) y el Agente B (frontend). Cada tarea dice quién la hace:

- **R**: Robert.
- **A**: Agente A (backend).
- **B**: Agente B (frontend).

Este plan se apoya en:

- La guía de despliegue `docs/deploy-sorteo360.md`, que trae los comandos.
- El plan de producto `plans/sorteo360.md`.
- El encargo del backend `plans/agente-a-cuentas-planes-escala.md`.

Regla que no cambia: **nadie hace commit, push ni despliegue sin la orden explícita de Robert.**

---

## 1. Lo que hoy impide salir a producción

Cada punto rompe el login, expone datos o falla delante de Banreservas. Sin todos resueltos no se
despliega.

| # | Bloqueante | Dónde | Quién | Arreglo |
| --- | --- | --- | --- | --- |
| 1 | El cliente `sorteo360-app` solo acepta `http://localhost:*`: el login en el dominio real falla | `infra/keycloak/sorteo360-realm.json:316-317` | A | Agregar `https://${APP_DOMAIN}/*` a `redirectUris`, `https://${APP_DOMAIN}` a `webOrigins` y `post.logout.redirect.uris`. En un Keycloak ya importado se hace desde la consola (ADR-0016) |
| 2 | Usuarios demo (`operador`, `supervisor`, `admin`, `puerta`, `proyector`) con contraseña `localdev` | El mismo realm, líneas 371-426 | R + A | Borrarlos en la consola justo después del primer arranque y crear los usuarios reales. Hacerlo antes de abrir el DNS |
| 3 | El registro de cuentas (`/v1/accounts/*`) no existe en el backend: "Crear cuenta" fallaría en producción | landing y `/crear-cuenta` | R decide, B ejecuta | **Recomendado para el miércoles:** apagar el registro con `SIGNUP_ENABLED=false` en `.env.prod` (**B17 hecho**: es el valor por omisión del build; con `CONTACT_EMAIL` los planes ofrecen escribir). El landing queda con "Entrar" y "Solicitar una demo" (enlace a correo o WhatsApp), y se enciende cuando A25 esté listo |
| 4 | Importar 10.000 premios se hace premio por premio | `prizes/import`, tarea A30 | A | Implementar A30. Mientras falte, se cae a una llamada por grupo: funciona, pero tarda varios minutos |
| 5 | Sin medir con 25.000 participantes y 10.000 premios | Tarea A31 | A | Correr las pruebas de escala de §3.3 y arreglar lo que no llegue a la meta |
| 6 | 21 pruebas de `RaffleEndpoints` siguen probando reglas anteriores a A4 | events-service | A | Actualizarlas. El CI tiene que quedar en verde |
| 7 | Las pruebas de pantalla (Playwright) prueban CRM y módulos que ya no existen | `frontend/e2e/interruptores-funciones.spec.ts` y otras | B | Reescribirlas para Sorteo360 (§3.1) |
| 8 | Los textos legales están en borrador | `frontend/src/features/legal/legalDatos.ts` | R | Completar razón social, RNC, domicilio y correos. Que lo revise un abogado |
| 9 | Falta el correo remitente y la autenticación del dominio | `.env.prod`: `NOTIFICATIONS_FROM_ADDRESS` y SMTP | R | Ver §4.1, punto 5 |

---

## 2. Ambientes

| Ambiente | Para qué | Cómo se levanta | Datos |
| --- | --- | --- | --- |
| **Demo (mock)** | Revisar pantallas sin backend | `cd frontend && npx vite --port 5199` | Navegador (`localStorage`); se borran con "Borrar datos del sitio" |
| **Dev completo** | Probar de punta a punta con Keycloak, PostgreSQL y Mailpit | `cp .env.example .env && docker compose up -d --build` | Volúmenes locales; `docker compose down -v` los borra |
| **Staging** | El ensayo con el mismo `docker-compose.prod.yml` y certificados reales | El servidor de producción con otro dominio (`staging.`), o el mismo servidor antes de abrir el DNS final | Datos de prueba, que se borran antes de producción |
| **Producción** | Banreservas y, después, clientes | `docker compose -f docker-compose.prod.yml --env-file .env.prod up -d` | PostgreSQL con respaldo diario |
| **Salón** | El día de la fiesta, si el salón no tiene internet confiable | `docker-compose.prod.yml` + `docker-compose.salon.yml` en una laptop | Copia del padrón y los premios; respaldo antes y después del evento |

**Decisión para Robert:** el día de la fiesta se corre en **producción** (en la nube) o en
**modo salón** (laptop local). Recomiendo:

- **Modo salón** si el wifi del lugar es dudoso.
- **Producción** si hay buena conexión cableada.

En los dos casos el check-in de la puerta aguanta cortes de red: guarda los escaneos en cola y los
envía cuando vuelve la conexión.

---

## 3. Pruebas en dev

### 3.1 Automáticas (se corren en cada cambio)

| Qué | Comando | Quién | Meta |
| --- | --- | --- | --- |
| Backend: compilación + unitarias | `dotnet build Sorteo360.Platform.slnx -c Release` y `dotnet test` (unitarias) | A | 0 errores, todo en verde |
| Backend: integración con PostgreSQL real | Proyectos `*.Tests.Integration` (Testcontainers; necesitan Docker) | A | Todo en verde, sin los 21 que hoy fallan |
| Backend: escala | `dotnet test --filter Category=Scale` (A31) | A | Los tiempos de §3.3 |
| Paridad de roles | `scripts/roles-parity.sh` | A | OK |
| Frontend | `npm run build && npm run lint && npm test` | B | 0 errores. Hoy: 172 pruebas en verde |
| Pantallas (Playwright, modo mock) | `npm run test:e2e` | B | Recorridos de Sorteo360 en verde (ver abajo) |
| CI | `.github/workflows/ci.yml` | A + B | Verde antes de promover a `main` |

**B18: reescribir las pruebas de pantalla para Sorteo360.**

- **Recorridos a cubrir:**
  - landing, registro y activación;
  - crear sorteo;
  - importar participantes;
  - importar premios;
  - iniciar sorteo y girar;
  - anular;
  - entregar con firma;
  - acta.
- **Lo que sobra:** se borran las pruebas de CRM y de "mesas y sillas".

### 3.2 Guion manual en el ambiente dev completo (con Keycloak)

El **primer ensayo** lo corre R junto con B, y lo repite Banreservas en staging.

**Datos de prueba.** Tarea B19. Se generan con un solo comando, desde `frontend/` y con Node 20 o
22:

```bash
cd frontend && node scripts/datos-prueba.mjs
```

Deja los archivos en `frontend/datos-prueba/`, que no entra a git. La semilla es fija, así que
cada corrida da los mismos archivos, y el script comprueba sus propias cuentas antes de terminar.

- `padron-25000.xlsx`: 25.000 filas con Nombre, Apellido, ID de empleado, Correo, Departamento,
  Ubicación y Teléfono. Trae 50 filas con problemas, intercaladas para que las tres clases salgan
  entre las 20 que muestra el informe: 20 sin nombre y 10 con el ID repetido, que no entran, y 20
  con correo inválido, que entran sin correo. Se importan **24.970** y el informe marca **50**.
- `premios-10000.csv` (separado por `;`): Ronda, Premio, Valor y Cantidad. Son 20 rondas de 500
  unidades, **10.000** en total, en 463 filas. Cada premio va partido en filas seguidas iguales
  para probar el agrupado (quedan 117 líneas), con montos como `1.250,75`, `RD$5,000.00` y `3500`.
- `padron-25.csv` y `premios-10.csv`: para probar los topes del plan Gratis.

En el modo mock (2026-11-20, portátil de desarrollo): el padrón tarda ~1,7 s en leerse y ~3,4 s en
importarse; los premios, ~0,1 s en leerse y ~1 s en importarse.

**Accesos y permisos** (un usuario por rol):

- [ ] El administrador entra y ve Rifas en vivo, Agenda de eventos, Invitaciones y Marca y ajustes.
- [ ] El operador ve solo lo suyo. Puerta entra directo al escáner. Proyector entra directo a la
      pantalla.
- [ ] Cerrar sesión vuelve al landing. `/app` sin sesión manda al login de Keycloak.
- [ ] `/`, `/terminos`, `/privacidad`, `/crear-cuenta` y `/activar/...` abren sin login.

**Preparación del sorteo:**

- [ ] Crear el sorteo con la marca de Banreservas (logo, colores) y revisar cómo se ve en claro y
      en oscuro.
- [ ] Importar `padron-25000.xlsx`:
  - [ ] la vista previa y el mapeo de columnas;
  - [ ] el informe de filas con problemas;
  - [ ] el tiempo de carga;
  - [ ] la tabla muestra 200 filas y "Mostrar 200 más";
  - [ ] la búsqueda responde sin trabarse.
- [ ] Importar `premios-10000.csv`: se crean las rondas que faltan, el agrupado funciona y se ve el
      tiempo.
- [ ] Editar, mover y borrar premios. Reordenar rondas.
- [ ] Reglas: un premio por persona o por empresa, exclusiones y anulación con motivo.
- [ ] Gafetes con QR y número de boleto: imprimir 10 de muestra y escanearlos.

**Puerta (check-in):**

- [ ] Escanear con la cámara de un celular o tablet. La cámara exige HTTPS: en dev se usa Caddy con
      `tls internal` o se prueba en staging.
- [ ] Buscar por boleto, nombre e ID.
- [ ] **Corte de red:**
  - [ ] apagar el wifi del celular;
  - [ ] escanear 20 personas;
  - [ ] encender el wifi;
  - [ ] la cola se envía sola y no hay duplicados.
- [ ] Dos puertas escaneando a la vez sin conflictos.

**Sorteo en vivo:**

- [ ] Iniciar el sorteo: la tómbola se congela con quienes llegaron.
- [ ] La consola en la laptop y la pantalla de proyector en el segundo monitor o el proyector
      (`/proyector/:eventId`).
- [ ] Ruleta y nombres pasando, a pantalla completa. Revisar la legibilidad desde 10 metros.
- [ ] Girar, revelar al ganador, anular con motivo, volver a girar y verificar que un ganador no
      repita.
- [ ] Refrescar la consola a mitad del sorteo: no se pierde nada.
- [ ] Entrega del premio con firma y código de canje. Certificado del ganador.
- [ ] Cerrar el sorteo:
  - [ ] el acta en PDF sale con todos los ganadores (y se mide el tiempo con 10.000 premios);
  - [ ] el historial de ganadores;
  - [ ] la bitácora.
- [ ] Correos al ganador (en Mailpit, `http://localhost:8025`).

**Landing, registro y textos legales:**

- [ ] El landing se ve en escritorio y en celular: planes, selector mensual/anual y enlaces.
- [ ] Si el registro está encendido (A25 listo):
  - [ ] registro → correo en Mailpit → activar con contraseña → entrar;
  - [ ] enlace vencido;
  - [ ] reenvío a los 60 segundos.
- [ ] `/terminos` y `/privacidad` sin el aviso de borrador.

**Calidad:**

- [ ] En celular de 360 px no hay desplazamiento horizontal.
- [ ] Se puede navegar con teclado.
- [ ] Contraste suficiente con los colores de Banreservas.
- [ ] La consola del navegador sin errores en todo el recorrido.

### 3.3 Pruebas de escala (A31)

| Operación | Meta |
| --- | --- |
| Importar 25.000 participantes (una petición) | < 10 s |
| Listar el padrón | < 1,5 s |
| Leer un sorteo con 10.000 premios | < 1,5 s |
| Cada giro | < 300 ms |
| Check-in por QR o boleto | < 100 ms |
| Acta en PDF con 10.000 premios | < 30 s, sin quedarse sin memoria |
| 5 puertas escaneando a la vez durante 10 minutos | Sin errores 5xx |

Se mide en el servidor de staging con su hardware real, no solo en la laptop.

Lo mide `RaffleScaleTests` (`--filter Category=Scale`), que imprime una fila por operación. Las cifras
de la laptop del 2026-11-20, lo que se arregló y los riesgos que quedan están en el Registro de
`plans/bloques-pendientes.md` (Bloque A3).

### 3.4 Ensayo general (lunes 23 o martes 24)

Con el equipo real del evento:

- la laptop, el proyector o la pantalla, y el router;
- las tablets y celulares de la puerta;
- el padrón y los premios reales de Banreservas.

Se ensaya:

1. Recibir a 30 personas con gafete.
2. Cortar internet.
3. Seguir escaneando.
4. Volver la red.
5. Correr 2 rondas completas.
6. Entregar los premios.
7. Sacar el acta.

Se toma el tiempo de cada paso, y lo que falle se anota y se arregla antes del miércoles.

---

## 4. Puesta en producción

### 4.1 Lo que hay que conseguir (R)

1. **Servidor:**
   - Linux (Ubuntu 24.04 LTS) con **4 vCPU, 8 GB de RAM y 80 GB de SSD** como mínimo, para
     PostgreSQL, Keycloak, 3 servicios .NET, nginx y Caddy.
   - Docker Engine 26 o más y `docker compose` v2.24 o más.
   - Acceso SSH solo con llave, firewall con 22, 80 y 443, y actualizaciones de seguridad
     automáticas.
2. **Dominio y DNS:**
   - Un registro `A` (y `AAAA` si hay IPv6) para `APP_DOMAIN` (por ejemplo `sorteo360.com.do` o
     `app.sorteo360...`).
   - Otro para `AUTH_DOMAIN` (por ejemplo `auth.sorteo360...`).
   - Opcional: `staging.` y `auth.staging.`.
3. **Ruta de la aplicación:**
   - El landing queda en `/` y la plataforma en `/app`.
   - Si Banreservas quiere su propio subdominio, va como otro bloque en el `Caddyfile`.
4. **Datos legales:** razón social, RNC, domicilio, correo de contacto y correo de privacidad
   (§1, punto 8).
5. **Correo saliente:**
   - Una cuenta SMTP: SendGrid, Amazon SES o el relay que ya tenga la empresa.
   - El remitente, por ejemplo `no-reply@<dominio>`.
   - En el DNS:
     - el **SPF**;
     - la **DKIM** que dé el proveedor;
     - un **DMARC**, empezando con `p=none`.

   Sin esto, los correos de activación y de ganador terminan en spam.
6. **Datos bancarios** para los pagos por transferencia. Solo hacen falta cuando se encienda la
   parte comercial (A28).
7. **Lo que entrega Banreservas:**
   - el padrón real (Excel);
   - la lista de premios;
   - el logo en SVG o PNG grande y los colores;
   - quiénes son los operadores, la puerta y el notario;
   - la fecha y hora de la fiesta;
   - el lugar, y si tiene internet.

### 4.2 Secretos (`.env.prod`, nunca en git)

Se parte de `.env.prod.example`, con `chmod 600 .env.prod` y una copia del archivo en un gestor de
contraseñas, no en el servidor solamente.

| Variable | Cómo se genera |
| --- | --- |
| `POSTGRES_PASSWORD` y `EVENTS_/CONVOCATORIAS_/NOTIFICATIONS_/KEYCLOAK_DB_PASSWORD` | `openssl rand -hex 32` cada una |
| `KEYCLOAK_ADMIN_PASSWORD` | `openssl rand -base64 24` |
| `EVENTS_SERVICE_CLIENT_SECRET` y `CONVOCATORIAS_SERVICE_CLIENT_SECRET` | `openssl rand -base64 32` |
| `SMTP_PASSWORD` y `NOTIFICATIONS_FROM_ADDRESS` | Las da el proveedor de correo |
| `APP_DOMAIN`, `AUTH_DOMAIN` y `COMPOSE_PROFILES` | Para Banreservas: `events,notifications,frontend`, más `convocatorias` si se usan las invitaciones por correo |
| `SIGNUP_ENABLED` (llega al frontend como `VITE_SIGNUP_ENABLED`) | `false` el miércoles (B17) y `true` cuando esté A25 |

### 4.3 Preparar la versión (A y B; R da la orden)

1. Todo lo de §1 resuelto, el CI en verde y el guion de §3.2 aprobado en staging.
2. **Con la orden de Robert:** commit, PR y promoción con `scripts/release`, que etiqueta la
   versión candidata, abre el PR y promueve a `main`.
3. Etiqueta de versión, por ejemplo `v1.0.0`, que es la que se despliega. Hay que anotar la
   etiqueta anterior para poder volver atrás.
4. ~~Mover la carpeta y renombrar los proyectos a `Sorteo360`~~: hecho el 2026-11-20 por orden de
   Robert (carpeta `...\Pav - Banreservas\Sorteo360`, proyectos, soluciones, Dockerfile, CI y
   `scripts/check.sh`). Falta compilarlo en el SDK 10 para confirmarlo.

### 4.4 Primer despliegue (paso a paso)

Se hace el día antes de abrir el DNS final, o en staging primero.

1. Clonar la etiqueta en el servidor y crear `.env.prod` (§4.2).
2. Levantar el stack:
   ```bash
   docker compose -f docker-compose.prod.yml --env-file .env.prod up -d --build
   ```
3. Revisar que todo esté `healthy` con `docker compose -f docker-compose.prod.yml ps`, y que los
   logs de `events-service` digan que las migraciones se aplicaron.
4. **Keycloak** (`https://AUTH_DOMAIN/admin`), en este orden:
   1. Cambiar la contraseña del administrador inicial, o crear un administrador nominal y borrar el
      inicial.
   2. **Borrar los 5 usuarios demo** (§1, punto 2).
   3. Cliente `sorteo360-app`: las direcciones de redirección y orígenes de producción (§1,
      punto 1).
   4. Política de contraseñas: mínimo 10 caracteres, con letras y números. Es la misma que pide la
      pantalla de activación.
   5. Activar la detección de fuerza bruta.
   6. SMTP del realm, para el "olvidé mi contraseña".
   7. Tiempos de sesión: 8 horas de inactividad, para que no se cierre en medio de la fiesta.
   8. Crear los usuarios reales de Banreservas con sus roles: administrador, operadores, puerta y
      proyector.
5. Entrar como administrador:
   - Marca y ajustes: logo y colores de Banreservas, y el nombre de la organización.
   - Revisar qué módulos se ven.
6. Cargar los datos reales:
   - crear el sorteo;
   - importar el padrón;
   - importar los premios;
   - revisar las reglas;
   - imprimir los gafetes.
7. **Prueba de humo en producción** (15 min):
   - landing y legales;
   - login con cada rol;
   - una importación chica en un sorteo de prueba;
   - un check-in con celular;
   - un giro en el sorteo de prueba;
   - el PDF del acta;
   - un correo real recibido.

   Al terminar se borra el sorteo de prueba.
8. Abrir el DNS final si se trabajó con uno provisional.

### 4.5 Respaldos

- **Respaldo diario por `cron`:**
  ```cron
  30 3 * * * cd /opt/sorteo360 && ./scripts/backup-postgres.sh /var/backups/sorteo360 14
  ```
- **Copia fuera del servidor:** `rclone` o `aws s3 sync` de `/var/backups/sorteo360` a otro
  almacenamiento. Un respaldo que vive solo en el mismo disco no cuenta.
- **Respaldo manual** antes de cada despliegue, y antes y después de la fiesta.
- **Prueba de restauración**, una vez antes del miércoles y luego cada mes:
  - restaurar el último respaldo en una base aparte (comando en `docs/deploy-sorteo360.md` §4);
  - abrir el sorteo.

### 4.6 Monitoreo

- Un monitor externo de disponibilidad (UptimeRobot, Better Stack o similar) contra
  `https://APP_DOMAIN/` y `https://APP_DOMAIN/_alive`. Avisos por correo o WhatsApp a R.
- **Logs:** `docker compose logs`, con rotación configurada (`max-size: 10m`, `max-file: 5`) para
  que no llenen el disco.
- **Disco:** avisar al pasar del 80 %.
- **Certificados:** Caddy los renueva solo. Revisar que el puerto 80 quede abierto para la
  renovación.

### 4.7 Volver atrás

Las migraciones solo avanzan, así que el respaldo previo es el seguro.

1. Volver a la etiqueta anterior y levantar de nuevo:
   ```bash
   git checkout vX.Y.Z
   docker compose -f docker-compose.prod.yml --env-file .env.prod up -d --build
   ```
2. Si la nueva versión migró la base de una forma que la anterior no entiende, restaurar el
   respaldo que se tomó antes del despliegue.

Lo que se haya cargado entre el respaldo y la vuelta atrás se pierde. Por eso no se despliega el
día de la fiesta.

---

## 5. Día de la fiesta

### Antes

**T-2 días:**

- [ ] Padrón y premios definitivos cargados.
- [ ] Gafetes impresos.
- [ ] Respaldo.

**T-1 día:**

- [ ] **Congelar los cambios:** ningún despliegue hasta después de la fiesta.
- [ ] Ensayo corto en el salón: red, proyector, cámaras de las tablets y certificado de Caddy
      instalado en ellas, si es modo salón.
- [ ] Cargar todos los equipos.

### El día

**T-3 horas, montaje:**

- [ ] Laptop con cable al router.
- [ ] Proyector como segunda pantalla.
- [ ] Tablets de la puerta con sesión iniciada.
- [ ] Respaldo.

**T-1 hora:**

- [ ] Prueba de un escaneo y de un giro de prueba en un sorteo aparte.

**Durante:**

- [ ] Puerta escaneando.
- [ ] Al cerrar la puerta: iniciar el sorteo, que congela la tómbola.
- [ ] Rondas y entregas con firma.
- [ ] El notario presente si aplica.

**Al cierre:**

- [ ] Cerrar el sorteo.
- [ ] Descargar el acta en PDF y guardarla en dos lugares.
- [ ] Respaldo inmediato.

### Plan B

| Si pasa esto | Se hace esto |
| --- | --- |
| Cae el internet | La puerta sigue con la cola local. Si el sorteo corre en la nube, el salón necesita un respaldo de datos (hotspot de celular) o se usa el modo salón |
| Cae la laptop | Una segunda laptop con la sesión abierta. El sorteo vive en el servidor y se retoma donde iba |
| Falla el proyector | La consola del operador también muestra al ganador |

---

## 6. Calendario hasta el miércoles

| Día | A (backend) | B (frontend) | R |
| --- | --- | --- | --- |
| **Jue 19** | A30, A32 y el realm de producción (§1, punto 1) | B17 (apagar el registro), B18 (pruebas de pantalla), B19 (datos de prueba) | Servidor, dominio y proveedor de correo (§4.1) |
| **Vie 20** | A31 (escala) y las 21 pruebas viejas | Guion §3.2 en el ambiente dev completo, junto con R | Datos legales, decisión salón o nube, pedir los datos a Banreservas |
| **Sáb 21 - dom 22** | Arreglos de lo que salga del guion | Arreglos | — |
| **Lun 23** | Staging con `docker-compose.prod.yml` y prueba de escala en ese servidor | Apoyo al ensayo | **Orden de despliegue a staging**, y ensayo general §3.4 |
| **Mar 24** | Producción (§4.4), respaldos y monitoreo | Prueba de humo | Carga de los datos reales de Banreservas |
| **Mié 25** | Guardia | Guardia | **Entrega** |

Si la fiesta es el mismo miércoles, el ensayo general (§3.4) se pasa al lunes 23 sin falta.

---

## 7. Decisiones que necesita Robert

1. **Registro de cuentas el miércoles:** ¿se apaga (recomendado) o se espera a A25?
2. **Dónde corre el día de la fiesta:** nube o modo salón. Y la fecha, hora y lugar.
3. **Servidor y dominio:** proveedor, nombre del dominio y quién administra el DNS.
4. **Correo:** proveedor SMTP y remitente.
5. **Datos legales** del titular.
6. **Renombrar y mover la carpeta:** ¿después del miércoles (recomendado)?
7. **Quién de Banreservas** recibe la capacitación: operadores, puerta y administrador. Hace falta
   una sesión de 1 hora el lunes o el martes.
