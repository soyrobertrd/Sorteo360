import { LegalLayout } from './LegalLayout'
import { TITULAR } from './legalDatos'

/** `/privacidad`, anónima. Redactada sobre la Ley 172-13 de protección de datos personales. */
export function PrivacidadPage() {
  return (
    <LegalLayout
      titulo="Política de privacidad"
      otro={{ to: '/terminos', label: 'Términos de uso' }}
    >
      <p>
        Esta política explica qué datos personales trata Sorteo360, para qué y con qué derechos, de
        acuerdo con la Ley 172-13 sobre protección de datos personales de la República Dominicana.
        El responsable de Sorteo360 es {TITULAR.razonSocial} (RNC {TITULAR.rnc}),{' '}
        {TITULAR.domicilio}.
      </p>

      <h2>1. Dos papeles distintos</h2>
      <ul>
        <li>
          <strong>Datos de los participantes de un sorteo</strong> (por ejemplo, los empleados que
          carga una empresa): la empresa cliente es la responsable de esos datos y decide para qué
          se usan. Sorteo360 los trata por encargo suyo, solo para prestarle el servicio.
        </li>
        <li>
          <strong>Datos de las cuentas</strong> (quien registra la empresa y los usuarios que
          invita): en esos somos los responsables.
        </li>
      </ul>

      <h2>2. Qué datos tratamos</h2>
      <ul>
        <li>
          <strong>De los participantes</strong>: nombre y apellido, identificador o número de
          empleado, departamento, ubicación o empresa, correo y teléfono si el cliente los carga, el
          número de boleto y el código QR, la hora de llegada al evento y, si gana, el premio, la
          entrega y la firma de recibido.
        </li>
        <li>
          <strong>De los usuarios de la cuenta</strong>: nombre, correo, empresa, rol, y el registro
          de lo que hacen en la plataforma (bitácora). La contraseña la guarda el servicio de
          identidad cifrada; nadie de nuestro equipo puede verla.
        </li>
        <li>
          <strong>Del pago</strong>: el plan, el período y la referencia de la transferencia. No
          recibimos ni guardamos datos de tarjetas.
        </li>
        <li>
          <strong>Técnicos</strong>: dirección IP, navegador y registros de acceso, para la
          seguridad del servicio.
        </li>
      </ul>

      <h2>3. Para qué los usamos</h2>
      <ul>
        <li>Crear y activar la cuenta, y permitir el acceso de sus usuarios.</li>
        <li>
          Organizar el sorteo: check-in, elección al azar de ganadores, pantalla del evento, entrega
          de premios, acta y certificados.
        </li>
        <li>Enviar los correos del servicio (activación, avisos del plan y del sorteo).</li>
        <li>Confirmar pagos, cumplir obligaciones legales y proteger el servicio contra abusos.</li>
      </ul>
      <p>
        No vendemos datos personales, no los usamos para publicidad y no los compartimos con otras
        empresas clientes.
      </p>

      <h2>4. Base legal</h2>
      <p>
        Los datos de la cuenta se tratan para cumplir el contrato del servicio y por su
        consentimiento al registrarse. Los de los participantes, según las instrucciones de la
        empresa cliente, que debe contar con una base legal para cargarlos (por ejemplo, la relación
        laboral o el consentimiento de cada participante) e informarles del sorteo.
      </p>

      <h2>5. Con quién se comparten</h2>
      <ul>
        <li>
          Con los proveedores que necesitamos para operar (alojamiento de servidores y envío de
          correos), obligados por contrato a la misma confidencialidad.
        </li>
        <li>
          Dentro del evento: el nombre y los datos que el cliente elija mostrar aparecen en la
          pantalla del sorteo y en el acta.
        </li>
        <li>Con autoridades, solo cuando una ley o una orden judicial lo exija.</li>
      </ul>

      <h2>6. Cuánto tiempo se guardan</h2>
      <p>
        Mientras la cuenta esté activa. Si el cliente cierra la cuenta, borramos sus datos en un
        plazo de 90 días, salvo lo que la ley obligue a conservar (por ejemplo, registros contables
        de los pagos). Las copias de seguridad se renuevan y se eliminan en sus ciclos normales.
      </p>

      <h2>7. Seguridad</h2>
      <p>
        Usamos conexiones cifradas, contraseñas guardadas con cifrado, permisos por rol, separación
        de los datos de cada empresa cliente y copias de seguridad. Si ocurre un incidente que
        afecte datos personales, avisaremos a los clientes afectados sin demora.
      </p>

      <h2>8. Sus derechos</h2>
      <p>
        Toda persona puede pedir acceso a sus datos, su corrección, su eliminación o oponerse a su
        uso, de acuerdo con la Ley 172-13.
      </p>
      <ul>
        <li>
          Si es participante de un sorteo, la vía es la empresa que organizó el sorteo; si nos
          escribe, le pasaremos la solicitud.
        </li>
        <li>Si es usuario de una cuenta, escríbanos a {TITULAR.correoPrivacidad}.</li>
      </ul>
      <p>Respondemos en un plazo máximo de 10 días hábiles.</p>

      <h2>9. Cookies y almacenamiento del navegador</h2>
      <p>
        Usamos solo lo necesario para mantener la sesión, recordar preferencias (como el modo
        oscuro) y permitir que el check-in funcione sin internet. No usamos cookies de publicidad.
      </p>

      <h2>10. Cambios</h2>
      <p>
        Si cambiamos esta política, publicaremos la nueva versión aquí con su fecha y avisaremos por
        correo a los administradores de cuenta cuando el cambio sea importante.
      </p>
    </LegalLayout>
  )
}
