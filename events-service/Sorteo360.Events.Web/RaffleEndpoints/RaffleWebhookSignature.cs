using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>
/// Autenticación de webhooks entrantes para padrón y check-in del sorteo:
/// clave compartida por evento o global en tiempo constante, más firma HMAC-SHA256
/// opcional (<c>t=&lt;epoch&gt;,v1=&lt;hex&gt;</c>) con tolerancia de 5 minutos.
/// </summary>
public static class RaffleWebhookSignature
{
    public const string KeyHeader = "X-Webhook-Key";
    public const string LegacySecretHeader = "X-Webhook-Secret";
    public const string ApiKeyHeader = "API-key";
    public const string SignatureHeader = "X-Webhook-Signature";
    public const string CallbackSignatureHeader = "X-Callback-Signature";

    /// <summary>Ventana máxima permitida entre la firma del webhook y la recepción en el servidor.</summary>
    public static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(5);

    /// <summary>Tope de 1 MB para el cuerpo crudo recibido por webhook.</summary>
    public const int MaxBodyBytes = 1024 * 1024;

    /// <summary>Obtiene la clave esperada para un evento, o la clave global si no hay una específica.</summary>
    public static string? ResolveApiKey(IConfiguration configuration, Guid eventId)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return FirstNonEmpty(
            configuration[$"RaffleWebhooks:Events:{eventId}:ApiKey"],
            configuration["RaffleWebhooks:ApiKey"],
            configuration["Webhooks:Secret"]);
    }

    /// <summary>Obtiene el secreto HMAC esperado para un evento, o el secreto global.</summary>
    public static string? ResolveSigningSecret(IConfiguration configuration, Guid eventId)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return FirstNonEmpty(
            configuration[$"RaffleWebhooks:Events:{eventId}:SigningSecret"],
            configuration["RaffleWebhooks:SigningSecret"]);
    }

    /// <summary>Lee la clave enviada en cualquiera de las cabeceras aceptadas.</summary>
    public static string ReadPresentedKey(IHeaderDictionary headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        if (headers.TryGetValue(KeyHeader, out var key) && !string.IsNullOrWhiteSpace(key))
            return key.ToString();
        if (headers.TryGetValue(ApiKeyHeader, out var apiKey) && !string.IsNullOrWhiteSpace(apiKey))
            return apiKey.ToString();
        if (headers.TryGetValue(LegacySecretHeader, out var legacy) && !string.IsNullOrWhiteSpace(legacy))
            return legacy.ToString();
        return string.Empty;
    }

    /// <summary>Lee la cabecera de firma HMAC si viene presente.</summary>
    public static string ReadPresentedSignature(IHeaderDictionary headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        if (headers.TryGetValue(SignatureHeader, out var sig) && !string.IsNullOrWhiteSpace(sig))
            return sig.ToString();
        if (headers.TryGetValue(CallbackSignatureHeader, out var cbSig) && !string.IsNullOrWhiteSpace(cbSig))
            return cbSig.ToString();
        return string.Empty;
    }

    /// <summary>Compara en tiempo constante las dos claves mediante su hash SHA-256.</summary>
    public static bool KeyMatches(string? configured, string? presented)
    {
        if (string.IsNullOrEmpty(configured) || string.IsNullOrEmpty(presented))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(configured)),
            SHA256.HashData(Encoding.UTF8.GetBytes(presented)));
    }

    /// <summary>
    /// Valida la firma <c>t=&lt;epoch&gt;,v1=&lt;hex&gt;</c> como HMAC-SHA256 sobre <c>"{t}.{rawBody}"</c>
    /// dentro de la ventana de 5 minutos.
    /// </summary>
    public static bool SignatureMatches(string? secret, string? header, ReadOnlySpan<byte> body, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(header))
            return false;

        string? timestamp = null;
        var candidates = new List<string>(2);

        foreach (var part in header.Split(',', StringSplitOptions.TrimEntries))
        {
            var separator = part.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
                continue;

            var name = part[..separator];
            var value = part[(separator + 1)..];

            if (name == "t")
            {
                if (timestamp is not null)
                    return false;
                timestamp = value;
            }
            else if (name == "v1")
            {
                candidates.Add(value);
            }
        }

        if (timestamp is null
            || candidates.Count == 0
            || !long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            || seconds > DateTimeOffset.MaxValue.ToUnixTimeSeconds())
        {
            return false;
        }

        var signedAt = DateTimeOffset.FromUnixTimeSeconds(seconds);
        if ((now - signedAt).Duration() > Tolerance)
            return false;

        var prefix = Encoding.ASCII.GetBytes(timestamp + ".");
        var signed = new byte[prefix.Length + body.Length];
        prefix.CopyTo(signed, 0);
        body.CopyTo(signed.AsSpan(prefix.Length));
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), signed);

        var matched = false;
        foreach (var candidate in candidates)
            matched |= CryptographicOperations.FixedTimeEquals(expected, FromHex(candidate));

        return matched;
    }

    private static byte[] FromHex(string value)
    {
        try
        {
            return Convert.FromHexString(value);
        }
        catch (FormatException)
        {
            return [];
        }
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}

