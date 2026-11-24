import { expect, test, type Page } from '@playwright/test'
import { abrir, accion, pestanaConfiguracion, sortearUnPremio } from './sorteo.helpers'

// Completar el sorteo genera el acta y avisa a los ganadores; reemitirla corrige las autoridades
// (spec raffles/03 §4, §5). Corre en modo `mock`.

const versiones = (page: Page) => page.getByText(/^Versión \d+$/)

/** Escribe en un campo que se guarda al salir de él. */
async function escribir(page: Page, placeholder: string | RegExp, valor: string, n = 0) {
  const campo = page.getByPlaceholder(placeholder).nth(n)
  await campo.fill(valor)
  await campo.blur()
}

async function abrirCompletar(page: Page) {
  await accion(page, 'Completar sorteo')
  return page.getByRole('dialog')
}

test('completar exige las instrucciones de retiro, y al completarse lista el acta y el correo', async ({
  page,
}) => {
  await sortearUnPremio(page)
  await pestanaConfiguracion(page).click()
  await escribir(page, /Notario Público/, 'Lic. Manuel Rodríguez')
  await escribir(page, /Dirección de Promoción/, 'Licda. Carolina Ortiz')
  await escribir(page, /Auditoría Técnica/, 'Ing. David Ramos')
  for (const [n, documento] of ['001-1', '001-2', '001-3'].entries()) {
    await escribir(page, 'Documento de identidad', documento, n)
  }

  // Sin instrucciones de retiro no se puede confirmar, y el diálogo dice por qué
  let dialogo = await abrirCompletar(page)
  await expect(dialogo.getByText(/debe indicar dónde y hasta cuándo/)).toBeVisible()
  await expect(dialogo.getByRole('button', { name: 'Completar sorteo' })).toBeDisabled()
  await dialogo.getByRole('button', { name: 'Volver' }).click()

  await escribir(
    page,
    /Mesa de entrega de premios/,
    'Stand de la empresa, pabellón 4, hasta el viernes 18:00',
  )
  dialogo = await abrirCompletar(page)
  await expect(
    dialogo.getByText('Stand de la empresa, pabellón 4, hasta el viernes 18:00'),
  ).toBeVisible()
  await expect(dialogo.getByText(/correo a 1 ganador/)).toBeVisible()
  await dialogo.getByRole('button', { name: 'Completar sorteo' }).click()
  await expect(dialogo).toHaveCount(0)

  await expect(page.getByText('Completado').first()).toBeVisible()
  await expect(versiones(page)).toHaveText(['Versión 1'])
  await page.getByRole('tab', { name: /^Ganadores/ }).click()
  await expect(page.getByRole('row').nth(1).getByText('Correo enviado')).toBeVisible()
})

test('mientras está en curso, el correo del ganador sale al completar', async ({ page }) => {
  await sortearUnPremio(page)
  await page.getByRole('tab', { name: /^Ganadores/ }).click()
  await expect(page.getByRole('row').nth(1).getByText('Se envía al completar')).toBeVisible()
})

test('reemitir el acta corrige las autoridades y agrega una versión', async ({ page }) => {
  // raffle-0002 está completado, con su acta versión 1
  await abrir(page, '/sorteo/sorteos/ev-0003?tab=reglas')
  await expect(versiones(page)).toHaveText(['Versión 1'])

  await page.getByRole('button', { name: 'Reemitir acta' }).click()
  const dialogo = page.getByRole('dialog')
  const documentoNotario = dialogo.getByLabel('Documento del notario')
  await expect(documentoNotario).toHaveValue('001-1234567-8')
  // Las seis son obligatorias: un blanco no se manda
  await documentoNotario.fill('')
  await dialogo.getByRole('button', { name: 'Reemitir acta' }).click()
  // El mensaje va en su campo; el aviso de arriba lo repite
  const campo = dialogo.locator('label', { hasText: 'Documento del notario' })
  await expect(campo.getByText('El documento del notario es obligatorio.')).toBeVisible()

  await documentoNotario.fill('001-1234567-9')
  await dialogo.getByRole('button', { name: 'Reemitir acta' }).click()
  await expect(dialogo).toHaveCount(0)
  await expect(versiones(page)).toHaveText(['Versión 2', 'Versión 1'])
})
