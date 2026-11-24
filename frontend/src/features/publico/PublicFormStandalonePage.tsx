import { useState, useId, FormEvent } from 'react'
import { CargoSelect } from '@/components/ui/CargoSelect'
import { useParams, Link } from 'react-router-dom'
import { useMutation } from '@tanstack/react-query'
import {
  Calendar,
  MapPin,
  CheckCircle2,
  ShieldCheck,
  Download,
  QrCode,
  Sparkles,
  Lock,
} from 'lucide-react'
import jsPDF from 'jspdf'
import { usePublicEvento } from './hooks'
import { fieldError, portalErrorMessage } from './portal/portal'
import { api, ApiError } from '@/api'
import { Button } from '@/components/ui/Button'
import { Spinner } from '@/components/ui/Feedback'

export function PublicFormStandalonePage() {
  const { id } = useParams<{ id: string }>()
  const eventId = id || '1'
  // Un visitante anónimo lee la cartelera de convocatorias, no el evento de events-service.
  const { data: evento, isLoading } = usePublicEvento(eventId)

  const [nombre, setNombre] = useState('')
  const [apellido, setApellido] = useState('')
  const [email, setEmail] = useState('')
  const [empresa, setEmpresa] = useState('')
  const [cargo, setCargo] = useState('')
  const [telefono, setTelefono] = useState('')
  const [pais, setPais] = useState('República Dominicana')
  // La clasificación del negocio no existe en el servicio: se queda en pantalla y no viaja.
  const [tipoEmpresa, setTipoEmpresa] = useState('Agencia de Viajes')

  // Sin token: convocatorias-service solo acepta la inscripción en un evento público y abierto.
  const mutation = useMutation({
    mutationFn: () =>
      api.register({
        eventId,
        firstName: nombre.trim(),
        lastName: apellido.trim(),
        email: email.trim(),
        phone: telefono.trim() || null,
        company: empresa.trim() || null,
        jobTitle: cargo.trim() || null,
        country: pais.trim() || null,
      }),
  })
  const submitted = mutation.isSuccess
  const ticketId = mutation.data?.reference ?? ''
  const nombreCompleto = `${nombre.trim()} ${apellido.trim()}`.trim()
  const err = mutation.error
  // Un 400 de campo se muestra bajo cada input; el aviso de abajo solo dice que algo falta.
  const esDeCampo = err instanceof ApiError && Object.keys(err.fieldErrors ?? {}).length > 0

  const nameInputId = useId()
  const lastNameInputId = useId()
  const emailInputId = useId()
  const companyInputId = useId()
  const jobInputId = useId()
  const phoneInputId = useId()
  const typeInputId = useId()
  const countryInputId = useId()

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault()
    if (!nombre.trim() || !apellido.trim() || !email.trim()) return

    mutation.mutate()
  }

  const handleDownloadPDF = () => {
    const doc = new jsPDF({ orientation: 'portrait', unit: 'mm', format: 'a5' })
    const primary: [number, number, number] = [4, 17, 77]
    const gold: [number, number, number] = [202, 138, 4]

    doc.setDrawColor(primary[0], primary[1], primary[2])
    doc.setLineWidth(1)
    doc.rect(8, 8, 132, 194)

    doc.setFont('helvetica', 'bold')
    doc.setFontSize(7)
    doc.setTextColor(100, 116, 139)
    doc.text('REPÚBLICA DOMINICANA', 74, 18, { align: 'center' })

    doc.setFontSize(12)
    doc.setTextColor(primary[0], primary[1], primary[2])
    doc.text('SORTEO360', 74, 24, { align: 'center' })

    doc.setFontSize(8)
    doc.setFont('helvetica', 'normal')
    doc.setTextColor(gold[0], gold[1], gold[2])
    doc.text('PASE OFICIAL DE ACCESO INSTITUCIONAL', 74, 29, { align: 'center' })

    doc.setDrawColor(gold[0], gold[1], gold[2])
    doc.setLineWidth(0.5)
    doc.line(18, 33, 130, 33)

    doc.setFont('helvetica', 'bold')
    doc.setFontSize(13)
    doc.setTextColor(primary[0], primary[1], primary[2])
    doc.text(nombreCompleto.toUpperCase(), 74, 46, { align: 'center' })

    doc.setFont('helvetica', 'normal')
    doc.setFontSize(9)
    doc.setTextColor(30, 41, 59)
    doc.text(`${cargo || 'Invitado'} • ${empresa || 'Entidad'}`, 74, 52, { align: 'center' })

    doc.setFillColor(248, 250, 252)
    doc.roundedRect(18, 58, 112, 22, 2, 2, 'F')
    doc.setFont('helvetica', 'bold')
    doc.setFontSize(8)
    doc.setTextColor(primary[0], primary[1], primary[2])
    doc.text(evento?.name || 'Evento', 22, 65)
    doc.setFont('helvetica', 'normal')
    doc.setFontSize(7.5)
    doc.setTextColor(100, 116, 139)
    const sedeTexto =
      evento?.cities && evento.cities.length > 0
        ? `${evento.cities[0].name}, ${evento.cities[0].country}`
        : 'República Dominicana'
    doc.text(`Sede: ${sedeTexto}`, 22, 70)
    doc.text(
      `Fecha: ${evento?.startDate ? new Date(evento.startDate).toLocaleDateString() : 'Próximamente'}`,
      22,
      75,
    )

    doc.setFont('courier', 'bold')
    doc.setFontSize(10)
    doc.setTextColor(primary[0], primary[1], primary[2])
    doc.text(ticketId, 74, 150, { align: 'center' })

    doc.save(`Pase_Acceso_${ticketId}.pdf`)
  }

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-slate-900 text-white">
        <Spinner />
      </div>
    )
  }

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100 flex flex-col justify-between selection:bg-blue-600 selection:text-white font-sans">
      {/* Navbar Minimalista Público */}
      <header className="border-b border-slate-800/80 bg-slate-900/80 backdrop-blur-md sticky top-0 z-40">
        <div className="max-w-4xl mx-auto px-4 py-3.5 flex items-center justify-between">
          <Link
            to="/"
            className="flex items-center gap-2.5 text-white hover:opacity-90 transition-opacity"
          >
            <div className="w-8 h-8 rounded-xl bg-linear-to-tr from-blue-600 to-indigo-600 flex items-center justify-center font-black text-sm">
              M
            </div>
            <div>
              <span className="font-black text-sm tracking-tight block">Sorteo360</span>
              <span className="text-[10px] text-slate-400 block -mt-0.5">
                Portal Oficial de Registro y Actualización
              </span>
            </div>
          </Link>

          <div className="flex items-center gap-1.5 text-xs text-emerald-400 font-bold bg-emerald-500/10 px-2.5 py-1 rounded-full border border-emerald-500/20">
            <Lock className="w-3.5 h-3.5" />
            <span>Canal Seguro Encriptado</span>
          </div>
        </div>
      </header>

      {/* Main Container */}
      <main className="max-w-3xl w-full mx-auto px-4 py-8 sm:py-12 flex-1">
        {!submitted ? (
          <div className="space-y-6 animate-in fade-in duration-300">
            {/* Banner de Contexto */}
            <div className="p-6 rounded-2xl bg-linear-to-br from-brand-deep via-slate-900 to-slate-900 border border-blue-800/40 shadow-xl relative overflow-hidden">
              <div className="space-y-2 relative z-10">
                <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[10px] font-black uppercase bg-blue-500/20 text-blue-300 border border-blue-400/30">
                  <Sparkles className="w-3 h-3" /> Formulario de registro
                </span>
                <h1 className="text-xl sm:text-2xl font-black text-white tracking-tight">
                  {evento?.name || 'Actualización de Datos & Registro Institucional'}
                </h1>
                <p className="text-xs sm:text-sm text-slate-300 leading-relaxed max-w-2xl">
                  {evento?.description ||
                    'Estimado agente de viajes y tour operador, por favor complete o verifique sus datos para asegurar su acreditación oficial y actualización en el directorio turístico.'}
                </p>

                {evento && (
                  <div className="flex flex-wrap items-center gap-4 pt-3 text-xs text-slate-300">
                    <div className="flex items-center gap-1.5">
                      <Calendar className="w-4 h-4 text-blue-400" />
                      <span>{new Date(evento.startDate).toLocaleDateString()}</span>
                    </div>
                    <div className="flex items-center gap-1.5">
                      <MapPin className="w-4 h-4 text-emerald-400" />
                      <span>
                        {evento.cities && evento.cities.length > 0
                          ? `${evento.cities[0].name}, ${evento.cities[0].country}`
                          : 'República Dominicana'}
                      </span>
                    </div>
                  </div>
                )}
              </div>
            </div>

            {/* Formulario */}
            <form
              onSubmit={handleSubmit}
              className="bg-slate-900/90 border border-slate-800 p-6 sm:p-8 rounded-2xl shadow-xl space-y-5"
            >
              <h2 className="text-base font-bold text-white border-b border-slate-800 pb-3 flex items-center gap-2">
                <ShieldCheck className="w-4 h-4 text-blue-400" />
                Datos de Contacto y Representación
              </h2>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                {/* Nombre y apellido comparten la celda: la fila de dos columnas no se mueve. */}
                <div className="grid grid-cols-2 gap-2">
                  <div className="space-y-1.5">
                    <label htmlFor={nameInputId} className="text-xs font-bold text-slate-300">
                      Nombre <span className="text-rose-400">*</span>
                    </label>
                    <input
                      id={nameInputId}
                      type="text"
                      required
                      value={nombre}
                      onChange={(e) => setNombre(e.target.value)}
                      placeholder="Ej: Carlos"
                      className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3.5 py-2.5 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
                    />
                    <ErrorDeCampo error={err} field="FirstName" />
                  </div>
                  <div className="space-y-1.5">
                    <label htmlFor={lastNameInputId} className="text-xs font-bold text-slate-300">
                      Apellido <span className="text-rose-400">*</span>
                    </label>
                    <input
                      id={lastNameInputId}
                      type="text"
                      required
                      value={apellido}
                      onChange={(e) => setApellido(e.target.value)}
                      placeholder="Ej: Mendoza"
                      className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3.5 py-2.5 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
                    />
                    <ErrorDeCampo error={err} field="LastName" />
                  </div>
                </div>

                <div className="space-y-1.5">
                  <label htmlFor={emailInputId} className="text-xs font-bold text-slate-300">
                    Correo Electrónico Corporativo <span className="text-rose-400">*</span>
                  </label>
                  <input
                    id={emailInputId}
                    type="email"
                    required
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    placeholder="carlos@agencia.com"
                    className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3.5 py-2.5 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
                  />
                  <ErrorDeCampo error={err} field="Email" />
                </div>

                <div className="space-y-1.5">
                  <label htmlFor={companyInputId} className="text-xs font-bold text-slate-300">
                    Nombre de la Empresa / Agencia <span className="text-rose-400">*</span>
                  </label>
                  <input
                    id={companyInputId}
                    type="text"
                    required
                    value={empresa}
                    onChange={(e) => setEmpresa(e.target.value)}
                    placeholder="Ej: Caribe Travel Tours"
                    className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3.5 py-2.5 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
                  />
                  <ErrorDeCampo error={err} field="Company" />
                </div>

                <div className="space-y-1.5">
                  <label htmlFor={jobInputId} className="text-xs font-bold text-slate-300">
                    Cargo / Posición
                  </label>
                  <CargoSelect
                    id={jobInputId}
                    value={cargo}
                    onChange={setCargo}
                    placeholder="Ej: Director Comercial"
                    aria-label="Cargo / Posición"
                    className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3.5 py-2.5 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
                  />
                  <ErrorDeCampo error={err} field="JobTitle" />
                </div>

                <div className="space-y-1.5">
                  <label htmlFor={phoneInputId} className="text-xs font-bold text-slate-300">
                    Teléfono / WhatsApp Directo
                  </label>
                  <input
                    id={phoneInputId}
                    type="tel"
                    value={telefono}
                    onChange={(e) => setTelefono(e.target.value)}
                    placeholder="+1 (809) 555-0199"
                    className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3.5 py-2.5 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
                  />
                  <ErrorDeCampo error={err} field="Phone" />
                </div>

                <div className="space-y-1.5">
                  <label htmlFor={typeInputId} className="text-xs font-bold text-slate-300">
                    Clasificación del Negocio
                  </label>
                  <select
                    id={typeInputId}
                    value={tipoEmpresa}
                    onChange={(e) => setTipoEmpresa(e.target.value)}
                    className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3.5 py-2.5 text-xs text-white focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
                  >
                    <option value="Agencia de Viajes">Agencia de Viajes</option>
                    <option value="Tour Operador">Tour Operador</option>
                    <option value="Cadena Hotelera">Cadena Hotelera / Resort</option>
                    <option value="Aerolínea">Aerolínea / Transporte</option>
                    <option value="DMC">DMC / Receptivo</option>
                    <option value="Prensa Especializada">Prensa / Medio Especializado</option>
                    <option value="Otro">Otro Sector Turístico</option>
                  </select>
                </div>

                <div className="space-y-1.5 sm:col-span-2">
                  <label htmlFor={countryInputId} className="text-xs font-bold text-slate-300">
                    País / Región Principal de Operación
                  </label>
                  <input
                    id={countryInputId}
                    type="text"
                    value={pais}
                    onChange={(e) => setPais(e.target.value)}
                    placeholder="República Dominicana, Estados Unidos, España, etc."
                    className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3.5 py-2.5 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
                  />
                  <ErrorDeCampo error={err} field="Country" />
                </div>
              </div>

              {err && (
                <p role="alert" className="text-xs font-bold text-rose-400">
                  {esDeCampo ? 'Revisa los datos marcados.' : portalErrorMessage(err)}
                </p>
              )}

              <div className="pt-4 border-t border-slate-800 flex flex-col sm:flex-row items-center justify-between gap-3">
                <p className="text-[11px] text-slate-400">
                  🔒 Sus datos se usan solo para gestionar su registro al evento.
                </p>
                <button
                  type="submit"
                  disabled={mutation.isPending}
                  className="w-full sm:w-auto px-6 py-3 rounded-xl bg-blue-600 hover:bg-blue-500 text-white font-bold text-xs shadow-lg shadow-blue-600/30 active:scale-95 transition-all cursor-pointer"
                >
                  Confirmar Registro & Sincronizar CRM
                </button>
              </div>
            </form>
          </div>
        ) : (
          /* Confirmación y Pase Generado */
          <div className="bg-slate-900/90 border border-slate-800 p-8 rounded-2xl shadow-2xl space-y-6 text-center animate-in zoom-in-95 duration-200">
            <div className="w-16 h-16 bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 rounded-2xl flex items-center justify-center mx-auto">
              <CheckCircle2 className="w-8 h-8" />
            </div>

            <div className="space-y-1">
              <h2 className="text-2xl font-black text-white">¡Registro Confirmado Exitosamente!</h2>
              <p className="text-xs text-slate-300 max-w-md mx-auto">
                Sus datos han sido guardados y sincronizados con el CRM oficial. Su acreditación
                institucional ha sido generada.
              </p>
            </div>

            {/* Credencial Digital */}
            <div className="max-w-md mx-auto bg-white text-slate-900 p-6 rounded-2xl shadow-xl border-4 border-brand-deep text-left space-y-4">
              <div className="flex items-center justify-between border-b border-slate-200 pb-3">
                <div>
                  <span className="text-[9px] font-black uppercase text-slate-400 tracking-wider block">
                    República Dominicana
                  </span>
                  <span className="text-xs font-black text-brand-deep">SORTEO360</span>
                </div>
                <span className="px-2 py-0.5 rounded-full text-[9px] font-bold bg-blue-100 text-blue-800 border border-blue-200">
                  PASE VIP OFICIAL
                </span>
              </div>

              <div>
                <span className="text-[10px] text-slate-400 uppercase font-bold block">
                  Acreditado(a)
                </span>
                <p className="text-lg font-black text-brand-deep">{nombreCompleto}</p>
                <p className="text-xs font-semibold text-slate-600">
                  {cargo || 'Representante'} • {empresa}
                </p>
              </div>

              <div className="p-3 bg-slate-50 rounded-xl border border-slate-200 flex items-center justify-between">
                <div>
                  <span className="text-[9px] font-bold text-slate-400 uppercase block">
                    Código de Acceso
                  </span>
                  <span className="font-mono text-xs font-bold text-blue-700">{ticketId}</span>
                </div>
                <QrCode className="w-10 h-10 text-slate-800" />
              </div>
            </div>

            <div className="flex flex-col sm:flex-row items-center justify-center gap-3 pt-2">
              <Button
                onClick={handleDownloadPDF}
                className="bg-emerald-600 hover:bg-emerald-700 text-white font-bold text-xs cursor-pointer"
              >
                <Download className="w-4 h-4 mr-1.5" />
                Descargar Pase en PDF
              </Button>
              <Link to="/publico/eventos">
                <Button variant="outline" className="text-xs cursor-pointer">
                  Explorar otros eventos
                </Button>
              </Link>
            </div>
          </div>
        )}
      </main>

      {/* Footer */}
      <footer className="border-t border-slate-800/80 bg-slate-900/60 py-4 text-center text-xs text-slate-500">
        © {new Date().getFullYear()} Sorteo360 • Plataforma Sorteo360
      </footer>
    </div>
  )
}

/** El error que el validator del servicio devolvió para un campo, bajo su input. */
function ErrorDeCampo({ error, field }: { error: unknown; field: string }) {
  const mensaje = fieldError(error, field)
  return mensaje ? <p className="text-[11px] text-rose-400">{mensaje}</p> : null
}

export default PublicFormStandalonePage
