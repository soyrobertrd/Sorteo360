using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Sorteo360.Platform.Auth;

/// <summary>
/// Autenticación opcional del chasis: la validación de JWT emitidos por Keycloak.
/// Se registra aparte de <c>AddWebServices</c> porque no todo servicio protege sus
/// endpoints — el que no llama a <see cref="AddKeycloakAuthentication"/> no registra
/// esquema alguno y tampoco necesita las variables <c>Keycloak__*</c> en su entorno.
/// Las políticas de autorización y los accesores de identidad siguen siendo de cada
/// servicio: sus roles funcionales y sus claims son dominio, no chasis.
/// </summary>
public static class KeycloakAuthenticationExtensions
{
    /// <summary>
    /// Entorno en el que la factory de tests de integración registra su propio esquema
    /// como predeterminado, así que aquí no se exige configuración de Keycloak.
    /// </summary>
    private const string IntegrationTestingEnvironment = "IntegrationTesting";

    /// <summary>
    /// Registra la validación de JWT emitidos por Keycloak. Los roles
    /// llegan en el claim plano <c>roles</c> (mapper del realm pgti360) y el id
    /// del usuario en <c>sub</c>. Falla al arrancar si falta la configuración, salvo
    /// en el entorno de tests de integración.
    /// </summary>
    /// <param name="configuration">Configuración del host, de donde se lee la sección <c>Keycloak</c>.</param>
    /// <param name="environment">Entorno de ejecución; decide si la configuración es obligatoria.</param>
    public static IServiceCollection AddKeycloakAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var settings = configuration.GetSection(KeycloakSettings.SectionName).Get<KeycloakSettings>()
            ?? new KeycloakSettings();
        var authMode = configuration["Auth:Mode"];
        var localDevRequested = string.Equals(authMode, "LocalDev", StringComparison.OrdinalIgnoreCase);

        if (localDevRequested)
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "El modo de autenticación 'Auth:Mode=LocalDev' solo se permite en el entorno Development.");
            }

            services.AddAuthentication(LocalDevAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, LocalDevAuthenticationHandler>(
                    LocalDevAuthenticationHandler.SchemeName,
                    _ => { });
            return services;
        }

        if (!environment.IsEnvironment(IntegrationTestingEnvironment)
            && (string.IsNullOrWhiteSpace(settings.Authority) || string.IsNullOrWhiteSpace(settings.Audience)))
        {
            throw new InvalidOperationException(
                "Falta la configuración de Keycloak: se requieren 'Keycloak:Authority' y 'Keycloak:Audience'.");
        }

        var validIssuers = new List<string> { settings.Authority };
        if (!string.IsNullOrWhiteSpace(settings.AdditionalValidIssuer))
        {
            validIssuers.Add(settings.AdditionalValidIssuer);
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = settings.Authority;
                options.RequireHttpsMetadata = settings.RequireHttpsMetadata;
                // Conservar los claims tal como vienen en el token (sub,
                // preferred_username, roles, y los que añada cada servicio),
                // sin el mapeo legado a URIs.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidAudience = settings.Audience,
                    ValidIssuers = validIssuers,
                    NameClaimType = "preferred_username",
                    RoleClaimType = "roles",
                };
            });

        return services;
    }

    private sealed class LocalDevAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "LocalDev";

        public LocalDevAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // R3: nunca autenticar peticiones anónimas sin cabecera explícita.
            var hasAuthHeader = Request.Headers.TryGetValue("Authorization", out var authHeader)
                && !string.IsNullOrWhiteSpace(authHeader.ToString());
            var hasDevUserHeader = Request.Headers.TryGetValue("X-LocalDev-User", out var devUserHeader)
                && !string.IsNullOrWhiteSpace(devUserHeader.ToString());

            if (!hasAuthHeader && !hasDevUserHeader)
                return Task.FromResult(AuthenticateResult.NoResult());

            var accountId = Request.Headers.TryGetValue("X-LocalDev-Account-Id", out var accountHeader)
                && Guid.TryParse(accountHeader.ToString(), out var parsedAccountId)
                ? parsedAccountId.ToString()
                : "00000000-0000-0000-0000-000000000001";

            var claims = new List<Claim>
            {
                new("sub", "00000000-0000-0000-0000-000000000001"),
                new("preferred_username", hasDevUserHeader ? devUserHeader.ToString() : "admin@sorteo360.local"),
                new("name", "Administrador Local"),
                new("account_id", accountId),
                new("roles", "platform:staff"),
                new("roles", "events:write"),
                new("roles", "events:start"),
                new("roles", "events:cancel"),
                new("roles", "events:close"),
                new("roles", "raffles:write"),
                new("roles", "raffles:draw"),
                new("roles", "feature-switches:write"),
                new("roles", "platform-admin"),
                new("roles", "campaigns:write"),
                new("roles", "campaigns:launch"),
                new("roles", "convocatorias:read"),
                new("roles", "check-in:write"),
                new("roles", "check-in:read"),
                new("roles", "notifications:read"),
                new("roles", "central_admin"),
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name, "preferred_username", "roles");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
