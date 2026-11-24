import { expect, test, type Page } from '@playwright/test'
import { abrir, elegirEvento, EN_LA_VENTANA, ENTRADA_DEMO, ultimaLectura } from './scanner.helpers'

// La cámara: Chromium con su cámara de prueba, y un `BarcodeDetector` que "ve" lo que la prueba
// pone en `window.__enCuadro`. Así se prueba el camino de la cámara sin un QR real delante.
test.use({
  launchOptions: {
    args: ['--use-fake-ui-for-media-stream', '--use-fake-device-for-media-stream'],
  },
  permissions: ['camera'],
})

test.beforeEach(async ({ page }) => {
  // El reloj empieza en la ventana del evento y sigue corriendo: la ventana de repetición se mide
  // con él, y con la hora congelada un código sostenido nunca se repetiría
  await page.clock.install({ time: EN_LA_VENTANA })
  await page.clock.resume()
  await page.addInitScript(() => {
    const w = window as unknown as { __enCuadro: string[]; BarcodeDetector: unknown }
    w.__enCuadro = []
    w.BarcodeDetector = class {
      static async getSupportedFormats() {
        return ['qr_code']
      }
      async detect() {
        return w.__enCuadro.map((rawValue) => ({ rawValue }))
      }
    }
  })
})

const enCuadro = (page: Page, codes: string[]) =>
  page.evaluate((c) => {
    ;(window as unknown as { __enCuadro: string[] }).__enCuadro = c
  }, codes)

const visor = (page: Page) =>
  page.locator('[aria-hidden="true"]').filter({ has: page.locator('.rounded-tl-lg') })

const historial = (page: Page) =>
  page.locator('section').filter({ hasText: 'Lecturas anteriores' }).locator('li')

test('un código frente a la cámara se ilumina en el visor y hace check-in una sola vez', async ({
  page,
}) => {
  await abrir(page, '/scanner')
  await elegirEvento(page)
  await expect(visor(page)).toBeVisible()

  await enCuadro(page, [ENTRADA_DEMO])
  await expect(visor(page).locator('.border-emerald-400').first()).toBeVisible()
  await expect(ultimaLectura(page)).toContainText('Check-in registrado')
  // Pasado el destello el visor vuelve a su color
  await expect(visor(page).locator('.border-emerald-400')).toHaveCount(0)

  // Sostenido delante más de la ventana de repetición, no se vuelve a enviar
  await page.waitForTimeout(3_000)
  await expect(ultimaLectura(page)).toContainText('Check-in registrado')
  await expect(historial(page)).toHaveCount(0)

  // Apartado más de dos segundos y vuelto a mostrar, se lee otra vez
  await enCuadro(page, [])
  await page.waitForTimeout(2_500)
  await enCuadro(page, [ENTRADA_DEMO])
  await expect(ultimaLectura(page)).toContainText('Ya había ingresado')
})

test('dos códigos en el cuadro se leen una vez cada uno', async ({ page }) => {
  await abrir(page, '/scanner')
  await elegirEvento(page)

  await enCuadro(page, [ENTRADA_DEMO, 'RD-R4TW-8HNB'])
  await expect(historial(page)).toHaveCount(1)
  await page.waitForTimeout(3_000)
  await expect(historial(page)).toHaveCount(1)
})

test('con la lista de la puerta abierta la cámara no lee', async ({ page }) => {
  await abrir(page, '/scanner')
  await elegirEvento(page)
  await page.getByRole('button', { name: /Lista de la puerta/ }).click()

  await enCuadro(page, [ENTRADA_DEMO])
  await page.waitForTimeout(1_500)
  // Se aparta antes de cerrar: con la lista cerrada la cámara vuelve a leer, y eso es correcto
  await enCuadro(page, [])
  await page.keyboard.press('Escape')
  await expect(ultimaLectura(page)).toHaveCount(0)
})
