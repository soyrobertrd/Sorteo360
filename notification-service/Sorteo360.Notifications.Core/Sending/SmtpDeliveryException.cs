namespace Sorteo360.Notifications.Core.Sending;

/// <summary>
/// Una transacción SMTP que no terminó bien, con el código con el que el servidor la cerró.
/// <para>
/// Clasificar ese código en transitorio o terminal no es trabajo del transporte: lo hace el worker,
/// con la tabla de <see cref="SmtpFailureClassifier"/>. Un transporte que ya supiera la respuesta
/// dejaría la regla sin nada que probar.
/// </para>
/// </summary>
public sealed class SmtpDeliveryException : Exception
{
    public SmtpDeliveryException(int? statusCode, string response, Exception? innerException = null)
        : base(statusCode is null ? response : $"{statusCode} {response}", innerException)
    {
        StatusCode = statusCode;
        Response = response;
    }

    /// <summary>
    /// El código de respuesta SMTP, o null cuando no hubo ninguno: conexión rechazada, tiempo
    /// agotado, TLS caído. Sin código no hay nada que clasificar y se trata como transitorio.
    /// </summary>
    public int? StatusCode { get; }

    /// <summary>El texto que acompañó al código, tal como lo dijo el servidor.</summary>
    public string Response { get; }
}
