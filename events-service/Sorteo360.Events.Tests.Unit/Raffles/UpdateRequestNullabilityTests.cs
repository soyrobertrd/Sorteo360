using Sorteo360.Events.Web.RaffleEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Raffles;

/// <summary>
/// ADR-0009 regla 2: todo tipo de valor en un request de edición es anulable, y cuáles son
/// obligatorios lo decide el validador. Ningún test de un solo campo puede verlo —un <c>bool</c>
/// no anulable omitido llega como <c>false</c> y pasa en silencio—, así que se comprueba sobre el
/// tipo. Cada request de edición del sorteo agrega su línea a <see cref="UpdateRequests"/>.
/// </summary>
public sealed class UpdateRequestNullabilityTests
{
    public static TheoryData<Type> UpdateRequests =>
    [
        typeof(UpdateRaffleRequest),
        typeof(UpdateRaffleRoundRequest),
        typeof(UpdatePrizeRequest),
    ];

    [Theory]
    [MemberData(nameof(UpdateRequests))]
    public void GetProperties_ReturnsOnlyNullableValueTypes_WhenTheTypeIsAnUpdateRequest(Type requestType)
    {
        var nonNullable = requestType.GetProperties()
            .Where(p => p.PropertyType.IsValueType && Nullable.GetUnderlyingType(p.PropertyType) is null)
            .Select(p => p.Name)
            .ToList();

        nonNullable.ShouldBeEmpty();
    }
}
