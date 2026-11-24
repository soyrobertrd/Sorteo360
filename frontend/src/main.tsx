import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { iniciarApi } from './api'
import { App } from './app/App'
import './index.css'
// Aplica los colores de marca guardados antes del primer render.
import './lib/branding'

// Registro del Service Worker para soporte PWA
if ('serviceWorker' in navigator) {
  window.addEventListener('load', () => {
    navigator.serviceWorker.register('/sw.js').catch((err) => {
      console.info('SW registration skipped or failed:', err)
    })
  })
}

void iniciarApi().then(() =>
  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <App />
    </StrictMode>,
  ),
)
