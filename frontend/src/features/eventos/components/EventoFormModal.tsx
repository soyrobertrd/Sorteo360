import { useMemo, useState, useEffect, type FormEvent } from 'react'
import { Check, Globe, AlertTriangle, Lock } from 'lucide-react'
import { Button } from '@/components/ui/Button'
import { CoverPicker, GalleryPicker } from '@/components/ui/ImagePicker'
import { Callout } from '@/components/ui/Callout'
import { Card } from '@/components/ui/Card'
import { ErrorNote, Spinner } from '@/components/ui/Feedback'
import { api } from '@/api'
import { Modal } from '@/components/ui/Modal'
import { Badge } from '@/components/ui/Badge'
import { EVENTO_TYPES, eventoTypeLabel, eventoVisibilityLabel, montajeLabel } from '@/lib/labels'
import { validateHeicFile } from '@/lib/validation/fileValidation'
import {
  detalleDeZonaHorariaHoy,
  errorDeZonaHoraria,
  esZonaHorariaConocida,
  PAISES_CIUDADES,
  zonaHorariaDeCiudad,
} from '../zonasHorarias'
import { cambioDeCiudad, EventoSinCiudadError, type CambioDeCiudad } from '../ciudadDelEvento'
import { ZonaHorariaInput } from './ZonaHorariaInput'
import type {
  CreateEventoRequest,
  EventoDetail,
  EventoImage,
  EventoType,
  EventoVisibility,
  Idioma,
  MontajeEvento,
  UpdateEventoRequest,
} from '@/api/types'
import { MONTAJES_EVENTO } from '@/api/types'
import { Select, TextArea, TextInput } from '@/components/ui/Field'
import { DatePicker } from '@/components/ui/DatePicker'
import { cn } from '@/lib/utils'
import { useRevokeObjectUrls } from '@/lib/useRevokeObjectUrls'

/** Prellenado ligero para crear un evento nuevo a partir de una plantilla (no aplica en edición). */
interface EventoFormInitialValues {
  nombre?: string
  type?: EventoType
  presupuestoEstimado?: number
  pais?: string
  ciudad?: string
  idioma?: Idioma
  descripcion?: string
}

type Paso = 1 | 2 | 3 | 4
const PASOS: [Paso, string][] = [
  [1, 'Información general'],
  [2, 'Ubicación y Venue'],
  [3, 'Imágenes'],
  [4, 'Validar'],
]

export function EventoFormModal({
  evento,
  initialValues,
  venueNombre,
  onClose,
  onCreate,
  onUpdate,
  pending,
  error,
}: {
  evento?: EventoDetail | null
  initialValues?: EventoFormInitialValues
  venueNombre?: string
  onClose: () => void
  onCreate?: (
    body: CreateEventoRequest,
    files: {
      coverFile: File | null
      galleryFiles: File[]
    },
  ) => void
  onUpdate?: (body: UpdateEventoRequest, ciudad: CambioDeCiudad | undefined) => void
  pending: boolean
  error: unknown
}) {
  const isEdit = Boolean(evento)
  const [step, setStep] = useState<Paso>(1)
  const [errors, setErrors] = useState<Record<string, string>>({})

  // El destino sale de la primera ciudad del evento, que es la única que lo sabe. Nada se
  // deduce del nombre de la OPT: una OPT en Miami organiza eventos en cualquier parte.
  const primeraCiudad = evento?.cities?.[0]
  const initialUbicacion = {
    pais: primeraCiudad?.country ?? initialValues?.pais ?? 'República Dominicana',
    ciudad: primeraCiudad?.name ?? initialValues?.ciudad ?? 'Santo Domingo',
  }

  const [form, setForm] = useState({
    nombre: evento?.name ?? initialValues?.nombre ?? '',
    type: (evento?.type ?? initialValues?.type ?? '') as string,
    fecha: evento?.startDate ?? '',
    fechaFin: evento?.endDate ?? '',
    // Nivel de visibilidad de registro: es obligatorio y no viene seleccionado por defecto
    visibilidad: (evento?.visibility ?? '') as EventoVisibility | '',
    presupuestoEstimado:
      evento?.estimatedBudget?.amount?.toString() ??
      initialValues?.presupuestoEstimado?.toString() ??
      '',
    invitedCount: evento ? (evento.invitedCount?.toString() ?? '') : '250',
    // El montaje del salón es opcional en todo tipo de evento; vacío es "Sin montaje".
    seatingLayout: (evento?.seatingLayout ?? '') as MontajeEvento | '',
    descripcion: evento?.description ?? initialValues?.descripcion ?? '',
    pais: initialUbicacion.pais,
    ciudad: initialUbicacion.ciudad,
    // Idioma por defecto es "Sin definir" ('') y es obligatorio
    idioma: (evento ? (evento.language ?? '') : (initialValues?.idioma ?? '')) as Idioma | '',
    sede: venueNombre && venueNombre !== 'Por definir' ? venueNombre : '',
  })

  useEffect(() => {
    if (!error) {
      setErrors({})
      return
    }
    const newErrors: Record<string, string> = {}
    if (typeof error === 'object' && error !== null) {
      const apiErr = error as {
        code?: string
        message?: string
        fieldErrors?: Record<string, string[] | string>
      }
      if (
        apiErr.code === 'EVENT_BUDGET_CURRENCY_NOT_SUPPORTED' ||
        apiErr.code === 'EVENT_BUDGET_AMOUNT_INVALID'
      ) {
        newErrors.presupuestoEstimado = apiErr.message || 'Monto o moneda de presupuesto no válidos'
        setStep(1)
      }
      // La zona horaria vive en el paso 2.
      if (apiErr.code === 'TIME_ZONE_UNKNOWN') setStep(2)
      if (apiErr.fieldErrors) {
        // La visibilidad y el montaje viven en el paso 1; sin volver ahí el error no se vería.
        const delPaso1 = ['visibility', 'seatinglayout']
        if (Object.keys(apiErr.fieldErrors).some((k) => delPaso1.includes(k.toLowerCase()))) {
          setStep(1)
        }
        Object.entries(apiErr.fieldErrors).forEach(([field, msgs]) => {
          const fieldName = field.charAt(0).toLowerCase() + field.slice(1)
          newErrors[fieldName] = Array.isArray(msgs) ? msgs.join(', ') : String(msgs)
        })
      }
    }
    if (Object.keys(newErrors).length > 0) {
      setErrors(newErrors)
    }
  }, [error])

  const DEFAULT_COVER =
    'https://images.unsplash.com/photo-1544551763-46a013bb70d5?auto=format&fit=crop&w=1600&q=80'
  const [coverFile, setCoverFile] = useState<File | null>(null)
  const [galleryFiles, setGalleryFiles] = useState<File[]>([])
  const [coverUrl, setCoverUrl] = useState<string>(
    evento?.images?.find((img) => img.kind === 'Cover') ? '' : DEFAULT_COVER,
  )
  const [galleryImages, setGalleryImages] = useState<EventoImage[]>([])
  const [galleryUrls, setGalleryUrls] = useState<Record<string, string>>({})
  const [uploadPending, setUploadPending] = useState(false)
  const [uploadError, setUploadError] = useState<unknown>(null)

  useEffect(() => {
    // Una respuesta que llega después de cerrar o de cambiar de evento no se aplica: se libera.
    let cancelled = false
    const tomar = (url: string) => {
      if (cancelled) URL.revokeObjectURL(url)
      return !cancelled
    }
    if (evento?.images) {
      const cover = evento.images.find((img) => img.kind === 'Cover')
      if (cover) {
        api
          .getFileContentUrl(cover.storedFileId)
          .then((url) => tomar(url) && setCoverUrl(url))
          .catch(() => {})
      } else {
        setCoverUrl(DEFAULT_COVER)
      }

      const gallery = evento.images.filter((img) => img.kind === 'Gallery')
      setGalleryImages(gallery)

      Promise.all(
        gallery.map(async (img) => {
          try {
            const url = await api.getFileContentUrl(img.storedFileId)
            return { id: img.id, url }
          } catch {
            return { id: img.id, url: '' }
          }
        }),
      ).then((results) => {
        if (cancelled) {
          results.forEach((r) => r.url && URL.revokeObjectURL(r.url))
          return
        }
        const map: Record<string, string> = {}
        results.forEach((r) => {
          if (r.url) map[r.id] = r.url
        })
        setGalleryUrls(map)
      })
    }
    return () => {
      cancelled = true
    }
  }, [evento])

  // La portada y la galería descargadas son blob: URLs: se liberan al reemplazarlas o al cerrar.
  const urlsDescargadas = useMemo(
    () => [coverUrl, ...Object.values(galleryUrls)],
    [coverUrl, galleryUrls],
  )
  useRevokeObjectUrls(urlsDescargadas)

  const MAX_GALERIA = 20
  const totalGaleria = isEdit ? galleryImages.length : galleryFiles.length
  // Una URL por archivo y no una por render: se liberan al cambiar la lista.
  const previewsGaleria = useMemo(
    () => galleryFiles.map((file) => URL.createObjectURL(file)),
    [galleryFiles],
  )
  useEffect(
    () => () => previewsGaleria.forEach((url) => URL.revokeObjectURL(url)),
    [previewsGaleria],
  )

  const [mapEmbedUrl, setMapEmbedUrl] = useState(evento?.mapEmbedUrl || '')

  const handleUploadCover = async (file: File) => {
    setUploadError(null)
    const heicError = validateHeicFile(file)
    if (heicError) {
      setUploadError(heicError)
      return
    }

    if (isEdit && evento) {
      try {
        setUploadPending(true)
        const img = await api.uploadEventoImage(evento.id, file, 'Cover')
        const url = await api.getFileContentUrl(img.storedFileId)
        setCoverUrl(url)
      } catch (err) {
        setUploadError(err)
      } finally {
        setUploadPending(false)
      }
    } else {
      setCoverFile(file)
      setCoverUrl(URL.createObjectURL(file))
    }
  }

  const handleUploadGalleryImage = async (file: File) => {
    setUploadError(null)
    const heicError = validateHeicFile(file)
    if (heicError) {
      setUploadError(heicError)
      return
    }

    if (isEdit && evento) {
      try {
        setUploadPending(true)
        const img = await api.uploadEventoImage(evento.id, file, 'Gallery')
        const url = await api.getFileContentUrl(img.storedFileId)
        setGalleryImages((prev) => [...prev, img])
        setGalleryUrls((prev) => ({ ...prev, [img.id]: url }))
      } catch (err) {
        setUploadError(err)
      } finally {
        setUploadPending(false)
      }
    } else {
      setGalleryFiles((prev) => [...prev, file])
    }
  }

  const handleDeleteGalleryImage = async (id: string, index: number) => {
    setUploadError(null)
    if (isEdit && evento) {
      try {
        setUploadPending(true)
        await api.deleteEventoImage(evento.id, id)
        setGalleryImages((prev) => prev.filter((img) => img.id !== id))
        setGalleryUrls((prev) => {
          const copy = { ...prev }
          delete copy[id]
          return copy
        })
      } catch (err) {
        setUploadError(err)
      } finally {
        setUploadPending(false)
      }
    } else {
      setGalleryFiles((prev) => prev.filter((_, idx) => idx !== index))
    }
  }

  const clearErrors = (...keys: string[]) => {
    setErrors((prev) => {
      const copy = { ...prev }
      for (const key of keys) delete copy[key]
      return copy
    })
  }

  const set = (key: keyof typeof form) => (value: string) => {
    setForm((f) => ({ ...f, [key]: value }))
    clearErrors(key)
  }

  // La mayoría de los eventos dura un día, así que la fecha de fin sigue a la de inicio
  // mientras nadie la haya movido hacia adelante. Sigue siendo editable.
  const setFechaInicio = (value: string) => {
    setForm((f) => ({
      ...f,
      fecha: value,
      fechaFin: !f.fechaFin || f.fechaFin < value ? value : f.fechaFin,
    }))
    clearErrors('fecha', 'fechaFin')
  }

  const validateStep = (currentStep: number) => {
    const newErrors: Record<string, string> = {}
    if (currentStep === 1) {
      if (!form.nombre.trim()) {
        newErrors.nombre = 'El nombre del evento es obligatorio'
      }
      if (!form.type) {
        newErrors.type = 'Selecciona el tipo de evento'
      }
      if (!form.fecha) {
        newErrors.fecha = 'Debe seleccionar la fecha de inicio del evento'
      }
      if (!form.fechaFin) {
        newErrors.fechaFin = 'Debe seleccionar la fecha de fin del evento'
      } else if (form.fecha && form.fechaFin < form.fecha) {
        newErrors.fechaFin = 'La fecha de fin debe ser igual o posterior a la de inicio'
      }
      if (!form.visibilidad) {
        newErrors.visibilidad = 'Debe seleccionar el nivel de visibilidad de registro'
      }
    } else if (currentStep === 2) {
      if (!form.pais.trim()) {
        newErrors.pais = 'El país es obligatorio'
      }
      if (!form.ciudad.trim()) {
        newErrors.ciudad = 'La ciudad es obligatoria'
      }
      if (!form.idioma) {
        newErrors.idioma = 'El idioma del evento es obligatorio'
      }
      if (!esZonaHorariaConocida(zonaHoraria.trim(), primeraCiudad?.timeZone)) {
        newErrors.zonaHoraria = 'Elige una zona horaria de la lista'
      }
    }
    setErrors(newErrors)
    return Object.keys(newErrors).length === 0
  }

  const handleNext = (e?: React.MouseEvent) => {
    e?.preventDefault()
    e?.stopPropagation()
    if (validateStep(step)) {
      setStep((s) => (s + 1) as Paso)
    }
  }

  const handleBack = (e?: React.MouseEvent) => {
    e?.preventDefault()
    e?.stopPropagation()
    setErrors({})
    if (step > 1) setStep((s) => (s - 1) as Paso)
  }

  // Null mientras nadie toque el selector: la zona sigue a la ciudad elegida. Al editar, mientras
  // país y ciudad sigan como estaban, la zona es la guardada; elegir otra ciudad sugiere la suya.
  const [zonaElegida, setZonaElegida] = useState<string | null>(null)
  const ciudadSinCambiar =
    form.pais === primeraCiudad?.country && form.ciudad === primeraCiudad?.name
  const zonaHoraria =
    zonaElegida ??
    (ciudadSinCambiar ? primeraCiudad?.timeZone : null) ??
    zonaHorariaDeCiudad(form.pais, form.ciudad)

  // El selector marca lo que falla antes de guardar o, si no, lo que el servicio rechazó.
  const errorDeZona = errors.zonaHoraria || errorDeZonaHoraria(error)
  const ciudadesDelPais = (() => {
    const catalogo = PAISES_CIUDADES[form.pais] ?? []
    return form.ciudad && !catalogo.includes(form.ciudad) ? [form.ciudad, ...catalogo] : catalogo
  })()

  const PAISES_EUROPA = [
    'España',
    'Francia',
    'Alemania',
    'Italia',
    'Reino Unido',
    'Portugal',
    'Holanda',
    'Suiza',
    'Bélgica',
    'Austria',
    'Europa',
  ]
  const isEuropa = PAISES_EUROPA.includes(form.pais)
  const currencyCode = (isEuropa ? 'EUR' : 'USD') as 'EUR' | 'USD'

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault()
    // Guardar desde el paso 1 no exime del paso 2: si algo falta se vuelve a él,
    // que es donde se ve el campo marcado.
    if (!validateStep(1)) {
      setStep(1)
      return
    }
    if (!validateStep(2)) {
      setStep(2)
      return
    }

    // Guardar reemplaza el evento entero, así que el link de la convocatoria viaja tal como
    // vino: este formulario no lo edita, y omitirlo lo borraría.
    const shared: UpdateEventoRequest = {
      name: form.nombre.trim(),
      startDate: form.fecha,
      endDate: form.fechaFin,
      // Nunca nulo: el PATCH lo exige, y un evento creado sin idioma nace en español. El campo
      // del formulario solo queda vacío editando un evento anterior a que el idioma fuera
      // obligatorio.
      language: (form.idioma || 'es') as Idioma,
      // Siempre se manda: omitirla dejaría la guardada y el control no serviría de nada.
      // Una rueda de prensa no pide la visibilidad: conserva la guardada, o nace privada.
      visibility: form.visibilidad || evento?.visibility || 'private',
      externalUrl: evento?.externalUrl ?? null,
      invitedCount: form.invitedCount ? Number(form.invitedCount) : null,
      estimatedBudget: form.presupuestoEstimado
        ? { amount: Number(form.presupuestoEstimado), currency: currencyCode }
        : null,
      description: form.descripcion.trim() || null,
      // "Sin montaje" va nulo, nunca vacío: el servicio rechaza `""` como valor desconocido.
      seatingLayout: form.seatingLayout || null,
    }

    if (isEdit) {
      // La ciudad va en su propio PATCH, después del evento y solo si cambió.
      const ciudad = primeraCiudad
        ? cambioDeCiudad(primeraCiudad, { pais: form.pais, ciudad: form.ciudad, zona: zonaHoraria })
        : undefined
      onUpdate?.(shared, ciudad)
    } else {
      onCreate?.(
        {
          ...shared,
          type: form.type as EventoType,
          city: form.ciudad.trim(),
          country: form.pais.trim(),
          timeZone: zonaHoraria.trim(),
        },
        {
          coverFile,
          galleryFiles,
        },
      )
    }
  }

  const modalTitleNode = (
    <div className="flex items-center gap-2">
      <div>
        <h2 className="font-sans text-base font-extrabold text-slate-900 dark:text-white">
          {isEdit ? 'Editar evento' : 'Crear nuevo evento'}
        </h2>
        <p className="font-body text-xs font-normal leading-4 text-muted-foreground">
          Sigue los 4 sencillos pasos para completar la configuración
        </p>
      </div>
      {isEdit && (
        <Badge className="bg-blue-100 text-[11px] font-bold text-blue-800 dark:bg-blue-950/80 dark:text-blue-300">
          BORRADOR
        </Badge>
      )}
    </div>
  )

  const IDIOMA_LABEL: Record<string, string> = {
    es: 'Español (ES)',
    en: 'Inglés (EN)',
    fr: 'Francés (FR)',
    de: 'Alemán (DE)',
    it: 'Italiano (IT)',
    pt: 'Portugués (PT)',
  }

  return (
    <Modal title={modalTitleNode} onClose={onClose} width="sm:max-w-4xl">
      <div className="space-y-4 py-1">
        {/* Stepper horizontal compacto */}
        <div className="mx-auto max-w-2xl">
          <div className="relative flex items-center justify-between">
            {/* Línea punteada entre un paso y otro, a la altura de los círculos. */}
            <div className="absolute left-16 right-16 top-4 border-t-2 border-dashed border-slate-300 dark:border-slate-600" />
            {PASOS.map(([numero, etiqueta]) => (
              <button
                key={numero}
                type="button"
                onClick={() => {
                  // Para avanzar hay que tener completos los pasos anteriores.
                  for (let p = 1; p < numero; p++) if (!validateStep(p as Paso)) return
                  setStep(numero)
                }}
                className="relative z-10 flex flex-col items-center bg-white px-2 dark:bg-slate-900 cursor-pointer"
              >
                <div
                  className={`flex h-8 w-8 items-center justify-center rounded-full text-xs font-bold transition-colors ${
                    step === numero
                      ? 'bg-brand-deep text-white shadow-xs'
                      : step > numero
                        ? 'bg-emerald-600 text-white'
                        : 'bg-slate-100 text-slate-500 dark:bg-slate-800'
                  }`}
                >
                  {step > numero ? <Check size={14} /> : numero}
                </div>
                <span
                  className={`mt-1 text-sm font-semibold ${
                    step === numero ? 'text-brand-deep dark:text-blue-400' : 'text-slate-500'
                  }`}
                >
                  {etiqueta}
                </span>
              </button>
            ))}
          </div>
        </div>

        {Object.keys(errors).length > 0 && (
          <Callout tone="danger" icon={AlertTriangle} className="font-semibold">
            Por favor completa los campos obligatorios marcados en rojo antes de continuar.
          </Callout>
        )}

        <form
          onSubmit={handleSubmit}
          onKeyDown={(e) => {
            if (e.key === 'Enter' && step !== 4) {
              e.preventDefault()
            }
          }}
          className="space-y-4"
        >
          {/* PASO 1: Información General */}
          {step === 1 && (
            <Card className="p-4 space-y-4 border border-slate-200/80 dark:border-slate-800">
              <div className="grid grid-cols-1 gap-3.5 sm:grid-cols-3">
                <div className="sm:col-span-2">
                  <label className="mb-1 block text-sm font-medium text-slate-800 dark:text-slate-200">
                    Nombre del evento <span className="text-rose-500">*</span>
                  </label>
                  <TextInput
                    aria-invalid={Boolean(errors.nombre)}
                    type="text"
                    placeholder="Ej: Cumbre Anual de Innovación Tecnológica 2026"
                    value={form.nombre}
                    onChange={(e) => set('nombre')(e.target.value)}
                  />
                  {errors.nombre && (
                    <p className="mt-1 text-xs font-semibold text-rose-600 dark:text-rose-400">
                      * {errors.nombre}
                    </p>
                  )}
                </div>
                <div>
                  <label className="mb-1 block text-sm font-medium text-slate-800 dark:text-slate-200">
                    Tipo de evento <span className="text-rose-500">*</span>
                  </label>
                  <Select
                    aria-invalid={Boolean(errors.type)}
                    value={form.type}
                    onChange={(e) => set('type')(e.target.value)}
                  >
                    <option value="" disabled hidden>
                      Seleccionar tipo de evento
                    </option>
                    {EVENTO_TYPES.map((t) => (
                      <option key={t} value={t}>
                        {eventoTypeLabel[t]}
                      </option>
                    ))}
                  </Select>
                  {errors.type && (
                    <p className="mt-1 text-xs font-semibold text-rose-600 dark:text-rose-400">
                      * {errors.type}
                    </p>
                  )}
                </div>
              </div>

              <div className="grid grid-cols-1 gap-3.5 sm:grid-cols-3">
                <CampoFecha
                  label="Fecha de inicio"
                  value={form.fecha}
                  onChange={setFechaInicio}
                  error={errors.fecha}
                />

                <CampoFecha
                  label="Fecha de fin"
                  value={form.fechaFin}
                  onChange={set('fechaFin')}
                  min={form.fecha || undefined}
                  error={errors.fechaFin}
                />

                <div>
                  <label className="mb-1 block text-sm font-medium text-slate-800 dark:text-slate-200">
                    Invitados esperados
                  </label>
                  <TextInput
                    type="number"
                    min="1"
                    step="10"
                    value={form.invitedCount}
                    onChange={(e) => set('invitedCount')(e.target.value)}
                    placeholder="Ej: 250"
                  />
                </div>
              </div>

              <div className="grid grid-cols-1 gap-3.5 sm:grid-cols-2">
                <div>
                  <label
                    htmlFor="evento-montaje"
                    className="mb-1 block text-sm font-medium text-slate-800 dark:text-slate-200"
                  >
                    Montaje del salón
                  </label>
                  <Select
                    aria-invalid={Boolean(errors.seatingLayout)}
                    id="evento-montaje"
                    value={form.seatingLayout}
                    onChange={(e) => {
                      set('seatingLayout')(e.target.value)
                      clearErrors('seatingLayout')
                    }}
                  >
                    <option value="">Sin montaje</option>
                    {MONTAJES_EVENTO.map((m) => (
                      <option key={m} value={m}>
                        {montajeLabel[m]}
                      </option>
                    ))}
                  </Select>
                  {errors.seatingLayout && (
                    <p className="mt-1 text-xs font-semibold text-rose-600 dark:text-rose-400">
                      * {errors.seatingLayout}
                    </p>
                  )}
                </div>

                <>
                  <div>
                    <label className="mb-1 block text-sm font-medium text-slate-800 dark:text-slate-200">
                      Nivel de visibilidad de registro <span className="text-rose-500">*</span>
                    </label>
                    <div className="flex items-center gap-6 pt-0.5">
                      <label className="flex items-center gap-2 text-sm font-semibold text-slate-700 dark:text-slate-300 cursor-pointer">
                        <input
                          type="radio"
                          name="visibilidad"
                          value="public"
                          checked={form.visibilidad === 'public'}
                          onChange={() => {
                            set('visibilidad')('public')
                            clearErrors('visibilidad', 'visibility')
                          }}
                          className="text-blue-600 focus:ring-blue-500 cursor-pointer"
                        />
                        {eventoVisibilityLabel.public}
                      </label>
                      <label className="flex items-center gap-2 text-sm font-semibold text-slate-700 dark:text-slate-300 cursor-pointer">
                        <input
                          type="radio"
                          name="visibilidad"
                          value="private"
                          checked={form.visibilidad === 'private'}
                          onChange={() => {
                            set('visibilidad')('private')
                            clearErrors('visibilidad', 'visibility')
                          }}
                          className="text-blue-600 focus:ring-blue-500 cursor-pointer"
                        />
                        {eventoVisibilityLabel.private}
                      </label>
                    </div>
                    {form.visibilidad === 'private' && (
                      <p className="mt-1.5 flex items-start gap-1.5 text-xs text-slate-600 dark:text-slate-300">
                        <Lock size={13} className="mt-0.5 shrink-0 text-brand dark:text-blue-400" />
                        Solo podrán registrarse las personas invitadas al evento.
                      </p>
                    )}
                    {(errors.visibilidad || errors.visibility) && (
                      <p className="mt-1 text-xs font-semibold text-rose-600 dark:text-rose-400">
                        * {errors.visibilidad || errors.visibility}
                      </p>
                    )}
                  </div>
                </>
              </div>

              <div>
                <label className="mb-1 block text-sm font-medium text-slate-800 dark:text-slate-200">
                  {'Sobre este evento'}
                </label>
                <TextArea
                  rows={2}
                  placeholder={'Resumen ejecutivo del evento para materiales promocionales...'}
                  value={form.descripcion}
                  onChange={(e) => set('descripcion')(e.target.value)}
                />
              </div>
            </Card>
          )}

          {/* PASO 2: Ubicación del Evento & Venue */}
          {step === 2 && (
            <Card className="p-4 space-y-4 border border-slate-200/80 dark:border-slate-800">
              <>
                <div className="grid grid-cols-1 gap-3.5 sm:grid-cols-3">
                  <div>
                    <label className="mb-1 block text-sm font-medium text-slate-800 dark:text-slate-200">
                      País <span className="text-rose-500">*</span>
                    </label>
                    <Select
                      value={form.pais}
                      onChange={(e) => {
                        const newPais = e.target.value
                        const ciudades = PAISES_CIUDADES[newPais] ?? ['Santo Domingo']
                        setForm((f) => ({ ...f, pais: newPais, ciudad: ciudades[0] }))
                        clearErrors('pais', 'ciudad')
                      }}
                    >
                      <option value="" disabled hidden>
                        Seleccionar país
                      </option>
                      {/* El destino guardado puede no estar en el catálogo: se muestra igual. */}
                      {form.pais && !(form.pais in PAISES_CIUDADES) && (
                        <option value={form.pais}>{form.pais}</option>
                      )}
                      {Object.keys(PAISES_CIUDADES).map((pais) => (
                        <option key={pais} value={pais}>
                          {pais}
                        </option>
                      ))}
                    </Select>
                    {errors.pais && (
                      <p className="mt-1 text-xs font-semibold text-rose-600 dark:text-rose-400">
                        * {errors.pais}
                      </p>
                    )}
                  </div>

                  <div>
                    <label className="mb-1 block text-sm font-medium text-slate-800 dark:text-slate-200">
                      Ciudad <span className="text-rose-500">*</span>
                    </label>
                    <Select
                      disabled={!form.pais}
                      value={form.ciudad}
                      onChange={(e) => set('ciudad')(e.target.value)}
                    >
                      <option value="" disabled hidden>
                        Seleccionar ciudad
                      </option>
                      {ciudadesDelPais.map((ciudad) => (
                        <option key={ciudad} value={ciudad}>
                          {ciudad}
                        </option>
                      ))}
                    </Select>
                    {errors.ciudad && (
                      <p className="mt-1 text-xs font-semibold text-rose-600 dark:text-rose-400">
                        * {errors.ciudad}
                      </p>
                    )}
                  </div>

                  <div>
                    <label className="mb-1 block text-sm font-medium text-slate-800 dark:text-slate-200">
                      Idioma del evento <span className="text-rose-500">*</span>
                    </label>
                    <Select
                      aria-invalid={Boolean(errors.idioma || errors.language)}
                      value={form.idioma}
                      onChange={(e) => {
                        set('idioma')(e.target.value)
                        clearErrors('idioma', 'language')
                      }}
                    >
                      <option value="">Sin definir</option>
                      <option value="es">Español (ES)</option>
                      <option value="en">Inglés (EN)</option>
                      <option value="fr">Francés (FR)</option>
                      <option value="de">Alemán (DE)</option>
                      <option value="it">Italiano (IT)</option>
                      <option value="pt">Portugués (PT)</option>
                    </Select>
                    {(errors.idioma || errors.language) && (
                      <p className="mt-1 text-xs font-semibold text-rose-600 dark:text-rose-400">
                        * {errors.idioma || errors.language}
                      </p>
                    )}
                  </div>
                </div>
              </>

              <div>
                <label className="mb-1 block text-sm font-medium text-slate-800 dark:text-slate-200">
                  Zona horaria <span className="text-rose-500">*</span>
                </label>
                <ZonaHorariaInput
                  value={zonaHoraria}
                  onChange={(zona) => {
                    setZonaElegida(zona)
                    clearErrors('zonaHoraria')
                  }}
                  invalid={Boolean(errorDeZona)}
                />
                {errorDeZona ? (
                  <p className="mt-1 text-xs font-semibold text-rose-600 dark:text-rose-400">
                    * {errorDeZona}
                  </p>
                ) : (
                  <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">
                    {detalleDeZonaHorariaHoy(zonaHoraria)}
                  </p>
                )}
              </div>

              {/* Sede / Venue & Google Maps (Unificado) */}
              <div className="space-y-3.5 pt-2 border-t border-slate-100 dark:border-slate-800">
                <div>
                  <label className="mb-1 block text-sm font-medium text-slate-800 dark:text-slate-200">
                    Sede / Venue
                  </label>
                  <TextInput
                    type="text"
                    placeholder="Ej: Miami Beach Convention Center, Hotel Jaragua..."
                    value={form.sede}
                    onChange={(e) => set('sede')(e.target.value)}
                  />
                </div>

                <div>
                  <label className="mb-1 block text-sm font-medium text-slate-800 dark:text-slate-200">
                    Ubicación de Google Maps del Venue
                  </label>
                  <TextInput
                    type="text"
                    placeholder="Pegar código <iframe src='...'> o enlace URL de Google Maps..."
                    value={mapEmbedUrl}
                    onChange={(e) => setMapEmbedUrl(e.target.value)}
                    className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 transition-colors focus:border-blue-600 focus:outline-none dark:border-slate-700 dark:bg-slate-900 dark:text-white"
                  />
                  <p className="mt-1 text-[11px] text-slate-400">
                    Esta dirección / mapa se adjuntará automáticamente en los correos y en la página
                    pública de registro de invitados.
                  </p>
                </div>
              </div>
            </Card>
          )}

          {/* PASO 3: Portada y galería del portal público */}
          {step === 3 && (
            <Card className="p-4 space-y-4 border border-slate-200/80 dark:border-slate-800">
              <>
                <div className="space-y-4">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <p className="text-sm font-bold text-slate-800 dark:text-slate-200">
                        Imágenes del portal público
                      </p>
                      <p className="text-[11px] text-slate-500">
                        JPG, PNG o WebP. La portada encabeza la ficha pública y la página de
                        registro; la galería se muestra debajo de ella.
                      </p>
                    </div>
                    {uploadPending && <Spinner />}
                  </div>
                  {Boolean(uploadError) && <ErrorNote error={uploadError} />}

                  {/* Portada y galería lado a lado, para que el paso quepa sin scroll. */}
                  <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                    <div>
                      <p className="mb-1.5 text-xs font-bold text-slate-700 dark:text-slate-300">
                        Portada
                      </p>
                      <CoverPicker
                        src={coverUrl}
                        onFile={handleUploadCover}
                        badge={coverUrl === DEFAULT_COVER ? 'Imagen por defecto' : undefined}
                        aspectClassName="aspect-video"
                      />
                    </div>

                    <div>
                      <div className="mb-1.5 flex items-center justify-between">
                        <p className="text-xs font-bold text-slate-700 dark:text-slate-300">
                          Galería promocional
                        </p>
                        <span className="text-[11px] font-semibold text-slate-400">
                          {totalGaleria}/{MAX_GALERIA}
                        </span>
                      </div>
                      <GalleryPicker
                        max={MAX_GALERIA}
                        multiple={!isEdit}
                        photos={
                          isEdit
                            ? galleryImages.map((img) => ({
                                key: img.id,
                                src: galleryUrls[img.id] || 'https://placehold.co/200',
                              }))
                            : galleryFiles.map((file, idx) => ({
                                key: `${file.name}-${idx}`,
                                src: previewsGaleria[idx],
                              }))
                        }
                        onAdd={(files) => files.forEach((file) => handleUploadGalleryImage(file))}
                        onRemove={(idx) =>
                          handleDeleteGalleryImage(isEdit ? galleryImages[idx].id : '', idx)
                        }
                      />
                    </div>
                  </div>
                </div>
              </>
            </Card>
          )}

          {/* PASO 4: Validar (resumen para confirmar) */}
          {step === 4 && (
            <Card className="p-4 space-y-4 border border-slate-200/80 dark:border-slate-800">
              {/* Resumen Completo de Validación */}
              <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                <div className="rounded-xl border border-slate-200/80 bg-slate-50/70 p-3.5 dark:border-slate-800 dark:bg-slate-800/40 space-y-2">
                  <span className="text-xs font-extrabold uppercase tracking-wider text-brand-deep dark:text-blue-400">
                    Información general
                  </span>
                  <div className="text-xs space-y-1.5 pt-1">
                    <p>
                      <strong className="text-slate-500">Evento:</strong>{' '}
                      <span className="font-bold text-slate-900 dark:text-white">
                        {form.nombre}
                      </span>
                    </p>
                    <p>
                      <strong className="text-slate-500">Tipo:</strong> {form.type}
                    </p>
                    <p>
                      <strong className="text-slate-500">Fechas:</strong>{' '}
                      {form.fecha && form.fechaFin
                        ? `${form.fecha} — ${form.fechaFin}`
                        : 'No definidas'}
                    </p>

                    <p>
                      <strong className="text-slate-500">Visibilidad:</strong>{' '}
                      {form.visibilidad ? eventoVisibilityLabel[form.visibilidad] : 'Sin definir'}
                    </p>
                  </div>
                </div>

                <div className="rounded-xl border border-slate-200/80 bg-slate-50/70 p-3.5 dark:border-slate-800 dark:bg-slate-800/40 space-y-2">
                  <span className="text-xs font-extrabold uppercase tracking-wider text-brand-deep dark:text-blue-400">
                    Ubicación & Venue
                  </span>
                  <div className="text-xs space-y-1.5 pt-1">
                    <p>
                      <strong className="text-slate-500">Destino:</strong>{' '}
                      {form.ciudad && form.pais ? `${form.ciudad}, ${form.pais}` : 'No definido'}
                    </p>
                    <p>
                      <strong className="text-slate-500">Sede / Venue:</strong>{' '}
                      {form.sede || 'Por definir'}
                    </p>
                    <p>
                      <strong className="text-slate-500">Google Maps:</strong>{' '}
                      {mapEmbedUrl ? 'Configurado' : 'No configurado'}
                    </p>
                    <p>
                      <strong className="text-slate-500">Idioma oficial:</strong>{' '}
                      {IDIOMA_LABEL[form.idioma] ?? 'Sin definir'}
                    </p>
                  </div>
                </div>
              </div>

              <div className="rounded-xl border border-blue-200/80 bg-blue-50/70 p-3.5 dark:border-blue-900/60 dark:bg-blue-950/40">
                <div className="flex items-center gap-2 text-xs font-bold text-blue-900 dark:text-blue-300">
                  <Globe size={16} className="text-blue-600 dark:text-blue-400" />
                  <span>Publicación preliminar lista</span>
                </div>
                <p className="mt-1 font-body text-xs text-slate-600 leading-relaxed dark:text-slate-300">
                  Al hacer clic en <strong>{isEdit ? 'Guardar cambios' : 'Crear evento'}</strong>,
                  el evento se publicará y se generará su perfil inicial en la plataforma.
                </p>
              </div>
            </Card>
          )}

          {/* Un rechazo de la ciudad se dice aunque marque la zona: el evento ya quedó guardado. */}
          {error != null &&
            (!errorDeZonaHoraria(error) || error instanceof EventoSinCiudadError) && (
              <ErrorNote error={error} />
            )}

          <div className="flex items-center justify-between border-t border-slate-100 pt-3 dark:border-slate-800">
            <div>
              {step > 1 && (
                <Button type="button" variant="outline" onClick={handleBack}>
                  Anterior
                </Button>
              )}
            </div>

            <div className="flex items-center gap-2">
              <Button type="button" variant="outline" onClick={onClose}>
                Cancelar
              </Button>
              {isEdit && (
                <Button
                  type="button"
                  disabled={pending}
                  onClick={(e) => handleSubmit(e as unknown as FormEvent)}
                >
                  {pending ? 'Guardando cambios...' : 'Guardar cambios'}
                </Button>
              )}
              {step < 4 ? (
                <Button type="button" variant={isEdit ? 'outline' : 'default'} onClick={handleNext}>
                  Siguiente
                </Button>
              ) : (
                !isEdit && (
                  <Button type="submit" disabled={pending}>
                    {pending ? 'Creando evento...' : 'Crear evento'}
                  </Button>
                )
              )}
            </div>
          </div>
        </form>
      </div>
    </Modal>
  )
}

/** Las dos fechas del evento se ven igual; solo cambian etiqueta y mínimo. */
function CampoFecha({
  label,
  value,
  onChange,
  min,
  error,
}: {
  label: string
  value: string
  onChange: (value: string) => void
  min?: string
  error?: string
}) {
  return (
    <div>
      <label className="mb-1 flex items-center justify-between text-sm font-medium text-slate-800 dark:text-slate-200">
        <span>
          {label} <span className="text-rose-500">*</span>
        </span>
      </label>
      {/* El mismo selector de fecha del alta de edición de feria. */}
      <DatePicker
        value={value}
        onChange={onChange}
        placeholder="DD/MM/AAAA"
        minDate={min}
        inputClassName={cn(
          'h-[42px] rounded-lg bg-white dark:bg-slate-900 text-sm px-3.5',
          error &&
            'border-rose-500 focus-visible:border-rose-500 ring-0.5 ring-rose-500 dark:border-rose-500',
        )}
      />
      {error && (
        <p className="mt-1 text-xs font-semibold text-rose-600 dark:text-rose-400">* {error}</p>
      )}
    </div>
  )
}
