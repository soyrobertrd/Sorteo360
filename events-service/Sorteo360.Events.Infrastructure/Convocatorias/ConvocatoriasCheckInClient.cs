using System.Net.Http.Json;

namespace Sorteo360.Events.Infrastructure.Convocatorias;

/// <summary>
/// Cliente tipado de la lista de check-ins de convocatorias. El <c>HttpClient</c> lo arma el chasis
/// (<c>AddServiceClient</c>); acá solo viven la ruta y la forma de la respuesta. La ruta exige
/// <c>check-in:service-read</c>, que lleva la cuenta de servicio de este servicio.
/// </summary>
public sealed class ConvocatoriasCheckInClient : IConvocatoriasCheckInClient
{
    private readonly HttpClient _httpClient;

    public ConvocatoriasCheckInClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<ConvocatoriasCheckInList?> ListCheckInsAsync(Guid eventId, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync($"v1/convocatorias/events/{eventId}/check-ins", cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ConvocatoriasCheckInList>(cancellationToken);
    }
}
