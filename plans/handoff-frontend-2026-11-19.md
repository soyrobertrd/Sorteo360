# Handoff del frontend (Agente B), 2026-11-19

Resumen para retomar en otra sesión. Nada de esto tiene commit: **no hacer commit ni push sin la
orden explícita de Robert**. La carpeta ya vive en `...\Pav - Banreservas\Sorteo360` y los
proyectos .NET se llaman `Sorteo360.*` (renombrado del 2026-11-20, por orden de Robert).

## Cómo trabajar

- Node 22: `export PATH="/c/Users/Ro.Guzman/AppData/Roaming/nvm/v22.14.0:$PATH"`.
- Verificar: `npx tsc -p tsconfig.app.json --noEmit`, `npm run lint` (0 errores, 3 avisos viejos),
  `npm test` (175 pruebas en verde), `npm run build`.
- Demo: `npx vite --port 5199 --strictPort` en `frontend/` (modo mock). Datos en `localStorage`
  bajo `sorteo360.mock.state.v3`. Si el navegador da "Cannot read properties of undefined", es el
  servidor de Vite desincronizado: reiniciarlo.
- Reglas del frontend: `frontend/CLAUDE.md`. Sistema visual: `frontend/DESIGN.md`.

## Planes vivos

- `plans/sorteo360.md`: plan general (§9 cuentas y planes, §10 campos visibles).
- `plans/agente-a-cuentas-planes-escala.md`: **encargo del backend**. §0 con hallazgos R1–R15 y
  S1–S5 (revisión de código, seguridad, escala); §3 contratos que el frontend ya llama; tareas
  A24–A35. Prioridad P0 para el miércoles: R1–R3, R12, A30–A35.
- `plans/pruebas-y-produccion.md`: pruebas en dev, puesta en producción, día de la fiesta,
  calendario y decisiones pendientes de Robert.

## Hecho en el frontend

**Producto y cuentas**
- Landing en `/` (`features/landing`): portada con el tablero real en una ventana de app (lote cada
  12 s, con pausa), franja de cifras del producto, rejilla de funciones con el tablero en modo
  premio principal, cómo funciona, planes (Básico, Pro, Avanzado; el Gratis no se publicita),
  preguntas frecuentes, términos y privacidad. Plataforma en `/app`.
- Registro `/crear-cuenta` (sin contraseña) y activación `/activar/:token` (una contraseña con
  "Mostrar"). Contra `/v1/accounts/*`. Apagados en producción por omisión
  (`SIGNUP_ENABLED=false` en `.env.prod`; con `CONTACT_EMAIL` los planes ofrecen escribir).
- Textos legales en `features/legal` (Ley 172-13), en borrador hasta completar
  `legalDatos.ts` (razón social, RNC, domicilio, correos).
- Catálogo de planes en `features/cuenta/planes.ts` (copia del servidor; precios `null` hasta que
  Robert los fije). Pago mensual o anual (anual = 10 meses).

**Sorteo**
- Importar premios desde Excel/CSV con columnas Ronda, Premio, Valor y **Cantidad** (una fila de 25
  son 25 unidades), mapeo de columnas con vista previa, tope de 10.000 unidades. Usa
  `POST …/raffle/prizes/import` y cae a `createPrizes` en tandas de 50 si el servidor no lo tiene.
- Premios iguales agrupados en una línea con su cantidad y avance (`PremiosAgrupados`), se abre
  para ver cada unidad.
- Foto del premio: modal con subir archivo (PNG transparente, JPG, WebP, 5 MB) o pegar enlace
  `https://`, también en "Agregar premio". Se aplica a los iguales de la ronda.
- **Tablero de ganadores** (formato al lanzar, guardado en el navegador hasta A33): grupos de
  premios iguales por lotes de hasta 10, números que giran (ID de empleado relleno a 7 cifras o
  boleto), nombre y área; grupo con foto = premio principal uno a uno (foto, contador grande,
  ganador). Franja con títulos y logo blanco, nombre del premio en Freight Sans (`font-premio`,
  falta el `.woff2` del cliente) y color configurable, guirnalda navideña opcional. En proyector y
  pantalla completa ocupa todo el ancho. Sortea el lote y relee el sorteo una sola vez
  (`useDrawPrizes`).
- Sorteo creado solo (sin evento) = **sin puerta** (`requiresCheckIn=false`): todos presentes, sin
  escáner ni check-in en la interfaz. Interruptor "Solo participan quienes llegaron" en
  Configuración.
- Escala: la tabla de participantes dibuja de a 200 y busca sin trabarse; el mock importa 25.000
  sin cuadráticos.
- Administración menos amontonada: pestañas con raya, panel lateral solo en Premios, participantes
  con filtros con conteo y acciones en "Más", filas de dos líneas, consola del tablero con ajustes
  plegados, tablas neutras por omisión (`DataTable`).
- El administrador ve todos los módulos (Eventos y Convocatorias encendidos por omisión); nombres
  nuevos de las tarjetas: Rifas en vivo, Agenda de eventos, Invitaciones, Marca y ajustes.
- Demo: "Fiesta de Navidad Corporativa 2026" con 50 presentes y 39 premios (30 bonos, 5 tablets).

## Pendiente del frontend

- B18: reescribir las pruebas de Playwright (`frontend/e2e`), que siguen probando CRM y módulos
  quitados.
- B19: script de datos de prueba (padrón de 25.000, premios de 10.000) para el ensayo.
- Anular a un ganador ausente desde el tablero (hoy se hace en la pestaña Ganadores).
- Pantalla "Mi plan" y la del superadministrador para solicitudes de plan (B15).
- Campos visibles configurables del participante (B16) cuando el servidor tenga A29.
- Cambiar a `?limit=36` en la ruleta clásica cuando exista R14.
- Actualizar las imágenes base del Dockerfile del frontend (el editor marca vulnerabilidades).

## Decisiones que faltan de Robert

Precios de Básico, Pro y Avanzado; datos bancarios para la transferencia; correo remitente; datos
legales del titular; servidor y dominio; si el registro se enciende el miércoles (recomendado: no);
salón con o sin internet; quién de Banreservas recibe la capacitación.
