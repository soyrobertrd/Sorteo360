using System.Security.Cryptography;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sorteo360.Events.Core.Database;
using Sorteo360.Events.Core.DTOs;
using Sorteo360.Events.Core.Errors;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Platform.Database;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Infrastructure.Database;

/// <summary>
/// Repositorio EF Core para el padrón persistido de participantes de un sorteo.
/// </summary>
public sealed class RaffleParticipantRepository : IRaffleParticipantRepository
{
    private readonly EventsDbContext _context;
    private readonly ILogger<RaffleParticipantRepository> _logger;

    public RaffleParticipantRepository(EventsDbContext context, ILogger<RaffleParticipantRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        _context = context;
        _logger = logger;
    }

    public Task<Result<Maybe<EventWebhookAccess>, BaseError>> ResolveEventWebhookAsync(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        return DbResult.TryAsync(_logger, "resolver acceso de webhook del evento", async () =>
        {
            var row = await _context.Events
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(e => e.Id == eventId && e.DeletedAt == null)
                .Select(e => new { e.Id, e.AccountId, e.WebhookSecret })
                .FirstOrDefaultAsync(cancellationToken);

            return row is null
                ? Maybe<EventWebhookAccess>.None
                : Maybe.From(new EventWebhookAccess(row.Id, row.AccountId, row.WebhookSecret));
        });
    }

    public Task<Result<IReadOnlyList<RaffleParticipant>, BaseError>> ListByEventIdAsync(
        Guid eventId,
        CancellationToken cancellationToken,
        bool bypassAccountFilter = false)
    {
        return DbResult.TryResultAsync<IReadOnlyList<RaffleParticipant>>(_logger, "listar los participantes del sorteo", async () =>
        {
            if (!await EventExistsAsync(eventId, bypassAccountFilter, cancellationToken))
                return Result.Failure<IReadOnlyList<RaffleParticipant>, BaseError>(EventImageErrors.EventNotFound());

            var rows = await _context.RaffleParticipants
                .AsNoTracking()
                .Where(p => p.EventId == eventId)
                .OrderBy(p => p.FullName)
                .ThenBy(p => p.IdentityKey)
                .ToListAsync(cancellationToken);

            return Result.Success<IReadOnlyList<RaffleParticipant>, BaseError>(rows.Select(MapToDomain).ToList());
        });
    }

    public Task<Result<RaffleParticipantImportResult, BaseError>> ImportAsync(
        Guid eventId,
        IReadOnlyList<RaffleParticipantInput> participants,
        bool replaceExisting,
        bool markCheckedIn,
        string source,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        bool bypassAccountFilter = false)
    {
        ArgumentNullException.ThrowIfNull(participants);

        return DbResult.TryResultAsync(_logger, "importar participantes del sorteo", async () =>
        {
            if (!await EventExistsAsync(eventId, bypassAccountFilter, cancellationToken))
                return Result.Failure<RaffleParticipantImportResult, BaseError>(EventImageErrors.EventNotFound());

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            List<RaffleParticipantData> existing;
            if (replaceExisting)
            {
                await _context.RaffleParticipants
                    .Where(p => p.EventId == eventId)
                    .ExecuteDeleteAsync(cancellationToken);
                existing = [];
            }
            else
            {
                existing = await _context.RaffleParticipants
                    .Where(p => p.EventId == eventId)
                    .ToListAsync(cancellationToken);
            }

            var byEmployeeCode = new Dictionary<string, RaffleParticipantData>(StringComparer.OrdinalIgnoreCase);
            var byEmail = new Dictionary<string, RaffleParticipantData>(StringComparer.Ordinal);
            var usedQrCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var usedTicketNumbers = new HashSet<string>(StringComparer.Ordinal);

            foreach (var row in existing)
            {
                if (row.EmailNormalized is not null)
                    byEmail[row.EmailNormalized] = row;
                usedQrCodes.Add(row.QrCode);
                if (!string.IsNullOrWhiteSpace(row.TicketNumber))
                    usedTicketNumbers.Add(row.TicketNumber);
                if (!string.IsNullOrWhiteSpace(row.EmployeeCode))
                    byEmployeeCode[row.EmployeeCode.Trim()] = row;
            }

            var normalizedSource = string.IsNullOrWhiteSpace(source) ? "manual" : source.Trim().ToLowerInvariant();
            var imported = 0;
            var updated = 0;
            var rejected = new List<RaffleParticipantImportRejection>();
            var rowNumber = 0;

            // A31: desactivar DetectChanges por fila para evitar O(n²) al importar 25.000 participantes.
            var previousAutoDetect = _context.ChangeTracker.AutoDetectChangesEnabled;
            _context.ChangeTracker.AutoDetectChangesEnabled = false;
            try
            {
                foreach (var raw in participants)
            {
                rowNumber++;
                var firstName = NormalizeNullable(raw.FirstName);
                var lastName = NormalizeNullable(raw.LastName);
                var fullName = NormalizeNullable(raw.FullName)
                    ?? NormalizeNullable($"{firstName} {lastName}");
                if (fullName is null)
                    continue;

                var employeeCode = NormalizeNullable(raw.EmployeeCode);
                var email = NormalizeNullable(raw.Email)?.ToLowerInvariant();
                var company = NormalizeNullable(raw.Company);
                var department = NormalizeNullable(raw.Department);
                var location = NormalizeNullable(raw.Location);
                var phone = NormalizeNullable(raw.Phone);
                var requestedQr = NormalizeNullable(raw.QrCode);

                // Sin correo ni ID no hay cómo reconocer a la persona: la fila se informa y el lote sigue.
                if (email is null && employeeCode is null)
                {
                    rejected.Add(new RaffleParticipantImportRejection(rowNumber, RaffleParticipantImportRejection.MissingEmailAndId));
                    continue;
                }

                RaffleParticipantData? match = null;
                if (employeeCode is not null)
                    byEmployeeCode.TryGetValue(employeeCode, out match);
                // El correo solo une si no contradice el código: dos empleados con código distinto y
                // el mismo correo son dos personas, no una fusión que borra a la primera.
                if (match is null && email is not null
                    && byEmail.TryGetValue(email, out var byMail)
                    && (employeeCode is null || string.IsNullOrWhiteSpace(byMail.EmployeeCode)))
                    match = byMail;

                if (match is not null)
                {
                    match.FullName = fullName;
                    if (firstName is not null)
                        match.FirstName = firstName;
                    if (lastName is not null)
                        match.LastName = lastName;
                    if (company is not null)
                        match.Company = company;
                    if (department is not null)
                        match.Department = department;
                    if (location is not null)
                        match.Location = location;
                    if (phone is not null)
                        match.Phone = phone;

                    if (employeeCode is not null && !string.Equals(match.EmployeeCode, employeeCode, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrWhiteSpace(match.EmployeeCode))
                            byEmployeeCode.Remove(match.EmployeeCode.Trim());
                        match.EmployeeCode = employeeCode;
                        byEmployeeCode[employeeCode] = match;
                    }

                    if (email is not null && !string.Equals(match.EmailNormalized, email, StringComparison.Ordinal) && !byEmail.ContainsKey(email))
                    {
                        if (match.EmailNormalized is not null)
                            byEmail.Remove(match.EmailNormalized);
                        match.EmailNormalized = email;
                        byEmail[email] = match;
                    }

                    // La clave sigue al correo y al ID; una fila vieja sin ninguno conserva la suya.
                    match.IdentityKey = PersonIdentity.KeyFor(match.EmailNormalized, match.EmployeeCode) ?? match.IdentityKey;

                    if (requestedQr is not null
                        && !string.Equals(match.QrCode, requestedQr, StringComparison.OrdinalIgnoreCase)
                        && !usedQrCodes.Contains(requestedQr))
                    {
                        usedQrCodes.Remove(match.QrCode);
                        match.QrCode = requestedQr;
                        usedQrCodes.Add(requestedQr);
                    }

                    if (raw.CheckedIn.HasValue || markCheckedIn)
                    {
                        var nextCheckedIn = raw.CheckedIn ?? markCheckedIn;
                        match.CheckedIn = nextCheckedIn;
                        match.CheckedInAt = nextCheckedIn ? (match.CheckedInAt ?? now) : null;
                    }

                    if (string.IsNullOrWhiteSpace(match.TicketNumber))
                    {
                        match.TicketNumber = TicketNumberGenerator.NextUnique(usedTicketNumbers);
                        usedTicketNumbers.Add(match.TicketNumber);
                    }

                    match.Source = normalizedSource;
                    match.UpdatedAt = now;
                    updated++;
                    continue;
                }

                // Un correo compartido (buzón de área, relleno como na@na.com) no puede tumbar la carga
                // por el índice único: el ID manda y el segundo entra sin ese correo.
                var finalEmail = email is not null && !byEmail.ContainsKey(email) ? email : null;
                var identityKey = PersonIdentity.KeyFor(finalEmail, employeeCode)!;
                var finalQrCode = ResolveUniqueQrCode(requestedQr, usedQrCodes);
                var ticketNumber = TicketNumberGenerator.NextUnique(usedTicketNumbers);
                var checkedIn = raw.CheckedIn ?? markCheckedIn;

                var created = new RaffleParticipantData
                {
                    Id = Guid.NewGuid(),
                    EventId = eventId,
                    FullName = fullName,
                    FirstName = firstName,
                    LastName = lastName,
                    EmailNormalized = finalEmail,
                    IdentityKey = identityKey,
                    Company = company,
                    Department = department,
                    Location = location,
                    EmployeeCode = employeeCode,
                    Phone = phone,
                    QrCode = finalQrCode,
                    TicketNumber = ticketNumber,
                    CheckedIn = checkedIn,
                    CheckedInAt = checkedIn ? now : null,
                    Source = normalizedSource,
                    CreatedAt = now,
                    UpdatedAt = now,
                };

                _context.RaffleParticipants.Add(created);
                existing.Add(created);
                if (created.EmailNormalized is not null)
                    byEmail[created.EmailNormalized] = created;
                usedQrCodes.Add(created.QrCode);
                usedTicketNumbers.Add(created.TicketNumber);
                if (created.EmployeeCode is not null)
                    byEmployeeCode[created.EmployeeCode] = created;

                imported++;
                }

                _context.ChangeTracker.DetectChanges();
                await _context.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                _context.ChangeTracker.AutoDetectChangesEnabled = previousAutoDetect;
            }

            await transaction.CommitAsync(cancellationToken);

            var domainList = existing
                .OrderBy(p => p.FullName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.IdentityKey, StringComparer.Ordinal)
                .Select(MapToDomain)
                .ToList();

            return Result.Success<RaffleParticipantImportResult, BaseError>(
                new RaffleParticipantImportResult(imported, updated, domainList.Count, domainList, rejected));
        });
    }

    public Task<Result<Maybe<RaffleParticipant>, BaseError>> SetCheckInAsync(
        Guid eventId,
        string lookupCode,
        bool checkedIn,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        bool bypassAccountFilter = false)
    {
        ArgumentNullException.ThrowIfNull(lookupCode);

        return DbResult.TryResultAsync<Maybe<RaffleParticipant>>(_logger, "registrar el check-in del participante", async () =>
        {
            if (!await EventExistsAsync(eventId, bypassAccountFilter, cancellationToken))
                return Result.Failure<Maybe<RaffleParticipant>, BaseError>(EventImageErrors.EventNotFound());

            var normalized = lookupCode.Trim();
            if (normalized.Length == 0)
                return Result.Success<Maybe<RaffleParticipant>, BaseError>(Maybe<RaffleParticipant>.None);

            var target = await FindByLookupAsync(eventId, normalized, cancellationToken);
            if (target is null)
                return Result.Success<Maybe<RaffleParticipant>, BaseError>(Maybe<RaffleParticipant>.None);

            target.CheckedIn = checkedIn;
            target.CheckedInAt = checkedIn ? (target.CheckedInAt ?? now) : null;
            target.UpdatedAt = now;

            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success<Maybe<RaffleParticipant>, BaseError>(Maybe.From(MapToDomain(target)));
        });
    }

    public async Task<UnitResult<BaseError>> BulkSetCheckInAsync(
        Guid eventId,
        bool checkedIn,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var result = await DbResult.TryResultAsync<bool>(_logger, "actualizar check-in masivo del sorteo", async () =>
        {
            if (!await EventExistsAsync(eventId, bypassAccountFilter: false, cancellationToken))
                return Result.Failure<bool, BaseError>(EventImageErrors.EventNotFound());

            // Un solo UPDATE en la base: no trae los 25.000 participantes a memoria.
            DateTimeOffset? checkedInAt = checkedIn ? now : null;
            await _context.RaffleParticipants
                .Where(p => p.EventId == eventId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.CheckedIn, checkedIn)
                    .SetProperty(p => p.CheckedInAt, p => checkedIn ? (p.CheckedInAt ?? checkedInAt) : null)
                    .SetProperty(p => p.UpdatedAt, now),
                    cancellationToken);

            return Result.Success<bool, BaseError>(true);
        });

        return result.IsSuccess ? UnitResult.Success<BaseError>() : UnitResult.Failure(result.Error);
    }

    public async Task<UnitResult<BaseError>> RemoveAsync(
        Guid eventId,
        string emailOrCode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(emailOrCode);

        var result = await DbResult.TryResultAsync<bool>(_logger, "quitar participante del sorteo", async () =>
        {
            if (!await EventExistsAsync(eventId, bypassAccountFilter: false, cancellationToken))
                return Result.Failure<bool, BaseError>(EventImageErrors.EventNotFound());

            var normalized = emailOrCode.Trim();
            if (normalized.Length == 0)
                return Result.Success<bool, BaseError>(true);

            var target = await FindByLookupAsync(eventId, normalized, cancellationToken);
            if (target is not null)
            {
                _context.RaffleParticipants.Remove(target);
                await _context.SaveChangesAsync(cancellationToken);
            }

            return Result.Success<bool, BaseError>(true);
        });

        return result.IsSuccess ? UnitResult.Success<BaseError>() : UnitResult.Failure(result.Error);
    }

    private Task<bool> EventExistsAsync(Guid eventId, bool bypassAccountFilter, CancellationToken cancellationToken) =>
        bypassAccountFilter
            ? _context.Events.IgnoreQueryFilters().AsNoTracking().AnyAsync(e => e.Id == eventId && e.DeletedAt == null, cancellationToken)
            : _context.Events.AsNoTracking().AnyAsync(e => e.Id == eventId, cancellationToken);

    /// <summary>Busca por identificador, código QR, código de empleado o correo normalizado.</summary>
    private async Task<RaffleParticipantData?> FindByLookupAsync(
        Guid eventId,
        string normalizedLookup,
        CancellationToken cancellationToken)
    {
        var lower = normalizedLookup.ToLowerInvariant();
        var upper = normalizedLookup.ToUpperInvariant();
        var hasGuid = Guid.TryParse(normalizedLookup, out var participantId);

        var candidates = await _context.RaffleParticipants
            .Where(p => p.EventId == eventId && (
                (hasGuid && p.Id == participantId)
                || p.EmailNormalized == lower
                || p.QrCode == normalizedLookup
                || p.TicketNumber == normalizedLookup
                || p.EmployeeCode == normalizedLookup))
            .ToListAsync(cancellationToken);

        if (candidates.Count > 0)
            return candidates[0];

        // Sin distinguir mayúsculas, resuelto en SQL (upper/trim): antes cada QR desconocido en la
        // puerta traía el padrón entero a memoria.
        return await _context.RaffleParticipants
            .Where(p => p.EventId == eventId && (
                p.QrCode.ToUpper() == upper
                || p.TicketNumber.ToUpper() == upper
                || (p.EmployeeCode != null && p.EmployeeCode.Trim().ToUpper() == upper)))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string ResolveUniqueQrCode(string? requestedQr, HashSet<string> usedQrCodes)
    {
        if (requestedQr is not null && !usedQrCodes.Contains(requestedQr))
            return requestedQr;

        string generated;
        do
        {
            generated = $"QR-{Convert.ToHexString(RandomNumberGenerator.GetBytes(8))}";
        }
        while (usedQrCodes.Contains(generated));

        return generated;
    }

    private static string? NormalizeNullable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return value.Trim();
    }

    private static RaffleParticipant MapToDomain(RaffleParticipantData data) => new(
        id: data.Id,
        eventId: data.EventId,
        fullName: data.FullName,
        email: data.EmailNormalized,
        company: data.Company,
        department: data.Department,
        employeeCode: data.EmployeeCode,
        phone: data.Phone,
        qrCode: data.QrCode,
        checkedIn: data.CheckedIn,
        checkedInAt: data.CheckedInAt,
        source: data.Source,
        createdAt: data.CreatedAt,
        updatedAt: data.UpdatedAt,
        ticketNumber: data.TicketNumber,
        firstName: data.FirstName,
        lastName: data.LastName,
        location: data.Location,
        identityKey: data.IdentityKey);
}

