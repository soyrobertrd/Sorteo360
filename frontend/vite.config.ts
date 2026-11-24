import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'node:path'

// En modo http sin VITE_EVENTOS_API_URL, la app llama /v1 en su mismo origen y el
// dev server lo reenvía al servicio dueño de cada ruta — así el navegador no
// necesita CORS. Las rutas son planas (`/v1/...` sin prefijo por servicio), así que las de
// convocatorias y notificaciones van antes del comodín de eventos: gana la primera que calce.
const convocatorias = {
  target: process.env.CONVOCATORIAS_PROXY_TARGET ?? 'http://localhost:5185',
  changeOrigin: true,
}

const notifications = {
  target: process.env.NOTIFICATIONS_PROXY_TARGET ?? 'http://localhost:5190',
  changeOrigin: true,
}

const proxy = {
  // El portal de registro (anónimo) y todo lo interno de convocatorias. La lista gemela
  // vive en `nginx.conf`; nada las compara, así que una ruta enumerada en una sola cae
  // en events-service.
  // Los interruptores públicos son de events-service: van antes que el resto de /v1/public/.
  '/v1/public/feature-switches': {
    target: process.env.EVENTOS_PROXY_TARGET ?? 'http://localhost:5170',
    changeOrigin: true,
  },
  '/v1/public/': convocatorias,
  '/v1/convocatorias/': convocatorias,
  // El historial de envíos (ADR-0023): el listado y el detalle de un lote, nada más. Una
  // clave que empieza con ^ es una expresión regular contra la URL con su query; con un
  // prefijo simple, /v1/notifications/orphan-events también llegaría, y no debe. La lista
  // gemela, que además solo deja pasar GET, vive en `nginx.conf`.
  '^/v1/notifications(\\?|$)': notifications,
  '^/v1/notifications/batches/[^/?]+(\\?|$)': notifications,
  '/v1': {
    target: process.env.EVENTOS_PROXY_TARGET ?? 'http://localhost:5170',
    changeOrigin: true,
  },
}

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, 'src'),
    },
  },
  server: {
    port: 5173,
    proxy,
  },
  preview: {
    proxy,
  },
})
