using System.Text.Encodings.Web;
using System.Text.Json;

namespace Sorteo360.Notifications.Infrastructure.Database;

/// <summary>
/// Entidad de persistencia de la tabla <c>notifications.notification_payload</c>: los valores de
/// sustitución de una persona, guardados solo mientras su envío sigue vivo.
/// <para>
/// Es la única tabla del servicio que contiene un valor de <c>data</c>, y por eso es la única que
/// se borra sola: la fila desaparece en la misma transacción en que el envío llega a un estado
/// terminal. Las cuatro tablas del registro no la contienen nunca.
/// </para>
/// </summary>
internal sealed class NotificationPayloadData
{
    /// <summary>
    /// Cómo se escriben y se leen los valores. Sin escapes de más: la columna es <c>jsonb</c> y la
    /// copia es toda en español, así que convertir cada tilde llenaría de ruido algo que nadie lee
    /// en crudo salvo depurando.
    /// </summary>
    internal static readonly JsonSerializerOptions Json = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public Guid RecipientId { get; set; }

    /// <summary>Los valores en JSON, planos: clave a texto.</summary>
    public string RenderValues { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
