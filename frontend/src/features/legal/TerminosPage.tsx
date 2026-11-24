import { Link } from 'react-router-dom'
import { LegalLayout } from './LegalLayout'
import { TITULAR } from './legalDatos'

/** `/terminos`, anónima. */
export function TerminosPage() {
  return (
    <LegalLayout
      titulo="Términos de uso"
      otro={{ to: '/privacidad', label: 'Política de privacidad' }}
    >
      <p>
        Estos términos regulan el uso de Sorteo360, la plataforma para organizar sorteos en fiestas
        y eventos corporativos, ofrecida por {TITULAR.razonSocial} (RNC {TITULAR.rnc}), con
        domicilio en {TITULAR.domicilio} ("nosotros"). Al crear una cuenta, la empresa que la crea
        ("el cliente") y la persona que la registra en su nombre aceptan estos términos.
      </p>

      <h2>1. El servicio</h2>
      <p>
        Sorteo360 permite cargar el listado de participantes, registrar la asistencia con códigos
        QR, configurar rondas y premios, realizar el sorteo en vivo, registrar la entrega de cada
        premio y generar el acta y los certificados. Las funciones y los topes disponibles dependen
        del plan de la cuenta.
      </p>

      <h2>2. La cuenta</h2>
      <ul>
        <li>
          La cuenta se activa con el enlace que enviamos al correo registrado. Quien la activa es su
          administrador y declara que tiene autorización de la empresa para hacerlo.
        </li>
        <li>
          El cliente es responsable de mantener seguras las contraseñas y de lo que hagan los
          usuarios que invite. Si sospecha un acceso no autorizado, debe avisarnos de inmediato.
        </li>
        <li>Los datos de registro deben ser verdaderos y mantenerse al día.</li>
      </ul>

      <h2>3. Planes, pagos y vencimiento</h2>
      <ul>
        <li>
          El plan Gratis no tiene costo y tiene los topes publicados en la página de planes (entre
          ellos, 25 participantes por sorteo, un sorteo al mes, una ronda, un usuario y sin pantalla
          de proyector).
        </li>
        <li>
          Los planes de pago (Básico, Pro y Avanzado) se pagan por adelantado, en forma mensual o
          anual; el pago anual tiene el descuento publicado. Elegir un plan de pago crea una
          solicitud: el plan se activa cuando confirmamos la transferencia, y hasta entonces la
          cuenta sigue en su plan actual.
        </li>
        <li>
          Al terminar el período pagado sin una renovación confirmada, la cuenta pasa al plan
          Gratis. No se borra información: lo que pase los topes del plan Gratis se conserva, pero
          no se puede ampliar.
        </li>
        <li>
          Los precios pueden cambiar; un cambio se aplica desde la siguiente renovación y se avisa
          con al menos 30 días. Salvo que la ley disponga otra cosa, no se reembolsan períodos ya
          iniciados.
        </li>
      </ul>

      <h2>4. Los sorteos son del cliente</h2>
      <ul>
        <li>
          El cliente decide las reglas, los premios y quién participa, y es el único responsable de
          entregar los premios y de cumplir las normas que apliquen a su sorteo (permisos, impuestos
          sobre premios, comunicaciones a los participantes y cualquier otra obligación).
        </li>
        <li>
          La plataforma elige a los ganadores al azar entre los participantes que cumplen las reglas
          configuradas y deja constancia en la bitácora. El acta que genera es un documento de
          apoyo: su valor notarial depende del notario que el cliente contrate.
        </li>
      </ul>

      <h2>5. Uso permitido</h2>
      <p>El cliente y sus usuarios se comprometen a no:</p>
      <ul>
        <li>cargar datos de personas sin tener una base legal para hacerlo;</li>
        <li>usar la plataforma para sorteos o actividades contrarias a la ley;</li>
        <li>
          intentar acceder a cuentas ajenas, saltarse los topes del plan o afectar el funcionamiento
          del servicio;
        </li>
        <li>revender el servicio sin un acuerdo escrito con nosotros.</li>
      </ul>
      <p>Podemos suspender una cuenta que incumpla esta sección, avisando al administrador.</p>

      <h2>6. Datos e información del cliente</h2>
      <p>
        La información que el cliente carga (participantes, premios, marca y demás) es suya.
        Nosotros la tratamos solo para prestar el servicio, como se explica en la{' '}
        <Link to="/privacidad" className="font-semibold underline">
          política de privacidad
        </Link>
        . El cliente puede exportar su información y pedir el cierre de la cuenta en cualquier
        momento.
      </p>

      <h2>7. Disponibilidad y modo sin internet</h2>
      <p>
        Trabajamos para que el servicio esté disponible siempre, pero puede haber interrupciones por
        mantenimiento o causas ajenas. El modo de salón sin internet depende del equipo y la red
        local que el cliente prepare; recomendamos probarlo antes del evento.
      </p>

      <h2>8. Responsabilidad</h2>
      <p>
        El servicio se presta tal como está. En la medida en que la ley lo permita, nuestra
        responsabilidad total frente al cliente se limita a lo que pagó en los doce meses anteriores
        al hecho que la origine, y no respondemos por daños indirectos ni por la entrega o el valor
        de los premios.
      </p>

      <h2>9. Cambios en estos términos</h2>
      <p>
        Si cambiamos estos términos, avisaremos al administrador de cada cuenta por correo con al
        menos 15 días de anticipación. Seguir usando el servicio después de esa fecha significa
        aceptarlos.
      </p>

      <h2>10. Ley aplicable y contacto</h2>
      <p>
        Estos términos se rigen por las leyes de la República Dominicana, y cualquier controversia
        se somete a los tribunales de Santo Domingo. Para cualquier consulta: {TITULAR.correo}.
      </p>
    </LegalLayout>
  )
}
