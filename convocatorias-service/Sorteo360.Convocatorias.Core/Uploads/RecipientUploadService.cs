using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Crm;
using Sorteo360.Convocatorias.Core.DTOs;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Uploads;

/// <inheritdoc cref="IRecipientUploadService" />
public sealed class RecipientUploadService : IRecipientUploadService
{
    private readonly ICampaignUploadRepository _repository;
    private readonly ISpreadsheetUploadPolicy _policy;
    private readonly ISpreadsheetParser _parser;
    private readonly ICrmContactDirectory _directory;

    public RecipientUploadService(
        ICampaignUploadRepository repository,
        ISpreadsheetUploadPolicy policy,
        ISpreadsheetParser parser,
        ICrmContactDirectory directory)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(directory);

        _repository = repository;
        _policy = policy;
        _parser = parser;
        _directory = directory;
    }

    /// <inheritdoc />
    public async Task<Result<Maybe<PendingUpload>, BaseError>> StageAsync(
        StageUploadRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // La guarda de borrador primero: si la campaña ya no admite cambios, no se gasta ni una
        // lectura del CRM ni un byte de parseo. La misma guarda vuelve a correr dentro de la
        // transacción que escribe, que es donde tiene que correr para no dejar pasar una campaña
        // que dejó de ser borrador en vuelo.
        var context = await _repository.GetStagingContextAsync(request.CampaignId, cancellationToken);

        if (context.IsFailure)
            return Result.Failure<Maybe<PendingUpload>, BaseError>(context.Error);

        if (context.Value.HasNoValue)
            return Result.Success<Maybe<PendingUpload>, BaseError>(Maybe<PendingUpload>.None);

        var format = await _policy.InspectAsync(
            request.Content,
            request.FileName,
            request.DeclaredContentType,
            cancellationToken);

        if (format.IsFailure)
            return Result.Failure<Maybe<PendingUpload>, BaseError>(format.Error);

        var parsed = await _parser.ParseAsync(request.Content, format.Value, cancellationToken);

        if (parsed.IsFailure)
            return Result.Failure<Maybe<PendingUpload>, BaseError>(parsed.Error);

        var rowCount = _policy.CheckRowCount(parsed.Value.Count);

        if (rowCount.IsFailure)
            return Result.Failure<Maybe<PendingUpload>, BaseError>(rowCount.Error);

        var classified = UploadRowClassifier.Classify(parsed.Value);

        var resolved = await ResolveAgainstCrmAsync(
            classified.Rows,
            context.Value.Value.RecipientContactIds,
            cancellationToken);

        if (resolved.IsFailure)
            return Result.Failure<Maybe<PendingUpload>, BaseError>(resolved.Error);

        var staged = await _repository.StageAsync(
            new StageRecipientUploadDto(
                request.CampaignId,
                request.FileName,
                format.Value.Encoding,
                parsed.Value.Count,
                UploadBucketCounts.From(resolved.Value.Select(row => row.Bucket), classified.CollapsedDuplicates),
                resolved.Value,
                request.UploadedById),
            cancellationToken);

        if (staged.IsFailure)
            return Result.Failure<Maybe<PendingUpload>, BaseError>(staged.Error);

        if (staged.Value.HasNoValue)
            return Result.Success<Maybe<PendingUpload>, BaseError>(Maybe<PendingUpload>.None);

        // Se relee en vez de devolver lo que esta petición creyó dejar: la respuesta tiene que
        // decir lo que hay en la base, incluidos los ids de fila con los que después se habla.
        return await _repository.GetPendingAsync(request.CampaignId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<Maybe<CampaignUploadBatch>, BaseError>> ConfirmAsync(
        Guid campaignId,
        Guid confirmedById,
        CancellationToken cancellationToken)
    {
        var context = await _repository.GetStagingContextAsync(campaignId, cancellationToken);

        if (context.IsFailure)
            return Result.Failure<Maybe<CampaignUploadBatch>, BaseError>(context.Error);

        if (context.Value.HasNoValue)
            return Result.Success<Maybe<CampaignUploadBatch>, BaseError>(Maybe<CampaignUploadBatch>.None);

        var pending = await _repository.GetPendingAsync(campaignId, cancellationToken);

        if (pending.IsFailure)
            return Result.Failure<Maybe<CampaignUploadBatch>, BaseError>(pending.Error);

        // La campaña existe y no tiene carga pendiente. Código propio, porque desde esta misma
        // ruta un 404 también significa "no existe esa campaña".
        if (pending.Value.HasNoValue)
            return Result.Failure<Maybe<CampaignUploadBatch>, BaseError>(RecipientUploadErrors.NoPendingUpload());

        var completed = 0;

        // Una petición del navegador, N peticiones por dentro, y en secuencia. Las de dentro van
        // por nombre de servicio de Compose y no tocan el nginx, así que ni su
        // `client_max_body_size` ni su `proxy_read_timeout` les aplican; lo que sí aplica es que
        // la duración de la petición de fuera es la suma de las de dentro, que es de donde sale
        // el tope de 500 filas (spec 05 §6 y §7).
        foreach (var row in pending.Value.Value.Rows
            .Where(row => row.AppliedAt is null && UploadRowBucket.IsConfirmable(row.Bucket))
            .OrderBy(row => row.SourceRowNumber))
        {
            var effect = await ResolveEffectAsync(campaignId, row, cancellationToken);

            if (effect.IsFailure)
            {
                // El cortacircuitos del chasis es lo que hace real este diseño reanudable: a la
                // fila diez de un CRM enfermo el circuito se abre y el resto falla al instante,
                // en vez de quemar medio minuto por fila. El chasis ya traduce el circuito
                // abierto a HttpRequestException, así que llega acá como este mismo 503 y no
                // como un 500 (spec 05 §6.1).
                return Result.Failure<Maybe<CampaignUploadBatch>, BaseError>(
                    new UploadCrmUnavailableError(completed));
            }

            var applied = await _repository.ApplyRowAsync(effect.Value, cancellationToken);

            if (applied.IsFailure)
                return Result.Failure<Maybe<CampaignUploadBatch>, BaseError>(applied.Error);

            completed++;
        }

        return await _repository.CompleteAsync(campaignId, confirmedById, cancellationToken);
    }

    /// <summary>
    /// Los cuatro cubos que necesitan al CRM, en el orden de §4: ya destinataria gana a
    /// suprimida, suprimida gana a encontrada, y lo que el CRM no tenga es nueva.
    /// </summary>
    private async Task<Result<IReadOnlyList<StagedUploadRowDto>, BaseError>> ResolveAgainstCrmAsync(
        IReadOnlyList<ClassifiedUploadRow> rows,
        IReadOnlySet<Guid> recipientContactIds,
        CancellationToken cancellationToken)
    {
        var resolved = new List<StagedUploadRowDto>(rows.Count);

        foreach (var row in rows)
        {
            if (row.Bucket is not null)
            {
                resolved.Add(ToDto(row, row.Bucket, row.ReasonCode, contactId: null));
                continue;
            }

            var lookup = await _directory.FindByEmailAsync(row.Row.Email!.Trim(), cancellationToken);

            if (lookup.IsFailure)
                return Result.Failure<IReadOnlyList<StagedUploadRowDto>, BaseError>(lookup.Error);

            if (lookup.Value.HasNoValue)
            {
                resolved.Add(ToDto(row, UploadRowBucket.Unmatched, reasonCode: null, contactId: null));
                continue;
            }

            var contact = lookup.Value.Value;

            var (bucket, reason) = Classify(contact, recipientContactIds.Contains(contact.Id));

            resolved.Add(ToDto(row, bucket, reason, contact.Id));
        }

        return Result.Success<IReadOnlyList<StagedUploadRowDto>, BaseError>(resolved);
    }

    /// <summary>
    /// Qué hay que escribir por esta fila, creando en el CRM si hacía falta. El cubo que sale
    /// puede no ser el que la fila traía: un 409 mueve una fila nueva a encontrada, o a
    /// suprimida si el contacto que había detrás estaba dado de baja.
    /// </summary>
    private async Task<Result<ApplyUploadRowDto, BaseError>> ResolveEffectAsync(
        Guid campaignId,
        CampaignUploadRow row,
        CancellationToken cancellationToken)
    {
        // Nombre y apellido están por construcción: una fila a la que le falte cualquiera de los
        // dos es `malformed` y no llega hasta acá, que es lo que evita inventar la mitad que falte
        // y escribirla en el CRM.
        var firstName = row.FirstName!.Trim();
        var lastName = row.LastName!.Trim();

        if (row.Bucket != UploadRowBucket.Unmatched)
        {
            return Result.Success<ApplyUploadRowDto, BaseError>(new ApplyUploadRowDto(
                campaignId,
                row.Id,
                row.Bucket,
                row.ContactId!.Value,
                row.ReasonCode,
                firstName,
                lastName,
                row.Email));
        }

        var creation = await _directory.CreateContactAsync(
            new NewCrmContact(firstName, lastName, row.Email!.Trim(), row.Phone, ConsentSources.VerbalUpload),
            cancellationToken);

        if (creation.IsFailure)
            return Result.Failure<ApplyUploadRowDto, BaseError>(creation.Error);

        var contact = creation.Value.Contact;

        // Un 409 es una coincidencia. Se toma el id y se sigue, pero hay que volver a mirar su
        // `canBeEmailed`: el contacto que había detrás puede estar suprimido, y sin esta
        // comprobación la carrera le escribe en silencio a quien se dio de baja. Sale gratis,
        // porque el 409 ya trae el contacto, y lo decide el mismo sitio que lo decidió al revisar.
        // Un contacto recién creado se queda en `unmatched`: el cubo dice de dónde salió.
        var (bucket, reason) = creation.Value.AlreadyExisted
            ? Classify(contact, alreadyRecipient: false)
            : (UploadRowBucket.Unmatched, (string?)null);

        return Result.Success<ApplyUploadRowDto, BaseError>(new ApplyUploadRowDto(
            campaignId,
            row.Id,
            bucket,
            contact.Id,
            reason,
            firstName,
            lastName,
            row.Email));
    }

    /// <summary>
    /// Qué significa para una fila lo que el CRM dijo de un contacto. Es el <b>único</b> sitio
    /// donde se decide, lo use la revisión o lo use un 409 de la confirmación, para que las dos
    /// no puedan discrepar.
    /// <para>
    /// <c>already_a_recipient</c> gana a <c>suppressed</c> a propósito: quien se dio de baja
    /// después de la selección ya está en la campaña, y quitarlo acá duplicaría el trabajo que la
    /// recomprobación del lanzamiento ya hace en el único momento en que es autoritativa (§4).
    /// </para>
    /// </summary>
    private static (string Bucket, string? ReasonCode) Classify(CrmContact contact, bool alreadyRecipient)
    {
        if (alreadyRecipient)
            return (UploadRowBucket.AlreadyARecipient, null);

        return contact.CanBeEmailed
            ? (UploadRowBucket.Matched, null)
            : (UploadRowBucket.Suppressed, ExclusionReason.FromCrm(contact.SuppressionReason));
    }

    private static StagedUploadRowDto ToDto(
        ClassifiedUploadRow row,
        string bucket,
        string? reasonCode,
        Guid? contactId) =>
        new(
            row.Row.SourceRowNumber,
            row.Row.FirstName,
            row.Row.LastName,
            row.Row.Email,
            row.Row.Phone,
            row.Row.Company,
            bucket,
            reasonCode,
            contactId,
            row.DuplicateRowNumbers,
            row.CollapsedDuplicates);

}
