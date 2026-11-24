import { useState, useRef, useEffect } from 'react'
import { createPortal } from 'react-dom'
import { Calendar as CalendarIcon, ChevronDown, ChevronLeft, ChevronRight, X } from 'lucide-react'
import { cn } from '@/lib/utils'

export interface DatePickerProps {
  /** Fecha en formato ISO 'YYYY-MM-DD' */
  value?: string
  /** Callback al seleccionar una fecha */
  onChange?: (date: string) => void
  /** Texto cuando está vacío */
  placeholder?: string
  /** Deshabilitar componente */
  disabled?: boolean
  /** Mostrar u ocultar el borde del control (por defecto: true) */
  bordered?: boolean
  /** Clases adicionales del contenedor */
  className?: string
  /** Clases adicionales del botón de disparo (permite cambiar borde, grosor, color, focus, padding, redondeado) */
  inputClassName?: string
  /** Clases adicionales del texto de placeholder */
  placeholderClassName?: string
  /** Clases adicionales del icono de calendario */
  iconClassName?: string
  /** Clases adicionales del menú desplegable */
  popoverClassName?: string
  /** Clases adicionales para el día seleccionado */
  selectedDayClassName?: string
  /** Clases adicionales para el día de hoy */
  todayDayClassName?: string
  /** Clases adicionales para la barra superior del desplegable */
  headerClassName?: string
  /** Alineación del menú ('left' | 'right') */
  align?: 'left' | 'right'
  /** Tamaño del control ('sm' | 'default' | 'lg') */
  size?: 'sm' | 'default' | 'lg'
  /** Mostrar opción de limpiar fecha */
  clearable?: boolean
  /** Fecha mínima seleccionable, en ISO 'YYYY-MM-DD'. Los días anteriores quedan deshabilitados en el calendario. */
  minDate?: string
  /** Fecha máxima seleccionable, en ISO 'YYYY-MM-DD'. Los días posteriores quedan deshabilitados en el calendario. */
  maxDate?: string
  /**
   * Mes/año que debe mostrar el calendario mientras no haya una fecha elegida (p. ej. el
   * año y mes de una edición de feria, para que "Desde" no abra en el mes actual sino en
   * el de la feria). Solo importa el año y el mes; se ignora en cuanto `value` trae una
   * fecha propia.
   */
  anchorDate?: string
  /** Formateador de texto personalizado */
  formatDisplay?: (isoDate: string) => string
  /** Etiqueta superior opcional (ej: 'Desde', 'Hasta') */
  label?: string
}

const MONTHS_SHORT = [
  'Ene',
  'Feb',
  'Mar',
  'Abr',
  'May',
  'Jun',
  'Jul',
  'Ago',
  'Sep',
  'Oct',
  'Nov',
  'Dic',
]

const MONTHS_FULL = [
  'Enero',
  'Febrero',
  'Marzo',
  'Abril',
  'Mayo',
  'Junio',
  'Julio',
  'Agosto',
  'Septiembre',
  'Octubre',
  'Noviembre',
  'Diciembre',
]

const WEEKDAYS = ['Lu', 'Ma', 'Mi', 'Ju', 'Vi', 'Sá', 'Do']

function formatDefaultDisplay(isoString: string): string {
  if (!isoString) return ''
  const parts = isoString.split('-')
  if (parts.length !== 3) return isoString
  const [year, month, day] = parts
  return `${day.padStart(2, '0')}/${month.padStart(2, '0')}/${year}`
}

function parseIsoDate(isoString?: string): Date | null {
  if (!isoString) return null
  const parts = isoString.split('-')
  if (parts.length !== 3) return null
  const year = parseInt(parts[0], 10)
  const month = parseInt(parts[1], 10) - 1
  const day = parseInt(parts[2], 10)
  if (isNaN(year) || isNaN(month) || isNaN(day)) return null
  return new Date(year, month, day)
}

function formatToIso(year: number, month: number, day: number): string {
  const y = String(year).padStart(4, '0')
  const m = String(month + 1).padStart(2, '0')
  const d = String(day).padStart(2, '0')
  return `${y}-${m}-${d}`
}

export function DatePicker({
  value = '',
  onChange,
  placeholder = 'dd/mm/aaaa',
  disabled = false,
  bordered = true,
  className,
  inputClassName,
  placeholderClassName,
  iconClassName,
  popoverClassName,
  selectedDayClassName,
  todayDayClassName,
  headerClassName,
  align = 'left',
  size = 'default',
  clearable = false,
  formatDisplay = formatDefaultDisplay,
  minDate,
  maxDate,
  anchorDate,
  label,
}: DatePickerProps) {
  const [isOpen, setIsOpen] = useState(false)
  const containerRef = useRef<HTMLDivElement>(null)
  const buttonRef = useRef<HTMLButtonElement>(null)
  const popoverRef = useRef<HTMLDivElement>(null)

  const [coords, setCoords] = useState<{ top: number; left: number }>({ top: 0, left: 0 })

  const selectedDate = parseIsoDate(value)
  const parsedMin = parseIsoDate(minDate)
  const parsedMax = parseIsoDate(maxDate)

  const getInitialView = () => {
    if (selectedDate) return { y: selectedDate.getFullYear(), m: selectedDate.getMonth() }
    const ancla = parseIsoDate(anchorDate)
    if (ancla) return { y: ancla.getFullYear(), m: ancla.getMonth() }
    if (parsedMin) return { y: parsedMin.getFullYear(), m: parsedMin.getMonth() }
    if (parsedMax) return { y: parsedMax.getFullYear(), m: parsedMax.getMonth() }
    const now = new Date()
    return { y: now.getFullYear(), m: now.getMonth() }
  }

  const initial = getInitialView()
  const [viewYear, setViewYear] = useState<number>(initial.y)
  const [viewMonth, setViewMonth] = useState<number>(initial.m)

  useEffect(() => {
    const fecha = parseIsoDate(value)
    if (fecha) {
      setViewYear(fecha.getFullYear())
      setViewMonth(fecha.getMonth())
      return
    }
    const ancla = parseIsoDate(anchorDate)
    if (ancla) {
      setViewYear(ancla.getFullYear())
      setViewMonth(ancla.getMonth())
      return
    }
    if (parsedMin) {
      setViewYear(parsedMin.getFullYear())
      setViewMonth(parsedMin.getMonth())
      return
    }
    if (parsedMax) {
      setViewYear(parsedMax.getFullYear())
      setViewMonth(parsedMax.getMonth())
    }
  }, [value, anchorDate, minDate, maxDate])

  const handleToggle = () => {
    if (disabled) return
    if (!isOpen && buttonRef.current) {
      const rect = buttonRef.current.getBoundingClientRect()
      const popoverWidth = popoverRef.current ? popoverRef.current.offsetWidth : 288
      const popoverHeight = popoverRef.current ? popoverRef.current.offsetHeight : 265
      const spaceBelow = window.innerHeight - rect.bottom
      const spaceAbove = rect.top
      const gap = 6

      let top = rect.bottom + gap
      if (spaceBelow < popoverHeight + gap && spaceAbove >= popoverHeight + gap) {
        top = rect.top - popoverHeight - gap
      } else if (spaceBelow < popoverHeight + gap) {
        top = Math.max(10, window.innerHeight - popoverHeight - 10)
      }

      let left = align === 'right' ? rect.right - popoverWidth : rect.left
      if (left < 10) left = 10
      if (left + popoverWidth > window.innerWidth - 10) {
        left = window.innerWidth - popoverWidth - 10
      }

      setCoords({ top, left })
    }
    setIsOpen((prev) => !prev)
  }

  // Position popover relative to button in viewport
  useEffect(() => {
    if (isOpen && buttonRef.current) {
      const updateCoords = () => {
        if (!buttonRef.current) return
        const rect = buttonRef.current.getBoundingClientRect()
        const popoverWidth = popoverRef.current ? popoverRef.current.offsetWidth : 288
        const popoverHeight = popoverRef.current ? popoverRef.current.offsetHeight : 265
        const spaceBelow = window.innerHeight - rect.bottom
        const spaceAbove = rect.top
        const gap = 6

        let top = rect.bottom + gap
        if (spaceBelow < popoverHeight + gap && spaceAbove >= popoverHeight + gap) {
          top = rect.top - popoverHeight - gap
        } else if (spaceBelow < popoverHeight + gap) {
          top = Math.max(10, window.innerHeight - popoverHeight - 10)
        }

        let left = align === 'right' ? rect.right - popoverWidth : rect.left
        if (left < 10) left = 10
        if (left + popoverWidth > window.innerWidth - 10) {
          left = window.innerWidth - popoverWidth - 10
        }

        setCoords({ top, left })
      }

      updateCoords()
      const animId = requestAnimationFrame(updateCoords)
      window.addEventListener('resize', updateCoords)
      return () => {
        cancelAnimationFrame(animId)
        window.removeEventListener('resize', updateCoords)
      }
    }
  }, [isOpen, align])

  // Cierra el selector al hacer scroll fuera o hacer clic fuera
  useEffect(() => {
    if (!isOpen) return

    function handleClickOutside(event: MouseEvent) {
      const target = event.target as Node
      if (
        containerRef.current &&
        !containerRef.current.contains(target) &&
        popoverRef.current &&
        !popoverRef.current.contains(target)
      ) {
        setIsOpen(false)
      }
    }

    function handleScroll(event: Event) {
      const target = event.target as Node
      // Si el scroll ocurrió dentro del menú desplegable, no cerrar
      if (popoverRef.current && popoverRef.current.contains(target)) {
        return
      }
      setIsOpen(false)
    }

    document.addEventListener('mousedown', handleClickOutside)
    window.addEventListener('scroll', handleScroll, true)

    return () => {
      document.removeEventListener('mousedown', handleClickOutside)
      window.removeEventListener('scroll', handleScroll, true)
    }
  }, [isOpen])

  const handlePrevMonth = () => {
    if (!canGoPrevMonth) return
    if (viewMonth === 0) {
      setViewMonth(11)
      setViewYear((y) => y - 1)
    } else {
      setViewMonth((m) => m - 1)
    }
  }

  const handleNextMonth = () => {
    if (!canGoNextMonth) return
    if (viewMonth === 11) {
      setViewMonth(0)
      setViewYear((y) => y + 1)
    } else {
      setViewMonth((m) => m + 1)
    }
  }

  const isDayDisabled = (day: number) => {
    const iso = formatToIso(viewYear, viewMonth, day)
    if (minDate && iso < minDate) return true
    if (maxDate && iso > maxDate) return true
    return false
  }

  const minYear = parsedMin?.getFullYear()
  const maxYear = parsedMax?.getFullYear()

  // Años disponibles dentro del rango permitido
  const currentYr = new Date().getFullYear()
  const availableYears = Array.from({ length: 20 }, (_, i) => currentYr - 5 + i).filter(
    (yr) => (minYear === undefined || yr >= minYear) && (maxYear === undefined || yr <= maxYear),
  )
  if (minYear !== undefined && !availableYears.includes(minYear)) {
    availableYears.push(minYear)
    availableYears.sort((a, b) => a - b)
  }
  if (maxYear !== undefined && !availableYears.includes(maxYear)) {
    availableYears.push(maxYear)
    availableYears.sort((a, b) => a - b)
  }

  // Meses disponibles para el año que se está visualizando
  const minMonthForYear =
    parsedMin && viewYear === parsedMin.getFullYear() ? parsedMin.getMonth() : 0
  const maxMonthForYear =
    parsedMax && viewYear === parsedMax.getFullYear() ? parsedMax.getMonth() : 11

  const availableMonths = MONTHS_SHORT.map((name, idx) => ({
    name,
    idx,
    full: MONTHS_FULL[idx],
  })).filter(({ idx }) => idx >= minMonthForYear && idx <= maxMonthForYear)

  // ¿El rango está totalmente fijo a un único mes y año? (ej. viático del 18 al 26 de Enero de 2027)
  const isSingleMonthRange =
    minYear !== undefined &&
    maxYear !== undefined &&
    minYear === maxYear &&
    parsedMin?.getMonth() === parsedMax?.getMonth()

  const isMonthFixed = availableMonths.length <= 1
  const isYearFixed = availableYears.length <= 1

  // Navegación de mes previo y siguiente
  const prevMonthDate =
    viewMonth === 0 ? { y: viewYear - 1, m: 11 } : { y: viewYear, m: viewMonth - 1 }
  const canGoPrevMonth =
    !isSingleMonthRange &&
    (!parsedMin ||
      prevMonthDate.y > parsedMin.getFullYear() ||
      (prevMonthDate.y === parsedMin.getFullYear() && prevMonthDate.m >= parsedMin.getMonth()))

  const nextMonthDate =
    viewMonth === 11 ? { y: viewYear + 1, m: 0 } : { y: viewYear, m: viewMonth + 1 }
  const canGoNextMonth =
    !isSingleMonthRange &&
    (!parsedMax ||
      nextMonthDate.y < parsedMax.getFullYear() ||
      (nextMonthDate.y === parsedMax.getFullYear() && nextMonthDate.m <= parsedMax.getMonth()))

  const handleSelectDay = (day: number) => {
    if (isDayDisabled(day)) return
    const isoStr = formatToIso(viewYear, viewMonth, day)
    onChange?.(isoStr)
    setIsOpen(false)
  }

  const handleClear = (e: React.SyntheticEvent) => {
    e.stopPropagation()
    onChange?.('')
  }

  const firstDayOfMonth = new Date(viewYear, viewMonth, 1)
  let startDayIndex = firstDayOfMonth.getDay() - 1
  if (startDayIndex === -1) startDayIndex = 6

  const daysInMonth = new Date(viewYear, viewMonth + 1, 0).getDate()
  const daysInPrevMonth = new Date(viewYear, viewMonth, 0).getDate()

  const sizeStyles = {
    sm: 'py-2 px-3 text-xs gap-2',
    default: 'py-2.5 px-3.5 text-xs gap-2',
    lg: 'py-3 px-4 text-sm gap-2.5',
  }

  const isToday = (day: number) => {
    const today = new Date()
    return (
      today.getFullYear() === viewYear && today.getMonth() === viewMonth && today.getDate() === day
    )
  }

  const isSelected = (day: number) => {
    if (!selectedDate) return false
    return (
      selectedDate.getFullYear() === viewYear &&
      selectedDate.getMonth() === viewMonth &&
      selectedDate.getDate() === day
    )
  }

  const displayText = value ? formatDisplay(value) : ''

  return (
    <div ref={containerRef} className={cn('relative inline-block w-full', className)}>
      <button
        ref={buttonRef}
        type="button"
        disabled={disabled}
        onClick={handleToggle}
        className={cn(
          'flex w-full items-center justify-between rounded-md font-medium text-slate-900 transition-all focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-deep/20 focus-visible:border-brand-deep dark:text-white dark:focus-visible:ring-blue-500/20 dark:focus-visible:border-blue-500',
          bordered
            ? 'border border-slate-200 dark:border-slate-700 bg-slate-50/50'
            : 'border border-transparent bg-transparent',
          sizeStyles[size],
          isOpen
            ? 'border-brand-deep ring-2 ring-brand-deep/15 text-brand-deep dark:border-blue-500 dark:ring-blue-500/20 bg-white dark:bg-slate-900'
            : 'hover:border-slate-300 dark:hover:border-slate-600 hover:bg-white dark:hover:bg-slate-900',
          disabled && 'cursor-not-allowed opacity-50 bg-slate-100 dark:bg-slate-800/40',
          !disabled && 'cursor-pointer',
          inputClassName,
        )}
      >
        <div className="flex items-center gap-2.5 min-w-0 pl-0.5">
          <CalendarIcon
            className={cn(
              'h-4 w-4 shrink-0 transition-colors',
              isOpen ? 'text-brand-deep dark:text-blue-400' : 'text-slate-400 dark:text-slate-500',
              iconClassName,
            )}
          />
          {label ? (
            <div className="flex flex-col text-left min-w-0 leading-tight">
              <span className="text-[10px] font-medium text-slate-500 dark:text-slate-400">
                {label}
              </span>
              {displayText ? (
                <span className="text-xs font-bold text-slate-900 dark:text-white whitespace-nowrap px-0.5">
                  {displayText}
                </span>
              ) : (
                <span
                  className={cn(
                    'text-xs text-slate-400 dark:text-slate-500 whitespace-nowrap px-0.5',
                    placeholderClassName,
                  )}
                >
                  {placeholder}
                </span>
              )}
            </div>
          ) : displayText ? (
            <span className="text-slate-900 dark:text-white font-medium text-xs whitespace-nowrap px-1">
              {displayText}
            </span>
          ) : (
            <span
              className={cn(
                'text-slate-400 dark:text-slate-500 text-xs whitespace-nowrap px-1',
                placeholderClassName,
              )}
            >
              {placeholder}
            </span>
          )}
        </div>

        <div className="flex items-center gap-1.5 shrink-0 ml-1.5 pr-0.5">
          {clearable && value && !disabled && (
            // No puede ser un <button>: ya está anidado dentro del trigger, que es uno.
            <span
              role="button"
              tabIndex={0}
              aria-label="Limpiar fecha"
              onClick={handleClear}
              onKeyDown={(e) => {
                if (e.key !== 'Enter' && e.key !== ' ') return
                e.preventDefault()
                handleClear(e)
              }}
              className="rounded p-0.5 text-slate-400 hover:bg-slate-200/60 hover:text-slate-600 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-deep/30 dark:hover:bg-slate-700 dark:focus-visible:ring-blue-500/30"
            >
              <X className="h-3.5 w-3.5" />
            </span>
          )}
          <ChevronDown
            className={cn(
              'h-3.5 w-3.5 text-slate-400 transition-transform duration-200 dark:text-slate-500',
              isOpen && 'rotate-180 text-brand-deep dark:text-blue-400',
            )}
          />
        </div>
      </button>

      {/* Popover desplegado vía Portal en document.body con posición fija (flotando por encima de todo) */}
      {isOpen &&
        createPortal(
          <div
            ref={popoverRef}
            style={{ top: `${coords.top}px`, left: `${coords.left}px` }}
            className={cn(
              'fixed z-9999 w-72 rounded-xl border border-slate-200/80 bg-white p-4 shadow-xl transition-opacity duration-150 dark:border-slate-800 dark:bg-slate-900 dark:shadow-slate-950/50 animate-in fade-in-0 zoom-in-95 origin-top-left',
              popoverClassName,
            )}
          >
            {/* Encabezado: Selectores de mes y año + Botones de navegación */}
            <div className={cn('flex items-center justify-between pb-3', headerClassName)}>
              {isSingleMonthRange ? (
                /* Rango fijo (ej. viático del 18 al 26 de Enero 2027): No se puede cambiar mes ni año */
                <div className="flex items-center gap-1.5 px-1 py-1">
                  <span className="text-sm font-bold text-slate-800 dark:text-slate-100">
                    {MONTHS_FULL[viewMonth]} {viewYear}
                  </span>
                </div>
              ) : (
                <div className="flex items-center gap-2">
                  {/* Selector de Mes */}
                  {isMonthFixed ? (
                    <span className="rounded-md bg-slate-100/80 px-2.5 py-1.5 text-xs font-bold text-slate-800 dark:bg-slate-800 dark:text-slate-200">
                      {MONTHS_SHORT[viewMonth]}
                    </span>
                  ) : (
                    <div className="relative flex items-center rounded-md bg-slate-100/80 px-2.5 py-1.5 text-xs font-bold text-slate-800 hover:bg-slate-200/80 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700/80 transition-colors">
                      <select
                        value={viewMonth}
                        onChange={(e) => setViewMonth(Number(e.target.value))}
                        aria-label="Mes"
                        className="appearance-none bg-transparent pr-4 font-bold cursor-pointer focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-deep/30 dark:focus-visible:ring-blue-500/30 rounded"
                      >
                        {availableMonths.map(({ name, idx }) => (
                          <option
                            key={name}
                            value={idx}
                            className="dark:bg-slate-900 text-slate-900 dark:text-white"
                          >
                            {name}
                          </option>
                        ))}
                      </select>
                      <ChevronDown className="pointer-events-none absolute right-2 h-3.5 w-3.5 text-slate-500" />
                    </div>
                  )}

                  {/* Selector de Año */}
                  {isYearFixed ? (
                    <span className="rounded-md bg-slate-100/80 px-2.5 py-1.5 text-xs font-bold text-slate-800 dark:bg-slate-800 dark:text-slate-200">
                      {viewYear}
                    </span>
                  ) : (
                    <div className="relative flex items-center rounded-md bg-slate-100/80 px-2.5 py-1.5 text-xs font-bold text-slate-800 hover:bg-slate-200/80 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700/80 transition-colors">
                      <select
                        value={viewYear}
                        onChange={(e) => {
                          const newYear = Number(e.target.value)
                          setViewYear(newYear)
                          const minM =
                            parsedMin && newYear === parsedMin.getFullYear()
                              ? parsedMin.getMonth()
                              : 0
                          const maxM =
                            parsedMax && newYear === parsedMax.getFullYear()
                              ? parsedMax.getMonth()
                              : 11
                          if (viewMonth < minM) setViewMonth(minM)
                          else if (viewMonth > maxM) setViewMonth(maxM)
                        }}
                        aria-label="Año"
                        className="appearance-none bg-transparent pr-4 font-bold cursor-pointer focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-deep/30 dark:focus-visible:ring-blue-500/30 rounded"
                      >
                        {availableYears.map((yr) => (
                          <option
                            key={yr}
                            value={yr}
                            className="dark:bg-slate-900 text-slate-900 dark:text-white"
                          >
                            {yr}
                          </option>
                        ))}
                      </select>
                      <ChevronDown className="pointer-events-none absolute right-2 h-3.5 w-3.5 text-slate-500" />
                    </div>
                  )}
                </div>
              )}

              {/* Botones de Navegación: solo se muestran si no está fijo */}
              {!isSingleMonthRange && (
                <div className="flex items-center gap-1.5">
                  <button
                    type="button"
                    onClick={handlePrevMonth}
                    disabled={!canGoPrevMonth}
                    aria-label="Mes anterior"
                    className="flex h-7 w-7 items-center justify-center rounded-full bg-slate-100/80 text-slate-600 hover:bg-slate-200 hover:text-slate-900 dark:bg-slate-800 dark:text-slate-400 dark:hover:bg-slate-700 dark:hover:text-white cursor-pointer transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-deep/30 dark:focus-visible:ring-blue-500/30 disabled:cursor-not-allowed disabled:opacity-40 disabled:hover:bg-slate-100/80 dark:disabled:hover:bg-slate-800"
                  >
                    <ChevronLeft className="h-4 w-4" />
                  </button>
                  <button
                    type="button"
                    onClick={handleNextMonth}
                    disabled={!canGoNextMonth}
                    aria-label="Mes siguiente"
                    className="flex h-7 w-7 items-center justify-center rounded-full bg-slate-100/80 text-slate-600 hover:bg-slate-200 hover:text-slate-900 dark:bg-slate-800 dark:text-slate-400 dark:hover:bg-slate-700 dark:hover:text-white cursor-pointer transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-deep/30 dark:focus-visible:ring-blue-500/30 disabled:cursor-not-allowed disabled:opacity-40 disabled:hover:bg-slate-100/80 dark:disabled:hover:bg-slate-800"
                  >
                    <ChevronRight className="h-4 w-4" />
                  </button>
                </div>
              )}
            </div>

            {/* Días de la semana */}
            <div className="grid grid-cols-7 gap-1 pt-2 pb-2 text-center">
              {WEEKDAYS.map((day) => (
                <span key={day} className="text-xs font-bold text-slate-800 dark:text-slate-200">
                  {day}
                </span>
              ))}
            </div>

            {/* Grilla de días */}
            <div className="grid grid-cols-7 gap-1 text-center">
              {Array.from({ length: startDayIndex }).map((_, i) => {
                const dayNum = daysInPrevMonth - startDayIndex + i + 1
                return (
                  <span
                    key={`prev-${i}`}
                    className="flex h-8 w-8 items-center justify-center text-xs text-slate-300 dark:text-slate-700 pointer-events-none mx-auto"
                  >
                    {dayNum}
                  </span>
                )
              })}

              {Array.from({ length: daysInMonth }).map((_, i) => {
                const dayNum = i + 1
                const selected = isSelected(dayNum)
                const today = isToday(dayNum)
                const dayDisabled = isDayDisabled(dayNum)

                return (
                  <button
                    type="button"
                    key={`day-${dayNum}`}
                    disabled={dayDisabled}
                    aria-disabled={dayDisabled}
                    aria-pressed={selected}
                    aria-label={`${dayNum} de ${MONTHS_SHORT[viewMonth]}, ${viewYear}`}
                    onClick={() => handleSelectDay(dayNum)}
                    className={cn(
                      'flex h-8 w-8 items-center justify-center rounded-full text-xs font-bold transition-all cursor-pointer mx-auto focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-deep/40 dark:focus-visible:ring-blue-500/40',
                      dayDisabled
                        ? 'text-slate-300 dark:text-slate-700 cursor-not-allowed hover:bg-transparent'
                        : selected
                          ? cn(
                              'bg-brand-deep text-white shadow-md dark:bg-blue-600',
                              selectedDayClassName,
                            )
                          : today
                            ? cn(
                                'border border-brand-deep text-brand-deep font-extrabold dark:border-blue-400 dark:text-blue-400',
                                todayDayClassName,
                              )
                            : 'text-slate-800 hover:bg-slate-100 dark:text-slate-200 dark:hover:bg-slate-800',
                    )}
                  >
                    {dayNum}
                  </button>
                )
              })}
            </div>
          </div>,
          document.body,
        )}
    </div>
  )
}
