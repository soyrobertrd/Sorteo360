// Service Worker de Sorteo360: la aplicación abre aunque se caiga internet en el salón.
//
// Guarda el "cascarón" (index.html) y cada archivo estático que el navegador ya descargó (JS, CSS,
// fuentes, imágenes de /assets). Nunca guarda la API (/v1): los datos siempre se piden en vivo, y
// sin servidor alcanzable la pantalla dice que no hay conexión en vez de mostrar datos viejos.
const CACHE_NAME = 'sorteo360-app-v2'
const SHELL = ['/', '/index.html', '/manifest.webmanifest', '/logo-brand.svg', '/logo-brand-dark.svg']

self.addEventListener('install', (event) => {
  event.waitUntil(caches.open(CACHE_NAME).then((cache) => cache.addAll(SHELL)))
  self.skipWaiting()
})

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) => Promise.all(keys.filter((k) => k !== CACHE_NAME).map((k) => caches.delete(k)))),
  )
  self.clients.claim()
})

self.addEventListener('fetch', (event) => {
  const { request } = event
  if (request.method !== 'GET') return
  const url = new URL(request.url)
  if (url.origin !== self.location.origin || url.pathname.startsWith('/v1/')) return

  // Navegación: la red primero (para tener la última versión); sin red, el cascarón de la SPA.
  if (request.mode === 'navigate') {
    event.respondWith(
      fetch(request).catch(async () => (await caches.match('/index.html')) ?? caches.match('/')),
    )
    return
  }

  // Solo los estáticos del build (/assets, con hash en el nombre) y el cascarón: nunca el código
  // del servidor de desarrollo (/src, /@vite), que cambia sin cambiar de nombre.
  const esEstatico = url.pathname.startsWith('/assets/') || SHELL.includes(url.pathname)
  if (!esEstatico) return

  // Lo guardado primero; lo nuevo se guarda al llegar, para la próxima vez sin red.
  event.respondWith(
    caches.match(request).then(
      (cached) =>
        cached ??
        fetch(request).then((response) => {
          if (response.ok && response.type === 'basic') {
            const copia = response.clone()
            caches.open(CACHE_NAME).then((cache) => cache.put(request, copia))
          }
          return response
        }),
    ),
  )
})
