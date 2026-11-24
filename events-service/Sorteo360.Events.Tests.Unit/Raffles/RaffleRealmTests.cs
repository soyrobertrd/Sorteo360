using System.Text.Json;
using Sorteo360.Events.Web.Auth;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Raffles;

/// <summary>
/// Lo que el realm JSON tiene que decir sobre el sorteo: los dos roles existen y los llevan los
/// tres compuestos de personas. Los compuestos son listas planas —ninguno hereda del otro—, así
/// que un rol que falte en uno deja a ese puesto con <c>403</c>.
/// <para>
/// Como en notification-service, esto afirma el archivo y no un realm vivo (ADR-0016): un entorno
/// que ya corre recibe los roles por el paso de consola de <c>docs/deploy-with-dokploy.md</c>.
/// </para>
/// </summary>
public sealed class RaffleRealmTests
{
    public static TheoryData<string> PeopleComposites => ["opt_coordinator", "opt_director", "central_admin"];

    [Fact]
    public void RealmJson_ReturnsBothRaffleRoles_WhenRead()
    {
        var roles = RealmRoles(ReadRealm()).Select(role => role.GetProperty("name").GetString()).ToList();

        roles.ShouldContain(AuthorizationPolicies.RafflesWrite);
        roles.ShouldContain(AuthorizationPolicies.RafflesDraw);
    }

    [Theory]
    [MemberData(nameof(PeopleComposites))]
    public void RealmJson_ReturnsBothRaffleRolesInTheComposite_WhenRead(string composite)
    {
        var members = RealmRoles(ReadRealm())
            .Single(role => role.GetProperty("name").GetString() == composite)
            .GetProperty("composites").GetProperty("realm").EnumerateArray()
            .Select(member => member.GetString())
            .ToList();

        members.ShouldContain(AuthorizationPolicies.RafflesWrite);
        members.ShouldContain(AuthorizationPolicies.RafflesDraw);
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
