import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'

/** El marco de las páginas anónimas de la cuenta: logo, tarjeta centrada y vuelta al landing. */
export function CuentaLayout({ titulo, children }: { titulo: string; children: ReactNode }) {
  return (
    <div className="flex min-h-dvh flex-col bg-background px-4 py-10 text-foreground">
      <Link to="/" className="mx-auto" aria-label="Sorteo360, página principal">
        <img src="/logo-brand.svg" alt="Sorteo360" className="h-10 w-auto dark:hidden" />
        <img src="/logo-brand-dark.svg" alt="Sorteo360" className="hidden h-10 w-auto dark:block" />
      </Link>
      <main className="mx-auto mt-8 w-full max-w-md rounded-3xl border border-border bg-card p-6 shadow-sm sm:p-8">
        <h1 className="text-2xl font-black text-balance">{titulo}</h1>
        <div className="mt-6">{children}</div>
      </main>
    </div>
  )
}
