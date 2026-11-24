using System.Text.Json;
using Shouldly;

namespace Sorteo360.Notifications.Tests.Unit.Realm;

/// <summary>
/// Lo que el realm JSON tiene que decir sobre notificaciones.
/// <para>
/// Riesgo residual, dicho en voz alta en vez de disimulado: ADR-0016 le da la propiedad del
/// realm a la consola después del sembrado, y ningún test ve un realm vivo. Esta suite
/// afirma el archivo, que es lo único afirmable, y prueba los entornos que se creen de acá
/// en adelante. Que <c>notifications:send</c> llegue a un compuesto en la consola de un
/// entorno ya corriendo es un fallo real e indetectable desde acá: por eso la regla está
/// escrita con palabras en ADR-0020 y como paso de consola en el runbook de despliegue.
/// </para>
/// </summary>
public sealed class RealmConfigurationTests
{
    private const string SendRole = "notifications:send";
    private const string ReadRole = "notifications:read";
    private const string AudienceClient = "notifications-api";
    private const string EventsServiceClient = "events-service";
    private const string ConvocatoriasServiceClient = "convocatorias-service";
    private const string FrontendClient = "sorteo360-app";
    private const string AudienceMapper = "audiencia-notifications-api";

    [Fact]
    public void RealmJson_ReturnsNoCompositeHoldingTheSendRole_WhenRead()
    {
        var realm = ReadRealm();

        var compositesHoldingSend = realm.RootElement
            .GetProperty("roles").GetProperty("realm").EnumerateArray()
            .Where(role => role.TryGetProperty("composite", out var composite) && composite.GetBoolean())
            .Where(role => role.GetProperty("composites").GetProperty("realm").EnumerateArray()
                .Any(member => member.GetString() == SendRole))
            .Select(role => role.GetProperty("name").GetString())
            .ToArray();

        compositesHoldingSend.ShouldBeEmpty(
            $"notifications:send nunca va en un compuesto. Lo llevan: {string.Join(", ", compositesHoldingSend)}");
    }

    [Fact]
    public void RealmJson_ReturnsBothFunctionRoles_WhenRead()
    {
        var realm = ReadRealm();

        var roles = RealmRoleNames(realm);

        roles.ShouldContain(SendRole);
        roles.ShouldContain(ReadRole);
    }

    [Fact]
    public void RealmJson_ReturnsTheReadRoleOnlyInCentralAdmin_WhenCompositesAreRead()
    {
        var realm = ReadRealm();

        var compositesHoldingRead = realm.RootElement
            .GetProperty("roles").GetProperty("realm").EnumerateArray()
            .Where(role => role.TryGetProperty("composite", out var composite) && composite.GetBoolean())
            .Where(role => role.GetProperty("composites").GetProperty("realm").EnumerateArray()
                .Any(member => member.GetString() == ReadRole))
            .Select(role => role.GetProperty("name").GetString())
            .ToArray();

        compositesHoldingRead.ShouldBe(["central_admin"]);
    }

    [Fact]
    public void RealmJson_ReturnsAnAudienceClientThatStartsNoLogin_WhenRead()
    {
        var realm = ReadRealm();

        var client = realm.RootElement.GetProperty("clients").EnumerateArray()
            .Single(item => item.GetProperty("clientId").GetString() == AudienceClient);

        client.GetProperty("publicClient").GetBoolean().ShouldBeFalse();
        client.GetProperty("standardFlowEnabled").GetBoolean().ShouldBeFalse();
        client.GetProperty("directAccessGrantsEnabled").GetBoolean().ShouldBeFalse();
        client.GetProperty("serviceAccountsEnabled").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public void RealmJson_ReturnsTheNotificationsAudienceMapper_WhenTheEventsServiceClientIsRead()
    {
        var realm = ReadRealm();

        var mapper = realm.RootElement.GetProperty("clients").EnumerateArray()
            .Single(item => item.GetProperty("clientId").GetString() == EventsServiceClient)
            .GetProperty("protocolMappers").EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == "audiencia-notifications-api");

        mapper.GetProperty("config").GetProperty("included.client.audience")
            .GetString().ShouldBe(AudienceClient);
        mapper.GetProperty("config").GetProperty("access.token.claim")
            .GetString().ShouldBe("true");
    }

    [Fact]
    public void RealmJson_ReturnsTheNotificationsAudienceMapper_WhenTheFrontendClientIsRead()
    {
        // Sin este mapper, quien tiene central_admin lleva notifications:read pero su token no
        // trae notifications-api en el aud, y la pantalla de historial responde 401 y no 403: el
        // servicio rechaza el token antes de mirar los roles y el log no dice por qué. Es el
        // mismo fallo sordo que ya documenta el runbook para convocatorias.
        var realm = ReadRealm();

        var mapper = realm.RootElement.GetProperty("clients").EnumerateArray()
            .Single(item => item.GetProperty("clientId").GetString() == FrontendClient)
            .GetProperty("protocolMappers").EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == AudienceMapper);

        mapper.GetProperty("config").GetProperty("included.client.audience")
            .GetString().ShouldBe(AudienceClient);
        mapper.GetProperty("config").GetProperty("access.token.claim")
            .GetString().ShouldBe("true");
    }

    [Fact]
    public void RealmJson_ReturnsTheAudienceMapperOnEveryCaller_WhenClientsAreRead()
    {
        // La lista es a mano a propósito: un cliente nuevo que llame a este servicio se agrega
        // acá y falla hasta que tenga su mapper, en vez de descubrirse como un 401 en stage.
        string[] callers = [EventsServiceClient, ConvocatoriasServiceClient, FrontendClient];
        var realm = ReadRealm();

        var missing = realm.RootElement.GetProperty("clients").EnumerateArray()
            .Where(client => callers.Contains(client.GetProperty("clientId").GetString()))
            .Where(client => !client.GetProperty("protocolMappers").EnumerateArray()
                .Any(mapper => mapper.GetProperty("name").GetString() == AudienceMapper))
            .Select(client => client.GetProperty("clientId").GetString())
            .ToArray();

        missing.ShouldBeEmpty(
            $"Sin el mapper de audiencia, sus tokens dan 401 contra este servicio: {string.Join(", ", missing)}");
    }

    [Fact]
    public void RealmJson_ReturnsSendAndRealmReadWithoutAdmin_WhenTheEventsServiceAccountIsRead()
    {
        var realm = ReadRealm();

        var serviceAccount = realm.RootElement.GetProperty("users").EnumerateArray()
            .Single(user => user.TryGetProperty("serviceAccountClientId", out var clientId)
                && clientId.GetString() == EventsServiceClient);

        serviceAccount.GetProperty("realmRoles").EnumerateArray()
            .Select(role => role.GetString())
            .ShouldContain(SendRole);

        var realmManagement = serviceAccount.GetProperty("clientRoles").GetProperty("realm-management")
            .EnumerateArray().Select(role => role.GetString()).ToArray();

        realmManagement.ShouldBe(["view-realm", "view-users"], ignoreOrder: true);
        realmManagement.ShouldNotContain("realm-admin");
    }

    [Fact]
    public void ConvocatoriasServiceClient_ReturnsTheNotificationsAudienceInItsToken_WhenTheRealmIsSeeded()
    {
        // Sin este mapper, con Notifications__Transport=http cada invitación y cada confirmación
        // recibe 401: el servicio rechaza el token antes de mirar los roles.
        var realm = ReadRealm();

        var mapper = realm.RootElement.GetProperty("clients").EnumerateArray()
            .Single(item => item.GetProperty("clientId").GetString() == ConvocatoriasServiceClient)
            .GetProperty("protocolMappers").EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == AudienceMapper);

        mapper.GetProperty("config").GetProperty("included.client.audience")
            .GetString().ShouldBe(AudienceClient);
        mapper.GetProperty("config").GetProperty("access.token.claim")
            .GetString().ShouldBe("true");
    }

    [Fact]
    public void ConvocatoriasServiceAccount_ReturnsTheSendRoleHeldDirectly_WhenTheRealmIsSeeded()
    {
        var realm = ReadRealm();

        var serviceAccount = realm.RootElement.GetProperty("users").EnumerateArray()
            .Single(user => user.TryGetProperty("serviceAccountClientId", out var clientId)
                && clientId.GetString() == ConvocatoriasServiceClient);

        serviceAccount.GetProperty("realmRoles").EnumerateArray()
            .Select(role => role.GetString())
            .ShouldContain(SendRole);

        // Nada de realm-management: convocatorias no resuelve roles de nadie.
        serviceAccount.TryGetProperty("clientRoles", out _).ShouldBeFalse();
    }

    [Fact]
    public void RealmJson_ReturnsNoHumanUserHoldingTheSendRole_WhenUsersAreRead()
    {
        var realm = ReadRealm();

        var composites = realm.RootElement
            .GetProperty("roles").GetProperty("realm").EnumerateArray()
            .Where(role => role.TryGetProperty("composite", out var composite) && composite.GetBoolean())
            .ToDictionary(
                role => role.GetProperty("name").GetString()!,
                role => role.GetProperty("composites").GetProperty("realm").EnumerateArray()
                    .Select(member => member.GetString()!).ToArray());

        var peopleHoldingSend = realm.RootElement.GetProperty("users").EnumerateArray()
            .Where(user => !user.TryGetProperty("serviceAccountClientId", out _))
            .Where(user => user.GetProperty("realmRoles").EnumerateArray()
                .Select(role => role.GetString()!)
                .Any(role => role == SendRole
                    || (composites.TryGetValue(role, out var members) && members.Contains(SendRole))))
            .Select(user => user.GetProperty("username").GetString())
            .ToArray();

        peopleHoldingSend.ShouldBeEmpty(
            $"notifications:send no le llega a una persona. Lo llevan: {string.Join(", ", peopleHoldingSend)}");
    }

    private static List<string?> RealmRoleNames(JsonDocument realm) =>
        [.. realm.RootElement.GetProperty("roles").GetProperty("realm").EnumerateArray()
            .Select(role => role.GetProperty("name").GetString())];

    private static JsonDocument ReadRealm() =>
        JsonDocument.Parse(File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "infra", "keycloak", "sorteo360-realm.json")));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "infra", "keycloak", "sorteo360-realm.json")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
