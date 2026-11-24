using System.Net.Http.Json;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.Infrastructure;

/// <summary>
/// La colección de los tests que miden tiempos, que corren solos.
/// <para>
/// xUnit corre cada clase en paralelo con las demás, y esta mide una carrera de milisegundos
/// contra la máquina: setecientos tests compitiendo por los mismos núcleos pueden retrasar una
/// petición lo bastante como para que llegue del lado equivocado del corte, y el test se pondría
/// rojo por la carga y no por el andamio —un test intermitente dentro del arreglo de una
/// intermitencia—. Sale barato: lo que corre en exclusiva son unos segundos.
/// </para>
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class StubTimingCollection
{
    public const string Name = "stub-timing";
}

/// <summary>
/// El andamio contándose a sí mismo: lo que el stub reporta es lo que mandó el test que corre, y
/// nada más.
/// <para>
/// Existe por una intermitencia real (PGTI-32): una petición que el vecino lento contesta tarde se
/// registraba después de que el test siguiente ya había limpiado el registro, y aparecía como un
/// aviso suyo. Contra ese fallo no protege ningún test de cotizaciones —los dos que lo sufrían
/// afirman cuántos avisos salieron, que es justo lo que la fuga falsea—, así que se prueba aquí.
/// </para>
/// </summary>
[Collection(StubTimingCollection.Name)]
public sealed class NotificationStubTests
{
    private const string NotificationsUrl = "v1/notifications";

    /// <summary>Lo que tarda en llegar la respuesta tardía. Corto: el test lo espera de verdad.</summary>
    private static readonly TimeSpan LateReply = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Lo que el cliente aguanta antes de rendirse. Muy por debajo de <see cref="LateReply"/>, y
    /// muy por encima de lo que tarda en salir una petición por una conexión ya abierta: lo que
    /// este test necesita es que la petición <b>llegue</b> antes de la línea que mueve el corte, y
    /// una máquina cargada no puede meter un segundo entre esas dos cosas.
    /// </summary>
    private static readonly TimeSpan ClientPatience = TimeSpan.FromSeconds(1);

    [Fact]
    public async Task ShouldHaveReceivedNothing_IgnoresTheRequest_WhenTheNeighbourAnsweredItLate()
    {
        using var stub = new NotificationStub();
        using var client = new HttpClient { BaseAddress = new Uri(stub.Url), Timeout = Timeout.InfiniteTimeSpan };

        // Primero uno que sí se contesta, por el mismo cliente: deja abierta la conexión que usará
        // el siguiente, que así sale al cable en el acto en vez de negociar una nueva mientras se
        // le acaba la paciencia.
        await PostAsync(client, "events:quote:warmup:attempt:1:submitted", CancellationToken.None);

        stub.GoSlow(LateReply);

        // El envío que un test de avería hace y abandona: el chasis se rinde por tiempo de espera
        // mientras el vecino sigue con la petición en la mano.
        using var patience = new CancellationTokenSource(ClientPatience);
        await Should.ThrowAsync<OperationCanceledException>(() =>
            PostAsync(client, "events:quote:abandoned:attempt:1:submitted", patience.Token));

        // Lo que hace el constructor del test siguiente, antes de mandar nada suyo.
        stub.ServeAccepted();
        stub.ResetRequestLog();

        // Y aquí es donde el vecino contesta por fin, ya con el registro limpio.
        await WaitUntilLoggedAsync(stub, count: 1);

        stub.ShouldHaveReceivedNothing();
        stub.SendKeys.ShouldBeEmpty();
    }

    /// <summary>
    /// Manda un aviso y se rinde cuando lo diga el testigo. Es <c>HttpClient</c> a pelo y no el de
    /// la factory —excepción declarada a §Testing de las convenciones— porque lo que se prueba acá
    /// es el andamio y no la aplicación: no hay servicio que levantar, y pasar por el chasis
    /// metería su cortacircuitos, que es justo una de las cosas que se filtran entre tests. La
    /// rendición es del testigo y no del <c>Timeout</c> del cliente para poder reusar el mismo
    /// cliente, que es lo que mantiene la conexión abierta entre una petición y la siguiente.
    /// </summary>
    private static Task PostAsync(HttpClient client, string idempotencyKey, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(NotificationsUrl, new { idempotencyKey }, cancellationToken);

    /// <summary>
    /// Espera a que el servidor haya registrado tantas peticiones, sin filtrar por el corte: hasta
    /// que la respuesta tardía no ha aterrizado no hay nada que afirmar, y dormir un rato fijo
    /// haría que el test pasara por no haber esperado lo suficiente.
    /// </summary>
    private static async Task WaitUntilLoggedAsync(NotificationStub stub, int count)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);

        while (stub.AllSendArrivals.Count < count)
        {
            DateTime.UtcNow.ShouldBeLessThan(deadline, "el vecino nunca contestó la petición tardía");
            await Task.Delay(50);
        }
    }
}
