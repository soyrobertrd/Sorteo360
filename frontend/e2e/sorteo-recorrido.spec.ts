import { expect, test, type Page } from '@playwright/test'
import { abrir, accion, girar, lanzar, pestanaConfiguracion } from './sorteo.helpers'

// El recorrido de la noche, de punta a punta, en modo `mock`: crear el sorteo, cargar premios y un
// padrón con una fila mala, iniciar, girar, recargar sin perder al ganador y bajar el acta. El mock
// guarda su estado en `localStorage`, así que la recarga lo conserva como lo haría el servicio.

const csv = (name: string, texto: string) => ({
  name,
  mimeType: 'text/csv',
  buffer: Buffer.from(texto, 'utf8'),
})

const PREMIOS = csv(
  'premios.csv',
  'Ronda,Premio,Valor,Cantidad\n1,Smart TV 55",25000,1\n1,Tablet 10",12000,1\n',
)

// La fila 4 no tiene nombre: se informa y no se importa.
const PADRON = csv(
  'padron.csv',
  [
    'Nombre,Correo,Departamento,Codigo',
    'Ana Gómez,ana.gomez@empresa.com,Finanzas,E-101',
    'Luis Peña,luis.pena@empresa.com,Operaciones,E-102',
    ',sin.nombre@empresa.com,Ventas,E-103',
    'Rosa Díaz,rosa.diaz@empresa.com,Tecnología,E-104',
  ].join('\n'),
)
const PARTICIPANTES = ['Ana Gómez', 'Luis Peña', 'Rosa Díaz']

/** Escribe en un campo que se guarda al salir de él. */
async function escribir(page: Page, placeholder: string | RegExp, valor: string, n = 0) {
  const campo = page.getByPlaceholder(placeholder).nth(n)
  await campo.fill(valor)
  await campo.blur()
}

test('crear, importar, sortear, recargar y bajar el acta', async ({ page }) => {
  // 1. Crear el sorteo, sin evento: el padrón entra por archivo y todos participan.
  await abrir(page, '/sorteo/sorteos')
  await page.getByRole('button', { name: 'Nuevo Sorteo' }).click()
  const nuevo = page.getByRole('dialog')
  await nuevo.getByLabel(/Nombre del Sorteo/).fill('Fiesta de Prueba E2E')
  await nuevo.getByRole('button', { name: 'Crear Sorteo' }).click()
  await expect(page).toHaveURL(/\/sorteo\/sorteos\/[^/]+$/)
  const detalle = page.url()
  await expect(page.getByRole('heading', { level: 1, name: 'Fiesta de Prueba E2E' })).toBeVisible()

  // 2. Los premios, por CSV: la ronda 1 se crea sola.
  await page.getByRole('button', { name: 'Agregar', exact: true }).click()
  await page.getByRole('button', { name: 'Importar premios (Excel/CSV)' }).click()
  const premios = page.getByRole('dialog', { name: 'Importar premios' })
  await premios.locator('input[type="file"]').setInputFiles(PREMIOS)
  await premios.getByRole('button', { name: 'Importar 2 premio(s)' }).click()
  await expect(premios).toBeHidden()
  await expect(page.getByRole('tab', { name: 'Premios (2)' })).toBeVisible()

  // 3. El padrón, con una fila mala que se informa y no entra.
  await page.getByRole('tab', { name: /^Participantes/ }).click()
  await page.getByRole('button', { name: 'Cargar padrón' }).click()
  const padron = page.getByRole('dialog')
  await padron.locator('input[type="file"]').setInputFiles(PADRON)
  await expect(padron.getByText('1 fila(s) con observaciones')).toBeVisible()
  await expect(padron.getByText('Fila sin nombre: no se importa.')).toBeVisible()
  await expect(padron.getByText('3 participante(s) listos para importar')).toBeVisible()
  await padron.getByRole('button', { name: 'Importar 3 participante(s)' }).click()
  await expect(padron).toBeHidden()
  await expect(page.getByRole('tab', { name: 'Participantes (3)' })).toBeVisible()

  // 4. Iniciar congela la tómbola; 5. girar da un ganador del padrón.
  await lanzar(page)
  await girar(page)
  const ganador = (
    await page.getByRole('dialog').getByRole('heading', { level: 2 }).textContent()
  )?.trim()
  expect(PARTICIPANTES).toContain(ganador)

  // 6. Recargar en plena noche no pierde al ganador.
  await page.reload({ waitUntil: 'domcontentloaded' })
  await expect(page.getByRole('button', { name: /GIRAR RULETA/ })).toBeVisible()
  await abrir(page, `${detalle}?tab=ganadores`)
  await expect(page.getByRole('tab', { name: 'Ganadores (1)' })).toBeVisible()
  await expect(page.getByRole('row').filter({ hasText: ganador! })).toBeVisible()

  // 7. Completar el sorteo genera el acta, y se descarga en PDF.
  await pestanaConfiguracion(page).click()
  await escribir(page, /Notario Público/, 'Lic. Manuel Rodríguez')
  await escribir(page, /Dirección de Promoción/, 'Licda. Carolina Ortiz')
  await escribir(page, /Auditoría Técnica/, 'Ing. David Ramos')
  for (const [n, documento] of ['001-1', '001-2', '001-3'].entries()) {
    await escribir(page, 'Documento de identidad', documento, n)
  }
  await escribir(page, /Mesa de entrega de premios/, 'Mesa de premios del salón, hasta las 22:00')
  await accion(page, 'Completar sorteo')
  const completar = page.getByRole('dialog')
  await completar.getByRole('button', { name: 'Completar sorteo' }).click()
  await expect(completar).toBeHidden()
  await expect(page.getByText('Completado').first()).toBeVisible()

  await expect(
    page.getByRole('button', { name: 'Descargar el acta generada, versión 1' }),
  ).toBeVisible()
  // El enlace abre en otra pestaña (`target=_blank`).
  // shortcut: el mock no arma el PDF (`getFileContentUrl` da un marcador de placehold.co), así que
  // solo se comprueba que el acta se abre; cuando el mock lo genere, exigir un `.pdf` o un `blob:`.
  const acta = page.context().waitForEvent('page')
  await accion(page, 'Descargar acta notarial')
  expect((await acta).url()).not.toBe('')
})
