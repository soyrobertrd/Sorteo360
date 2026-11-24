using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Sorteo360.Events.Core.Accounts;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Infrastructure.Keycloak;

/// <summary>
/// Crea el usuario administrador de una cuenta nueva en Keycloak vía Admin API (§9.3 paso 3).
/// La contraseña viaja directo en el payload a Keycloak y nunca se persiste ni se escribe en logs.
/// </summary>
public sealed class KeycloakAccountProvisioner : IKeycloakAccountProvisioner
{
    private const string AccountAdminRoleName = "account_admin";

    private readonly HttpClient _httpClient;
    private readonly ILogger<KeycloakAccountProvisioner> _logger;

    public KeycloakAccountProvisioner(HttpClient httpClient, ILogger<KeycloakAccountProvisioner> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Result<Guid, BaseError>> CreateAccountAdminAsync(
        string email,
        string fullName,
        string password,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var (firstName, lastName) = SplitName(fullName);

        var createBody = new
        {
            username = email,
            email,
            emailVerified = true,
            enabled = true,
            firstName,
            lastName,
            attributes = new Dictionary<string, string[]>
            {
                ["account_id"] = [accountId.ToString("D")],
            },
            credentials = new[]
            {
                new
                {
                    type = "password",
                    value = password,
                    temporary = false,
                },
            },
        };

        try
        {
            using var createResp = await _httpClient.PostAsJsonAsync("users", createBody, cancellationToken);

            Guid? userId;
            if (createResp.StatusCode == HttpStatusCode.Conflict)
            {
                // R8: si el usuario ya existe en Keycloak para esta misma cuenta (reintento tras
                // un fallo de BD), reutilizarlo en vez de fallar con 409.
                var existing = await FindExistingUserByEmailAsync(email, cancellationToken);
                if (existing is null || existing.Value.AccountId != accountId)
                    return Result.Failure<Guid, BaseError>(AccountErrors.EmailTaken());

                userId = existing.Value.UserId;
            }
            else if (createResp.StatusCode == HttpStatusCode.BadRequest)
            {
                var detail = await TryReadErrorMessageAsync(createResp, cancellationToken);
                return Result.Failure<Guid, BaseError>(AccountErrors.PasswordPolicyRejected(detail));
            }
            else if (!createResp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Keycloak devolvió {StatusCode} al crear el usuario administrador de cuenta {AccountId}",
                    (int)createResp.StatusCode, accountId);
                return Result.Failure<Guid, BaseError>(IdentityProviderUnavailable());
            }
            else
            {
                userId = ExtractUserIdFromLocation(createResp.Headers.Location);
                if (userId is null)
                {
                    // R8: nunca inventar un Guid; buscar el usuario recién creado por correo.
                    var lookup = await FindExistingUserByEmailAsync(email, cancellationToken);
                    userId = lookup?.UserId;
                }
            }

            if (userId is null)
            {
                _logger.LogWarning("Keycloak creó el usuario para cuenta {AccountId} pero no fue posible resolver su identificador.", accountId);
                return Result.Failure<Guid, BaseError>(IdentityProviderUnavailable());
            }

            var roleAssigned = await AssignRoleAsync(userId.Value, AccountAdminRoleName, cancellationToken);
            if (!roleAssigned)
                return Result.Failure<Guid, BaseError>(IdentityProviderUnavailable());

            return Result.Success<Guid, BaseError>(userId.Value);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Keycloak no respondió al activar la cuenta {AccountId}", accountId);
            return Result.Failure<Guid, BaseError>(IdentityProviderUnavailable());
        }
    }

    private static BaseError IdentityProviderUnavailable() => new(
        "El servicio de identidad no está disponible en este momento.",
        "IDENTITY_PROVIDER_UNAVAILABLE",
        ErrorKind.Unavailable);

    private async Task<(Guid UserId, Guid? AccountId)?> FindExistingUserByEmailAsync(
        string email, CancellationToken cancellationToken)
    {
        using var resp = await _httpClient.GetAsync(
            $"users?email={Uri.EscapeDataString(email)}&exact=true", cancellationToken);
        if (!resp.IsSuccessStatusCode)
            return null;

        using var doc = await JsonDocument.ParseAsync(
            await resp.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out var idProp) || !Guid.TryParse(idProp.GetString(), out var parsedId))
                continue;

            Guid? parsedAccountId = null;
            if (item.TryGetProperty("attributes", out var attrs)
                && attrs.ValueKind == JsonValueKind.Object
                && attrs.TryGetProperty("account_id", out var accProp))
            {
                var rawAcc = accProp.ValueKind == JsonValueKind.Array && accProp.GetArrayLength() > 0
                    ? accProp[0].GetString()
                    : accProp.ValueKind == JsonValueKind.String
                        ? accProp.GetString()
                        : null;
                if (Guid.TryParse(rawAcc, out var accGuid))
                    parsedAccountId = accGuid;
            }

            return (parsedId, parsedAccountId);
        }

        return null;
    }

    private async Task<bool> AssignRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken)
    {
        using var roleResp = await _httpClient.GetAsync($"roles/{Uri.EscapeDataString(roleName)}", cancellationToken);
        if (!roleResp.IsSuccessStatusCode)
        {
            _logger.LogWarning("No se pudo obtener el rol {Role} en Keycloak ({StatusCode})", roleName, (int)roleResp.StatusCode);
            return false;
        }

        using var roleDoc = await JsonDocument.ParseAsync(await roleResp.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var roleId = roleDoc.RootElement.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(roleId))
            return false;

        var mapping = new[] { new { id = roleId, name = roleName } };
        using var assignResp = await _httpClient.PostAsJsonAsync(
            $"users/{userId:D}/role-mappings/realm", mapping, cancellationToken);
        if (!assignResp.IsSuccessStatusCode)
        {
            _logger.LogWarning("No se pudo asignar el rol {Role} al usuario {UserId} ({StatusCode})",
                roleName, userId, (int)assignResp.StatusCode);
            return false;
        }

        return true;
    }

    private static Guid? ExtractUserIdFromLocation(Uri? location)
    {
        if (location is null)
            return null;
        var segment = location.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        return Guid.TryParse(segment, out var id) ? id : null;
    }

    private static async Task<string> TryReadErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            if (doc.RootElement.TryGetProperty("errorMessage", out var msg) && msg.ValueKind == JsonValueKind.String)
                return msg.GetString() ?? string.Empty;
        }
        catch
        {
            // Ignorar errores de parseo del mensaje de error de Keycloak.
        }
        return string.Empty;
    }

    private static (string FirstName, string LastName) SplitName(string fullName)
    {
        var parts = fullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => ("Usuario", string.Empty),
            1 => (parts[0], string.Empty),
            _ => (parts[0], parts[1]),
        };
    }
}

