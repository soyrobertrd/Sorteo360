namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Un firmante del certificado de premio: el nombre y el cargo que se imprimen bajo su línea de firma.
/// Es texto libre, no una referencia al CRM, porque el cargo cambia con quien ocupe la oficina.
/// </summary>
public sealed record CertificateSigner(string Name, string Title);

/// <summary>
/// Los firmantes que el certificado de premio propone por omisión, uno por cada una de sus dos líneas.
/// Son un punto de partida: el certificado se edita antes de descargarlo y lo que se imprime no se
/// guarda. Son dos y no una lista abierta porque el certificado tiene dos líneas, ni una más. El
/// segundo nunca está sin el primero.
/// </summary>
public sealed record CertificateSigners
{
    public static CertificateSigners None { get; } = new(null, null);

    public CertificateSigner? One { get; }
    public CertificateSigner? Two { get; }

    public CertificateSigners(CertificateSigner? one, CertificateSigner? two)
    {
        if (one is null && two is not null)
            throw new ArgumentException("El segundo firmante no puede estar sin el primero.", nameof(two));

        One = one;
        Two = two;
    }

    /// <summary>Los firmantes en el orden de sus líneas. Quien llama ya validó que son a lo sumo dos.</summary>
    public static CertificateSigners FromList(IReadOnlyList<CertificateSigner> signers)
    {
        ArgumentNullException.ThrowIfNull(signers);
        if (signers.Count > 2)
            throw new ArgumentException("El certificado tiene dos líneas de firma.", nameof(signers));

        return new CertificateSigners(signers.ElementAtOrDefault(0), signers.ElementAtOrDefault(1));
    }

    /// <summary>Los firmantes guardados, en el orden de sus líneas.</summary>
    public IReadOnlyList<CertificateSigner> InOrder => new[] { One, Two }.OfType<CertificateSigner>().ToList();
}
