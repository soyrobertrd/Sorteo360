using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Errors;

/// <summary>
/// No hay nada en lo que inscribirse por ahí. <b>Un solo código para todos los motivos</b> —el
/// token no existe, ya se gastó, se revocó; el evento no existe, es privado y no traen token,
/// fue cancelado, se cerró o su última fecha ya pasó— y a propósito: separarlos le confirmaría a
/// un desconocido que el evento existe y que MITUR lo escondió, o que la dirección que probó es
/// una invitada de verdad (spec <c>06</c> §1 y §4.6).
/// <para>
/// <b>Es distinto de <see cref="PublicEventNotFoundError"/> y las dos rutas lo comparten.</b> El
/// detalle público sirve un evento cerrado —una convocatoria terminada es historia pública y un
/// enlace compartido la semana pasada tiene que seguir resolviendo—, así que ese <c>404</c> y
/// este cubren conjuntos de motivos que no se solapan. Resolver un token y mandar el formulario
/// sí los comparten enteros, y por eso llevan el mismo.
/// </para>
/// <para>
/// <b>La puerta cerrada de la ventana de inscripción no cae acá</b> (spec <c>06</c> §5.3): ahí
/// el evento está en la lista y su detalle lo está sirviendo, así que un <c>404</c> contradiría
/// la página de dos clics atrás. Es un <c>409</c> con código propio,
/// <see cref="RegistrationsClosedError"/>.
/// </para>
/// </summary>
public sealed record PublicRegistrationNotFoundError()
    : BaseError(
        "No se pudo abrir la inscripción con ese enlace.",
        ErrorCode,
        ErrorKind.NotFound)
{
    public const string ErrorCode = "PUBLIC_REGISTRATION_NOT_FOUND";
}
