using System.Security.Cryptography;
using System.Text;
using Sorteo360.Notifications.Core.DTOs;

namespace Sorteo360.Notifications.Core.Notifications;

/// <summary>
/// La huella de una petición: un SHA-256 de todo lo que la define, que se guarda al lado de la
/// clave de idempotencia. Es lo que convierte "la misma clave con otro contenido" en un 409 en vez
/// de en un correo que nadie recibió y de un fallo que nadie vio.
/// </summary>
public static class NotificationFingerprint
{
    /// <summary>Separa dos campos dentro de un registro (unit separator).</summary>
    private const char FieldSeparator = '\u001F';

    /// <summary>Separa dos registros: una persona, una entidad, un par clave-valor.</summary>
    private const char RecordSeparator = '\u001E';

    /// <summary>
    /// Calcula la huella. La forma canónica ordena las claves de los diccionarios, porque el orden
    /// de las claves de un objeto JSON no significa nada, y conserva el orden de las listas, que sí
    /// significa. Los separadores son caracteres de control: no aparecen en una celda de hoja de
    /// cálculo ni en un cuerpo HTML, así que dos peticiones distintas no pueden compartir huella
    /// porque un valor lleve el separador dentro.
    /// </summary>
    public static string Compute(SendNotificationDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var canonical = new StringBuilder();

        Append(canonical, dto.Type);
        Append(canonical, dto.Language);
        Append(canonical, dto.FromAddress);
        Append(canonical, dto.FromName ?? string.Empty);
        Append(canonical, dto.Subject);
        Append(canonical, dto.Body);
        Append(canonical, dto.IdempotencyKey);

        AppendData(canonical, dto.SharedData);

        foreach (var recipient in dto.Recipients)
        {
            canonical.Append(RecordSeparator);
            Append(canonical, recipient.Email);

            // El nombre entra solo cuando viene, así que la huella de un envío sin nombres es la
            // misma que antes de que el contrato lo tuviera. No se confunde con un valor de data:
            // el nombre va antes de cualquier separador de registro y el par clave-valor, después.
            if (recipient.Name is not null)
                Append(canonical, recipient.Name);

            AppendData(canonical, recipient.Data);
        }

        foreach (var about in dto.About)
        {
            canonical.Append(RecordSeparator);
            Append(canonical, about.SourceService);
            Append(canonical, about.EntityType);
            Append(canonical, about.EntityId);
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));

        return Convert.ToHexStringLower(hash);
    }

    /// <summary>Agrega un campo y su separador.</summary>
    private static void Append(StringBuilder canonical, string value) =>
        canonical.Append(value).Append(FieldSeparator);

    /// <summary>Agrega un diccionario con las claves ordenadas.</summary>
    private static void AppendData(StringBuilder canonical, IReadOnlyDictionary<string, string> data)
    {
        foreach (var key in data.Keys.OrderBy(key => key, StringComparer.Ordinal))
        {
            canonical.Append(RecordSeparator);
            Append(canonical, key);
            Append(canonical, data[key]);
        }
    }
}
