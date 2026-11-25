import { useEffect, useMemo, useSyncExternalStore } from 'react'
import { createStore } from './store'
import { ESTADOS_INSCRITOS, type Invitado } from './mockParticipants'
import type { MontajeEvento } from '@/api/types'

/**
 * Mesas y sillas de un evento. Quien recibe una campaña se registra en el formulario público,
 * pasa a la lista de registrados y el día del evento entra con su QR. La silla es de un
 * registrado: en modo automático la recibe al registrarse, en manual alguien se la da después.
 *
 * No todos los eventos asignan asiento: por defecto la gente se sienta donde quiera. Cada
 * evento se hace en un sitio distinto, así que no hay plano real: el montaje del evento dice
 * si hay mesas (banquete), filas (auditorio, conferencia, escuela, herradura) o nadie se
 * sienta (cóctel), y aquí solo se guarda cuántas hay, cuántos asientos tiene cada una y
 * quién va en cada asiento. Internamente una fila también es una "mesa".
 *
 * shortcut: vive en `localStorage` de este navegador, solo para la demo; cuando
 * convocatorias-service lo guarde (REQUERIMIENTOS_BACKEND.md §12) se lee de la API.
 */

/** `libre`: sin asignación. `automatica`: al registrarse. `manual`: alguien la hace después. */
export type ModoMesas = 'libre' | 'automatica' | 'manual'
export type CriterioMesas = 'registro' | 'empresa' | 'categoria'

export interface ConfigMesas {
  modo: ModoMesas
  criterio: CriterioMesas
  mesas: number
  sillasPorMesa: number
  /** Categoría reservada por número de mesa; una mesa sin categoría recibe a cualquiera. */
  categorias: Record<number, string>
  /** Nombre opcional por número de mesa ("Mesa Presidencial"). */
  nombres?: Record<number, string>
  /**
   * Zona de protocolo: las primeras N mesas o filas, al frente, son para autoridades y VIP.
   * El reparto automático las llena primero con ellos y no sienta ahí a nadie más.
   */
  reservadas?: number
  /**
   * Las mesas o filas VIP elegidas una a una (la 1, la 3...). Si falta, valen las primeras
   * `reservadas`; con asiento libre siempre valen esas, porque son todo lo que se asigna.
   */
  vip?: number[]
  /** Auditorio y conferencia: en cuántos bloques (1 a 3) se dibuja cada fila. Solo visual. */
  columnas?: number
  /**
   * Conferencia: asientos de la mesa principal, sobre el escenario y de frente al público. Es la
   * mesa 0 y se asigna a mano, de antemano.
   */
  mesaPrincipal?: number
  /**
   * Mesa principal: invitados sin registro ni QR (autoridades, oradores), escritos a mano por
   * número de asiento. Un registrado sentado en ese asiento manda sobre el nombre.
   */
  invitadosPrincipal?: Record<number, string>
}

/** Cómo se reparte el salón según el montaje del evento. */
export type Distribucion = 'mesas' | 'filas' | 'herradura' | 'escuela' | 'de_pie'

/**
 * Lo que cada montaje decide del salón: cómo se reparte y con qué configuración nace, encima de
 * `CONFIG_POR_DEFECTO`. Un montaje nuevo no compila hasta que alguien decide las dos cosas acá.
 */
const SALON_POR_MONTAJE: Record<
  MontajeEvento,
  { distribucion: Distribucion; inicial: Partial<ConfigMesas> }
> = {
  Banquet: { distribucion: 'mesas', inicial: { reservadas: 1 } },
  Auditorium: {
    distribucion: 'filas',
    inicial: { mesas: 10, sillasPorMesa: 12, columnas: 2, reservadas: 1 },
  },
  // Una conferencia es un auditorio con su mesa principal.
  Conference: {
    distribucion: 'filas',
    inicial: { mesas: 10, sillasPorMesa: 12, columnas: 2, reservadas: 1, mesaPrincipal: 6 },
  },
  Cocktail: { distribucion: 'de_pie', inicial: { reservadas: 1 } },
  UShape: { distribucion: 'herradura', inicial: { mesas: 1, sillasPorMesa: 20, reservadas: 0 } },
  Classroom: { distribucion: 'escuela', inicial: { mesas: 12, sillasPorMesa: 2, reservadas: 0 } },
}

export function distribucionDe(montaje: MontajeEvento | null | undefined): Distribucion | null {
  return montaje ? SALON_POR_MONTAJE[montaje].distribucion : null
}

export interface Etiquetas {
  unidad: string
  unidades: string
  asiento: string
  asientos: string
  /** Abreviaturas para etiquetas cortas: "M3 · S8", "F2 · A5". */
  u: string
  a: string
}

export function etiquetasDe(montaje: MontajeEvento | null | undefined): Etiquetas {
  const dist = distribucionDe(montaje)
  if (dist === 'filas') {
    return {
      unidad: 'Fila',
      unidades: 'Filas',
      asiento: 'Asiento',
      asientos: 'Asientos',
      u: 'F',
      a: 'A',
    }
  }
  if (dist === 'herradura') {
    return {
      unidad: 'Mesa en U',
      unidades: 'Mesas en U',
      asiento: 'Asiento',
      asientos: 'Asientos',
      u: 'U',
      a: 'A',
    }
  }
  return { unidad: 'Mesa', unidades: 'Mesas', asiento: 'Silla', asientos: 'Sillas', u: 'M', a: 'S' }
}

/** Las categorías del CRM que van en la zona de protocolo. */
export const CATEGORIAS_PROTOCOLO = ['VIP', 'Autoridad']
export const esProtocolo = (categoria?: string | null) =>
  Boolean(categoria && CATEGORIAS_PROTOCOLO.includes(categoria))

/** La primera configuración según el montaje: una conferencia nace con su mesa principal. */
export function configInicial(montaje: MontajeEvento | null | undefined): ConfigMesas {
  return {
    ...CONFIG_POR_DEFECTO,
    ...(montaje ? SALON_POR_MONTAJE[montaje].inicial : { reservadas: 1 }),
  }
}

export interface Asiento {
  mesa: number
  silla: number
  /** La empresa de quien se sienta, para agrupar a los que lleguen después. */
  empresa?: string
}

/** Quien puede ocupar una silla: lo que el plan necesita saber de un registrado. */
export interface Sentable {
  email: string
  empresa: string
  categoria?: string | null
  fechaRegistro?: string | null
}

export const MODO_MESAS_LABEL: Record<ModoMesas, string> = {
  libre: 'Sin asignación',
  automatica: 'Automática al registrarse',
  manual: 'Asignación manual',
}

export const CRITERIO_MESAS_LABEL: Record<CriterioMesas, string> = {
  registro: 'Por orden de registro',
  empresa: 'Agrupar por empresa',
  categoria: 'Por categoría (CRM)',
}

export const CONFIG_POR_DEFECTO: ConfigMesas = {
  modo: 'libre',
  criterio: 'registro',
  mesas: 10,
  sillasPorMesa: 8,
  categorias: {},
}

/** Los asientos se guardan por correo: el mismo registrado en el check-in y en la lista. */
export const claveDe = (email: string) => email.trim().toLowerCase()

/** Solo un registrado ocupa silla; quien no se registró o canceló, no. */
export const puedeSentarse = (i: Invitado) => ESTADOS_INSCRITOS.includes(i.status)

export const nombreDeMesa = (
  config: ConfigMesas,
  mesa: number,
  etiquetas: Etiquetas = etiquetasDe(null),
) =>
  mesa === MESA_PRINCIPAL
    ? 'Mesa principal'
    : config.nombres?.[mesa]?.trim() || `${etiquetas.unidad} ${mesa}`

/** "Mesa 3 · Silla 5" o "Fila 2 · Asiento 7" para mostrar donde haga falta. */
export const textoAsiento = (
  a: Asiento,
  config?: ConfigMesas,
  etiquetas: Etiquetas = etiquetasDe(null),
) =>
  `${config ? nombreDeMesa(config, a.mesa, etiquetas) : `${etiquetas.unidad} ${a.mesa}`} · ${etiquetas.asiento} ${a.silla}`

/** "M3 · S5" o "F2 · A7" para una etiqueta corta en una tabla; la mesa principal es "MP". */
export const textoCorto = (a: Asiento, etiquetas: Etiquetas) =>
  `${a.mesa === MESA_PRINCIPAL ? 'MP' : `${etiquetas.u}${a.mesa}`} · ${etiquetas.a}${a.silla}`

/** La mesa principal de una conferencia es la mesa 0. */
export const MESA_PRINCIPAL = 0

/** Cuántos asientos tiene una mesa o fila: la principal tiene los suyos. */
export const sillasDe = (config: ConfigMesas, mesa: number) =>
  mesa === MESA_PRINCIPAL ? (config.mesaPrincipal ?? 0) : config.sillasPorMesa

/** Todos los asientos del salón, con la mesa principal. */
export const capacidadDe = (config: ConfigMesas) =>
  config.mesas * config.sillasPorMesa + (config.mesaPrincipal ?? 0)

/** Las mesas o filas que existen, con la principal primero si la hay. */
export const unidadesDe = (config: ConfigMesas) => [
  ...((config.mesaPrincipal ?? 0) > 0 ? [MESA_PRINCIPAL] : []),
  ...Array.from({ length: config.mesas }, (_, i) => i + 1),
]

export const esReservada = (config: ConfigMesas, mesa: number) =>
  mesa === MESA_PRINCIPAL ||
  (config.modo !== 'libre' && config.vip
    ? config.vip.includes(mesa)
    : mesa <= (config.reservadas ?? 0))

/** Los números de las mesas o filas VIP, sin la principal. */
export const unidadesVip = (config: ConfigMesas) =>
  Array.from({ length: config.mesas }, (_, i) => i + 1).filter((m) => esReservada(config, m))

/** Si alguna mesa o fila del salón es VIP. */
export const hayVip = (config: ConfigMesas) =>
  Array.from({ length: config.mesas }, (_, i) => i + 1).some((m) => esReservada(config, m))

/** Máximo de asientos por mesa: redonda 12, taller 6; filas y herradura sin tope. */
export const maxSillasPorMesa = (dist: Distribucion | null) =>
  dist === 'mesas' ? 12 : dist === 'escuela' ? 6 : Infinity

/**
 * Sin asignación, pero con las primeras mesas o filas reservadas al frente: solo la zona de
 * protocolo tiene asientos; el resto del salón es de asiento libre.
 */
export const reservaEnLibre = (config: ConfigMesas) =>
  config.modo === 'libre' && ((config.reservadas ?? 0) > 0 || (config.mesaPrincipal ?? 0) > 0)

/** Si el evento asigna algún asiento: todo el salón, o solo la zona reservada al frente. */
export const asignaAsientos = (config: ConfigMesas) =>
  config.modo !== 'libre' || reservaEnLibre(config)

const configStore = createStore<Record<string, ConfigMesas>>('sorteo360.convocatorias.mesas', {})
const asientosStore = createStore<Record<string, Record<string, Asiento>>>(
  'sorteo360.convocatorias.asientos',
  {},
)

export function useMesasDeEvento(eventoId: string) {
  const configs = useSyncExternalStore(configStore.subscribe, configStore.get)
  const asientos = useSyncExternalStore(asientosStore.subscribe, asientosStore.get)
  return {
    config: configs[eventoId] ?? CONFIG_POR_DEFECTO,
    /** Si alguien ya configuró el evento; si no, la pantalla propone `configInicial`. */
    configurado: Boolean(configs[eventoId]),
    asientos: asientos[eventoId] ?? {},
  }
}

const libre = (asientos: Record<string, Asiento>, mesa: number, silla: number) =>
  !Object.values(asientos).some((a) => a.mesa === mesa && a.silla === silla)

/** Las sillas que ocupan en una mesa los de una empresa. */
const sillasDeEmpresa = (asientos: Record<string, Asiento>, mesa: number, empresa: string) =>
  Object.values(asientos)
    .filter((a) => a.mesa === mesa && empresa && a.empresa?.toLowerCase() === empresa.toLowerCase())
    .map((a) => a.silla)

/**
 * Las sillas libres de una mesa, de la más cercana a la más lejana de las dadas (las siguientes o
 * anteriores primero). Sin referencia, en orden. En una mesa redonda la última está junto a la 1.
 */
export function libresCercanas(
  asientos: Record<string, Asiento>,
  mesa: number,
  total: number,
  cerca: number[],
  circular = false,
): number[] {
  const libres = Array.from({ length: total }, (_, j) => j + 1).filter((s) =>
    libre(asientos, mesa, s),
  )
  if (cerca.length === 0) return libres
  const distancia = (a: number, b: number) => {
    const d = Math.abs(a - b)
    return circular ? Math.min(d, total - d) : d
  }
  return libres
    .map((s) => ({ s, d: Math.min(...cerca.map((c) => distancia(s, c))) }))
    .sort((x, y) => x.d - y.d || x.s - y.s)
    .map((x) => x.s)
}

const ocupacion = (asientos: Record<string, Asiento>, mesa: number) =>
  Object.values(asientos).filter((a) => a.mesa === mesa).length

/**
 * Sienta a los pendientes según el criterio, sin mover a nadie ya sentado. Quien no cabe queda
 * sin asiento: la pantalla avisa para que se agregue una mesa.
 */
export function planificarAsientos(
  config: ConfigMesas,
  actuales: Record<string, Asiento>,
  pendientes: Sentable[],
): Record<string, Asiento> {
  const asientos = { ...actuales }
  const orden = pendientes.toSorted((a, b) =>
    (a.fechaRegistro ?? '').localeCompare(b.fechaRegistro ?? ''),
  )
  const mesas = Array.from({ length: config.mesas }, (_, i) => i + 1)

  for (const p of orden) {
    const clave = claveDe(p.email)
    if (asientos[clave]) continue
    const empresa = p.empresa.trim()
    // La zona de protocolo es solo de autoridades y VIP: ellos la llenan primero y nadie más
    // entra ahí por reparto automático. Si se llena, siguen en las mesas generales.
    const protocolo = esProtocolo(p.categoria)
    const generalesDelSalon = mesas.filter((m) => !esReservada(config, m))
    const zonaProtocolo = protocolo ? mesas.filter((m) => esReservada(config, m)) : []
    // Con asiento libre fuera de la reserva, solo la zona de protocolo se asigna.
    let candidatas = config.modo === 'libre' ? [] : generalesDelSalon
    if (config.criterio === 'categoria') {
      // Primero las mesas de su categoría; quien no tiene categoría, o no cabe, va a las generales.
      const propias = generalesDelSalon.filter(
        (m) => p.categoria && config.categorias[m] === p.categoria,
      )
      const generales = generalesDelSalon.filter((m) => !config.categorias[m])
      candidatas = [...propias, ...generales]
    } else if (config.criterio === 'empresa' && empresa) {
      // Junto a los de su empresa si hay lugar; si no, en la mesa más vacía.
      const conSuEmpresa = generalesDelSalon.filter((m) =>
        Object.values(asientos).some((a) => a.mesa === m && a.empresa === empresa),
      )
      const masVacias = generalesDelSalon.toSorted(
        (a, b) => ocupacion(asientos, a) - ocupacion(asientos, b),
      )
      candidatas = [...conSuEmpresa, ...masVacias]
    }
    for (const mesa of [...zonaProtocolo, ...candidatas]) {
      // Junto a los de su empresa en esa mesa, si hay; si no, la primera libre.
      const silla = libresCercanas(
        asientos,
        mesa,
        config.sillasPorMesa,
        empresa ? sillasDeEmpresa(asientos, mesa, empresa) : [],
      )[0]
      if (silla) {
        asientos[clave] = { mesa, silla, empresa }
        break
      }
    }
  }
  return asientos
}

function escribirAsientos(eventoId: string, asientos: Record<string, Asiento>) {
  asientosStore.set((prev) => ({ ...prev, [eventoId]: asientos }))
}

const configDe = (eventoId: string) => configStore.get()[eventoId] ?? CONFIG_POR_DEFECTO
const asientosDe = (eventoId: string) => asientosStore.get()[eventoId] ?? {}

/** Los asientos que quedarían fuera si la capacidad baja a la de `config`. */
export function asientosFueraDeCapacidad(
  config: Pick<ConfigMesas, 'mesas' | 'sillasPorMesa' | 'mesaPrincipal'>,
  asientos: Record<string, Asiento>,
): string[] {
  return Object.entries(asientos)
    .filter(([, a]) =>
      a.mesa === MESA_PRINCIPAL
        ? a.silla > (config.mesaPrincipal ?? 0)
        : a.mesa > config.mesas || a.silla > config.sillasPorMesa,
    )
    .map(([clave]) => clave)
}

/**
 * Cambiar el modo conserva lo ya asignado; achicar mesas o sillas libera a quien quedó fuera.
 * En modo automático se sienta enseguida a los registrados que todavía no tienen silla.
 */
export function guardarConfigMesas(eventoId: string, config: ConfigMesas) {
  configStore.set((prev) => ({ ...prev, [eventoId]: config }))
  const fuera = new Set(asientosFueraDeCapacidad(config, asientosDe(eventoId)))
  escribirAsientos(
    eventoId,
    Object.fromEntries(Object.entries(asientosDe(eventoId)).filter(([clave]) => !fuera.has(clave))),
  )
}

/** "Avisar y crear mesa": suma una mesa o fila más al salón. */
export function agregarMesa(eventoId: string) {
  const config = configDe(eventoId)
  guardarConfigMesas(eventoId, { ...config, mesas: config.mesas + 1 })
}

/** Borra todas las sillas y las vuelve a calcular con el criterio actual. */
export function reasignarTodo(eventoId: string, registrados: Sentable[]) {
  escribirAsientos(eventoId, planificarAsientos(configDe(eventoId), {}, registrados))
}

/**
 * Sienta a una persona en una silla concreta, o la levanta con `null`. Si la silla está
 * ocupada y la persona ya tenía una, intercambian; si no tenía, la silla ocupada se rechaza.
 */
export function asignarAsiento(
  eventoId: string,
  persona: { email: string; empresa?: string },
  asiento: Pick<Asiento, 'mesa' | 'silla'> | null,
) {
  const actuales = { ...asientosDe(eventoId) }
  const clave = claveDe(persona.email)
  const anterior = actuales[clave]
  if (!asiento) {
    delete actuales[clave]
    escribirAsientos(eventoId, actuales)
    return
  }
  const ocupante = Object.entries(actuales).find(
    ([k, a]) => k !== clave && a.mesa === asiento.mesa && a.silla === asiento.silla,
  )
  if (ocupante) {
    if (!anterior) throw new Error('Esa silla ya está ocupada.')
    actuales[ocupante[0]] = { ...ocupante[1], mesa: anterior.mesa, silla: anterior.silla }
  }
  actuales[clave] = { ...asiento, empresa: persona.empresa?.trim() || anterior?.empresa }
  escribirAsientos(eventoId, actuales)
}

/** Un registro nuevo desde el portal: en un evento automático recibe su silla al registrarse. */
export function sentarSiEsAutomatico(eventoId: string, registrado: Sentable) {
  const config = configStore.get()[eventoId]
  if (!config || (config.modo !== 'automatica' && !reservaEnLibre(config))) return
  escribirAsientos(eventoId, planificarAsientos(config, asientosDe(eventoId), [registrado]))
}

/**
 * Quien deja de estar registrado (canceló o dijo que no asiste) suelta su silla. En automático,
 * quien no tiene asiento lo recibe al registrarse o, si ya estaba registrado cuando se configuró,
 * al hacer check-in. Solo escribe si algo cambió.
 */
export function sincronizarAsientos(eventoId: string, registrados: Sentable[]) {
  const config = configDe(eventoId)
  if (!asignaAsientos(config)) return
  const actuales = asientosDe(eventoId)
  const vigentes = new Set(registrados.map((r) => claveDe(r.email)))
  const conservados = Object.fromEntries(
    Object.entries(actuales).filter(([clave]) => vigentes.has(clave)),
  )
  const siguientes = conservados
  const cambio =
    Object.keys(siguientes).length !== Object.keys(actuales).length ||
    Object.entries(siguientes).some(
      ([k, a]) => actuales[k]?.mesa !== a.mesa || actuales[k]?.silla !== a.silla,
    )
  if (cambio) escribirAsientos(eventoId, siguientes)
}

/**
 * La categoría para sentar por categoría sale del CRM: VIP o su primera clasificación.
 * shortcut: un invitado sin contacto en el CRM usa el tipo de la demo, hasta que la inscripción
 * traiga el `contactId` y su clasificación de crm-service.
 */
export function categoriaDeInvitado(
  invitado: Invitado,
  contactoPorCorreo: Map<string, { isVip?: boolean; classifications?: { name: string }[] }>,
): string | null {
  const contacto = contactoPorCorreo.get(claveDe(invitado.email))
  if (contacto?.isVip) return 'VIP'
  return contacto?.classifications?.[0]?.name ?? invitado.tipo ?? null
}

/**
 * Lo que el CRM decía de cada invitado (VIP, clasificación). Sin CRM no hay nada que cruzar y la
 * categoría sale solo del tipo del invitado.
 * shortcut: vacío hasta que el padrón de participantes traiga departamento/VIP; entonces leerlo de ahí.
 */
export const CONTACTOS_SIN_CRM: {
  email: string
  isVip?: boolean
  classifications?: { name: string }[]
}[] = []

/**
 * Los registrados de un evento y lo que el plan necesita de cada uno (con su categoría del
 * CRM). Mantiene los asientos al día: quien deja de estar registrado suelta su silla.
 */
export function useSentables(eventoId: string, invitados: Invitado[], listo = true) {
  const contactos = CONTACTOS_SIN_CRM
  const resultado = useMemo(() => {
    const porCorreo = new Map(contactos.map((c) => [claveDe(c.email), c]))
    const registrados = invitados.filter(puedeSentarse)
    const sentables: Sentable[] = registrados.map((i) => ({
      email: i.email,
      empresa: i.empresa,
      categoria: categoriaDeInvitado(i, porCorreo),
      fechaRegistro: i.fechaRegistro,
    }))
    return { registrados, sentables }
  }, [invitados, contactos])

  // Con la lista todavía cargando no se libera a nadie.
  useEffect(() => {
    if (listo && eventoId) sincronizarAsientos(eventoId, resultado.sentables)
  }, [eventoId, listo, resultado.sentables])

  return resultado
}

/** Las sillas libres de una mesa, en orden. */
export function sillasLibres(eventoId: string, mesa: number): number[] {
  const asientos = asientosDe(eventoId)
  return Array.from({ length: sillasDe(configDe(eventoId), mesa) }, (_, j) => j + 1).filter(
    (silla) => libre(asientos, mesa, silla),
  )
}

/** Los registrados de la misma empresa que todavía no tienen silla: a quién sugerir sentar junto. */
export function companerosSinMesa(
  persona: { email: string; empresa: string },
  registrados: Invitado[],
  asientos: Record<string, Asiento>,
): Invitado[] {
  const empresa = persona.empresa.trim().toLowerCase()
  if (!empresa) return []
  return registrados.filter(
    (r) =>
      puedeSentarse(r) &&
      claveDe(r.email) !== claveDe(persona.email) &&
      r.empresa.trim().toLowerCase() === empresa &&
      !asientos[claveDe(r.email)],
  )
}

/** Sienta a varias personas en las sillas libres de una mesa, hasta llenarla. */
export function sentarEnMesa(
  eventoId: string,
  mesa: number,
  personas: Invitado[],
  /** Mesa redonda: la última silla queda junto a la primera. */
  circular = false,
) {
  const actuales = { ...asientosDe(eventoId) }
  const total = sillasDe(configDe(eventoId), mesa)
  // Uno por uno, en la silla libre más cercana a los de su empresa ya sentados ahí, para que
  // queden seguidos y no en la primera libre de la mesa.
  for (const p of personas) {
    const empresa = p.empresa.trim()
    const silla = libresCercanas(
      actuales,
      mesa,
      total,
      sillasDeEmpresa(actuales, mesa, empresa),
      circular,
    )[0]
    if (!silla) break
    actuales[claveDe(p.email)] = { mesa, silla, empresa }
  }
  escribirAsientos(eventoId, actuales)
}

/**
 * Los que están sentados seguidos a una persona en su misma mesa o fila y comparten algo con
 * ella (la empresa o la categoría): el bloque contiguo, sin contarla a ella.
 */
export function grupoContiguo(
  asientos: Record<string, Asiento>,
  email: string,
  mismoGrupo: (clave: string) => boolean,
  total: number,
  circular = false,
): string[] {
  const propio = asientos[claveDe(email)]
  if (!propio) return []
  const enMesa = new Map(
    Object.entries(asientos)
      .filter(([, a]) => a.mesa === propio.mesa)
      .map(([k, a]) => [a.silla, k]),
  )
  const vecinos = (s: number) =>
    [s - 1, s + 1].map((v) => (circular ? ((v - 1 + total) % total) + 1 : v))
  const grupo = new Set<string>()
  const pendientes = [propio.silla]
  const vistos = new Set([propio.silla])
  while (pendientes.length) {
    const actual = pendientes.pop()!
    for (const v of vecinos(actual)) {
      const clave = enMesa.get(v)
      if (vistos.has(v) || !clave || !mismoGrupo(clave)) continue
      vistos.add(v)
      grupo.add(clave)
      pendientes.push(v)
    }
  }
  return [...grupo]
}

/**
 * Mueve a una persona y a su grupo: ella va al asiento elegido y los demás a los libres más
 * cercanos de esa mesa o fila. Si no caben, no mueve a nadie.
 */
export function moverGrupo(
  eventoId: string,
  claves: string[],
  destino: Pick<Asiento, 'mesa' | 'silla'>,
  circular = false,
) {
  const actuales = { ...asientosDe(eventoId) }
  const anteriores = claves.map((k) => [k, actuales[k]] as const)
  claves.forEach((k) => delete actuales[k])
  const total = sillasDe(configDe(eventoId), destino.mesa)
  if (!libre(actuales, destino.mesa, destino.silla)) {
    throw new Error('Ese asiento está ocupado: elige uno libre para mover al grupo.')
  }
  const libresEnMesa = Array.from({ length: total }, (_, j) => j + 1).filter((s) =>
    libre(actuales, destino.mesa, s),
  )
  if (libresEnMesa.length < claves.length) {
    throw new Error(`No hay ${claves.length} asientos libres en esa mesa o fila para el grupo.`)
  }
  const ocupadas: number[] = []
  anteriores.forEach(([k, previo], index) => {
    const silla =
      index === 0
        ? destino.silla
        : libresCercanas(actuales, destino.mesa, total, ocupadas, circular)[0]
    actuales[k] = { mesa: destino.mesa, silla, empresa: previo?.empresa }
    ocupadas.push(silla)
  })
  escribirAsientos(eventoId, actuales)
}

/** Levanta a todos de una mesa o fila, o de todo el salón con `null`. */
export function liberarAsientos(eventoId: string, mesa: number | null) {
  escribirAsientos(
    eventoId,
    mesa === null
      ? {}
      : Object.fromEntries(Object.entries(asientosDe(eventoId)).filter(([, a]) => a.mesa !== mesa)),
  )
}
