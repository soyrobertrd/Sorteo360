import { expect, test, type Page } from '@playwright/test'

/** Abre una pantalla sin esperar al evento `load`, como `smoke.spec.ts`. */
const abrir = (page: Page, ruta: string) => page.goto(ruta, { waitUntil: 'domcontentloaded' })

/**
 * Navega dentro de la aplicación, sin recargar: la identidad simulada vive en memoria y una recarga
 * volvería a la de por defecto.
 */
const navegar = (page: Page, ruta: string) =>
  page.evaluate((destino) => {
    window.history.pushState({}, '', destino)
    window.dispatchEvent(new PopStateEvent('popstate'))
  }, ruta)

const cambiarA = async (page: Page, persona: RegExp) => {
  await page.getByTitle('Cambiar usuario para probar permisos de módulos').click()
  await page.getByRole('button', { name: persona }).click()
}

// Los interruptores salen de `GET /v1/public/feature-switches` (en el mock, su réplica). La
// identidad por defecto del mock es Administración General, que tiene feature-switches:write.
test('quien administra apaga el Historial de Ganadores y desaparece de Rifas en vivo', async ({
  page,
}) => {
  await abrir(page, '/app')
  await page
    .getByRole('link', { name: 'Acceder', exact: true })
    .and(page.locator('[href="/admin"]'))
    .click()
  await expect(page).toHaveURL(/\/admin$/)
  await page.getByRole('link', { name: 'Abrir Interruptores de funciones' }).click()

  const historial = page.getByRole('switch', { name: 'Rifas en vivo › Historial de Ganadores' })
  await expect(historial).toHaveAttribute('aria-checked', 'true')
  await historial.click()
  await expect(historial).toHaveAttribute('aria-checked', 'false')
  await expect(page.getByText('Desactivada por María Fernández')).toBeVisible()

  await page.getByRole('link', { name: 'Ir a Rifas en vivo' }).click()
  const barra = page.locator('aside')
  await expect(barra.getByRole('link', { name: 'Sorteos', exact: true })).toBeVisible()
  await expect(barra.getByRole('link', { name: 'Historial de Ganadores' })).toHaveCount(0)

  await navegar(page, '/sorteo/historial')
  await expect(page.getByRole('heading', { name: 'Función desactivada' })).toBeVisible()

  // Quien no administra no sabe que está apagada: la misma 404 que un enlace roto.
  await navegar(page, '/app')
  await cambiarA(page, /Luis Ortega/)
  await navegar(page, '/sorteo/historial')
  await expect(page.getByRole('heading', { name: '404' })).toBeVisible()
  await expect(page.getByText('Función desactivada')).toHaveCount(0)
})

test('un cambio de interruptor sobrevive a la recarga', async ({ page }) => {
  await abrir(page, '/admin/feature-switches')
  const historial = page.getByRole('switch', { name: 'Rifas en vivo › Historial de Ganadores' })
  await expect(historial).toHaveAttribute('aria-checked', 'true')
  await historial.click()
  await expect(historial).toHaveAttribute('aria-checked', 'false')
  // La pantalla cambia antes de guardar (actualización optimista). Con la laptop cargada, recargar
  // enseguida perdía el cambio: se espera a que el mock lo haya guardado.
  await expect
    .poll(() =>
      page.evaluate(() => {
        const estado = JSON.parse(localStorage.getItem('sorteo360.mock.state.v4') ?? '{}') as {
          featureSwitches?: { enabled: boolean }[]
        }
        return (estado.featureSwitches ?? []).some((s) => !s.enabled)
      }),
    )
    .toBe(true)

  await page.reload({ waitUntil: 'domcontentloaded' })
  await expect(historial).toHaveAttribute('aria-checked', 'false')
})

test('quien no administra no ve la tarjeta de Marca y ajustes', async ({ page }) => {
  await abrir(page, '/app')
  await expect(page.getByRole('heading', { name: 'Marca y ajustes' })).toBeVisible()

  await cambiarA(page, /Luis Ortega/)
  await expect(page.getByRole('heading', { name: 'Rifas en vivo' })).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Marca y ajustes' })).toHaveCount(0)
})
