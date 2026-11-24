# Assets y textos del tema de login

## `resources/`

Estos archivos los sirve Keycloak desde su propia URL (`/resources/<v>/login/sorteo360/...`).
No es una preferencia: **el entorno de producción está en sitio y no tiene salida a
internet**, así que un asset traído de un tercero no llega. En el caso de una fuente ni
siquiera falla, solo cambia la tipografía en silencio, que es la peor forma de romperse.

Antes de agregar cualquier asset acá, la prueba es la de siempre: cargar el login con todo
lo que no sea Keycloak bloqueado y ver que no cambie nada.

## `resources/img/sorteo360-logo.svg`

Vector limpio del logotipo de Sorteo360, sin dependencias externas ni referencias a terceros.
Para personalizar el branding del evento en el portal o certificados, usa `PUT /v1/branding`
o `PUT /v1/events/{eventId}/raffle/branding`.

## `resources/fonts/*.woff2`

Inter para títulos y Roboto para texto, las mismas dos familias que usa el frontend. Ambas
son **fuentes variables de un solo archivo** con el eje de peso completo (100 a 900), así que
un archivo por familia cubre todos los grosores y no hay que vendorizar uno por peso.

Subconjunto latino solamente: el login está en español y le sobra. El `@font-face` de
`resources/css/sorteo360.css` conserva el `unicode-range` de Google a propósito, para que un carácter
fuera de ese rango caiga a la familia de reserva en vez de dibujarse como un cuadro vacío.

Para refrescarlas, pedir el CSS a Google con un User-Agent de navegador moderno (si no,
devuelve formatos viejos), quedarse con los bloques `/* latin */` y bajar esos dos `.woff2`:

```bash
curl -H 'User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 \
(KHTML, like Gecko) Chrome/120.0 Safari/537.36' \
  'https://fonts.googleapis.com/css2?family=Inter:wght@100..900&family=Roboto:wght@100..900&display=swap'
```

Las dos están bajo SIL Open Font License 1.1. La licencia obliga a distribuir el aviso de
copyright junto a la fuente: eso son `resources/fonts/inter-OFL.txt` y `roboto-OFL.txt`, copiados tal cual de
sus repositorios de origen. Si se reemplaza una fuente, se reemplaza también su licencia.

## `messages/messages_es.properties`

El español de la pantalla de login. Se **superpone** al de Keycloak clave por clave, no lo
reemplaza: lo que este archivo no declara sale con el texto de fábrica.

Existe por dos razones, y la segunda es la que importa:

1. Keycloak sin internacionalización sirve `en`. El realm la habilita con `es` como único
   idioma (`infra/keycloak/sorteo360-realm.json`).
2. El español de fábrica está a medias y **mezcla registros**. En la misma pantalla trae
   "¿Has olvidado tu contraseña?" al lado de "Confirme la contraseña", y deja claves sin
   traducir que caen al inglés (`traceIdSupportMessage`, por ejemplo). Acá se fija el
   tuteo, que es como habla el resto del producto.

Por eso el archivo no declara solo las frases mal traducidas, sino las de todas las
pantallas que este realm puede mostrar: el formulario de login, las páginas de error,
expiración y cierre de sesión, la acción requerida de cambiar contraseña, y los mensajes
que el flujo de autenticación emite en caliente. Fijar una frase que hoy está bien no es
de más: es lo que evita que una actualización de Keycloak la devuelva al usted sin que
nada falle.

Quedan afuera a propósito las claves de las pantallas que el realm no enciende (OTP,
WebAuthn, federación, organizaciones, flujo de dispositivo) y `loginTitleHtml`, que es
`{0}` y no tiene texto. Encender cualquiera de esas funciones trae pantallas nuevas: sus
claves entran acá en el mismo PR, o media pantalla sale en usted.

## `resources/css/sorteo360.css` y las clases de `theme.properties`

La hoja lleva una **segunda copia de la paleta de la aplicación**, escrita a mano. No hay paso
de build que la mantenga al día: un token que cambie en `frontend/src/index.css` o en un
componente de `frontend/src/components/ui/` es una edición acá, en el mismo PR. Cada
declaración del bloque `:root` dice al lado de qué token o de qué clase de Tailwind sale,
justamente para poder ir a buscarla.

Los valores van en hexadecimal aunque la aplicación los escriba en oklch. Un navegador que no
entienda oklch descarta la declaración entera y se queda con el color anterior de la cascada,
que acá sería el de PatternFly: la pantalla no falla, solo sale con los colores de Red Hat.

La otra mitad del trabajo está en `theme.properties`, en el bloque de claves `kc*`. Esas claves
son el nombre con el que las plantillas del tema padre escriben la clase de cada elemento, y
agregarles una clase nuestra es lo que deja restilar ese elemento con un solo selector de clase.
Del otro lado, en la hoja, lo que se redefine son casi siempre **variables de PatternFly**
(`--pf-v5-c-*`) y no propiedades sueltas: es la vía que PatternFly ofrece para vestirlo, y
evita encadenar selectores suyos hasta ganarle por especificidad.

Dos trampas de esas claves, las dos silenciosas:

- **El valor no se acumula con el del padre.** Al declarar `kcInputClass` acá hay que repetir
  las clases de PatternFly que traía, o el elemento se queda sin sus estilos base. La copia de
  referencia está dentro de la imagen, en `theme/keycloak.v2/login/theme.properties`.
- **Una clase que el padre renombre deja de existir** y el elemento pierde el estilo base sin
  que nada falle. Ver la sección de abajo.

## Lo que el frontend hace distinto

`frontend/` tiene hoy el mismo defecto que este tema ya no tiene: trae el logo y las fuentes
de internet en caliente. Está en su propia tarjeta del tablero y se arregla allá, no acá.

Cuando se arregle, los assets van a quedar duplicados en los dos lados a propósito.
Compartirlos obligaría a meter un paso de build en la imagen del frontend y a acoplar dos
cosas que se despliegan por separado; un logo y dos fuentes pesan mucho menos que eso.

## Qué revisar cuando Keycloak suba de versión

Las variables `--pf-v5-global--FontFamily--*` que redefine `resources/css/sorteo360.css` llevan la
versión de PatternFly en el nombre. Si Keycloak salta a PatternFly v6, esas líneas quedan
muertas y la tipografía vuelve a Red Hat **sin que nada falle ni se registre en ningún log**.
Por eso la hoja además fija `font-family` con selectores de elemento, que no dependen del
prefijo; pero conviene comprobarlo después de cada salto de versión:

```js
// En la consola del navegador, sobre la pantalla de login.
getComputedStyle(document.querySelector('#kc-page-title')).fontFamily  // -> Inter
getComputedStyle(document.body).fontFamily                             // -> Roboto
[...document.fonts].filter(f => f.status === 'loaded').map(f => f.family)
```

El mismo prefijo aparece en todas las variables `--pf-v5-c-*` con las que la hoja viste la
tarjeta, los campos, el botón y el aviso. Ahí no hay red de seguridad: si el prefijo cambia,
esa parte de la pantalla vuelve al estilo de PatternFly, otra vez sin que nada falle. Es lo
mismo que revisar en el mismo PR, y a ojo se ve en una carga de la pantalla.

Junto con eso, las clases del bloque `kc*` de `theme.properties`: cada valor repite las clases
del tema padre, y una que el padre haya renombrado deja al elemento sin sus estilos base. Las
dos listas se comparan directo, la nuestra contra la de la versión nueva:

```bash
KC=$(grep -o 'quay.io/keycloak/keycloak:[0-9.]*' ../../../../docker-compose.yml | head -1)
docker run --rm --entrypoint sh "$KC" \
  -c 'cat /opt/keycloak/lib/lib/main/org.keycloak.keycloak-themes-[0-9]*.jar' > /tmp/kc.jar
unzip -o -q /tmp/kc.jar -d /tmp/kc 'theme/keycloak.v2/login/theme.properties'

# Por cada clave que declaramos, la del padre al lado. Las clases del padre tienen que
# aparecer todas en la nuestra y lo unico que sobra debe ser un sorteo360-*, con una sola
# excepcion a proposito: kcLoginMainTitle le quita el pf-m-3xl, que es el tamano de
# titulo de PatternFly y lo fija la hoja.
grep -oE '^kc[A-Za-z]+=' theme.properties | sed 's/=$//' | while read -r k; do
  printf '%s\n  padre:    %s\n  nuestro:  %s\n' "$k" \
    "$(grep -E "^$k=" /tmp/kc/theme/keycloak.v2/login/theme.properties | cut -d= -f2-)" \
    "$(grep -E "^$k=" theme.properties | cut -d= -f2-)"
done
```

Lo otro que revisar en ese mismo PR es `messages/messages_es.properties`, por el mismo
motivo y con el mismo silencio: **una clave que Keycloak renombró deja de traducir sin que
nada falle**, y esa frase vuelve al español de fábrica, en usted. Nada avisa.

Se compara contra el bundle **inglés** de la versión nueva, que es el idioma de origen y el
único completo (el español de fábrica ya viene con claves sin traducir). Desde la carpeta
del tema, sin necesidad de que el stack esté levantado:

```bash
KC=$(grep -o 'quay.io/keycloak/keycloak:[0-9.]*' ../../../../docker-compose.yml | head -1)

# El [0-9] importa: sin el, el glob agarra tambien el jar -vendor- y concatena dos zips.
docker run --rm --entrypoint sh "$KC" \
  -c 'cat /opt/keycloak/lib/lib/main/org.keycloak.keycloak-themes-[0-9]*.jar' > /tmp/kc.jar
unzip -o -q /tmp/kc.jar -d /tmp/kc \
  'theme/base/login/messages/messages_en.properties' \
  'theme/keycloak.v2/login/messages/messages_en.properties'

cat /tmp/kc/theme/*/login/messages/messages_en.properties \
  | grep -oE '^[A-Za-z][A-Za-z0-9._-]*=' | sed 's/=$//' | sort -u > /tmp/kc/keys.txt

# Cordura primero: si dice 0, la extraccion fallo y la comparacion de abajo miente.
wc -l < /tmp/kc/keys.txt

comm -23 \
  <(grep -oE '^[A-Za-z][A-Za-z0-9._-]*=' messages/messages_es.properties | sed 's/=$//' | sort -u) \
  /tmp/kc/keys.txt
```

En 26.6 el conteo da 497 y el `comm` sale vacío. Cada línea que aparezca ahí es una clave
nuestra a renombrar o a borrar. Y ojo con el orden: si el conteo sale mal, el `comm` lista
**todas** nuestras claves, que parece una catástrofe y es solo la extracción rota.
