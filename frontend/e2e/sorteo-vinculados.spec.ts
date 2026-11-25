import { expect, test } from '@playwright/test'
import { abrir } from './sorteo.helpers'

// Sorteos vinculados, en modo `mock`: la Fiesta de Navidad 2025 (ev-0003) está completada con dos
// ganadores vivos, entre ellos David Miller; el borrador de ev-0001 se vincula con ella.

test('vincular con otro sorteo muestra sus ganadores y saca del padrón a quien ya ganó allí', async ({
  page,
}) => {
  await abrir(page, '/sorteo/sorteos/ev-0001?tab=reglas')

  await page.getByLabel('Vincular con', { exact: true }).selectOption('ev-0003')
  await expect(page.getByRole('link', { name: '2 ganador(es)' })).toBeVisible()

  await page.getByRole('tab', { name: /^Participantes/ }).click()
  await page.getByRole('button', { name: 'Agregar', exact: true }).click()
  await page.getByLabel('Nombre completo').fill('David Miller')
  await page.getByLabel('Correo electrónico').fill('david.miller@libertytravel.com')
  await page.getByRole('button', { name: 'Agregar Participante' }).click()
  await expect(page.getByText('Participante "David Miller" agregado')).toBeVisible()
  await page.getByLabel('Buscar participantes').fill('David Miller')

  const fila = page.getByRole('row').filter({ hasText: 'David Miller' })
  await expect(fila.getByText('Excluido', { exact: true })).toBeVisible()
  await expect(fila.getByText('Ganó en un sorteo vinculado.')).toBeVisible()
})
