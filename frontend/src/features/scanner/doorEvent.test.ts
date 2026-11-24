import { describe, expect, it } from 'vitest'
import type { DoorEventResponse } from '@/api/types'
import { rememberedEvent } from './doorEvent'

const evento = (id: string, name: string): DoorEventResponse => ({
  id,
  name,
  startDate: '2026-10-23',
  endDate: '2026-10-24',
  cities: [],
})

const ventana = [evento('ev-1', 'Boda en Punta Cana'), evento('ev-2', 'Concierto en Toronto')]

describe('rememberedEvent', () => {
  it('devuelve el evento guardado si sigue en la ventana', () => {
    expect(rememberedEvent(ventana, 'ev-2')).toEqual(ventana[1])
  })

  it('vuelve a preguntar si el evento guardado salió de la ventana', () => {
    expect(rememberedEvent(ventana, 'ev-ayer')).toBeNull()
  })

  it('pregunta si no hay nada guardado, aunque la ventana tenga un solo evento', () => {
    expect(rememberedEvent([ventana[0]], null)).toBeNull()
  })
})
