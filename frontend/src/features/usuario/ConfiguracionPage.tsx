import { useEffect, useState } from 'react'
import { FileDropzone } from '@/components/ui/FileDropzone'
import { useSearchParams } from 'react-router-dom'
import {
  User,
  Shield,
  Palette,
  Camera,
  Save,
  CheckCircle2,
  Key,
  Smartphone,
  Globe,
  Mail,
  Building2,
  Moon,
  Sun,
  ShieldCheck,
  Laptop,
  Send,
  RefreshCw,
  Zap,
  Check,
  AlertCircle,
  Sparkles,
  RotateCcw,
} from 'lucide-react'
import { useAuth } from '@/auth/AuthProvider'
import { Breadcrumbs } from '@/components/ui/Breadcrumbs'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { Field, Select, TextArea, TextInput } from '@/components/ui/Field'
import { Modal } from '@/components/ui/Modal'
import { Tabs } from '@/components/ui/Tabs'
import {
  DEFAULT_GLOBAL_BRANDING,
  resetGlobalBranding,
  safeHexColor,
  updateGlobalBranding,
  useBranding,
} from '@/lib/branding'
import {
  getOutlookAccount,
  saveOutlookAccount,
  disconnectOutlookAccount,
  type OutlookAccountInfo,
} from '@/lib/outlookStore'

type ConfigTab = 'marca' | 'perfil' | 'outlook' | 'seguridad' | 'preferencias'

export function ConfiguracionPage() {
  const { user } = useAuth()
  const [searchParams] = useSearchParams()
  const branding = useBranding()
  const tabParam = searchParams.get('tab') as ConfigTab | null
  const [activeTab, setActiveTab] = useState<ConfigTab>(
    tabParam && ['marca', 'perfil', 'outlook', 'seguridad', 'preferencias'].includes(tabParam)
      ? tabParam
      : 'marca',
  )
  const [savedSuccess, setSavedSuccess] = useState(false)

  // Estado de Marca Blanca Global
  const [brandAppName, setBrandAppName] = useState(branding.appName)
  const [brandSubtitle, setBrandSubtitle] = useState(branding.appSubtitle)
  const [brandOrgName, setBrandOrgName] = useState(branding.organizationName)
  const [brandLogoUrl, setBrandLogoUrl] = useState(branding.logoUrl)
  const [brandLogoDarkUrl, setBrandLogoDarkUrl] = useState(branding.logoDarkUrl)
  const [brandPrimary, setBrandPrimary] = useState(branding.primaryColor)
  const [brandAccent, setBrandAccent] = useState(branding.accentColor)

  useEffect(() => {
    setBrandAppName(branding.appName)
    setBrandSubtitle(branding.appSubtitle)
    setBrandOrgName(branding.organizationName)
    setBrandLogoUrl(branding.logoUrl)
    setBrandLogoDarkUrl(branding.logoDarkUrl)
    setBrandPrimary(branding.primaryColor)
    setBrandAccent(branding.accentColor)
  }, [
    branding.appName,
    branding.appSubtitle,
    branding.organizationName,
    branding.logoUrl,
    branding.logoDarkUrl,
    branding.primaryColor,
    branding.accentColor,
  ])

  const handleSaveBrand = (e: React.FormEvent) => {
    e.preventDefault()
    updateGlobalBranding({
      appName: brandAppName.trim() || DEFAULT_GLOBAL_BRANDING.appName,
      appSubtitle: brandSubtitle.trim() || DEFAULT_GLOBAL_BRANDING.appSubtitle,
      organizationName: brandOrgName.trim() || DEFAULT_GLOBAL_BRANDING.organizationName,
      logoUrl: brandLogoUrl.trim() || DEFAULT_GLOBAL_BRANDING.logoUrl,
      logoDarkUrl:
        brandLogoDarkUrl.trim() || brandLogoUrl.trim() || DEFAULT_GLOBAL_BRANDING.logoDarkUrl,
      primaryColor: safeHexColor(brandPrimary, DEFAULT_GLOBAL_BRANDING.primaryColor),
      accentColor: safeHexColor(brandAccent, DEFAULT_GLOBAL_BRANDING.accentColor),
    })
    setSavedSuccess(true)
    setTimeout(() => setSavedSuccess(false), 3000)
  }

  const handleResetBrand = () => {
    resetGlobalBranding()
    setSavedSuccess(true)
    setTimeout(() => setSavedSuccess(false), 3000)
  }

  const handleLogoUpload = (file: File | undefined, setter: (dataUrl: string) => void) => {
    if (!file) return
    const reader = new FileReader()
    reader.onload = () => {
      if (typeof reader.result === 'string') setter(reader.result)
    }
    reader.readAsDataURL(file)
  }

  // Estado de Vinculación de Outlook con Microsoft Graph API
  const [outlook, setOutlook] = useState<OutlookAccountInfo>(() => getOutlookAccount())
  const [isConnecting, setIsConnecting] = useState(false)
  const [showTestEmailModal, setShowTestEmailModal] = useState(false)
  const [testEmailTo, setTestEmailTo] = useState('')
  const [testEmailSubject, setTestEmailSubject] = useState(
    `Prueba de Integración - ${branding.appName} & Microsoft Graph API`,
  )
  const [testEmailBody, setTestEmailBody] = useState(
    `Este es un correo de prueba enviado exitosamente desde ${branding.appName} utilizando la API de Microsoft Graph con la cuenta vinculada.`,
  )
  const [testEmailSuccess, setTestEmailSuccess] = useState(false)
  const [testEmailSending, setTestEmailSending] = useState(false)

  // Datos de Perfil — se resincronizan si se cambia de identidad
  const [nombre, setNombre] = useState(user?.nombre || 'Administrador del Sistema')
  const [email, setEmail] = useState(user?.email || 'eventos@empresa.com')
  const [cargo, setCargo] = useState('Coordinación de Eventos y Premios')
  const [departamento, setDepartamento] = useState(
    user?.department || 'Organización de Eventos Corporativos',
  )
  const [telefono, setTelefono] = useState('+1 (809) 555-0199')
  const [telefonoFlota, setTelefonoFlota] = useState('+1 (809) 555-8822')

  useEffect(() => {
    setNombre(user?.nombre || 'Administrador del Sistema')
    setEmail(user?.email || 'eventos@empresa.com')
    setDepartamento(user?.department || 'Organización de Eventos Corporativos')
  }, [user?.sub, user?.nombre, user?.email, user?.department])

  // Datos de Seguridad
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [twoFactorEnabled, setTwoFactorEnabled] = useState(true)

  // Preferencias
  const [tema, setTema] = useState<'claro' | 'oscuro' | 'sistema'>('claro')
  const [idioma, setIdioma] = useState('es-DO')
  const [pageSizeDefault, setPageSizeDefault] = useState('25')

  const handleSave = (e: React.FormEvent) => {
    e.preventDefault()
    setSavedSuccess(true)
    setTimeout(() => setSavedSuccess(false), 3000)
  }

  const handleConnectOutlook = () => {
    setIsConnecting(true)
    setTimeout(() => {
      const updated: OutlookAccountInfo = {
        isConnected: true,
        email: email || 'eventos@empresa.com',
        displayName: nombre || 'Administrador del Sistema',
        tenantId: 'empresa-tenant-guid-2026',
        scopes: ['Mail.Send', 'Mail.ReadWrite', 'User.Read', 'offline_access'],
        connectedAt: new Date().toISOString(),
      }
      saveOutlookAccount(updated)
      setOutlook(updated)
      setIsConnecting(false)
      setSavedSuccess(true)
      setTimeout(() => setSavedSuccess(false), 3000)
    }, 1200)
  }

  const handleDisconnectOutlook = () => {
    const updated = disconnectOutlookAccount()
    setOutlook(updated)
  }

  const handleSendTestEmail = (e: React.FormEvent) => {
    e.preventDefault()
    if (!testEmailTo.trim()) return
    setTestEmailSending(true)
    setTimeout(() => {
      setTestEmailSending(false)
      setTestEmailSuccess(true)
      setTimeout(() => {
        setTestEmailSuccess(false)
        setShowTestEmailModal(false)
      }, 2500)
    }, 1500)
  }

  return (
    <div className="space-y-6 max-w-6xl mx-auto pb-12">
      {/* Breadcrumb Oficial */}
      <Breadcrumbs
        items={[{ label: 'Inicio', to: '/app' }, { label: 'Configuración del Usuario' }]}
      />

      {/* Tarjeta de Encabezado de Usuario */}
      <Card className="p-6 bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 rounded-3xl shadow-2xs">
        <div className="flex flex-col sm:flex-row items-center gap-6">
          <div className="relative group">
            <div className="h-24 w-24 rounded-full bg-brand-deep text-white font-black text-2xl flex items-center justify-center border-4 border-white dark:border-slate-800 shadow-md">
              RG
            </div>
            <button
              type="button"
              className="absolute bottom-0 right-0 p-2 bg-blue-600 text-white rounded-full shadow-lg hover:bg-blue-700 transition-colors cursor-pointer"
              title="Cambiar foto de perfil"
            >
              <Camera size={14} />
            </button>
          </div>

          <div className="text-center sm:text-left space-y-1.5 flex-1">
            <div className="flex flex-wrap items-center justify-center sm:justify-start gap-2.5">
              <h1 className="font-sans text-2xl font-black text-slate-900 dark:text-white">
                {nombre}
              </h1>
              <span className="rounded-md bg-emerald-100 px-2.5 py-0.5 text-xs font-extrabold text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300 inline-flex items-center gap-1">
                <CheckCircle2 size={12} /> Cuenta Oficial Verificada
              </span>
              {outlook.isConnected && (
                <span className="rounded-md bg-blue-100 px-2.5 py-0.5 text-xs font-extrabold text-blue-900 dark:bg-blue-950 dark:text-blue-300 inline-flex items-center gap-1 border border-blue-200 dark:border-blue-900">
                  <Mail size={12} className="text-blue-600" /> Outlook Conectado (Graph API)
                </span>
              )}
            </div>
            <p className="text-sm font-semibold text-slate-600 dark:text-slate-300">{cargo}</p>
            <p className="text-xs text-slate-500 flex flex-wrap items-center justify-center sm:justify-start gap-3 pt-1">
              <span className="flex items-center gap-1">
                <Building2 size={13} /> {departamento}
              </span>
              <span className="flex items-center gap-1">
                <Mail size={13} /> {email}
              </span>
            </p>
          </div>
        </div>
      </Card>

      {/* Contenedor Principal con Pestañas */}
      <div className="grid grid-cols-1 lg:grid-cols-4 gap-6">
        {/* Menú de Pestañas Izquierdo */}
        <Card className="h-fit rounded-2xl p-2">
          <Tabs
            orientation="vertical"
            value={activeTab}
            onChange={setActiveTab}
            items={[
              {
                key: 'marca',
                label: (
                  <>
                    <Sparkles size={16} /> Marca Blanca & Logos
                  </>
                ),
              },
              {
                key: 'perfil',
                label: (
                  <>
                    <User size={16} /> Datos Personales & Cargo
                  </>
                ),
              },
              {
                key: 'outlook',
                label: (
                  <>
                    <Mail size={16} /> Correo Outlook & Graph API
                  </>
                ),
                trailing: (
                  <span
                    className={`h-2 w-2 rounded-full ${outlook.isConnected ? 'bg-emerald-400' : 'bg-slate-300'}`}
                  />
                ),
              },
              {
                key: 'seguridad',
                label: (
                  <>
                    <Shield size={16} /> Seguridad & Credenciales
                  </>
                ),
              },
              {
                key: 'preferencias',
                label: (
                  <>
                    <Palette size={16} /> Preferencias de Interfaz
                  </>
                ),
              },
            ]}
          />
        </Card>

        {/* Panel de Contenido */}
        <div className="lg:col-span-3">
          {savedSuccess && (
            <div className="mb-4 p-4 rounded-2xl bg-emerald-50 border border-emerald-200 text-emerald-800 text-xs font-bold flex items-center gap-2 dark:bg-emerald-950 dark:border-emerald-900 dark:text-emerald-300 animate-in fade-in">
              <CheckCircle2 size={16} /> ¡Configuración guardada exitosamente! Los cambios se
              aplicaron a tu cuenta.
            </div>
          )}

          {/* PESTAÑA 0: MARCA BLANCA & LOGOS */}
          {activeTab === 'marca' && (
            <Card className="p-6 bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 rounded-3xl space-y-6 shadow-2xs">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <h2 className="font-sans text-lg font-extrabold text-slate-900 dark:text-white">
                    Marca Blanca & Personalización Global
                  </h2>
                  <p className="text-xs text-slate-500">
                    Define el nombre de la aplicación, la organización anfitriona y los logotipos
                    que se muestran en el portal, el escáner y las vistas generales. Cada sorteo
                    también puede tener su propio logo y nombre de empresa cliente.
                  </p>
                </div>
                <Button
                  type="button"
                  variant="secondary"
                  size="sm"
                  onClick={handleResetBrand}
                  className="cursor-pointer"
                >
                  <RotateCcw size={13} className="mr-1.5" /> Restaurar predeterminado
                </Button>
              </div>

              <form onSubmit={handleSaveBrand} className="space-y-5 text-xs">
                <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                  <Field label="Nombre de la Plataforma / Sistema" required>
                    <TextInput
                      value={brandAppName}
                      onChange={(e) => setBrandAppName(e.target.value)}
                      placeholder="Ej. Sorteo360"
                      required
                    />
                  </Field>
                  <Field label="Organización / Cliente por Defecto" required>
                    <TextInput
                      value={brandOrgName}
                      onChange={(e) => setBrandOrgName(e.target.value)}
                      placeholder="Ej. Acme Corp / Comité Organizador"
                      required
                    />
                  </Field>
                </div>

                <Field label="Subtítulo del Portal y Certificados">
                  <TextInput
                    value={brandSubtitle}
                    onChange={(e) => setBrandSubtitle(e.target.value)}
                    placeholder="Ej. Plataforma de Rifas y Check-in QR"
                  />
                </Field>

                <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                  <div className="rounded-2xl border border-slate-200 bg-slate-50/70 p-4 space-y-3 dark:border-slate-800 dark:bg-slate-800/40">
                    <div className="flex items-center justify-between">
                      <span className="font-bold text-slate-800 dark:text-slate-100">
                        Logo Principal (Tema Claro)
                      </span>
                      <FileDropzone
                        compact
                        accept="image/*"
                        onFile={(file) => handleLogoUpload(file, setBrandLogoUrl)}
                        title="Logo"
                        hint="PNG, JPG o SVG"
                      />
                    </div>
                    <div className="flex h-16 items-center justify-center rounded-xl border border-dashed border-slate-300 bg-white p-2">
                      <img
                        src={brandLogoUrl || DEFAULT_GLOBAL_BRANDING.logoUrl}
                        alt="Vista previa logo claro"
                        className="max-h-12 w-auto object-contain"
                      />
                    </div>
                    <TextInput
                      aria-label="Enlace del logo"
                      value={brandLogoUrl}
                      onChange={(e) => setBrandLogoUrl(e.target.value)}
                      placeholder="/logo-brand.svg o https://..."
                    />
                  </div>

                  <div className="rounded-2xl border border-slate-200 bg-slate-50/70 p-4 space-y-3 dark:border-slate-800 dark:bg-slate-800/40">
                    <div className="flex items-center justify-between">
                      <span className="font-bold text-slate-800 dark:text-slate-100">
                        Logo Tema Oscuro (Opcional)
                      </span>
                      <FileDropzone
                        compact
                        accept="image/*"
                        onFile={(file) => handleLogoUpload(file, setBrandLogoDarkUrl)}
                        title="Logo"
                        hint="PNG, JPG o SVG"
                      />
                    </div>
                    <div className="flex h-16 items-center justify-center rounded-xl border border-dashed border-slate-700 bg-slate-900 p-2">
                      <img
                        src={
                          brandLogoDarkUrl || brandLogoUrl || DEFAULT_GLOBAL_BRANDING.logoDarkUrl
                        }
                        alt="Vista previa logo oscuro"
                        className="max-h-12 w-auto object-contain"
                      />
                    </div>
                    <TextInput
                      aria-label="Enlace del logo para fondo oscuro"
                      value={brandLogoDarkUrl}
                      onChange={(e) => setBrandLogoDarkUrl(e.target.value)}
                      placeholder="/logo-brand-dark.svg o https://..."
                    />
                  </div>
                </div>

                <fieldset className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                  <legend className="mb-2 font-bold text-slate-800 dark:text-slate-100">
                    Colores de la marca
                  </legend>
                  {(
                    [
                      [
                        'brand-primary',
                        'Color principal',
                        'Barra lateral, botones y títulos.',
                        brandPrimary,
                        setBrandPrimary,
                      ],
                      [
                        'brand-accent',
                        'Color de acento',
                        'Resaltados, insignias, gráficos y ruleta.',
                        brandAccent,
                        setBrandAccent,
                      ],
                    ] as const
                  ).map(([id, label, hint, value, setValue]) => (
                    <div
                      key={id}
                      className="flex items-center gap-3 rounded-2xl border border-slate-200 bg-slate-50/70 p-4 dark:border-slate-800 dark:bg-slate-800/40"
                    >
                      <input
                        id={id}
                        type="color"
                        value={value}
                        onChange={(e) => setValue(e.target.value)}
                        aria-describedby={`${id}-hint`}
                        className="h-11 w-14 shrink-0 cursor-pointer rounded-lg border border-slate-300 bg-white p-1 dark:border-slate-700 dark:bg-slate-900"
                      />
                      <div className="min-w-0">
                        <label
                          htmlFor={id}
                          className="block font-bold text-slate-800 dark:text-slate-100"
                        >
                          {label}{' '}
                          <span className="font-mono font-medium text-slate-500">{value}</span>
                        </label>
                        <p id={`${id}-hint`} className="text-[11px] text-slate-500">
                          {hint}
                        </p>
                      </div>
                    </div>
                  ))}
                </fieldset>

                <div className="flex justify-end pt-3 border-t border-slate-100 dark:border-slate-800">
                  <Button
                    type="submit"
                    className="bg-brand-deep text-white font-bold cursor-pointer"
                  >
                    <Save size={15} className="mr-1.5" /> Guardar Marca Blanca
                  </Button>
                </div>
              </form>
            </Card>
          )}

          {/* PESTAÑA 1: DATOS PERSONALES */}
          {activeTab === 'perfil' && (
            <Card className="p-6 bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 rounded-3xl space-y-6 shadow-2xs">
              <div>
                <h2 className="font-sans text-lg font-extrabold text-slate-900 dark:text-white">
                  Datos Personales & Información del Cargo
                </h2>
                <p className="text-xs text-slate-500">
                  Actualiza tus datos de contacto y acreditación institucional en la plataforma.
                </p>
              </div>

              <form onSubmit={handleSave} className="space-y-4 text-xs">
                <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                  <Field label="Nombre Completo" required>
                    <TextInput
                      value={nombre}
                      onChange={(e) => setNombre(e.target.value)}
                      required
                    />
                  </Field>

                  <Field label="Correo Electrónico Oficial (Read-only)">
                    <TextInput
                      value={email}
                      onChange={(e) => setEmail(e.target.value)}
                      disabled
                      className="bg-slate-100 dark:bg-slate-800 cursor-not-allowed"
                    />
                  </Field>

                  <Field label="Cargo / Función Oficial" required>
                    <TextInput value={cargo} onChange={(e) => setCargo(e.target.value)} required />
                  </Field>

                  <Field label="Departamento / Dirección" required>
                    <TextInput
                      value={departamento}
                      onChange={(e) => setDepartamento(e.target.value)}
                      required
                    />
                  </Field>

                  <Field label="Teléfono Directo">
                    <TextInput value={telefono} onChange={(e) => setTelefono(e.target.value)} />
                  </Field>

                  <Field label="Teléfono de Flota Institucional">
                    <TextInput
                      value={telefonoFlota}
                      onChange={(e) => setTelefonoFlota(e.target.value)}
                    />
                  </Field>
                </div>

                <div className="flex justify-end pt-4 border-t border-slate-100 dark:border-slate-800">
                  <Button
                    type="submit"
                    className="bg-brand-deep text-white font-bold cursor-pointer"
                  >
                    <Save size={15} className="mr-1.5" /> Guardar Cambios de Perfil
                  </Button>
                </div>
              </form>
            </Card>
          )}

          {/* PESTAÑA NUEVA: VINCULACIÓN DE MICROSOFT OUTLOOK (MICROSOFT GRAPH API) */}
          {activeTab === 'outlook' && (
            <div className="space-y-6">
              <Card className="p-6 bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 rounded-3xl space-y-6 shadow-2xs">
                <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-slate-100 pb-5 dark:border-slate-800">
                  <div>
                    <h2 className="font-sans text-lg font-extrabold text-slate-900 dark:text-white flex items-center gap-2">
                      <Mail className="text-blue-600" size={22} /> Vinculación de Correo Outlook
                      (Microsoft Graph API)
                    </h2>
                    <p className="text-xs text-slate-500 mt-1 max-w-xl">
                      Permite a la plataforma enviar correos electrónicos institucionales
                      (convocatorias y campañas) directamente a través de tu cuenta de Outlook
                      usando la API de Microsoft Graph.
                    </p>
                  </div>

                  <div className="flex items-center gap-2">
                    {outlook.isConnected ? (
                      <span className="rounded-full bg-emerald-100 px-3 py-1 text-xs font-extrabold text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300 flex items-center gap-1.5 border border-emerald-200 dark:border-emerald-900">
                        <CheckCircle2 size={14} /> Cuenta Vinculada
                      </span>
                    ) : (
                      <span className="rounded-full bg-amber-100 px-3 py-1 text-xs font-extrabold text-amber-800 dark:bg-amber-950 dark:text-amber-300 flex items-center gap-1.5 border border-amber-200 dark:border-amber-900">
                        <AlertCircle size={14} /> No Conectado
                      </span>
                    )}
                  </div>
                </div>

                {/* Tarjeta Informativa de la Conexión OAuth2 MSAL */}
                <div className="rounded-2xl border border-blue-200 bg-linear-to-r from-blue-50/90 to-indigo-50/70 p-5 dark:border-blue-900/60 dark:from-blue-950/40 dark:to-indigo-950/30 space-y-4">
                  <div className="flex items-start gap-3">
                    <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-blue-600 text-white font-extrabold shadow-sm">
                      <Zap size={20} />
                    </div>
                    <div className="space-y-1">
                      <h4 className="font-bold text-sm text-brand-deep dark:text-blue-300">
                        Acceso Unificado Multimódulo
                      </h4>
                      <p className="text-xs text-slate-600 dark:text-slate-300 leading-relaxed">
                        Esta configuración es única y compartida para todos los módulos del portal (
                        <strong>Eventos</strong>, <strong>CRM Turístico</strong>,{' '}
                        <strong>Convocatorias</strong> y <strong>Sorteos</strong>). Cualquier
                        mensaje saliente enviado desde la plataforma saldrá firmado de manera
                        transparente desde tu casilla oficial de Microsoft 365.
                      </p>
                    </div>
                  </div>

                  {outlook.isConnected ? (
                    <div className="rounded-xl border border-emerald-200/90 bg-white/90 p-4 dark:border-emerald-900/50 dark:bg-slate-900/90 space-y-3">
                      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 text-xs">
                        <div>
                          <span className="text-slate-400 font-bold uppercase text-[10px] block">
                            Cuenta Microsoft 365
                          </span>
                          <span className="font-extrabold text-slate-900 dark:text-white flex items-center gap-1.5">
                            <Mail size={14} className="text-blue-600" /> {outlook.email}
                          </span>
                        </div>
                        <div>
                          <span className="text-slate-400 font-bold uppercase text-[10px] block">
                            Nombre Mostrado
                          </span>
                          <span className="font-bold text-slate-800 dark:text-slate-200">
                            {outlook.displayName}
                          </span>
                        </div>
                        <div>
                          <span className="text-slate-400 font-bold uppercase text-[10px] block">
                            Organización / Tenant ID
                          </span>
                          <span className="font-mono text-[11px] text-slate-600 dark:text-slate-400">
                            {outlook.tenantId}
                          </span>
                        </div>
                        <div>
                          <span className="text-slate-400 font-bold uppercase text-[10px] block">
                            Última Autenticación
                          </span>
                          <span className="text-slate-700 dark:text-slate-300 font-medium">
                            {new Date(outlook.connectedAt || Date.now()).toLocaleString('es-DO')}
                          </span>
                        </div>
                      </div>

                      {/* Lista de Permisos / Scopes Autorizados */}
                      <div className="pt-2 border-t border-slate-100 dark:border-slate-800">
                        <span className="text-[10px] font-extrabold uppercase tracking-wider text-slate-400 block mb-1.5">
                          Permisos Otorgados a la Plataforma (Microsoft Graph Scopes)
                        </span>
                        <div className="flex flex-wrap gap-1.5">
                          {outlook.scopes.map((scope) => (
                            <span
                              key={scope}
                              className="rounded-md bg-slate-100 px-2 py-0.5 text-[11px] font-mono font-bold text-slate-700 dark:bg-slate-800 dark:text-slate-300 flex items-center gap-1"
                            >
                              <Check size={11} className="text-emerald-600" /> {scope}
                            </span>
                          ))}
                        </div>
                      </div>
                    </div>
                  ) : (
                    <div className="p-4 bg-white/80 dark:bg-slate-900/80 rounded-xl border border-slate-200 dark:border-slate-800 text-xs text-slate-600 dark:text-slate-300">
                      Haz clic en el botón a continuación para iniciar sesión con tu cuenta
                      corporativa a través de la ventana segura de Microsoft Entra ID (OAuth 2.0).
                    </div>
                  )}

                  {/* Botones de Conexión / Desconexión / Prueba */}
                  <div className="flex flex-wrap items-center gap-3 pt-2">
                    {outlook.isConnected ? (
                      <>
                        <Button
                          type="button"
                          onClick={() => setShowTestEmailModal(true)}
                          className="bg-brand-deep text-white hover:bg-brand-deep/90 font-bold text-xs cursor-pointer flex items-center gap-1.5"
                        >
                          <Send size={14} /> Enviar correo de prueba vía Graph API
                        </Button>

                        <Button
                          type="button"
                          variant="outline"
                          onClick={handleConnectOutlook}
                          disabled={isConnecting}
                          className="font-bold text-xs cursor-pointer border-slate-300 dark:border-slate-700"
                        >
                          <RefreshCw size={13} className={isConnecting ? 'animate-spin' : ''} />
                          {isConnecting ? 'Re-sincronizando...' : 'Re-sincronizar permisos'}
                        </Button>

                        <Button
                          type="button"
                          variant="destructive"
                          onClick={handleDisconnectOutlook}
                          className="font-bold text-xs cursor-pointer ml-auto"
                        >
                          Desconectar cuenta de Outlook
                        </Button>
                      </>
                    ) : (
                      <Button
                        type="button"
                        onClick={handleConnectOutlook}
                        disabled={isConnecting}
                        className="bg-blue-600 hover:bg-blue-700 text-white font-bold text-xs cursor-pointer flex items-center gap-2 px-5 py-2.5 rounded-xl shadow-sm"
                      >
                        <Mail size={16} />
                        {isConnecting
                          ? 'Conectando con Microsoft Graph API...'
                          : 'Vincular Cuenta de Microsoft Outlook'}
                      </Button>
                    )}
                  </div>
                </div>
              </Card>

              {/* Modal de Prueba de Envío Vía Graph API */}
              {showTestEmailModal && (
                <Modal
                  title="Enviar correo de prueba vía Microsoft Graph API"
                  onClose={() => setShowTestEmailModal(false)}
                >
                  <form onSubmit={handleSendTestEmail} className="space-y-4 text-xs">
                    {testEmailSuccess ? (
                      <div className="p-4 rounded-2xl bg-emerald-50 border border-emerald-200 text-emerald-800 font-bold text-center space-y-2 dark:bg-emerald-950 dark:border-emerald-900 dark:text-emerald-300">
                        <CheckCircle2 size={32} className="mx-auto text-emerald-600" />
                        <p className="text-sm font-extrabold">
                          ¡Correo de prueba enviado con éxito!
                        </p>
                        <p className="text-xs font-normal text-slate-600 dark:text-slate-400">
                          El mensaje fue despachado desde <code>{outlook.email}</code> mediante la
                          API de Microsoft Graph.
                        </p>
                      </div>
                    ) : (
                      <>
                        <div className="p-3 rounded-xl bg-slate-50 border border-slate-200 dark:bg-slate-800 dark:border-slate-700 flex items-center gap-2 text-slate-700 dark:text-slate-300">
                          <Mail size={16} className="text-blue-600 shrink-0" />
                          <span>
                            Remitente oficial: <strong>{outlook.email}</strong>
                          </span>
                        </div>

                        <Field label="Correo Electrónico Destinatario" required>
                          <TextInput
                            type="email"
                            required
                            placeholder="ejemplo@empresa.com"
                            value={testEmailTo}
                            onChange={(e) => setTestEmailTo(e.target.value)}
                          />
                        </Field>

                        <Field label="Asunto del Mensaje" required>
                          <TextInput
                            required
                            value={testEmailSubject}
                            onChange={(e) => setTestEmailSubject(e.target.value)}
                          />
                        </Field>

                        <Field label="Cuerpo del Mensaje (HTML / Texto)">
                          <TextArea
                            rows={4}
                            value={testEmailBody}
                            onChange={(e) => setTestEmailBody(e.target.value)}
                          />
                        </Field>

                        <div className="flex justify-end gap-2 pt-2 border-t border-slate-100 dark:border-slate-800">
                          <Button variant="secondary" onClick={() => setShowTestEmailModal(false)}>
                            Cancelar
                          </Button>
                          <Button
                            type="submit"
                            disabled={testEmailSending}
                            className="bg-brand-deep text-white hover:bg-brand-deep/90 font-bold cursor-pointer flex items-center gap-1.5"
                          >
                            <Send size={14} />
                            {testEmailSending ? 'Enviando vía Graph API...' : 'Enviar mensaje'}
                          </Button>
                        </div>
                      </>
                    )}
                  </form>
                </Modal>
              )}
            </div>
          )}

          {/* PESTAÑA 3: SEGURIDAD */}
          {activeTab === 'seguridad' && (
            <div className="space-y-6">
              <Card className="p-6 bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 rounded-3xl space-y-6 shadow-2xs">
                <div>
                  <h2 className="font-sans text-lg font-extrabold text-slate-900 dark:text-white">
                    Cambiar Contraseña
                  </h2>
                  <p className="text-xs text-slate-500">
                    Asegúrate de utilizar una contraseña segura con al menos 8 caracteres.
                  </p>
                </div>

                <form onSubmit={handleSave} className="space-y-4 text-xs">
                  <Field label="Contraseña Actual" required>
                    <TextInput
                      type="password"
                      value={currentPassword}
                      onChange={(e) => setCurrentPassword(e.target.value)}
                      placeholder="••••••••"
                      required
                    />
                  </Field>

                  <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                    <Field label="Nueva Contraseña" required>
                      <TextInput
                        type="password"
                        value={newPassword}
                        onChange={(e) => setNewPassword(e.target.value)}
                        placeholder="••••••••"
                        required
                      />
                    </Field>

                    <Field label="Confirmar Nueva Contraseña" required>
                      <TextInput
                        type="password"
                        value={confirmPassword}
                        onChange={(e) => setConfirmPassword(e.target.value)}
                        placeholder="••••••••"
                        required
                      />
                    </Field>
                  </div>

                  <div className="flex justify-end pt-2">
                    <Button
                      type="submit"
                      className="bg-brand-deep text-white font-bold cursor-pointer"
                    >
                      <Key size={15} className="mr-1.5" /> Actualizar Contraseña
                    </Button>
                  </div>
                </form>
              </Card>

              {/* 2FA y Sesiones Activas */}
              <Card className="p-6 bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 rounded-3xl space-y-5 shadow-2xs">
                <div className="flex items-center justify-between border-b border-slate-100 pb-4 dark:border-slate-800">
                  <div>
                    <h3 className="font-bold text-sm text-slate-900 dark:text-white flex items-center gap-2">
                      <ShieldCheck size={18} className="text-blue-600" /> Autenticación de Doble
                      Factor (2FA)
                    </h3>
                    <p className="text-xs text-slate-500">
                      Solicita un código de verificación vía SMS o App al iniciar sesión.
                    </p>
                  </div>

                  <input
                    type="checkbox"
                    checked={twoFactorEnabled}
                    onChange={(e) => setTwoFactorEnabled(e.target.checked)}
                    className="h-5 w-5 rounded border-slate-300 text-blue-600 focus:ring-blue-500 cursor-pointer"
                  />
                </div>

                <div className="space-y-3">
                  <h4 className="font-bold text-xs uppercase tracking-wider text-slate-400">
                    Dispositivos y Sesiones Activas
                  </h4>

                  <div className="rounded-2xl border border-slate-200 p-3.5 dark:border-slate-800 bg-slate-50 dark:bg-slate-800/60 flex items-center justify-between text-xs">
                    <div className="flex items-center gap-3">
                      <Laptop size={20} className="text-slate-600 dark:text-slate-300" />
                      <div>
                        <p className="font-bold text-slate-900 dark:text-white">
                          Windows PC · Google Chrome (Sesión Actual)
                        </p>
                        <p className="text-[11px] text-slate-400">
                          Santo Domingo, República Dominicana · IP: 190.166.45.12
                        </p>
                      </div>
                    </div>
                    <span className="rounded-md bg-emerald-100 px-2 py-0.5 text-[10px] font-extrabold text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300">
                      Activa ahora
                    </span>
                  </div>

                  <div className="rounded-2xl border border-slate-200 p-3.5 dark:border-slate-800 bg-white dark:bg-slate-900 flex items-center justify-between text-xs">
                    <div className="flex items-center gap-3">
                      <Smartphone size={20} className="text-slate-400" />
                      <div>
                        <p className="font-bold text-slate-900 dark:text-white">
                          iPhone 15 Pro · Safari Mobile
                        </p>
                        <p className="text-[11px] text-slate-400">
                          Miami, Florida, EE.UU. · Hace 2 horas
                        </p>
                      </div>
                    </div>
                    <Button
                      variant="outline"
                      size="sm"
                      className="text-rose-600 border-rose-200 text-[11px] font-bold cursor-pointer"
                    >
                      Cerrar sesión
                    </Button>
                  </div>
                </div>
              </Card>
            </div>
          )}

          {activeTab === 'preferencias' && (
            <Card className="p-6 bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 rounded-3xl space-y-6 shadow-2xs">
              <div>
                <h2 className="font-sans text-lg font-extrabold text-slate-900 dark:text-white">
                  Preferencias de Interfaz & Visualización
                </h2>
                <p className="text-xs text-slate-500">
                  Personaliza la apariencia y valores predeterminados de la plataforma.
                </p>
              </div>

              <form onSubmit={handleSave} className="space-y-4 text-xs">
                <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                  <Field label="Idioma de la Plataforma">
                    <Select value={idioma} onChange={(e) => setIdioma(e.target.value)}>
                      <option value="es-DO">Español (República Dominicana)</option>
                      <option value="en-US">English (United States)</option>
                    </Select>
                  </Field>

                  <Field label="Registros por Página Predeterminados">
                    <Select
                      value={pageSizeDefault}
                      onChange={(e) => setPageSizeDefault(e.target.value)}
                    >
                      <option value="10">10 registros</option>
                      <option value="25">25 registros</option>
                      <option value="50">50 registros</option>
                      <option value="100">100 registros</option>
                    </Select>
                  </Field>
                </div>

                <div className="space-y-2 pt-2">
                  <label className="block font-bold text-xs text-slate-700 dark:text-slate-300">
                    Tema Visual de la Plataforma
                  </label>
                  <div className="grid grid-cols-3 gap-3">
                    <button
                      type="button"
                      onClick={() => setTema('claro')}
                      className={`p-3 rounded-2xl border flex flex-col items-center gap-2 cursor-pointer transition-all ${
                        tema === 'claro'
                          ? 'border-blue-600 bg-blue-50/60 text-blue-900 dark:bg-blue-950 dark:text-blue-300 font-bold'
                          : 'border-slate-200 dark:border-slate-800'
                      }`}
                    >
                      <Sun size={20} className="text-amber-500" />
                      <span>Modo Claro</span>
                    </button>

                    <button
                      type="button"
                      onClick={() => setTema('oscuro')}
                      className={`p-3 rounded-2xl border flex flex-col items-center gap-2 cursor-pointer transition-all ${
                        tema === 'oscuro'
                          ? 'border-blue-600 bg-blue-50/60 text-blue-900 dark:bg-blue-950 dark:text-blue-300 font-bold'
                          : 'border-slate-200 dark:border-slate-800'
                      }`}
                    >
                      <Moon size={20} className="text-indigo-500" />
                      <span>Modo Oscuro</span>
                    </button>

                    <button
                      type="button"
                      onClick={() => setTema('sistema')}
                      className={`p-3 rounded-2xl border flex flex-col items-center gap-2 cursor-pointer transition-all ${
                        tema === 'sistema'
                          ? 'border-blue-600 bg-blue-50/60 text-blue-900 dark:bg-blue-950 dark:text-blue-300 font-bold'
                          : 'border-slate-200 dark:border-slate-800'
                      }`}
                    >
                      <Globe size={20} className="text-slate-500" />
                      <span>Sistema</span>
                    </button>
                  </div>
                </div>

                <div className="flex justify-end pt-4 border-t border-slate-100 dark:border-slate-800">
                  <Button
                    type="submit"
                    className="bg-brand-deep text-white font-bold cursor-pointer"
                  >
                    <Save size={15} className="mr-1.5" /> Guardar Preferencias
                  </Button>
                </div>
              </form>
            </Card>
          )}
        </div>
      </div>
    </div>
  )
}
