using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Notifications.Core.Sending;
using Sorteo360.Notifications.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Notifications.Tests.Integration.Sending;

/// <summary>
/// <b>El único test de punta a punta contra un Mailpit de verdad</b>, y lo único del conjunto que
/// prueba los <b>bytes</b>: un mensaje por persona, sin <c>Cc</c> y sin <c>Bcc</c>, las dos partes
/// MIME, el <c>X-SMTPAPI</c> verbatim y el <c>List-Unsubscribe</c> presente para quien está fuera y
/// ausente para quien está dentro.
/// <para>
/// Es uno solo a propósito. Mailpit acepta todo lo que se le mande, así que las reglas —el 4xx, el
/// 535, la hora, la supresión— no se pueden ejercitar contra él y viven en
/// <see cref="DrainWorkerTests"/>, contra el doble. Lo que ningún doble puede dar son los bytes, y
/// eso es exactamente lo que se comprueba acá.
/// </para>
/// </summary>
public sealed class MailpitEndToEndTests : IAsyncLifetime
{
    /// <summary>Credenciales de la UI de Mailpit. En stage son obligatorias y acá se comprueban.</summary>
    private const string UiUser = "sorteo360";
    private const string UiPassword = "correo-de-prueba";

    private readonly IContainer _mailpit = new ContainerBuilder("axllent/mailpit:v1.31")
        // Stage sirve la bandeja por HTTPS público a través de Traefik, así que sin esto se
        // publicaría a internet cada correo que se mandó desde stage.
        .WithEnvironment("MP_UI_AUTH", $"{UiUser}:{UiPassword}")
        .WithPortBinding(1025, assignRandomHostPort: true)
        .WithPortBinding(8025, assignRandomHostPort: true)
        // El mismo sondeo que usa el HEALTHCHECK de Compose: la imagen no trae curl, así que es el
        // propio binario el que consulta su /readyz.
        .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("/mailpit", "readyz"))
        .Build();

    private MailpitApiFactory _factory = null!;
    private HttpClient _api = null!;

    public async Task InitializeAsync()
    {
        await _mailpit.StartAsync();

        _factory = new MailpitApiFactory("localhost", _mailpit.GetMappedPublicPort(1025));
        await ((IAsyncLifetime)_factory).InitializeAsync();

        _api = new HttpClient { BaseAddress = new Uri($"http://localhost:{_mailpit.GetMappedPublicPort(8025)}") };
        _api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{UiUser}:{UiPassword}")));
    }

    public async Task DisposeAsync()
    {
        _api?.Dispose();

        if (_factory is not null)
            await ((IAsyncLifetime)_factory).DisposeAsync();

        await _mailpit.DisposeAsync();
    }

    [Fact]
    public async Task Send_ReturnsTheSpecifiedBytesInTheInbox_WhenSentForReal()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/notifications", new Dictionary<string, object?>
        {
            ["type"] = "events.quote.submitted",
            ["language"] = "es",
            ["fromAddress"] = "no-reply@sorteo360.local",
            ["fromName"] = "Sorteo360",
            ["subject"] = "Cotización de {{evento}}",
            ["body"] = "<html><body><p>Hola {{nombre}}, la cotización de {{evento}} está pendiente.</p></body></html>",
            ["sharedData"] = new Dictionary<string, string> { ["evento"] = "Feria de Madrid" },
            ["recipients"] = new[]
            {
                // De dentro: sin enlace de baja. No se puede dar de baja del correo de su trabajo.
                new Dictionary<string, object?>(Person("maria@sorteo360.local", ("nombre", "María")))
                {
                    ["name"] = "María González"
                },
                // De fuera: con enlace de baja.
                Person("juan@expedia.com", ("nombre", "Juan"), ("enlace_baja", "https://app.sorteo360.local/baja?t=abc"))
            },
            ["about"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["sourceService"] = "events",
                    ["entityType"] = "event",
                    ["entityId"] = "xyz-789"
                }
            },
            ["idempotencyKey"] = $"tests:mailpit:{Guid.NewGuid()}"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        await DrainAsync();

        var messages = (await Json("/api/v1/messages"))["messages"]!.AsArray();

        // Un mensaje por persona: sobre SMTP eso no es un caso especial, es lo nativo.
        messages.Count.ShouldBe(2);

        var toMaria = await Message(messages, "maria@sorteo360.local");
        var toJuan = await Message(messages, "juan@expedia.com");

        // Un destinatario por mensaje, y nadie en copia. Mailpit expone Cc y Bcc como campos, así
        // que la regla de privacidad se comprueba directamente y no se infiere del conteo.
        toMaria["To"]!.AsArray().Count.ShouldBe(1);
        toMaria["Cc"]!.AsArray().Count.ShouldBe(0);
        toMaria["Bcc"]!.AsArray().Count.ShouldBe(0);
        toJuan["Cc"]!.AsArray().Count.ShouldBe(0);
        toJuan["Bcc"]!.AsArray().Count.ShouldBe(0);

        // El nombre que mandó quien llama va en el To; sin nombre, la dirección va sola.
        toMaria["To"]!.AsArray().Single()!["Name"]!.GetValue<string>().ShouldBe("María González");
        toJuan["To"]!.AsArray().Single()!["Name"]!.GetValue<string>().ShouldBeEmpty();

        // El asunto salió sustituido y sin escapar: es una cabecera, no marcado.
        toMaria["Subject"]!.GetValue<string>().ShouldBe("Cotización de Feria de Madrid");

        // Las dos partes MIME, presentes y distintas. La de texto se derivó del HTML.
        var html = toMaria["HTML"]!.GetValue<string>();
        var text = toMaria["Text"]!.GetValue<string>();

        html.ShouldContain("<p>Hola María, la cotización de Feria de Madrid está pendiente.</p>");
        text.ShouldContain("Hola María, la cotización de Feria de Madrid está pendiente.");
        text.ShouldNotContain("<p>");
        text.ShouldNotBe(html);

        // El X-SMTPAPI verbatim: unique_args con el id opaco de la fila y nada más. No se puede
        // borrar una vez enviado, así que ahí no entra ningún id de negocio ni dato personal.
        var recipientId = await RecipientIdOf("maria@sorteo360.local");
        var headers = await Json($"/api/v1/message/{toMaria["ID"]!.GetValue<string>()}/headers");

        // Mailpit devuelve el nombre de la cabecera en su forma canónica ("X-Smtpapi"), no tal cual
        // se escribió. Da igual —los nombres de cabecera no distinguen mayúsculas— pero se busca
        // sin distinguirlas para que el test no dependa de cómo la normaliza el servidor.
        var smtpApi = headers.AsObject()
            .Single(header => header.Key.Equals("X-SMTPAPI", StringComparison.OrdinalIgnoreCase))
            .Value!.AsArray().Single()!.GetValue<string>();

        smtpApi.ShouldBe("{\"unique_args\":{\"recipient_id\":\"" + recipientId + "\"}}");

        // El List-Unsubscribe: presente para quien está fuera, ausente para quien está dentro.
        toJuan["ListUnsubscribe"]!["Header"]!.GetValue<string>()
            .ShouldBe("<https://app.sorteo360.local/baja?t=abc>");
        toMaria["ListUnsubscribe"]!["Header"]!.GetValue<string>().ShouldBeEmpty();
    }

    [Fact]
    public async Task MailpitInbox_Returns401_WhenCalledWithoutCredentials()
    {
        // MP_UI_AUTH medido, no supuesto: es lo que impide que la bandeja de stage quede pública.
        using var anonymous = new HttpClient { BaseAddress = _api.BaseAddress };

        var response = await anonymous.GetAsync("/api/v1/messages");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Y con ellas, 200.
        (await _api.GetAsync("/api/v1/messages")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task DrainAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var drain = scope.ServiceProvider.GetRequiredService<INotificationDrainService>();

        await drain.DrainAsync(CancellationToken.None);
    }

    /// <summary>El mensaje completo de una dirección, leído por la API de Mailpit.</summary>
    private async Task<JsonNode> Message(JsonArray messages, string email)
    {
        var summary = messages.Single(message =>
            message!["To"]!.AsArray().Any(to => to!["Address"]!.GetValue<string>() == email));

        return await Json($"/api/v1/message/{summary!["ID"]!.GetValue<string>()}");
    }

    private async Task<JsonNode> Json(string path)
    {
        var response = await _api.GetAsync(path);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonNode>())!;
    }

    /// <summary>El id opaco de la fila de historial de una dirección, que es lo que viaja al proveedor.</summary>
    private async Task<Guid> RecipientIdOf(string email)
    {
        var database = new NotificationEndpoints.NotificationsDatabase(_factory.ConnectionString);
        var rows = await database.RecipientIdsByEmailAsync(email);

        return rows.ShouldHaveSingleItem();
    }

    private static Dictionary<string, object?> Person(string email, params (string Key, string Value)[] data) =>
        new()
        {
            ["email"] = email,
            ["data"] = data.ToDictionary(pair => pair.Key, pair => pair.Value)
        };
}
