/**
 * El catálogo de planes, igual al del servidor (plans/sorteo360.md §9.2). El servidor es quien hace
 * cumplir los topes; esta copia solo sirve para mostrarlos en el landing y en el registro.
 * `null` = sin límite.
 */

export type PlanKey = 'free' | 'basic' | 'pro' | 'advanced'
export type BillingPeriod = 'monthly' | 'annual'

export interface Plan {
  key: PlanKey
  nombre: string
  resumen: string
  participantesPorSorteo: number | null
  /** Gratis cuenta sorteos creados en el mes; los de pago, sorteos activos a la vez. */
  sorteos: { tope: number | null; por: 'mes' | 'activos' }
  rondasPorSorteo: number | null
  premiosPorSorteo: number | null
  usuarios: number | null
  proyector: boolean
  apiWebhook: boolean
  marcaPropia: boolean
  /** Precio mensual en RD$; `null` mientras Robert no lo fije (§9.8). */
  precioMensual: number | null
  destacado?: boolean
}

/** El pago anual cobra 10 meses: dos de regalo. */
export const MESES_COBRADOS_AL_ANO = 10

export const PLANES: readonly Plan[] = [
  {
    key: 'free',
    nombre: 'Gratis',
    resumen: 'Para probar con un equipo pequeño.',
    participantesPorSorteo: 25,
    sorteos: { tope: 1, por: 'mes' },
    rondasPorSorteo: 1,
    premiosPorSorteo: 10,
    usuarios: 1,
    proyector: false,
    apiWebhook: false,
    marcaPropia: false,
    precioMensual: 0,
  },
  {
    key: 'basic',
    nombre: 'Básico',
    resumen: 'La fiesta de un departamento o una sucursal.',
    participantesPorSorteo: 250,
    sorteos: { tope: 3, por: 'activos' },
    rondasPorSorteo: 5,
    premiosPorSorteo: 100,
    usuarios: 3,
    proyector: true,
    apiWebhook: false,
    marcaPropia: true,
    precioMensual: null,
  },
  {
    key: 'pro',
    nombre: 'Pro',
    resumen: 'Empresas medianas con varias actividades al año.',
    participantesPorSorteo: 1_000,
    sorteos: { tope: 10, por: 'activos' },
    rondasPorSorteo: 20,
    premiosPorSorteo: 1_000,
    usuarios: 10,
    proyector: true,
    apiWebhook: true,
    marcaPropia: true,
    precioMensual: null,
    destacado: true,
  },
  {
    key: 'advanced',
    nombre: 'Avanzado',
    resumen: 'La fiesta de toda la empresa, sin topes.',
    participantesPorSorteo: null,
    sorteos: { tope: null, por: 'activos' },
    rondasPorSorteo: null,
    premiosPorSorteo: null,
    usuarios: null,
    proyector: true,
    apiWebhook: true,
    marcaPropia: true,
    precioMensual: null,
  },
]

export const planPorClave = (key: string | null | undefined): Plan =>
  PLANES.find((p) => p.key === key) ?? PLANES[0]

const numero = new Intl.NumberFormat('es-DO')
const pesos = new Intl.NumberFormat('es-DO', {
  style: 'currency',
  currency: 'DOP',
  maximumFractionDigits: 0,
})

const cuanto = (n: number | null, uno: string, varios: string, sinLimite: string) =>
  n === null ? sinLimite : `${numero.format(n)} ${n === 1 ? uno : varios}`

/** Lo que el plan trae, en el orden en que se muestra. */
export function lineasDelPlan(plan: Plan): { texto: string; incluido: boolean }[] {
  const s = plan.sorteos
  return [
    {
      texto: cuanto(
        plan.participantesPorSorteo,
        'participante por sorteo',
        'participantes por sorteo',
        'Participantes sin límite',
      ),
      incluido: true,
    },
    {
      texto: cuanto(
        s.tope,
        s.por === 'mes' ? 'sorteo al mes' : 'sorteo activo',
        s.por === 'mes' ? 'sorteos al mes' : 'sorteos activos',
        'Sorteos sin límite',
      ),
      incluido: true,
    },
    {
      texto: cuanto(
        plan.rondasPorSorteo,
        'ronda por sorteo',
        'rondas por sorteo',
        'Rondas sin límite',
      ),
      incluido: true,
    },
    {
      texto: cuanto(
        plan.premiosPorSorteo,
        'premio por sorteo',
        'premios por sorteo',
        'Premios sin límite',
      ),
      incluido: true,
    },
    { texto: cuanto(plan.usuarios, 'usuario', 'usuarios', 'Usuarios sin límite'), incluido: true },
    { texto: 'Pantalla de proyector', incluido: plan.proyector },
    { texto: 'Tu marca, sin marca de agua', incluido: plan.marcaPropia },
    { texto: 'Padrón por API y webhook', incluido: plan.apiWebhook },
  ]
}

/** "RD$1,500 / mes", "RD$15,000 / año", "Gratis" o "Precio a consultar". */
export function precioTexto(plan: Plan, periodo: BillingPeriod): string {
  if (plan.precioMensual === 0) return 'Gratis'
  if (plan.precioMensual === null) return 'Precio a consultar'
  return periodo === 'monthly'
    ? `${pesos.format(plan.precioMensual)} / mes`
    : `${pesos.format(plan.precioMensual * MESES_COBRADOS_AL_ANO)} / año`
}
