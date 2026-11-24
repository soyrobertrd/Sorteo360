namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Genera imágenes QR individuales en PNG y planillas de gafetes en PDF para los participantes del sorteo.
/// </summary>
public interface IRaffleBadgeRenderer
{
    /// <summary>Renderiza el código QR de un participante en bytes PNG.</summary>
    byte[] RenderQrPng(string payload);

    /// <summary>Genera un documento PDF con gafetes imprimibles y QR para todos los participantes.</summary>
    byte[] RenderBadgesPdf(string eventName, IReadOnlyList<RaffleParticipant> participants);
}

