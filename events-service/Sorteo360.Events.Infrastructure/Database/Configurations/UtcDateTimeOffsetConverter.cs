using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Sorteo360.Events.Infrastructure.Database.Configurations;

/// <summary>
/// Lleva a UTC lo que va a una columna <c>timestamptz</c>. Npgsql solo acepta un
/// <see cref="DateTimeOffset"/> con offset cero, y una hora con el offset de su zona es el mismo
/// instante: normalizarla es exacto, rechazarla sería arbitrario. Al leer no hay nada que hacer,
/// porque la base ya devuelve UTC.
/// <para>
/// Solo hace falta donde la fecha la escribe el cliente. Las marcas de auditoría nacen de
/// <c>DateTimeOffset.UtcNow</c> y ya llegan en cero.
/// </para>
/// </summary>
internal sealed class UtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset, DateTimeOffset>
{
    public UtcDateTimeOffsetConverter()
        : base(value => value.ToUniversalTime(), value => value)
    {
    }
}
