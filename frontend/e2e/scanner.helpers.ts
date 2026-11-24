import { expect, type Page } from '@playwright/test'

// El terminal de la puerta en modo `mock`, sobre ev-0001: su lista de la puerta es el padrón del
// sorteo (51 participantes con check-in) y ahí se emite la entrada de demostración la primera vez
// que se lee. La hora fija deja el evento dentro de la lista del selector.
export const EN_LA_VENTANA = new Date('2026-09-20T15:00:00Z')
export const EVENTO = 'Fiesta de Navidad Corporativa 2026'
export const ENTRADA_DEMO =
  'https://sorteo360.example.com/publico/entrada/demo-entrada-0000000000000000000000000000000'

/** Igual que en `smoke.spec.ts`: sin esperar al logo externo de la barra lateral. */
export const abrir = (page: Page, ruta: string) =>
  page.goto(ruta, { waitUntil: 'domcontentloaded' })

export async function leer(page: Page, texto: string) {
  await page.getByLabel('Código', { exact: true }).fill(texto)
  await page.getByRole('button', { name: 'Validar' }).click()
}

export const ultimaLectura = (page: Page) => page.getByRole('status').first()
export const pie = (page: Page) => page.locator('footer')

export async function elegirEvento(page: Page) {
  await page.getByRole('button', { name: new RegExp(EVENTO.replace(/\./g, '\\.')) }).click()
  await expect(page.locator('header')).toContainText(EVENTO)
}
