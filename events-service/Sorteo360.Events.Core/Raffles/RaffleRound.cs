namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Una ronda del sorteo: un grupo con nombre de <see cref="Prize"/> que se sortean juntos. Su
/// número es el orden en que corre la noche, siempre de uno a tantas rondas como haya y sin huecos,
/// y lo asigna el servicio, nunca el operador: al crearla toma el siguiente, y al eliminar una las
/// de detrás bajan un puesto. No hay reordenamiento.
/// <para>
/// Una ronda con premios no se elimina: se vacía primero, moviendo o eliminando sus premios.
/// </para>
/// </summary>
public sealed record RaffleRound
{
    public Guid Id { get; init; }
    public Guid RaffleId { get; init; }
    public int Number { get; init; }
    public string Name { get; init; }
    public string? Description { get; init; }

    /// <summary>Los premios de la ronda, en el orden en que se crearon.</summary>
    public IReadOnlyList<Prize> Prizes { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public Guid CreatedById { get; init; }
    public Guid UpdatedById { get; init; }

    public RaffleRound(
        Guid id,
        Guid raffleId,
        int number,
        string name,
        string? description,
        IReadOnlyList<Prize> prizes,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        Guid createdById,
        Guid updatedById)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(prizes);
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);

        Id = id;
        RaffleId = raffleId;
        Number = number;
        Name = name;
        Description = description;
        Prizes = prizes;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        CreatedById = createdById;
        UpdatedById = updatedById;
    }
}
