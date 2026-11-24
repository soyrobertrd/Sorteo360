using System.Security.Cryptography;
using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Crm;
using Sorteo360.Convocatorias.Core.DTOs;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Events;
using Sorteo360.Convocatorias.Core.Portal;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Registrations;

/// <inheritdoc cref="IRegistrationService" />
/// <remarks>
/// <b>Un token, dos políticas</b> (spec <c>06</c> §2.2). En un evento privado admite exactamente
/// una inscripción, atada a ese invitado, y rellena sus datos. En uno público admite cuantas
/// hagan falta, no rellena nada, y cada inscripción anota a quién se lo mandamos — que es lo que
/// vuelve representable un reenvío en vez de dejarlo como un misterio.
/// <para>
/// <b>Todo rechazo sale por el mismo <see cref="PublicRegistrationNotFoundError"/>.</b> Separar
/// "ese token no existe" de "ese token ya se usó" le diría a quien prueba direcciones cuál de
/// ellas es una invitada de verdad.
/// </para>
/// <para>
/// <b>El CRM no puede rechazar una inscripción</b> (spec <c>06</c> §4.3). Es la única desviación
/// de la regla de <c>503 CRM_UNAVAILABLE</c> que hay en la plataforma, y es deliberada: esa regla
/// se escribió para nuestro propio personal en una pantalla de administración, donde reintentar
/// en un minuto no cuesta nada.
/// </para>
/// </remarks>
public sealed class RegistrationService : IRegistrationService
{
    private readonly IRegistrationRepository _registrations;
    private readonly IRegistrationWindowRepository _windows;
    private readonly IEventDirectory _events;
    private readonly ICrmContactDirectory _contacts;
    private readonly TimeProvider _clock;

    public RegistrationService(
        IRegistrationRepository registrations,
        IRegistrationWindowRepository windows,
        IEventDirectory events,
        ICrmContactDirectory contacts,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(contacts);
        ArgumentNullException.ThrowIfNull(clock);

        _registrations = registrations;
        _windows = windows;
        _events = events;
        _contacts = contacts;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<Result<ResolvedInvitation, BaseError>> ResolveAsync(
        string token,
        CancellationToken cancellationToken)
    {
        var invitation = await FindInvitationAsync(token, cancellationToken);

        if (invitation.IsFailure)
            return Fail<ResolvedInvitation>(invitation.Error);

        if (invitation.Value.HasNoValue)
            return NotFound<ResolvedInvitation>();

        var found = invitation.Value.Value;

        var openEvent = await ReadOpenEventAsync(found.EventId, cancellationToken);

        if (openEvent.IsFailure)
            return Fail<ResolvedInvitation>(openEvent.Error);

        var summary = openEvent.Value;

        // Un evento público no gasta su token y no rellena nada: el enlace está pensado para
        // reenviarse, y rellenar sobre uno reenviado le enseñaría a quien lo recibe el nombre y
        // la dirección del invitado original.
        if (!summary.IsPublic)
        {
            var spent = await _registrations.IsInvitationSpentAsync(found.RecipientId, cancellationToken);

            if (spent.IsFailure)
                return Fail<ResolvedInvitation>(spent.Error);

            // Gastado sale por el mismo 404 que uno inventado. El invitado ya tiene su entrada, y
            // decirle "ya te inscribiste" desde esta ruta sería la única respuesta del portal que
            // confirma que una dirección concreta está invitada.
            if (spent.Value)
                return NotFound<ResolvedInvitation>();
        }

        // La puerta va después del token gastado: un 409 ahí le confirmaría a quien lo trae que
        // el token fue real. Y con la puerta cerrada tampoco resuelve un token privado vigente
        // (spec 06 §5.4): el invitado aterriza en "inscripciones cerradas" y no en un formulario
        // relleno que no puede mandar. Es la parte que la spec marca como la primera a revisar.
        var shut = await RefuseIfShutAsync(summary.Id, cancellationToken);

        if (shut is not null)
            return Fail<ResolvedInvitation>(shut);

        return Result.Success<ResolvedInvitation, BaseError>(
            new ResolvedInvitation(
                summary,
                summary.IsPublic ? null : new InvitationPrefill(found.FirstName, found.LastName, found.Email)));
    }

    /// <inheritdoc />
    public async Task<Result<RegistrationOutcome, BaseError>> RegisterAsync(
        RegisterDto dto,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var openEvent = await ReadOpenEventAsync(dto.EventId, cancellationToken);

        if (openEvent.IsFailure)
            return Fail<RegistrationOutcome>(openEvent.Error);

        var summary = openEvent.Value;

        // Quién llama, sin preguntar todavía si su token se gastó. El orden de los tres pasos que
        // siguen es la parte delicada y está explicado en `AdmitAsync`.
        var caller = await AdmitAsync(dto, summary, cancellationToken);

        if (caller.IsFailure)
            return Fail<RegistrationOutcome>(caller.Error);

        var invitation = caller.Value;

        // El reenvío del mismo formulario, que es lo que pasa cuando alguien perdió el correo.
        // Se contesta con la inscripción que ya había y sus mismos códigos: no se crea fila y
        // no se pisa lo que escribió la primera vez (spec 06 §4.1).
        var existing = await _registrations.FindByEmailAsync(
            dto.EventId,
            EmailAddressShape.Normalize(dto.Email),
            cancellationToken);

        if (existing.IsFailure)
            return Fail<RegistrationOutcome>(existing.Error);

        if (existing.Value.HasValue)
            return Result.Success<RegistrationOutcome, BaseError>(
                new RegistrationOutcome(existing.Value.Value, summary, IsNew: false));

        // Y recién acá se pregunta si el token ya se gastó. Va **después** de la lectura de
        // arriba a propósito: en un evento privado el token se gasta con la primera inscripción,
        // así que preguntarlo antes le negaría a la propia invitada volver a mandar su
        // formulario — que es justo el caso que §4.1 contesta con un 200. Quien pasa por acá con
        // un token gastado y una dirección que no está inscrita es otra persona, y esa sí se
        // rechaza.
        if (invitation.HasValue && !summary.IsPublic)
        {
            var spent = await _registrations.IsInvitationSpentAsync(
                invitation.Value.RecipientId,
                cancellationToken);

            if (spent.IsFailure)
                return Fail<RegistrationOutcome>(spent.Error);

            if (spent.Value)
                return NotFound<RegistrationOutcome>();
        }

        // La puerta cerrada frena altas y no a quien ya entró, por eso va después del reenvío de
        // arriba: quien perdió su correo recupera su inscripción aunque la ventana esté cerrada
        // (spec 06 §5.3). Y después del token gastado, que sigue saliendo por el 404 sin revelar
        // que el token fue real. Lo que llega acá es una persona nueva, y a esa se le niega.
        var shut = await RefuseIfShutAsync(dto.EventId, cancellationToken);

        if (shut is not null)
            return Fail<RegistrationOutcome>(shut);

        // El CRM, antes de escribir la fila: llegar hasta acá significa que la inscripción va a
        // existir pase lo que pase, y así nace con su estado de sincronización ya puesto. El
        // porqué y sus dos costos están en `CrmSyncOutcome`.
        var crmSync = await SyncWithCrmAsync(dto.FirstName, dto.LastName, dto.Email, dto.Phone, cancellationToken);

        var created = await _registrations.CreateAsync(
            dto,
            invitation.HasValue ? invitation.Value.RecipientId : null,
            crmSync,
            cancellationToken);

        return created.IsFailure
            ? Fail<RegistrationOutcome>(created.Error)
            : Result.Success<RegistrationOutcome, BaseError>(
                new RegistrationOutcome(created.Value.Registration, summary, created.Value.IsNew));
    }

    /// <inheritdoc />
    public async Task<Result<Maybe<RegistrationTicket>, BaseError>> GetTicketAsync(
        string qrCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(qrCode))
            return Result.Success<Maybe<RegistrationTicket>, BaseError>(Maybe<RegistrationTicket>.None);

        var found = await _registrations.FindByQrCodeAsync(qrCode, cancellationToken);

        if (found.IsFailure)
            return Fail<Maybe<RegistrationTicket>>(found.Error);

        if (found.Value.HasNoValue)
            return Result.Success<Maybe<RegistrationTicket>, BaseError>(Maybe<RegistrationTicket>.None);

        var registration = found.Value.Value;

        // El evento sin el filtro de "todavía admite inscripciones": la entrada sigue sirviendo en
        // un evento cancelado o cerrado (§4.6). Solo se cae si events-service no contesta, que sale
        // como 503, o si de verdad no conoce ese id.
        var summary = await _events.GetEventAsync(registration.EventId, cancellationToken);

        if (summary.IsFailure)
            return Fail<Maybe<RegistrationTicket>>(summary.Error);

        if (summary.Value.HasNoValue)
            return Result.Success<Maybe<RegistrationTicket>, BaseError>(Maybe<RegistrationTicket>.None);

        return Result.Success<Maybe<RegistrationTicket>, BaseError>(
            Maybe.From(new RegistrationTicket(
                registration.Reference,
                summary.Value.Value,
                registration.FullName,
                registration.Company)));
    }

    /// <inheritdoc />
    public Task<Result<bool, BaseError>> QrCodeExistsAsync(string qrCode, CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(qrCode)
            ? Task.FromResult(Result.Success<bool, BaseError>(false))
            : _registrations.QrCodeExistsAsync(qrCode, cancellationToken);

    /// <inheritdoc />
    public async Task<Result<Maybe<CrmSyncRetry>, BaseError>> RetryCrmSyncAsync(
        Guid registrationId,
        CancellationToken cancellationToken)
    {
        var found = await _registrations.FindByIdAsync(registrationId, cancellationToken);

        if (found.IsFailure)
            return Fail<Maybe<CrmSyncRetry>>(found.Error);

        if (found.Value.HasNoValue)
            return Result.Success<Maybe<CrmSyncRetry>, BaseError>(Maybe<CrmSyncRetry>.None);

        var registration = found.Value.Value;

        // Ya sincronizada: no se le pregunta nada al CRM, igual que al reenvío del formulario.
        // Pulsar dos veces el botón no puede costar una segunda alta.
        if (registration.CrmContactId is not null)
            return Result.Success<Maybe<CrmSyncRetry>, BaseError>(
                Maybe.From(new CrmSyncRetry(registration, CanBeEmailed: null)));

        // El mismo camino que la inscripción pública, con lo que la persona escribió entonces.
        var outcome = await SyncWithCrmAsync(
            registration.FirstName,
            registration.LastName,
            registration.Email,
            registration.Phone,
            cancellationToken);

        // Acá sí vale la regla del 503: quien pulsa es nuestro personal en una pantalla de
        // administración, y reintentar en un minuto no le cuesta nada (spec 06 §4.3). La fila se
        // queda pendiente tal como estaba.
        if (outcome.ContactId is null)
            return Fail<Maybe<CrmSyncRetry>>(new CrmUnavailableError());

        var recorded = await _registrations.RecordCrmSyncAsync(registrationId, outcome, cancellationToken);

        if (recorded.IsFailure)
            return Fail<Maybe<CrmSyncRetry>>(recorded.Error);

        return Result.Success<Maybe<CrmSyncRetry>, BaseError>(
            recorded.Value.Map(updated => new CrmSyncRetry(updated, outcome.CanBeEmailed)));
    }

    /// <summary>
    /// Lleva la inscripción al CRM, o anota que no se pudo (spec <c>06</c> §4.2 y §4.3).
    /// <para>
    /// <b>Crear y nunca pisar.</b> Con la dirección libre se crea el contacto con su
    /// consentimiento; con la dirección ocupada se enlaza la ficha que ya está y <b>el CRM se
    /// queda exactamente como estaba</b>. La regla de unicidad del cliente es lo que lo hace
    /// necesario: al decretar una persona por dirección, <c>info@empresa.com</c> <i>es</i> una
    /// persona, así que un empleado inscribiéndose con el buzón de su empresa pisaría a quien lo
    /// tiene. Y no sabemos distinguir una corrección de una errata: un desconocido escribiendo
    /// "J. Perez" no puede reescribir "Juan Pérez" en el sistema de registro.
    /// </para>
    /// <para>
    /// <b>El CRM caído no rechaza a nadie.</b> Se anota el sello, se guarda la inscripción y
    /// <c>07</c> saca el pendiente para que una persona lo reconcilie a mano. Un
    /// <c>503</c> acá sería un portal del gobierno caído, ante un desconocido, por un servicio
    /// del que nunca oyó hablar.
    /// </para>
    /// </summary>
    private async Task<CrmSyncOutcome> SyncWithCrmAsync(
        string firstName,
        string lastName,
        string email,
        string? phone,
        CancellationToken cancellationToken)
    {
        var found = await _contacts.FindByEmailAsync(email, cancellationToken);

        if (found.IsFailure)
            return CrmSyncOutcome.Pending(_clock.GetUtcNow());

        if (found.Value.HasValue)
            return LinkedToExistingContact(firstName, lastName, found.Value.Value);

        var created = await _contacts.CreateContactAsync(
            new NewCrmContact(
                firstName.Trim(),
                lastName.Trim(),
                // Cruda: normalizar es del CRM, y una segunda regla de este lado dejaría filas
                // que su columna ya no puede distinguir (spec 05 §5).
                email.Trim(),
                OptionalText.Trimmed(phone),
                ConsentSources.SelfRegistration),
            cancellationToken);

        if (created.IsFailure)
            return CrmSyncOutcome.Pending(_clock.GetUtcNow());

        // Un 409 es que alguien ocupó la dirección entre la lectura de arriba y esta alta, y el
        // CRM devuelve su ficha dentro de la respuesta. Sale por la misma puerta que la lectura
        // que la encuentra: es el mismo caso, descubierto un momento después.
        return created.Value.AlreadyExisted
            ? LinkedToExistingContact(firstName, lastName, created.Value.Contact)
            : CrmSyncOutcome.Synced(created.Value.Contact, contactDetailsDiffer: false);
    }

    /// <summary>
    /// La dirección ya estaba en el CRM: se enlaza y no se escribe nada allí. Lo único que queda
    /// por decidir es si lo que escribió coincide con la ficha, porque de eso sale la lista que
    /// <c>07</c> da a revisar a mano con el <c>PATCH</c> que ya existe.
    /// <para>
    /// La comparación entera vive en <see cref="PersonNameShape"/>, recorte incluido: una ficha
    /// del CRM sin nombre llega acá con el relleno que le puso el directorio, así que discrepa y
    /// alguien la mira, que es lo correcto.
    /// </para>
    /// </summary>
    private static CrmSyncOutcome LinkedToExistingContact(string firstName, string lastName, CrmContact contact) =>
        CrmSyncOutcome.Synced(
            contact,
            !PersonNameShape.Matches(firstName, lastName, contact.FirstName, contact.LastName));

    /// <summary>
    /// Si quien manda el formulario puede estar acá, y con qué invitación. Es donde se decide
    /// §1.1: con token se valida y se ata la referencia, y sin token se acepta solo si el evento
    /// es público.
    /// <para>
    /// <b>No mira si el token se gastó.</b> Eso es lo único que se decide más tarde, después de
    /// leer si esa dirección ya está inscrita, porque un token privado se gasta con la primera
    /// inscripción y preguntarlo acá le cerraría la puerta a la propia invitada reenviando su
    /// formulario.
    /// </para>
    /// </summary>
    private async Task<Result<Maybe<Invitation>, BaseError>> AdmitAsync(
        RegisterDto dto,
        EventSummary summary,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Token))
        {
            // Quien llega desde la cartelera no tiene token, y eso es la mitad del camino que
            // §1.1 se negó a partir en dos endpoints. A un evento privado no se entra así: su
            // lista de invitados es lo único que lo hace privado.
            return summary.IsPublic
                ? Result.Success<Maybe<Invitation>, BaseError>(Maybe<Invitation>.None)
                : NotFound<Maybe<Invitation>>();
        }

        var invitation = await FindInvitationAsync(dto.Token, cancellationToken);

        if (invitation.IsFailure)
            return invitation;

        if (invitation.Value.HasNoValue)
            return NotFound<Maybe<Invitation>>();

        // Un token vale para su evento y para ningún otro. Sin esto, el token de un evento
        // público cualquiera abriría la puerta de todos los privados.
        return invitation.Value.Value.EventId == dto.EventId
            ? invitation
            : NotFound<Maybe<Invitation>>();
    }

    /// <summary>
    /// El evento, si todavía admite inscripciones. Un id desconocido, uno cancelado, uno cerrado
    /// y uno cuya última fecha ya pasó salen por la misma puerta (spec <c>06</c> §4.6).
    /// <para>
    /// events-service caído no es eso y no se disfraza de eso: sale como
    /// <c>503 EVENTS_UNAVAILABLE</c>, tal como lo devuelve el directorio, porque decirle a
    /// alguien que su invitación no existe cuando lo que pasa es que el vecino no contesta le
    /// hace tirar un enlace que sirve.
    /// </para>
    /// </summary>
    private async Task<Result<EventSummary, BaseError>> ReadOpenEventAsync(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var result = await _events.GetEventAsync(eventId, cancellationToken);

        if (result.IsFailure)
            return Fail<EventSummary>(result.Error);

        if (result.Value.HasNoValue)
            return NotFound<EventSummary>();

        var summary = result.Value.Value;

        return summary.AcceptsRegistrationsOn(Today())
            ? Result.Success<EventSummary, BaseError>(summary)
            : NotFound<EventSummary>();
    }

    /// <summary>
    /// El <c>409 REGISTRATIONS_CLOSED</c> si la ventana del evento está cerrada, el fallo de la
    /// lectura si no se pudo leer, o <c>null</c> si la puerta está abierta (spec <c>06</c> §5.3).
    /// <para>
    /// Corre siempre <b>después</b> de la lectura del evento: uno cancelado, privado o inexistente
    /// sale por el <c>404</c> que no dice por qué, y solo un evento que el portal sí muestra puede
    /// contestar que la puerta está cerrada.
    /// </para>
    /// </summary>
    private async Task<BaseError?> RefuseIfShutAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var window = await _windows.GetAsync(eventId, cancellationToken);

        if (window.IsFailure)
            return window.Error;

        return window.Value.IsOpen ? null : new RegistrationsClosedError();
    }

    /// <summary>
    /// La fila del destinatario que lleva ese token, aceptada con una comparación en tiempo fijo.
    /// <para>
    /// <b>El índice encuentra y la comparación acepta</b> (spec <c>06</c> §2.1). Buscar por hash
    /// es lo que hace la lectura barata; volver a comparar los bytes con
    /// <see cref="CryptographicOperations.FixedTimeEquals"/> es lo que impide que el tiempo de
    /// respuesta diga cuántos bytes acertó quien está probando tokens.
    /// </para>
    /// </summary>
    private async Task<Result<Maybe<Invitation>, BaseError>> FindInvitationAsync(
        string token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Result.Success<Maybe<Invitation>, BaseError>(Maybe<Invitation>.None);

        var hash = PortalToken.HashOf(token);

        var found = await _registrations.FindInvitationAsync(hash, cancellationToken);

        if (found.IsFailure || found.Value.HasNoValue)
            return found;

        return CryptographicOperations.FixedTimeEquals(found.Value.Value.TokenHash, hash)
            ? found
            : Result.Success<Maybe<Invitation>, BaseError>(Maybe<Invitation>.None);
    }

    /// <summary>Hoy en UTC, que es como se guarda toda fecha del servicio.</summary>
    private DateOnly Today() => DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

    /// <summary>
    /// El único rechazo del portal, en la forma que pide quien llama. Todas las razones —token
    /// desconocido, gastado o revocado, evento inexistente, privado, cancelado, cerrado o
    /// pasado— salen por acá a propósito (spec <c>06</c> §4.6): separarlas le diría a quien
    /// prueba direcciones cuál es la que de verdad está invitada.
    /// </summary>
    private static Result<T, BaseError> NotFound<T>() =>
        Result.Failure<T, BaseError>(new PublicRegistrationNotFoundError());

    /// <summary>
    /// Reenvía el error de un vecino tal como vino, cambiando solo el tipo que lo envuelve. Lo
    /// que llega por acá es el <c>503</c> del directorio de eventos, que no se traduce a un
    /// <c>404</c>: decirle a alguien que su invitación no existe cuando lo que pasa es que el
    /// vecino no contesta le hace tirar un enlace que sirve.
    /// </summary>
    private static Result<T, BaseError> Fail<T>(BaseError error) =>
        Result.Failure<T, BaseError>(error);
}
