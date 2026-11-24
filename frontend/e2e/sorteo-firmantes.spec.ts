import { test, expect, type Page } from '@playwright/test'
import { abrir, pestanaConfiguracion, sortearUnPremio } from './sorteo.helpers'

// Los firmantes que el certificado de premio propone se guardan en el sorteo, en cualquier estado
// mientras el evento siga abierto, y el certificado parte de ellos. Corre en modo `mock`.

const panel = (page: Page) => page.getByRole('region', { name: 'Firmantes del certificado' })

test('los firmantes guardados con el sorteo en curso son los que el certificado propone', async ({
  page,
}) => {
  await sortearUnPremio(page)
  await pestanaConfiguracion(page).click()

  const firmantes = panel(page)
  await firmantes.getByLabel('Nombre del primer firmante').fill('Ana Pérez')
  await firmantes.getByLabel('Cargo del primer firmante').fill('Gerente General')
  await firmantes.getByLabel('Nombre del segundo firmante').fill('Luis Gómez')
  await firmantes.getByLabel('Cargo del segundo firmante').fill('Director de Recursos Humanos')
  await firmantes.getByRole('button', { name: 'Guardar firmantes' }).click()
  await expect(firmantes.getByText('Firmantes guardados.')).toBeVisible()

  await page.getByRole('tab', { name: /^Ganadores/ }).click()
  await page
    .getByRole('button', { name: /^Acciones de / })
    .first()
    .click()
  await page.getByRole('button', { name: 'Certificado' }).click()
  const certificado = page.getByRole('dialog')
  await expect(certificado.getByLabel('Nombre del primer firmante')).toHaveValue('Ana Pérez')
  await expect(certificado.getByLabel('Cargo del primer firmante')).toHaveValue('Gerente General')
  await expect(certificado.getByLabel('Nombre del segundo firmante')).toHaveValue('Luis Gómez')
  await expect(certificado.getByLabel('Cargo del segundo firmante')).toHaveValue(
    'Director de Recursos Humanos',
  )
})

test('un único firmante escrito en la segunda línea se guarda en la primera', async ({ page }) => {
  await abrir(page, '/sorteo/sorteos/ev-0001?tab=reglas')

  const firmantes = panel(page)
  await firmantes.getByLabel('Nombre del segundo firmante').fill('  Luis Gómez ')
  await firmantes.getByLabel('Cargo del segundo firmante').fill('Director de Recursos Humanos')
  await firmantes.getByRole('button', { name: 'Guardar firmantes' }).click()

  await expect(firmantes.getByText('Firmantes guardados.')).toBeVisible()
  await expect(firmantes.getByLabel('Nombre del primer firmante')).toHaveValue('Luis Gómez')
  await expect(firmantes.getByLabel('Nombre del segundo firmante')).toHaveValue('')
})

test('un firmante sin cargo no se guarda, y el error va en su campo', async ({ page }) => {
  await abrir(page, '/sorteo/sorteos/ev-0001?tab=reglas')

  const firmantes = panel(page)
  await firmantes.getByLabel('Nombre del primer firmante').fill('Ana Pérez')
  await firmantes.getByRole('button', { name: 'Guardar firmantes' }).click()

  const cargo = firmantes.locator('label', { hasText: 'Cargo del primer firmante' })
  await expect(cargo.getByText('El cargo del firmante es obligatorio.')).toBeVisible()
  await expect(firmantes.getByText('Firmantes guardados.')).toHaveCount(0)
})

test('con el evento cerrado, los firmantes no se pueden cambiar', async ({ page }) => {
  // raffle-0002 está completado, en un evento cerrado
  await abrir(page, '/sorteo/sorteos/ev-0003?tab=reglas')

  const firmantes = panel(page)
  await expect(firmantes.getByLabel('Nombre del primer firmante')).toBeDisabled()
  await expect(firmantes.getByRole('button', { name: 'Guardar firmantes' })).toBeDisabled()
})
