import { expect, test } from '@playwright/test'
import { abrir } from './sorteo.helpers'

// Exclusión en lote (plans/exclusion-en-lote.md): quien ganó en un sorteo de la fiesta no gana en
// otro. Corre en modo `mock`: la Fiesta de Navidad 2025 (ev-0003) está completada con dos ganadores
// vivos, David Miller y Sofia Bianchi; el borrador es el de ev-0001.

test('excluye en el borrador a los ganadores vivos de otro sorteo y avisa a quién no encontró', async ({
  page,
}) => {
  await abrir(page, '/sorteo/sorteos/ev-0001?tab=participantes')

  // David Miller también está en esta fiesta; Sofia Bianchi no.
  await page.getByRole('button', { name: 'Agregar', exact: true }).click()
  await page.getByLabel('Nombre completo').fill('David Miller')
  await page.getByLabel('Correo electrónico').fill('david.miller@libertytravel.com')
  await page.getByRole('button', { name: 'Agregar Participante' }).click()
  const fila = page.getByRole('row').filter({ hasText: 'David Miller' })
  await expect(fila.getByText('Califica')).toBeVisible()

  await page.getByRole('button', { name: 'Excluir en lote' }).click()
  await page.getByLabel('Sorteo de origen').selectOption('ev-0003')
  await page.getByRole('button', { name: 'Excluir', exact: true }).click()
  await page.getByRole('button', { name: 'Excluir', exact: true }).last().click()

  await expect(page.getByText('1 excluidos · 0 ya estaban').first()).toBeVisible()
  await page.getByText('1 sin excluir').click()
  await expect(page.getByText('sofia.bianchi@alpitour.it')).toBeVisible()
  await page.getByRole('button', { name: 'Cerrar', exact: true }).last().click()

  await expect(fila.getByText('Excluido', { exact: true })).toBeVisible()
  await expect(fila.getByText(/Excluido a mano: Ganador de «Fiesta de Navidad 2025»/)).toBeVisible()
})
