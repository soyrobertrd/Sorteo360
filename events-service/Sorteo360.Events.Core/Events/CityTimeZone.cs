using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.Errors;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Events;

/// <summary>La zona horaria de una ciudad, como id IANA (<c>America/Chicago</c>).</summary>
/// <remarks>
/// Es la hora en la que el acta notarial escribe cada momento del sorteo, porque el notario la
/// compara con su reloj. Solo se acepta un id que el sistema resuelva y que sea IANA: .NET también
/// resuelve los ids de Windows (<c>Eastern Standard Time</c>), pero aceptarlos dejaría en la base
/// dos maneras de escribir la misma zona, y el contrato promete una sola.
/// </remarks>
public sealed record CityTimeZone
{
    /// <summary>
    /// La zona de una ciudad creada sin indicarla, y la de toda ciudad anterior a este campo: es la
    /// hora que imprimió toda acta emitida hasta entonces.
    /// </summary>
    public static CityTimeZone Default { get; } = new(TimeZoneInfo.FindSystemTimeZoneById("America/Santo_Domingo"));

    public string Id => Info.Id;

    public TimeZoneInfo Info { get; }

    private CityTimeZone(TimeZoneInfo info)
    {
        Info = info;
    }

    public static Result<CityTimeZone, BaseError> Create(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)
            || !TimeZoneInfo.TryFindSystemTimeZoneById(id, out var info)
            || !info.HasIanaId)
            return Result.Failure<CityTimeZone, BaseError>(EventErrors.TimeZoneUnknown());

        return Result.Success<CityTimeZone, BaseError>(new CityTimeZone(info));
    }

    /// <summary>
    /// La zona de una ciudad que se crea: la indicada, si es un id IANA conocido, o
    /// <see cref="Default"/> si no se indicó ninguna.
    /// </summary>
    /// <remarks>Una zona presente pero vacía no es una omisión y se rechaza.</remarks>
    public static Result<CityTimeZone, BaseError> CreateOrDefault(string? id) =>
        id is null ? Result.Success<CityTimeZone, BaseError>(Default) : Create(id);
}
