using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sorteo360.Events.Core.Accounts;
using Sorteo360.Events.Core.Database;
using Sorteo360.Events.Core.Errors;
using Sorteo360.Events.Core.Files;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Core.Shared;
using Sorteo360.Platform.Database;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Infrastructure.Database;

public sealed class RaffleRepository : IRaffleRepository
{
    /// <summary>Cuántos códigos de retiro se prueban antes de rendirse (spec 03 §2.3).</summary>
    private const int MaxRedemptionCodeAttempts = 3;

    private readonly EventsDbContext _context;
    private readonly ILogger<RaffleRepository> _logger;

    public RaffleRepository(EventsDbContext context, ILogger<RaffleRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        _context = context;
        _logger = logger;
    }

    public Task<Result<Maybe<Raffle>, BaseError>> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken)
    {
        return DbResult.TryAsync<Maybe<Raffle>>(_logger, "obtener el sorteo", async () =>
        {
            var data = await _context.Raffles
                .AsNoTracking()
                .Where(r => r.EventId == eventId)
                .FirstOrDefaultAsync(cancellationToken);

            return data is null ? Maybe<Raffle>.None : Maybe.From(MapToRaffle(data));
        });
    }

    public Task<Result<Guid, BaseError>> AddAsync(Raffle raffle, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(raffle);

        return DbResult.TryResultAsync<Guid>(_logger, "crear el sorteo", async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            var accountId = await _context.Events
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(e => e.Id == raffle.EventId && e.DeletedAt == null)
                .Select(e => e.AccountId)
                .FirstOrDefaultAsync(cancellationToken);

            if (accountId == Guid.Empty)
                accountId = _context.CurrentAccountId == Guid.Empty
                    ? Account.DefaultBanreservasAccountId
                    : _context.CurrentAccountId;

            // R9: bloquear la fila de la cuenta para serializar creaciones concurrentes
            // (tope de sorteos activos y tope mensual en la misma transacción).
            await _context.Database
                .SqlQuery<Guid>($"SELECT id AS \"Value\" FROM events.account WHERE id = {accountId} FOR UPDATE")
                .ToListAsync(cancellationToken);

            var account = await _context.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);

            if (account is not null)
            {
                if (account.Status != AccountStatus.Active)
                    return Result.Failure<Guid, BaseError>(AccountErrors.AccountNotActive());

                var plan = PlanCatalog.GetOrFree(account.Plan);
                var now = raffle.CreatedAt;
                var yearMonth = PlanCatalog.CurrentYearMonth(now);

                if (plan.MaxActiveRaffles is { } activeCap)
                {
                    var activeCount = await (
                        from r in _context.Raffles.AsNoTracking()
                        join e in _context.Events.IgnoreQueryFilters().AsNoTracking() on r.EventId equals e.Id
                        where e.DeletedAt == null && e.AccountId == account.Id && r.State != RaffleState.Completed
                        select r.Id
                    ).CountAsync(cancellationToken);

                    if (activeCount >= activeCap)
                        return Result.Failure<Guid, BaseError>(AccountErrors.PlanActiveRaffleLimit(activeCap));
                }

                if (plan.MaxMonthlyRaffles is { } monthlyCap)
                {
                    var updated = await _context.Database
                        .SqlQuery<int>($"""
                            INSERT INTO events.account_monthly_usage (account_id, year_month, raffles_created, updated_at)
                            VALUES ({account.Id}, {yearMonth}, 1, {now})
                            ON CONFLICT (account_id, year_month) DO UPDATE
                              SET raffles_created = events.account_monthly_usage.raffles_created + 1,
                                  updated_at = {now}
                              WHERE events.account_monthly_usage.raffles_created < {monthlyCap}
                            RETURNING raffles_created AS "Value"
                            """)
                        .ToListAsync(cancellationToken);

                    if (updated.Count == 0)
                        return Result.Failure<Guid, BaseError>(
                            AccountErrors.PlanMonthlyRaffleLimit(monthlyCap, PlanCatalog.NextMonthlyResetUtc(now)));
                }
                else
                {
                    await _context.Database.ExecuteSqlInterpolatedAsync($"""
                        INSERT INTO events.account_monthly_usage (account_id, year_month, raffles_created, updated_at)
                        VALUES ({account.Id}, {yearMonth}, 1, {now})
                        ON CONFLICT (account_id, year_month) DO UPDATE
                          SET raffles_created = events.account_monthly_usage.raffles_created + 1,
                              updated_at = {now}
                        """, cancellationToken);
                }
            }

            var data = new RaffleData { Id = raffle.Id };
            CopyToData(raffle, data);
            _context.Raffles.Add(data);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success<Guid, BaseError>(raffle.Id);
        });
    }

    public async Task<UnitResult<BaseError>> UpdateAsync(Raffle raffle, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(raffle);

        var result = await DbResult.TryResultAsync<bool>(_logger, "actualizar el sorteo", async () =>
        {
            // Un borrador se puede eliminar entre la lectura del servicio y esta escritura.
            var data = await _context.Raffles
                .Where(r => r.Id == raffle.Id)
                .SingleOrDefaultAsync(cancellationToken);
            if (data is null)
                return Result.Failure<bool, BaseError>(RaffleErrors.NotFound());

            CopyToData(raffle, data);

            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success<bool, BaseError>(true);
        });

        return result.IsSuccess ? UnitResult.Success<BaseError>() : UnitResult.Failure(result.Error);
    }

    public async Task<UnitResult<BaseError>> DeleteDraftAsync(Guid raffleId, CancellationToken cancellationToken)
    {
        var result = await DbResult.TryResultAsync<bool>(_logger, "eliminar el sorteo", async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var locked = await LockExistingRaffleAsync(raffleId, cancellationToken);
            if (locked.IsFailure)
                return Result.Failure<bool, BaseError>(locked.Error);

            // Releído bajo el bloqueo: un arranque que entró mientras se esperaba lo sacó de draft.
            var data = await _context.Raffles
                .AsNoTracking()
                .Where(r => r.Id == raffleId)
                .SingleAsync(cancellationToken);

            var isDraft = MapToRaffle(data).EnsureDraft();
            if (isDraft.IsFailure)
                return Result.Failure<bool, BaseError>(isDraft.Error);

            // Las FK son Restrict, así que se borra de las hojas hacia el sorteo. Las filas congeladas
            // caen en cascada; un borrador no tiene, pero uno reabierto pudo tenerlas.
            var roundIds = _context.RaffleRounds.Where(r => r.RaffleId == raffleId).Select(r => r.Id);
            await _context.Prizes.Where(p => roundIds.Contains(p.RoundId)).ExecuteDeleteAsync(cancellationToken);
            await _context.RaffleRounds.Where(r => r.RaffleId == raffleId).ExecuteDeleteAsync(cancellationToken);
            await _context.RaffleAttendeeOverrides.Where(o => o.RaffleId == raffleId).ExecuteDeleteAsync(cancellationToken);
            await _context.Raffles.Where(r => r.Id == raffleId).ExecuteDeleteAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result.Success<bool, BaseError>(true);
        });

        return result.IsSuccess ? UnitResult.Success<BaseError>() : UnitResult.Failure(result.Error);
    }


    // Las tres lecturas que cruzan sorteos unen con _context.Events, cuyo filtro global deja fuera los
    // eventos eliminados: el sorteo de un evento eliminado no aparece en ninguna.

    public Task<Result<PagedResult<RaffleListItem>, BaseError>> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        return DbResult.TryAsync(_logger, "listar los sorteos", async () =>
        {
            var query = from r in _context.Raffles.AsNoTracking()
                        join e in _context.Events.AsNoTracking() on r.EventId equals e.Id
                        select new { Raffle = r, EventName = e.Name, EventStartDate = e.StartDate };

            var total = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(x => x.EventStartDate)
                .ThenBy(x => x.Raffle.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new RaffleListItem(
                    x.Raffle.Id,
                    x.Raffle.EventId,
                    x.EventName,
                    x.EventStartDate,
                    x.Raffle.State,
                    _context.Prizes.Count(p =>
                        _context.RaffleRounds.Any(rr => rr.Id == p.RoundId && rr.RaffleId == x.Raffle.Id)),
                    _context.PrizeDraws.Count(d =>
                        d.VoidedAt == null
                        && _context.Prizes.Any(p => p.Id == d.PrizeId
                            && _context.RaffleRounds.Any(rr => rr.Id == p.RoundId && rr.RaffleId == x.Raffle.Id))),
                    _context.PrizeDraws.Count(d =>
                        d.VoidedAt == null
                        && _context.PrizeHandovers.Any(h => h.PrizeDrawId == d.Id && h.ReversedAt == null)
                        && _context.Prizes.Any(p => p.Id == d.PrizeId
                            && _context.RaffleRounds.Any(rr => rr.Id == p.RoundId && rr.RaffleId == x.Raffle.Id))),
                    x.Raffle.StartedAt,
                    x.Raffle.CompletedAt,
                    x.Raffle.LinkedEventId))
                .ToListAsync(cancellationToken);

            return new PagedResult<RaffleListItem>(items, total, page, pageSize);
        });
    }

    public Task<Result<RaffleTotals, BaseError>> GetTotalsAsync(CancellationToken cancellationToken)
    {
        return DbResult.TryAsync(_logger, "contar los sorteos", async () =>
        {
            var raffles = from r in _context.Raffles.AsNoTracking()
                          join e in _context.Events.AsNoTracking() on r.EventId equals e.Id
                          select r;
            var raffleIds = raffles.Select(r => r.Id);
            var prizes = from p in _context.Prizes.AsNoTracking()
                         join rr in _context.RaffleRounds.AsNoTracking() on p.RoundId equals rr.Id
                         where raffleIds.Contains(rr.RaffleId)
                         select p;
            var prizeIds = prizes.Select(p => p.Id);
            var liveDraws = _context.PrizeDraws.AsNoTracking()
                .Where(d => d.VoidedAt == null && prizeIds.Contains(d.PrizeId));

            return new RaffleTotals(
                Raffles: await raffles.CountAsync(cancellationToken),
                Active: await raffles.CountAsync(r => r.State == RaffleState.InProgress, cancellationToken),
                Prizes: await prizes.CountAsync(cancellationToken),
                Winners: await liveDraws.CountAsync(cancellationToken),
                Redeemed: await liveDraws.CountAsync(
                    d => _context.PrizeHandovers.Any(h => h.PrizeDrawId == d.Id && h.ReversedAt == null),
                    cancellationToken),
                QualifiedParticipants: await _context.RaffleAttendees.AsNoTracking()
                    .CountAsync(a => a.Qualified && raffleIds.Contains(a.RaffleId), cancellationToken));
        });
    }

    public Task<Result<Maybe<RaffleWithRounds>, BaseError>> GetWithRoundsByEventIdAsync(Guid eventId, CancellationToken cancellationToken)
    {
        return DbResult.TryAsync<Maybe<RaffleWithRounds>>(_logger, "obtener el sorteo con sus rondas", async () =>
        {
            var raffleData = await _context.Raffles
                .AsNoTracking()
                .Where(r => r.EventId == eventId)
                .FirstOrDefaultAsync(cancellationToken);

            if (raffleData is null)
                return Maybe<RaffleWithRounds>.None;

            var withRounds = await LoadRoundsAsync(MapToRaffle(raffleData), cancellationToken);

            var records = await _context.NotarialRecords
                .AsNoTracking()
                .Where(n => n.RaffleId == raffleData.Id)
                .OrderByDescending(n => n.Version)
                .Select(n => new NotarialRecord(n.Id, n.RaffleId, n.Version, n.StoredFileId, n.GeneratedAt, n.GeneratedById))
                .ToListAsync(cancellationToken);

            return Maybe.From(withRounds with { NotarialRecords = records });
        });
    }

    /// <summary>
    /// El sorteo con sus rondas, sus premios y todos sus sorteos de premio. El orden lo pone el
    /// agregado, así que acá no se ordena nada.
    /// </summary>
    private async Task<RaffleWithRounds> LoadRoundsAsync(Raffle raffle, CancellationToken cancellationToken)
    {
        var roundRows = await _context.RaffleRounds
            .AsNoTracking()
            .Where(r => r.RaffleId == raffle.Id)
            .ToListAsync(cancellationToken);

        var roundIds = roundRows.Select(r => r.Id).ToList();
        var prizesByRound = (await _context.Prizes
                .AsNoTracking()
                .Where(p => roundIds.Contains(p.RoundId))
                // Sin orden Postgres devuelve el del heap, que cambia con cada sorteo: los premios
                // iguales de un mismo lote (mismo CreatedAt) salían intercalados con los demás.
                .OrderBy(p => p.CreatedAt).ThenBy(p => p.Name).ThenBy(p => p.Value).ThenBy(p => p.Id)
                .ToListAsync(cancellationToken))
            .ToLookup(p => p.RoundId, MapToPrize);

        var rounds = roundRows
            .Select(r => MapToRound(r, prizesByRound[r.Id].ToList()))
            .ToList();

        var draws = await ListDrawsAsync(raffle.Id, cancellationToken);

        var logoIds = prizesByRound
            .SelectMany(g => g)
            .Where(p => p.SponsorLogoFileId is not null)
            .Select(p => p.SponsorLogoFileId!.Value)
            .ToList();
        var logos = logoIds.Count == 0
            ? []
            : (await _context.StoredFiles
                    .AsNoTracking()
                    .Where(f => logoIds.Contains(f.Id))
                    .ToListAsync(cancellationToken))
                .Select(StoredFileMapper.ToDomain)
                .ToList();

        IReadOnlyList<Guid> reinstatedAttendeeIds = [];
        if (raffle.State != RaffleState.Draft)
        {
            var reinstatedCandidates = await _context.RaffleAttendees
                .AsNoTracking()
                .Where(a => a.RaffleId == raffle.Id && a.ReinstatedAt != null)
                .Select(a => new
                {
                    a.Id,
                    a.ReinstatedAt,
                })
                .ToListAsync(cancellationToken);

            reinstatedAttendeeIds = reinstatedCandidates
                .Where(candidate =>
                {
                    var latestVoid = draws
                        .Where(d => !d.IsLive && d.WinnerAttendeeId == candidate.Id && d.VoidedAt != null)
                        .Select(d => d.VoidedAt!.Value)
                        .DefaultIfEmpty()
                        .Max();
                    return latestVoid != default && candidate.ReinstatedAt!.Value >= latestVoid;
                })
                .Select(c => c.Id)
                .ToList();
        }

        return new RaffleWithRounds(raffle, rounds)
        {
            Draws = draws,
            SponsorLogos = logos,
            ReinstatedAttendeeIds = reinstatedAttendeeIds,
        };
    }

    public Task<Result<Guid, BaseError>> AddRoundAsync(RaffleRound round, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(round);

        return DbResult.TryResultAsync<Guid>(_logger, "crear la ronda", async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var locked = await LockExistingRaffleAsync(round.RaffleId, cancellationToken);
            if (locked.IsFailure)
                return Result.Failure<Guid, BaseError>(locked.Error);

            // Bajo el bloqueo nadie más agrega ni elimina rondas de este sorteo, así que el mayor
            // más uno es el siguiente de verdad y no una carrera que detecta el índice único.
            var lastNumber = await _context.RaffleRounds
                .Where(r => r.RaffleId == round.RaffleId)
                .MaxAsync(r => (int?)r.Number, cancellationToken) ?? 0;

            _context.RaffleRounds.Add(new RaffleRoundData
            {
                Id = round.Id,
                RaffleId = round.RaffleId,
                Number = lastNumber + 1,
                Name = round.Name,
                Description = round.Description,
                CreatedAt = round.CreatedAt,
                UpdatedAt = round.UpdatedAt,
                CreatedById = round.CreatedById,
                UpdatedById = round.UpdatedById,
            });

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success<Guid, BaseError>(round.Id);
        });
    }

    public Task<Result<bool, BaseError>> UpdateRoundAsync(
        RaffleRound round,
        Func<IReadOnlyCollection<Guid>, IReadOnlyList<PrizeDraw>, UnitResult<BaseError>> guard,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(round);
        ArgumentNullException.ThrowIfNull(guard);

        return DbResult.TryResultAsync<bool>(_logger, "actualizar la ronda", async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            await LockRaffleAsync(round.RaffleId, cancellationToken);

            // Releídos bajo el bloqueo: un premio que entró en la ronda o se sorteó mientras tanto cuenta.
            var prizeIds = await _context.Prizes
                .Where(p => p.RoundId == round.Id)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);
            var allowed = guard(prizeIds, await ListDrawsAsync(round.RaffleId, cancellationToken));
            if (allowed.IsFailure)
                return Result.Failure<bool, BaseError>(allowed.Error);

            var updated = await _context.RaffleRounds
                .Where(r => r.Id == round.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Name, round.Name)
                    .SetProperty(r => r.Description, round.Description)
                    .SetProperty(r => r.UpdatedAt, round.UpdatedAt)
                    .SetProperty(r => r.UpdatedById, round.UpdatedById), cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result.Success<bool, BaseError>(updated > 0);
        });
    }

    public Task<Result<bool, BaseError>> DeleteRoundAsync(Guid raffleId, Guid roundId, CancellationToken cancellationToken)
    {
        return DbResult.TryAsync(_logger, "eliminar la ronda", async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            await LockRaffleAsync(raffleId, cancellationToken);

            var number = await _context.RaffleRounds
                .Where(r => r.Id == roundId && r.RaffleId == raffleId)
                .Select(r => (int?)r.Number)
                .FirstOrDefaultAsync(cancellationToken);

            if (number is null)
                return false;

            await _context.RaffleRounds
                .Where(r => r.Id == roundId)
                .ExecuteDeleteAsync(cancellationToken);

            // Las de detrás bajan un puesto en una sola sentencia. Pasa por el único
            // (raffle_id, number) porque es DEFERRABLE INITIALLY DEFERRED y se comprueba al
            // confirmar, cuando ya no queda ningún número repetido.
            await _context.RaffleRounds
                .Where(r => r.RaffleId == raffleId && r.Number > number)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Number, r => r.Number - 1), cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return true;
        });
    }

    public async Task<UnitResult<BaseError>> AddPrizesAsync(Guid raffleId, IReadOnlyList<Prize> prizes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prizes);

        var result = await DbResult.TryResultAsync<bool>(_logger, "crear los premios", async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var locked = await LockExistingRaffleAsync(raffleId, cancellationToken);
            if (locked.IsFailure)
                return Result.Failure<bool, BaseError>(locked.Error);

            _context.Prizes.AddRange(prizes.Select(p => new PrizeData
            {
                Id = p.Id,
                RoundId = p.RoundId,
                Name = p.Name,
                Description = p.Description,
                Value = p.Value,
                SponsorCompanyId = p.SponsorCompanyId,
                SponsorContactId = p.SponsorContactId,
                SponsorName = p.SponsorName,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                CreatedById = p.CreatedById,
                UpdatedById = p.UpdatedById,
            }));

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success<bool, BaseError>(true);
        });

        return result.IsSuccess ? UnitResult.Success<BaseError>() : UnitResult.Failure(result.Error);
    }

    public async Task<UnitResult<BaseError>> ImportPrizesAsync(
        Guid raffleId,
        IReadOnlyList<RaffleRound> newRounds,
        IReadOnlyList<Prize> newPrizes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(newRounds);
        ArgumentNullException.ThrowIfNull(newPrizes);

        var result = await DbResult.TryResultAsync<bool>(_logger, "importar los premios en bloque", async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var locked = await LockExistingRaffleAsync(raffleId, cancellationToken);
            if (locked.IsFailure)
                return Result.Failure<bool, BaseError>(locked.Error);

            var raffleData = await _context.Raffles
                .AsNoTracking()
                .SingleAsync(r => r.Id == raffleId, cancellationToken);
            var draftCheck = MapToRaffle(raffleData).EnsureDraft();
            if (draftCheck.IsFailure)
                return Result.Failure<bool, BaseError>(draftCheck.Error);

            var prevAutoDetect = _context.ChangeTracker.AutoDetectChangesEnabled;
            _context.ChangeTracker.AutoDetectChangesEnabled = false;
            try
            {
                if (newRounds.Count > 0)
                {
                    _context.RaffleRounds.AddRange(newRounds.Select(r => new RaffleRoundData
                    {
                        Id = r.Id,
                        RaffleId = r.RaffleId,
                        Number = r.Number,
                        Name = r.Name,
                        Description = r.Description,
                        CreatedAt = r.CreatedAt,
                        UpdatedAt = r.UpdatedAt,
                        CreatedById = r.CreatedById,
                        UpdatedById = r.UpdatedById,
                    }));
                }

                if (newPrizes.Count > 0)
                {
                    _context.Prizes.AddRange(newPrizes.Select(p => new PrizeData
                    {
                        Id = p.Id,
                        RoundId = p.RoundId,
                        Name = p.Name,
                        Description = p.Description,
                        Value = p.Value,
                        SponsorCompanyId = p.SponsorCompanyId,
                        SponsorContactId = p.SponsorContactId,
                        SponsorName = p.SponsorName,
                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt,
                        CreatedById = p.CreatedById,
                        UpdatedById = p.UpdatedById,
                    }));
                }

                await _context.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                _context.ChangeTracker.AutoDetectChangesEnabled = prevAutoDetect;
            }

            await transaction.CommitAsync(cancellationToken);
            return Result.Success<bool, BaseError>(true);
        });

        return result.IsSuccess ? UnitResult.Success<BaseError>() : UnitResult.Failure(result.Error);
    }


    public Task<Result<bool, BaseError>> UpdatePrizeAsync(
        Guid raffleId,
        Prize prize,
        Func<IReadOnlyList<PrizeDraw>, UnitResult<BaseError>> guard,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prize);

        return GuardedPrizeWriteAsync(raffleId, guard, "actualizar el premio", async () =>
            await _context.Prizes
                .Where(p => p.Id == prize.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.RoundId, prize.RoundId)
                    .SetProperty(p => p.Name, prize.Name)
                    .SetProperty(p => p.Description, prize.Description)
                    .SetProperty(p => p.Value, prize.Value)
                    .SetProperty(p => p.SponsorCompanyId, prize.SponsorCompanyId)
                    .SetProperty(p => p.SponsorContactId, prize.SponsorContactId)
                    .SetProperty(p => p.SponsorName, prize.SponsorName)
                    // Comparado contra el patrocinador que la fila tenía antes de esta sentencia, no el que
                    // leyó el servicio: un logo subido mientras tanto se conserva, y uno de otro
                    // patrocinador no sobrevive al cambio.
                    .SetProperty(
                        p => p.SponsorLogoFileId,
                        p => (p.SponsorCompanyId == prize.SponsorCompanyId && p.SponsorName == prize.SponsorName)
                            ? p.SponsorLogoFileId
                            : null)
                    .SetProperty(p => p.UpdatedAt, prize.UpdatedAt)
                    .SetProperty(p => p.UpdatedById, prize.UpdatedById), cancellationToken), cancellationToken);
    }

    public Task<Result<bool, BaseError>> DeletePrizeAsync(
        Guid raffleId,
        Guid prizeId,
        Func<IReadOnlyList<PrizeDraw>, UnitResult<BaseError>> guard,
        CancellationToken cancellationToken)
    {
        return GuardedPrizeWriteAsync(raffleId, guard, "eliminar el premio", () =>
            _context.Prizes
                .Where(p => p.Id == prizeId)
                .ExecuteDeleteAsync(cancellationToken), cancellationToken);
    }

    public async Task<UnitResult<BaseError>> SetPrizeSponsorLogoAsync(
        Guid raffleId,
        Guid prizeId,
        StoredFile logo,
        Func<Prize, IReadOnlyList<PrizeDraw>, UnitResult<BaseError>> guard,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(logo);
        ArgumentNullException.ThrowIfNull(guard);

        var result = await DbResult.TryResultAsync<bool>(_logger, "subir el logo del patrocinador", async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            // Releído bajo el bloqueo: un sorteo o un cambio de patrocinador que entró antes decide.
            var locked = await ReadPrizeUnderLockAsync(raffleId, prizeId, cancellationToken);
            if (locked.IsFailure)
                return Result.Failure<bool, BaseError>(locked.Error);

            var (_, prize, draws) = locked.Value;
            var allowed = guard(prize, draws);
            if (allowed.IsFailure)
                return Result.Failure<bool, BaseError>(allowed.Error);

            // El archivo y su enlace entran juntos. El logo anterior, si había, queda solo desenlazado.
            _context.StoredFiles.Add(StoredFileMapper.ToData(logo));
            await _context.SaveChangesAsync(cancellationToken);

            await _context.Prizes
                .Where(p => p.Id == prizeId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.SponsorLogoFileId, logo.Id)
                    .SetProperty(p => p.UpdatedAt, logo.CreatedAt)
                    .SetProperty(p => p.UpdatedById, logo.CreatedById), cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result.Success<bool, BaseError>(true);
        });

        return result.IsSuccess ? UnitResult.Success<BaseError>() : UnitResult.Failure(result.Error);
    }


    public Task<Result<bool, BaseError>> RemovePrizeSponsorLogoAsync(
        Guid raffleId,
        Guid prizeId,
        Guid removedById,
        DateTimeOffset removedAt,
        Func<IReadOnlyList<PrizeDraw>, UnitResult<BaseError>> guard,
        CancellationToken cancellationToken)
    {
        return GuardedPrizeWriteAsync(raffleId, guard, "quitar el logo del patrocinador", () =>
            _context.Prizes
                .Where(p => p.Id == prizeId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.SponsorLogoFileId, (Guid?)null)
                    .SetProperty(p => p.UpdatedAt, removedAt)
                    .SetProperty(p => p.UpdatedById, removedById), cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Una escritura sobre un premio bajo el bloqueo del sorteo, después de que <paramref name="guard"/>
    /// vea los sorteos releídos: una edición que compite con un sorteo de ese premio entra antes o se
    /// rechaza (ADR-0028). <c>false</c> si <paramref name="write"/> no tocó ninguna fila.
    /// </summary>
    private Task<Result<bool, BaseError>> GuardedPrizeWriteAsync(
        Guid raffleId,
        Func<IReadOnlyList<PrizeDraw>, UnitResult<BaseError>> guard,
        string operation,
        Func<Task<int>> write,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(guard);

        return DbResult.TryResultAsync<bool>(_logger, operation, async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            await LockRaffleAsync(raffleId, cancellationToken);

            var allowed = guard(await ListDrawsAsync(raffleId, cancellationToken));
            if (allowed.IsFailure)
                return Result.Failure<bool, BaseError>(allowed.Error);

            var affected = await write();

            await transaction.CommitAsync(cancellationToken);
            return Result.Success<bool, BaseError>(affected > 0);
        });
    }

    public Task<Result<IReadOnlyList<AttendeeOverride>, BaseError>> ListOverridesAsync(Guid raffleId, CancellationToken cancellationToken)
    {
        return DbResult.TryAsync<IReadOnlyList<AttendeeOverride>>(_logger, "listar las decisiones manuales del sorteo", async () =>
            await _context.RaffleAttendeeOverrides
                .AsNoTracking()
                .Where(o => o.RaffleId == raffleId)
                .Select(o => new AttendeeOverride(o.IdentityKey, o.Decision, o.Note, o.DecidedById, o.DecidedAt))
                .ToListAsync(cancellationToken));
    }

    public async Task<UnitResult<BaseError>> UpsertOverrideAsync(
        Guid raffleId,
        AttendeeOverride attendeeOverride,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attendeeOverride);

        var result = await DbResult.TryResultAsync<bool>(_logger, "guardar la decisión manual", async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var isDraft = await LockDraftAsync(raffleId, cancellationToken);
            if (isDraft.IsFailure)
                return Result.Failure<bool, BaseError>(isDraft.Error);

            // Una sola sentencia contra el índice único: dos escrituras simultáneas sobre la misma
            // persona terminan en la última, nunca en un choque ni en dos filas.
            await _context.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO events.raffle_attendee_override
                    (id, raffle_id, identity_key, decision, note, decided_by_id, decided_at)
                VALUES
                    ({Guid.NewGuid()}, {raffleId}, {attendeeOverride.IdentityKey}, {attendeeOverride.Decision},
                     {attendeeOverride.Note}, {attendeeOverride.DecidedById}, {attendeeOverride.DecidedAt})
                ON CONFLICT (raffle_id, identity_key) DO UPDATE SET
                    decision = EXCLUDED.decision,
                    note = EXCLUDED.note,
                    decided_by_id = EXCLUDED.decided_by_id,
                    decided_at = EXCLUDED.decided_at
                """,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result.Success<bool, BaseError>(true);
        });

        return result.IsSuccess ? UnitResult.Success<BaseError>() : UnitResult.Failure(result.Error);
    }


    public async Task<UnitResult<BaseError>> DeleteOverrideAsync(
        Guid raffleId,
        string identityKey,
        CancellationToken cancellationToken)
    {
        var result = await DbResult.TryResultAsync<bool>(_logger, "borrar la decisión manual", async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var isDraft = await LockDraftAsync(raffleId, cancellationToken);
            if (isDraft.IsFailure)
                return Result.Failure<bool, BaseError>(isDraft.Error);

            await _context.RaffleAttendeeOverrides
                .Where(o => o.RaffleId == raffleId && o.IdentityKey == identityKey)
                .ExecuteDeleteAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result.Success<bool, BaseError>(true);
        });

        return result.IsSuccess ? UnitResult.Success<BaseError>() : UnitResult.Failure(result.Error);
    }


    public async Task<UnitResult<BaseError>> UpsertOverridesAsync(
        Guid raffleId,
        IReadOnlyList<AttendeeOverride> overrides,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(overrides);

        var result = await DbResult.TryResultAsync<bool>(_logger, "guardar las decisiones manuales en lote", async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var isDraft = await LockDraftAsync(raffleId, cancellationToken);
            if (isDraft.IsFailure)
                return Result.Failure<bool, BaseError>(isDraft.Error);

            if (overrides.Count > 0)
            {
                var ids = overrides.Select(_ => Guid.NewGuid()).ToArray();
                var keys = overrides.Select(o => o.IdentityKey).ToArray();
                var decisions = overrides.Select(o => o.Decision).ToArray();
                var notes = overrides.Select(o => o.Note).ToArray();
                var decidedBy = overrides.Select(o => o.DecidedById).ToArray();
                var decidedAt = overrides.Select(o => o.DecidedAt).ToArray();

                // Una sola sentencia para el lote entero, sobre el mismo índice único que la de a uno.
                await _context.Database.ExecuteSqlAsync(
                    $"""
                    INSERT INTO events.raffle_attendee_override
                        (id, raffle_id, identity_key, decision, note, decided_by_id, decided_at)
                    SELECT t.id, {raffleId}, t.identity_key, t.decision, t.note, t.decided_by_id, t.decided_at
                    FROM unnest({ids}, {keys}, {decisions}, {notes}, {decidedBy}, {decidedAt})
                        AS t(id, identity_key, decision, note, decided_by_id, decided_at)
                    ON CONFLICT (raffle_id, identity_key) DO UPDATE SET
                        decision = EXCLUDED.decision,
                        note = EXCLUDED.note,
                        decided_by_id = EXCLUDED.decided_by_id,
                        decided_at = EXCLUDED.decided_at
                    """,
                    cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return Result.Success<bool, BaseError>(true);
        });

        return result.IsSuccess ? UnitResult.Success<BaseError>() : UnitResult.Failure(result.Error);
    }

    public Task<Result<IReadOnlyList<RosterPerson>, BaseError>> FindRosterPeopleAsync(
        Guid eventId,
        IReadOnlyCollection<string> emails,
        IReadOnlyCollection<string> employeeIds,
        IReadOnlyCollection<string> identityKeys,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(emails);
        ArgumentNullException.ThrowIfNull(employeeIds);
        ArgumentNullException.ThrowIfNull(identityKeys);

        return DbResult.TryAsync<IReadOnlyList<RosterPerson>>(_logger, "buscar personas en el padrón", async () =>
        {
            var emailList = emails.ToList();
            var idList = employeeIds.ToList();
            var keyList = identityKeys.ToList();

            return await _context.RaffleParticipants
                .AsNoTracking()
                .Where(p => p.EventId == eventId && (
                    (p.EmailNormalized != null && emailList.Contains(p.EmailNormalized))
                    || (p.EmployeeCode != null && idList.Contains(p.EmployeeCode.Trim().ToLower()))
                    || keyList.Contains(p.IdentityKey)))
                .Select(p => new RosterPerson(p.IdentityKey, p.EmailNormalized, p.EmployeeCode))
                .ToListAsync(cancellationToken);
        });
    }


    public async Task<UnitResult<BaseError>> StartAsync(
        Guid raffleId,
        Func<Raffle, IReadOnlyList<AttendeeOverride>, Result<RaffleFreeze, BaseError>> freeze,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(freeze);

        var result = await DbResult.TryResultAsync<bool>(_logger, "arrancar el sorteo", async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var locked = await LockExistingRaffleAsync(raffleId, cancellationToken);
            if (locked.IsFailure)
                return Result.Failure<bool, BaseError>(locked.Error);

            // Releído bajo el bloqueo: un arranque que esperaba detrás de otro ve el sorteo ya en
            // curso, y las decisiones manuales son las últimas que pudieron entrar.
            var data = await _context.Raffles
                .Where(r => r.Id == raffleId)
                .SingleAsync(cancellationToken);

            var overrides = await _context.RaffleAttendeeOverrides
                .AsNoTracking()
                .Where(o => o.RaffleId == raffleId)
                .Select(o => new AttendeeOverride(o.IdentityKey, o.Decision, o.Note, o.DecidedById, o.DecidedAt))
                .ToListAsync(cancellationToken);

            var decided = freeze(MapToRaffle(data), overrides);
            if (decided.IsFailure)
                return Result.Failure<bool, BaseError>(decided.Error);

            CopyToData(decided.Value.Started, data);
            _context.RaffleAttendees.AddRange(decided.Value.Attendees.Select(a => MapToAttendeeData(raffleId, a)));

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success<bool, BaseError>(true);
        });

        return result.IsSuccess ? UnitResult.Success<BaseError>() : UnitResult.Failure(result.Error);
    }


    public Task<Result<IReadOnlyList<RaffleAttendee>, BaseError>> ListFrozenAttendeesAsync(Guid raffleId, CancellationToken cancellationToken)
    {
        return DbResult.TryAsync(_logger, "listar los asistentes del sorteo", () => QueryFrozenAttendeesAsync(raffleId, cancellationToken));
    }

    public Task<Result<Maybe<PrizeWithDraws>, BaseError>> GetPrizeWithDrawsAsync(Guid raffleId, Guid prizeId, CancellationToken cancellationToken)
    {
        return DbResult.TryAsync<Maybe<PrizeWithDraws>>(_logger, "obtener el premio con sus sorteos", async () =>
        {
            var prizeData = await FindPrizeDataAsync(raffleId, prizeId, cancellationToken);
            if (prizeData is null)
                return Maybe<PrizeWithDraws>.None;

            var draws = (await ListDrawsAsync(raffleId, cancellationToken, prizeId))
                .OrderByDescending(d => d.DrawnAt)
                .ThenByDescending(d => d.Id)
                .ToList();

            var logo = prizeData.SponsorLogoFileId is { } logoId
                ? await _context.StoredFiles.AsNoTracking().FirstOrDefaultAsync(f => f.Id == logoId, cancellationToken)
                : null;

            return Maybe.From(new PrizeWithDraws(
                MapToPrize(prizeData), draws, logo is null ? null : StoredFileMapper.ToDomain(logo)));
        });
    }

    public async Task<Result<PrizeDraw, BaseError>> DrawAsync(
        Guid raffleId,
        Guid prizeId,
        Func<Raffle, Prize, IReadOnlyList<RaffleAttendee>, IReadOnlyList<PrizeDraw>, Result<PrizeDraw, BaseError>> decide,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decide);

        return await DbResult.TryResultAsync<PrizeDraw>(_logger, "sortear el premio", async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            await LockRaffleAsync(raffleId, cancellationToken);

            // Releído bajo el bloqueo: un sorteo que esperaba detrás de otro ve su ganador, y el bombo
            // que calcula ya no lo incluye si las reglas lo sacan.
            var raffleData = await _context.Raffles
                .AsNoTracking()
                .Where(r => r.Id == raffleId)
                .SingleAsync(cancellationToken);

            var prizeData = await FindPrizeDataAsync(raffleId, prizeId, cancellationToken);
            if (prizeData is null)
                return Result.Failure<PrizeDraw, BaseError>(RaffleErrors.PrizeNotFound());

            var raffle = MapToRaffle(raffleData);
            var attendees = await QueryAttendeesForDrawAsync(raffle, cancellationToken);
            var draws = await ListDrawsAsync(raffleId, cancellationToken);

            var decided = decide(raffle, MapToPrize(prizeData), attendees, draws);
            if (decided.IsFailure)
                return decided;

            var draw = decided.Value;
            for (var attempt = 1; await IsRedemptionCodeTakenAsync(draw.RedemptionCode, cancellationToken); attempt++)
            {
                if (attempt == MaxRedemptionCodeAttempts)
                    return Result.Failure<PrizeDraw, BaseError>(RedemptionCodeExhausted());

                draw = draw with { RedemptionCode = RedemptionCode.Generate() };
            }

            _context.PrizeDraws.Add(MapToDrawData(draw));

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success<PrizeDraw, BaseError>(draw);
        });
    }

    public async Task<Result<DrawBatchOutcome, BaseError>> DrawBatchAsync(
        Guid raffleId,
        IReadOnlyList<Guid> prizeIds,
        Func<Raffle, Prize, IReadOnlyList<RaffleAttendee>, IReadOnlyList<PrizeDraw>, Result<PrizeDraw, BaseError>> decide,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prizeIds);
        ArgumentNullException.ThrowIfNull(decide);

        return await DbResult.TryResultAsync<DrawBatchOutcome>(_logger, "sortear el lote de premios", async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            await LockRaffleAsync(raffleId, cancellationToken);

            var raffleData = await _context.Raffles
                .AsNoTracking()
                .Where(r => r.Id == raffleId)
                .SingleAsync(cancellationToken);

            var raffle = MapToRaffle(raffleData);

            // Una sola lectura de premios, asistentes y sorteos para todo el lote (A33 / R13).
            var prizeRows = await (
                from p in _context.Prizes.AsNoTracking()
                join r in _context.RaffleRounds.AsNoTracking() on p.RoundId equals r.Id
                where r.RaffleId == raffleId && prizeIds.Contains(p.Id)
                select p
            ).ToListAsync(cancellationToken);

            var prizesById = prizeRows.ToDictionary(p => p.Id, MapToPrize);
            foreach (var prizeId in prizeIds)
            {
                if (!prizesById.ContainsKey(prizeId))
                    return Result.Failure<DrawBatchOutcome, BaseError>(RaffleErrors.PrizeNotFound());
            }

            var attendees = await QueryAttendeesForDrawAsync(raffle, cancellationToken);
            var allDraws = (await ListDrawsAsync(raffleId, cancellationToken)).ToList();
            var drawnInBatch = new List<PrizeDraw>(prizeIds.Count);
            var usedCodes = new HashSet<string>(StringComparer.Ordinal);
            PoolExhaustedError? exhaustedError = null;

            foreach (var prizeId in prizeIds)
            {
                var prize = prizesById[prizeId];
                var decided = decide(raffle, prize, attendees, allDraws);
                if (decided.IsFailure)
                {
                    if (decided.Error is PoolExhaustedError poolExhausted)
                    {
                        exhaustedError = poolExhausted;
                        break;
                    }

                    return Result.Failure<DrawBatchOutcome, BaseError>(decided.Error);
                }

                var draw = decided.Value;
                for (var attempt = 1;
                     usedCodes.Contains(draw.RedemptionCode) || await IsRedemptionCodeTakenAsync(draw.RedemptionCode, cancellationToken);
                     attempt++)
                {
                    if (attempt == MaxRedemptionCodeAttempts)
                        return Result.Failure<DrawBatchOutcome, BaseError>(RedemptionCodeExhausted());

                    draw = draw with { RedemptionCode = RedemptionCode.Generate() };
                }

                usedCodes.Add(draw.RedemptionCode);
                _context.PrizeDraws.Add(MapToDrawData(draw));
                allDraws.Add(draw);
                drawnInBatch.Add(draw);
            }

            if (drawnInBatch.Count > 0)
            {
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            return Result.Success<DrawBatchOutcome, BaseError>(new DrawBatchOutcome(drawnInBatch, exhaustedError));
        });
    }

    public async Task<Result<PrizeDraw, BaseError>> VoidAsync(
        Guid raffleId,
        Guid prizeId,
        Func<Raffle, Prize, IReadOnlyList<PrizeDraw>, Result<PrizeDraw, BaseError>> decide,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decide);

        return await DbResult.TryResultAsync<PrizeDraw>(_logger, "anular el sorteo del premio", async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            await LockRaffleAsync(raffleId, cancellationToken);

            // Releído bajo el bloqueo: una anulación que esperaba detrás de un sorteo anula a ese
            // ganador, y una que esperaba detrás de otra anulación ya no encuentra ganador vivo.
            var raffleData = await _context.Raffles
                .AsNoTracking()
                .Where(r => r.Id == raffleId)
                .SingleAsync(cancellationToken);

            var prizeData = await FindPrizeDataAsync(raffleId, prizeId, cancellationToken);
            if (prizeData is null)
                return Result.Failure<PrizeDraw, BaseError>(RaffleErrors.PrizeNotFound());

            var draws = await ListDrawsAsync(raffleId, cancellationToken);

            var decided = decide(MapToRaffle(raffleData), MapToPrize(prizeData), draws);
            if (decided.IsFailure)
                return decided;

            var voided = decided.Value;
            await _context.PrizeDraws
                .Where(d => d.Id == voided.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.VoidCode, voided.VoidCode)
                    .SetProperty(d => d.VoidNote, voided.VoidNote)
                    .SetProperty(d => d.VoidedAt, voided.VoidedAt)
                    .SetProperty(d => d.VoidedById, voided.VoidedById), cancellationToken);

            // Anularlo otra vez revoca cualquier reactivación previa y lo saca del bombo.
            await _context.RaffleAttendees
                .Where(a => a.RaffleId == raffleId && a.Id == voided.WinnerAttendeeId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(a => a.ReinstatedAt, (DateTimeOffset?)null)
                    .SetProperty(a => a.ReinstatedById, (Guid?)null), cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result.Success<PrizeDraw, BaseError>(voided);
        });
    }

    public async Task<Result<PrizeHandover, BaseError>> HandOverAsync(
        Guid raffleId,
        Guid prizeId,
        StoredFile signature,
        Func<Raffle, Prize, IReadOnlyList<PrizeDraw>, Result<PrizeHandover, BaseError>> decide,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(decide);

        return await DbResult.TryResultAsync<PrizeHandover>(_logger, "entregar el premio", async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            // Releído bajo el bloqueo: una entrega que esperaba detrás de una anulación ya no encuentra
            // sorteo vivo, y una que esperaba detrás de otra entrega lo encuentra entregado.
            var locked = await ReadPrizeUnderLockAsync(raffleId, prizeId, cancellationToken);
            if (locked.IsFailure)
                return Result.Failure<PrizeHandover, BaseError>(locked.Error);

            var (raffle, prize, draws) = locked.Value;
            var decided = decide(raffle, prize, draws);
            if (decided.IsFailure)
                return decided;

            // El archivo y su enlace entran juntos: una firma sin entrega no se escribe nunca.
            _context.StoredFiles.Add(StoredFileMapper.ToData(signature));
            _context.PrizeHandovers.Add(MapToHandoverData(decided.Value));

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return decided;
        });
    }

    public async Task<Result<PrizeHandover, BaseError>> ReverseHandoverAsync(
        Guid raffleId,
        Guid prizeId,
        Func<Raffle, Prize, IReadOnlyList<PrizeDraw>, Result<PrizeHandover, BaseError>> decide,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decide);

        return await DbResult.TryResultAsync<PrizeHandover>(_logger, "revertir la entrega del premio", async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            var locked = await ReadPrizeUnderLockAsync(raffleId, prizeId, cancellationToken);
            if (locked.IsFailure)
                return Result.Failure<PrizeHandover, BaseError>(locked.Error);

            var (raffle, prize, draws) = locked.Value;
            var decided = decide(raffle, prize, draws);
            if (decided.IsFailure)
                return decided;

            var reversed = decided.Value;
            await _context.PrizeHandovers
                .Where(h => h.Id == reversed.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(h => h.ReversedAt, reversed.ReversedAt)
                    .SetProperty(h => h.ReversedById, reversed.ReversedById)
                    .SetProperty(h => h.ReversalReason, reversed.ReversalReason), cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return decided;
        });
    }

    public Task<Result<Maybe<PrizeDrawLookup>, BaseError>> FindDrawByCodeAsync(
        string redemptionCode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(redemptionCode);

        return DbResult.TryAsync<Maybe<PrizeDrawLookup>>(_logger, "buscar el sorteo por su código de retiro", async () =>
        {
            var found = await (from d in _context.PrizeDraws.AsNoTracking()
                               join p in _context.Prizes on d.PrizeId equals p.Id
                               join rr in _context.RaffleRounds on p.RoundId equals rr.Id
                               join r in _context.Raffles on rr.RaffleId equals r.Id
                               // El evento filtrado es el que pone la cuenta: el código se busca
                               // solo en los sorteos de quien pregunta (auditoría run-1, pendiente 4).
                               join e in _context.Events on r.EventId equals e.Id
                               where d.RedemptionCode == redemptionCode
                               select new { Draw = d, PrizeName = p.Name, RaffleId = r.Id, r.EventId })
                .SingleOrDefaultAsync(cancellationToken);

            if (found is null)
                return Maybe<PrizeDrawLookup>.None;

            var handovers = await _context.PrizeHandovers
                .AsNoTracking()
                .Where(h => h.PrizeDrawId == found.Draw.Id)
                .ToListAsync(cancellationToken);

            var draw = WithHandovers([found.Draw], handovers).Single();
            return Maybe.From(new PrizeDrawLookup(draw, found.EventId, found.RaffleId, found.PrizeName));
        });
    }

    public async Task<UnitResult<BaseError>> CompleteAsync(
        Guid raffleId,
        Guid completedById,
        DateTimeOffset now,
        Func<NotarialRecordFacts, int, NotarialRecordReplaces?, Task<Result<StoredFile, BaseError>>> produce,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(produce);

        var result = await DbResult.TryResultAsync<bool>(_logger, "completar el sorteo", async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            await LockRaffleAsync(raffleId, cancellationToken);

            // Releído bajo el bloqueo, que se sostiene hasta confirmar: un sorteo o una anulación que
            // esperaban detrás no pueden quedar fuera del acta ni entrar después de ella.
            var data = await _context.Raffles
                .Where(r => r.Id == raffleId)
                .SingleAsync(cancellationToken);

            var completed = MapToRaffle(data).Complete(completedById, now);
            if (completed.IsFailure)
                return Result.Failure<bool, BaseError>(completed.Error);

            var facts = await ReadRecordFactsAsync(completed.Value, cancellationToken);

            var produced = await produce(facts, NotarialRecord.FirstVersion, null);
            if (produced.IsFailure)
                return Result.Failure<bool, BaseError>(produced.Error);

            var file = produced.Value;
            _context.StoredFiles.Add(StoredFileMapper.ToData(file));
            _context.NotarialRecords.Add(new NotarialRecordData
            {
                Id = Guid.NewGuid(),
                RaffleId = raffleId,
                Version = NotarialRecord.FirstVersion,
                StoredFileId = file.Id,
                GeneratedAt = now,
                GeneratedById = completedById,
            });
            CopyToData(completed.Value, data);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success<bool, BaseError>(true);
        });

        return result.IsSuccess ? UnitResult.Success<BaseError>() : UnitResult.Failure(result.Error);
    }


    public async Task<UnitResult<BaseError>> ReissueAsync(
        Guid raffleId,
        RaffleAuthorities authorities,
        Guid reissuedById,
        DateTimeOffset now,
        Func<NotarialRecordFacts, int, NotarialRecordReplaces?, Task<Result<StoredFile, BaseError>>> produce,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(produce);

        var result = await DbResult.TryResultAsync<bool>(_logger, "reemitir el acta del sorteo", async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            await LockRaffleAsync(raffleId, cancellationToken);

            var data = await _context.Raffles
                .Where(r => r.Id == raffleId)
                .SingleAsync(cancellationToken);

            var reissued = MapToRaffle(data).Reissue(authorities, reissuedById, now);
            if (reissued.IsFailure)
                return Result.Failure<bool, BaseError>(reissued.Error);

            // La vigente es la más nueva, leída bajo el bloqueo: una reemisión que esperaba detrás de
            // otra ve la que esa escribió y numera la siguiente.
            var current = await _context.NotarialRecords
                .AsNoTracking()
                .Where(n => n.RaffleId == raffleId)
                .OrderByDescending(n => n.Version)
                .Select(n => new NotarialRecordReplaces(n.Version, n.GeneratedAt))
                .FirstAsync(cancellationToken);

            var facts = await ReadRecordFactsAsync(reissued.Value, cancellationToken);
            var version = current.Version + 1;

            var produced = await produce(facts, version, current);
            if (produced.IsFailure)
                return Result.Failure<bool, BaseError>(produced.Error);

            var file = produced.Value;
            _context.StoredFiles.Add(StoredFileMapper.ToData(file));
            _context.NotarialRecords.Add(new NotarialRecordData
            {
                Id = Guid.NewGuid(),
                RaffleId = raffleId,
                Version = version,
                StoredFileId = file.Id,
                GeneratedAt = now,
                GeneratedById = reissuedById,
            });
            CopyToData(reissued.Value, data);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success<bool, BaseError>(true);
        });

        return result.IsSuccess ? UnitResult.Success<BaseError>() : UnitResult.Failure(result.Error);
    }


    /// <summary>
    /// Todo lo que el acta afirma sobre un sorteo ya completado, leído dentro de la transacción que
    /// tiene su bloqueo: rondas, premios y sorteos, la lista congelada y el evento.
    /// </summary>
    private async Task<NotarialRecordFacts> ReadRecordFactsAsync(Raffle raffle, CancellationToken cancellationToken) => new(
        await LoadRoundsAsync(raffle, cancellationToken),
        await QueryFrozenAttendeesAsync(raffle.Id, cancellationToken),
        await ReadRecordEventAsync(raffle.EventId, cancellationToken))
    {
        LinkedRaffleNames = await QueryLinkedRaffleNamesAsync(raffle, cancellationToken),
    };

    /// <summary>
    /// El evento como lo dice el acta, con sus ciudades vivas en orden de pantalla. Sin mirar su
    /// estado: un evento cerrado o cancelado no impide completar (spec 00 §5). La zona se lee acá,
    /// en vivo, y no se guarda con el acta: una reemisión toma la zona corregida.
    /// </summary>
    private async Task<NotarialRecordEvent> ReadRecordEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var eventData = await _context.Events
            .AsNoTracking()
            .Where(e => e.Id == eventId)
            .Select(e => new { e.AccountId, e.Name, e.StartDate, e.EndDate })
            .SingleAsync(cancellationToken);

        var cities = await _context.RoadshowCities
            .AsNoTracking()
            .Where(c => c.EventId == eventId && c.DeletedAt == null)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.CreatedAt)
            .Select(c => new { c.Name, c.Country, c.TimeZone })
            .ToListAsync(cancellationToken);

        // La zona se validó al escribirla. Si hoy no se reconoce, el acta no puede decir sus horas
        // con verdad, y no completar es mejor que imprimirlas en otro reloj.
        var places = cities
            .Select(c => new NotarialRecordPlace(c.Name, c.Country, CityTimeZone.Create(c.TimeZone).Value))
            .ToList();

        // R7: la marca se lee de la cuenta dueña del evento (primero la marca del evento, luego la de esa cuenta).
        var brandings = await _context.BrandingSettings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(b => b.AccountId == eventData.AccountId && (b.EventId == eventId || b.EventId == null))
            .Select(b => new { b.EventId, b.OrganizationName, b.UpdatedAt })
            .ToListAsync(cancellationToken);

        var eventOrganizer = brandings
            .FirstOrDefault(b => b.EventId == eventId)?.OrganizationName;
        var accountOrganizer = brandings
            .Where(b => b.EventId == null)
            .OrderByDescending(b => b.UpdatedAt)
            .FirstOrDefault()?.OrganizationName;
        var organizerName = !string.IsNullOrWhiteSpace(eventOrganizer)
            ? eventOrganizer.Trim()
            : !string.IsNullOrWhiteSpace(accountOrganizer)
                ? accountOrganizer.Trim()
                : null;

        return new NotarialRecordEvent(eventData.Name, eventData.StartDate, eventData.EndDate, places, organizerName);
    }

    public Task<Result<bool, BaseError>> UpdateWinnerNotificationAsync(
        Guid drawId,
        string winnerNotificationStatus,
        string? winnerNotificationId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(winnerNotificationStatus);

        return DbResult.TryAsync<bool>(_logger, "registrar el correo al ganador", async () =>
        {
            var updated = await _context.PrizeDraws
                .Where(d => d.Id == drawId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.WinnerNotificationStatus, winnerNotificationStatus)
                    .SetProperty(d => d.WinnerNotificationId, winnerNotificationId), cancellationToken);

            return updated == 1;
        });
    }

    public async Task<UnitResult<BaseError>> ReopenAsync(
        Guid raffleId,
        Func<Raffle, IReadOnlyList<PrizeDraw>, Result<Raffle, BaseError>> decide,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decide);

        var result = await DbResult.TryResultAsync<bool>(_logger, "reabrir el sorteo", async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var locked = await LockExistingRaffleAsync(raffleId, cancellationToken);
            if (locked.IsFailure)
                return Result.Failure<bool, BaseError>(locked.Error);

            // Releído bajo el bloqueo: una reapertura que esperaba detrás del primer sorteo lo ve.
            var data = await _context.Raffles
                .Where(r => r.Id == raffleId)
                .SingleAsync(cancellationToken);

            var decided = decide(MapToRaffle(data), await ListDrawsAsync(raffleId, cancellationToken));
            if (decided.IsFailure)
                return Result.Failure<bool, BaseError>(decided.Error);

            await _context.RaffleAttendees
                .Where(a => a.RaffleId == raffleId)
                .ExecuteDeleteAsync(cancellationToken);

            CopyToData(decided.Value, data);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success<bool, BaseError>(true);
        });

        return result.IsSuccess ? UnitResult.Success<BaseError>() : UnitResult.Failure(result.Error);
    }

    public async Task<UnitResult<BaseError>> ReinstateAttendeeAsync(
        Guid raffleId,
        Guid attendeeId,
        Guid reinstatedById,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var result = await DbResult.TryResultAsync<bool>(_logger, "reactivar al participante en el sorteo", async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var locked = await LockExistingRaffleAsync(raffleId, cancellationToken);
            if (locked.IsFailure)
                return Result.Failure<bool, BaseError>(locked.Error);

            var data = await _context.Raffles
                .AsNoTracking()
                .Where(r => r.Id == raffleId)
                .SingleAsync(cancellationToken);

            var inProgress = MapToRaffle(data).EnsureInProgress();
            if (inProgress.IsFailure)
                return Result.Failure<bool, BaseError>(inProgress.Error);

            var draws = await ListDrawsAsync(raffleId, cancellationToken);
            var hasVoidedDraw = draws.Any(d => !d.IsLive && d.WinnerAttendeeId == attendeeId);
            if (!hasVoidedDraw)
                return Result.Failure<bool, BaseError>(RaffleErrors.AttendeeNotVoided());

            var updated = await _context.RaffleAttendees
                .Where(a => a.RaffleId == raffleId && a.Id == attendeeId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(a => a.ReinstatedAt, now)
                    .SetProperty(a => a.ReinstatedById, reinstatedById), cancellationToken);

            if (updated == 0)
                return Result.Failure<bool, BaseError>(RaffleErrors.AttendeeNotVoided());

            await transaction.CommitAsync(cancellationToken);
            return Result.Success<bool, BaseError>(true);
        });

        return result.IsSuccess ? UnitResult.Success<BaseError>() : UnitResult.Failure(result.Error);
    }

    /// <summary>
    /// Bloquea la fila del sorteo y relee el sorteo, el premio y todos los sorteos con sus entregas, que
    /// es lo que deciden la entrega y su reversión. <c>PRIZE_NOT_FOUND</c> si el premio se fue antes
    /// del bloqueo. Corre dentro de la transacción de quien llama.
    /// </summary>
    private async Task<Result<(Raffle Raffle, Prize Prize, IReadOnlyList<PrizeDraw> Draws), BaseError>> ReadPrizeUnderLockAsync(
        Guid raffleId,
        Guid prizeId,
        CancellationToken cancellationToken)
    {
        await LockRaffleAsync(raffleId, cancellationToken);

        var raffleData = await _context.Raffles
            .AsNoTracking()
            .Where(r => r.Id == raffleId)
            .SingleAsync(cancellationToken);

        var prizeData = await FindPrizeDataAsync(raffleId, prizeId, cancellationToken);
        if (prizeData is null)
            return RaffleErrors.PrizeNotFound();

        var draws = await ListDrawsAsync(raffleId, cancellationToken);
        return (MapToRaffle(raffleData), MapToPrize(prizeData), draws);
    }

    /// <summary>El premio, solo si es de una ronda de este sorteo.</summary>
    private Task<PrizeData?> FindPrizeDataAsync(Guid raffleId, Guid prizeId, CancellationToken cancellationToken) =>
        _context.Prizes
            .AsNoTracking()
            .Where(p => p.Id == prizeId && _context.RaffleRounds.Any(r => r.Id == p.RoundId && r.RaffleId == raffleId))
            .FirstOrDefaultAsync(cancellationToken);

    private Task<bool> IsRedemptionCodeTakenAsync(string code, CancellationToken cancellationToken) =>
        _context.PrizeDraws.AnyAsync(d => d.RedemptionCode == code, cancellationToken);

    /// <summary>
    /// Tres códigos seguidos ya usados. Entre 10¹² posibles no es algo que vaya a pasar; está para que
    /// la cota quede dicha y no supuesta (spec 03 §2.3), con el mismo código que el índice único.
    /// </summary>
    private static BaseError RedemptionCodeExhausted() => new(
        "Error al sortear el premio: no se pudo generar un código de retiro sin usar.",
        "DB_CONFLICT",
        ErrorKind.Conflict);

    /// <summary>
    /// Todos los sorteos de los premios del sorteo, vivos y anulados, sin orden, cada uno con sus
    /// entregas de la más nueva a la más vieja.
    /// </summary>
    private async Task<IReadOnlyList<PrizeDraw>> ListDrawsAsync(Guid raffleId, CancellationToken cancellationToken, Guid? prizeId = null)
    {
        var drawIds = from d in _context.PrizeDraws
                      join p in _context.Prizes on d.PrizeId equals p.Id
                      join r in _context.RaffleRounds on p.RoundId equals r.Id
                      where r.RaffleId == raffleId && (prizeId == null || d.PrizeId == prizeId)
                      select d.Id;

        var draws = await _context.PrizeDraws
            .AsNoTracking()
            .Where(d => drawIds.Contains(d.Id))
            .ToListAsync(cancellationToken);

        if (draws.Count == 0)
            return [];

        var handovers = await _context.PrizeHandovers
            .AsNoTracking()
            .Where(h => drawIds.Contains(h.PrizeDrawId))
            .ToListAsync(cancellationToken);

        var winnerEmails = draws
            .Select(d => d.WinnerIdentityKey)
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Dictionary<string, WinnerRosterInfo>? participantByEmail = null;
        if (winnerEmails.Count > 0)
        {
            var eventId = await _context.Raffles
                .AsNoTracking()
                .Where(r => r.Id == raffleId)
                .Select(r => r.EventId)
                .FirstOrDefaultAsync(cancellationToken);

            if (eventId != Guid.Empty)
            {
                var participants = await _context.RaffleParticipants
                    .AsNoTracking()
                    .Where(p => p.EventId == eventId && winnerEmails.Contains(p.IdentityKey))
                    .Select(p => new
                    {
                        p.IdentityKey,
                        p.TicketNumber,
                        p.EmployeeCode,
                        p.Department,
                        p.Location,
                        p.FirstName,
                        p.LastName,
                    })
                    .ToListAsync(cancellationToken);

                participantByEmail = new Dictionary<string, WinnerRosterInfo>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in participants)
                {
                    participantByEmail.TryAdd(
                        p.IdentityKey,
                        new WinnerRosterInfo(
                            string.IsNullOrWhiteSpace(p.TicketNumber) ? null : p.TicketNumber,
                            p.EmployeeCode,
                            p.Department,
                            p.Location,
                            p.FirstName,
                            p.LastName));
                }
            }
        }

        return WithHandovers(draws, handovers, participantByEmail);
    }

    /// <summary>
    /// Los sorteos con sus entregas colgadas, de la más nueva a la más vieja: la que se ve primero en
    /// la historia es la que está en pie, si la hay.
    /// </summary>
    private static IReadOnlyList<PrizeDraw> WithHandovers(
        IReadOnlyList<PrizeDrawData> draws,
        IReadOnlyList<PrizeHandoverData> handovers,
        IReadOnlyDictionary<string, WinnerRosterInfo>? participantByEmail = null)
    {
        var byDraw = handovers.ToLookup(h => h.PrizeDrawId, MapToHandover);

        return draws
            .Select(d =>
            {
                var mapped = MapToDraw(d);
                WinnerRosterInfo? info = null;
                if (participantByEmail is not null && !string.IsNullOrWhiteSpace(d.WinnerIdentityKey))
                    participantByEmail.TryGetValue(d.WinnerIdentityKey, out info);

                return mapped with
                {
                    WinnerTicketNumber = info?.TicketNumber,
                    WinnerEmployeeCode = info?.EmployeeCode,
                    WinnerDepartment = info?.Department,
                    WinnerLocation = info?.Location,
                    WinnerFirstName = info?.FirstName,
                    WinnerLastName = info?.LastName,
                    Handovers = byDraw[d.Id]
                        .OrderByDescending(h => h.RedeemedAt)
                        .ThenByDescending(h => h.Id)
                        .ToList(),
                };
            })
            .ToList();
    }

    private async Task<IReadOnlyList<RaffleAttendee>> QueryFrozenAttendeesAsync(Guid raffleId, CancellationToken cancellationToken) =>
        await _context.RaffleAttendees
            .AsNoTracking()
            .Where(a => a.RaffleId == raffleId)
            .Select(a => new RaffleAttendee(
                a.Id,
                a.IdentityKey,
                a.ContactId,
                a.FirstName,
                a.LastName,
                a.Company,
                a.CheckedInAt,
                a.Qualified,
                a.ExclusionReason,
                a.ManualDecision,
                a.ManualDecidedById,
                a.ManualDecidedAt,
                a.ManualNote,
                a.OverruledReason,
                a.ReinstatedAt,
                a.ReinstatedById))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Los asistentes congelados para girar, ya marcados si ganaron en un sorteo vinculado. Antes toma
    /// el bloqueo del grupo, así que dos sorteos vinculados no giran a la vez y ninguno elige a quien
    /// el otro acaba de premiar. Va después del bloqueo de la fila del sorteo: siempre en ese orden.
    /// </summary>
    private async Task<IReadOnlyList<RaffleAttendee>> QueryAttendeesForDrawAsync(
        Raffle raffle, CancellationToken cancellationToken)
    {
        await _context.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({raffle.GroupEventId}::text, 0))", cancellationToken);

        return RaffleAttendee.TagLinkedWinners(
            await QueryFrozenAttendeesAsync(raffle.Id, cancellationToken),
            await QueryLinkedWinnerKeysAsync(raffle, cancellationToken));
    }

    /// <summary>Los sorteos del mismo grupo que <paramref name="raffle"/>, sin él, de la cuenta en curso.</summary>
    private IQueryable<RaffleData> LinkedRafflesOf(Raffle raffle)
    {
        var groupEventId = raffle.GroupEventId;
        return from r in _context.Raffles.AsNoTracking()
               join e in _context.Events.AsNoTracking() on r.EventId equals e.Id
               where r.Id != raffle.Id && (r.LinkedEventId == groupEventId || (r.LinkedEventId == null && r.EventId == groupEventId))
               select r;
    }

    /// <summary>
    /// Las claves de persona, en este sorteo, de quien tiene un premio vivo en un sorteo vinculado. Se
    /// cruza por clave y también por ID de empleado: la misma persona puede venir con correo en un
    /// padrón y solo con su ID en otro, y entonces sus claves difieren.
    /// </summary>
    private async Task<IReadOnlySet<string>> QueryLinkedWinnerKeysAsync(Raffle raffle, CancellationToken cancellationToken)
    {
        var winners = await (
            from d in _context.PrizeDraws.AsNoTracking()
            join p in _context.Prizes.AsNoTracking() on d.PrizeId equals p.Id
            join rr in _context.RaffleRounds.AsNoTracking() on p.RoundId equals rr.Id
            join r in LinkedRafflesOf(raffle) on rr.RaffleId equals r.Id
            where d.VoidedAt == null
            select new
            {
                Key = d.WinnerIdentityKey,
                Code = _context.RaffleParticipants
                    .Where(rp => rp.EventId == r.EventId && rp.IdentityKey == d.WinnerIdentityKey)
                    .Select(rp => rp.EmployeeCode)
                    .FirstOrDefault(),
            }
        ).ToListAsync(cancellationToken);

        var keys = winners.Select(w => w.Key).ToHashSet(StringComparer.Ordinal);
        var codes = winners
            .Select(w => PersonIdentity.NormalizeId(w.Code) ?? PersonIdentity.IdOf(w.Key))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (codes.Count == 0)
            return keys;

        foreach (var code in codes)
            keys.Add(PersonIdentity.IdPrefix + code);

        // Quien en este padrón trae correo y el mismo ID que un ganador vinculado.
        var sameId = await _context.RaffleParticipants.AsNoTracking()
            .Where(rp => rp.EventId == raffle.EventId
                && rp.EmployeeCode != null
                && codes.Contains(rp.EmployeeCode.Trim().ToLower()))
            .Select(rp => rp.IdentityKey)
            .ToListAsync(cancellationToken);
        keys.UnionWith(sameId);
        return keys;
    }

    public Task<Result<IReadOnlySet<string>, BaseError>> ListLinkedWinnerKeysAsync(Raffle raffle, CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "leer los ganadores de los sorteos vinculados", () =>
            QueryLinkedWinnerKeysAsync(raffle, cancellationToken));

    public Task<Result<IReadOnlyList<string>, BaseError>> ListLinkedRaffleNamesAsync(Raffle raffle, CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "leer los sorteos vinculados", async () =>
            (IReadOnlyList<string>)await QueryLinkedRaffleNamesAsync(raffle, cancellationToken));

    private Task<List<string>> QueryLinkedRaffleNamesAsync(Raffle raffle, CancellationToken cancellationToken) => (
        from r in LinkedRafflesOf(raffle)
        join e in _context.Events.AsNoTracking() on r.EventId equals e.Id
        orderby e.StartDate, e.Name
        select e.Name
    ).ToListAsync(cancellationToken);

    public Task<Result<bool, BaseError>> HasRafflesLinkedToAsync(Guid eventId, CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "buscar sorteos vinculados", () =>
            _context.Raffles.AsNoTracking().AnyAsync(r => r.LinkedEventId == eventId, cancellationToken));

    /// <summary>
    /// Bloquea la fila del sorteo hasta el final de la transacción en curso. Es lo que serializa
    /// las altas y bajas de rondas de un mismo sorteo —sin él, dos altas simultáneas tomarían el
    /// mismo número y una baja concurrente renumeraría bajo los pies de la otra—, el arranque
    /// contra sí mismo y contra las decisiones manuales, y la fase de sorteo entera (ADR-0028).
    /// </summary>
    private Task LockRaffleAsync(Guid raffleId, CancellationToken cancellationToken) =>
        _context.Database.ExecuteSqlAsync(
            $"SELECT id FROM events.raffle WHERE id = {raffleId} FOR UPDATE", cancellationToken);

    /// <summary>
    /// Bloquea la fila del sorteo como <see cref="LockRaffleAsync"/> y dice si seguía ahí. Un borrador
    /// se puede eliminar mientras otra escritura de armado espera este bloqueo, y esa escritura tiene
    /// que contestar <c>RAFFLE_NOT_FOUND</c>, no fallar al releer ni escribir contra un sorteo que ya
    /// no existe. Las escrituras de la noche del sorteo no lo necesitan: exigen un sorteo arrancado, y
    /// solo un borrador se elimina.
    /// </summary>
    private async Task<UnitResult<BaseError>> LockExistingRaffleAsync(Guid raffleId, CancellationToken cancellationToken)
    {
        var locked = await _context.Database
            .SqlQuery<Guid>($"SELECT id AS \"Value\" FROM events.raffle WHERE id = {raffleId} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return locked.Count == 0 ? UnitResult.Failure(RaffleErrors.NotFound()) : UnitResult.Success<BaseError>();
    }

    /// <summary>
    /// Bloquea la fila del sorteo y comprueba, ya bajo el bloqueo, que siga en draft. Es lo que
    /// ordena una decisión manual contra el arranque: o entra antes y forma parte del congelado, o
    /// llega después y es <c>RAFFLE_NOT_DRAFT</c> (spec 01 §9.1).
    /// </summary>
    private async Task<UnitResult<BaseError>> LockDraftAsync(Guid raffleId, CancellationToken cancellationToken)
    {
        var locked = await LockExistingRaffleAsync(raffleId, cancellationToken);
        if (locked.IsFailure)
            return locked;

        var data = await _context.Raffles
            .AsNoTracking()
            .Where(r => r.Id == raffleId)
            .SingleAsync(cancellationToken);

        return MapToRaffle(data).EnsureDraft();
    }

    private static void CopyToData(Raffle raffle, RaffleData data)
    {
        data.EventId = raffle.EventId;
        data.State = raffle.State;
        data.VisualStyle = raffle.VisualStyle;
        data.ShowPrizeOnScreen = raffle.ShowPrizeOnScreen;
        data.AnimationSeconds = raffle.AnimationSeconds;
        data.RequiresPresenceConfirmation = raffle.RequiresPresenceConfirmation;
        data.RequiresCheckIn = raffle.RequiresCheckIn;
        data.OnePrizePerPerson = raffle.OnePrizePerPerson;
        data.OnePrizePerCompany = raffle.OnePrizePerCompany;
        data.LinkedEventId = raffle.LinkedEventId;
        data.BoardTitle = raffle.BoardTitle;
        data.BoardSubtitle = raffle.BoardSubtitle;
        data.BoardIdentifier = raffle.BoardIdentifier;
        data.BoardBatchSize = raffle.BoardBatchSize;
        data.NotaryName = raffle.Authorities.NotaryName;
        data.NotaryIdentityDocument = raffle.Authorities.NotaryIdentityDocument;
        data.WitnessOneName = raffle.Authorities.WitnessOneName;
        data.WitnessOneIdentityDocument = raffle.Authorities.WitnessOneIdentityDocument;
        data.WitnessTwoName = raffle.Authorities.WitnessTwoName;
        data.WitnessTwoIdentityDocument = raffle.Authorities.WitnessTwoIdentityDocument;
        data.CreatedAt = raffle.CreatedAt;
        data.UpdatedAt = raffle.UpdatedAt;
        data.CreatedById = raffle.CreatedById;
        data.UpdatedById = raffle.UpdatedById;
        data.StartedAt = raffle.StartedAt;
        data.StartedById = raffle.StartedById;
        data.AwardsBatchId = raffle.AwardsBatchId;
        data.PickupInstructions = raffle.PickupInstructions;
        data.CertificateSignerOneName = raffle.CertificateSigners.One?.Name;
        data.CertificateSignerOneTitle = raffle.CertificateSigners.One?.Title;
        data.CertificateSignerTwoName = raffle.CertificateSigners.Two?.Name;
        data.CertificateSignerTwoTitle = raffle.CertificateSigners.Two?.Title;
        data.CompletedAt = raffle.CompletedAt;
        data.CompletedById = raffle.CompletedById;
        data.DisplayNameMode = raffle.DisplayFields.Name;
        data.DisplayIdentifierMode = raffle.DisplayFields.Identifier;
        data.DisplayDetailMode = raffle.DisplayFields.Detail;
    }

    private static PrizeDrawData MapToDrawData(PrizeDraw draw) => new()
    {
        Id = draw.Id,
        PrizeId = draw.PrizeId,
        WinnerAttendeeId = draw.WinnerAttendeeId,
        WinnerName = draw.WinnerName,
        WinnerCompany = draw.WinnerCompany,
        WinnerIdentityKey = draw.WinnerIdentityKey,
        DrawnAt = draw.DrawnAt,
        DrawnById = draw.DrawnById,
        EligiblePoolSize = draw.EligiblePoolSize,
        OnePrizePerPerson = draw.OnePrizePerPerson,
        OnePrizePerCompany = draw.OnePrizePerCompany,
        RedemptionCode = draw.RedemptionCode,
        VoidCode = draw.VoidCode,
        VoidNote = draw.VoidNote,
        VoidedAt = draw.VoidedAt,
        VoidedById = draw.VoidedById,
        WinnerNotificationId = draw.WinnerNotificationId,
        WinnerNotificationStatus = draw.WinnerNotificationStatus,
    };

    private static PrizeDraw MapToDraw(PrizeDrawData data) => new(
        data.Id,
        data.PrizeId,
        data.WinnerAttendeeId,
        data.WinnerName,
        data.WinnerCompany,
        data.WinnerIdentityKey,
        data.DrawnAt,
        data.DrawnById,
        data.EligiblePoolSize,
        data.OnePrizePerPerson,
        data.OnePrizePerCompany,
        data.RedemptionCode,
        data.VoidCode,
        data.VoidNote,
        data.VoidedAt,
        data.VoidedById)
    {
        WinnerNotificationId = data.WinnerNotificationId,
        WinnerNotificationStatus = data.WinnerNotificationStatus,
    };

    private static PrizeHandover MapToHandover(PrizeHandoverData data) => new(
        data.Id,
        data.PrizeDrawId,
        data.ReceiverName,
        data.ReceiverIdentityDocument,
        data.ReceiverIsWinner,
        data.SignatureStoredFileId,
        data.RedeemedAt,
        data.RedeemedById,
        data.ReversedAt,
        data.ReversedById,
        data.ReversalReason);

    private static PrizeHandoverData MapToHandoverData(PrizeHandover handover) => new()
    {
        Id = handover.Id,
        PrizeDrawId = handover.PrizeDrawId,
        ReceiverName = handover.ReceiverName,
        ReceiverIdentityDocument = handover.ReceiverIdentityDocument,
        ReceiverIsWinner = handover.ReceiverIsWinner,
        SignatureStoredFileId = handover.SignatureStoredFileId,
        RedeemedAt = handover.RedeemedAt,
        RedeemedById = handover.RedeemedById,
        ReversedAt = handover.ReversedAt,
        ReversedById = handover.ReversedById,
        ReversalReason = handover.ReversalReason,
    };

    private static RaffleAttendeeData MapToAttendeeData(Guid raffleId, RaffleAttendee attendee) => new()
    {
        Id = attendee.Id,
        RaffleId = raffleId,
        IdentityKey = attendee.IdentityKey,
        ContactId = attendee.ContactId,
        FirstName = attendee.FirstName,
        LastName = attendee.LastName,
        Company = attendee.Company,
        CheckedInAt = attendee.CheckedInAt,
        Qualified = attendee.Qualified,
        ExclusionReason = attendee.ExclusionReason,
        ManualDecision = attendee.ManualDecision,
        ManualDecidedById = attendee.ManualDecidedById,
        ManualDecidedAt = attendee.ManualDecidedAt,
        ManualNote = attendee.ManualNote,
        OverruledReason = attendee.OverruledReason,
        ReinstatedAt = attendee.ReinstatedAt,
        ReinstatedById = attendee.ReinstatedById,
    };

    private static Raffle MapToRaffle(RaffleData data) => new(
        id: data.Id,
        eventId: data.EventId,
        state: data.State,
        visualStyle: data.VisualStyle == RaffleVisualStyle.BallTombola ? RaffleVisualStyle.Roulette : data.VisualStyle,
        showPrizeOnScreen: data.ShowPrizeOnScreen,
        animationSeconds: data.AnimationSeconds,
        requiresPresenceConfirmation: data.RequiresPresenceConfirmation,
        onePrizePerPerson: data.OnePrizePerPerson,
        onePrizePerCompany: data.OnePrizePerCompany,
        authorities: new RaffleAuthorities(
            data.NotaryName,
            data.NotaryIdentityDocument,
            data.WitnessOneName,
            data.WitnessOneIdentityDocument,
            data.WitnessTwoName,
            data.WitnessTwoIdentityDocument),
        createdAt: data.CreatedAt,
        updatedAt: data.UpdatedAt,
        createdById: data.CreatedById,
        updatedById: data.UpdatedById)
    {
        StartedAt = data.StartedAt,
        StartedById = data.StartedById,
        AwardsBatchId = data.AwardsBatchId,
        PickupInstructions = data.PickupInstructions,
        CertificateSigners = new CertificateSigners(
            MapToCertificateSigner(data.CertificateSignerOneName, data.CertificateSignerOneTitle),
            MapToCertificateSigner(data.CertificateSignerTwoName, data.CertificateSignerTwoTitle)),
        CompletedAt = data.CompletedAt,
        CompletedById = data.CompletedById,
        RequiresCheckIn = data.RequiresCheckIn,
        LinkedEventId = data.LinkedEventId,
        BoardTitle = data.BoardTitle,
        BoardSubtitle = data.BoardSubtitle,
        BoardIdentifier = data.BoardIdentifier,
        BoardBatchSize = data.BoardBatchSize,
        DisplayFields = RaffleDisplayFields.NormalizeOrDefault(
            data.DisplayNameMode,
            data.DisplayIdentifierMode,
            data.DisplayDetailMode),
    };

    /// <summary>
    /// Un firmante del certificado desde sus dos columnas, o ninguno. <c>ck_raffle_certificate_signer_one</c>
    /// y <c>_two</c> guardan nombre y cargo juntos, así que con el nombre está el cargo.
    /// </summary>
    private static CertificateSigner? MapToCertificateSigner(string? name, string? title) =>
        name is null ? null : new CertificateSigner(name, title!);

    private static RaffleRound MapToRound(RaffleRoundData data, IReadOnlyList<Prize> prizes) => new(
        id: data.Id,
        raffleId: data.RaffleId,
        number: data.Number,
        name: data.Name,
        description: data.Description,
        prizes: prizes,
        createdAt: data.CreatedAt,
        updatedAt: data.UpdatedAt,
        createdById: data.CreatedById,
        updatedById: data.UpdatedById);

    private static Prize MapToPrize(PrizeData data) => new(
        id: data.Id,
        roundId: data.RoundId,
        name: data.Name,
        description: data.Description,
        value: data.Value,
        createdAt: data.CreatedAt,
        updatedAt: data.UpdatedAt,
        createdById: data.CreatedById,
        updatedById: data.UpdatedById,
        sponsorCompanyId: data.SponsorCompanyId,
        sponsorContactId: data.SponsorContactId,
        sponsorLogoFileId: data.SponsorLogoFileId,
        sponsorName: data.SponsorName,
        imageStoredFileId: data.ImageStoredFileId);

    public async Task<Result<bool, BaseError>> SetPrizesImageAsync(
        Guid raffleId,
        IReadOnlyList<Guid> prizeIds,
        StoredFile? imageFile,
        Guid updatedById,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prizeIds);

        return await DbResult.TryResultAsync<bool>(_logger, "actualizar la foto de los premios", async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            var matchingIds = await (
                from p in _context.Prizes.AsNoTracking()
                join r in _context.RaffleRounds.AsNoTracking() on p.RoundId equals r.Id
                where r.RaffleId == raffleId && prizeIds.Contains(p.Id)
                select p.Id
            ).ToListAsync(cancellationToken);

            if (matchingIds.Count != prizeIds.Distinct().Count())
                return Result.Success<bool, BaseError>(false);

            if (imageFile is not null)
            {
                _context.StoredFiles.Add(StoredFileMapper.ToData(imageFile));
                await _context.SaveChangesAsync(cancellationToken);
            }

            Guid? fileId = imageFile?.Id;
            await _context.Prizes
                .Where(p => matchingIds.Contains(p.Id))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.ImageStoredFileId, fileId)
                    .SetProperty(p => p.UpdatedById, updatedById)
                    .SetProperty(p => p.UpdatedAt, updatedAt), cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result.Success<bool, BaseError>(true);
        });
    }
}

/// <summary>Lo que el padrón aporta del ganador para mostrarlo (A33, §10): se lee, no se guarda en el sorteo.</summary>
internal sealed record WinnerRosterInfo(
    string? TicketNumber,
    string? EmployeeCode,
    string? Department,
    string? Location,
    string? FirstName,
    string? LastName);
