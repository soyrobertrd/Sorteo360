# Design System: Sorteo360

Fuente de verdad visual del frontend. Sirve para pantallas nuevas (a mano o generadas con
Google Stitch) y para revisar que lo existente no se desvíe. Los valores salen de
`src/index.css`; si cambian allí, se cambian aquí.

## 1. Atmósfera

Una herramienta de trabajo para la noche de la fiesta: sobria, ordenada y rápida de leer bajo
presión. Densidad media de aplicación diaria (5 de 10), simetría predecible en la administración
(3 de 10) y movimiento contenido (3 de 10): solo lo que comunica algo, como el número que gira
hasta el ganador. La pantalla del salón (tablero y proyector) es la única parte expresiva:
grande, con la marca del cliente y decoración de temporada.

El producto lleva la marca de cada cliente: los colores no son de Sorteo360, son de la cuenta.

## 2. Colores y funciones

- **Azul de marca** (`--brand-primary`, #0d3b7d por omisión): botones primarios, pestaña activa,
  barras de avance, franja del tablero. Personalizable por cuenta en Configuración → Marca.
- **Azul profundo** (`--color-brand-deep`, la marca mezclada con 35 % de negro): encabezados de
  ficha, franja del proyector, contadores del tablero.
- **Acento** (`--brand-accent`, #1ea2dc por omisión): textos de énfasis en modo oscuro,
  indicadores "en vivo". Uno solo por pantalla.
- **Lienzo** (`--background`, #f9f9ff) y **Superficie** (`--card`): fondo de página y de tarjetas.
- **Tinta** (`--foreground`, #181c23): texto principal. Nunca negro puro.
- **Tinta suave** (`--muted-foreground`, #454650): descripciones, metadatos, ayudas.
- **Estados**: esmeralda para "califica / entregado", ámbar para "pendiente", rosa para
  "excluido / error". Siempre con texto, nunca solo color.
- **Color del premio en el tablero** (#01AEF0 por omisión): lo elige quien arma el tablero.

Prohibido: hex sueltos en clases o estilos (`CLAUDE.md`), degradados morados o neón, brillos
exteriores en botones.

## 3. Tipografía

- **Interfaz**: Inter (ya cargada en todo el producto). En una herramienta de trabajo prima la
  legibilidad y la cifra tabular, no la personalidad.
- **Cifras**: `tabular-nums` en contadores, montos y boletos; `font-mono` solo para boletos y
  números de empleado.
- **Nombre del premio en el tablero**: Freight Sans Bold (`font-premio`), la del cliente
  Banreservas; cae a sans-serif si el equipo no la tiene.
- **Escala**: títulos de página `text-2xl/3xl font-extrabold`; secciones `text-base font-bold`;
  cuerpo `text-sm`; tablas y metadatos `text-xs`. Jerarquía por peso y color antes que por tamaño.
- Etiquetas en mayúsculas con tracking solo en cabeceras de tabla y rótulos de métricas.

## 4. Componentes

- **Botones**: primario relleno con la marca y texto `--brand-primary-contrast`; secundario con
  borde; acciones de fila como icono con `aria-label`. Una acción principal por zona; el resto en
  un menú "Más".
- **Pestañas de ficha**: texto con raya inferior en la activa (`Tabs variant="underline"`).
  Filtros de tabla: segmentado con el conteo al lado (`Califican 51`).
- **Tablas**: variante neutra por omisión (cabecera gris clara, filas blancas, resaltado al
  pasar). Una fila dice lo esencial en dos líneas: nombre y, debajo en gris, los datos
  secundarios separados por " · ". Premios iguales se agrupan en una línea con su cantidad.
- **Tarjetas**: solo cuando la elevación ordena (métricas de la ficha, panel lateral). Radio
  `rounded-2xl` para contenedores, `rounded-lg` para campos y botones.
- **Formularios**: etiqueta arriba, ayuda debajo, error debajo en rosa y en el campo. Nunca el
  placeholder como etiqueta.
- **Modales**: una tarea por modal, botón principal a la derecha, "Cancelar" a su lado; acciones
  destructivas a la izquierda y en rosa.
- **Estados**: vacío con una frase que dice cómo llenarlo; carga con spinner corto o "Cargando…";
  errores en `ErrorNote` o en el campo.

## 5. Distribución

- Contenido de página dentro de `max-w-7xl`; el panel lateral de la ficha (300 px) aparece solo
  en la pestaña donde ayuda (Premios). Las tablas usan todo el ancho.
- Ajustes que se fijan una vez (pantalla del tablero) van plegados en un `<details>`; lo que se
  usa en cada giro, arriba y a mano.
- Debajo de 768 px todo pasa a una columna; las tablas largas se desplazan dentro de su caja.
- El tablero y el proyector usan medidas relativas al ancho (`cqw`): se ven igual en la vista
  previa y en cualquier pantalla, de borde a borde y sin bandas laterales.

## 6. Movimiento

- Transiciones de color y opacidad en `hover`/`focus`; nada se mueve solo en la administración.
- Tablero: los números giran y se detienen fila por fila; el ganador del premio principal
  espera 3,5 s. Todo se detiene con `prefers-reduced-motion`.
- Animación que corre sola más de 5 s (nombres del landing) lleva botón de pausa.

## 7. Prohibido

- Colores fuera de los tokens; negro puro; neón; degradados de texto.
- Siete botones sueltos en una barra: agrupar en "Más".
- Tarjetas de métricas que repiten lo que ya dice un filtro o la ficha.
- Seis tarjetas iguales en fila en el landing (usar rejilla con ritmo).
- Rayas largas (—) en textos visibles; números de paso ("01", "Paso 1") como adorno.
- Nombres de relleno ("John Doe", "Acme") y cifras inventadas en pantallas de muestra.
- Emojis en la interfaz.
