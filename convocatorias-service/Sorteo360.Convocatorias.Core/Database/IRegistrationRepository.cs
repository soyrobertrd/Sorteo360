using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.DTOs;
using Sorteo360.Convocatorias.Core.Registrations;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Database;

/// <summary>
/// La tabla de inscripciones y la lectura del token que las trae (spec <c>06</c> §4).
/// </summary>
public interface IRegistrationRepository
{
    /// <summary>
    /// A quién apunta un token, buscado <b>por su hash</b>. <see cref="Maybe{T}.None"/> cuando
    /// ningún destinatario lo lleva, que es tanto un token inventado como uno revocado al sacar
    /// a esa persona de la campaña: los dos son lo mismo visto desde el portal.
    /// <para>
    /// Devuelve el hash guardado junto al resto para que quien llama lo compare en tiempo fijo
    /// (spec <c>06</c> §2.1). El texto plano no está en ninguna columna y esta lectura no lo
    /// recibe.
    /// </para>
    /// </summary>
    Task<Result<Maybe<Invitation>, BaseError>> FindInvitationAsync(
        byte[] tokenHash,
        CancellationToken cancellationToken);

    /// <summary>
    /// La inscripción de una dirección en un evento, si ya existe. Es la mitad amable de la
    /// regla de §4.1: un reenvío del formulario devuelve esto en vez de fallar.
    /// </summary>
    Task<Result<Maybe<Registration>, BaseError>> FindByEmailAsync(
        Guid eventId,
        string emailNormalized,
        CancellationToken cancellationToken);

    /// <summary>
    /// Si ese destinatario ya trajo una inscripción. Es lo que gasta un token privado, que
    /// admite exactamente una (spec <c>06</c> §2.2); en un evento público no se pregunta, porque
    /// ahí el token admite cuantas hagan falta.
    /// </summary>
    Task<Result<bool, BaseError>> IsInvitationSpentAsync(
        Guid campaignRecipientId,
        CancellationToken cancellationToken);

    /// <summary>
    /// La inscripción que lleva ese <c>qr_code</c>, o <see cref="Maybe{T}.None"/> si ninguna lo
    /// hace. Es la lectura de la página de la entrada (spec <c>06</c> §2.3): entra por el índice
    /// único <c>ux_registration_qr_code</c>, así que un código inventado no encuentra nada y sale
    /// por el <c>404</c> de las dos rutas.
    /// </summary>
    Task<Result<Maybe<Registration>, BaseError>> FindByQrCodeAsync(
        string qrCode,
        CancellationToken cancellationToken);

    /// <summary>
    /// Si existe una inscripción con ese <c>qr_code</c>. Es lo único que la imagen del QR necesita
    /// saber antes de dibujarse (spec <c>06</c> §4.5): la imagen codifica una URL armada solo con
    /// el código, sin leer el evento, así que confirmar que el código existe basta para separar un
    /// PNG de un <c>404</c>.
    /// </summary>
    Task<Result<bool, BaseError>> QrCodeExistsAsync(
        string qrCode,
        CancellationToken cancellationToken);

    /// <summary>La inscripción con ese id, o <see cref="Maybe{T}.None"/> si no existe.</summary>
    Task<Result<Maybe<Registration>, BaseError>> FindByIdAsync(
        Guid registrationId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Escribe en una fila pendiente lo que contestó el CRM al reintentarla (spec <c>07</c> §6.1)
    /// y la devuelve como quedó. <b>Solo si sigue sin contacto</b>, en la misma sentencia: si otro
    /// reintento ganó, esa fila se queda con el suyo. El sello del fallo se conserva como historia.
    /// </summary>
    Task<Result<Maybe<Registration>, BaseError>> RecordCrmSyncAsync(
        Guid registrationId,
        CrmSyncOutcome crmSync,
        CancellationToken cancellationToken);

    /// <summary>
    /// Las inscripciones del evento que todavía no recibieron el aviso de cancelación, las que
    /// tienen <c>cancellation_notified_at</c> nulo (spec <c>06</c> §4.7). Es la lista de una
    /// pasada del aviso, y el cubo de <c>07</c> §6.5 pregunta lo mismo.
    /// </summary>
    Task<Result<IReadOnlyList<Registration>, BaseError>> ListAwaitingCancellationNoticeAsync(
        Guid eventId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Sella <c>cancellation_notified_at</c> en esa fila <b>solo si sigue sin sello</b>, en una
    /// sola sentencia. Falso significa que otra pasada ya la reclamó y el correo es suyo: es lo que
    /// impide que dos pulsaciones simultáneas le escriban dos veces a la misma persona.
    /// </summary>
    Task<Result<bool, BaseError>> ClaimCancellationNoticeAsync(
        Guid registrationId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Quita el sello de una fila reclamada cuyo correo no salió, para que vuelva a contar como
    /// sin avisar.
    /// </summary>
    Task<UnitResult<BaseError>> ReleaseCancellationNoticeAsync(
        Guid registrationId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Escribe la inscripción, acuñando sus dos códigos (spec <c>06</c> §2.3).
    /// <para>
    /// <b>Una carrera contra la restricción de unicidad no es un fallo</b>: dos envíos
    /// simultáneos de la misma dirección pasan los dos por <see cref="FindByEmailAsync"/>, y el
    /// que pierde lee la fila del que ganó y la devuelve. Por eso el resultado dice si la creó,
    /// que es lo que separa un <c>201</c> de un <c>200</c>.
    /// </para>
    /// <para>
    /// <b>El desenlace del CRM llega ya decidido</b> y se escribe en la misma fila, de una sola
    /// vez. Por qué se decide antes y no después está en <see cref="CrmSyncOutcome"/>, con sus
    /// dos costos.
    /// </para>
    /// </summary>
    /// <param name="dto">Lo que mandó el formulario.</param>
    /// <param name="campaignRecipientId">El destinatario cuyo enlace la trajo, o nulo.</param>
    /// <param name="crmSync">En qué acabó llevarla al CRM.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<Result<CreatedRegistration, BaseError>> CreateAsync(
        RegisterDto dto,
        Guid? campaignRecipientId,
        CrmSyncOutcome crmSync,
        CancellationToken cancellationToken);
}

/// <summary>La inscripción escrita, y si esta petición fue la que la creó.</summary>
/// <param name="Registration">La fila, nueva o la que ya estaba.</param>
/// <param name="IsNew">Falso cuando otra petición ganó la carrera.</param>
public sealed record CreatedRegistration(Registration Registration, bool IsNew);
