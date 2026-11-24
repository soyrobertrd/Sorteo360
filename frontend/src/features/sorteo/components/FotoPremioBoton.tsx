import { useState } from 'react'
import { ImagePlus } from 'lucide-react'
import type { PrizeResponse } from '@/api/types'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import type { FotoElegida } from '../hooks'
import { SelectorFoto } from './SelectorFoto'

/**
 * El botón de la foto de un premio: abre un modal para subirla, pegar un enlace o quitarla. Se
 * aplica a los premios iguales de la ronda.
 */
export function FotoPremioBoton({
  premio,
  onImagen,
}: {
  premio: PrizeResponse
  onImagen: (foto: FotoElegida) => void
}) {
  const [abierto, setAbierto] = useState(false)
  const [elegida, setElegida] = useState<Exclude<FotoElegida, null> | undefined>(undefined)
  const cerrar = () => {
    setAbierto(false)
    setElegida(undefined)
  }

  return (
    <>
      <button
        type="button"
        onClick={() => setAbierto(true)}
        title={premio.imageUrl ? 'Cambiar la foto' : 'Agregar foto'}
        aria-label={`${premio.imageUrl ? 'Cambiar la foto de' : 'Agregar foto a'} ${premio.name}`}
        className="cursor-pointer rounded-md p-1 text-slate-400 transition-colors hover:bg-muted hover:text-brand dark:hover:text-brand-accent"
      >
        <ImagePlus size={15} aria-hidden />
      </button>
      {abierto && (
        <Modal title={`Foto de ${premio.name}`} onClose={cerrar}>
          <div className="space-y-4">
            <SelectorFoto actual={premio.imageUrl} onChange={setElegida} />
            <p className="text-xs text-muted-foreground">
              Se aplica a todos los premios iguales de la ronda.
            </p>
            <div className="flex flex-wrap justify-between gap-2 border-t border-border pt-4">
              {premio.imageUrl ? (
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => {
                    onImagen(null)
                    cerrar()
                  }}
                  className="text-rose-600"
                >
                  Quitar la foto
                </Button>
              ) : (
                <span />
              )}
              <div className="flex gap-2">
                <Button type="button" variant="outline" onClick={cerrar}>
                  Cancelar
                </Button>
                <Button
                  type="button"
                  disabled={!elegida}
                  onClick={() => {
                    if (elegida) onImagen(elegida)
                    cerrar()
                  }}
                >
                  Guardar foto
                </Button>
              </div>
            </div>
          </div>
        </Modal>
      )}
    </>
  )
}
