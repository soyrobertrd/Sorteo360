using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sorteo360.Convocatorias.Core.CheckIns;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Platform.Database;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Infrastructure.Database;

/// <inheritdoc cref="IContactCheckInRepository" />
public sealed class ContactCheckInRepository : IContactCheckInRepository
{
    private readonly ConvocatoriasDbContext _context;
    private readonly ILogger<ContactCheckInRepository> _logger;

    public ContactCheckInRepository(ConvocatoriasDbContext context, ILogger<ContactCheckInRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<ContactCheckIn>, BaseError>> ListByContactsAsync(
        IReadOnlyCollection<Guid> contactIds,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "leer los check-ins de los contactos", async () =>
        {
            var ids = contactIds.Distinct().ToList();

            // Solo el contacto propio de la inscripción (spec 09 §7.1): a quien reenvió la
            // invitación no le cuenta que su colega viniera. Sin contacto no hay a quién atribuirlo,
            // que es como un asistente que no está en el CRM no cuenta para nadie.
            var rows = await _context.Registrations
                .AsNoTracking()
                .Where(r => r.CheckedInAt != null && r.CrmContactId != null && ids.Contains(r.CrmContactId.Value))
                .GroupBy(r => new { ContactId = r.CrmContactId!.Value, r.EventId })
                .Select(g => new { g.Key.ContactId, g.Key.EventId, CheckedInAt = g.Min(r => r.CheckedInAt!.Value) })
                .ToListAsync(cancellationToken);

            IReadOnlyList<ContactCheckIn> checkIns =
            [
                .. rows
                    .OrderByDescending(r => r.CheckedInAt)
                    .ThenBy(r => r.EventId)
                    .Select(r => new ContactCheckIn(r.ContactId, r.EventId, r.CheckedInAt)),
            ];

            return checkIns;
        });
}
