import { expect, test, type Page } from '@playwright/test'
import {
  abrir,
  elegirEvento,
  EN_LA_VENTANA,
  EVENTO,
  leer,
  pie,
  ultimaLectura,
} from './scanner.helpers'

test.beforeEach(async ({ page }) => {
  await page.clock.setFixedTime(EN_LA_VENTANA)
})

// ev-0001 trae 51 participantes, todos con check-in: para leer una entrada nueva primero se le
// deshace el check-in a Ana Martínez (QR-NAV-2026-0001) desde la lista de la puerta.
const ANA = 'Ana Martínez'
const QR_ANA = 'QR-NAV-2026-0001'

async function deshacerCheckInDe(page: Page, nombre: string) {
  await page.getByRole('button', { name: /Lista de la puerta/ }).click()
  const lista = page.getByRole('dialog', { name: 'Lista de la puerta' })
  await lista.getByLabel('Buscar en la lista de la puerta').fill(nombre)
  await lista.getByRole('button', { name: `Deshacer el check-in de ${nombre}` }).click()
  await page
    .getByRole('dialog', { name: 'Deshacer el check-in' })
    .getByRole('button', { name: 'Deshacer check-in' })
    .click()
  await expect(lista.getByRole('button', { name: `Hacer check-in de ${nombre}` })).toBeVisible()
  await page.keyboard.press('Escape')
  await expect(lista).toBeHidden()
}

test('elige el evento, lo recuerda, y una entrada hace check-in una sola vez', async ({ page }) => {
  await abrir(page, '/scanner')
  // Sin pantalla de escritorio: el terminal se abre en cualquier ancho
  await expect(page.getByText('Elija el evento de esta puerta')).toBeVisible()
  await elegirEvento(page)
  await expect(pie(page)).toContainText('Entradas: 51/51')
  await deshacerCheckInDe(page, ANA)
  await expect(pie(page)).toContainText('Entradas: 50/51')
  await expect(page.getByLabel('Código', { exact: true })).toBeFocused()

  // El código del gafete va a `participants/check-in` del sorteo del evento
  await leer(page, QR_ANA)
  await expect(ultimaLectura(page)).toContainText('Check-in registrado')
  await expect(ultimaLectura(page)).toContainText(ANA)
  await expect(pie(page)).toContainText('Entradas: 51/51')

  await leer(page, QR_ANA)
  await expect(ultimaLectura(page)).toContainText('Ya había ingresado')
  // La hora ya termina en «a. m.»: sin un punto de más detrás
  await expect(ultimaLectura(page)).toContainText(/Entró a las \d{1,2}:\d{2}\s?[ap]\. m\.(?!\.)/)
  await expect(pie(page)).toContainText('Entradas: 51/51')

  await leer(page, 'QR-NO-EXISTE-9999')
  await expect(ultimaLectura(page)).toContainText('Entrada no encontrada')

  await leer(page, 'hola')
  await expect(ultimaLectura(page)).toContainText('Código no reconocido')

  // Al recargar, el evento guardado sigue elegido y el check-in quedó guardado
  await page.reload({ waitUntil: 'domcontentloaded' })
  await expect(page.locator('header')).toContainText(EVENTO)
  await expect(page.getByLabel('Código', { exact: true })).toBeVisible()
  await expect(pie(page)).toContainText('Entradas: 51/51')
})

test('una referencia abre la lista de la puerta y no hace check-in a nadie', async ({ page }) => {
  await abrir(page, '/scanner')
  await elegirEvento(page)
  await deshacerCheckInDe(page, ANA)

  await leer(page, 'ref-2026-00001')
  const lista = page.getByRole('dialog', { name: 'Lista de la puerta' })
  await expect(lista.getByLabel('Buscar en la lista de la puerta')).toHaveValue('REF-2026-00001')
  await lista.getByRole('button', { name: 'Cerrar' }).click()
  await expect(pie(page)).toContainText('Entradas: 50/51')
})

test('check-in desde la lista y deshacer con confirmación', async ({ page }) => {
  await abrir(page, '/scanner')
  await elegirEvento(page)
  await page.getByRole('button', { name: /Lista de la puerta/ }).click()
  const lista = page.getByRole('dialog', { name: 'Lista de la puerta' })
  await lista.getByLabel('Buscar en la lista de la puerta').fill(ANA)

  const deshacer = lista.getByRole('button', { name: `Deshacer el check-in de ${ANA}` })
  await deshacer.click()
  const confirmar = page.getByRole('dialog', { name: 'Deshacer el check-in' })
  // Escape cierra solo la confirmación, no la lista que está debajo
  await page.keyboard.press('Escape')
  await expect(confirmar).toBeHidden()
  await expect(lista).toBeVisible()
  await deshacer.click()
  await confirmar.getByRole('button', { name: 'Deshacer check-in' }).click()
  await expect(confirmar).toBeHidden()
  await expect(deshacer).toHaveCount(0)
  await expect(pie(page)).toContainText('Entradas: 50/51')

  await lista.getByRole('button', { name: `Hacer check-in de ${ANA}` }).click()
  await expect(deshacer).toHaveCount(1)
  await expect(pie(page)).toContainText('Entradas: 51/51')

  // Con teclado (este navegador de escritorio) el foco vuelve a la caja, para un lector USB
  await page.keyboard.press('Escape')
  await expect(lista).toBeHidden()
  await expect(page.getByLabel('Código', { exact: true })).toBeFocused()
})

test('con raffles:draw, un código de premio muestra su tarjeta', async ({ page }) => {
  await abrir(page, '/scanner')
  await elegirEvento(page)

  await leer(page, 'rd r4tw 8hnb')
  await expect(ultimaLectura(page)).toContainText('Gift Card Amex Travel US$200')
  await expect(page.getByRole('link', { name: 'Registrar entrega' })).toHaveAttribute(
    'href',
    /^\/sorteo\/sorteos\/ev-0003\?entrega=/,
  )

  await leer(page, 'RD-H3PX-7MWQ')
  await expect(ultimaLectura(page)).toContainText('se declaró nulo')
  await expect(page.getByRole('link', { name: 'Registrar entrega' })).toHaveCount(0)

  await leer(page, 'RD-K7Q2-M9XD')
  await expect(ultimaLectura(page)).toContainText('Entregado el')
})

// La cola sin conexión del escáner del sorteo (`colaCheckin.ts`). El mock no usa la red, así que la
// prueba deja la cola como la dejaría una puerta sin wifi y comprueba que se manda sola al abrir.
test('el escáner del sorteo manda solo los check-in guardados sin conexión', async ({ page }) => {
  await page.addInitScript(() => {
    // Solo la primera carga: el script corre en cada navegación
    if (sessionStorage.getItem('cola-sembrada')) return
    sessionStorage.setItem('cola-sembrada', '1')
    localStorage.setItem(
      'sorteo360.checkin.pendientes.ev-0001',
      JSON.stringify([
        { codigo: 'QR-NAV-2026-0002', leidoEn: '2026-12-18T18:40:00.000Z' },
        { codigo: 'QR-NO-EXISTE-9999', leidoEn: '2026-12-18T18:41:00.000Z' },
      ]),
    )
  })
  await abrir(page, '/sorteo/sorteos/ev-0001?tab=participantes')
  await page.getByRole('button', { name: 'Más' }).click()
  await page.getByRole('button', { name: 'Escáner de check-in QR' }).click()

  await expect(page.getByText('Se enviaron 1 check-in guardados sin conexión.')).toBeVisible()
  await expect(page.getByText('Sin participante para: QR-NO-EXISTE-9999')).toBeVisible()
  await expect(page.getByText(/guardados sin conexión; se enviarán/)).toHaveCount(0)
  expect(
    await page.evaluate(() => localStorage.getItem('sorteo360.checkin.pendientes.ev-0001')),
  ).toBeNull()
})
