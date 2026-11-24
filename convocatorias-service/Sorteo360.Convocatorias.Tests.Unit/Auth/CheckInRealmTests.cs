using System.Text.Json;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Auth;

/// <summary>
/// Lo que el realm JSON tiene que decir sobre la puerta: los dos roles existen y los llevan los
/// tres compuestos de personas y <c>event_staff</c>, y el de la rifa solo lo lleva events-service. Los compuestos son listas planas —ninguno hereda
/// del otro—, así que un rol que falte en uno deja a ese puesto con <c>403</c> en la puerta.
/// <para>
/// Como en events-service, esto afirma el archivo y no un realm vivo (ADR-0016): un entorno que ya
/// corre recibe el rol por el paso de consola de <c>docs/deploy-with-dokploy.md</c>.
/// </para>
/// </summary>
public sealed class CheckInRealmTests
{
    /// <summary>
    /// <c>AuthorizationPolicies.CheckInWrite</c>, escrito a mano: este proyecto no referencia Web.
    /// Los tests de <c>CheckInWriteEndpointsTests</c> usan la constante, así que un cambio de nombre
    /// sin el realm falla allá o acá.
    /// </summary>
    private const string CheckInWrite = "check-in:write";

    /// <summary><c>AuthorizationPolicies.CheckInRead</c>, escrito a mano por lo mismo.</summary>
    private const string CheckInRead = "check-in:read";

    /// <summary><c>AuthorizationPolicies.CheckInServiceRead</c>, escrito a mano por lo mismo.</summary>
    private const string CheckInServiceRead = "check-in:service-read";

    public static TheoryData<string> CheckInRoles => [CheckInWrite, CheckInRead];

    public static TheoryData<string, string> CheckInRolesByPeopleComposite => new()
    {
        { CheckInWrite, "opt_coordinator" },
        { CheckInWrite, "opt_director" },
        { CheckInWrite, "central_admin" },
        { CheckInRead, "opt_coordinator" },
        { CheckInRead, "opt_director" },
        { CheckInRead, "central_admin" },
        { CheckInWrite, "event_staff" },
        { CheckInRead, "event_staff" },
    };

    [Theory]
    [MemberData(nameof(CheckInRoles))]
    public void RealmJson_ReturnsTheCheckInRole_WhenRead(string checkInRole)
    {
        var roles = RealmRoles(ReadRealm()).Select(role => role.GetProperty("name").GetString()).ToList();

        roles.ShouldContain(checkInRole);
    }

    [Theory]
    [MemberData(nameof(CheckInRolesByPeopleComposite))]
    public void RealmJson_ReturnsTheCheckInRoleInTheComposite_WhenRead(string checkInRole, string composite)
    {
        var members = RealmRoles(ReadRealm())
            .Single(role => role.GetProperty("name").GetString() == composite)
            .GetProperty("composites").GetProperty("realm").EnumerateArray()
            .Select(member => member.GetString())
            .ToList();

        members.ShouldContain(checkInRole);
    }

    [Theory]
    [MemberData(nameof(CheckInRoles))]
    public void RealmJson_ReturnsOnlyThePeopleCompositesAndEventStaffHoldingTheCheckInRole_WhenRead(string checkInRole)
    {
        var holders = RealmRoles(ReadRealm())
            .Where(role => role.TryGetProperty("composites", out var composites)
                && composites.TryGetProperty("realm", out var realm)
                && realm.EnumerateArray().Any(member => member.GetString() == checkInRole))
            .Select(role => role.GetProperty("name").GetString())
            .ToList();

        // `account_admin` (el administrador de una cuenta, plans/sorteo360.md §9.3) recibe todos los
        // permisos de la plataforma dentro de su cuenta, también los de la puerta.
        holders.ShouldBe(
            ["opt_coordinator", "opt_director", "central_admin", "event_staff", "account_admin"],
            ignoreOrder: true);
    }

    /// <summary>
    /// La puerta y nada más: ni <c>platform:staff</c> ni ningún otro rol (ADR-0025). Un rol de más
    /// abre a quien está en la puerta lo que ese rol abra en toda la plataforma.
    /// </summary>
    [Fact]
    public void RealmJson_ReturnsExactlyTheTwoCheckInRolesInEventStaff_WhenRead()
    {
        var composites = RealmRoles(ReadRealm())
            .Single(role => role.GetProperty("name").GetString() == "event_staff")
            .GetProperty("composites");
        var members = composites.GetProperty("realm").EnumerateArray()
            .Select(member => member.GetString())
            .ToList();

        members.ShouldBe([CheckInWrite, CheckInRead], ignoreOrder: true);
        composites.TryGetProperty("client", out _).ShouldBeFalse();
    }

    /// <summary>
    /// Ningún compuesto contiene a <c>event_staff</c>: si uno lo hiciera, la puerta dejaría de ser un
    /// puesto aparte y quitarle el compuesto a alguien dejaría de quitarle la puerta.
    /// </summary>
    [Fact]
    public void RealmJson_ReturnsNoCompositeContainingEventStaff_WhenRead()
    {
        var containers = RealmRoles(ReadRealm())
            .Where(role => role.TryGetProperty("composites", out var composites)
                && composites.TryGetProperty("realm", out var realm)
                && realm.EnumerateArray().Any(member => member.GetString() == "event_staff"))
            .Select(role => role.GetProperty("name").GetString())
            .ToList();

        containers.ShouldBeEmpty();
    }

    /// <summary>
    /// La lista de la rifa (spec <c>09</c> §7, §8.3): la lleva la cuenta de servicio de events-service,
    /// asignada directo, y ningún compuesto. Si un compuesto la llevara, una persona leería los correos
    /// de todos los que entraron.
    /// </summary>
    [Fact]
    public void RealmJson_ReturnsCheckInServiceReadOnlyOnEventsServicesServiceAccount_WhenRead()
    {
        var realm = ReadRealm();

        RealmRoles(realm).Select(role => role.GetProperty("name").GetString()).ShouldContain(CheckInServiceRead);
        RealmRoles(realm)
            .Where(role => role.TryGetProperty("composites", out var composites)
                && composites.TryGetProperty("realm", out var members)
                && members.EnumerateArray().Any(member => member.GetString() == CheckInServiceRead))
            .ShouldBeEmpty();
        realm.RootElement.GetProperty("users").EnumerateArray()
            .Where(user => user.TryGetProperty("realmRoles", out var roles)
                && roles.EnumerateArray().Any(role => role.GetString() == CheckInServiceRead))
            .Select(user => user.GetProperty("username").GetString())
            .ShouldBe(["service-account-events-service"]);
    }

    /// <summary>
    /// El token de events-service tiene que llevar <c>convocatorias-api</c> en <c>aud</c>, o esta API lo
    /// rechaza con <c>401</c> antes de mirar el rol (spec <c>09</c> §8.3 paso 3).
    /// </summary>
    [Fact]
    public void RealmJson_ReturnsTheConvocatoriasAudienceOnTheEventsServiceClient_WhenRead()
    {
        var audiences = ReadRealm().RootElement.GetProperty("clients").EnumerateArray()
            .Single(client => client.GetProperty("clientId").GetString() == "events-service")
            .GetProperty("protocolMappers").EnumerateArray()
            .Where(mapper => mapper.GetProperty("protocolMapper").GetString() == "oidc-audience-mapper")
            .Select(mapper => mapper.GetProperty("config").GetProperty("included.client.audience").GetString());

        audiences.ShouldContain("convocatorias-api");
    }

    private static IEnumerable<JsonElement> RealmRoles(JsonDocument realm) =>
        realm.RootElement.GetProperty("roles").GetProperty("realm").EnumerateArray();

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
