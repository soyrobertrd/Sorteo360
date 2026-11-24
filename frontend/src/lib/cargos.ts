/**
 * La lista de cargos de toda la app: la usan todos los formularios que preguntan un cargo
 * (`CargoSelect`), para que ofrezcan los mismos valores. Está por idioma porque los
 * formularios públicos se muestran en el idioma del evento; los internos usan español.
 *
 * No es un catálogo de ningún servicio: el cargo es texto libre en crm-service y en
 * convocatorias-service, así que viaja el rótulo elegido tal cual. "Otro" deja escribirlo
 * a mano. La lista es provisional: producto la va a definir.
 */
export const CARGOS: Record<string, { select: string; other: string; options: string[] }> = {
  es: {
    select: 'Selecciona tu cargo',
    other: 'Otro',
    options: [
      'Propietario / Socio',
      'Director General / CEO',
      'Director Comercial / Ventas',
      'Director de Marketing',
      'Gerente de Producto / Contratación',
      'Gerente de Operaciones',
      'Agente de Viajes',
      'Ejecutivo de Ventas',
      'Coordinador de Eventos / MICE',
      'Periodista / Prensa',
      'Funcionario Público',
    ],
  },
  en: {
    select: 'Select your job title',
    other: 'Other',
    options: [
      'Owner / Partner',
      'Managing Director / CEO',
      'Sales Director',
      'Marketing Director',
      'Product / Contracting Manager',
      'Operations Manager',
      'Travel Agent',
      'Sales Executive',
      'Events / MICE Coordinator',
      'Journalist / Press',
      'Public Official',
    ],
  },
  fr: {
    select: 'Sélectionnez votre poste',
    other: 'Autre',
    options: [
      'Propriétaire / Associé',
      'Directeur Général / PDG',
      'Directeur Commercial',
      'Directeur Marketing',
      'Responsable Produit / Achats',
      'Responsable des Opérations',
      'Agent de Voyages',
      'Chargé de Ventes',
      'Coordinateur Événements / MICE',
      'Journaliste / Presse',
      'Fonctionnaire',
    ],
  },
  pt: {
    select: 'Selecione o seu cargo',
    other: 'Outro',
    options: [
      'Proprietário / Sócio',
      'Diretor Geral / CEO',
      'Diretor Comercial / Vendas',
      'Diretor de Marketing',
      'Gerente de Produto / Contratação',
      'Gerente de Operações',
      'Agente de Viagens',
      'Executivo de Vendas',
      'Coordenador de Eventos / MICE',
      'Jornalista / Imprensa',
      'Funcionário Público',
    ],
  },
  de: {
    select: 'Position auswählen',
    other: 'Sonstiges',
    options: [
      'Inhaber / Gesellschafter',
      'Geschäftsführer / CEO',
      'Vertriebsleiter',
      'Marketingleiter',
      'Produkt- / Einkaufsmanager',
      'Betriebsleiter',
      'Reiseberater',
      'Vertriebsmitarbeiter',
      'Event- / MICE-Koordinator',
      'Journalist / Presse',
      'Beamter',
    ],
  },
  it: {
    select: 'Seleziona il tuo ruolo',
    other: 'Altro',
    options: [
      'Proprietario / Socio',
      'Direttore Generale / CEO',
      'Direttore Commerciale / Vendite',
      'Direttore Marketing',
      'Responsabile Prodotto / Contrattazione',
      'Responsabile Operativo',
      'Agente di Viaggio',
      'Addetto alle Vendite',
      'Coordinatore Eventi / MICE',
      'Giornalista / Stampa',
      'Funzionario Pubblico',
    ],
  },
}

/** Los cargos de un idioma; uno que no esté en la lista cae a español. */
export const cargosDe = (lang?: string | null) => CARGOS[(lang ?? 'es').toLowerCase()] ?? CARGOS.es
