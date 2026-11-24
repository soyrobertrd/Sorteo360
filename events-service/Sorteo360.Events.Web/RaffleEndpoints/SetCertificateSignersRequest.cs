using Sorteo360.Events.Core.Raffles;

namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>
/// Los firmantes del certificado de premio, enteros (ADR-0009): la lista reemplaza a la guardada, en
/// orden, y una lista vacía los borra. A lo sumo dos, uno por línea de firma.
/// </summary>
public record SetCertificateSignersRequest
{
    public IReadOnlyList<CertificateSignerRequest>? Signers { get; init; }

    /// <summary>Los firmantes recortados, en orden. Solo después de validar: ninguno llega a medias.</summary>
    public CertificateSigners ToCertificateSigners() => CertificateSigners.FromList(Signers!
        .Select(s => new CertificateSigner(s.Name!.Trim(), s.Title!.Trim()))
        .ToList());
}

/// <summary>Un firmante: el nombre y el cargo que se imprimen bajo su línea, de hasta 100 caracteres.</summary>
public record CertificateSignerRequest
{
    public string? Name { get; init; }
    public string? Title { get; init; }
}
