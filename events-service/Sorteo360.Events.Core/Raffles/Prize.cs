namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Un premio: se entrega entero a un solo ganador. "3 botellas de ron" es un premio; cinco bolsas
/// son cinco premios con cinco ganadores y el mismo nombre. Cuántas cosas físicas contiene es prosa
/// en su nombre y nada la lee, así que no hay cantidad: la cantidad del alta solo dice cuántas filas
/// idénticas crear.
/// <para>
/// El valor es texto impreso tal como lo escribió el operador (<c>RD$8,000</c>), no un monto: nada
/// calcula sobre él y el acta imprime lo que se escribió.
/// </para>
/// <para>
/// El patrocinador es una empresa del CRM y, si se quiere, uno de sus contactos, guardados por
/// identificador y nunca por nombre (ADR-0013): el nombre se resuelve al leer. Un contacto sin
/// empresa no existe (spec 00 §8).
/// </para>
/// <para>
/// El logo del patrocinador es del premio, uno por premio, y se referencia por el archivo guardado
/// (<see cref="SponsorLogoFileId"/>). Sin empresa no hay logo: cambiarla o quitarla lo desenlaza.
/// </para>
/// </summary>
public sealed record Prize
{
    public Guid Id { get; init; }
    public Guid RoundId { get; init; }
    public string Name { get; init; }
    public string? Description { get; init; }
    public string? Value { get; init; }
    public Guid? SponsorCompanyId { get; init; }
    public Guid? SponsorContactId { get; init; }
    public string? SponsorName { get; init; }
    public Guid? SponsorLogoFileId { get; init; }
    public Guid? ImageStoredFileId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public Guid CreatedById { get; init; }
    public Guid UpdatedById { get; init; }

    public Prize(
        Guid id,
        Guid roundId,
        string name,
        string? description,
        string? value,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        Guid createdById,
        Guid updatedById,
        Guid? sponsorCompanyId = null,
        Guid? sponsorContactId = null,
        Guid? sponsorLogoFileId = null,
        string? sponsorName = null,
        Guid? imageStoredFileId = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (sponsorContactId is not null && sponsorCompanyId is null)
            throw new ArgumentException("Un contacto patrocinador va siempre con su empresa.", nameof(sponsorContactId));
        var normalizedSponsorName = string.IsNullOrWhiteSpace(sponsorName) ? null : sponsorName.Trim();
        if (sponsorLogoFileId is not null && sponsorCompanyId is null && normalizedSponsorName is null)
            throw new ArgumentException("El logo del patrocinador va siempre con su empresa o nombre.", nameof(sponsorLogoFileId));

        Id = id;
        RoundId = roundId;
        Name = name;
        Description = description;
        Value = value;
        SponsorCompanyId = sponsorCompanyId;
        SponsorContactId = sponsorContactId;
        SponsorName = normalizedSponsorName;
        SponsorLogoFileId = sponsorLogoFileId;
        ImageStoredFileId = imageStoredFileId;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        CreatedById = createdById;
        UpdatedById = updatedById;
    }
}
