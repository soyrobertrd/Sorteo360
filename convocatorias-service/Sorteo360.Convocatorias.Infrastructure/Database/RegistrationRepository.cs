using System.Linq.Expressions;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sorteo360.Convocatorias.Core.DTOs;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.Registrations;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Platform.Database;
using Sorteo360.Platform.Errors;
using Npgsql;

namespace Sorteo360.Convocatorias.Infrastructure.Database;

/// <inheritdoc cref="IRegistrationRepository" />
public sealed class RegistrationRepository : IRegistrationRepository
{
    /// <summary>SQLSTATE de una violación de unicidad en PostgreSQL.</summary>
    private const string UniqueViolation = "23505";

    /// <summary>
    /// La restricción que se gana una carrera entre dos envíos de la misma dirección. Se mira
    /// por nombre y no cualquier <c>23505</c>: una colisión de <c>qr_code</c> es otra cosa
    /// entera y volver a leer por correo no la arreglaría.
    /// </summary>
    private const string SamePersonTwiceConstraint = "ux_registration_event_email";

    private readonly ConvocatoriasDbContext _context;
    private readonly ILogger<RegistrationRepository> _logger;
    private readonly TimeProvider _clock;

    public RegistrationRepository(
        ConvocatoriasDbContext context,
        ILogger<RegistrationRepository> logger,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(clock);

        _context = context;
        _logger = logger;
        _clock = clock;
    }

    /// <inheritdoc />
    public Task<Result<Maybe<Invitation>, BaseError>> FindInvitationAsync(
        byte[] tokenHash,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "resolver la invitación", async () =>
        {
            // Por hash, que es lo único que la columna guarda. El texto plano no llega hasta
            // acá y no existe en ninguna fila (spec 06 §2.1).
            var row = await _context.CampaignRecipients
                .AsNoTracking()
                .Where(recipient => recipient.RegistrationTokenHash == tokenHash)
                .Select(recipient => new
                {
                    recipient.Id,
                    recipient.Campaign!.EventId,
                    recipient.RegistrationTokenHash,
                    recipient.FirstName,
                    recipient.LastName,
                    recipient.Email,
                })
                .FirstOrDefaultAsync(cancellationToken);

            // Un mensaje directo no acuña token de registro, así que su fila no llega hasta acá; la
            // comprobación del evento es la misma regla vista desde este lado.
            return row is null || row.RegistrationTokenHash is null || row.EventId is not { } eventId
                ? Maybe<Invitation>.None
                : Maybe.From(new Invitation(
                    row.Id,
                    eventId,
                    row.RegistrationTokenHash,
                    row.FirstName,
                    row.LastName,
                    row.Email));
        });

    /// <inheritdoc />
    public Task<Result<Maybe<Registration>, BaseError>> FindByEmailAsync(
        Guid eventId,
        string emailNormalized,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "buscar la inscripción", async () =>
        {
            var row = await _context.Registrations
                .AsNoTracking()
                .FirstOrDefaultAsync(SamePerson(eventId, emailNormalized), cancellationToken);

            return row is null ? Maybe<Registration>.None : Maybe.From(ToRegistration(row));
        });

    /// <inheritdoc />
    public Task<Result<bool, BaseError>> IsInvitationSpentAsync(
        Guid campaignRecipientId,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "comprobar si la invitación ya se usó", () =>
            _context.Registrations
                .AsNoTracking()
                .AnyAsync(
                    registration => registration.CampaignRecipientId == campaignRecipientId,
                    cancellationToken));

    /// <inheritdoc />
    public Task<Result<Maybe<Registration>, BaseError>> FindByQrCodeAsync(
        string qrCode,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "buscar la entrada", async () =>
        {
            var row = await _context.Registrations
                .AsNoTracking()
                .FirstOrDefaultAsync(registration => registration.QrCode == qrCode, cancellationToken);

            return row is null ? Maybe<Registration>.None : Maybe.From(ToRegistration(row));
        });

    /// <inheritdoc />
    public Task<Result<bool, BaseError>> QrCodeExistsAsync(
        string qrCode,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "comprobar si la entrada existe", () =>
            _context.Registrations
                .AsNoTracking()
                .AnyAsync(registration => registration.QrCode == qrCode, cancellationToken));

    /// <inheritdoc />
    public Task<Result<Maybe<Registration>, BaseError>> FindByIdAsync(
        Guid registrationId,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "buscar la inscripción por su id", async () =>
        {
            var row = await _context.Registrations
                .AsNoTracking()
                .FirstOrDefaultAsync(registration => registration.Id == registrationId, cancellationToken);

            return row is null ? Maybe<Registration>.None : Maybe.From(ToRegistration(row));
        });

    /// <inheritdoc />
    public async Task<Result<Maybe<Registration>, BaseError>> RecordCrmSyncAsync(
        Guid registrationId,
        CrmSyncOutcome crmSync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(crmSync);

        var recorded = await DbResult.TryAsync(_logger, "anotar la sincronización con el CRM", () =>
            // La condición viaja dentro del UPDATE: dos pulsaciones a la vez no se pisan, y la que
            // pierde lee la fila del que ganó. `crm_sync_failed_at` no se toca.
            _context.Registrations
                .Where(registration => registration.Id == registrationId
                    && registration.CrmContactId == null)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(registration => registration.CrmContactId, crmSync.ContactId)
                        .SetProperty(registration => registration.ContactDetailsDiffer, crmSync.ContactDetailsDiffer)
                        .SetProperty(registration => registration.UpdatedAt, _clock.GetUtcNow()),
                    cancellationToken));

        return recorded.IsFailure
            ? Result.Failure<Maybe<Registration>, BaseError>(recorded.Error)
            : await FindByIdAsync(registrationId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<Registration>, BaseError>> ListAwaitingCancellationNoticeAsync(
        Guid eventId,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "leer las inscripciones sin aviso de cancelación", async () =>
        {
            var rows = await _context.Registrations
                .AsNoTracking()
                .Where(registration => registration.EventId == eventId
                    && registration.CancellationNotifiedAt == null)
                .OrderBy(registration => registration.CreatedAt)
                .ToListAsync(cancellationToken);

            return (IReadOnlyList<Registration>)rows.Select(ToRegistration).ToList();
        });

    /// <inheritdoc />
    public Task<Result<bool, BaseError>> ClaimCancellationNoticeAsync(
        Guid registrationId,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "reclamar el aviso de cancelación", async () =>
        {
            // La condición viaja dentro del UPDATE y no se lee antes: dos pasadas que leyeran
            // "sin sello" a la vez sellarían las dos y las dos mandarían el correo.
            var now = _clock.GetUtcNow();

            var claimed = await _context.Registrations
                .Where(registration => registration.Id == registrationId
                    && registration.CancellationNotifiedAt == null)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(registration => registration.CancellationNotifiedAt, now)
                        .SetProperty(registration => registration.UpdatedAt, now),
                    cancellationToken);

            return claimed == 1;
        });

    /// <inheritdoc />
    public async Task<UnitResult<BaseError>> ReleaseCancellationNoticeAsync(
        Guid registrationId,
        CancellationToken cancellationToken)
    {
        var released = await DbResult.TryAsync(_logger, "liberar el aviso de cancelación", () =>
            _context.Registrations
                .Where(registration => registration.Id == registrationId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(registration => registration.CancellationNotifiedAt, (DateTimeOffset?)null)
                        .SetProperty(registration => registration.UpdatedAt, _clock.GetUtcNow()),
                    cancellationToken));

        return released.IsFailure ? UnitResult.Failure(released.Error) : UnitResult.Success<BaseError>();
    }

    /// <inheritdoc />
    public Task<Result<CreatedRegistration, BaseError>> CreateAsync(
        RegisterDto dto,
        Guid? campaignRecipientId,
        CrmSyncOutcome crmSync,
        CancellationToken cancellationToken)
    {
        // Fuera del `TryAsync`: un argumento nulo es un fallo de programación de quien llama, y
        // dentro del lambda saldría disfrazado de 500 `UNEXPECTED_ERROR` en vez de reventar donde
        // está el error.
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(crmSync);

        return DbResult.TryAsync(_logger, "crear la inscripción", async () =>
        {
            var emailNormalized = EmailAddressShape.Normalize(dto.Email);
            var now = _clock.GetUtcNow();

            // La entidad primero y la fila después, no al revés: sus guardas son las que declaran
            // qué es una inscripción válida, y construyendo la fila directamente no correrían
            // nunca en el camino de escritura — solo al leer de vuelta, que es tarde.
            var registration = new Registration(
                Guid.CreateVersion7(),
                dto.EventId,
                campaignRecipientId,
                dto.FirstName.Trim(),
                dto.LastName.Trim(),
                dto.Email.Trim(),
                emailNormalized,
                OptionalText.Trimmed(dto.Phone),
                OptionalText.Trimmed(dto.Company),
                OptionalText.Trimmed(dto.JobTitle),
                OptionalText.Trimmed(dto.Country),
                RegistrationCodes.Reference(now.Year, await NextReferenceAsync(cancellationToken)),
                RegistrationCodes.MintQrCode(),
                crmSync.ContactDetailsDiffer,
                crmSync.ContactId,
                crmSync.FailedAt,
                now,
                now);

            var data = ToData(registration);

            _context.Registrations.Add(data);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsSamePersonTwice(ex))
            {
                // Otro envío de la misma dirección ganó la carrera. No es un fallo: es la misma
                // persona dos veces, que §4.1 contesta con la inscripción que ya hay. La
                // comprobación previa no puede cubrir esto —los dos envíos la pasan— y por eso
                // la regla vive en la base.
                _context.Entry(data).State = EntityState.Detached;

                var winner = await _context.Registrations
                    .AsNoTracking()
                    .FirstAsync(SamePerson(dto.EventId, emailNormalized), cancellationToken);

                return new CreatedRegistration(ToRegistration(winner), IsNew: false);
            }

            return new CreatedRegistration(registration, IsNew: true);
        });
    }

    /// <summary>
    /// El siguiente número de <c>reference</c>. Sale de una secuencia de PostgreSQL y no de un
    /// <c>COUNT</c> ni de una fila contador: una secuencia nunca entrega el mismo número dos
    /// veces por concurrida que esté y nunca pone a nadie a esperar.
    /// </summary>
    private async Task<long> NextReferenceAsync(CancellationToken cancellationToken)
    {
        var sequence = $"{ConvocatoriasDbContext.SchemaName}.{RegistrationCodes.SequenceName}";

        // El nombre viaja como parámetro y se convierte a `regclass` en el servidor, en vez de
        // interpolarse en el SQL. Hoy es una constante del propio código y daría igual; escrito
        // así no hay una consulta cruda en el servicio que alguien pueda tomar de ejemplo.
        var values = await _context.Database
            .SqlQuery<long>($"SELECT nextval({sequence}::regclass) AS \"Value\"")
            .ToListAsync(cancellationToken);

        return values.Single();
    }

    /// <summary>
    /// La misma persona en el mismo evento: la regla de unicidad de §4.1, escrita una vez. La
    /// leen la comprobación previa y la lectura de vuelta cuando la restricción salta, y tienen
    /// que preguntar exactamente lo mismo — dos copias que se separaran harían que la segunda no
    /// encontrara la fila que la primera dejó pasar, y eso es un <c>FirstAsync</c> que revienta.
    /// </summary>
    private static Expression<Func<RegistrationData, bool>> SamePerson(Guid eventId, string emailNormalized) =>
        registration => registration.EventId == eventId
            && registration.EmailNormalized == emailNormalized;

    /// <summary>Si el fallo es la carrera de §4.1 y no cualquier otra colisión.</summary>
    private static bool IsSamePersonTwice(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: UniqueViolation,
            ConstraintName: SamePersonTwiceConstraint
        };

    /// <summary>
    /// La entidad como fila. Escribe cada columna a mano y no por reflexión: una columna nueva
    /// en <see cref="RegistrationData"/> que nadie mapee acá se guardaría nula sin que nada se
    /// queje, y las de PGTI-57 en adelante van a llegar de una en una.
    /// </summary>
    private static RegistrationData ToData(Registration registration) => new()
    {
        Id = registration.Id,
        EventId = registration.EventId,
        CampaignRecipientId = registration.CampaignRecipientId,
        FirstName = registration.FirstName,
        LastName = registration.LastName,
        Email = registration.Email,
        EmailNormalized = registration.EmailNormalized,
        Phone = registration.Phone,
        Company = registration.Company,
        JobTitle = registration.JobTitle,
        Country = registration.Country,
        Reference = registration.Reference,
        QrCode = registration.QrCode,
        ContactDetailsDiffer = registration.ContactDetailsDiffer,
        CrmContactId = registration.CrmContactId,
        CrmSyncFailedAt = registration.CrmSyncFailedAt,
        CancellationNotifiedAt = registration.CancellationNotifiedAt,
        CreatedAt = registration.CreatedAt,
        UpdatedAt = registration.UpdatedAt,
    };

    /// <summary>
    /// La fila como entidad, pasando por el constructor y por tanto por sus guardas. Una fila
    /// que no las pase revienta al leerla, que es donde se quiere enterar uno, y no más tarde
    /// donde el dato ya se usó.
    /// </summary>
    private static Registration ToRegistration(RegistrationData data) => new(
        data.Id,
        data.EventId,
        data.CampaignRecipientId,
        data.FirstName,
        data.LastName,
        data.Email,
        data.EmailNormalized,
        data.Phone,
        data.Company,
        data.JobTitle,
        data.Country,
        data.Reference,
        data.QrCode,
        data.ContactDetailsDiffer,
        data.CrmContactId,
        data.CrmSyncFailedAt,
        data.CreatedAt,
        data.UpdatedAt)
    {
        CancellationNotifiedAt = data.CancellationNotifiedAt,
    };
}
