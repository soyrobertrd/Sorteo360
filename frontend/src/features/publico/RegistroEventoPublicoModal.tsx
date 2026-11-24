import { useState, FormEvent } from 'react'
import { useMutation } from '@tanstack/react-query'
import {
  CheckCircle2,
  AlertTriangle,
  ShieldCheck,
  Printer,
  Download,
  CalendarDays,
  MapPin,
  Loader2,
} from 'lucide-react'
import jsPDF from 'jspdf'
import { api, ApiError } from '@/api'
import type { PublicEventoDetail } from '@/api/types'
import { Modal } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { Callout } from '@/components/ui/Callout'
import { Card } from '@/components/ui/Card'
import { CargoSelect } from '@/components/ui/CargoSelect'
import { Field, TextInput } from '@/components/ui/Field'
import { config } from '@/lib/config'
import { formatDateRange } from '@/lib/format'
import { fieldError, portalErrorMessage } from './portal/portal'

interface RegistroEventoPublicoModalProps {
  evento: PublicEventoDetail
  open: boolean
  onClose: () => void
}

// Lista simulada de dominios/correos en whitelist institucional
const MOCK_WHITELIST = [
  'roberto.guzman@empresa.example.com',
  'yeny.then@empresa.example.com',
  'mercedes.castillo@empresa.example.com',
  'director.promocion@empresa.example.com',
]

const WHITELIST_DOMAINS = [
  '@empresa.example.com',
  '@tui.com',
  '@expedia.com',
  '@despegar.com',
  '@jetblue.com',
  '@americanairlines.com',
  '@booking.com',
  '@marriott.com',
  '@hilton.com',
]

const REG_LANG: Record<
  string,
  {
    title: string
    exclusiveWhitelist: string
    firstName: string
    placeholderFirstName: string
    lastName: string
    placeholderLastName: string
    email: string
    placeholderEmail: string
    company: string
    placeholderCompany: string
    jobTitle: string
    placeholderJobTitle: string
    phone: string
    placeholderPhone: string
    country: string
    btnSubmit: string
    successTitle: string
    successMsg: string
    ticketBadge: string
    btnDownload: string
    btnPrint: string
    btnClose: string
  }
> = {
  es: {
    title: 'Formulario de Inscripción al Evento',
    exclusiveWhitelist: 'Exclusivo con Whitelist',
    firstName: 'Nombre',
    placeholderFirstName: 'Ej: Juan',
    lastName: 'Apellido',
    placeholderLastName: 'Ej: Pérez',
    email: 'Correo Electrónico Corporativo',
    placeholderEmail: 'juan.perez@empresa.com',
    company: 'Empresa / Organización',
    placeholderCompany: 'Ej: Expedia Group, TUI Travel, etc.',
    jobTitle: 'Cargo / Función',
    placeholderJobTitle: 'Ej: Director de Compras Turísticas',
    phone: 'Teléfono de Contacto',
    placeholderPhone: '+1 (809) 555-0199',
    country: 'País de Origen / Delegación',
    btnSubmit: 'Completar Registro & Generar Pase VIP',
    successTitle: '¡Inscripción Confirmada Exitosamente!',
    successMsg:
      'Hemos registrado tu participación. Presenta este pase digital con tu código QR al ingresar a la sede.',
    ticketBadge: 'PASE DIGITAL DE ACCESO VIP',
    btnDownload: 'Descargar Boleto PDF',
    btnPrint: 'Imprimir Pase',
    btnClose: 'Cerrar y Finalizar',
  },
  en: {
    title: 'Event Registration Form',
    exclusiveWhitelist: 'Exclusive Whitelist Access',
    firstName: 'First Name',
    placeholderFirstName: 'John',
    lastName: 'Last Name',
    placeholderLastName: 'Smith',
    email: 'Corporate Email Address',
    placeholderEmail: 'john.smith@company.com',
    company: 'Company / Organization',
    placeholderCompany: 'Expedia Group, TUI Travel, etc.',
    jobTitle: 'Job Title / Position',
    placeholderJobTitle: 'VP of Tourism Procurement',
    phone: 'Contact Phone Number',
    placeholderPhone: '+1 (305) 555-0199',
    country: 'Country of Origin / Delegation',
    btnSubmit: 'Complete Registration & Generate VIP Pass',
    successTitle: 'Registration Successfully Confirmed!',
    successMsg:
      'Your registration has been processed. Present this digital ticket with QR code at the venue entrance.',
    ticketBadge: 'VIP DIGITAL ACCESS PASS',
    btnDownload: 'Download PDF Ticket',
    btnPrint: 'Print Ticket',
    btnClose: 'Close & Finish',
  },
  fr: {
    title: "Formulaire d'Inscription à l'Événement",
    exclusiveWhitelist: 'Accès Exclusif Liste Blanche',
    firstName: 'Prénom',
    placeholderFirstName: 'Jean',
    lastName: 'Nom',
    placeholderLastName: 'Dupont',
    email: 'Adresse E-mail Professionnelle',
    placeholderEmail: 'jean.dupont@entreprise.fr',
    company: 'Entreprise / Organisation',
    placeholderCompany: 'TUI France, Expedia, etc.',
    jobTitle: 'Poste / Fonction',
    placeholderJobTitle: 'Directeur Commercial',
    phone: 'Numéro de Téléphone',
    placeholderPhone: '+33 1 42 68 55 00',
    country: "Pays d'Origine / Délégation",
    btnSubmit: "Valider l'Inscription & Obtenir le Pass VIP",
    successTitle: 'Inscription Confirmée avec Succès !',
    successMsg:
      "Votre inscription est enregistrée. Présentez ce pass numérique avec code QR à l'entrée.",
    ticketBadge: "PASS D'ACCÈS NUMÉRIQUE VIP",
    btnDownload: 'Télécharger le Billet PDF',
    btnPrint: 'Imprimer le Pass',
    btnClose: 'Fermer',
  },
  pt: {
    title: 'Formulário de Inscrição no Evento',
    exclusiveWhitelist: 'Acesso Exclusivo Whitelist',
    firstName: 'Nome',
    placeholderFirstName: 'João',
    lastName: 'Sobrenome',
    placeholderLastName: 'Silva',
    email: 'E-mail Corporativo',
    placeholderEmail: 'joao.silva@empresa.com.br',
    company: 'Empresa / Organização',
    placeholderCompany: 'CVC Viagens, TUI, etc.',
    jobTitle: 'Cargo / Função',
    placeholderJobTitle: 'Diretor de Produto Turístico',
    phone: 'Telefone de Contato',
    placeholderPhone: '+55 11 98888-7777',
    country: 'País de Origem / Delegação',
    btnSubmit: 'Concluir Inscrição & Gerar Passaporte VIP',
    successTitle: 'Inscrição Confirmada com Sucesso!',
    successMsg:
      'Sua inscrição foi processada. Apresente este passe digital com código QR no evento.',
    ticketBadge: 'PASSE DE ACESSO DIGITAL VIP',
    btnDownload: 'Baixar Ingresso PDF',
    btnPrint: 'Imprimir Passe',
    btnClose: 'Fechar',
  },
  de: {
    title: 'Veranstaltungs-Anmeldeformular',
    exclusiveWhitelist: 'Exklusiver Whitelist-Zugang',
    firstName: 'Vorname',
    placeholderFirstName: 'Max',
    lastName: 'Nachname',
    placeholderLastName: 'Mustermann',
    email: 'Geschäftliche E-Mail-Adresse',
    placeholderEmail: 'max.mustermann@firma.de',
    company: 'Firma / Organisation',
    placeholderCompany: 'TUI Deutschland, Lufthansa, etc.',
    jobTitle: 'Position / Funktion',
    placeholderJobTitle: 'Einkaufsleiter Tourismus',
    phone: 'Telefonnummer',
    placeholderPhone: '+49 30 123456',
    country: 'Herkunftsland / Delegation',
    btnSubmit: 'Registrierung Abschließen & VIP-Pass Erstellen',
    successTitle: 'Registrierung Erfolgreich Bestätigt!',
    successMsg:
      'Ihre Anmeldung wurde erfasst. Zeigen Sie diesen digitalen Pass mit QR-Code am Eingang vor.',
    ticketBadge: 'DIGITALER VIP-ZUGANGSPASS',
    btnDownload: 'PDF-Ticket Herunterladen',
    btnPrint: 'Pass Drucken',
    btnClose: 'Schließen',
  },
  it: {
    title: "Modulo di Registrazione all'Evento",
    exclusiveWhitelist: 'Accesso Esclusivo Whitelist',
    firstName: 'Nome',
    placeholderFirstName: 'Mario',
    lastName: 'Cognome',
    placeholderLastName: 'Rossi',
    email: 'E-mail Aziendale',
    placeholderEmail: 'mario.rossi@azienda.it',
    company: 'Azienda / Organizzazione',
    placeholderCompany: 'Alpitour, Costa Crociere, etc.',
    jobTitle: 'Ruolo / Posizione',
    placeholderJobTitle: 'Direttore Commerciale',
    phone: 'Telefono di Contatto',
    placeholderPhone: '+39 06 1234567',
    country: 'Paese di Origine / Delegazione',
    btnSubmit: 'Completa Registrazione & Genera Pass VIP',
    successTitle: 'Registrazione Confermata con Successo!',
    successMsg:
      "La tua registrazione è stata completata. Presenta questo pass digitale con codice QR all'ingresso.",
    ticketBadge: 'PASS DIGITALE DI ACCESSO VIP',
    btnDownload: 'Scarica Biglietto PDF',
    btnPrint: 'Stampa Pass',
    btnClose: 'Chiudi',
  },
}

export function RegistroEventoPublicoModal({
  evento,
  open,
  onClose,
}: RegistroEventoPublicoModalProps) {
  const langKey = evento.language?.toLowerCase() || 'es'
  const t = REG_LANG[langKey] ?? REG_LANG.es

  const [nombre, setNombre] = useState('')
  const [apellido, setApellido] = useState('')
  const [email, setEmail] = useState('')
  const [empresa, setEmpresa] = useState('')
  const [cargo, setCargo] = useState('')
  const [telefono, setTelefono] = useState('')
  const [pais, setPais] = useState('Estados Unidos')

  const [whitelistError, setWhitelistError] = useState<string | null>(null)

  // La whitelist no existe en el servicio (spec convocatorias/06 §7.1): solo la demo la simula.
  const isWhitelist = config.apiMode !== 'http' && evento.registrationType === 'whitelist'

  // Sin token: convocatorias-service solo acepta la inscripción en un evento público y abierto.
  const mutation = useMutation({
    mutationFn: () =>
      api.register({
        eventId: evento.id,
        firstName: nombre.trim(),
        lastName: apellido.trim(),
        email: email.trim(),
        phone: telefono.trim() || null,
        company: empresa.trim() || null,
        jobTitle: cargo.trim() || null,
        country: pais.trim() || null,
      }),
  })
  const registro = mutation.data ?? null
  const submitted = registro !== null
  const regCode = registro?.reference ?? ''
  const nombreCompleto = `${nombre.trim()} ${apellido.trim()}`.trim()
  const err = mutation.error
  // Un 400 de campo se muestra sobre cada input; el aviso de abajo solo dice que algo falta.
  const esDeCampo = err instanceof ApiError && Object.keys(err.fieldErrors ?? {}).length > 0

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault()
    setWhitelistError(null)

    // Validar Whitelist si el evento está configurado como exclusivo
    if (isWhitelist) {
      const emailLower = email.trim().toLowerCase()
      const isApprovedEmail = MOCK_WHITELIST.includes(emailLower)
      const isApprovedDomain = WHITELIST_DOMAINS.some((domain) => emailLower.endsWith(domain))

      if (!isApprovedEmail && !isApprovedDomain) {
        setWhitelistError(
          'Acceso Denegado: Este evento es exclusivo con invitación previa (Whitelist). Tu dirección de correo no figura en la lista de invitados autorizados.',
        )
        return
      }
    }

    mutation.mutate()
  }

  const handleDownloadPdf = async () => {
    const doc = new jsPDF({ format: [148, 210] })
    doc.setFillColor(4, 17, 77)
    doc.rect(0, 0, 148, 40, 'F')
    doc.setTextColor(255, 255, 255)
    doc.setFontSize(9)
    doc.text('SORTEO360', 10, 12)
    doc.setFontSize(13)
    doc.text(evento.name, 10, 22, { maxWidth: 128 })
    doc.setFontSize(8)
    doc.text(t.ticketBadge, 10, 34)

    try {
      const res = await fetch(qrUrl)
      const blob = await res.blob()
      const dataUrl = await new Promise<string>((resolve, reject) => {
        const reader = new FileReader()
        reader.onloadend = () => resolve(reader.result as string)
        reader.onerror = reject
        reader.readAsDataURL(blob)
      })
      doc.addImage(dataUrl, 'PNG', 44, 48, 60, 60)
    } catch {
      // Sin la imagen del QR (o sin poder incrustarla): la referencia sigue legible en texto.
    }

    doc.setTextColor(20, 20, 20)
    doc.setFontSize(11)
    doc.text(regCode, 74, 118, { align: 'center' })
    doc.setFontSize(13)
    doc.text(nombreCompleto, 74, 128, { align: 'center' })
    doc.setFontSize(9)
    doc.text(`${cargo} · ${empresa}`, 74, 135, { align: 'center' })
    doc.setFontSize(8)
    doc.text(formatDateRange(evento.startDate, evento.endDate), 74, 145, { align: 'center' })
    if (sede) doc.text(`${sede.name}, ${sede.country}`, 74, 151, { align: 'center' })

    doc.save(`ticket-${regCode}.pdf`)
  }

  const handleResetAndClose = () => {
    mutation.reset()
    setWhitelistError(null)
    setNombre('')
    setApellido('')
    setEmail('')
    setEmpresa('')
    setCargo('')
    setTelefono('')
    onClose()
  }

  // La imagen la genera convocatorias-service a partir del qrCode; nada del invitado sale a
  // un tercero (spec convocatorias/06 §4.5).
  const qrUrl = registro ? api.ticketQrUrl(registro.qrCode) : ''

  const sede = evento.cities[0]

  if (!open) return null

  return (
    <Modal onClose={handleResetAndClose} title={t.title} width="max-w-2xl">
      {!submitted ? (
        <div className="space-y-5 p-1">
          {/* Cabecera: a qué evento se inscribe, cuándo y dónde */}
          <div className="rounded-2xl bg-linear-to-br from-brand-deep to-brand p-4 text-white shadow-2xs">
            <div className="flex flex-wrap items-start justify-between gap-2">
              <div className="min-w-0 space-y-1">
                <p className="text-[10px] font-black uppercase tracking-widest text-amber-300">
                  Sorteo360
                </p>
                <p className="font-sans text-base font-extrabold leading-snug">{evento.name}</p>
              </div>

              {isWhitelist && (
                <span className="inline-flex items-center gap-1 rounded-md bg-amber-400 px-2.5 py-0.5 text-[11px] font-black text-brand-deep">
                  <ShieldCheck size={13} /> {t.exclusiveWhitelist}
                </span>
              )}
            </div>

            <div className="mt-3 flex flex-wrap gap-x-5 gap-y-1.5 border-t border-white/15 pt-3 text-xs text-blue-100">
              <span className="inline-flex items-center gap-1.5">
                <CalendarDays size={14} className="shrink-0 text-amber-300" />
                {formatDateRange(evento.startDate, evento.endDate)}
              </span>
              {sede && (
                <span className="inline-flex items-center gap-1.5">
                  <MapPin size={14} className="shrink-0 text-amber-300" />
                  {sede.name}, {sede.country}
                </span>
              )}
            </div>
          </div>

          {whitelistError && (
            <div className="p-4 rounded-2xl bg-rose-50 border border-rose-200 text-rose-900 text-xs font-bold flex items-start gap-3 dark:bg-rose-950 dark:border-rose-900 dark:text-rose-300">
              <AlertTriangle size={20} className="text-rose-600 shrink-0 mt-0.5" />
              <div className="space-y-1">
                <p className="font-extrabold">Acceso restringido por Whitelist</p>
                <p className="font-normal text-rose-700 dark:text-rose-400">{whitelistError}</p>
                <p className="text-[11px] text-slate-500 dark:text-slate-400 pt-1">
                  Prueba registrándote con tu correo corporativo
                  {evento.organizer ? ` o contacta a la ${evento.organizer}` : ''}.
                </p>
              </div>
            </div>
          )}

          <form onSubmit={handleSubmit} className="space-y-5">
            {/* Tres filas de a dos: quién es, cómo se le contacta, a quién representa. */}
            <div className="grid grid-cols-1 gap-x-4 gap-y-4 sm:grid-cols-2">
              <Field label={t.firstName} required error={fieldError(err, 'FirstName')}>
                <TextInput
                  value={nombre}
                  onChange={(e) => setNombre(e.target.value)}
                  placeholder={t.placeholderFirstName}
                  autoComplete="given-name"
                  required
                />
              </Field>
              <Field label={t.lastName} required error={fieldError(err, 'LastName')}>
                <TextInput
                  value={apellido}
                  onChange={(e) => setApellido(e.target.value)}
                  placeholder={t.placeholderLastName}
                  autoComplete="family-name"
                  required
                />
              </Field>

              <Field label={t.email} required error={fieldError(err, 'Email')}>
                <TextInput
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder={t.placeholderEmail}
                  autoComplete="email"
                  required
                />
              </Field>

              <Field label={t.phone} required error={fieldError(err, 'Phone')}>
                <TextInput
                  type="tel"
                  value={telefono}
                  onChange={(e) => setTelefono(e.target.value)}
                  placeholder={t.placeholderPhone}
                  autoComplete="tel"
                  required
                />
              </Field>

              <Field label={t.company} required error={fieldError(err, 'Company')}>
                <TextInput
                  value={empresa}
                  onChange={(e) => setEmpresa(e.target.value)}
                  placeholder={t.placeholderCompany}
                  required
                />
              </Field>

              <Field label={t.jobTitle} required error={fieldError(err, 'JobTitle')}>
                <CargoSelect
                  value={cargo}
                  onChange={setCargo}
                  lang={langKey}
                  placeholder={t.placeholderJobTitle}
                  aria-label={t.jobTitle}
                  required
                />
              </Field>

              <div className="sm:col-span-2">
                <Field label={t.country} error={fieldError(err, 'Country')}>
                  <TextInput
                    value={pais}
                    onChange={(e) => setPais(e.target.value)}
                    placeholder="Estados Unidos"
                    autoComplete="country-name"
                  />
                </Field>
              </div>
            </div>

            {err && (
              <Callout tone="danger">
                {esDeCampo ? 'Revisa los datos marcados.' : portalErrorMessage(err)}
              </Callout>
            )}

            <div className="flex flex-col-reverse gap-3 border-t border-slate-100 pt-4 sm:flex-row sm:justify-end dark:border-slate-800">
              <Button
                type="button"
                variant="outline"
                onClick={handleResetAndClose}
                className="cursor-pointer"
              >
                Cancelar
              </Button>
              <Button
                type="submit"
                disabled={mutation.isPending}
                className="bg-brand-deep font-bold text-white hover:bg-brand-deep/90 cursor-pointer"
              >
                {mutation.isPending && <Loader2 size={15} className="mr-1.5 animate-spin" />}
                {t.btnSubmit}
              </Button>
            </div>
          </form>
        </div>
      ) : (
        /* Pantalla de Confirmación & Pase QR */
        <div className="space-y-6 p-2 text-center">
          <div className="mx-auto h-14 w-14 rounded-full bg-emerald-100 text-emerald-700 flex items-center justify-center dark:bg-emerald-950 dark:text-emerald-300">
            <CheckCircle2 size={32} />
          </div>

          <div className="space-y-1">
            <h2 className="font-sans text-2xl font-black text-slate-900 dark:text-white">
              {t.successTitle}
            </h2>
            <p className="text-xs text-slate-600 dark:text-slate-300 max-w-md mx-auto">
              {t.successMsg}
            </p>
          </div>

          {/* Tarjeta de Acreditación / Pase QR */}
          <Card className="max-w-md mx-auto p-6 bg-linear-to-b from-brand-deep to-brand text-white rounded-3xl shadow-xl space-y-4 border-2 border-amber-400/40 relative overflow-hidden">
            <div className="flex items-center justify-between border-b border-white/20 pb-3">
              <div className="text-left">
                <p className="text-[10px] font-black uppercase tracking-widest text-amber-300">
                  SORTEO360
                </p>
                <p className="font-extrabold text-sm text-white">{evento.name}</p>
              </div>
              <span className="rounded-md bg-amber-400 px-2 py-0.5 text-[10px] font-black text-brand-deep">
                {t.ticketBadge}
              </span>
            </div>

            <div className="bg-white p-3 rounded-2xl inline-block shadow-md mx-auto">
              <img src={qrUrl} alt="Código QR de Registro" className="h-44 w-44 mx-auto" />
            </div>

            <div className="space-y-1 text-center">
              <p className="text-xs font-mono font-bold tracking-widest text-amber-300">
                {regCode}
              </p>
              <p className="text-base font-extrabold text-white">{nombreCompleto}</p>
              <p className="text-xs text-blue-200">
                {cargo} · {empresa}
              </p>
            </div>

            <div className="pt-2 border-t border-white/10 text-[11px] text-blue-200 flex justify-between">
              <span>{formatDateRange(evento.startDate, evento.endDate)}</span>
              <span>{sede ? `${sede.name}, ${sede.country}` : 'Sede oficial'}</span>
            </div>
          </Card>

          <div className="flex flex-wrap items-center justify-center gap-3 pt-2">
            <Button
              type="button"
              onClick={handleDownloadPdf}
              className="bg-brand-deep hover:bg-brand-deep/90 text-white font-bold text-xs cursor-pointer"
            >
              <Download size={15} className="mr-1.5" /> {t.btnDownload}
            </Button>
            <Button
              type="button"
              variant="outline"
              onClick={() => window.print()}
              className="font-bold text-xs cursor-pointer"
            >
              <Printer size={15} className="mr-1.5" /> {t.btnPrint}
            </Button>
            <Button
              type="button"
              variant="outline"
              onClick={handleResetAndClose}
              className="text-xs font-bold cursor-pointer"
            >
              {t.btnClose}
            </Button>
          </div>
        </div>
      )}
    </Modal>
  )
}
