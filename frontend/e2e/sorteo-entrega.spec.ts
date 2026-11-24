import { expect, test, type Locator, type Page } from '@playwright/test'
import { abrir, accion } from './sorteo.helpers'

// La entrega de un premio en el mostrador: firma, reversión con motivo y búsqueda por código (spec
// raffles/03 §2.4, §3). Corre en modo `mock` sobre raffle-0002 (ev-0003), completado: Sofia Bianchi
// tiene la gift card sin retirar, David Miller ya retiró el tour, y el código de Tom Becker se anuló.

const filaDe = (page: Page, ganador: string) => page.getByRole('row').filter({ hasText: ganador })

async function abrirAcciones(page: Page, ganador: string) {
  await page.getByRole('button', { name: `Acciones de ${ganador}` }).click()
}

/** Un trazo en el lienzo, que es lo que habilita confirmar. */
async function firmar(dialogo: Locator, page: Page) {
  const caja = (await dialogo.getByLabel('Lienzo de firma').boundingBox())!
  await page.mouse.move(caja.x + 30, caja.y + 40)
  await page.mouse.down()
  await page.mouse.move(caja.x + 120, caja.y + 90, { steps: 8 })
  await page.mouse.move(caja.x + 220, caja.y + 50, { steps: 8 })
  await page.mouse.up()
}

test('entregar con firma, revertir con motivo y volver a entregar', async ({ page }) => {
  await abrir(page, '/sorteo/sorteos/ev-0003?tab=ganadores')

  await abrirAcciones(page, 'Sofia Bianchi')
  await page.getByRole('button', { name: 'Registrar entrega' }).click()
  let dialogo = page.getByRole('dialog')
  const confirmar = dialogo.getByRole('button', { name: 'Registrar entrega' })
  await expect(confirmar).toBeDisabled()

  await dialogo.getByLabel('Sí, es Sofia Bianchi').check()
  await expect(dialogo.getByLabel('Nombre de quien retira')).toHaveValue('Sofia Bianchi')
  await dialogo.getByLabel('Documento de identidad de quien retira').fill('YA1234567')
  // Sin trazo en el lienzo no se confirma
  await expect(confirmar).toBeDisabled()
  await firmar(dialogo, page)
  await confirmar.click()
  await expect(dialogo).toHaveCount(0)
  await expect(filaDe(page, 'Sofia Bianchi').getByText('Retiró: Sofia Bianchi')).toBeVisible()

  // Revertir exige un motivo
  await abrirAcciones(page, 'Sofia Bianchi')
  await page.getByRole('button', { name: 'Revertir entrega' }).click()
  dialogo = page.getByRole('dialog')
  await dialogo.getByRole('button', { name: 'Revertir entrega' }).click()
  await expect(dialogo.getByText('El motivo de la reversión es obligatorio.')).toBeVisible()
  await dialogo.getByLabel('Motivo').fill('Se marcó el premio equivocado.')
  await dialogo.getByRole('button', { name: 'Revertir entrega' }).click()
  await expect(dialogo).toHaveCount(0)
  await expect(filaDe(page, 'Sofia Bianchi').getByText('Pendiente de entrega')).toBeVisible()

  // Una segunda entrega es una entrega más, esta vez por otra persona en su nombre
  await abrirAcciones(page, 'Sofia Bianchi')
  await page.getByRole('button', { name: 'Registrar entrega' }).click()
  dialogo = page.getByRole('dialog')
  await dialogo.getByLabel('No, lo retira otra persona en su nombre').check()
  await dialogo.getByLabel('Nombre de quien retira').fill('Marco Rossi')
  await dialogo.getByLabel('Documento de identidad de quien retira').fill('YB7654321')
  await firmar(dialogo, page)
  await dialogo.getByRole('button', { name: 'Registrar entrega' }).click()
  await expect(dialogo).toHaveCount(0)
  await expect(
    filaDe(page, 'Sofia Bianchi').getByText('Retiró en su nombre: Marco Rossi'),
  ).toBeVisible()
})

test('el enlace con ?entrega= abre la entrega de ese premio', async ({ page }) => {
  await abrir(page, '/sorteo/sorteos/ev-0003?entrega=prize-0102')
  const dialogo = page.getByRole('dialog')
  await expect(dialogo.getByText('Gift Card Amex Travel US$200')).toBeVisible()
  await expect(dialogo.getByLabel('Sí, es Sofia Bianchi')).toBeVisible()
})

test('un código se busca en el servicio y dice en qué está, anulado incluido', async ({ page }) => {
  await abrir(page, '/sorteo/sorteos/ev-0003')
  await accion(page, 'Canjear QR')
  const dialogo = page.getByRole('dialog')
  const codigo = dialogo.getByPlaceholder(/RD-7K3M/)
  const buscar = dialogo.getByRole('button', { name: 'Buscar' })

  // Sin guiones ni mayúsculas también se encuentra
  await codigo.fill('rd k7q2 m9xd')
  await buscar.click()
  await expect(dialogo.getByText(/Entregado el .* a David Miller/)).toBeVisible()

  await codigo.fill('RD-H3PX-7MWQ')
  await buscar.click()
  await expect(dialogo.getByText('Anulado', { exact: true })).toBeVisible()
  await expect(dialogo.getByText(/El código ya no da derecho al premio/)).toBeVisible()

  await codigo.fill('RD-0000-0000')
  await buscar.click()
  await expect(dialogo.getByText('No existe ningún premio con ese código de canje.')).toBeVisible()

  // Un código pendiente lleva a la entrega, no entrega solo
  await codigo.fill('R4TW8HNB')
  await buscar.click()
  await dialogo.getByRole('button', { name: 'Registrar entrega' }).click()
  await expect(page).toHaveURL(/entrega=prize-0102/)
  await expect(page.getByRole('dialog').getByLabel('Sí, es Sofia Bianchi')).toBeVisible()
})

test('el certificado lleva los firmantes que se escriben antes de descargarlo', async ({
  page,
}) => {
  await abrir(page, '/sorteo/sorteos/ev-0003?tab=ganadores')
  await abrirAcciones(page, 'Sofia Bianchi')
  await page.getByRole('button', { name: 'Certificado' }).click()
  const dialogo = page.getByRole('dialog')

  // Sin nombres fijos: cambian con cada evento (Robert, 2026-10-22)
  await expect(dialogo.getByText('Comité Organizador')).toHaveCount(0)
  await expect(dialogo.getByText('Validación Notarial')).toHaveCount(0)

  await dialogo.getByLabel('Nombre del primer firmante').fill('Laura Méndez')
  await dialogo.getByLabel('Cargo del primer firmante').fill('Gerente General')
  await dialogo.getByLabel('Nombre del segundo firmante').fill('Ana Pérez')
  await dialogo.getByLabel('Cargo del segundo firmante').fill('Directora de Recursos Humanos')
  // getByText no lee el valor de los campos: lo que encuentra es la vista previa
  await expect(dialogo.getByText('Laura Méndez', { exact: true })).toBeVisible()
  await expect(dialogo.getByText('Directora de Recursos Humanos', { exact: true })).toBeVisible()

  const descarga = page.waitForEvent('download')
  await dialogo.getByRole('button', { name: 'Descargar PDF', exact: true }).click()
  expect((await descarga).suggestedFilename()).toBe('Certificado_Premio_Sofia_Bianchi.pdf')
})
