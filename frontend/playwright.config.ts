import { defineConfig, devices } from '@playwright/test'

const PORT = 5199

/**
 * Pruebas de humo de la interfaz. Corren contra el servidor de desarrollo en modo `mock`, así
 * que no necesitan backend ni Keycloak: `npm run test:e2e`.
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? 'github' : 'list',
  // El servidor de desarrollo compila cada pantalla la primera vez que se pide.
  timeout: 90_000,
  expect: { timeout: 15_000 },
  workers: 2,
  use: {
    baseURL: `http://localhost:${PORT}`,
    trace: 'retain-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      // Ancho de escritorio: la barra lateral solo existe desde `lg`.
      use: { ...devices['Desktop Chrome'], viewport: { width: 1440, height: 900 } },
    },
  ],
  webServer: {
    command: `npx vite --port ${PORT} --strictPort`,
    url: `http://localhost:${PORT}`,
    env: { VITE_API_MODE: 'mock', VITE_MOCK_LOGIN: 'false' },
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
})
