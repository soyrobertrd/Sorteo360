using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.DTOs;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Database;

public sealed record EventWebhookAccess(Guid EventId, Guid AccountId, string? WebhookSecret);

/// <summary>
/// Contrato de persistencia del padrón de participantes de un sorteo y su estado de check-in QR.
/// </summary>
public interface IRaffleParticipantRepository
{
    /// <summary>
    /// Resuelve la cuenta dueña y la clave de webhook propia del evento (sin filtro de usuario,
    /// para endpoints anónimos de webhook, R5).
    /// </summary>
    Task<Result<Maybe<EventWebhookAccess>, BaseError>> ResolveEventWebhookAsync(
        Guid eventId,
        CancellationToken cancellationToken);

    /// <summary>Obtiene todos los participantes registrados en el padrón del evento ordenados por nombre.</summary>
    Task<Result<IReadOnlyList<RaffleParticipant>, BaseError>> ListByEventIdAsync(
        Guid eventId,
        CancellationToken cancellationToken,
        bool bypassAccountFilter = false);

    /// <summary>
    /// Inserta o actualiza participantes en el padrón usando <c>employeeCode</c> como identidad principal
    /// y <c>email</c> como alternativa cuando no viene código de empleado.
    /// </summary>
    Task<Result<RaffleParticipantImportResult, BaseError>> ImportAsync(
        Guid eventId,
        IReadOnlyList<RaffleParticipantInput> participants,
        bool replaceExisting,
        bool markCheckedIn,
        string source,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        bool bypassAccountFilter = false);

    /// <summary>
    /// Marca o revierte el check-in de un participante identificado por ID, token QR, correo o código de empleado.
    /// </summary>
    Task<Result<Maybe<RaffleParticipant>, BaseError>> SetCheckInAsync(
        Guid eventId,
        string lookupCode,
        bool checkedIn,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        bool bypassAccountFilter = false);

    /// <summary>Aplica el mismo estado de check-in a todos los participantes del padrón del evento.</summary>
    Task<UnitResult<BaseError>> BulkSetCheckInAsync(
        Guid eventId,
        bool checkedIn,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>Elimina un participante del padrón del evento por correo o código de empleado.</summary>
    Task<UnitResult<BaseError>> RemoveAsync(
        Guid eventId,
        string emailOrCode,
        CancellationToken cancellationToken);
}

