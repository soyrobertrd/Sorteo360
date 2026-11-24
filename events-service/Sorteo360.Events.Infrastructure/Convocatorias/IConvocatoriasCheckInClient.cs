namespace Sorteo360.Events.Infrastructure.Convocatorias;

/// <summary>
/// La lista de check-ins de un evento, leída de convocatorias. Como los clientes del CRM, acá no hay
/// política, solo transporte.
/// <para>
/// Todo fallo sale como <see cref="HttpRequestException"/> —red, autenticación, cualquier estado que
/// no sea de éxito, <c>404</c> incluido, y el circuito abierto, que el chasis traduce a esa misma
/// excepción— o como <see cref="System.Text.Json.JsonException"/> si el cuerpo no se puede leer.
/// </para>
/// </summary>
public interface IConvocatoriasCheckInClient
{
    /// <summary>Todas las filas del evento. Devuelve <c>null</c> si convocatorias contestó <c>null</c>.</summary>
    Task<ConvocatoriasCheckInList?> ListCheckInsAsync(Guid eventId, CancellationToken cancellationToken);
}
