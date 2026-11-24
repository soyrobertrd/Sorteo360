/**
 * Catálogo de roles del portal Sorteo360.
 *
 * Los nombres son exactamente los del realm de Keycloak
 * (`infra/keycloak/sorteo360-realm.json`): el token trae los permisos finos ya
 * expandidos desde el rol compuesto del usuario, así que se comparan tal cual.
 */
export const ROLES = {
  // Módulo Eventos
  eventsWrite: 'events:write',
  eventsStart: 'events:start',
  eventsCancel: 'events:cancel',
  eventsClose: 'events:close',
  quotesWrite: 'quotes:write',
  quotesSubmit: 'quotes:submit',
  quotesApprove: 'quotes:approve',
  serviceSuppliersWrite: 'service-suppliers:write',
  serviceSuppliersDelete: 'service-suppliers:delete',

  // Módulo Sorteo (los aplica events-service)
  rafflesWrite: 'raffles:write',
  rafflesDraw: 'raffles:draw',

  // Módulo CRM
  companiesWrite: 'companies:write',
  companiesSuspend: 'companies:suspend',
  companiesStatus: 'companies:status',
  contactsWrite: 'contacts:write',
  contactsDelete: 'contacts:delete',
  optsWrite: 'opts:write',
  companyCategoriesWrite: 'company-categories:write',
  contactClassificationsWrite: 'contact-classifications:write',
  originSourcesWrite: 'origin-sources:write',
  auditRead: 'audit:read',
  bankAccountsWrite: 'bank-accounts:write',
  bankAccountsReveal: 'bank-accounts:reveal',

  // Módulo Convocatorias
  campaignsWrite: 'campaigns:write',
  campaignsLaunch: 'campaigns:launch',
  convocatoriasRead: 'convocatorias:read',
  checkInWrite: 'check-in:write',
  checkInRead: 'check-in:read',
  // Crear, listar y apagar los enlaces de la carga de proveedores de cada OPT (docs/adr/0040).
  supplierIntakeManage: 'supplier-intake:manage',

  // Notificaciones (historial)
  notificationsRead: 'notifications:read',

  // Plataforma: la mitad de la política por defecto de cada servicio (ADR-0025). Es un rol,
  // no el personal de una OPT (OptStaff), y `event_staff` no lo lleva.
  platformStaff: 'platform:staff',

  // Plataforma: encender y apagar los interruptores de funciones (docs/adr/0039). Leerlos pide
  // `platform:staff`, que tiene todo el personal menos la puerta.
  featureSwitchesWrite: 'feature-switches:write',
} as const

export type FunctionRole = (typeof ROLES)[keyof typeof ROLES]

/**
 * Administración no es un módulo: abre con cualquiera de los roles de sus herramientas, y cada
 * herramienta pide el suyo. Una herramienta nueva suma su rol aquí.
 */
export const ADMIN_ROLES: readonly FunctionRole[] = [
  ROLES.featureSwitchesWrite,
  ROLES.supplierIntakeManage,
]

/** Roles compuestos del realm: el perfil con el que entra cada usuario. */
export const COMPOSITES = {
  optCoordinator: 'opt_coordinator',
  optDirector: 'opt_director',
  centralAdmin: 'central_admin',
  eventStaff: 'event_staff',
  proyector: 'proyector',
} as const

export type CompositeRole = (typeof COMPOSITES)[keyof typeof COMPOSITES]

/** Etiqueta en español de cada rol compuesto. */
const compositeLabels: Record<CompositeRole, string> = {
  opt_coordinator: 'Operador',
  opt_director: 'Supervisor',
  central_admin: 'Administrador',
  event_staff: 'Personal de la puerta',
  proyector: 'Sorteo',
}

/**
 * Permisos finos que concede cada rol compuesto. Es el espejo de los
 * `composites` del realm: si allá se agrega o quita uno, aquí también.
 */
export const COMPOSITE_GRANTS: Record<CompositeRole, readonly FunctionRole[]> = {
  opt_coordinator: [
    ROLES.eventsWrite,
    ROLES.eventsStart,
    ROLES.quotesWrite,
    ROLES.quotesSubmit,
    ROLES.serviceSuppliersWrite,
    ROLES.rafflesWrite,
    ROLES.rafflesDraw,
    ROLES.companiesStatus,
    ROLES.contactsWrite,
    ROLES.contactsDelete,
    ROLES.campaignsWrite,
    ROLES.campaignsLaunch,
    ROLES.convocatoriasRead,
    ROLES.checkInWrite,
    ROLES.checkInRead,
    ROLES.platformStaff,
  ],
  opt_director: [
    ROLES.eventsWrite,
    ROLES.eventsStart,
    ROLES.eventsCancel,
    ROLES.eventsClose,
    ROLES.quotesWrite,
    ROLES.quotesSubmit,
    ROLES.quotesApprove,
    ROLES.serviceSuppliersWrite,
    ROLES.serviceSuppliersDelete,
    ROLES.rafflesWrite,
    ROLES.rafflesDraw,
    ROLES.companiesWrite,
    ROLES.companiesStatus,
    ROLES.contactsWrite,
    ROLES.contactsDelete,
    ROLES.campaignsWrite,
    ROLES.campaignsLaunch,
    ROLES.convocatoriasRead,
    ROLES.checkInWrite,
    ROLES.checkInRead,
    ROLES.platformStaff,
  ],
  central_admin: [
    ROLES.eventsWrite,
    ROLES.eventsStart,
    ROLES.eventsCancel,
    ROLES.eventsClose,
    ROLES.quotesWrite,
    ROLES.quotesSubmit,
    ROLES.quotesApprove,
    ROLES.serviceSuppliersWrite,
    ROLES.serviceSuppliersDelete,
    ROLES.rafflesWrite,
    ROLES.rafflesDraw,
    ROLES.companiesWrite,
    ROLES.companiesSuspend,
    ROLES.companiesStatus,
    ROLES.companyCategoriesWrite,
    ROLES.contactClassificationsWrite,
    ROLES.originSourcesWrite,
    ROLES.contactsWrite,
    ROLES.contactsDelete,
    ROLES.optsWrite,
    ROLES.auditRead,
    ROLES.bankAccountsWrite,
    ROLES.bankAccountsReveal,
    ROLES.campaignsWrite,
    ROLES.campaignsLaunch,
    ROLES.convocatoriasRead,
    ROLES.checkInWrite,
    ROLES.checkInRead,
    ROLES.supplierIntakeManage,
    ROLES.notificationsRead,
    ROLES.platformStaff,
    ROLES.featureSwitchesWrite,
  ],
  event_staff: [ROLES.checkInWrite, ROLES.checkInRead],
  // El usuario «sorteo»: corre la tómbola en vivo, sin configurar nada.
  proyector: [ROLES.rafflesDraw, ROLES.platformStaff],
}

/** Perfil mínimo que concede cada permiso — para el tooltip de un control bloqueado. */
const grantedBy: Record<FunctionRole, CompositeRole> = {
  'events:write': 'opt_coordinator',
  'events:start': 'opt_coordinator',
  'events:cancel': 'opt_director',
  'events:close': 'opt_director',
  'quotes:write': 'opt_coordinator',
  'quotes:submit': 'opt_coordinator',
  'quotes:approve': 'opt_director',
  'service-suppliers:write': 'opt_coordinator',
  'service-suppliers:delete': 'opt_director',
  'raffles:write': 'opt_coordinator',
  'raffles:draw': 'opt_coordinator',
  'companies:write': 'opt_director',
  'companies:suspend': 'central_admin',
  'companies:status': 'opt_coordinator',
  'contacts:write': 'opt_coordinator',
  'contacts:delete': 'opt_coordinator',
  'opts:write': 'central_admin',
  'company-categories:write': 'central_admin',
  'contact-classifications:write': 'central_admin',
  'origin-sources:write': 'central_admin',
  'audit:read': 'central_admin',
  'bank-accounts:write': 'central_admin',
  'bank-accounts:reveal': 'central_admin',
  'campaigns:write': 'opt_coordinator',
  'campaigns:launch': 'opt_coordinator',
  'convocatorias:read': 'opt_coordinator',
  'check-in:write': 'opt_coordinator',
  'check-in:read': 'opt_coordinator',
  'supplier-intake:manage': 'central_admin',
  'notifications:read': 'central_admin',
  'platform:staff': 'opt_coordinator',
  'feature-switches:write': 'central_admin',
}

export function requiredRoleHint(role: FunctionRole): string {
  return `Requiere rol ${compositeLabels[grantedBy[role]]}`
}

export type ModuleKey =
  | 'events'
  | 'crm'
  | 'convocatorias'
  | 'sorteo'
  | 'acuerdos'
  | 'doTravelRewards'
  | 'marketing'
  | 'ferias'
  | 'meeting'
  | 'farmTrip'

/**
 * El realm no tiene un concepto de módulo: concede permisos finos y los agrupa en
 * roles compuestos. Por eso el acceso a un módulo se deriva de sus permisos — se
 * abre en cuanto el usuario tiene alguno. Sorteo se abre con los permisos de rifa
 * del realm (`raffles:write`, `raffles:draw`), que events-service aplica y que
 * tienen los tres compuestos.
 */
const moduleGrants: Record<ModuleKey, { label: string; grants: readonly FunctionRole[] }> = {
  events: {
    label: 'Agenda de eventos',
    grants: [
      ROLES.eventsWrite,
      ROLES.eventsStart,
      ROLES.eventsCancel,
      ROLES.eventsClose,
      ROLES.quotesWrite,
      ROLES.quotesSubmit,
      ROLES.quotesApprove,
      ROLES.serviceSuppliersWrite,
      ROLES.serviceSuppliersDelete,
    ],
  },
  crm: {
    label: 'CRM',
    grants: [
      ROLES.companiesWrite,
      ROLES.companiesSuspend,
      ROLES.companiesStatus,
      ROLES.contactsWrite,
      ROLES.contactsDelete,
      ROLES.optsWrite,
      ROLES.companyCategoriesWrite,
      ROLES.contactClassificationsWrite,
      ROLES.originSourcesWrite,
      ROLES.auditRead,
      ROLES.bankAccountsWrite,
    ],
  },
  convocatorias: {
    label: 'Invitaciones',
    grants: [ROLES.campaignsWrite, ROLES.campaignsLaunch, ROLES.convocatoriasRead],
  },
  sorteo: { label: 'Rifas en vivo', grants: [ROLES.rafflesWrite, ROLES.rafflesDraw] },
  acuerdos: { label: 'Acuerdos', grants: [] },
  doTravelRewards: { label: 'DO Travel Rewards', grants: [] },
  marketing: { label: 'Marketing', grants: [] },
  ferias: { label: 'Ferias', grants: [] },
  meeting: { label: 'Meeting', grants: [] },
  farmTrip: { label: 'Farm Trip', grants: [] },
}

/** Un módulo se abre con cualquiera de sus permisos. */
export function grantsModuleAccess(roles: readonly string[], module: ModuleKey): boolean {
  // Módulos futuros sin permisos definidos en el realm de Keycloak aún; abiertos temporalmente
  // para permitir la navegación y pruebas de prototipo en frontend.
  // Actualizar cuando existan los servicios correspondientes y sus permisos en el realm.
  if (module === 'meeting' || module === 'farmTrip') return true
  const { grants } = moduleGrants[module]
  // Un módulo sin permisos en el realm todavía no existe como tal: hasta que los
  // tenga, solo lo alcanza Administración General.
  if (grants.length === 0) return roles.includes(COMPOSITES.centralAdmin)
  return grants.some((role) => roles.includes(role))
}

export function moduleAccessHint(module: ModuleKey): string {
  const { grants, label } = moduleGrants[module]
  if (grants.length === 0)
    return `El módulo ${label} solo está disponible para Administración General`
  return `Requiere permisos del módulo ${label}`
}

/** Etiqueta del perfil del usuario según su rol compuesto. */
export function compositeRoleLabel(roles: string[]): string {
  if (roles.includes(COMPOSITES.centralAdmin)) return compositeLabels.central_admin
  if (roles.includes(COMPOSITES.optDirector)) return compositeLabels.opt_director
  if (roles.includes(COMPOSITES.optCoordinator)) return compositeLabels.opt_coordinator
  if (roles.includes(COMPOSITES.eventStaff)) return compositeLabels.event_staff
  return 'Usuario'
}

/**
 * El personal de la puerta entra directo al escáner y no al portal (spec convocatorias/09 §9):
 * solo cuando `event_staff` es su único rol compuesto, porque cualquier otro abre módulos.
 */
export function landsOnScanner(roles: readonly string[]): boolean {
  return (
    roles.includes(COMPOSITES.eventStaff) &&
    Object.values(COMPOSITES).every((c) => c === COMPOSITES.eventStaff || !roles.includes(c))
  )
}
