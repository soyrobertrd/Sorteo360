import { readFile } from 'node:fs/promises'
import { expect, test, type Page } from '@playwright/test'
import {
  abrir,
  accion,
  lanzar,
  pestanaConfiguracion,
  salir as salirDe,
  sortear as girarYSeguir,
} from './sorteo.helpers'

// Un premio por persona rige siempre (RR. HH., 2026-11-23); la regla de departamento cambia con el
// sorteo en curso y aplica desde el próximo premio. Corre en modo `mock`.

const porPersona = (page: Page) => page.getByText('Un solo premio por persona (siempre)')
const porEmpresa = (page: Page) =>
  page.getByRole('checkbox', { name: /Un solo premio por departamento/ })
const avisoCambio = /^Un cambio aplica desde el próximo premio sorteado/
const elegibles = (page: Page) => page.getByText(/^\d+ participantes? elegibles?$/)

/** Arranca el borrador `raffle-0001` de `ev-0001`: deja la pantalla de sorteo abierta. */
async function iniciarSorteo(page: Page) {
  await abrir(page, '/sorteo/sorteos/ev-0001')
  await lanzar(page)
}

const salir = (page: Page) => salirDe(page, 'ev-0001')
const continuarSorteo = (page: Page) => lanzar(page, 'Continuar Sorteo')

/** Sortea el premio en pantalla y pasa al siguiente; devuelve cuántos elegibles tenía. */
async function sortear(page: Page) {
  const antes = Number((await elegibles(page).textContent())?.split(' ')[0])
  await girarYSeguir(page)
  return antes
}

test('con el sorteo en curso, la regla de departamento se cambia y la de persona es fija', async ({
  page,
}) => {
  await iniciarSorteo(page)
  await salir(page)
  await pestanaConfiguracion(page).click()

  // Un premio por persona no es un interruptor: se lee, no se marca
  await expect(porPersona(page)).toBeVisible()
  await expect(page.getByRole('checkbox', { name: /Un solo premio por persona/ })).toHaveCount(0)
  await expect(page.getByText(avisoCambio)).toBeVisible()
  // Los interruptores son controlados: cambian cuando vuelve lo guardado, no al hacer clic
  await expect(porEmpresa(page)).toBeEnabled()
  await porEmpresa(page).click()
  await expect(porEmpresa(page)).toBeChecked()

  // Lo guardado es lo que vuelve del servicio: sigue así al volver a la pestaña
  await page.getByRole('tab', { name: /^Premios/ }).click()
  await pestanaConfiguracion(page).click()
  await expect(porEmpresa(page)).toBeChecked()
})

test('el ganador anterior no vuelve al bombo, ni al continuar el sorteo', async ({ page }) => {
  await iniciarSorteo(page)
  const participantes = await sortear(page)
  // El ganador queda fuera del premio siguiente
  await expect(elegibles(page)).toHaveText(`${participantes - 1} participantes elegibles`)

  await salir(page)
  await continuarSorteo(page)
  await expect(elegibles(page)).toHaveText(`${participantes - 1} participantes elegibles`)
})

test('el CSV lleva las reglas con que se sorteó cada ganador', async ({ page }) => {
  await iniciarSorteo(page)
  await sortear(page)
  await salir(page)
  await pestanaConfiguracion(page).click()
  await porEmpresa(page).click()
  await expect(porEmpresa(page)).toBeChecked()
  await continuarSorteo(page)
  await sortear(page)
  await salir(page)

  // La tabla de Ganadores ya no las muestra (Robert, 2026-10-22): quedan en el CSV y en el acta
  const descarga = page.waitForEvent('download')
  await accion(page, 'Exportar resultados (CSV)')
  // La línea 0 es la cabecera; las filas siguen el orden de los premios, que es el del sorteo
  const [, primero, segundo] = (await readFile(await (await descarga).path(), 'utf8')).split('\n')
  expect(primero).toContain('"Sí","No"')
  expect(segundo).toContain('"Sí","Sí"')
})

test('un sorteo completado no deja cambiar las reglas y su CSV lleva las de cada ganador', async ({
  page,
}) => {
  // raffle-0002 está completado, con dos ganadores sorteados con las dos reglas encendidas
  await abrir(page, '/sorteo/sorteos/ev-0003?tab=reglas')
  await expect(porPersona(page)).toBeVisible()
  await expect(porEmpresa(page)).toBeDisabled()
  await expect(page.getByText(avisoCambio)).toHaveCount(0)

  const descarga = page.waitForEvent('download')
  await accion(page, 'Exportar resultados (CSV)')
  const [cabecera, primera] = (await readFile(await (await descarga).path(), 'utf8')).split('\n')
  expect(cabecera).toContain('Un premio por persona,Un premio por empresa')
  expect(primera).toContain('"Sí","Sí"')
})
