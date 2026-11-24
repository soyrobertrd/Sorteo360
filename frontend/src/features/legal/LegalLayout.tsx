import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { ES_BORRADOR, VIGENTE_DESDE } from './legalDatos'

/** El marco de los textos legales: anónimo, legible y con el enlace al otro texto. */
export function LegalLayout({
  titulo,
  otro,
  children,
}: {
  titulo: string
  otro: { to: string; label: string }
  children: ReactNode
}) {
  return (
    <div className="min-h-dvh bg-background text-foreground">
      <header className="border-b border-border">
        <div className="mx-auto flex max-w-3xl items-center justify-between gap-4 px-4 py-3 sm:px-6">
          <Link to="/" aria-label="Sorteo360, página principal">
            <img src="/logo-brand.svg" alt="Sorteo360" className="h-9 w-auto dark:hidden" />
            <img
              src="/logo-brand-dark.svg"
              alt="Sorteo360"
              className="hidden h-9 w-auto dark:block"
            />
          </Link>
          <Link
            to={otro.to}
            className="text-sm font-semibold text-muted-foreground hover:text-foreground"
          >
            {otro.label}
          </Link>
        </div>
      </header>
      <main className="mx-auto max-w-3xl px-4 py-10 sm:px-6">
        {ES_BORRADOR && (
          <p className="mb-6 rounded-xl border border-amber-300 bg-amber-50 px-4 py-3 text-sm text-amber-900 dark:border-amber-800 dark:bg-amber-950/40 dark:text-amber-200">
            Borrador: faltan los datos del titular y la revisión legal antes de publicarlo.
          </p>
        )}
        <h1 className="text-3xl font-black text-balance">{titulo}</h1>
        <p className="mt-2 text-sm text-muted-foreground">Vigente desde el {VIGENTE_DESDE}.</p>
        <div className="mt-8 space-y-4 text-[15px] leading-relaxed [&_h2]:mt-10 [&_h2]:text-xl [&_h2]:font-bold [&_li]:ml-5 [&_li]:list-disc [&_ul]:space-y-1.5">
          {children}
        </div>
      </main>
    </div>
  )
}
