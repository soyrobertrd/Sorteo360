using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.DTOs;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Registrations;

/// <summary>
/// Lo que hace el portal de registro: abrir un enlace y mandar el formulario (spec <c>06</c> §1).
/// <para>
/// Las dos operaciones se intercalan con events-service, así que no caben dentro de una
/// transacción y no viven en el repositorio, igual que el lanzamiento de una campaña.
/// </para>
/// </summary>
public interface IRegistrationService
{
    /// <summary>
    /// Qué hay detrás de un token: el evento, y los datos del invitado solo si el evento es
    /// privado (spec <c>06</c> §2.2).
    /// </summary>
    Task<Result<ResolvedInvitation, BaseError>> ResolveAsync(string token, CancellationToken cancellationToken);

    /// <summary>
    /// Inscribe a una persona. Un reenvío del mismo formulario devuelve la inscripción que ya
    /// había, sin crear fila y sin pisar nada (spec <c>06</c> §4.1).
    /// </summary>
    Task<Result<RegistrationOutcome, BaseError>> RegisterAsync(RegisterDto dto, CancellationToken cancellationToken);

    /// <summary>
    /// La entrada que lleva ese <c>qr_code</c> (spec <c>06</c> §2.3): referencia, evento, nombre y
    /// empresa. <see cref="Maybe{T}.None"/> cuando el código no existe, que las dos rutas públicas
    /// contestan con un <c>404</c>.
    /// <para>
    /// <b>El evento se lee en vivo y no se filtra por estado</b>: una entrada sigue sirviendo
    /// aunque el evento esté cancelado o cerrado, porque quien ya tiene la suya puede mirarla
    /// (spec <c>06</c> §4.6). events-service caído sale como <c>503</c>, no como un <c>404</c>.
    /// </para>
    /// </summary>
    Task<Result<Maybe<RegistrationTicket>, BaseError>> GetTicketAsync(string qrCode, CancellationToken cancellationToken);

    /// <summary>
    /// Si existe una entrada con ese <c>qr_code</c>. Es lo que separa la imagen del QR de un
    /// <c>404</c> (spec <c>06</c> §4.5): la imagen no lee el evento, así que confirmar el código
    /// basta, y por eso sigue sirviendo aunque events-service esté caído.
    /// </summary>
    Task<Result<bool, BaseError>> QrCodeExistsAsync(string qrCode, CancellationToken cancellationToken);

    /// <summary>
    /// Reintenta a mano llevar al CRM una inscripción que quedó pendiente (spec <c>07</c> §6.1),
    /// <b>por el mismo camino que la inscripción pública</b>: un <c>409</c> enlaza la ficha que ya
    /// estaba y vuelve a mirar su <c>canBeEmailed</c>. Una ya sincronizada vuelve tal cual, sin
    /// preguntarle nada al CRM; con el CRM todavía caído sale <c>503 CRM_UNAVAILABLE</c> y la fila
    /// no cambia. <see cref="Maybe{T}.None"/> si ese id no existe.
    /// </summary>
    Task<Result<Maybe<CrmSyncRetry>, BaseError>> RetryCrmSyncAsync(Guid registrationId, CancellationToken cancellationToken);
}
