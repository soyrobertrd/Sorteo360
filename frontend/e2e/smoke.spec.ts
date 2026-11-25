import { expect, test, type Page } from '@playwright/test'

/**
 * Abre una pantalla sin esperar al evento `load`: un logo puede pedirse a un servidor externo, y
 * una prueba no debe depender de que conteste.
 */
const abrir = (page: Page, ruta: string) => page.goto(ruta, { waitUntil: 'domcontentloaded' })

test('la raíz salta el landing y abre el portal con sus cuatro tarjetas', async ({ page }) => {
  await abrir(page, '/')
  await expect(page).toHaveURL(/\/app$/)
  // La identidad por defecto del mock es Administración General: ve los cuatro módulos.
  for (const tarjeta of ['Rifas en vivo', 'Agenda de eventos', 'Invitaciones', 'Marca y ajustes']) {
    await expect(page.getByRole('heading', { level: 2, name: tarjeta })).toBeVisible()
  }
  await page
    .getByRole('link', { name: 'Acceder', exact: true })
    .and(page.locator('[href="/sorteo"]'))
    .click()
  await expect(page).toHaveURL(/\/sorteo$/)
})

test.describe('barra lateral', () => {
  test('se pliega y se despliega', async ({ page }) => {
    await abrir(page, '/sorteo')
    const barra = page.locator('aside')
    const sorteos = barra.getByRole('link', { name: 'Sorteos', exact: true })
    await expect(sorteos).toBeVisible()
    await expect(barra).toHaveClass(/\bw-64\b/)

    await page.getByTitle('Ocultar barra lateral').click()
    await expect(barra).toHaveClass(/\bw-20\b/)
    // Plegada, la entrada sigue ahí y se sigue llamando igual para un lector de pantalla.
    await expect(sorteos).toBeVisible()

    await page.getByTitle('Expandir barra lateral').click()
    await expect(barra).toHaveClass(/\bw-64\b/)
  })

  test('marca la entrada de la pantalla abierta', async ({ page }) => {
    await abrir(page, '/sorteo/historial')
    const barra = page.locator('aside')
    await expect(barra.getByRole('link', { name: 'Historial de Ganadores' })).toHaveAttribute(
      'aria-current',
      'page',
    )
    await expect(barra.getByRole('link', { name: 'Sorteos', exact: true })).not.toHaveAttribute(
      'aria-current',
      'page',
    )
  })
})

test('el menú de acciones de un sorteo abre y se cierra al hacer clic fuera', async ({ page }) => {
  await abrir(page, '/sorteo/sorteos')
  const lista = page
    .getByRole('group', { name: 'Seleccionar modo de vista' })
    .getByRole('button', { name: 'Lista' })
  await lista.click()
  const boton = page.getByRole('button', { name: 'Opciones del sorteo' }).first()
  await expect(boton).toHaveAttribute('aria-expanded', 'false')

  await boton.click()
  await expect(boton).toHaveAttribute('aria-expanded', 'true')
  const verDetalles = page.getByRole('button', { name: 'Ver detalles' })
  await expect(verDetalles).toBeVisible()

  await page.getByRole('heading', { level: 1 }).first().click()
  await expect(verDetalles).toBeHidden()
  await expect(boton).toHaveAttribute('aria-expanded', 'false')
})

test('sorteos: cambia entre lista y cuadrícula', async ({ page }) => {
  await abrir(page, '/sorteo/sorteos')
  const grupo = page.getByRole('group', { name: 'Seleccionar modo de vista' })
  const lista = grupo.getByRole('button', { name: 'Lista' })
  const cuadricula = grupo.getByRole('button', { name: 'Cuadrícula' })

  await lista.click()
  await expect(lista).toHaveAttribute('aria-pressed', 'true')
  await expect(cuadricula).toHaveAttribute('aria-pressed', 'false')
  await expect(page.locator('main table').first()).toBeVisible()

  await cuadricula.click()
  await expect(cuadricula).toHaveAttribute('aria-pressed', 'true')
  await expect(page.locator('main table')).toHaveCount(0)
})
