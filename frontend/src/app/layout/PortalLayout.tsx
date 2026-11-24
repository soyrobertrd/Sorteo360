import { Outlet } from 'react-router-dom'
import { PortalHeader } from './PortalHeader'
import { PortalFooter } from './PortalFooter'

export function PortalLayout() {
  return (
    <div className="flex min-h-screen flex-col bg-background text-foreground transition-colors lg:h-screen dark:bg-card dark:text-foreground">
      <PortalHeader />
      <main className="flex flex-1 flex-col justify-center py-4">
        <div className="mx-auto w-full max-w-7xl px-4 sm:px-6 lg:px-8">
          <Outlet />
        </div>
      </main>
      <PortalFooter />
    </div>
  )
}
