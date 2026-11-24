import { useState } from 'react'
import { useParams } from 'react-router-dom'
import {
  Calendar,
  Building2,
  MapPin,
  Globe,
  Info,
  CheckCircle2,
  ExternalLink,
  Image as ImageIcon,
  X,
} from 'lucide-react'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { Spinner, ErrorNote } from '@/components/ui/Feedback'
import { Modal } from '@/components/ui/Modal'
import { formatDateRange } from '@/lib/format'
import { eventoTypeLabel, idiomaLabel } from '@/lib/labels'
import { coverImageFor, GALLERY_PHOTOS } from './coverImages'
import { RegistroEventoPublicoModal } from './RegistroEventoPublicoModal'
import { usePublicEvento } from './hooks'

// Diccionario multi-idioma para detalles del evento público
const UI_LANG: Record<
  string,
  {
    badgeOfficial: string
    badgeConcluded: string
    aboutTitle: string
    /** Sin organizador (CRM caído) la frase no lo nombra, en vez de dejar un hueco. */
    aboutText: (opt: string | null) => string
    highlightsHeader: string
    highlightType: (type: string, opt: string | null) => string
    highlightSede: (sede: string) => string
    highlightLanguage: (lang: string) => string
    regStatusHeader: string
    regStatusOpen: string
    galleryTitle: string
    galleryPhotosCount: (n: number) => string
    galleryExpand: string
    locationTitle: string
    locationNotice: string
    sidebarTitle: string
    sidebarDate: string
    sidebarOrganizer: string
    sidebarVenue: string
    sidebarLanguage: string
    sidebarTbc: string
    regEnabled: string
    regClosed: string
    btnRegister: string
    btnNotActive: string
  }
> = {
  es: {
    badgeOfficial: 'Publicación oficial',
    badgeConcluded: 'Evento concluido',
    aboutTitle: 'Sobre este evento',
    aboutText: (opt) =>
      `Evento institucional de promoción turística${opt ? ` organizado por ${opt}` : ''} con la finalidad de fortalecer las relaciones comerciales, promover los destinos del país y conectar con socios estratégicos del sector.`,
    highlightsHeader: 'Lo que debes saber',
    highlightType: (type, opt) =>
      `Evento tipo ${eventoTypeLabel[type as keyof typeof eventoTypeLabel] || type}${opt ? `, organizado por ${opt}` : ''}.`,
    highlightSede: (sede) => `Se realizará en ${sede}.`,
    highlightLanguage: (lang) => `Idioma oficial: ${lang}.`,
    regStatusHeader: 'Estado de inscripción',
    regStatusOpen:
      'La convocatoria ha sido lanzada oficialmente. Haz clic en "Registrarme al evento" para confirmar tu asistencia.',
    galleryTitle: 'Galería de Imágenes',
    galleryPhotosCount: (n) => `${n} fotografías`,
    galleryExpand: 'Ampliar',
    locationTitle: 'Ubicación & Mapa del Venue',
    locationNotice: 'Mapa interactivo no configurado para este evento',
    sidebarTitle: 'Información del evento',
    sidebarDate: 'Fecha',
    sidebarOrganizer: 'Organizador',
    sidebarVenue: 'Sede',
    sidebarLanguage: 'Idioma',
    sidebarTbc: 'Por confirmar',
    regEnabled: 'Registro de invitados habilitado',
    regClosed: 'Este evento ya no admite registros.',
    btnRegister: 'Registrarme al evento',
    btnNotActive: 'Inscripción no disponible',
  },
  en: {
    badgeOfficial: 'Official Publication',
    badgeConcluded: 'Event Concluded',
    aboutTitle: 'About this Event',
    aboutText: (opt) => `Event${opt ? ` organized by ${opt}` : ''}.`,
    highlightsHeader: 'Key Information',
    highlightType: (type, opt) => `Event type: ${type}${opt ? `, organized by ${opt}` : ''}.`,
    highlightSede: (sede) => `Held in ${sede}.`,
    highlightLanguage: (lang) => `Official language: ${lang}.`,
    regStatusHeader: 'Registration Status',
    regStatusOpen:
      'Official registration is now open. Click "Register for event" to confirm your attendance.',
    galleryTitle: 'Photo Gallery',
    galleryPhotosCount: (n) => `${n} photos`,
    galleryExpand: 'Enlarge',
    locationTitle: 'Location & Venue Map',
    locationNotice: 'Interactive map not configured for this event',
    sidebarTitle: 'Event Information',
    sidebarDate: 'Date',
    sidebarOrganizer: 'Organizer',
    sidebarVenue: 'Venue',
    sidebarLanguage: 'Language',
    sidebarTbc: 'To be confirmed',
    regEnabled: 'Guest registration enabled',
    regClosed: 'Registration for this event is closed.',
    btnRegister: 'Register for event',
    btnNotActive: 'Registration Unavailable',
  },
  fr: {
    badgeOfficial: 'Publication Officielle',
    badgeConcluded: 'Événement Terminé',
    aboutTitle: 'À propos de cet événement',
    aboutText: (opt) =>
      `Événement institutionnel de promotion touristique${opt ? ` organisé par ${opt}` : ''} visant à renforcer les relations commerciales et promouvoir les destinations nationales.`,
    highlightsHeader: 'Informations Clés',
    highlightType: (type, opt) => `Événement de type ${type}${opt ? `, organisé par ${opt}` : ''}.`,
    highlightSede: (sede) => `Se déroulera à ${sede}.`,
    highlightLanguage: (lang) => `Langue officielle: ${lang}.`,
    regStatusHeader: "Statut de l'inscription",
    regStatusOpen:
      "L'inscription officielle est ouverte. Cliquez sur \"S'inscrire à l'événement\" pour confirmer votre présence.",
    galleryTitle: "Galerie d'Images",
    galleryPhotosCount: (n) => `${n} photos`,
    galleryExpand: 'Agrandir',
    locationTitle: 'Emplacement & Carte du Lieu',
    locationNotice: 'Carte interactive non configurée pour cet événement',
    sidebarTitle: "Informations sur l'événement",
    sidebarDate: 'Date',
    sidebarOrganizer: 'Organisateur',
    sidebarVenue: 'Lieu',
    sidebarLanguage: 'Langue',
    sidebarTbc: 'À confirmer',
    regEnabled: 'Inscription des invités activée',
    regClosed: "Cet événement n'accepte plus d'inscriptions.",
    btnRegister: "S'inscrire à l'événement",
    btnNotActive: 'Inscription indisponible',
  },
  pt: {
    badgeOfficial: 'Publicação Oficial',
    badgeConcluded: 'Evento Concluído',
    aboutTitle: 'Sobre este evento',
    aboutText: (opt) =>
      `Evento institucional de promoção turística${opt ? ` organizado por ${opt}` : ''} com o objetivo de fortalecer as relações comerciais e promover os destinos do país.`,
    highlightsHeader: 'Informações importantes',
    highlightType: (type, opt) => `Evento do tipo ${type}${opt ? `, organizado por ${opt}` : ''}.`,
    highlightSede: (sede) => `Será realizado em ${sede}.`,
    highlightLanguage: (lang) => `Idioma oficial: ${lang}.`,
    regStatusHeader: 'Status da inscrição',
    regStatusOpen:
      'A convocatória foi lançada oficialmente. Clique em "Registrar-se no evento" para confirmar sua presença.',
    galleryTitle: 'Galeria de Imagens',
    galleryPhotosCount: (n) => `${n} fotos`,
    galleryExpand: 'Ampliar',
    locationTitle: 'Localização & Mapa do Local',
    locationNotice: 'Mapa interativo não configurado para este evento',
    sidebarTitle: 'Informações do evento',
    sidebarDate: 'Data',
    sidebarOrganizer: 'Organizador',
    sidebarVenue: 'Local',
    sidebarLanguage: 'Idioma',
    sidebarTbc: 'A confirmar',
    regEnabled: 'Inscrição de convidados habilitada',
    regClosed: 'Este evento não aceita mais inscrições.',
    btnRegister: 'Registrar-se no evento',
    btnNotActive: 'Inscrição indisponível',
  },
  de: {
    badgeOfficial: 'Offizielle Publikation',
    badgeConcluded: 'Veranstaltung Beendet',
    aboutTitle: 'Über diese Veranstaltung',
    aboutText: (opt) =>
      `Offizielle Tourismusförderungsveranstaltung${opt ? `, organisiert von ${opt}` : ''}.`,
    highlightsHeader: 'Wichtige Informationen',
    highlightType: (type, opt) =>
      `Veranstaltungstyp: ${type}${opt ? `, organisiert von ${opt}` : ''}.`,
    highlightSede: (sede) => `Ort: ${sede}.`,
    highlightLanguage: (lang) => `Offizielle Sprache: ${lang}.`,
    regStatusHeader: 'Anmeldestatus',
    regStatusOpen:
      'Die Anmeldephase ist eröffnet. Klicken Sie auf "Anmelden", um Ihre Teilnahme zu bestätigen.',
    galleryTitle: 'Bildergalerie',
    galleryPhotosCount: (n) => `${n} Fotos`,
    galleryExpand: 'Vergrößern',
    locationTitle: 'Veranstaltungsort & Karte',
    locationNotice: 'Interaktive Karte für diese Veranstaltung nicht konfiguriert',
    sidebarTitle: 'Veranstaltungsinformationen',
    sidebarDate: 'Datum',
    sidebarOrganizer: 'Veranstalter',
    sidebarVenue: 'Ort',
    sidebarLanguage: 'Sprache',
    sidebarTbc: 'Zu bestätigen',
    regEnabled: 'Gästeanmeldung aktiviert',
    regClosed: 'Anmeldung für diese Veranstaltung geschlossen.',
    btnRegister: 'Anmelden zur Veranstaltung',
    btnNotActive: 'Anmeldung nicht verfügbar',
  },
  it: {
    badgeOfficial: 'Pubblicazione Ufficiale',
    badgeConcluded: 'Evento Concluso',
    aboutTitle: "Informazioni sull'evento",
    aboutText: (opt) =>
      `Evento istituzionale di promozione turistica${opt ? ` organizzato da ${opt}` : ''}.`,
    highlightsHeader: 'Cosa devi sapere',
    highlightType: (type, opt) => `Evento tipo ${type}${opt ? `, organizzato da ${opt}` : ''}.`,
    highlightSede: (sede) => `Si terrà a ${sede}.`,
    highlightLanguage: (lang) => `Lingua ufficiale: ${lang}.`,
    regStatusHeader: 'Stato di registrazione',
    regStatusOpen:
      'La registrazione è ufficialmente aperta. Clicca su "Registrati all\'evento" per confermare.',
    galleryTitle: 'Galleria Immagini',
    galleryPhotosCount: (n) => `${n} foto`,
    galleryExpand: 'Ingrandisci',
    locationTitle: 'Posizione & Mappa del Luogo',
    locationNotice: 'Mappa interattiva non configurata per questo evento',
    sidebarTitle: "Informazioni sull'evento",
    sidebarDate: 'Data',
    sidebarOrganizer: 'Organizzatore',
    sidebarVenue: 'Sede',
    sidebarLanguage: 'Lingua',
    sidebarTbc: 'Da confermare',
    regEnabled: 'Registrazione ospiti abilitata',
    regClosed: 'Questo evento non accetta più registrazioni.',
    btnRegister: "Registrati all'evento",
    btnNotActive: 'Registrazione non disponible',
  },
}

export function PublicEventoDetailPage() {
  const { id = '' } = useParams()
  // La lectura anónima de convocatorias: un evento privado, cancelado o desconocido es un 404.
  const { data: evento, isLoading, error } = usePublicEvento(id)
  const [selectedPhoto, setSelectedPhoto] = useState<string | null>(null)
  const [openRegistroModal, setOpenRegistroModal] = useState(false)
  // Las imágenes del evento viven detrás de `/v1/files`, que es autenticado, y la cartelera
  // pública no las publica todavía: portada y galería son las fotos de archivo.
  const galleryPhotos = GALLERY_PHOTOS

  if (isLoading) return <Spinner label="Cargando publicación del evento..." />
  if (error || !evento) return <ErrorNote error={error ?? new Error('Evento no encontrado')} />

  const langCode = evento.language?.toLowerCase() || 'es'
  const t = UI_LANG[langCode] ?? UI_LANG.es

  // La cartelera no trae el estado: un cancelado ya fue un 404, y el detalle solo sirve un
  // evento cerrado después de su fecha de fin, así que esa fecha basta para rotularlo.
  // Fecha local (en-CA da AAAA-MM-DD): en UTC el rótulo cambiaría de día antes de medianoche.
  const isClosed = evento.endDate < new Date().toLocaleDateString('en-CA')
  // Sin fila de ventana, convocatorias la da por abierta, también para un evento ya terminado:
  // la pista sola ofrecería inscribirse a algo que ya pasó.
  const registroAbierto = evento.registrationOpen && !isClosed
  const sede = evento.cities[0]
  const organizer = evento.organizer ?? null
  const langName = idiomaLabel[evento.language] ?? evento.language

  const highlights = [
    t.highlightType(evento.type, organizer),
    t.highlightSede(sede ? `${sede.name}, ${sede.country}` : 'por confirmar'),
    t.highlightLanguage(langName),
  ]

  return (
    <div className="w-full">
      <div className="relative w-full h-[calc(100vh-40px)] min-h-125 overflow-hidden shadow-2xl flex flex-col justify-end p-6 sm:p-12 lg:p-16 border-b border-slate-800">
        <img
          src={coverImageFor(evento.type)}
          alt={evento.name}
          className="absolute inset-0 h-full w-full object-cover"
        />
        <div className="absolute inset-0 bg-linear-to-t from-brand-deep via-brand-deep/75 to-brand-deep/25" />

        <div className="relative z-10 mx-auto w-full max-w-7xl space-y-4 pb-4">
          <div className="flex flex-wrap items-center gap-2">
            <span className="rounded-md bg-white/95 px-3 py-1 text-xs font-bold text-brand-deep">
              {eventoTypeLabel[evento.type]}
            </span>
            {isClosed && (
              <span className="rounded-md bg-slate-800/90 px-3 py-1 text-xs font-extrabold uppercase tracking-wider text-white">
                {t.badgeConcluded}
              </span>
            )}
            {!isClosed && (
              <span className="inline-flex items-center gap-1.5 rounded-md bg-amber-500/90 px-3 py-1 text-xs font-extrabold uppercase tracking-wider text-white">
                {t.badgeOfficial}
              </span>
            )}
          </div>

          <h1 className="font-sans text-3xl sm:text-5xl lg:text-6xl font-extrabold text-white tracking-tight leading-tight max-w-4xl">
            {evento.name}
          </h1>

          <div className="flex flex-wrap gap-4 text-xs sm:text-base font-semibold text-blue-100 pt-1">
            <span className="inline-flex items-center gap-2">
              <Calendar size={18} className="text-amber-300" />{' '}
              {formatDateRange(evento.startDate, evento.endDate)}
            </span>
            {sede && (
              <span className="inline-flex items-center gap-2">
                <MapPin size={18} className="text-amber-300" /> {sede.name}, {sede.country}
              </span>
            )}
          </div>
        </div>
      </div>

      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8 py-8">
        <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
          <div className="space-y-6 lg:col-span-2">
            <Card className="p-6 bg-white dark:bg-slate-900 space-y-5">
              <div>
                <h2 className="font-sans text-lg font-bold text-slate-900 dark:text-white">
                  {t.aboutTitle}
                </h2>
                <p className="mt-2 font-body text-sm text-slate-600 leading-relaxed dark:text-slate-300 whitespace-pre-line">
                  {evento.description || t.aboutText(organizer)}
                </p>
              </div>

              <div className="space-y-2 border-t border-slate-100 pt-4 dark:border-slate-800">
                <h3 className="text-xs font-bold uppercase tracking-wider text-slate-400">
                  {t.highlightsHeader}
                </h3>
                <ul className="space-y-2">
                  {highlights.map((item) => (
                    <li
                      key={item}
                      className="flex items-start gap-2 text-sm text-slate-700 dark:text-slate-300"
                    >
                      <CheckCircle2
                        size={16}
                        className="mt-0.5 shrink-0 text-emerald-600 dark:text-emerald-400"
                      />
                      <span>{item}</span>
                    </li>
                  ))}
                </ul>
              </div>

              <div className="rounded-2xl border border-amber-200/80 bg-amber-50/80 p-5 dark:border-amber-900/60 dark:bg-amber-950/40">
                <div className="flex items-start gap-3">
                  <Info size={20} className="text-amber-700 shrink-0 mt-0.5 dark:text-amber-400" />
                  <div className="space-y-1">
                    <h3 className="font-sans text-sm font-bold text-amber-900 dark:text-amber-300">
                      {t.regStatusHeader}
                    </h3>
                    <p className="font-body text-xs text-amber-800 leading-relaxed dark:text-amber-300">
                      {registroAbierto ? t.regStatusOpen : t.regClosed}
                    </p>
                  </div>
                </div>
              </div>
            </Card>

            <Card className="p-6 bg-white dark:bg-slate-900 space-y-4">
              <div className="flex items-center justify-between">
                <h2 className="flex items-center gap-2 font-sans text-lg font-bold text-slate-900 dark:text-white">
                  <ImageIcon size={18} className="text-blue-600" /> {t.galleryTitle}
                </h2>
                <span className="text-xs font-semibold text-slate-400">
                  {t.galleryPhotosCount(galleryPhotos.length)}
                </span>
              </div>

              <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
                {galleryPhotos.map((src: string, idx: number) => (
                  <button
                    key={idx}
                    type="button"
                    onClick={() => setSelectedPhoto(src)}
                    className="aspect-square overflow-hidden rounded-xl border border-slate-200 dark:border-slate-800 cursor-pointer group relative"
                  >
                    <img
                      src={src}
                      alt={`Foto ${idx + 1}`}
                      className="h-full w-full object-cover transition-transform duration-300 group-hover:scale-110"
                    />
                    <div className="absolute inset-0 bg-black/30 opacity-0 group-hover:opacity-100 transition-opacity flex items-center justify-center text-white text-xs font-bold">
                      {t.galleryExpand}
                    </div>
                  </button>
                ))}
              </div>
            </Card>

            <Card className="p-6 bg-white dark:bg-slate-900 space-y-4">
              <div className="flex items-center justify-between">
                <h2 className="flex items-center gap-2 font-sans text-lg font-bold text-slate-900 dark:text-white">
                  <MapPin size={18} className="text-rose-600" /> {t.locationTitle}
                </h2>
                <span className="rounded-md bg-blue-50 px-2 py-1 text-[11px] font-extrabold text-blue-700 dark:bg-blue-950 dark:text-blue-300">
                  Google Maps
                </span>
              </div>

              <p className="text-sm font-semibold text-slate-700 dark:text-slate-300">
                {sede ? `${sede.name}, ${sede.country}` : t.sidebarTbc}
              </p>

              <div className="p-4 bg-slate-50 border border-slate-200/80 rounded-2xl text-xs text-slate-500 font-medium dark:bg-slate-800/50 dark:border-slate-700 flex items-center justify-center gap-2">
                <MapPin size={16} className="text-slate-400" />
                <span>{t.locationNotice}</span>
              </div>
            </Card>
          </div>

          <div className="space-y-6">
            <Card className="overflow-hidden bg-white dark:bg-slate-900 p-0">
              <div className="bg-brand-deep px-6 py-4">
                <h3 className="font-sans text-base font-bold text-white">{t.sidebarTitle}</h3>
              </div>
              <div className="space-y-3.5 p-6 text-xs">
                <div className="flex justify-between border-b border-slate-100 pb-2 dark:border-slate-800">
                  <span className="flex items-center gap-1.5 text-slate-500">
                    <Calendar size={13} /> {t.sidebarDate}
                  </span>
                  <span className="font-bold text-slate-900 dark:text-white">
                    {formatDateRange(evento.startDate, evento.endDate)}
                  </span>
                </div>

                {/* Sin nombre (CRM caído) el bloque no se dibuja: nunca un hueco ni un id. */}
                {organizer && (
                  <div className="flex justify-between border-b border-slate-100 pb-2 dark:border-slate-800">
                    <span className="flex items-center gap-1.5 text-slate-500">
                      <Building2 size={13} /> {t.sidebarOrganizer}
                    </span>
                    <span className="font-bold text-slate-900 dark:text-white">{organizer}</span>
                  </div>
                )}

                <div className="flex justify-between border-b border-slate-100 pb-2 dark:border-slate-800">
                  <span className="flex items-center gap-1.5 text-slate-500">
                    <MapPin size={13} /> {t.sidebarVenue}
                  </span>
                  <span className="font-bold text-slate-900 dark:text-white">
                    {sede ? `${sede.name}, ${sede.country}` : t.sidebarTbc}
                  </span>
                </div>

                <div className="flex justify-between">
                  <span className="flex items-center gap-1.5 text-slate-500">
                    <Globe size={13} /> {t.sidebarLanguage}
                  </span>
                  <span className="font-bold text-slate-900 dark:text-white">{langName}</span>
                </div>
              </div>
            </Card>

            <Card className="p-6 bg-white dark:bg-slate-900 space-y-3 text-center">
              {registroAbierto ? (
                <>
                  <p className="text-xs font-semibold text-emerald-700 dark:text-emerald-400">
                    {t.regEnabled}
                  </p>
                  <Button
                    type="button"
                    onClick={() => setOpenRegistroModal(true)}
                    className="w-full gap-2 rounded-xl bg-emerald-600 py-3 text-xs hover:bg-emerald-700"
                  >
                    {t.btnRegister} <ExternalLink size={14} />
                  </Button>
                </>
              ) : (
                <>
                  <p className="text-xs font-semibold text-slate-500 dark:text-slate-400">
                    {t.regClosed}
                  </p>
                  <Button
                    type="button"
                    disabled
                    variant="secondary"
                    className="w-full gap-2 rounded-xl py-3 text-xs"
                  >
                    {t.btnNotActive}
                  </Button>
                </>
              )}
            </Card>
          </div>
        </div>
      </div>

      {/* Modal de Registro al Evento con Whitelist & Código QR */}
      {openRegistroModal && (
        <RegistroEventoPublicoModal
          evento={evento}
          open={openRegistroModal}
          onClose={() => setOpenRegistroModal(false)}
        />
      )}

      {/* Modal Lightbox de Fotografía */}
      {selectedPhoto && (
        <Modal
          open={Boolean(selectedPhoto)}
          onClose={() => setSelectedPhoto(null)}
          width="max-w-4xl"
        >
          <div className="relative p-2 flex flex-col items-center">
            <button
              type="button"
              onClick={() => setSelectedPhoto(null)}
              className="absolute top-4 right-4 p-2 bg-slate-900/80 text-white rounded-full hover:bg-slate-900 transition-colors cursor-pointer z-10"
            >
              <X size={18} />
            </button>
            <img
              src={selectedPhoto}
              alt="Ampliada"
              className="max-h-[80vh] w-auto rounded-2xl object-contain shadow-2xl"
            />
          </div>
        </Modal>
      )}
    </div>
  )
}
