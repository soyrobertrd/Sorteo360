import { lazy, Suspense, type ComponentType } from 'react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createBrowserRouter, Navigate, RouterProvider, type RouteObject } from 'react-router-dom'
import { config } from '@/lib/config'
import { AuthProvider, requireLoginOutsidePublicPaths } from '@/auth/AuthProvider'
import { RequireModule, RequireRole } from '@/auth/RequireModule'
import { ADMIN_ROLES, ROLES } from '@/auth/roles'
import { RequireFeature, RequirePublicPortal } from '@/features/admin/FeatureGate'
import type { FeatureKey } from '@/features/admin/featureCatalogue'
import { PortalLayout } from './layout/PortalLayout'
import { AppShell } from './layout/AppShell'
import { PublicLayout } from './layout/PublicLayout'
import { Spinner } from '@/components/ui/Feedback'
import { AppToaster } from '@/components/ui/AppToaster'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { retry: 1, staleTime: 30_000, refetchOnWindowFocus: false },
  },
})

/** Cada página del ruteador se carga bajo demanda: evita un único bundle gigante con todos los módulos. */
function lazyPage(loader: () => Promise<Record<string, unknown>>, exportName: string) {
  const LazyComponent = lazy(() =>
    loader().then((module) => ({ default: module[exportName] as ComponentType })),
  )
  return (
    <Suspense fallback={<Spinner />}>
      <LazyComponent />
    </Suspense>
  )
}

const TerminosPage = () => lazyPage(() => import('@/features/legal/TerminosPage'), 'TerminosPage')
const PrivacidadPage = () =>
  lazyPage(() => import('@/features/legal/PrivacidadPage'), 'PrivacidadPage')
const CrearCuentaPage = () =>
  lazyPage(() => import('@/features/cuenta/CrearCuentaPage'), 'CrearCuentaPage')
const ActivarCuentaPage = () =>
  lazyPage(() => import('@/features/cuenta/ActivarCuentaPage'), 'ActivarCuentaPage')
const PortalPage = () => lazyPage(() => import('@/features/portal/PortalPage'), 'PortalPage')
const DashboardPage = () =>
  lazyPage(() => import('@/features/dashboard/DashboardPage'), 'DashboardPage')
const EventosListPage = () =>
  lazyPage(() => import('@/features/eventos/EventosListPage'), 'EventosListPage')
const EventoDetailPage = () =>
  lazyPage(() => import('@/features/eventos/EventoDetailPage'), 'EventoDetailPage')
const CalendarioPage = () =>
  lazyPage(() => import('@/features/eventos/CalendarioPage'), 'CalendarioPage')
const PublicEventosListPage = () =>
  lazyPage(() => import('@/features/publico/PublicEventosListPage'), 'PublicEventosListPage')
const PublicEventoDetailPage = () =>
  lazyPage(() => import('@/features/publico/PublicEventoDetailPage'), 'PublicEventoDetailPage')
const PublicCalendarioPage = () =>
  lazyPage(() => import('@/features/publico/PublicCalendarioPage'), 'PublicCalendarioPage')
const ConvocatoriasDashboardPage = () =>
  lazyPage(
    () => import('@/features/convocatorias/ConvocatoriasDashboardPage'),
    'ConvocatoriasDashboardPage',
  )
const ConvocatoriasEventosPage = () =>
  lazyPage(
    () => import('@/features/convocatorias/ConvocatoriasEventosPage'),
    'ConvocatoriasEventosPage',
  )
const CampanasPage = () =>
  lazyPage(() => import('@/features/convocatorias/CampanasPage'), 'CampanasPage')
const ConvocatoriaEventoPage = () =>
  lazyPage(
    () => import('@/features/convocatorias/ConvocatoriaEventoPage'),
    'ConvocatoriaEventoPage',
  )
const CampanaDetallePage = () =>
  lazyPage(() => import('@/features/convocatorias/CampanaDetallePage'), 'CampanaDetallePage')
const InvitadosPage = () =>
  lazyPage(() => import('@/features/convocatorias/InvitadosPage'), 'InvitadosPage')
const InvitadosEventoDetailPage = () =>
  lazyPage(
    () => import('@/features/convocatorias/InvitadosEventoDetailPage'),
    'InvitadosEventoDetailPage',
  )
const CheckinPage = () =>
  lazyPage(() => import('@/features/convocatorias/CheckinPage'), 'CheckinPage')
const PlantillasConvocatoriaPage = () =>
  lazyPage(
    () => import('@/features/convocatorias/PlantillasConvocatoriaPage'),
    'PlantillasConvocatoriaPage',
  )
const AdminHubPage = () => lazyPage(() => import('@/features/admin/AdminHubPage'), 'AdminHubPage')
const FeatureSwitchesPage = () =>
  lazyPage(() => import('@/features/admin/FeatureSwitchesPage'), 'FeatureSwitchesPage')
const ConfiguracionPage = () =>
  lazyPage(() => import('@/features/usuario/ConfiguracionPage'), 'ConfiguracionPage')
const SorteoDashboardPage = () =>
  lazyPage(() => import('@/features/sorteo/SorteoDashboardPage'), 'SorteoDashboardPage')
const SorteosListPage = () =>
  lazyPage(() => import('@/features/sorteo/SorteosListPage'), 'SorteosListPage')
const SorteoDetailPage = () =>
  lazyPage(() => import('@/features/sorteo/SorteoDetailPage'), 'SorteoDetailPage')
const SorteoEjecucionPage = () =>
  lazyPage(() => import('@/features/sorteo/SorteoEjecucionPage'), 'SorteoEjecucionPage')
const HistorialGanadoresPage = () =>
  lazyPage(() => import('@/features/sorteo/HistorialGanadoresPage'), 'HistorialGanadoresPage')
const ProyectorPage = () =>
  lazyPage(() => import('@/features/sorteo/ProyectorPage'), 'ProyectorPage')
const PwaScannerPage = () =>
  lazyPage(() => import('@/features/scanner/PwaScannerPage'), 'PwaScannerPage')
const PublicFormStandalonePage = () =>
  lazyPage(() => import('@/features/publico/PublicFormStandalonePage'), 'PublicFormStandalonePage')

// Las páginas a las que llevan los correos de convocatorias; anónimas, fuera de Keycloak.
const RegistroInvitacionPage = () =>
  lazyPage(
    () => import('@/features/publico/portal/RegistroInvitacionPage'),
    'RegistroInvitacionPage',
  )
const BajaPage = () => lazyPage(() => import('@/features/publico/portal/BajaPage'), 'BajaPage')
const EntradaPage = () =>
  lazyPage(() => import('@/features/publico/portal/EntradaPage'), 'EntradaPage')
const NotFoundPage = () => lazyPage(() => import('@/features/errors/NotFoundPage'), 'NotFoundPage')

/** Las rutas de una función, detrás de su interruptor. */
const feature = (featureKey: FeatureKey, children: RouteObject[]): RouteObject => ({
  element: <RequireFeature featureKey={featureKey} />,
  children,
})

const router = createBrowserRouter([
  // v1.0: el landing comercial queda oculto (features/landing sigue en el árbol); la raíz va
  // directo al login y de ahí a /app.
  { path: '/', element: <Navigate to="/app" replace /> },
  // Con el registro apagado (VITE_SIGNUP_ENABLED=false) estas rutas vuelven al landing.
  {
    path: '/crear-cuenta',
    element: config.signupEnabled ? CrearCuentaPage() : <Navigate to="/" replace />,
  },
  { path: '/terminos', element: TerminosPage() },
  { path: '/privacidad', element: PrivacidadPage() },
  {
    path: '/activar/:token',
    element: config.signupEnabled ? ActivarCuentaPage() : <Navigate to="/" replace />,
  },
  {
    path: '/app',
    element: <PortalLayout />,
    children: [{ index: true, element: PortalPage() }],
  },
  {
    element: <RequireRole role={ROLES.checkInWrite} />,
    children: [{ path: '/scanner', element: PwaScannerPage() }],
  },
  {
    // La pantalla del salón: sin marco, la maneja la consola del operador desde esta laptop.
    element: <RequireModule module="sorteo" />,
    children: [{ path: '/proyector/:eventId', element: ProyectorPage() }],
  },
  {
    element: <AppShell />,
    children: [
      {
        element: <RequireModule module="events" />,
        children: [
          { path: '/eventos', element: DashboardPage() },
          feature('events.events', [
            { path: '/eventos/todos', element: EventosListPage() },
            { path: '/eventos/:id', element: EventoDetailPage() },
          ]),
          feature('events.calendar', [{ path: '/eventos/calendario', element: CalendarioPage() }]),
        ],
      },
      {
        element: <RequireModule module="sorteo" />,
        children: [
          { path: '/convocatorias', element: ConvocatoriasDashboardPage() },
          // El detalle de una campaña se abre desde su evento.
          feature('convocatorias.events', [
            { path: '/convocatorias/eventos', element: ConvocatoriasEventosPage() },
            { path: '/convocatorias/eventos/:id', element: ConvocatoriaEventoPage() },
            {
              path: '/convocatorias/eventos/:id/campanas/:campaignId',
              element: CampanaDetallePage(),
            },
          ]),
          feature('convocatorias.campaigns', [
            { path: '/convocatorias/campanas', element: CampanasPage() },
          ]),
          feature('convocatorias.templates', [
            { path: '/convocatorias/plantillas', element: PlantillasConvocatoriaPage() },
          ]),
          feature('convocatorias.guests', [
            { path: '/convocatorias/invitados', element: InvitadosPage() },
            { path: '/convocatorias/invitados/:id', element: InvitadosEventoDetailPage() },
          ]),
          feature('convocatorias.check-in', [
            { path: '/convocatorias/checkin', element: CheckinPage() },
          ]),
        ],
      },
      {
        element: <RequireModule module="sorteo" />,
        children: [
          { path: '/sorteo', element: SorteoDashboardPage() },
          feature('sorteo.raffles', [
            { path: '/sorteo/sorteos', element: SorteosListPage() },
            { path: '/sorteo/sorteos/:eventId', element: SorteoDetailPage() },
            { path: '/sorteo/sorteos/:eventId/ejecutar', element: SorteoEjecucionPage() },
          ]),
          feature('sorteo.winners-history', [
            { path: '/sorteo/historial', element: HistorialGanadoresPage() },
          ]),
        ],
      },
      {
        // El hub abre con cualquiera de los roles de Administración; cada herramienta pide el suyo.
        element: <RequireRole role={ADMIN_ROLES} />,
        children: [
          { path: '/admin', element: AdminHubPage() },
          {
            element: <RequireRole role={ROLES.featureSwitchesWrite} />,
            children: [{ path: '/admin/feature-switches', element: FeatureSwitchesPage() }],
          },
        ],
      },
      { path: '/configuracion', element: ConfiguracionPage() },
      { path: '*', element: NotFoundPage() },
    ],
  },
  {
    // El portal público y el registro existen solo con convocatorias encendido.
    element: <RequirePublicPortal />,
    children: [
      { path: '/registro/:id', element: PublicFormStandalonePage() },
      {
        path: '/publico',
        element: <PublicLayout />,
        children: [
          { path: 'eventos', element: PublicEventosListPage() },
          { path: 'eventos/:id', element: PublicEventoDetailPage() },
          { path: 'calendario', element: PublicCalendarioPage() },
          { path: 'registro', element: RegistroInvitacionPage() },
          { path: 'baja', element: BajaPage() },
          { path: 'entrada/:qrCode', element: EntradaPage() },
          { path: '*', element: NotFoundPage() },
        ],
      },
    ],
  },
])

// Quien entró sin login por una página pública no ve las del personal sin pasar por Keycloak.
router.subscribe(({ location }) => requireLoginOutsidePublicPaths(location.pathname))

export function App() {
  return (
    <AuthProvider>
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
        <AppToaster />
      </QueryClientProvider>
    </AuthProvider>
  )
}
