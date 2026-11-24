using System.Security.Cryptography;
using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Crm;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Portal;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Unsubscribe;

/// <inheritdoc cref="IUnsubscribeService" />
public sealed class UnsubscribeService : IUnsubscribeService
{
    private readonly ICampaignRepository _repository;
    private readonly ICrmContactDirectory _crm;
    private readonly TimeProvider _clock;

    public UnsubscribeService(
        ICampaignRepository repository,
        ICrmContactDirectory crm,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(crm);
        ArgumentNullException.ThrowIfNull(clock);

        _repository = repository;
        _crm = crm;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<Result<string, BaseError>> UnsubscribeAsync(string token, CancellationToken cancellationToken)
    {
        var found = await FindTargetAsync(token, cancellationToken);

        if (found.IsFailure)
            return Result.Failure<string, BaseError>(found.Error);

        // Token que no casa ninguna fila: un solo 404, sin decir si el token no existe o si nunca
        // hubo baja que dar. No hay nada más que un token pueda fallar acá.
        if (found.Value.HasNoValue)
            return Result.Failure<string, BaseError>(new PublicUnsubscribeNotFoundError());

        var target = found.Value.Value;
        var masked = MaskedEmail.Of(target.Email);

        var written = await WriteThroughAsync(
            target.RecipientId,
            target.ContactId,
            target.UnsubscribedAt,
            target.UnsubscribeSyncedAt,
            cancellationToken);

        // El fallo del empujón no llega hasta acá: con el CRM caído la baja igual triunfa del lado
        // de la persona. Solo sube un fallo de la base.
        return written.IsFailure
            ? Result.Failure<string, BaseError>(written.Error)
            : Result.Success<string, BaseError>(masked);
    }

    /// <inheritdoc />
    public async Task<Result<Maybe<RecipientUnsubscribe>, BaseError>> RetrySyncAsync(
        Guid recipientId,
        CancellationToken cancellationToken)
    {
        var found = await _repository.FindRecipientUnsubscribeAsync(recipientId, cancellationToken);

        if (found.IsFailure || found.Value.HasNoValue)
            return found;

        var recipient = found.Value.Value;

        // Sin clic no hay baja que sincronizar, y empujarla sería dar de baja a quien no lo pidió.
        if (recipient.UnsubscribedAt is null)
            return Result.Failure<Maybe<RecipientUnsubscribe>, BaseError>(new RecipientNotUnsubscribedError());

        var written = await WriteThroughAsync(
            recipient.RecipientId,
            recipient.ContactId,
            recipient.UnsubscribedAt,
            recipient.UnsubscribeSyncedAt,
            cancellationToken);

        if (written.IsFailure)
            return Result.Failure<Maybe<RecipientUnsubscribe>, BaseError>(written.Error);

        // A diferencia del portal, acá sí vale la regla del 503: quien pulsa es nuestro personal y
        // tiene que saber que la baja sigue sin llegar.
        if (written.Value is null)
            return Result.Failure<Maybe<RecipientUnsubscribe>, BaseError>(new CrmUnavailableError());

        return Maybe.From(recipient with { UnsubscribeSyncedAt = written.Value });
    }

    /// <summary>
    /// El empujón al CRM y el sello, <b>un solo camino para el portal y para el reintento</b>
    /// (spec <c>06</c> §6 y <c>07</c> §6.2). Devuelve cuándo quedó sincronizada, o nulo si el CRM
    /// no la recibió; lo que cada llamador hace con ese nulo es suyo.
    /// </summary>
    private async Task<Result<DateTimeOffset?, BaseError>> WriteThroughAsync(
        Guid recipientId,
        Guid contactId,
        DateTimeOffset? unsubscribedAt,
        DateTimeOffset? unsubscribeSyncedAt,
        CancellationToken cancellationToken)
    {
        // Ya sincronizada: el CRM ya lo sabe y en fase 1 nada limpia una baja, así que una segunda
        // visita no empuja ni vuelve a sellar. Es lo que hace idempotente el segundo clic.
        if (unsubscribeSyncedAt is not null)
            return Result.Success<DateTimeOffset?, BaseError>(unsubscribeSyncedAt);

        var now = _clock.GetUtcNow();

        // La hora del primer clic se conserva: si ya se había hecho clic pero el CRM estaba caído,
        // esta visita es la que completa el empujón, no una baja nueva.
        var clickedAt = unsubscribedAt ?? now;

        // Se empuja al CRM y solo se sella si llegó. Con el CRM caído la fila queda pendiente —el
        // próximo lanzamiento la vacía por el mismo camino (spec 06 §6, espeja §4.3)— y quien
        // llama decide si eso es un fallo: en el portal público no lo es, porque un 503 por un
        // servicio del que el desconocido nunca oyó es el error que esta degradación existe para
        // no cometer.
        var pushed = await _crm.OptOutAsync(contactId, cancellationToken);
        var syncedAt = pushed.IsSuccess ? now : (DateTimeOffset?)null;

        var marked = await _repository.MarkUnsubscribedAsync(recipientId, clickedAt, syncedAt, cancellationToken);

        return marked.IsFailure
            ? Result.Failure<DateTimeOffset?, BaseError>(marked.Error)
            : Result.Success<DateTimeOffset?, BaseError>(syncedAt);
    }

    /// <summary>
    /// La fila que lleva ese token, aceptada con una comparación en tiempo fijo. El índice
    /// encuentra por hash; <see cref="CryptographicOperations.FixedTimeEquals"/> es lo que impide
    /// que el tiempo de respuesta le diga a quien prueba tokens cuántos bytes acertó (spec
    /// <c>06</c> §2.1).
    /// </summary>
    private async Task<Result<Maybe<UnsubscribeTarget>, BaseError>> FindTargetAsync(
        string token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Result.Success<Maybe<UnsubscribeTarget>, BaseError>(Maybe<UnsubscribeTarget>.None);

        var hash = PortalToken.HashOf(token);

        var found = await _repository.FindRecipientByUnsubscribeTokenAsync(hash, cancellationToken);

        if (found.IsFailure || found.Value.HasNoValue)
            return found;

        return CryptographicOperations.FixedTimeEquals(found.Value.Value.TokenHash, hash)
            ? found
            : Result.Success<Maybe<UnsubscribeTarget>, BaseError>(Maybe<UnsubscribeTarget>.None);
    }
}
