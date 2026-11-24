import { expect, type Page } from '@playwright/test'

// Lo común a las pruebas del sorteo en modo `mock`. El mock guarda su estado en `localStorage`, y
// cada prueba abre un navegador limpio: empieza siempre con la demo sembrada.

/**
 * Igual que en `smoke.spec.ts`: sin esperar al evento `load`. Estas pruebas recorren la consola
 * clásica (ruleta), que nace apagada: en un navegador limpio se encienden sus dos interruptores
 * antes de que cargue el mock, como lo haría Administración.
 */
export async function abrir(page: Page, ruta: string) {
  await page.addInitScript(() => {
    const clave = 'sorteo360.mock.state.v3'
    if (localStorage.getItem(clave)) return
    const fila = (key: string) => ({
      key,
      enabled: true,
      changedById: 'u-admin',
      changedByName: 'María Fernández',
      changedAt: '2026-11-23T00:00:00.000Z',
    })
    localStorage.setItem(
      clave,
      JSON.stringify({
        featureSwitches: [fila('sorteo.style-roulette'), fila('sorteo.style-names')],
      }),
    )
  })
  return page.goto(ruta, { waitUntil: 'domcontentloaded' })
}

export const pestanaConfiguracion = (page: Page) => page.getByRole('tab', { name: 'Configuración' })

/** Abre el menú "Acciones" del encabezado y elige una. */
export async function accion(page: Page, nombre: string) {
  await page.getByRole('button', { name: 'Acciones', exact: true }).click()
  // El menú se dibuja al final de la página: si la pantalla repite la acción (la tarjeta de accesos
  // de Premios también exporta el CSV), la del menú es la última.
  await page.getByRole('button', { name: nombre, exact: true }).last().click()
}

/**
 * Desde el detalle, lanza el sorteo: `boton` es "Iniciar Sorteo" o "Continuar Sorteo". El modal
 * tiene tres pasos (formato, pantalla y presentación) y su último botón repite el texto.
 */
export async function lanzar(page: Page, boton = 'Iniciar Sorteo') {
  await accion(page, boton)
  await page.getByRole('button', { name: 'Siguiente' }).click()
  await page.getByRole('button', { name: 'Siguiente' }).click()
  await page.getByRole('button', { name: boton }).last().click()
  await expect(page).toHaveURL(/\/ejecutar$/)
}

/** Gira el premio en pantalla, espera al ganador y, si el sorteo la pide, confirma su presencia. */
export async function girar(page: Page) {
  await page.getByRole('button', { name: /GIRAR RULETA/ }).click()
  await expect(page.getByRole('button', { name: 'Anular ganador' })).toBeVisible()
  const confirmar = page.getByRole('button', { name: 'Confirmar presencia' })
  if (await confirmar.isVisible()) await confirmar.click()
}

/** Gira y pasa al premio siguiente. */
export async function sortear(page: Page) {
  await girar(page)
  await page.getByRole('button', { name: 'Siguiente premio' }).click()
}

/** De la pantalla de sorteo al detalle, sin recargar. */
export async function salir(page: Page, eventId: string) {
  await page.getByRole('button', { name: 'Salir', exact: true }).click()
  await expect(page).toHaveURL(new RegExp(`/sorteo/sorteos/${eventId}$`))
}

/** Arranca el borrador de `ev-0001`, sortea un premio y vuelve al detalle. */
export async function sortearUnPremio(page: Page) {
  await abrir(page, '/sorteo/sorteos/ev-0001')
  await lanzar(page)
  await sortear(page)
  await salir(page, 'ev-0001')
}
