import {
  forwardRef,
  useImperativeHandle,
  useLayoutEffect,
  useRef,
  type ElementType,
  type HTMLAttributes,
  type ReactNode,
} from 'react'

export type AnimateVariant = 'fade' | 'fade-up' | 'fade-down' | 'fade-left' | 'fade-right' | 'zoom'

export type AnimateTrigger = 'mount' | 'visible'

type AnimateProps = Omit<HTMLAttributes<HTMLElement>, 'children'> & {
  variant?: AnimateVariant
  trigger?: AnimateTrigger
  /** Milisegundos, antes de aplicar `ESCALA_DURACION`. */
  duration?: number
  /** Milisegundos antes de empezar; sirve para escalonar una lista. */
  delay?: number
  /** Píxeles que recorre el bloque en las variantes con desplazamiento. */
  distance?: number
  as?: ElementType
  /** `true` pinta el elemento tal cual, sin animar; sirve a un componente con la animación opcional. */
  disabled?: boolean
  className?: string
  children: ReactNode
}

// Multiplica la duración de todas las animaciones de la app. Es el único ajuste para hacerlas
// más lentas o más rápidas sin tocar cada uso: un `duration={600}` dura 600 × este factor.
const ESCALA_DURACION = 1.5

/**
 * Animación de entrada sobria para cualquier bloque. Usa la Web Animations API del
 * navegador, así no depende de ninguna librería ni de clases en `index.css`. Si el
 * usuario pidió reducir el movimiento en su sistema, el contenido aparece sin animar.
 *
 * `trigger="mount"` anima al montarse; `trigger="visible"` espera a que el bloque entre
 * en pantalla, y lo hace una sola vez. Cualquier otra prop (`onClick`, `role`, `style`…) pasa
 * tal cual al elemento, y también un `ref`, así puede reemplazar a un `<div>` sin perder nada.
 */
export const Animate = forwardRef<HTMLElement, AnimateProps>(function Animate(
  {
    variant = 'fade-up',
    trigger = 'mount',
    duration = 300,
    delay = 0,
    distance = 12,
    as: Tag = 'div',
    disabled = false,
    className,
    children,
    ...rest
  },
  refExterno,
) {
  const ref = useRef<HTMLElement>(null)
  // Quien pase un `ref` recibe el mismo elemento que se anima.
  useImperativeHandle(refExterno, () => ref.current as HTMLElement)

  // useLayoutEffect y no useEffect: la animación arranca antes del primer pintado,
  // así el contenido no parpadea visible un instante antes de animarse. Corre una sola vez,
  // al montarse: si después cambia el `delay` (una tarjeta que se mueve de lugar al filtrar)
  // no debe volver a animarse. Para repetirla, quien la usa cambia la `key`.
  useLayoutEffect(() => {
    const el = ref.current
    if (!el || disabled || prefersReducedMotion()) return

    // `fill: 'backwards'` mantiene el primer fotograma durante el `delay` y, al terminar,
    // devuelve el control a los estilos normales del elemento.
    const play = () =>
      el.animate(keyframesFor(variant, distance), {
        duration: duration * ESCALA_DURACION,
        delay,
        easing: 'cubic-bezier(0.16, 1, 0.3, 1)',
        fill: 'backwards',
      })

    if (trigger === 'mount') {
      const animation = play()
      return () => animation.cancel()
    }

    // Oculto hasta que entre en pantalla; se quita la opacidad en el mismo instante en
    // que arranca la animación, que ya parte de opacidad 0.
    el.style.opacity = '0'
    const observer = new IntersectionObserver(
      (entries) => {
        if (!entries.some((entry) => entry.isIntersecting)) return
        observer.disconnect()
        el.style.opacity = ''
        play()
      },
      { threshold: 0.15 },
    )
    observer.observe(el)
    return () => {
      observer.disconnect()
      el.style.opacity = ''
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- solo al montarse, ver arriba
  }, [])

  return (
    <Tag ref={ref} className={className} {...rest}>
      {children}
    </Tag>
  )
})

function prefersReducedMotion(): boolean {
  return window.matchMedia('(prefers-reduced-motion: reduce)').matches
}

// Cada variante describe solo el punto de partida; todas terminan en su sitio y opacas.
// Las direcciones nombran hacia dónde se mueve el bloque: `fade-up` sube desde abajo.
// `fade` no anima `transform`: un fondo `fixed` con un transform en curso pasaría a ser el
// contenedor de sus hijos `fixed` y los descolocaría mientras dura la animación.
function keyframesFor(variant: AnimateVariant, distance: number): Keyframe[] {
  if (variant === 'fade') return [{ opacity: 0 }, { opacity: 1 }]
  const from: Record<Exclude<AnimateVariant, 'fade'>, string> = {
    'fade-up': `translateY(${distance}px)`,
    'fade-down': `translateY(-${distance}px)`,
    'fade-left': `translateX(${distance}px)`,
    'fade-right': `translateX(-${distance}px)`,
    zoom: 'scale(0.96)',
  }
  return [
    { opacity: 0, transform: from[variant] },
    { opacity: 1, transform: 'none' },
  ]
}
