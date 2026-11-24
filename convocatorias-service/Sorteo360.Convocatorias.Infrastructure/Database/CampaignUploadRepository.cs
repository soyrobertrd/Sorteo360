using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.DTOs;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Uploads;
using Sorteo360.Platform.Database;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Infrastructure.Database;

/// <inheritdoc cref="ICampaignUploadRepository" />
public sealed class CampaignUploadRepository : ICampaignUploadRepository
{
    private readonly ConvocatoriasDbContext _context;
    private readonly ILogger<CampaignUploadRepository> _logger;

    public CampaignUploadRepository(ConvocatoriasDbContext context, ILogger<CampaignUploadRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<Maybe<UploadStagingContext>, BaseError>> GetStagingContextAsync(
        Guid campaignId,
        CancellationToken cancellationToken) =>
        DbResult.TryResultAsync(_logger, "preparar la carga de destinatarios", async () =>
        {
            var draft = await CampaignDraftGuard.LoadAsync<UploadStagingContext>(_context, campaignId, cancellationToken);

            if (draft.Campaign is null)
                return draft.Refusal;

            var recipients = await _context.CampaignRecipients
                .AsNoTracking()
                .Where(r => r.CampaignId == campaignId)
                .Select(r => r.ContactId)
                .ToListAsync(cancellationToken);

            return Result.Success<Maybe<UploadStagingContext>, BaseError>(
                Maybe.From(new UploadStagingContext(recipients.ToHashSet())));
        });

    /// <inheritdoc />
    public Task<Result<Maybe<CampaignUploadBatch>, BaseError>> StageAsync(
        StageRecipientUploadDto dto,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return DbResult.TryResultAsync(_logger, "guardar la carga de destinatarios", async () =>
        {
            var draft = await CampaignDraftGuard.LoadAsync<CampaignUploadBatch>(_context, dto.CampaignId, cancellationToken);

            if (draft.Campaign is null)
                return draft.Refusal;

            // Una segunda subida reemplaza a la primera. El índice parcial lo exige de todos
            // modos; borrar acá es lo que convierte esa exigencia en el comportamiento que la
            // spec pide, en vez de en un 500 por violación de restricción.
            var previous = await _context.CampaignUploadBatches
                .Where(b => b.CampaignId == dto.CampaignId && b.ConfirmedAt == null)
                .ToListAsync(cancellationToken);

            _context.CampaignUploadBatches.RemoveRange(previous);

            var batch = new CampaignUploadBatchData
            {
                Id = Guid.CreateVersion7(),
                CampaignId = dto.CampaignId,
                OriginalFileName = dto.OriginalFileName,
                DetectedEncoding = dto.DetectedEncoding,
                RowCount = dto.RowCount,
                UploadedAt = DateTimeOffset.UtcNow,
                UploadedById = dto.UploadedById,
            };

            Apply(batch, dto.Counts);

            _context.CampaignUploadBatches.Add(batch);

            foreach (var row in dto.Rows)
            {
                _context.CampaignUploadRows.Add(new CampaignUploadRowData
                {
                    Id = Guid.CreateVersion7(),
                    BatchId = batch.Id,
                    SourceRowNumber = row.SourceRowNumber,
                    FirstName = row.FirstName,
                    LastName = row.LastName,
                    Email = row.Email,
                    Phone = row.Phone,
                    CompanyRaw = row.CompanyRaw,
                    Bucket = row.Bucket,
                    ReasonCode = row.ReasonCode,
                    ContactId = row.ContactId,
                    DuplicateRowNumbers = [.. row.DuplicateRowNumbers],
                    CollapsedDuplicates = row.CollapsedDuplicates,
                });
            }

            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success<Maybe<CampaignUploadBatch>, BaseError>(Maybe.From(ToEntity(batch)));
        });
    }

    /// <inheritdoc />
    public Task<Result<Maybe<PendingUpload>, BaseError>> GetPendingAsync(
        Guid campaignId,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "leer la carga pendiente", async () =>
        {
            var batch = await _context.CampaignUploadBatches
                .AsNoTracking()
                .Include(b => b.Rows)
                .FirstOrDefaultAsync(b => b.CampaignId == campaignId && b.ConfirmedAt == null, cancellationToken);

            if (batch is null)
                return Maybe<PendingUpload>.None;

            return Maybe.From(new PendingUpload(
                ToEntity(batch),
                [.. batch.Rows.OrderBy(r => r.SourceRowNumber).Select(ToEntity)]));
        });

    /// <inheritdoc />
    public async Task<UnitResult<BaseError>> ApplyRowAsync(ApplyUploadRowDto dto, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // El chasis no trae una variante de `DbResult` que devuelva `UnitResult`, así que la
        // operación devuelve un booleano de relleno que se descarta abajo. Lo que aporta el
        // envoltorio es el registro y la traducción del fallo de base de datos, no su valor.
        var applied = await DbResult.TryResultAsync(_logger, "aplicar una fila de la carga", async () =>
        {
            var campaign = await _context.Campaigns
                .FirstOrDefaultAsync(c => c.Id == dto.CampaignId, cancellationToken);

            if (campaign is null)
                return Result.Failure<bool, BaseError>(RecipientUploadErrors.UploadGoneWhileConfirming());

            // La misma guarda que en el resto del modelo, y acá dentro de la transacción que
            // escribe: una campaña que deja de ser borrador a mitad de una confirmación para el
            // resto de las filas en vez de seguir escribiéndole encima.
            if (campaign.State != CampaignState.Draft)
                return Result.Failure<bool, BaseError>(new CampaignNotDraftError(campaign.State));

            var row = await _context.CampaignUploadRows
                .FirstOrDefaultAsync(r => r.Id == dto.RowId, cancellationToken);

            if (row is null)
                return Result.Failure<bool, BaseError>(RecipientUploadErrors.UploadGoneWhileConfirming());

            // Un reintento después de un corte vuelve a pasar por acá: una fila ya aplicada no
            // se escribe dos veces, que es lo que impide crear a nadie por duplicado.
            if (row.AppliedAt is not null)
                return Result.Success<bool, BaseError>(true);

            var now = DateTimeOffset.UtcNow;

            if (UploadRowBucket.CreatesRecipient(dto.Bucket))
                await LinkRecipientAsync(dto, now, cancellationToken);
            else
                await ExcludeAsync(dto, now, cancellationToken);

            row.Bucket = dto.Bucket;
            row.ReasonCode = dto.ReasonCode;
            row.ContactId = dto.ContactId;
            row.AppliedAt = now;

            campaign.UpdatedAt = now;

            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success<bool, BaseError>(true);
        });

        return applied.IsFailure ? UnitResult.Failure(applied.Error) : UnitResult.Success<BaseError>();
    }

    /// <inheritdoc />
    public Task<Result<Maybe<CampaignUploadBatch>, BaseError>> CompleteAsync(
        Guid campaignId,
        Guid confirmedById,
        CancellationToken cancellationToken) =>
        DbResult.TryResultAsync(_logger, "cerrar la carga de destinatarios", async () =>
        {
            var draft = await CampaignDraftGuard.LoadAsync<CampaignUploadBatch>(_context, campaignId, cancellationToken);

            if (draft.Campaign is null)
                return draft.Refusal;

            var batch = await _context.CampaignUploadBatches
                .Include(b => b.Rows)
                .FirstOrDefaultAsync(b => b.CampaignId == campaignId && b.ConfirmedAt == null, cancellationToken);

            if (batch is null)
                return Result.Success<Maybe<CampaignUploadBatch>, BaseError>(Maybe<CampaignUploadBatch>.None);

            // Las cuentas se recalculan desde el cubo final de cada fila: un 409 pudo mover una
            // fila de "creada" a "encontrada", o a "suprimida" si el contacto que había detrás
            // estaba de baja, y el resumen que sobrevive tiene que decir lo que de verdad pasó.
            // Se tallan por el mismo sitio que al revisar, o las dos cuentas acabarían discrepando.
            Apply(batch, UploadBucketCounts.From(
                batch.Rows.Select(r => r.Bucket),
                batch.Rows.Sum(r => r.CollapsedDuplicates)));

            batch.ConfirmedAt = DateTimeOffset.UtcNow;
            batch.ConfirmedById = confirmedById;

            // Acá está el bulto y acá se va: lo que queda son siete enteros y dos identidades,
            // que es lo que contesta "subimos 340 filas y 12 estaban mal" sin guardar 340 filas.
            _context.CampaignUploadRows.RemoveRange(batch.Rows);

            draft.Campaign.UpdatedAt = batch.ConfirmedAt.Value;
            draft.Campaign.UpdatedById = confirmedById;

            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success<Maybe<CampaignUploadBatch>, BaseError>(Maybe.From(ToEntity(batch)));
        });

    /// <summary>Copia las siete cuentas sobre la fila del lote.</summary>
    private static void Apply(CampaignUploadBatchData batch, UploadBucketCounts counts)
    {
        batch.MatchedCount = counts.Matched;
        batch.UnmatchedCount = counts.Unmatched;
        batch.SuppressedCount = counts.Suppressed;
        batch.AlreadyRecipientCount = counts.AlreadyARecipient;
        batch.DuplicateInFileCount = counts.DuplicateInFile;
        batch.MalformedCount = counts.Malformed;
        batch.CollapsedDuplicateCount = counts.CollapsedExactDuplicates;
    }

    /// <summary>
    /// Materializa un destinatario. Es idempotente y quita la exclusión que hubiera, igual que la
    /// selección.
    /// <para>
    /// Quitarla <b>no es opcional</b>: las dos tablas son únicas en <c>(campaign_id, contact_id)</c>
    /// y una persona está en una campaña como destinataria <b>o</b> como exclusión y nunca como las
    /// dos, que es lo que mantiene honesta cualquier cuenta sobre ellas. Dejar la exclusión ahí
    /// rompería ese par de restricciones; y sin ninguna ruta que des-excluya, un contacto excluido
    /// una vez no podría entrar nunca más en esa campaña.
    /// </para>
    /// </summary>
    private async Task LinkRecipientAsync(ApplyUploadRowDto dto, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var exclusion = await _context.CampaignExclusions
            .FirstOrDefaultAsync(e => e.CampaignId == dto.CampaignId && e.ContactId == dto.ContactId, cancellationToken);

        if (exclusion is not null)
            _context.CampaignExclusions.Remove(exclusion);

        var exists = await _context.CampaignRecipients
            .AnyAsync(r => r.CampaignId == dto.CampaignId && r.ContactId == dto.ContactId, cancellationToken);

        if (exists)
            return;

        _context.CampaignRecipients.Add(new CampaignRecipientData
        {
            Id = Guid.CreateVersion7(),
            CampaignId = dto.CampaignId,
            ContactId = dto.ContactId,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email!,
            SendStatus = RecipientSendStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
        });
    }

    /// <summary>
    /// Escribe la exclusión de una fila suprimida. Si esa persona ya es destinataria no se toca
    /// nada: una persona está en una campaña una vez, y "ya es destinataria" gana (spec <c>05</c>
    /// §4).
    /// </summary>
    private async Task ExcludeAsync(ApplyUploadRowDto dto, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var isRecipient = await _context.CampaignRecipients
            .AnyAsync(r => r.CampaignId == dto.CampaignId && r.ContactId == dto.ContactId, cancellationToken);

        if (isRecipient)
            return;

        var exists = await _context.CampaignExclusions
            .AnyAsync(e => e.CampaignId == dto.CampaignId && e.ContactId == dto.ContactId, cancellationToken);

        if (exists)
            return;

        _context.CampaignExclusions.Add(new CampaignExclusionData
        {
            Id = Guid.CreateVersion7(),
            CampaignId = dto.CampaignId,
            ContactId = dto.ContactId,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            Reason = dto.ReasonCode ?? ExclusionReason.ContactUnavailable,
            ExcludedAt = now,
        });
    }

    private static CampaignUploadBatch ToEntity(CampaignUploadBatchData data) =>
        new(
            data.Id,
            data.CampaignId,
            data.OriginalFileName,
            data.DetectedEncoding,
            data.RowCount,
            new UploadBucketCounts
            {
                Matched = data.MatchedCount,
                Unmatched = data.UnmatchedCount,
                Suppressed = data.SuppressedCount,
                AlreadyARecipient = data.AlreadyRecipientCount,
                DuplicateInFile = data.DuplicateInFileCount,
                Malformed = data.MalformedCount,
                CollapsedExactDuplicates = data.CollapsedDuplicateCount,
            },
            data.UploadedAt,
            data.UploadedById,
            data.ConfirmedAt,
            data.ConfirmedById);

    private static CampaignUploadRow ToEntity(CampaignUploadRowData data) =>
        new(
            data.Id,
            data.BatchId,
            data.SourceRowNumber,
            data.FirstName,
            data.LastName,
            data.Email,
            data.Phone,
            data.CompanyRaw,
            data.Bucket,
            data.ReasonCode,
            data.ContactId,
            data.DuplicateRowNumbers,
            data.CollapsedDuplicates,
            data.AppliedAt);
}
