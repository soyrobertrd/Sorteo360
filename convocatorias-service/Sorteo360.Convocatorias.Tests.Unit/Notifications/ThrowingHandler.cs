namespace Sorteo360.Convocatorias.Tests.Unit.Notifications;

/// <summary>
/// Un <see cref="HttpMessageHandler"/> que no llega a contestar: lanza lo que se le dé. Existe
/// para probar la traducción de la avería de transporte sin levantar un vecino de verdad.
/// </summary>
public sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        Task.FromException<HttpResponseMessage>(exception);
}
