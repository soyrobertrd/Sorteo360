using Sorteo360.Events.Core.Raffles;

namespace Sorteo360.Events.Core.DTOs;

/// <summary>
/// Datos de entrada de un participante enviado al padrón del sorteo desde archivo, API, webhook o captura manual.
/// DTO de entrada: usa constructor posicional acorde con ADR-0001.
/// </summary>
public sealed record RaffleParticipantInput(
    string FullName,
    string? Email = null,
    string? Company = null,
    string? Department = null,
    string? EmployeeCode = null,
    string? Phone = null,
    string? QrCode = null,
    bool? CheckedIn = null,
    string? FirstName = null,
    string? LastName = null,
    string? Location = null);

/// <summary>Resultado consolidado de una importación o sincronización de participantes del sorteo.</summary>
public sealed record RaffleParticipantImportResult(
    int Imported,
    int Updated,
    int Total,
    IReadOnlyList<RaffleParticipant> Participants);

