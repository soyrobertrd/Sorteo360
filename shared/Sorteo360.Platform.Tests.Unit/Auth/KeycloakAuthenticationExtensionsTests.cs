using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sorteo360.Platform.Auth;
using NSubstitute;
using Shouldly;

namespace Sorteo360.Platform.Tests.Unit.Auth;

/// <summary>
/// Autenticación opcional del chasis. Cubre las dos decisiones que un servicio no
/// puede revisar a ojo: que el arranque falle rápido si falta la configuración de
/// Keycloak (salvo en los tests de integración, donde la factory pone su propio
/// esquema), y que el esquema quede validando la audiencia, los issuers y los claims
/// tal como vienen en el token.
/// </summary>
public sealed class KeycloakAuthenticationExtensionsTests
{
    private const string Authority = "https://keycloak.pgti360.test/realms/pgti360";
    private const string Audience = "events-api";
    private const string PublicIssuer = "https://localhost:8082/realms/pgti360";

    /// <summary>Configuración con solo las claves de la sección <c>Keycloak</c> que se indican.</summary>
    private static IConfiguration CreateConfiguration(string? authority = null, string? audience = null, string? additionalValidIssuer = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Keycloak:Authority"] = authority,
                ["Keycloak:Audience"] = audience,
                ["Keycloak:AdditionalValidIssuer"] = additionalValidIssuer,
            })
            .Build();

    private static IWebHostEnvironment CreateEnvironment(string environmentName)
    {
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        return environment;
    }

    /// <summary>Opciones con las que quedó configurado el esquema JWT registrado.</summary>
    private static JwtBearerOptions RegisteredOptions(IServiceCollection services) =>
        services.BuildServiceProvider()
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

    #region La configuración es obligatoria fuera de los tests de integración

    [Theory]
    [InlineData(null, Audience)]
    [InlineData(Authority, null)]
    [InlineData(" ", Audience)]
    [InlineData(Authority, " ")]
    public void AddKeycloakAuthentication_Throws_WhenTheAuthorityOrTheAudienceIsMissing(string? authority, string? audience)
    {
        var services = new ServiceCollection();

        Should.Throw<InvalidOperationException>(() => services.AddKeycloakAuthentication(
            CreateConfiguration(authority, audience),
            CreateEnvironment("Production")));
    }

    [Fact]
    public void AddKeycloakAuthentication_DoesNotThrow_WhenTheEnvironmentIsIntegrationTesting()
    {
        var services = new ServiceCollection();

        Should.NotThrow(() => services.AddKeycloakAuthentication(
            CreateConfiguration(),
            CreateEnvironment("IntegrationTesting")));
    }

    #endregion

    #region El esquema queda validando lo que emite el realm

    [Fact]
    public void AddKeycloakAuthentication_SetsTheValidAudience_WhenAnAudienceIsConfigured()
    {
        var services = new ServiceCollection();

        services.AddKeycloakAuthentication(CreateConfiguration(Authority, Audience), CreateEnvironment("Production"));

        RegisteredOptions(services).TokenValidationParameters.ValidAudience.ShouldBe(Audience);
    }

    [Fact]
    public void AddKeycloakAuthentication_AcceptsOnlyTheAuthority_WhenNoAdditionalIssuerIsConfigured()
    {
        var services = new ServiceCollection();

        services.AddKeycloakAuthentication(CreateConfiguration(Authority, Audience), CreateEnvironment("Production"));

        RegisteredOptions(services).TokenValidationParameters.ValidIssuers.ShouldBe([Authority]);
    }

    [Fact]
    public void AddKeycloakAuthentication_AlsoAcceptsTheAdditionalIssuer_WhenItIsConfigured()
    {
        var services = new ServiceCollection();

        services.AddKeycloakAuthentication(
            CreateConfiguration(Authority, Audience, PublicIssuer),
            CreateEnvironment("Production"));

        RegisteredOptions(services).TokenValidationParameters.ValidIssuers.ShouldBe([Authority, PublicIssuer], ignoreOrder: true);
    }

    /// <summary>
    /// Los claims conservan el nombre con el que viajan en el token (<c>sub</c>,
    /// <c>preferred_username</c>, <c>roles</c>): sin esto las políticas por rol no
    /// encuentran el claim y todo endpoint protegido responde 403.
    /// </summary>
    [Fact]
    public void AddKeycloakAuthentication_KeepsTheTokenClaimNames_WhenAuthorityAndAudienceAreConfigured()
    {
        var services = new ServiceCollection();

        services.AddKeycloakAuthentication(CreateConfiguration(Authority, Audience), CreateEnvironment("Production"));

        var options = RegisteredOptions(services);

        options.MapInboundClaims.ShouldBeFalse();
        options.TokenValidationParameters.NameClaimType.ShouldBe("preferred_username");
        options.TokenValidationParameters.RoleClaimType.ShouldBe("roles");
    }

    #endregion
}
